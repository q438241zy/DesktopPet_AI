using Microsoft.Win32;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopPet.Core;

namespace DesktopPet.App;

public sealed partial class PetWindow
{
    internal AgendaStore Agenda { get; private set; } = null!;
    private DispatcherTimer? agendaClock;
    private Window? agendaNotice;
    private AgendaEntry? agendaShowing;
    private bool agendaPreview, closingAgendaNotice;
    private DateTimeOffset agendaRetry;
    internal string? AgendaError { get; private set; }
    internal Func<DateTimeOffset> AgendaNow { get; set; } = () => DateTimeOffset.UtcNow;
    internal Window? AgendaNotice => agendaNotice;
    private void InitializeAgenda()
    {
        Agenda = new AgendaStore(App.DataRoot); AgendaError = Agenda.LoadError;
        // This timer never depends on visibility, frame rendering, wander, or the work-mode clock.
        agendaClock = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        agendaClock.Tick += (_, _) => PollAgenda();
        Loaded += (_, _) => { agendaClock.Start(); PollAgenda(); };
        SystemEvents.PowerModeChanged += AgendaPowerChanged; SystemEvents.TimeChanged += AgendaTimeChanged;
        Closed += (_, _) =>
        {
            agendaClock.Stop(); SystemEvents.PowerModeChanged -= AgendaPowerChanged; SystemEvents.TimeChanged -= AgendaTimeChanged;
            CloseAgendaNotice();
        };
    }
    private void AgendaPowerChanged(object sender, PowerModeChangedEventArgs e)
    { if (e.Mode == PowerModes.Resume) QueueAgendaCheck(); }
    private void AgendaTimeChanged(object? sender, EventArgs e) => QueueAgendaCheck();
    private void QueueAgendaCheck()
    {
        if (Dispatcher.HasShutdownStarted) return;
        Dispatcher.BeginInvoke(() => { if (!closing) { TimeZoneInfo.ClearCachedData(); agendaRetry = default; PollAgenda(); settings?.RefreshAgenda(); } });
    }
    internal void OpenAgenda()
    { OpenSettings(); settings!.ShowAgenda(); }
    internal void AgendaChanged()
    { AgendaError = Agenda.LoadError; agendaRetry = default; settings?.RefreshAgenda(); PollAgenda(); }
    internal void EditAgenda(AgendaEntry e)
    {
        if (!e.Active) return;
        if (!IsVisible) ToggleVisible();
        SelectCharacter(Catalog.VariantId(e.Family, Character.Category)); OpenChat(); Chat.ShowAgendaDraft(PetAgenda.Edit(e), e.Id);
    }
    internal void PollAgenda()
    {
        if (closing || Agenda.LoadError is not null || AgendaNow() < agendaRetry) return;
        try
        {
            if (agendaShowing is { } shown && !agendaPreview && !Agenda.Events.Any(e => e.Id == shown.Id && e == shown)) CloseAgendaNotice();
            var next = Agenda.Due(AgendaNow()).FirstOrDefault();
            if (next is null || agendaNotice is not null && !agendaPreview) return;
            if (agendaPreview) CloseAgendaNotice();
            if (next.Status == "scheduled")
            {
                Agenda.Deliver(next, AgendaNow()); next = Agenda.Events.Single(e => e.Id == next.Id); settings?.RefreshAgenda();
            }
            ShowAgendaReminder(next);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException)
        { AgendaError = "提醒暂未送达：" + ex.Message; agendaRetry = AgendaNow().AddSeconds(30); settings?.RefreshAgenda(); }
    }
    private void CloseAgendaNotice()
    {
        var old = agendaNotice; agendaNotice = null; agendaShowing = null; agendaPreview = false;
        if (old is not null) { closingAgendaNotice = true; old.Close(); closingAgendaNotice = false; }
    }
    internal void ShowAgendaReminder(AgendaEntry e, bool preview = false)
    {
        // Previews cannot displace an unread real reminder or modify scheduler state.
        if (preview && agendaNotice is not null && !agendaPreview) return;
        CloseAgendaNotice(); DismissWorkReminder(); agendaShowing = e; agendaPreview = preview;
        var character = Catalog.Find(Catalog.VariantId(e.Family, Character.Category));
        string outfit = State.Outfits.GetValueOrDefault(character.Id, "original"); var frame = character.Resolve(outfit, "happy", 0);
        var text = new StackPanel();
        text.Children.Add(new TextBlock { Text = preview ? "提醒预览" : "该记起这件事啦", FontSize = 11, Foreground = CloudTheme.Muted });
        text.Children.Add(new TextBlock { Text = e.Title, FontSize = 18, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 7, 0, 5) });
        text.Children.Add(new TextBlock { Text = $"{e.StartAt.ToLocalTime():yyyy年M月d日 HH:mm} · {PetAgenda.RepeatName(e.Repeat)}", FontSize = 11, Foreground = CloudTheme.Muted, TextWrapping = TextWrapping.Wrap });
        string line = string.IsNullOrWhiteSpace(e.Reminder) ? "这件事记在这里啦，我们一起从容地开始吧。" : e.Reminder;
        text.Children.Add(new TextBlock { Text = CompanionPersonas.Text(e.Family, "name") + "：" + line, FontSize = 13, LineHeight = 21, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 9, 0, 4) });
        var error = new TextBlock { Foreground = CloudTheme.Blue, TextWrapping = TextWrapping.Wrap, FontSize = 11 }; text.Children.Add(error);
        var buttons = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) }; text.Children.Add(buttons);
        void Button(string label, Action action)
        {
            var b = new Button { Content = label, Margin = new Thickness(0, 0, 6, 6), Padding = new Thickness(10, 6, 10, 6) }; AutomationProperties.SetName(b, label);
            b.Click += (_, _) =>
            {
                try { action(); }
                catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException) { error.Text = ex.Message; }
            }; buttons.Children.Add(b);
        }
        if (preview) Button("结束预览", CloseAgendaNotice);
        else
        {
            Button("已完成", () => { Agenda.Finish(e, AgendaNow()); CloseAgendaNotice(); AgendaChanged(); });
            Button("10 分钟后", () => { Agenda.Snooze(e, AgendaNow()); CloseAgendaNotice(); AgendaChanged(); });
        }
        Button("查看日程", OpenAgenda);
        var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(102) }); row.ColumnDefinitions.Add(new ColumnDefinition());
        row.Children.Add(new Image { Source = Art.Frame(character, frame.Sprite, frame.Frame), Width = 94, Height = 152, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(text, 1); row.Children.Add(text);
        var notice = new Window { Title = "云朵伙伴 · 日程提醒", Width = 430, SizeToContent = SizeToContent.Height, WindowStyle = WindowStyle.None,
            AllowsTransparency = true, Background = Brushes.Transparent, ShowInTaskbar = false, ShowActivated = false, Topmost = true, Foreground = CloudTheme.Ink,
            Content = new Border { Background = CloudTheme.Cream, BorderBrush = CloudTheme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(22), Padding = new Thickness(18), Child = row } };
        var area = WorkArea; notice.Left = Math.Max(area.Left, area.Right - notice.Width - 16); notice.Top = Math.Max(area.Top, area.Bottom - 270);
        notice.SizeChanged += (_, _) => notice.Top = Math.Max(area.Top, area.Bottom - notice.ActualHeight - 16);
        // Alt+F4/Escape must not silently mark an unread event complete or reopen it every second.
        notice.Closing += (_, args) => { if (!closingAgendaNotice && !closing && !preview) { args.Cancel = true; error.Text = "请选择已完成或 10 分钟后。"; } };
        notice.Closed += (_, _) => { if (ReferenceEquals(agendaNotice, notice)) { agendaNotice = null; agendaShowing = null; agendaPreview = false; } };
        agendaNotice = notice; notice.Show();
    }
}

using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopPet.Core;

namespace DesktopPet.App;

public sealed partial class PetWindow
{
    private readonly WorkReminderClock workClock = new();
    private readonly Queue<double> recentTouches = new();
    private double lastCompanionActivity, nextCompanionSave;
    private int workReminderIndex, reminderGeneration;
    private Window? workReminder;
    private CancellationTokenSource? workRequest;
    internal void CompanionActivity() { lastCompanionActivity = Now; ResetIdlePosture(); }
    internal void InitializeCompanion()
    {
        CompanionActivity(); nextCompanionSave = Now + 5000;
        if (State.WorkModeEnabled) workClock.Start(Now, State.WorkMinutes);
        Closed += (_, _) => { DismissWorkReminder(); };
        IsVisibleChanged += (_, _) => { if (IsVisible) workClock.Resume(Now); else { workClock.Pause(Now); DismissWorkReminder(); } CompanionActivity(); ResetAllIdlePostures(); settings?.RefreshPresence(); };
    }
    internal int AwardCompanion(int amount, string label, string key, int cooldownSeconds = 30)
    {
        int result = State.Affect(Character.FamilyId, amount, label, DateTimeOffset.Now, key, TimeSpan.FromSeconds(cooldownSeconds));
        Save(); settings?.RefreshCompanion(); return result;
    }
    private bool TouchCompanion()
    {
        CompanionActivity(); while (recentTouches.TryPeek(out double at) && Now - at > 10000) recentTouches.Dequeue(); recentTouches.Enqueue(Now);
        if (recentTouches.Count >= 6)
        {
            AwardCompanion(-2, "连续逗弄", "rough", 10); Say(CompanionPersonas.Text(Character.FamilyId, "distant"), 3000); return false;
        }
        AwardCompanion(1, "温柔照顾", "care", 30); return true;
    }
    internal bool MakeupCheckIn(DateOnly date)
    {
        if (!State.Makeup(date, DateOnly.FromDateTime(DateTime.Now))) return false;
        Save(); settings?.RefreshLife(); return true;
    }
    internal void ConfigureWork(bool enabled, int minutes)
    {
        State.WorkModeEnabled = enabled; State.WorkMinutes = Math.Clamp(minutes, 1, 180); DismissWorkReminder();
        if (enabled) { workClock.Start(Now, State.WorkMinutes); if (!IsVisible) workClock.Pause(Now); } else workClock.Stop(); Save();
    }
    internal bool WorkReminderEnabled => workClock.Enabled;
    private void TickCompanion(double seconds)
    {
        if (previewClock is not null || closing) return;
        if (IsVisible) State.Accompany(Character.FamilyId, seconds, DateTimeOffset.Now);
        if (workClock.Tick(Now)) ShowWorkReminder();
        if (Now >= nextCompanionSave) { nextCompanionSave = Now + 5000; Save(); settings?.RefreshCompanion(); }
        if (State.AutoHide && Now - lastCompanionActivity >= 60000 && IsVisible && !clickThrough && settings?.IsVisible != true
            && ActiveChat is null && !pressed && !dragging && !resting && !dropping
            && hideJourney is null && menu.Children.Count == 0 && (action == "idle" || roaming && !exploring))
        { CompanionActivity(); BeginHide(); }
    }
    internal void DismissWorkReminder()
    {
        reminderGeneration++; workRequest?.Cancel(); workRequest = null;
        if (workReminder is { } old) { workReminder = null; old.Close(); }
    }
    internal async void ShowWorkReminder()
    {
        if (!State.WorkModeEnabled || closing || !IsVisible || agendaNotice is not null) return;
        DismissWorkReminder(); int generation = reminderGeneration, index = workReminderIndex++; string family = Character.FamilyId;
        string preset = CompanionPersonas.Reminder(family, index);
        var message = new TextBlock { Text = preset, TextWrapping = TextWrapping.Wrap, FontSize = 13, LineHeight = 22, Foreground = CloudTheme.Ink, MaxWidth = 230 };
        var origin = new TextBlock { Text = "预设提醒", FontSize = 10, Foreground = CloudTheme.Muted, Margin = new Thickness(0, 7, 0, 0) };
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; text.Children.Add(new TextBlock { Text = CompanionPersonas.Text(family, "name"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) }); text.Children.Add(message); text.Children.Add(origin);
        var row = new DockPanel(); var close = new Button { Content = CloudTheme.Icon("close", 13), Width = 27, Height = 27, Padding = new Thickness(0), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(10, 0, 0, 0) }; close.Click += (_, _) => DismissWorkReminder(); DockPanel.SetDock(close, Dock.Right); row.Children.Add(close);
        var frame = Character.Resolve(State.Outfit, "happy", 0); row.Children.Add(new Image { Source = Art.Frame(Character, frame.Sprite, frame.Frame), Width = 84, Height = 112, Stretch = Stretch.Uniform, Margin = new Thickness(0, 0, 12, 0) }); row.Children.Add(text);
        var notice = new Window { Title = "云朵伙伴 · 工作提醒", Width = 390, SizeToContent = SizeToContent.Height, WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent, ShowInTaskbar = false, ShowActivated = false, Topmost = State.Topmost,
            Content = new Border { Background = CloudTheme.Cream, BorderBrush = CloudTheme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(22), Padding = new Thickness(15), Child = row } };
        workReminder = notice; var area = WorkArea; notice.Left = Math.Max(area.Left, area.Right - notice.Width - 20); notice.Top = Math.Max(area.Top, area.Bottom - 170);
        notice.SizeChanged += (_, _) => notice.Top = Math.Max(area.Top, area.Bottom - notice.ActualHeight - 16); notice.Show();
        var dismiss = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) }; dismiss.Tick += (_, _) => { dismiss.Stop(); if (ReferenceEquals(workReminder, notice)) DismissWorkReminder(); }; notice.Closed += (_, _) => dismiss.Stop(); dismiss.Start();
        if (!Chat.CanGenerate) return;
        var cancellation = new CancellationTokenSource(); workRequest = cancellation; origin.Text = "预设提醒 · 正在准备回复";
        try
        {
            string answer = await Chat.GenerateReminder(family, index, cancellation.Token);
            if (generation != reminderGeneration || cancellation.IsCancellationRequested || !ReferenceEquals(workReminder, notice)) return;
            message.Text = answer.Length > 150 ? answer[..150] + "…" : answer; origin.Text = "模型回复";
        }
        catch (Exception ex) when (ex is OperationCanceledException or HttpRequestException or JsonException or IOException or ArgumentException or FormatException)
        { if (generation == reminderGeneration && ReferenceEquals(workReminder, notice)) origin.Text = "预设提醒 · 模型暂不可用"; }
        finally { if (ReferenceEquals(workRequest, cancellation)) workRequest = null; cancellation.Dispose(); }
    }
}

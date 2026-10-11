using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopPet.Core;

namespace DesktopPet.App;

/// <summary>Real WPF calendar controls, using only the verification --data-dir.</summary>
internal static class SettingsAgendaLayoutVerification
{
    public static async Task Run(PetWindow pet, string output)
    {
        Directory.CreateDirectory(output); var checks = new List<string>();
        void Require(bool ok, string name) { if (!ok) throw new InvalidOperationException(name); checks.Add("PASS " + name); File.WriteAllLines(Path.Combine(output, "calendar-layout-check.txt"), checks); }
        IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); if (child is T item) yield return item; foreach (var nested in Find<T>(child)) yield return nested; }
        }
        void Capture(FrameworkElement view, string name)
        {
            view.UpdateLayout(); var image = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32); image.Render(view); VerificationImages.Save(image, Path.Combine(output, name + ".png"));
        }
        pet.IsHitTestVisible = false; pet.State.AutoHide = false; pet.State.Wander = false; pet.State.CalendarView = "month"; pet.State.CheckIns.Clear(); pet.State.MakeupCheckIns.Clear(); pet.State.MakeupCards = 0;
        pet.ConfigureWork(false, 10); pet.SelectCharacter("gpt"); pet.StopInteraction(); pet.OpenAgenda();
        var window = Application.Current.Windows.OfType<SettingsWindow>().Single(); window.IsHitTestVisible = false; window.ShowActivated = false; window.UpdateLayout();
        Button FindButton(string name, DependencyObject? root = null) => Find<Button>(root ?? window).Single(b => AutomationProperties.GetName(b) == name);
        void Click(string name) { var b = FindButton(name); Require(b.IsEnabled, "enabled " + name); b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout(); }
        Border Pane(string id) => Find<Border>(window).Single(b => AutomationProperties.GetAutomationId(b) == id);
        Button[] Days() => Find<Button>(window).Where(b => AutomationProperties.GetAutomationId(b).StartsWith("agenda-day-", StringComparison.Ordinal)).ToArray();
        string Key(DateOnly d) => d.ToString("yyyy-MM-dd");
        void Pick(DateOnly date) { var b = Days().Single(b => AutomationProperties.GetAutomationId(b) == "agenda-day-" + Key(date)); b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout(); }
        var today = DateOnly.FromDateTime(DateTime.Now);
        Require(Days().Length == 42, "unified month includes 42 dates with adjoining months");
        Require(Pane("agenda-composer").Visibility == Visibility.Collapsed, "calendar is primary; event composer initially collapsed");
        Require(Find<Button>(window).Any(b => AutomationProperties.GetName(b) == "行事历"), "unified sidebar label");
        Require(Find<TextBlock>(window).Any(t => t.Text == "补签卡：0 张"), "makeup balance moved into unified calendar");
        Pick(today); Require(pet.State.CheckedIn(today), "clicking today records attendance");
        int count = pet.State.CheckIns.Count, bond = pet.State.Companion("gpt").Score; Pick(today);
        Require(pet.State.CheckIns.Count == count && pet.State.Companion("gpt").Score == bond, "repeated calendar click cannot add attendance or affection");
        Require(Find<TextBlock>(Pane("agenda-selected-day")).Any(t => t.Text == "✓ 已打卡"), "selected day shows same attendance state");
        Capture(Pane("agenda-calendar"), "calendar-month-full"); Capture(window, "calendar-month-window");
        Click("周历"); Require(Days().Length == 7 && pet.State.CalendarView == "week", "week is seven dates and persists preference");
        Capture(window, "calendar-week"); Click("上一周"); Click("下一周"); Click("月历"); Click("今天");
        var tomorrow = today.AddDays(1);
        var draft = new AgendaDraft("下个日期的复核", PetAgenda.Local(DateTimeOffset.Now.AddDays(1)), 10, "none", PetAgenda.Zone, "笑着提醒你，复核时间到啦。");
        var scheduled = pet.Agenda.Put(draft, "gpt", DateTimeOffset.UtcNow);
        window.RefreshAgenda(); Pick(tomorrow);
        Require(!pet.State.CheckedIn(tomorrow) && pet.State.CheckIns.Count == count, "future date browses events without attendance");
        Require(Find<StackPanel>(Pane("agenda-selected-day")).Any(p => AutomationProperties.GetAutomationId(p) == "agenda-event-" + scheduled.Id), "future event appears in selected-day sidebar");
        Capture(window, "calendar-selected-event");
        Click("全部日程"); Require(Find<TextBlock>(Pane("agenda-selected-day")).Any(t => t.Text == "所有日期"), "all dates filter remains accessible"); Click("查看当天");
        Click("已完成"); Require(!Find<StackPanel>(Pane("agenda-selected-day")).Any(p => AutomationProperties.GetAutomationId(p).StartsWith("agenda-event-")), "completed filter excludes pending events"); Click("待提醒");
        Click("今天"); var old = today.AddDays(-1); Pick(old);
        Require(!pet.State.CheckedIn(old) && Find<TextBlock>(window).Any(t => t.Text == "补签卡不足。"), "past unsigned date never consumes missing card");
        pet.State.MakeupCards = 1; window.RefreshAgenda();
        void MakeupChoice(string choice)
        {
            Exception? error = null; var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            timer.Tick += (_, _) =>
            {
                timer.Stop(); var modal = Application.Current.Windows.OfType<Window>().SingleOrDefault(w => w.Title == "云朵伙伴 · 补签");
                try { if (modal is null) throw new InvalidOperationException("makeup confirmation did not open"); modal.UpdateLayout(); Require(modal.Owner == window, "makeup confirmation is owned by calendar"); FindButton(choice, modal).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
                catch (Exception ex) { error = ex; modal?.Close(); }
            };
            timer.Start(); Pick(old); timer.Stop(); if (error is not null) throw error;
        }
        MakeupChoice("暂不补签"); Require(pet.State.MakeupCards == 1 && !pet.State.CheckedIn(old), "cancel confirmation retains balance and attendance");
        double seconds = pet.State.Companion("gpt").Seconds; bond = pet.State.Companion("gpt").Score;
        MakeupChoice("使用 1 张补签卡");
        Require(pet.State.MakeupCards == 0 && pet.State.CheckedIn(old) && pet.State.MakeupCheckIns.Contains(Key(old)), "confirmed makeup consumes exactly one card");
        Require(pet.State.Companion("gpt").Score == bond && pet.State.Companion("gpt").Seconds >= seconds && pet.State.Companion("gpt").Seconds - seconds < 2, "makeup creates no affection or invented companion time");
        Pick(old); Require(pet.State.MakeupCards == 0, "signed date cannot consume a second makeup card");
        Click("记一件事"); var input = window.EventChat;
        Require(input.IsVisible && Pane("agenda-composer").Visibility == Visibility.Visible, "composer expands in calendar");
        var text = Find<TextBox>(input).Single(t => AutomationProperties.GetName(t) == "日程内容"); text.Text = "尚未送出的日程文字";
        window.RefreshLife(); Require(ReferenceEquals(input, window.EventChat) && Find<TextBox>(input).Single(t => AutomationProperties.GetName(t) == "日程内容").Text == "尚未送出的日程文字", "background collection refresh preserves unsent agenda input");
        input.ShowAgendaDraft(draft); var title = Find<TextBox>(input).Single(t => AutomationProperties.GetName(t) == "事情"); title.Text = "用户修改但未确认的标题";
        window.RefreshLife(); Require(input.HasAgendaDraft && Find<TextBox>(input).Single(t => AutomationProperties.GetName(t) == "事情").Text == "用户修改但未确认的标题", "background collection refresh preserves edited confirmation draft");
        Click("收起输入"); Click("记一件事"); Require(input.HasAgendaDraft && Find<TextBox>(input).Single(t => AutomationProperties.GetName(t) == "事情").Text == "用户修改但未确认的标题", "composer fold and unfold preserve unconfirmed draft");
        Click("暂不保存日程"); Click("收起输入");
        var dateField = typeof(SettingsWindow).GetField("agendaDate", BindingFlags.Instance | BindingFlags.NonPublic)!;
        dateField.SetValue(window, new DateOnly(2026, 12, 31)); window.RefreshAgenda(); Click("下一月");
        Require(Days().Any(b => AutomationProperties.GetAutomationId(b) == "agenda-day-2027-01-01"), "next month crosses year correctly");
        Click("周历"); Click("下一周"); Require(Days().Length == 7 && Days().Select(b => AutomationProperties.GetAutomationId(b)).Distinct().Count() == 7, "year-boundary week has seven distinct days");
        Click("月历"); Click("今天");
        dateField.SetValue(window, today.AddMonths(-2)); window.RefreshAgenda(); window.RefreshAgendaDate(today.AddDays(1));
        Require((DateOnly)dateField.GetValue(window)! == today.AddMonths(-2), "midnight does not replace intentionally browsed history");
        window.RefreshAgendaDate(today); dateField.SetValue(window, today); window.RefreshAgendaDate(today.AddDays(1));
        Require((DateOnly)dateField.GetValue(window)! == today.AddDays(1), "midnight follows today when no history is being browsed"); window.RefreshAgendaDate(today); Click("今天");
        window.Width = window.MinWidth; window.UpdateLayout(); await Task.Delay(150);
        var calendar = Pane("agenda-calendar"); var sidebar = Pane("agenda-selected-day");
        Require(calendar.ActualWidth >= 340 && sidebar.ActualWidth >= 220, "narrow desktop calendar and day sidebar retain usable widths");
        foreach (var day in Days()) Require(day.ActualWidth >= 34 && day.ActualHeight >= 70, "narrow date cell remains clickable " + AutomationProperties.GetAutomationId(day));
        Capture(window, "calendar-narrow");
        Click("接入 AI"); Require(Find<ComboBox>(window).Any(c => AutomationProperties.GetName(c) == "模型服务"), "calendar API entry opens actual model settings tab");
        window.ShowAgendaComposer(); input.ShowAgendaDraft(draft); Click("我的伙伴"); Require(!input.HasAgendaDraft, "leaving calendar still cancels unconfirmed draft");
        pet.Save(); var restored = new StateStore(output).Load(); Require(restored.CheckedIn(today) && restored.CheckedIn(old) && restored.MakeupCards == 0 && restored.CalendarView == "month", "attendance and preferred view survive reload");
        Require(new AgendaStore(output).Events.Single().Title == scheduled.Title, "calendar layout does not mutate existing agenda data");
        window.Close(); pet.StopInteraction();
        File.AppendAllText(Path.Combine(output, "calendar-layout-check.txt"), $"PASS {checks.Count} unified native calendar checks.\n");
    }
}

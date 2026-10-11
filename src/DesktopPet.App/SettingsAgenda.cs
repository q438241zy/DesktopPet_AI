using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using DesktopPet.Core;

namespace DesktopPet.App;

public sealed partial class SettingsWindow
{
    private DateOnly agendaDate = DateOnly.FromDateTime(DateTime.Now);
    private DateOnly agendaToday = DateOnly.FromDateTime(DateTime.Now);
    private bool agendaAll, agendaDone, agendaComposerExpanded;
    private string agendaFeedback = "";
    private InlineChat? agendaComposer;
    internal InlineChat EventChat => agendaComposer ??= new InlineChat(pet, true) { Width = double.NaN, BorderThickness = new Thickness(0), Padding = new Thickness(0) };
    internal void ResetAgendaComposer(bool reset = false) { if (reset) agendaComposer?.ResetSession(); else { agendaComposer?.CancelResponse(); agendaComposer?.SetCompanion(); } }
    internal void EditAgendaDraft(AgendaDraft draft, string id) { agendaComposerExpanded = true; Navigate("agenda"); EventChat.ShowAgendaDraft(draft, id); }
    internal void ShowAgenda() => Navigate("agenda");
    internal void ShowAgendaComposer() { agendaComposerExpanded = true; Navigate("agenda"); }
    internal void RefreshAgenda() { if (page == "agenda") Rebuild(); }
    internal void RefreshAgendaDate(DateOnly today)
    {
        if (!agendaAll && agendaDate == agendaToday) agendaDate = today;
        agendaToday = today;
    }
    private static string AgendaDayKey(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private void BuildAgenda()
    {
        RefreshAgendaDate(DateOnly.FromDateTime(DateTime.Now));
        Heading("", "行事历", "");
        var toolbar = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };
        var tools = new StackPanel { Orientation = Orientation.Horizontal };
        var api = MakeButton("接入 AI", ShowApiSettings); api.Margin = new Thickness(0, 0, 8, 0); tools.Children.Add(api);
        var compose = MakeButton(agendaComposerExpanded ? "收起输入" : "记一件事", () => { agendaComposerExpanded = !agendaComposerExpanded; Rebuild(); }, "chat");
        compose.Background = CloudTheme.Pale; tools.Children.Add(compose); DockPanel.SetDock(tools, Dock.Right); toolbar.Children.Add(tools);
        var status = Text(pet.Chat.CanGenerate ? "AI 已设定" : "AI 未接入", 12, true); status.VerticalAlignment = VerticalAlignment.Center; status.Margin = new Thickness(0); toolbar.Children.Add(status); content.Children.Add(toolbar);

        if (EventChat.Parent is Panel previous) previous.Children.Remove(EventChat);
        else if (EventChat.Parent is Border previousCard) previousCard.Child = null;
        var composerCard = Card(EventChat); composerCard.Visibility = agendaComposerExpanded ? Visibility.Visible : Visibility.Collapsed;
        AutomationProperties.SetAutomationId(composerCard, "agenda-composer");
        // Hiding the container preserves unsaved input and confirmation when browsing dates.
        // Leaving this page still cancels its request through the shared navigation lifecycle.
        EventChat.Open(); content.Children.Add(composerCard);
        if (pet.AgendaError is { } error) content.Children.Add(Text(error, 13));

        var active = pet.Agenda.Events.Where(e => e.Active).OrderBy(e => e.RemindAt).ToArray();
        var next = active.FirstOrDefault();
        var hero = new Grid(); hero.ColumnDefinitions.Add(new ColumnDefinition()); hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(124) });
        var summary = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        summary.Children.Add(Text(next?.Status == "notified" ? "有一件事在等你" : next is null ? "陪伴日历" : "下一件小事", 12, true));
        summary.Children.Add(Text(next?.Title ?? "今天，也一起过", 23));
        summary.Children.Add(Text(next is null ? "把值得记住的小事，留在这一天。" : $"{next.StartAt.ToLocalTime():M月d日 HH:mm} · {CompanionPersonas.Text(next.Family, "name")} 来提醒", 12, true));
        summary.Children.Add(Text($"待提醒 {active.Length}　 ·　今天 {active.Count(e => DateOnly.FromDateTime(e.StartAt.LocalDateTime) == agendaToday)}", 12, true)); hero.Children.Add(summary);
        var partner = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(partner, 1);
        var smile = pet.Character.Resolve(pet.State.Outfit, "happy", 0);
        partner.Children.Add(new Image { Source = pet.Art.Frame(pet.Character, smile.Sprite, smile.Frame), Stretch = Stretch.Uniform, Height = 108, Margin = new Thickness(8, 0, 8, 4) });
        var owner = Text(CompanionName(pet.Character) + " 的小日历", 11, true); owner.TextAlignment = TextAlignment.Center; owner.Margin = new Thickness(0); partner.Children.Add(owner); hero.Children.Add(partner);
        var heroCard = Card(hero, CloudTheme.Brush("#FFF1F5")); heroCard.Margin = new Thickness(0, 12, 0, 16); content.Children.Add(heroCard);

        var layout = new Grid(); layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.85, GridUnitType.Star) }); layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) }); layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 220 });
        var calendarCard = Card(AgendaCalendar()); calendarCard.Padding = new Thickness(16); calendarCard.Margin = new Thickness(0); calendarCard.VerticalAlignment = VerticalAlignment.Top;
        AutomationProperties.SetAutomationId(calendarCard, "agenda-calendar"); layout.Children.Add(calendarCard);
        var listCard = Card(AgendaDayList()); listCard.Padding = new Thickness(16); listCard.Margin = new Thickness(0); listCard.VerticalAlignment = VerticalAlignment.Top; Grid.SetColumn(listCard, 2);
        AutomationProperties.SetAutomationId(listCard, "agenda-selected-day"); layout.Children.Add(listCard); content.Children.Add(layout);
        var scope = Text("电脑开机且云朵伙伴运行时提醒；退出或关机后，下次启动补提醒。重复日程显示下一次时间。", 11, true); scope.Margin = new Thickness(0, 15, 0, 0); content.Children.Add(scope);
    }
    private UIElement AgendaCalendar()
    {
        bool week = pet.State.CalendarView == "week";
        var panel = new StackPanel();
        var heading = new DockPanel();
        var modes = Tabs([("month", "月历"), ("week", "周历")], pet.State.CalendarView, mode => { pet.State.CalendarView = mode; pet.Save(); Rebuild(); });
        foreach (var button in modes.Children.OfType<Button>()) { button.Padding = new Thickness(9, 5, 9, 5); button.Margin = new Thickness(3, 0, 0, 0); }
        DockPanel.SetDock(modes, Dock.Right); heading.Children.Add(modes);
        var begin = CompanionCalendar.Start(agendaDate, week);
        var caption = Text(week ? $"{begin:M月d日} — {begin.AddDays(6):M月d日}" : agendaDate.ToString("yyyy 年 M 月", CultureInfo.InvariantCulture), 18);
        caption.Margin = new Thickness(0, 3, 8, 0); heading.Children.Add(caption); panel.Children.Add(heading);
        var nav = new DockPanel { Margin = new Thickness(0, 12, 0, 12) };
        var arrows = new StackPanel { Orientation = Orientation.Horizontal };
        var previous = MakeButton(week ? "上一周" : "上一月", () => MoveAgendaPeriod(-1), "back"); previous.Content = CloudTheme.Icon("back", 14); previous.Width = 28; previous.Padding = new Thickness(4); previous.Margin = new Thickness(0, 0, 4, 0); arrows.Children.Add(previous);
        var current = MakeButton("今天", () => { agendaDate = DateOnly.FromDateTime(DateTime.Now); agendaAll = false; agendaFeedback = ""; Rebuild(); }); current.Padding = new Thickness(8, 4, 8, 4); arrows.Children.Add(current);
        var forward = MakeButton(week ? "下一周" : "下一月", () => MoveAgendaPeriod(1), "back"); var forwardIcon = CloudTheme.Icon("back", 14); forwardIcon.RenderTransformOrigin = new Point(.5, .5); forwardIcon.RenderTransform = new RotateTransform(180); forward.Content = forwardIcon; forward.Width = 28; forward.Padding = new Thickness(4); forward.Margin = new Thickness(4, 0, 0, 0); arrows.Children.Add(forward);
        DockPanel.SetDock(arrows, Dock.Right); nav.Children.Add(arrows);
        var tag = Text(week ? $"{begin.Year} 年 · 日程与打卡" : "日程 · 打卡", 11, true); tag.VerticalAlignment = VerticalAlignment.Center; tag.Margin = new Thickness(0); nav.Children.Add(tag); panel.Children.Add(nav);
        var stats = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        foreach (string value in new[] { $"连续 {pet.State.Streak(agendaToday)} 天", $"累计 {pet.State.CheckIns.Count} 天", $"补签卡：{pet.State.MakeupCards} 张" })
        { var text = Text(value, 11, true); text.Margin = new Thickness(0, 0, 12, 4); stats.Children.Add(text); }
        panel.Children.Add(stats);
        var weekdays = new UniformGrid { Columns = 7, Height = 27 };
        foreach (string label in new[] { "一", "二", "三", "四", "五", "六", "日" }) weekdays.Children.Add(new TextBlock { Text = label, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Foreground = muted });
        panel.Children.Add(weekdays);
        var days = new UniformGrid { Columns = 7 }; AutomationProperties.SetAutomationId(days, "agenda-days");
        var start = week ? begin : begin.AddDays(-((int)begin.DayOfWeek + 6) % 7);
        for (int index = 0; index < (week ? 7 : 42); index++) days.Children.Add(AgendaDay(start.AddDays(index), week));
        panel.Children.Add(days);
        var foot = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        var all = MakeButton(agendaAll ? "查看当天" : "全部日程", () => { agendaAll = !agendaAll; agendaFeedback = ""; Rebuild(); }); all.Padding = new Thickness(7, 4, 7, 4); all.FontSize = 11; DockPanel.SetDock(all, Dock.Right); foot.Children.Add(all);
        var legend = Text("● 日程　✓ 已打卡", 10, true); legend.Margin = new Thickness(0); legend.VerticalAlignment = VerticalAlignment.Center; foot.Children.Add(legend); panel.Children.Add(foot);
        return panel;
    }
    private Button AgendaDay(DateOnly date, bool week)
    {
        bool signed = pet.State.CheckedIn(date), makeup = pet.State.MakeupCheckIns.Contains(AgendaDayKey(date)), today = date == agendaToday, selected = !agendaAll && date == agendaDate;
        var entries = pet.Agenda.Events.Where(e => e.Status != "cancelled" && DateOnly.FromDateTime(e.StartAt.LocalDateTime) == date).OrderBy(e => e.StartAt).ToArray();
        string status = signed ? makeup ? "已补签" : "已打卡" : today ? "今天打卡" : date < agendaToday ? "补签" : "未到日期";
        var button = MakeButton($"{date:yyyy-MM-dd} {status}", () => SelectAgendaDate(date));
        AutomationProperties.SetAutomationId(button, "agenda-day-" + AgendaDayKey(date));
        AutomationProperties.SetHelpText(button, $"{entries.Length} 件日程；" + status);
        button.HorizontalContentAlignment = HorizontalAlignment.Stretch; button.VerticalContentAlignment = VerticalAlignment.Top;
        button.Padding = new Thickness(4, 7, 4, 5); button.Margin = new Thickness(2); button.Height = week ? 114 : 75; button.MinWidth = 0;
        button.Background = today ? CloudTheme.Brush("#FCE3ED") : signed ? CloudTheme.Brush("#EFF7F1") : Brushes.White;
        button.BorderBrush = selected ? CloudTheme.Blue : today ? CloudTheme.Brush("#E5B3C7") : CloudTheme.Brush("#F2E8EC"); button.BorderThickness = new Thickness(selected ? 1.5 : 1);
        var body = new StackPanel(); var number = new DockPanel();
        if (signed || today)
        {
            var mark = new TextBlock { Text = signed ? "✓" : "·", Foreground = signed ? CloudTheme.Brush("#5E9577") : CloudTheme.Blue, FontSize = 12, FontWeight = FontWeights.SemiBold, Margin = new Thickness(2, 0, 0, 0) };
            DockPanel.SetDock(mark, Dock.Right); number.Children.Add(mark);
        }
        number.Children.Add(new TextBlock { Text = date.Day.ToString(CultureInfo.InvariantCulture), FontSize = 14, FontWeight = today || selected ? FontWeights.SemiBold : FontWeights.Normal, Foreground = !week && date.Month != agendaDate.Month ? muted : CloudTheme.Ink }); body.Children.Add(number);
        foreach (var entry in entries.Take(week ? 2 : 1))
            body.Children.Add(new TextBlock { Text = entry.Title, FontSize = 9, Foreground = entry.Status == "done" ? muted : CloudTheme.Blue, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 5, 0, 0) });
        if (entries.Length > (week ? 2 : 1)) body.Children.Add(new TextBlock { Text = $"+{entries.Length - (week ? 2 : 1)} 件", FontSize = 9, Foreground = muted, Margin = new Thickness(0, 3, 0, 0) });
        else if (today && !signed && entries.Length == 0) body.Children.Add(new TextBlock { Text = "打卡", FontSize = 10, Foreground = CloudTheme.Blue, Margin = new Thickness(0, 6, 0, 0) });
        button.Content = body; button.ToolTip = entries.Length == 0 ? status : string.Join("\n", entries.Select(e => $"{e.StartAt.LocalDateTime:HH:mm} {e.Title}")) + "\n" + status;
        return button;
    }
    private void MoveAgendaPeriod(int direction)
    {
        agendaDate = pet.State.CalendarView == "week" ? agendaDate.AddDays(direction * 7) : new DateOnly(agendaDate.Year, agendaDate.Month, 1).AddMonths(direction);
        agendaAll = false; agendaFeedback = ""; Rebuild();
    }
    private void SelectAgendaDate(DateOnly date)
    {
        agendaDate = date; agendaAll = false; agendaFeedback = "";
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (pet.State.CheckedIn(date) || date > today) { Rebuild(); return; }
        if (date == today) { pet.CheckIn(false); Rebuild(); return; }
        if (pet.State.MakeupCards < 1) { agendaFeedback = "补签卡不足。"; Rebuild(); return; }
        Rebuild();
        if (ConfirmAgendaMakeup(date))
        {
            if (!pet.MakeupCheckIn(date)) agendaFeedback = "这一天已打卡，或补签卡不足。";
            Rebuild();
        }
    }
    private bool ConfirmAgendaMakeup(DateOnly date)
    {
        var body = new StackPanel { Margin = new Thickness(24) };
        body.Children.Add(Text("补上这一天", 22)); body.Children.Add(Text($"消耗 1 张补签卡，补签 {date:M 月 d 日}？", 14));
        var dialog = new Window { Owner = this, Title = "云朵伙伴 · 补签", Width = 380, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = CloudTheme.Cream, Foreground = CloudTheme.Ink, FontFamily = FontFamily, ShowInTaskbar = false, Content = body };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cancel = MakeButton("暂不补签", () => dialog.DialogResult = false); cancel.IsCancel = true; cancel.Padding = new Thickness(14, 8, 14, 8); buttons.Children.Add(cancel);
        var confirm = MakeButton("使用 1 张补签卡", () => dialog.DialogResult = true); confirm.IsDefault = true; confirm.Padding = new Thickness(14, 8, 14, 8); confirm.Margin = new Thickness(8, 0, 0, 0); confirm.Background = CloudTheme.Pale; buttons.Children.Add(confirm); body.Children.Add(buttons);
        return dialog.ShowDialog() == true;
    }
    private UIElement AgendaDayList()
    {
        var panel = new StackPanel();
        panel.Children.Add(Text(agendaAll ? "所有日期" : agendaDate == agendaToday ? "今天" : "这一天", 11, true));
        panel.Children.Add(Text(agendaAll ? agendaDone ? "已完成" : "待提醒" : agendaDate.ToString("M月d日 dddd", CultureInfo.GetCultureInfo("zh-CN")), 19));
        if (!agendaAll)
        {
            string attendance = pet.State.CheckedIn(agendaDate) ? pet.State.MakeupCheckIns.Contains(AgendaDayKey(agendaDate)) ? "✓ 已补签" : "✓ 已打卡" : agendaDate == agendaToday ? "点日历中的今天，即可打卡" : agendaDate < agendaToday ? "尚未打卡" : "留给未来的一天";
            panel.Children.Add(Text(attendance, 11, true));
        }
        if (agendaFeedback.Length > 0) { var feedback = Text(agendaFeedback, 12); AutomationProperties.SetAutomationId(feedback, "agenda-feedback"); panel.Children.Add(feedback); }
        var filters = Tabs([("pending", "待提醒"), ("done", "已完成")], agendaDone ? "done" : "pending", id => { agendaDone = id == "done"; Rebuild(); });
        foreach (var button in filters.Children.OfType<Button>()) { button.FontSize = 12; button.Padding = new Thickness(11, 6, 11, 6); button.Margin = new Thickness(0, 0, 5, 5); }
        panel.Children.Add(filters);
        var entries = pet.Agenda.Events.Where(e => agendaDone ? e.Status == "done" : e.Active).Where(e => agendaAll || DateOnly.FromDateTime(e.StartAt.LocalDateTime) == agendaDate).OrderBy(e => e.StartAt).ToArray();
        if (entries.Length == 0)
        {
            var empty = new StackPanel { Margin = new Thickness(0, 32, 0, 32) }; empty.Children.Add(new CloudIcon { Width = 65, Height = 62, HorizontalAlignment = HorizontalAlignment.Center, Opacity = .6 });
            var label = Text(agendaDone ? "还没有完成的日程" : "这一天，留一点空白", 13, true); label.TextAlignment = TextAlignment.Center; label.Margin = new Thickness(0, 14, 0, 0); empty.Children.Add(label); panel.Children.Add(empty);
        }
        foreach (var entry in entries)
        {
            var row = new StackPanel { Margin = new Thickness(0, 8, 0, 12) }; AutomationProperties.SetAutomationId(row, "agenda-event-" + entry.Id);
            var time = Text(entry.StartAt.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture), 13); time.Foreground = CloudTheme.Blue; time.FontWeight = FontWeights.SemiBold; time.Margin = new Thickness(0, 0, 0, 4); row.Children.Add(time);
            row.Children.Add(Text(entry.Title, 15));
            row.Children.Add(Text($"{entry.StartAt.ToLocalTime():M月d日} · {CompanionPersonas.Text(entry.Family, "name")} · {PetAgenda.RepeatName(entry.Repeat)}", 11, true));
            row.Children.Add(Text(entry.SnoozeUntil is { } snooze ? $"延后至 {snooze.ToLocalTime():M月d日 HH:mm}" : entry.Status == "notified" ? "正在提醒" : entry.Status == "done" ? "已完成" : entry.LeadMinutes > 0 ? $"提前 {entry.LeadMinutes} 分钟" : "准时提醒", 11, true));
            if (entry.Zone != PetAgenda.Zone) row.Children.Add(Text("原时区：" + entry.Zone + "；重复日程需修改并确认当前时区。", 10, true));
            var buttons = new WrapPanel(); row.Children.Add(buttons);
            if (entry.Active) { buttons.Children.Add(MakeButton("预览提醒", () => pet.ShowAgendaReminder(entry, true))); buttons.Children.Add(MakeButton("修改", () => pet.EditAgenda(entry))); }
            var feedback = Text("", 11, true);
            buttons.Children.Add(MakeButton("删除", () =>
            {
                if (MessageBox.Show(this, "删除「" + entry.Title + "」？", "删除日程", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
                try { pet.Agenda.Cancel(entry); pet.AgendaChanged(); } catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException) { feedback.Text = ex.Message; }
            }));
            foreach (var button in buttons.Children.OfType<Button>()) { button.FontSize = 10; button.Padding = new Thickness(6, 4, 6, 4); button.Margin = new Thickness(0, 0, 5, 5); }
            row.Children.Add(feedback); var separator = new Border { BorderBrush = CloudTheme.Line, BorderThickness = new Thickness(0, 0, 0, 1), Child = row, Margin = new Thickness(0, 0, 0, 8) }; panel.Children.Add(separator);
        }
        return panel;
    }
}

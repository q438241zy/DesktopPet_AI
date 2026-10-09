using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using DesktopPet.Core;

namespace DesktopPet.App;

public sealed partial class SettingsWindow
{
    private DateOnly agendaDate = DateOnly.FromDateTime(DateTime.Now);
    private bool agendaWeek = true, agendaAll = true, agendaDone;
    internal void ShowAgenda() => Navigate("agenda");
    internal void RefreshAgenda() { if (page == "agenda") Rebuild(); }
    private void BuildAgenda()
    {
        Heading("", "宠物行事历", "");
        var top = new DockPanel();
        var chat = MakeButton("告诉宠物", () => { if (!pet.IsVisible) pet.ToggleVisible(); pet.OpenChat(); }); DockPanel.SetDock(chat, Dock.Right); top.Children.Add(chat);
        var api = MakeButton(pet.Chat.CanGenerate ? "AI 接口已设定" : "接入 AI", () => Navigate("preferences")); DockPanel.SetDock(api, Dock.Right); api.Margin = new Thickness(0, 0, 10, 0); top.Children.Add(api);
        top.Children.Add(Text("先聊一聊，确认后再记下。", 13, true)); content.Children.Add(top);
        if (pet.AgendaError is { } error) content.Children.Add(Text(error, 13));
        var active = pet.Agenda.Events.Where(e => e.Active).OrderBy(e => e.RemindAt).ToArray();
        var next = active.FirstOrDefault();
        if (next is not null)
        {
            var box = new StackPanel(); box.Children.Add(Text(next.Status == "notified" ? "等待你处理" : "下一件事", 12, true));
            box.Children.Add(Text(next.Title, 21)); box.Children.Add(Text($"{next.StartAt.ToLocalTime():M月d日 HH:mm}  ·  {CompanionPersonas.Text(next.Family, "name")}  ·  {PetAgenda.RepeatName(next.Repeat)}", 12, true));
            content.Children.Add(Card(box, CloudTheme.Sky()));
        }
        var calendar = new StackPanel(); var nav = new WrapPanel();
        nav.Children.Add(MakeButton("上一段", () => { agendaDate = agendaWeek ? agendaDate.AddDays(-7) : agendaDate.AddMonths(-1); Rebuild(); }));
        var begin = CompanionCalendar.Start(agendaDate, agendaWeek);
        nav.Children.Add(new TextBlock { Text = agendaWeek ? $"{begin:yyyy/MM/dd} — {begin.AddDays(6):yyyy/MM/dd}" : agendaDate.ToString("yyyy 年 M 月", CultureInfo.InvariantCulture), FontSize = 17, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(13, 0, 13, 0) });
        nav.Children.Add(MakeButton("下一段", () => { agendaDate = agendaWeek ? agendaDate.AddDays(7) : agendaDate.AddMonths(1); Rebuild(); }));
        nav.Children.Add(MakeButton("今天", () => { agendaDate = DateOnly.FromDateTime(DateTime.Now); agendaAll = false; Rebuild(); })); calendar.Children.Add(nav);
        calendar.Children.Add(Tabs([("week", "周历"), ("month", "月历")], agendaWeek ? "week" : "month", id => { agendaWeek = id == "week"; Rebuild(); }));
        var weekdays = new UniformGrid { Columns = 7, Height = 28 };
        foreach (string label in new[] { "一", "二", "三", "四", "五", "六", "日" }) weekdays.Children.Add(new TextBlock { Text = label, HorizontalAlignment = HorizontalAlignment.Center, Foreground = muted });
        calendar.Children.Add(weekdays); var days = new UniformGrid { Columns = 7 };
        foreach (var value in CompanionCalendar.Days(agendaDate, agendaWeek))
        {
            if (value is not { } date) { days.Children.Add(new Border()); continue; }
            var entries = pet.Agenda.Events.Where(e => e.Status != "cancelled" && DateOnly.FromDateTime(e.StartAt.LocalDateTime) == date).ToArray();
            var label = new StackPanel(); label.Children.Add(new TextBlock { Text = date.Day.ToString(), FontSize = 17, HorizontalAlignment = HorizontalAlignment.Center });
            label.Children.Add(new TextBlock { Text = entries.Length == 0 ? "" : entries.Length + " 件", FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 5, 0, 0) });
            var button = MakeButton("日程 " + date.ToString("yyyy-MM-dd"), () => { agendaDate = date; agendaAll = false; Rebuild(); }); button.Content = label; button.Height = agendaWeek ? 80 : 60; button.Padding = new Thickness(2); button.Margin = new Thickness(3);
            button.Background = date == DateOnly.FromDateTime(DateTime.Now) ? CloudTheme.Blush : date == agendaDate && !agendaAll ? CloudTheme.Brush("#DDEEE5") : Brushes.White;
            if (entries.Length > 0) button.ToolTip = string.Join("\n", entries.Select(e => $"{e.StartAt.LocalDateTime:HH:mm} {e.Title}")); days.Children.Add(button);
        }
        calendar.Children.Add(days); content.Children.Add(Card(calendar));
        var filter = new DockPanel(); var all = MakeButton(agendaAll ? "查看当天" : "查看全部日程", () => { agendaAll = !agendaAll; Rebuild(); }); DockPanel.SetDock(all, Dock.Right); filter.Children.Add(all);
        filter.Children.Add(Tabs([("pending", "待提醒"), ("done", "已完成")], agendaDone ? "done" : "pending", id => { agendaDone = id == "done"; Rebuild(); })); content.Children.Add(filter);
        var rows = pet.Agenda.Events.Where(e => agendaDone ? e.Status == "done" : e.Active).Where(e => agendaAll || DateOnly.FromDateTime(e.StartAt.LocalDateTime) == agendaDate).OrderBy(e => e.StartAt).ToArray();
        if (rows.Length == 0) content.Children.Add(Text("这里还没有日程。", 13, true));
        foreach (var entry in rows)
        {
            var row = new StackPanel(); row.Children.Add(Text(entry.Title, 17));
            row.Children.Add(Text($"{entry.StartAt.ToLocalTime():yyyy/MM/dd HH:mm}  ·  提前 {entry.LeadMinutes} 分钟  ·  {PetAgenda.RepeatName(entry.Repeat)}  ·  {CompanionPersonas.Text(entry.Family, "name")}", 12, true));
            if (entry.Zone != PetAgenda.Zone) row.Children.Add(Text("原时区：" + entry.Zone + "；重复日程需修改并确认当前时区。", 11, true));
            if (entry.SnoozeUntil is { } snooze) row.Children.Add(Text($"延后至 {snooze.ToLocalTime():M月d日 HH:mm}", 11, true));
            var buttons = new WrapPanel(); row.Children.Add(buttons);
            buttons.Children.Add(MakeButton("预览提醒", () => pet.ShowAgendaReminder(entry, true)));
            if (entry.Active) buttons.Children.Add(MakeButton("修改", () => pet.EditAgenda(entry)));
            var feedback = Text("", 11, true);
            buttons.Children.Add(MakeButton("删除", () =>
            {
                if (MessageBox.Show(this, "删除「" + entry.Title + "」？", "删除日程", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
                try { pet.Agenda.Cancel(entry); pet.AgendaChanged(); } catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException) { feedback.Text = ex.Message; }
            })); row.Children.Add(feedback); content.Children.Add(Card(row));
        }
        content.Children.Add(Text("电脑开机且云朵伙伴运行时提醒；退出或关机后，下次启动补提醒。重复日程显示下一次时间。", 11, true));
    }
}

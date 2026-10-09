using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using DesktopPet.Core;

namespace DesktopPet.App;

public sealed partial class SettingsWindow
{
    private TextBlock? heroBond, profileTime, profileScore, profileCount, presenceLabel;
    private Ellipse? presenceLight;
    private (bool Visible, bool Reduced)? lastPresence;
    private ProgressBar? bondMeter;
    private StackPanel? bondHistory;
    private string profileTab = "bond", collectionTab = "balls";
    private DateOnly calendarDate = DateOnly.FromDateTime(DateTime.Now);
    private DateOnly calendarToday = DateOnly.FromDateTime(DateTime.Now);
    private void RefreshCalendarDate()
    {
        var today = DateOnly.FromDateTime(DateTime.Now); if (today == calendarToday) return;
        bool week = pet.State.CalendarView == "week";
        if (CompanionCalendar.Start(calendarDate, week) == CompanionCalendar.Start(calendarToday, week)) calendarDate = today;
        if (agendaDate == calendarToday) agendaDate = today;
        calendarToday = today; if (page is "life" or "agenda") Rebuild();
    }
    private static string CompanionName(Character character) => character.Name.Split('·')[0].Trim();
    private static string TogetherTime(double seconds)
    {
        var time = TimeSpan.FromSeconds(seconds);
        return time.TotalHours >= 1 ? $"{(int)time.TotalHours} 小时 {time.Minutes} 分钟" : $"{(int)time.TotalMinutes} 分钟";
    }
    private UIElement PresenceControl()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        presenceLight = new Ellipse { Width = 8, Height = 8, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
        presenceLabel = new TextBlock { FontSize = 12, Foreground = muted };
        row.Children.Add(presenceLight); row.Children.Add(presenceLabel);
        var button = MakeButton("切换陪伴状态", pet.ToggleVisible); button.Content = row;
        button.Background = Brushes.Transparent; button.BorderThickness = new Thickness(0); button.Padding = new Thickness(0); button.HorizontalAlignment = HorizontalAlignment.Left;
        RefreshPresence(); return button;
    }
    internal void RefreshPresence()
    {
        if (presenceLabel is null || presenceLight is null) return;
        var state = (pet.IsVisible, pet.State.ReducedMotion); if (lastPresence == state) return; lastPresence = state;
        presenceLabel.Text = pet.IsVisible ? "正在陪伴" : "暂停陪伴";
        presenceLight.Fill = CloudTheme.Brush(pet.IsVisible ? "#65A995" : "#CE8295");
        presenceLight.BeginAnimation(OpacityProperty, null);
        if (!pet.State.ReducedMotion) presenceLight.BeginAnimation(OpacityProperty, new DoubleAnimation(.38, 1, TimeSpan.FromSeconds(1.6)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
    }
    private void ClearCompanionBindings() { heroBond = profileTime = profileScore = profileCount = null; bondMeter = null; bondHistory = null; }
    internal void RefreshCompanion()
    {
        var current = pet.State.Companion(pet.Character.FamilyId);
        if (heroBond is not null) heroBond.Text = $"{current.Name}  ·  已陪伴 {TogetherTime(current.Seconds)}";
        var bond = pet.State.Companion(compareFamily);
        if (profileTime is not null) profileTime.Text = TogetherTime(bond.Seconds);
        if (profileCount is not null) profileCount.Text = $"{bond.Interactions} 次互动";
        if (profileScore is not null) profileScore.Text = $"{bond.Score:+0;-0;0}  ·  {bond.Name}";
        if (bondMeter is not null) bondMeter.Value = bond.Score;
        if (bondHistory is not null)
        {
            bondHistory.Children.Clear();
            foreach (var item in bond.History.TakeLast(5).Reverse()) bondHistory.Children.Add(Text($"{item.At.LocalDateTime:MM-dd HH:mm}   {item.Label}   {item.Delta:+0;-0;0}", 12, true));
            if (bond.History.Count == 0) bondHistory.Children.Add(Text("从今天的陪伴开始。", 12, true));
        }
        RefreshPresence();
    }
    internal void ShowProfile(string family)
    {
        compareFamily = family; compareOutfit = "original"; profileTab = "bond"; Navigate("profile");
    }
    private WrapPanel Tabs(IEnumerable<(string Id, string Name)> choices, string selected, Action<string> changed)
    {
        var row = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        foreach (var (id, name) in choices)
        {
            var b = MakeButton(name, () => changed(id)); b.Padding = new Thickness(15, 8, 15, 8); b.Margin = new Thickness(0, 0, 6, 5);
            b.Background = id == selected ? CloudTheme.Pale : Brushes.White; b.Foreground = id == selected ? CloudTheme.Blue : CloudTheme.Ink;
            row.Children.Add(b);
        }
        return row;
    }
    private void BuildCompanionProfile()
    {
        var back = MakeButton("返回我的伙伴", () => Navigate("partners")); back.HorizontalAlignment = HorizontalAlignment.Left; content.Children.Add(back);
        string family = compareFamily;
        var selected = pet.Catalog.Characters.FirstOrDefault(c => c.FamilyId == family && c.Category == category) ?? pet.Character;
        var heading = new DockPanel(); var image = Stage(selected, 190, pet.State.Outfits.GetValueOrDefault(selected.Id, "original")); image.Width = 180; heading.Children.Add(image);
        var summary = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 0, 0) };
        summary.Children.Add(Text(CompanionName(selected), 28)); summary.Children.Add(Text(string.Join(" · ", CompanionPersonas.Traits(family)), 13, true)); summary.Children.Add(Text(CompanionPersonas.Text(family, "description"), 13)); heading.Children.Add(summary); content.Children.Add(Card(heading));
        content.Children.Add(Tabs([("bond", "我们的默契"), ("styles", "风格预览"), ("about", "说明")], profileTab, tab => { profileTab = tab; Rebuild(); }));
        var body = new StackPanel();
        if (profileTab == "bond")
        {
            profileScore = Text("", 23); body.Children.Add(profileScore);
            bondMeter = new ProgressBar { Minimum = -100, Maximum = 100, Height = 9, Foreground = CloudTheme.Blue, Background = CloudTheme.Pale, Margin = new Thickness(0, 4, 0, 7) }; body.Children.Add(bondMeter);
            var range = new DockPanel(); var max = Text("亲密 100", 11, true); max.HorizontalAlignment = HorizontalAlignment.Right; DockPanel.SetDock(max, Dock.Right); range.Children.Add(max); range.Children.Add(Text("疏远 −100", 11, true)); body.Children.Add(range);
            profileTime = Text("", 21); body.Children.Add(profileTime); profileCount = Text("", 12, true); body.Children.Add(profileCount);
            body.Children.Add(Text("温柔照顾、聊天和共读会增加默契；连续逗弄时，她也需要一点空间。", 12, true));
            bondHistory = new StackPanel { Margin = new Thickness(0, 12, 0, 0) }; body.Children.Add(bondHistory);
        }
        else if (profileTab == "styles")
        {
            var outfits = new[] { ("original", "原装"), ("sports", "运动服"), ("swim", "泳装"), ("wedding", "婚纱") };
            body.Children.Add(Tabs(outfits, compareOutfit, id => { compareOutfit = id; Rebuild(); }));
            var versions = new UniformGrid { Columns = 2 };
            int gate = compareOutfit switch { "sports" => 20, "swim" => 50, "wedding" => 80, _ => 0 };
            foreach (string style in new[] { CharacterStyles.Chibi, CharacterStyles.Realistic })
            {
                var character = pet.Catalog.Characters.FirstOrDefault(c => c.FamilyId == family && c.Category == style); if (character is null) continue;
                var view = new StackPanel { Margin = new Thickness(5) }; view.Children.Add(Stage(character, 280, compareOutfit)); view.Children.Add(Text(CloudTheme.CategoryName(style), 16));
                if (pet.State.Companion(family).Score < gate)
                {
                    var hint = new StackPanel { Orientation = Orientation.Horizontal }; hint.Children.Add(new MemberLock { Width = 26, Height = 28 }); hint.Children.Add(Text($" 默契 {gate} · 测试期开放", 11, true)); view.Children.Add(hint);
                }
                var use = MakeButton("陪伴我 · " + CloudTheme.CategoryName(style), () => { pet.State.Outfits[character.Id] = compareOutfit; pet.SelectCharacter(character.Id); category = style; Rebuild(); }); view.Children.Add(use); versions.Children.Add(view);
            }
            body.Children.Add(versions);
        }
        else
        {
            var profile = CompanionPersonas.Profile(family);
            foreach (var paragraph in profile.GetProperty("about").EnumerateArray()) body.Children.Add(Text(paragraph.GetString()!, 14));
            body.Children.Add(Text("喜欢的东西", 18));
            foreach (var like in profile.GetProperty("likes").EnumerateArray()) { body.Children.Add(Text(like[0].GetString()!, 14)); body.Children.Add(Text(like[1].GetString()!, 12, true)); }
            body.Children.Add(Text("相处方式", 18)); body.Children.Add(Text(profile.GetProperty("together").GetString()!, 14));
            body.Children.Add(MakeButton("和她聊聊", () => { pet.SelectCharacter(selected.Id); pet.OpenChat(); }, "chat"));
        }
        content.Children.Add(Card(body)); RefreshCompanion();
    }
    private void BuildCompanionLife()
    {
        Heading("", "陪伴日常", ""); var today = DateOnly.FromDateTime(DateTime.Now); bool week = pet.State.CalendarView == "week";
        var panel = new StackPanel(); var header = new DockPanel();
        var cards = Text($"补签卡：{pet.State.MakeupCards} 张", 12, true); DockPanel.SetDock(cards, Dock.Right); header.Children.Add(cards); header.Children.Add(Text("打卡", 20)); panel.Children.Add(header);
        var toolbar = new DockPanel();
        var modes = Tabs([("month", "月历"), ("week", "周历")], pet.State.CalendarView, mode => { pet.State.CalendarView = mode; pet.Save(); Rebuild(); }); DockPanel.SetDock(modes, Dock.Right); toolbar.Children.Add(modes);
        var start = CompanionCalendar.Start(calendarDate, week); string caption = week ? $"{start:yyyy.MM.dd} — {start.AddDays(6):MM.dd}" : $"{calendarDate:yyyy 年 M 月}";
        toolbar.Children.Add(Text(caption, 18)); panel.Children.Add(toolbar);
        var nav = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        var previous = MakeButton(week ? "上一周" : "上一月", () => { calendarDate = week ? calendarDate.AddDays(-7) : calendarDate.AddMonths(-1); Rebuild(); }); nav.Children.Add(previous);
        var current = MakeButton("今天", () => { calendarDate = today; Rebuild(); }); current.Margin = new Thickness(6, 0, 6, 0); nav.Children.Add(current);
        var next = MakeButton(week ? "下一周" : "下一月", () => { calendarDate = week ? calendarDate.AddDays(7) : calendarDate.AddMonths(1); Rebuild(); }); next.IsEnabled = start < CompanionCalendar.Start(today, week); nav.Children.Add(next); panel.Children.Add(nav);
        var weekdays = new UniformGrid { Columns = 7, Height = 28 };
        foreach (string label in new[] { "一", "二", "三", "四", "五", "六", "日" }) weekdays.Children.Add(new TextBlock { Text = label, Foreground = muted, HorizontalAlignment = HorizontalAlignment.Center });
        panel.Children.Add(weekdays); var days = new UniformGrid { Columns = 7 };
        var feedback = Text("", 11, true); feedback.Margin = new Thickness(0, 8, 0, 0);
        foreach (DateOnly? item in CompanionCalendar.Days(calendarDate, week))
        {
            if (item is not { } date) { days.Children.Add(new Border()); continue; }
            bool signed = pet.State.CheckedIn(date), makeup = pet.State.MakeupCheckIns.Contains(date.ToString("yyyy-MM-dd")), isToday = date == today;
            var label = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            label.Children.Add(new TextBlock { Text = date.Day.ToString(CultureInfo.InvariantCulture), FontSize = 17, FontWeight = isToday ? FontWeights.SemiBold : FontWeights.Normal, HorizontalAlignment = HorizontalAlignment.Center });
            label.Children.Add(new TextBlock { Text = signed ? makeup ? "补签" : "✓" : isToday ? "今天" : "", FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 0) });
            var b = MakeButton($"{date:yyyy-MM-dd} " + (signed ? makeup ? "已补签" : "已打卡" : isToday ? "今天打卡" : date < today ? "补签" : "未到日期"), () =>
            {
                if (signed) return;
                if (isToday) { pet.CheckIn(false); return; }
                if (pet.State.MakeupCards < 1) { feedback.Text = "补签卡不足。"; return; }
                if (MessageBox.Show(this, $"消耗 1 张补签卡，补签 {date:M 月 d 日}？", "补签", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK) pet.MakeupCheckIn(date);
            });
            b.Content = label; b.Height = week ? 86 : 62; b.Margin = new Thickness(3); b.Padding = new Thickness(2); b.IsEnabled = date <= today;
            b.Background = isToday ? CloudTheme.Brush("#F6D6E2") : signed ? CloudTheme.Brush("#E6F1EA") : Brushes.White;
            if (isToday) b.BorderBrush = CloudTheme.Brush("#CA7997"); days.Children.Add(b);
        }
        panel.Children.Add(days); panel.Children.Add(feedback); panel.Children.Add(Text($"已打卡 {pet.State.CheckIns.Count} 天  ·  连续 {pet.State.Streak(today)} 天", 12, true)); content.Children.Add(Card(panel));
        var collection = new StackPanel(); collection.Children.Add(Text("收藏", 20)); collection.Children.Add(Tabs([("balls", "球区"), ("food", "食物区"), ("stories", "故事区")], collectionTab, id => { collectionTab = id; Rebuild(); }));
        if (collectionTab == "stories")
        {
            foreach (var story in StoryLibrary.All)
            {
                int count = pet.State.ReadStories.GetValueOrDefault(story.Id); var row = new DockPanel { Margin = new Thickness(0, 8, 0, 8) };
                var read = MakeButton(count > 0 ? "再读一次 · " + story.Title : "阅读 · " + story.Title, () => pet.ReadStory(story.Id)); DockPanel.SetDock(read, Dock.Right); row.Children.Add(read);
                row.Children.Add(Text(story.Title + (count > 0 ? $"  ·  已读 {count} 次" : "  ·  未收录"), 14)); collection.Children.Add(row);
            }
        }
        else
        {
            var tiles = new UniformGrid { Columns = 3 };
            foreach (var item in collectionTab == "balls" ? Collectibles.Sports : Collectibles.Food)
            {
                int count = pet.State.Treasures.Count(t => Collectibles.FromSavedName(t)?.Id == item.Id);
                var box = new StackPanel(); box.Children.Add(Text(item.Name, 16)); box.Children.Add(Text(count > 0 ? $"已收藏 × {count}" : "未收录", 12, true));
                if (count > 0 && item.Kind == ItemKind.Sport)
                {
                    var play = MakeButton("玩收藏 " + item.Name, () => { pet.CompanionActivity(); pet.PlayWithToy(item.Id); }); play.Content = box; play.Background = CloudTheme.Brush("#FFF6F9"); play.HorizontalContentAlignment = HorizontalAlignment.Stretch; play.Padding = new Thickness(15); play.Margin = new Thickness(3); tiles.Children.Add(play);
                }
                else { var card = Card(box, count > 0 ? CloudTheme.Brush("#FFF6F9") : Brushes.White); card.Margin = new Thickness(3); card.Padding = new Thickness(15); tiles.Children.Add(card); }
            }
            collection.Children.Add(tiles);
            if (collectionTab == "food")
            {
                var other = pet.State.Treasures.Where(t => Collectibles.FromSavedName(t) is not { Kind: ItemKind.Sport } and not { Edible: true }).GroupBy(t => t).Select(g => $"{g.Key} × {g.Count()}").ToArray();
                if (other.Length > 0) { collection.Children.Add(Text("其他收藏", 14)); collection.Children.Add(Text(string.Join("　", other), 12, true)); }
            }
        }
        content.Children.Add(Card(collection));
    }
    private UIElement WorkSettingsPanel()
    {
        var panel = new StackPanel(); var row = new DockPanel();
        var track = new Border { Width = 44, Height = 26, CornerRadius = new CornerRadius(13), Padding = new Thickness(3) };
        var knob = new Ellipse { Width = 20, Height = 20, Fill = Brushes.White }; track.Child = knob;
        var toggle = new ToggleButton { IsChecked = pet.State.WorkModeEnabled, Content = track, BorderThickness = new Thickness(0), Background = Brushes.Transparent, Padding = new Thickness(0), VerticalAlignment = VerticalAlignment.Top }; AutomationProperties.SetName(toggle, "工作模式开关"); DockPanel.SetDock(toggle, Dock.Right); row.Children.Add(toggle); row.Children.Add(Text("工作模式", 18)); panel.Children.Add(row);
        var detail = new StackPanel { Margin = new Thickness(0, 12, 0, 0) }; panel.Children.Add(detail);
        void Update() { bool on = toggle.IsChecked == true; detail.Visibility = on ? Visibility.Visible : Visibility.Collapsed; track.Background = CloudTheme.Brush(on ? "#92BBAA" : "#D9CCD2"); knob.HorizontalAlignment = on ? HorizontalAlignment.Right : HorizontalAlignment.Left; }
        var minutes = new TextBox { Text = pet.State.WorkMinutes.ToString(CultureInfo.InvariantCulture), Width = 72, Height = 36, MaxLength = 3, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(7, 0, 7, 0) }; AutomationProperties.SetName(minutes, "提醒间隔分钟");
        var interval = new StackPanel { Orientation = Orientation.Horizontal }; interval.Children.Add(new TextBlock { Text = "每", VerticalAlignment = VerticalAlignment.Center }); interval.Children.Add(minutes); interval.Children.Add(new TextBlock { Text = "分钟提醒一次", VerticalAlignment = VerticalAlignment.Center }); detail.Children.Add(interval);
        var message = Text("", 11, true); message.Margin = new Thickness(0, 7, 0, 0);
        var save = MakeButton("保存间隔", () => { if (!int.TryParse(minutes.Text, out int value) || value is < 1 or > 180) { message.Text = "请填写 1–180 分钟。"; return; } pet.ConfigureWork(toggle.IsChecked == true, value); message.Text = $"已设为每 {value} 分钟提醒。"; }); save.Margin = new Thickness(12, 0, 0, 0); interval.Children.Add(save);
        detail.Children.Add(message); detail.Children.Add(Text(CompanionPersonas.Reminder(pet.Character.FamilyId, 0), 13, true));
        var preview = MakeButton("试一下提醒", pet.ShowWorkReminder); preview.HorizontalAlignment = HorizontalAlignment.Left; detail.Children.Add(preview);
        toggle.Click += (_, _) => { pet.ConfigureWork(toggle.IsChecked == true, pet.State.WorkMinutes); Update(); }; Update(); return panel;
    }
}

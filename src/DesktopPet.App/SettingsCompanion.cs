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
    private string profileTab = "bond", collectionTab = "balls", lifeTab = "actions";
    private DateOnly calendarToday = DateOnly.FromDateTime(DateTime.Now);
    private void RefreshCalendarDate()
    {
        var today = DateOnly.FromDateTime(DateTime.Now); if (today == calendarToday) return;
        RefreshAgendaDate(today);
        calendarToday = today; if (page == "agenda") Rebuild();
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
        Heading("", "陪伴日常", "");
        var top = new DockPanel();
        var partner = MakeButton(CompanionName(pet.Character) + " · " + CloudTheme.CategoryName(pet.Character.Category), () => Navigate("partners"), "heart");
        DockPanel.SetDock(partner, Dock.Right); top.Children.Add(partner);
        top.Children.Add(Tabs([("actions", "动作"), ("collection", "图鉴收集")], lifeTab, id => { lifeTab = id; Rebuild(); })); content.Children.Add(top);
        if (lifeTab == "actions")
        {
            BuildDailyActions("玩耍", ["gift", "read", "butterfly", "highfive", "rps", "ball", "blocks", "walk", "peek", "bubbles", "stars", "think", "jump", "curl", "bonk"]);
            BuildDailyActions("互动", ["checkin", "snack", "headpat", "poke", "tickle", "praise", "comfort", "lullaby", "comb", "wipe", "stretch", "rest"]); return;
        }
        var collection = new StackPanel(); collection.Children.Add(Text("图鉴收集", 20));
        int collected = Collectibles.All.Count(item => pet.State.Treasures.Any(t => Collectibles.FromSavedName(t)?.Id == item.Id)) + StoryLibrary.All.Count(story => pet.State.ReadStories.GetValueOrDefault(story.Id) > 0);
        collection.Children.Add(Text($"{collected} / {Collectibles.All.Count + StoryLibrary.All.Count} 已收集", 12, true)); collection.Children.Add(Tabs([("balls", "球区"), ("food", "食物区"), ("stories", "故事区")], collectionTab, id => { collectionTab = id; Rebuild(); }));
        if (collectionTab == "stories")
        {
            foreach (var story in StoryLibrary.All)
            {
                int count = pet.State.ReadStories.GetValueOrDefault(story.Id); var row = new DockPanel { Margin = new Thickness(0, 8, 0, 8) };
                var read = MakeButton(count > 0 ? "再读一次" : "阅读", () => { if (!pet.IsVisible) pet.ToggleVisible(); pet.ReadStory(story.Id); });
                AutomationProperties.SetName(read, (count > 0 ? "再读一次 · " : "阅读 · ") + story.Title); DockPanel.SetDock(read, Dock.Right); row.Children.Add(read);
                row.Children.Add(Text(story.Title + (count > 0 ? $"  ·  已读 {count} 次" : "  ·  未收录"), 14)); collection.Children.Add(row);
            }
        }
        else
        {
            var tiles = new UniformGrid { Columns = 3 };
            foreach (var item in collectionTab == "balls" ? Collectibles.Sports : Collectibles.Food)
            {
                int count = pet.State.Treasures.Count(t => Collectibles.FromSavedName(t)?.Id == item.Id);
                var box = new StackPanel(); box.Children.Add(new ItemVisual { Item = item, Width = 48, Height = 48, Opacity = count > 0 ? 1 : .4, Margin = new Thickness(0, 0, 0, 10) }); box.Children.Add(Text(item.Name, 16)); box.Children.Add(Text(count > 0 ? $"已收藏 × {count}" : "未收录", 12, true));
                if (count > 0 && item.Kind == ItemKind.Sport)
                {
                    var play = MakeButton("玩收藏 " + item.Name, () => { if (!pet.IsVisible) pet.ToggleVisible(); pet.CompanionActivity(); pet.PlayWithToy(item.Id); }); play.Content = box; play.Background = CloudTheme.Brush("#FFF6F9"); play.HorizontalContentAlignment = HorizontalAlignment.Stretch; play.Padding = new Thickness(15); play.Margin = new Thickness(3); tiles.Children.Add(play);
                }
                else { var card = Card(box, count > 0 ? CloudTheme.Brush("#FFF6F9") : Brushes.White); card.Margin = new Thickness(3); card.Padding = new Thickness(15); tiles.Children.Add(card); }
            }
            collection.Children.Add(tiles);
            var keepsakes = new StackPanel();
            foreach (var item in Collectibles.All.Where(item => item.Kind != ItemKind.Sport && !item.Edible))
            {
                int count = pet.State.Treasures.Count(t => Collectibles.FromSavedName(t)?.Id == item.Id);
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
                row.Children.Add(new ItemVisual { Item = item, Width = 32, Height = 32, Opacity = count > 0 ? 1 : .4, Margin = new Thickness(0, 0, 12, 0) });
                row.Children.Add(Text(item.Name + (count > 0 ? $" × {count}" : " · 未收录"), 13, count == 0)); keepsakes.Children.Add(row);
            }
            var legacy = pet.State.Treasures.Where(t => Collectibles.FromSavedName(t) is null).GroupBy(t => t).Select(g => $"{g.Key} × {g.Count()}").ToArray();
            if (legacy.Length > 0) keepsakes.Children.Add(Text(string.Join("　", legacy), 12, true));
            collection.Children.Add(new Expander { Header = "其他小物", Content = keepsakes, Margin = new Thickness(0, 18, 0, 0) });
        }
        content.Children.Add(Card(collection));
    }
    private void BuildDailyActions(string title, string[] keys)
    {
        content.Children.Add(new TextBlock { Text = title, FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 20, 0, 15) });
        var grid = new UniformGrid { Columns = 5 };
        foreach (string key in keys)
        {
            var action = PetActions.Daily.First(a => a.Key == key);
            var symbol = CloudTheme.Icon(action.Icon, 25); var box = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            symbol.HorizontalAlignment = HorizontalAlignment.Center;
            box.Children.Add(new Border { Background = title == "玩耍" ? CloudTheme.Pale : CloudTheme.Brush("#EAF4EE"), CornerRadius = new CornerRadius(12), Padding = new Thickness(8), Child = symbol, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 9) });
            box.Children.Add(new TextBlock { Text = key == "blocks" ? "堆积木" : action.Title, TextAlignment = TextAlignment.Center, FontSize = 13 });
            var button = MakeButton("日常动作 " + action.Title, () => { if (!pet.IsVisible) pet.ToggleVisible(); pet.RunInteraction(key); }, action.Icon);
            button.Tag = key; button.Content = box; button.Height = 108; button.Padding = new Thickness(6); button.Margin = new Thickness(0, 0, 9, 10); grid.Children.Add(button);
        }
        content.Children.Add(grid);
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

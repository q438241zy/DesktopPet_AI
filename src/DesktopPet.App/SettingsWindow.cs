using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Shell;
using DesktopPet.Core;

namespace DesktopPet.App;

/// <summary>The character, life and creator controls share the live desktop pet's state.</summary>
public sealed class SettingsWindow : Window
{
    private readonly PetWindow pet;
    private readonly StackPanel content = new();
    private TextBlock status = new();
    private string page = "partners";
    private string category;
    private string compareFamily = "whale";
    private string compareOutfit = "original";
    private string importCategory = "chibi";
    private readonly Dictionary<string, Button> navigation = [];
    private readonly Brush muted = CloudTheme.Muted;
    private readonly Brush peach = CloudTheme.Pale;
    public SettingsWindow(PetWindow pet)
    {
        this.pet = pet; category = pet.Character.Category;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { pet.StopInteraction(); e.Handled = true; } };
        Title = "DesktopPet · 云朵伙伴"; Icon = CloudTheme.AppIcon;
        Foreground = CloudTheme.Ink; FontFamily = new FontFamily("Segoe UI Variable, Microsoft YaHei UI"); FontSize = 13;
        Width = Math.Min(1120, SystemParameters.WorkArea.Width - 40); Height = Math.Min(850, SystemParameters.WorkArea.Height - 40); MinWidth = 920; MinHeight = 650;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; Background = CloudTheme.Brush("#FBFBFD");
        WindowStyle = WindowStyle.None;
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 48, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(16) });
        var root = new Grid { Background = Background }; root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) }); root.RowDefinitions.Add(new RowDefinition()); Content = root;
        var titlebar = new DockPanel { Margin = new Thickness(22, 8, 12, 8) }; root.Children.Add(titlebar);
        var close = MakeButton("关闭设置", Close); close.Content = CloudTheme.Icon("close", 16); close.Width = 34; close.Padding = new Thickness(0); close.Margin = new Thickness(4, 0, 0, 0); close.Background = Brushes.Transparent; close.BorderThickness = new Thickness(0); DockPanel.SetDock(close, Dock.Right); WindowChrome.SetIsHitTestVisibleInChrome(close, true); titlebar.Children.Add(close);
        var minimize = MakeButton("最小化", () => WindowState = WindowState.Minimized); minimize.Content = CloudTheme.Icon("minimize", 16); minimize.Width = 34; minimize.Padding = new Thickness(0); minimize.Margin = new Thickness(0); minimize.Background = Brushes.Transparent; minimize.BorderThickness = new Thickness(0); DockPanel.SetDock(minimize, Dock.Right); WindowChrome.SetIsHitTestVisibleInChrome(minimize, true); titlebar.Children.Add(minimize);
        titlebar.Children.Add(new TextBlock { Text = "云朵伙伴", Foreground = muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        var layout = new Grid(); layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) }); layout.ColumnDefinitions.Add(new ColumnDefinition()); Grid.SetRow(layout, 1); root.Children.Add(layout);
        var sideSurface = new Border { Background = CloudTheme.Brush("#F2F3F6"), BorderBrush = CloudTheme.Line, BorderThickness = new Thickness(0, 0, 1, 0) }; layout.Children.Add(sideSurface);
        var sidebar = new DockPanel { Margin = new Thickness(15, 25, 15, 24) }; sideSurface.Child = sidebar;
        var brand = new StackPanel { Margin = new Thickness(12, 0, 0, 29) };
        var mark = CloudTheme.Icon("cloud", 35); mark.Foreground = CloudTheme.Blue; mark.HorizontalAlignment = HorizontalAlignment.Left; brand.Children.Add(mark);
        brand.Children.Add(new TextBlock { Text = "云朵伙伴", FontSize = 21, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 5) });
        brand.Children.Add(new TextBlock { Text = "你的桌边小小陪伴", Foreground = muted, FontSize = 11 });
        DockPanel.SetDock(brand, Dock.Top); sidebar.Children.Add(brand);
        var foot = new StackPanel { Margin = new Thickness(12, 0, 0, 0) }; foot.Children.Add(new TextBlock { Text = "●  正在桌面陪伴", Foreground = CloudTheme.Brush("#34845A"), FontSize = 11 }); foot.Children.Add(new TextBlock { Text = "DesktopPet  /  1.2 Preview 4", FontSize = 10, Foreground = muted, Margin = new Thickness(0, 8, 0, 0) });
        DockPanel.SetDock(foot, Dock.Bottom); sidebar.Children.Add(foot);
        var nav = new StackPanel(); sidebar.Children.Add(nav);
        foreach (var (id, label, icon) in new[] { ("partners", "我的伙伴", "heart"), ("styles", "风格预览", "cube"), ("life", "陪伴日常", "sun"), ("studio", "角色工坊", "brush"), ("preferences", "桌面偏好", "settings") })
        {
            var b = MakeButton(label, () => Navigate(id), icon); b.HorizontalContentAlignment = HorizontalAlignment.Left; b.Padding = new Thickness(12, 11, 6, 11); b.Margin = new Thickness(0, 0, 0, 5); b.BorderThickness = new Thickness(0); navigation[id] = b; nav.Children.Add(b);
        }
        var scroll = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(34, 20, 28, 20) };
        Grid.SetColumn(scroll, 1); layout.Children.Add(scroll); Rebuild();
    }
    private static Button MakeButton(string label, Action clicked, string icon = "")
    {
        var button = new Button { HorizontalContentAlignment = HorizontalAlignment.Center };
        if (icon.Length == 0) button.Content = label;
        else
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal }; row.Children.Add(CloudTheme.Icon(icon, 19));
            row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) }); button.Content = row;
        }
        AutomationProperties.SetName(button, label); button.Click += (_, _) => clicked(); return button;
    }
    private TextBlock Text(string text, double size = 13, bool quiet = false) => new() { Text = text, FontSize = size, Foreground = quiet ? muted : Foreground, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10), LineHeight = size * 1.6 };
    private void Heading(string eyebrow, string title, string description)
    {
        content.Children.Add(new TextBlock { Text = title, FontSize = 29, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 7) }); content.Children.Add(Text(description, 12, true));
    }
    private Border Card(UIElement child, Brush? background = null) => new() { Background = background ?? Brushes.White, BorderBrush = CloudTheme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Padding = new Thickness(22), Margin = new Thickness(0, 12, 0, 20), Child = child };
    private void Navigate(string destination)
    {
        if (destination == "styles" && page != "styles")
        {
            compareFamily = Catalog.BuiltInFamilies.Contains(pet.Character.FamilyId) ? pet.Character.FamilyId : "whale";
            compareOutfit = new[] { "original", "swim", "wedding" }.Contains(pet.State.Outfit) ? pet.State.Outfit : "original";
        }
        page = destination; Rebuild();
    }
    private void Rebuild()
    {
        content.Children.Clear();
        foreach (var (id, b) in navigation) { b.Background = id == page ? CloudTheme.Brush("#E2EAF6") : Brushes.Transparent; b.Foreground = id == page ? CloudTheme.Blue : CloudTheme.Ink; b.FontWeight = id == page ? FontWeights.SemiBold : FontWeights.Normal; }
        switch (page) { case "styles": Styles(); break; case "life": Life(); break; case "studio": Studio(); break; case "preferences": Preferences(); break; default: Partners(); break; }
    }
    private Grid Stage(Character character, double height, string outfit = "original")
    {
        var stage = new Grid { Height = height, ClipToBounds = true }; stage.Children.Add(new CloudScenery());
        var frame = character.Resolve(outfit, "idle", 0);
        stage.Children.Add(new Image { Source = pet.Art.Frame(character, frame.Sprite, frame.Frame), Stretch = Stretch.Uniform, Margin = new Thickness(12, 4, 12, 2) }); return stage;
    }
    private void Partners()
    {
        Heading("", "我的伙伴", "选一个喜欢的伙伴，让今天多一点陪伴。");
        var hero = new Grid(); hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(236) }); hero.ColumnDefinitions.Add(new ColumnDefinition());
        hero.Children.Add(Stage(pet.Character, 182, pet.State.Outfit));
        var intro = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 0, 0) }; Grid.SetColumn(intro, 1); hero.Children.Add(intro);
        var badges = new WrapPanel(); badges.Children.Add(CloudTheme.Badge("正在陪伴")); badges.Children.Add(CloudTheme.Badge(CloudTheme.CategoryName(pet.Character.Category))); intro.Children.Add(badges);
        var name = Text(pet.Character.Name, 25); name.FontWeight = FontWeights.SemiBold; name.Margin = new Thickness(0, 10, 0, 4); intro.Children.Add(name);
        intro.Children.Add(Text($"{pet.State.BondName}  ·  相伴 {pet.State.CheckIns.Count} 天", 12, true));
        if (pet.Character.Outfits.Count > 0)
        {
            var outfits = new WrapPanel();
            foreach (var (id, label) in new[] { ("original", "原装") }.Concat(pet.Character.Outfits.Select(x => (x.Key, x.Value.Name))))
            {
                var b = MakeButton(label, () => { pet.State.Outfits[pet.State.Character] = id; pet.ApplySettings(); Rebuild(); }, "dress"); b.Padding = new Thickness(12, 8, 12, 8); b.Margin = new Thickness(0, 4, 6, 3); b.Foreground = id == pet.State.Outfit ? CloudTheme.Blue : CloudTheme.Ink; b.Background = id == pet.State.Outfit ? CloudTheme.Pale : Brushes.White;
                AutomationProperties.SetName(b, "选择服装 " + label); outfits.Children.Add(b);
            }
            intro.Children.Add(outfits);
        }
        else intro.Children.Add(Text("当前使用你导入的角色。", 11, true));
        var heroCard = Card(hero, Brushes.White); heroCard.Padding = new Thickness(15, 12, 18, 12); content.Children.Add(heroCard);
        var categoryRow = new DockPanel { Margin = new Thickness(0, 0, 0, 11) };
        var compare = MakeButton("三种风格对照", () => Navigate("styles")); compare.Background = Brushes.Transparent; compare.BorderThickness = new Thickness(0); compare.FontSize = 11; DockPanel.SetDock(compare, Dock.Right); categoryRow.Children.Add(compare);
        var filters = new StackPanel { Orientation = Orientation.Horizontal }; categoryRow.Children.Add(new Border { Background = CloudTheme.Brush("#ECEEF2"), Padding = new Thickness(3), CornerRadius = new CornerRadius(11), HorizontalAlignment = HorizontalAlignment.Left, Child = filters });
        foreach (var (id, glyph) in new[] { ("chibi", "heart"), ("3d", "cube"), ("adult", "person") })
        {
            var b = MakeButton(CloudTheme.CategoryName(id), () => { pet.SelectStyle(id); category = id; Rebuild(); }, glyph); b.Padding = new Thickness(13, 7, 13, 7); b.Margin = new Thickness(1, 0, 1, 0); b.Background = category == id ? Brushes.White : Brushes.Transparent; b.Foreground = category == id ? CloudTheme.Blue : CloudTheme.Muted; b.BorderThickness = new Thickness(0);
            AutomationProperties.SetName(b, "分类 " + CloudTheme.CategoryName(id)); filters.Children.Add(b);
        }
        content.Children.Add(categoryRow);
        var tiles = new System.Windows.Controls.Primitives.UniformGrid { Columns = 4 };
        foreach (var character in pet.Catalog.Characters.Where(c => c.Category == category))
        {
            bool selected = character.Id == pet.State.Character;
            var tile = new Grid(); tile.RowDefinitions.Add(new RowDefinition()); tile.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            tile.Children.Add(Stage(character, 112, pet.State.Outfits.GetValueOrDefault(character.Id, "original")));
            var label = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center }; label.Children.Add(new TextBlock { Text = character.Name, FontSize = 12, FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal });
            if (selected) label.Children.Add(new TextBlock { Text = "  ✓", Foreground = CloudTheme.Blue }); Grid.SetRow(label, 1); tile.Children.Add(label);
            var b = MakeButton("选择角色 " + character.Name, () => { pet.SelectCharacter(character.Id); Rebuild(); }); b.Content = tile; b.Height = 154; b.Padding = new Thickness(6, 5, 6, 12); b.Margin = new Thickness(0, 0, 10, 10); b.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            if (selected) { b.BorderBrush = CloudTheme.Blue; b.Background = CloudTheme.Brush("#F1F6FF"); }
            tiles.Children.Add(b);
        }
        content.Children.Add(tiles);
        content.Children.Add(Text("点击摸摸头  ·  拖动抱起  ·  右键找它玩", 11, true));
    }

    private void Styles()
    {
        Heading("", "风格预览", "同一位伙伴，三种模样。选好画风和服装，就能带到桌边。");
        var families = new WrapPanel { Margin = new Thickness(0, 3, 0, 2) };
        foreach (string family in Catalog.BuiltInFamilies)
        {
            var b = MakeButton(pet.Catalog.Find(family).Name, () => { compareFamily = family; Rebuild(); });
            b.FontSize = 12; b.Padding = new Thickness(12, 6, 12, 6); b.Margin = new Thickness(0, 0, 7, 7);
            b.Background = compareFamily == family ? CloudTheme.Pale : Brushes.White;
            b.BorderBrush = compareFamily == family ? CloudTheme.Brush("#8EB9E0") : CloudTheme.Line;
            AutomationProperties.SetName(b, "对照角色 " + pet.Catalog.Find(family).Name); families.Children.Add(b);
        }
        content.Children.Add(families);
        var wardrobe = new WrapPanel { Margin = new Thickness(0, 2, 0, 4) };
        foreach (var (id, label) in new[] { ("original", "原装"), ("swim", "泳装"), ("wedding", "婚纱") })
        {
            var b = MakeButton(label, () => { compareOutfit = id; Rebuild(); }, "dress");
            b.Padding = new Thickness(10, 2, 13, 2); b.Margin = new Thickness(0, 0, 8, 2); b.Background = compareOutfit == id ? CloudTheme.Pale : Brushes.White;
            AutomationProperties.SetName(b, "对照服装 " + label); wardrobe.Children.Add(b);
        }
        content.Children.Add(wardrobe);
        var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3 };
        foreach (var (style, subtitle, detail) in new[] {
            ("chibi", "小比例 · 二次元", "现有逐帧动作与服饰。"),
            ("3d", "成年比例 · 3D 动画风格", "柔和塑形、立体发丝与布料。"),
            ("adult", "成年比例 · 写实真人风格", "自然五官、发丝与真实材质。") })
        {
            string id = Catalog.VariantId(compareFamily, style), title = CloudTheme.CategoryName(style);
            var character = pet.Catalog.Find(id); var panel = new StackPanel();
            var badge = CloudTheme.Badge(title); badge.HorizontalAlignment = HorizontalAlignment.Left; panel.Children.Add(badge);
            panel.Children.Add(Stage(character, 284, compareOutfit));
            var caption = Text(subtitle, 14); caption.FontWeight = FontWeights.SemiBold; panel.Children.Add(caption); panel.Children.Add(Text(detail, 11, true));
            bool selected = pet.State.Character == id && pet.State.Outfit == compareOutfit;
            var button = MakeButton(selected ? "正在桌面陪你" : "放到桌面陪你", () => { pet.State.Outfits[id] = compareOutfit; pet.SelectCharacter(id); category = character.Category; Rebuild(); }, "heart"); button.Margin = new Thickness(0); button.FontSize = 12;
            AutomationProperties.SetName(button, "试看 " + title); panel.Children.Add(button);
            var card = Card(panel); card.Margin = new Thickness(0, 6, 12, 16); card.Padding = new Thickness(15); grid.Children.Add(card);
        }
        content.Children.Add(grid);
        content.Children.Add(Text("三种画风、三套服装均有独立行走动画。切换画风会保留同一伙伴和服装；未补齐的互动使用当前服装的姿势图。", 12, true));
    }
    public void RefreshStatus()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        status.Text = $"{pet.State.BondName}  ·  Lv.{pet.State.BondLevel + 1}\n累计 {pet.State.CheckIns.Count} 天    连续 {pet.State.Streak(today)} 天\n初次相遇：{pet.State.AdoptedAt}";
    }
    private void Life()
    {
        status = new TextBlock();
        Heading("", "陪伴日常", "今天想玩什么？");
        string clothes = pet.Character.Outfits.TryGetValue(pet.State.Outfit, out var selectedOutfit) ? selectedOutfit.Name : "原装";
        content.Children.Add(Text($"当前陪伴：{pet.Character.Name} · {CloudTheme.CategoryName(pet.Character.Category)} · {clothes}", 14));
        status.FontSize = 16; status.LineHeight = 28; RefreshStatus();
        var progress = new StackPanel(); progress.Children.Add(status);
        int level = pet.State.BondLevel;
        progress.Children.Add(Text(level == 8 ? "已经是最亲密的伙伴。" : $"再相伴 {PetState.BondDays[level + 1] - pet.State.CheckIns.Count} 天，成为「{PetState.BondNames[level + 1]}」。", 12, true));
        var daily = MakeButton(pet.State.CheckedIn(DateOnly.FromDateTime(DateTime.Now)) ? "今天已打卡" : "打卡 · 一起吃早饭", () => { pet.CheckIn(); Rebuild(); }, "sun"); daily.Background = peach;
        progress.Children.Add(daily); content.Children.Add(Card(progress));
        var actionHeading = new DockPanel(); var stop = MakeButton("结束互动", pet.StopInteraction, "stop"); stop.FontSize = 11; stop.Padding = new Thickness(9, 5, 9, 5); stop.Background = Brushes.Transparent; stop.BorderThickness = new Thickness(0); DockPanel.SetDock(stop, Dock.Right); actionHeading.Children.Add(stop); actionHeading.Children.Add(Text("一起做点什么", 17)); content.Children.Add(actionHeading);
        var actions = new System.Windows.Controls.Primitives.UniformGrid { Columns = 4 };
        foreach (var entry in PetActions.Daily.Where(e => e.Key != "dance" || pet.CanDance))
        {
            var tile = new StackPanel(); var icon = CloudTheme.Icon(entry.Icon, 25); icon.Foreground = CloudTheme.Blue; icon.HorizontalAlignment = HorizontalAlignment.Left; tile.Children.Add(icon);
            tile.Children.Add(new TextBlock { Text = entry.Title, FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 4) });
            var b = MakeButton(entry.Title, () => pet.RunInteraction(entry.Key)); b.Content = tile; b.HorizontalContentAlignment = HorizontalAlignment.Stretch; b.Height = 88; b.Padding = new Thickness(14); actions.Children.Add(b);
        }
        content.Children.Add(actions);
        var treasures = new StackPanel(); treasures.Children.Add(Text("散步带回的小礼物", 16));
        treasures.Children.Add(Text(pet.State.Treasures.Count == 0 ? "还没有收藏。一起散步，看看它会捡到什么。" : string.Join("    ", pet.State.Treasures.GroupBy(x => x).Select(g => $"{g.Key} × {g.Count()}")), 13, true));
        content.Children.Add(Card(treasures));
        content.Children.Add(Text("摸头、戳脸、挠痒也可以直接点击角色相应位置。摇晃后会头晕，连续摇晃会躺下缓一会儿。", 12, true));
    }
    private void Preferences()
    {
        Heading("", "桌面偏好", "按你的习惯调整。关闭设置后，伙伴仍会留在桌面。");
        var panel = new StackPanel();
        void Slider(string label, double min, double max, double value, Action<double> change)
        {
            var caption = Text($"{label}  {value:0}", 14); panel.Children.Add(caption);
            var slider = new Slider { Minimum = min, Maximum = max, Value = value, Margin = new Thickness(0, 0, 0, 25), TickFrequency = label == "角色大小" ? 10 : 5, IsSnapToTickEnabled = true };
            AutomationProperties.SetName(slider, label); slider.ValueChanged += (_, _) => { caption.Text = $"{label}  {slider.Value:0}"; change(slider.Value); pet.ApplySettings(); }; panel.Children.Add(slider);
        }
        void Toggle(string label, bool value, Action<bool> change)
        {
            var box = new CheckBox { Content = label, IsChecked = value, Margin = new Thickness(0, 0, 0, 20), FontSize = 14 };
            box.Click += (_, _) => change(box.IsChecked == true); panel.Children.Add(box);
        }
        Slider("角色大小", 120, 280, pet.State.Size, x => pet.State.Size = x);
        Slider("不透明度 %", 30, 100, pet.State.Opacity * 100, x => pet.State.Opacity = x / 100);
        Toggle("始终置顶", pet.State.Topmost, x => { pet.State.Topmost = x; pet.ApplySettings(); });
        Toggle("闲时在桌边散步", pet.State.Wander, x => { pet.State.Wander = x; pet.ApplySettings(); });
        Toggle("减少动态效果", pet.State.ReducedMotion, x => { pet.State.ReducedMotion = x; pet.ApplySettings(); });
        Toggle("鼠标穿透（Ctrl+Alt+L 恢复）", pet.IsClickThrough, pet.SetClickThrough);
        content.Children.Add(Card(panel));
        content.Children.Add(Text("Ctrl+Alt+U  显示 / 隐藏     Ctrl+Alt+S  打开云朵伙伴\nCtrl+Alt+L  解除鼠标穿透     Esc  取消当前互动", 12, true));
        var buttons = new WrapPanel(); buttons.Children.Add(MakeButton("打开本地存档", () => { Directory.CreateDirectory(App.DataRoot); Process.Start(new ProcessStartInfo(App.DataRoot) { UseShellExecute = true }); }));
        buttons.Children.Add(MakeButton("退出桌面宠物", () => Application.Current.Shutdown())); content.Children.Add(buttons);
        content.Children.Add(Text("无需账号或 API Key。不会读取浏览器登录资料、监听麦克风或上传互动记录。", 12, true));
        foreach (string warning in pet.Catalog.Warnings) content.Children.Add(Text(warning, 11, true));
    }
    private void Studio()
    {
        Heading("", "角色工坊", "从一张喜欢的图开始，制作属于你的桌面伙伴。");
        var form = new StackPanel();
        form.Children.Add(Text("01   制作出图提示词", 16));
        var name = new TextBox { Text = pet.Character.Name, Margin = new Thickness(0, 0, 0, 12) }; AutomationProperties.SetName(name, "角色名称"); form.Children.Add(name);
        var description = new TextBox { Text = "沿用参考图片的发型、服装、瞳色、比例与配饰。", Margin = new Thickness(0, 0, 0, 12) }; AutomationProperties.SetName(description, "角色外观"); form.Children.Add(description);
        var style = new ComboBox();
        foreach (string id in new[] { "chibi", "3d", "adult" }) style.Items.Add(new ComboBoxItem { Content = CloudTheme.CategoryName(id), Tag = id });
        style.SelectedItem = style.Items.Cast<ComboBoxItem>().First(x => (string)x.Tag == importCategory);
        style.SelectionChanged += (_, _) => importCategory = (string)((ComboBoxItem)style.SelectedItem).Tag;
        AutomationProperties.SetName(style, "生成角色分类"); form.Children.Add(style);
        string definitions = Path.Combine(AppContext.BaseDirectory, "Studio", "character-storyboard-generator", "references");
        var pose = ReadOptions(Path.Combine(definitions, "poses.json")); var expression = ReadOptions(Path.Combine(definitions, "expressions.json"));
        AutomationProperties.SetName(pose, "动作姿势"); AutomationProperties.SetName(expression, "表情"); form.Children.Add(pose); form.Children.Add(expression);
        var prompt = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 155, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, IsReadOnly = true, FontSize = 12 };
        void Generate()
        {
            string poseText = (string)((ComboBoxItem)pose.SelectedItem).Tag, expressionText = (string)((ComboBoxItem)expression.SelectedItem).Tag;
            string rendering = importCategory switch { "3d" => "明确成年、正常成人比例的三维动画风格，立体塑形和柔和材质。", "adult" => "明确成年、正常成人比例的写实 3D 数字人，自然五官与发丝、真实布料。", _ => "沿用 Q 版头身比例与二次元画风。" };
            prompt.Text = $"为桌面宠物制作角色「{name.Text}」的透明背景 PNG。{description.Text}\n风格：{rendering}\n保持角色身份和完整身体，不举看板，不画界面、文字、背景光晕或投影。\n姿势：{poseText}\n表情：{expressionText}\n单图：512×512，身体中心 x=256，落地脚底 y=448，四周留透明边距。需要动画时生成 3 列 × 2 行、每格 512×512 的六帧图集，共 1536×1024；整组保持同一比例和基线，连续运动。走路统一朝右，脚底接地，步幅连贯。\n先确认角色校准图，再扩展动作。未成年人或年龄不明角色使用全年龄、非性化服装与动作。\n宠物动作：eat、chat、headpat、walk、pickup、shaken、shaken-strong、dizzy、bonk、ball-hit、ball-miss、think、jump、peek、curl、farewell。静态单图不可伪称六帧动画。";
        }
        Generate(); var row = new WrapPanel(); row.Children.Add(MakeButton("生成提示词", Generate)); row.Children.Add(MakeButton("复制提示词", () => { System.Windows.Clipboard.SetText(prompt.Text); pet.Play("happy", "提示词已经复制好啦。", 1500); }));
        row.Children.Add(MakeButton("导出角色包模板", ExportTemplate)); form.Children.Add(row); form.Children.Add(prompt);
        form.Children.Add(Text("这里制作提示词，不直接调用绘图服务。把提示词和参考图交给支持图片生成的工具，完成后导入。", 11, true)); content.Children.Add(Card(form));
        content.Children.Add(Text("02   导入你的图稿", 16));
        var imports = new WrapPanel(); imports.Children.Add(MakeButton("导入完整角色包", ImportPack)); imports.Children.Add(MakeButton("导入单张角色图片", ImportPortrait));
        imports.Children.Add(MakeButton("打开生成器 Skill", () => Process.Start(new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "Studio", "character-storyboard-generator")) { UseShellExecute = true })));
        content.Children.Add(imports); content.Children.Add(Text("完整角色包选择含 pet.json 的文件夹；也支持原生成器按 P01–P30 / E01–E40 命名的成果目录。单张图片可以拖动和互动，但没有绘制的动作只显示静态姿势。", 12, true));
    }
    private static ComboBox ReadOptions(string file)
    {
        var result = new ComboBox(); using var doc = JsonDocument.Parse(File.ReadAllText(file));
        foreach (var e in doc.RootElement.EnumerateArray()) result.Items.Add(new ComboBoxItem { Content = e.GetProperty("label_zh").GetString(), Tag = e.GetProperty("prompt").GetString() });
        result.SelectedIndex = 0; return result;
    }
    private void ImportPack()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "选择角色包或角色生成器的成果目录" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            Character c = File.Exists(Path.Combine(dialog.FolderName, "pet.json")) ? pet.Catalog.Import(dialog.FolderName) : Storyboard.Import(pet.Catalog, dialog.FolderName, importCategory);
            pet.SelectCharacter(c.Id); category = c.Category; page = "partners"; Rebuild();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { MessageBox.Show(this, ex.Message, "角色包未导入"); }
    }
    private void ImportPortrait()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "角色图片|*.png;*.webp;*.jpg;*.jpeg", Title = "选择透明背景角色图片" };
        if (dialog.ShowDialog(this) != true) return;
        try { var c = pet.Catalog.ImportPortrait(dialog.FileName, Path.GetFileNameWithoutExtension(dialog.FileName), importCategory); pet.SelectCharacter(c.Id); category = c.Category; page = "partners"; Rebuild(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { MessageBox.Show(this, ex.Message, "图片未导入"); }
    }
    private void ExportTemplate()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "角色清单|pet.json", FileName = "pet.json", Title = "把模板保存到一个新的角色文件夹" };
        if (dialog.ShowDialog(this) != true) return;
        var c = new Character { Id = "my-pet", Name = "我的角色", Category = importCategory, Atlas = new Sprite("atlas.png"), Motions = new() { ["walk"] = new Sprite("walk.webp", 3, 2, [160, 160, 160, 160, 160, 160]), ["headpat"] = new Sprite("headpat.webp", 3, 2, [250, 280, 390, 400, 400, 540]) } };
        File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(c, Json.Options));
        MessageBox.Show(this, "模板已保存。请补上 atlas.png、walk.webp、headpat.webp；没有画好的动作请从清单中移除，再导入文件夹。", "角色模板");
    }
}

using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Shell;
using DesktopPet.Core;

namespace DesktopPet.App;

/// <summary>The partner, calendar and settings pages share the live desktop pet's state.</summary>
public sealed partial class SettingsWindow : Window
{
    private readonly PetWindow pet;
    private readonly StackPanel content = new();
    private string page = "partners";
    private string category;
    private string compareFamily = "whale";
    private string compareOutfit = "original";
    private readonly Dictionary<string, Button> navigation = [];
    private readonly Brush muted = CloudTheme.Muted;
    private readonly Brush peach = CloudTheme.Pale;
    public SettingsWindow(PetWindow pet)
    {
        this.pet = pet; category = pet.Character.Category;
        var dayClock = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        dayClock.Tick += (_, _) => RefreshCalendarDate(); dayClock.Start(); Closed += (_, _) => dayClock.Stop();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { pet.StopInteraction(); e.Handled = true; } };
        Title = "DesktopPet · 云朵伙伴"; Icon = CloudTheme.AppIcon;
        Foreground = CloudTheme.Ink; FontFamily = new FontFamily("Segoe UI Variable, Microsoft YaHei UI"); FontSize = 13;
        Width = Math.Min(1120, SystemParameters.WorkArea.Width - 40); Height = Math.Min(850, SystemParameters.WorkArea.Height - 40); MinWidth = 920; MinHeight = 650;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; Background = CloudTheme.Brush("#FFFAF8");
        WindowStyle = WindowStyle.None;
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 48, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(24) });
        var root = new Grid { Background = Background }; root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) }); root.RowDefinitions.Add(new RowDefinition()); Content = root;
        var titlebar = new DockPanel { Margin = new Thickness(22, 8, 12, 8) }; root.Children.Add(titlebar);
        var close = MakeButton("关闭设置", Close); close.Content = CloudTheme.Icon("close", 16); close.Width = 34; close.Padding = new Thickness(0); close.Margin = new Thickness(4, 0, 0, 0); close.Background = Brushes.Transparent; close.BorderThickness = new Thickness(0); DockPanel.SetDock(close, Dock.Right); WindowChrome.SetIsHitTestVisibleInChrome(close, true); titlebar.Children.Add(close);
        var minimize = MakeButton("最小化", () => WindowState = WindowState.Minimized); minimize.Content = CloudTheme.Icon("minimize", 16); minimize.Width = 34; minimize.Padding = new Thickness(0); minimize.Margin = new Thickness(0); minimize.Background = Brushes.Transparent; minimize.BorderThickness = new Thickness(0); DockPanel.SetDock(minimize, Dock.Right); WindowChrome.SetIsHitTestVisibleInChrome(minimize, true); titlebar.Children.Add(minimize);
        titlebar.Children.Add(new TextBlock { Text = "云朵伙伴", Foreground = muted, FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        var layout = new Grid(); layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) }); layout.ColumnDefinitions.Add(new ColumnDefinition()); Grid.SetRow(layout, 1); root.Children.Add(layout);
        var sideSurface = new Border { Background = CloudTheme.Brush("#F9EEF1"), BorderBrush = CloudTheme.Line, BorderThickness = new Thickness(0, 0, 1, 0) }; layout.Children.Add(sideSurface);
        var sidebar = new DockPanel { Margin = new Thickness(15, 25, 15, 24) }; sideSurface.Child = sidebar;
        var brand = new StackPanel { Margin = new Thickness(12, 0, 0, 29) };
        var mark = new CloudIcon { Width = 64, Height = 58, HorizontalAlignment = HorizontalAlignment.Left }; brand.Children.Add(mark);
        brand.Children.Add(new TextBlock { Text = "云朵伙伴", FontSize = 21, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 12, 0, 5) });
        DockPanel.SetDock(brand, Dock.Top); sidebar.Children.Add(brand);
        var version = typeof(SettingsWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "开发版";
        var foot = new StackPanel { Margin = new Thickness(12, 0, 0, 0) }; foot.Children.Add(PresenceControl()); foot.Children.Add(new TextBlock { Text = $"云朵伙伴 / DesktopPet v{version}", FontSize = 10, Foreground = muted, Margin = new Thickness(0, 8, 0, 0) });
        DockPanel.SetDock(foot, Dock.Bottom); sidebar.Children.Add(foot);
        var nav = new StackPanel(); sidebar.Children.Add(nav);
        foreach (var (id, label, icon) in new[] { ("partners", "我的伙伴", "heart"), ("life", "陪伴日常", "sun"), ("members", "会员中心", "member"), ("preferences", "设定", "settings") })
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
        content.Children.Add(new TextBlock { Text = title, FontSize = 29, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 7) }); if (!string.IsNullOrWhiteSpace(description)) content.Children.Add(Text(description, 12, true));
    }
    private Border Card(UIElement child, Brush? background = null) => new() { Background = background ?? Brushes.White, BorderBrush = CloudTheme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(24), Padding = new Thickness(22), Margin = new Thickness(0, 12, 0, 20), Child = child };
    private void Navigate(string destination)
    {
        page = destination; Rebuild();
    }
    private void Rebuild()
    {
        content.Children.Clear(); ClearCompanionBindings();
        foreach (var (id, b) in navigation) { b.Background = id == (page == "profile" ? "partners" : page) ? CloudTheme.Brush("#F3DCE5") : Brushes.Transparent; b.Foreground = id == (page == "profile" ? "partners" : page) ? CloudTheme.Blue : CloudTheme.Ink; b.FontWeight = id == (page == "profile" ? "partners" : page) ? FontWeights.SemiBold : FontWeights.Normal; }
        switch (page) { case "members": Members(); break; case "profile": BuildCompanionProfile(); break; case "life": Life(); break; case "preferences": Preferences(); break; default: Partners(); break; }
    }
    private Grid Stage(Character character, double height, string outfit = "original")
    {
        var stage = new Grid { Height = height, ClipToBounds = true }; stage.Children.Add(new CloudScenery());
        var frame = character.Resolve(outfit, "idle", 0);
        stage.Children.Add(new Image { Source = pet.Art.Frame(character, frame.Sprite, frame.Frame), Stretch = Stretch.Uniform, Margin = new Thickness(12, 4, 12, 2) }); return stage;
    }
    private void Partners()
    {
        Heading("", "我的伙伴", "");
        var hero = new Grid(); hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(236) }); hero.ColumnDefinitions.Add(new ColumnDefinition());
        hero.Children.Add(Stage(pet.Character, 182, pet.State.Outfit));
        var intro = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 0, 0) }; Grid.SetColumn(intro, 1); hero.Children.Add(intro);
        var badges = new WrapPanel(); badges.Children.Add(CloudTheme.Badge("正在陪伴")); badges.Children.Add(CloudTheme.Badge(CloudTheme.CategoryName(pet.Character.Category))); intro.Children.Add(badges);
        var name = Text(CompanionName(pet.Character), 25); name.FontWeight = FontWeights.SemiBold; name.Margin = new Thickness(0, 10, 0, 4); intro.Children.Add(name);
        heroBond = Text("", 12, true); intro.Children.Add(heroBond); RefreshCompanion();
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
        var heroCard = Card(hero, CloudTheme.Sky()); heroCard.Padding = new Thickness(15, 12, 18, 12);
        var categoryRow = new DockPanel { Margin = new Thickness(0, 0, 0, 11) };
        var compare = MakeButton("伙伴档案", () => ShowProfile(pet.Character.FamilyId)); compare.Background = Brushes.Transparent; compare.BorderThickness = new Thickness(0); compare.FontSize = 11; DockPanel.SetDock(compare, Dock.Right); categoryRow.Children.Add(compare);
        var filters = new StackPanel { Orientation = Orientation.Horizontal }; categoryRow.Children.Add(new Border { Background = CloudTheme.Brush("#F3E6EB"), Padding = new Thickness(3), CornerRadius = new CornerRadius(11), HorizontalAlignment = HorizontalAlignment.Left, Child = filters });
        foreach (var (id, glyph) in new[] { (CharacterStyles.Chibi, "heart"), (CharacterStyles.Realistic, "person") })
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
            if (selected) { b.BorderBrush = CloudTheme.Blue; b.Background = CloudTheme.Brush("#FFF1F5"); }
            b.PreviewMouseRightButtonUp += (_, e) => { e.Handled = true; ShowProfile(character.FamilyId); };
            b.PreviewKeyDown += (_, e) => { if (e.Key == Key.Apps || e.Key == Key.F10 && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) { e.Handled = true; ShowProfile(character.FamilyId); } };
            tiles.Children.Add(b);
        }
        content.Children.Add(tiles);
        content.Children.Add(heroCard);
    }

    public void RefreshStatus() => RefreshCompanion();
    public void RefreshLife() { if (page == "life") Rebuild(); else RefreshStatus(); }
    private void Life() => BuildCompanionLife();
    private void Preferences()
    {
        Heading("", "设定", "");
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
        Toggle("落地与闲时散步", pet.State.Wander, x => { pet.State.Wander = x; pet.ApplySettings(); });
        Toggle("闲置一分钟后躲藏", pet.State.AutoHide, x => { pet.State.AutoHide = x; pet.Save(); });
        Toggle("减少动态效果", pet.State.ReducedMotion, x => { pet.State.ReducedMotion = x; pet.ApplySettings(); });
        Toggle("鼠标穿透（Ctrl+Alt+L 恢复）", pet.IsClickThrough, pet.SetClickThrough);
        content.Children.Add(Card(panel));
        content.Children.Add(Card(WorkSettingsPanel()));
        content.Children.Add(Text("AI 模型接口", 17));
        content.Children.Add(Card(pet.Chat.SettingsPanel()));
    }
}

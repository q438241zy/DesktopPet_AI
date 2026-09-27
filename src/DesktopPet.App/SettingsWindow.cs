using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;
using System.Windows.Media;
using DesktopPet.Core;

namespace DesktopPet.App;

/// <summary>The character, life and creator controls share the live desktop pet's state.</summary>
public sealed class SettingsWindow : Window
{
    private readonly PetWindow pet;
    private readonly StackPanel content = new();
    private TextBlock status = new();
    private string page = "partners";
    private readonly Brush muted = new SolidColorBrush(Color.FromRgb(138, 133, 130));
    private readonly Brush peach = new SolidColorBrush(Color.FromRgb(245, 227, 214));
    public SettingsWindow(PetWindow pet)
    {
        this.pet = pet; Title = "DesktopPet · 宠物之家"; Width = 1000; Height = 780; MinWidth = 850; MinHeight = 650;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; Background = new SolidColorBrush(Color.FromRgb(250, 248, 244));
        var layout = new Grid { Background = Background }; layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) }); layout.ColumnDefinitions.Add(new ColumnDefinition()); Content = layout;
        var sidebar = new DockPanel { Margin = new Thickness(22, 30, 18, 22) }; layout.Children.Add(sidebar);
        var brand = new StackPanel { Margin = new Thickness(0, 0, 0, 35) };
        brand.Children.Add(new TextBlock { Text = "✦  DesktopPet", FontSize = 19, FontWeight = FontWeights.SemiBold });
        brand.Children.Add(new TextBlock { Text = "桌 边 伙 伴", Foreground = muted, FontSize = 11, Margin = new Thickness(26, 8, 0, 0) });
        DockPanel.SetDock(brand, Dock.Top); sidebar.Children.Add(brand);
        var foot = new StackPanel(); foot.Children.Add(new TextBlock { Text = "只陪伴，不催促。", FontSize = 12, Foreground = muted }); foot.Children.Add(new TextBlock { Text = "LOCAL FIRST  ·  1.0", FontSize = 9, Foreground = muted, Margin = new Thickness(0, 8, 0, 0) });
        DockPanel.SetDock(foot, Dock.Bottom); sidebar.Children.Add(foot);
        var nav = new StackPanel(); sidebar.Children.Add(nav);
        foreach (var (id, label) in new[] { ("partners", "♡   我的伙伴"), ("life", "☀   陪伴日常"), ("studio", "✎   角色工坊"), ("preferences", "⚙   桌面偏好") })
        {
            var b = MakeButton(label, () => { page = id; Rebuild(); }); b.HorizontalContentAlignment = HorizontalAlignment.Left; b.Background = Brushes.Transparent; b.BorderThickness = new Thickness(0); b.Margin = new Thickness(0, 0, 0, 10); nav.Children.Add(b);
        }
        var scroll = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(24, 30, 32, 24) };
        Grid.SetColumn(scroll, 1); layout.Children.Add(scroll); Rebuild();
    }
    private static Button MakeButton(string label, Action clicked)
    { var button = new Button { Content = label }; button.Click += (_, _) => clicked(); return button; }
    private TextBlock Text(string text, double size = 13, bool quiet = false) => new() { Text = text, FontSize = size, Foreground = quiet ? muted : Foreground, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10), LineHeight = size * 1.6 };
    private void Heading(string eyebrow, string title, string description)
    {
        content.Children.Add(Text(eyebrow, 10, true)); content.Children.Add(new TextBlock { Text = title, FontSize = 29, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) }); content.Children.Add(Text(description, 13, true));
    }
    private Border Card(UIElement child, Brush? background = null) => new() { Background = background ?? Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(233, 229, 223)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Padding = new Thickness(20), Margin = new Thickness(0, 12, 0, 10), Child = child };
    private void Rebuild()
    {
        content.Children.Clear();
        switch (page) { case "life": Life(); break; case "studio": Studio(); break; case "preferences": Preferences(); break; default: Partners(); break; }
    }
    private void Partners()
    {
        Heading("YOUR LITTLE COMPANION", "桌边，有个小伙伴。", "选一位朋友，让普通的一天多一点回应。点击角色即可来到桌面。");
        var hero = new Grid(); hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) }); hero.ColumnDefinitions.Add(new ColumnDefinition());
        var resolved = pet.Character.Resolve(pet.State.Outfit, "idle", 0);
        hero.Children.Add(new Image { Source = pet.Art.Frame(pet.Character, resolved.Sprite, resolved.Frame), Width = 132, Height = 132 });
        var intro = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) }; Grid.SetColumn(intro, 1); hero.Children.Add(intro);
        intro.Children.Add(Text("正在陪你  /  ON YOUR DESKTOP", 10, true)); intro.Children.Add(Text(pet.Character.Name, 24)); intro.Children.Add(Text($"{pet.State.BondName}  ·  相伴 {pet.State.CheckIns.Count} 天", 12, true));
        var outfits = new ComboBox { Width = 220, HorizontalAlignment = HorizontalAlignment.Left };
        outfits.Items.Add(new ComboBoxItem { Content = "原装", Tag = "original" });
        foreach (var entry in pet.Character.Outfits) outfits.Items.Add(new ComboBoxItem { Content = entry.Value.Name, Tag = entry.Key });
        outfits.SelectedItem = outfits.Items.Cast<ComboBoxItem>().FirstOrDefault(x => (string)x.Tag == pet.State.Outfit) ?? outfits.Items[0];
        AutomationProperties.SetName(outfits, "选择服装");
        outfits.SelectionChanged += (_, _) => { pet.State.Outfits[pet.State.Character] = (string)((ComboBoxItem)outfits.SelectedItem).Tag; pet.ApplySettings(); Rebuild(); };
        intro.Children.Add(outfits); content.Children.Add(Card(hero, new SolidColorBrush(Color.FromRgb(246, 237, 225))));
        var tiles = new WrapPanel();
        foreach (var character in pet.Catalog.Characters)
        {
            var tile = new StackPanel();
            tile.Children.Add(new Image { Source = pet.Art.Frame(character, character.Atlas, 0), Width = 103, Height = 96 });
            tile.Children.Add(new TextBlock { Text = character.Name, FontSize = 12, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 4, 0, 0) });
            var b = MakeButton("", () => { pet.SelectCharacter(character.Id); Rebuild(); }); b.Content = tile; b.Width = 130; b.Height = 144; b.Padding = new Thickness(8); b.Margin = new Thickness(0, 0, 10, 10);
            if (character.Id == pet.State.Character) { b.BorderBrush = new SolidColorBrush(Color.FromRgb(208, 158, 124)); b.Background = new SolidColorBrush(Color.FromRgb(252, 242, 229)); b.BorderThickness = new Thickness(2); }
            AutomationProperties.SetName(b, "选择角色 " + character.Name); tiles.Children.Add(b);
        }
        content.Children.Add(tiles);
        content.Children.Add(Text("左键摸摸 · 拖动抱起 · 右键互动菜单。婚纱有独立的走路与摸头动作，其余缺少的服装动作使用原装画稿。", 11, true));
    }
    public void RefreshStatus()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        status.Text = $"{pet.State.BondName}  ·  Lv.{pet.State.BondLevel + 1}\n累计 {pet.State.CheckIns.Count} 天    连续 {pet.State.Streak(today)} 天\n初次相遇：{pet.State.AdoptedAt}";
    }
    private void Life()
    {
        status = new TextBlock();
        Heading("LITTLE MOMENTS, TOGETHER", "把日常，慢慢过成回忆。", "打卡是一起吃早饭。偶尔忘记也没关系，已经积累的亲密度一直都在。");
        status.FontSize = 16; status.LineHeight = 28; RefreshStatus();
        var progress = new StackPanel(); progress.Children.Add(status);
        int level = pet.State.BondLevel;
        progress.Children.Add(Text(level == 8 ? "已经是最亲密的伙伴。" : $"再相伴 {PetState.BondDays[level + 1] - pet.State.CheckIns.Count} 天，成为「{PetState.BondNames[level + 1]}」。", 12, true));
        var daily = MakeButton(pet.State.CheckedIn(DateOnly.FromDateTime(DateTime.Now)) ? "✓ 今天已打卡" : "☀ 打卡 · 一起吃早饭", () => { pet.CheckIn(); Rebuild(); }); daily.Background = peach;
        progress.Children.Add(daily); content.Children.Add(Card(progress));
        content.Children.Add(Text("陪它玩一会儿", 17)); var actions = new WrapPanel();
        foreach (var (key, label) in new[] { ("headpat", "♡ 摸摸头"), ("poke", "◌ 戳戳脸"), ("tickle", "✧ 挠痒痒"), ("snack", "♨ 喂零食"), ("chat", "☏ 聊聊天"), ("ball", "● 一起玩球"), ("blocks", "▦ 搭积木"), ("walk", "⌁ 散步寻宝"), ("peek", "⌂ 躲猫猫"), ("letter", "✉ 纪念卡片"), ("rest", "☾ 歇一会儿") })
        { var b = MakeButton(label, () => pet.RunInteraction(key)); b.MinWidth = 133; actions.Children.Add(b); }
        content.Children.Add(actions);
        var treasures = new StackPanel(); treasures.Children.Add(Text("散步带回的小礼物", 16));
        treasures.Children.Add(Text(pet.State.Treasures.Count == 0 ? "还没有收藏。一起散步，看看它会捡到什么。" : string.Join("    ", pet.State.Treasures.GroupBy(x => x).Select(g => $"{g.Key} × {g.Count()}")), 13, true));
        content.Children.Add(Card(treasures));
        content.Children.Add(Text("摸头、戳脸、挠痒也可以直接点击角色相应位置。摇晃后会头晕，连续摇晃会躺下缓一会儿。", 12, true));
    }
    private void Preferences()
    {
        Heading("MAKE ROOM FOR A FRIEND", "按你的习惯，安静陪伴。", "设置立即生效。关闭这扇窗口后，宠物仍会留在桌面。");
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
        content.Children.Add(Text("Ctrl+Alt+U  显示 / 隐藏     Ctrl+Alt+S  打开宠物之家\nCtrl+Alt+L  解除鼠标穿透     Esc  取消当前互动", 12, true));
        var buttons = new WrapPanel(); buttons.Children.Add(MakeButton("打开本地存档", () => { Directory.CreateDirectory(App.DataRoot); Process.Start(new ProcessStartInfo(App.DataRoot) { UseShellExecute = true }); }));
        buttons.Children.Add(MakeButton("退出桌面宠物", () => Application.Current.Shutdown())); content.Children.Add(buttons);
        content.Children.Add(Text("无需账号或 API Key。不会读取浏览器登录资料、监听麦克风或上传互动记录。", 12, true));
        foreach (string warning in pet.Catalog.Warnings) content.Children.Add(Text(warning, 11, true));
    }
    private void Studio()
    {
        Heading("CHARACTER STUDIO", "把喜欢的角色，带到桌边。", "保留原角色生成器的 30 种姿势与 40 种表情。先制作图稿，再导入桌面宠物。");
        var form = new StackPanel();
        form.Children.Add(Text("01   制作出图提示词", 16));
        var name = new TextBox { Text = pet.Character.Name, Margin = new Thickness(0, 0, 0, 12) }; AutomationProperties.SetName(name, "角色名称"); form.Children.Add(name);
        var description = new TextBox { Text = "沿用参考图片的发型、服装、瞳色、比例与配饰。", Margin = new Thickness(0, 0, 0, 12) }; AutomationProperties.SetName(description, "角色外观"); form.Children.Add(description);
        string definitions = Path.Combine(AppContext.BaseDirectory, "Studio", "character-storyboard-generator", "references");
        var pose = ReadOptions(Path.Combine(definitions, "poses.json")); var expression = ReadOptions(Path.Combine(definitions, "expressions.json"));
        AutomationProperties.SetName(pose, "动作姿势"); AutomationProperties.SetName(expression, "表情"); form.Children.Add(pose); form.Children.Add(expression);
        var prompt = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 155, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, IsReadOnly = true, FontSize = 12 };
        void Generate()
        {
            string poseText = (string)((ComboBoxItem)pose.SelectedItem).Tag, expressionText = (string)((ComboBoxItem)expression.SelectedItem).Tag;
            prompt.Text = $"为桌面宠物制作角色「{name.Text}」的透明背景 PNG。{description.Text}\n保持参考角色身份、头身比例和完整身体，不举看板，不画任何界面或文字。\n姿势：{poseText}\n表情：{expressionText}\n单图：512×512，身体中心 x=256，落地脚底 y=448，四周留透明边距。需要动画时生成 3 列 × 2 行、每格 512×512 的六帧图集，共 1536×1024；整组保持同一比例和基线，连续运动。\n先确认角色校准图，再扩展动作。未成年人或年龄不明角色使用全年龄、非性化服装与动作。\n宠物动作：eat、chat、headpat、walk、pickup、shaken、shaken-strong、dizzy、bonk、ball-hit、ball-miss、think、jump、peek、curl、farewell。新动作可补 meal、pounce、poke、tickle、kick。静态单图不可伪称六帧动画。";
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
            Character c = File.Exists(Path.Combine(dialog.FolderName, "pet.json")) ? pet.Catalog.Import(dialog.FolderName) : Storyboard.Import(pet.Catalog, dialog.FolderName);
            pet.SelectCharacter(c.Id); page = "partners"; Rebuild();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { MessageBox.Show(this, ex.Message, "角色包未导入"); }
    }
    private void ImportPortrait()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "角色图片|*.png;*.webp;*.jpg;*.jpeg", Title = "选择透明背景角色图片" };
        if (dialog.ShowDialog(this) != true) return;
        try { var c = pet.Catalog.ImportPortrait(dialog.FileName, Path.GetFileNameWithoutExtension(dialog.FileName)); pet.SelectCharacter(c.Id); page = "partners"; Rebuild(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { MessageBox.Show(this, ex.Message, "图片未导入"); }
    }
    private void ExportTemplate()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "角色清单|pet.json", FileName = "pet.json", Title = "把模板保存到一个新的角色文件夹" };
        if (dialog.ShowDialog(this) != true) return;
        var c = new Character { Id = "my-pet", Name = "我的角色", Atlas = new Sprite("atlas.png"), Motions = new() { ["walk"] = new Sprite("walk.webp", 3, 2, [160, 160, 160, 160, 160, 160]), ["headpat"] = new Sprite("headpat.webp", 3, 2, [250, 280, 390, 400, 400, 540]) } };
        File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(c, Json.Options));
        MessageBox.Show(this, "模板已保存。请补上 atlas.png、walk.webp、headpat.webp；没有画好的动作请从清单中移除，再导入文件夹。", "角色模板");
    }
}

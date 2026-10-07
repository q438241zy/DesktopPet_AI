using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core;

namespace DesktopPet.App;

public sealed partial class PetWindow
{
    internal void OpenCompanionPhoto()
    {
        CompanionActivity();
        if (photoWindow is { } opened) { opened.Activate(); return; }
        var first = Character; string firstOutfit = State.Outfit;
        var others = Catalog.Characters.Where(c => c.Category == first.Category && c.FamilyId != first.FamilyId).ToArray();
        if (others.Length == 0) { Say("还没有其他同风格伙伴。", 2500); return; }
        var panel = new Grid { Margin = new Thickness(24) }; panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); panel.RowDefinitions.Add(new RowDefinition()); panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var controls = new StackPanel(); panel.Children.Add(controls);
        controls.Children.Add(new TextBlock { Text = "一起合照", FontSize = 25, Foreground = CloudTheme.Ink, Margin = new Thickness(0, 0, 0, 15) });
        var selectors = new DockPanel(); controls.Children.Add(selectors);
        var partner = new ComboBox { ItemsSource = others, DisplayMemberPath = "Name", SelectedIndex = 0, MinWidth = 165, Margin = new Thickness(0, 0, 12, 10) }; AutomationProperties.SetName(partner, "合照伙伴"); selectors.Children.Add(partner);
        var frames = new ComboBox { ItemsSource = new[] { "奶油相纸", "玫瑰云朵", "浅玉留影" }, SelectedIndex = 0, MinWidth = 130, Margin = new Thickness(0, 0, 0, 10) }; AutomationProperties.SetName(frames, "合照相框"); selectors.Children.Add(frames);
        var caption = new TextBox { Text = "今天也一起", MaxLength = 60, MinHeight = 38, VerticalContentAlignment = VerticalAlignment.Center }; AutomationProperties.SetName(caption, "合照文字"); controls.Children.Add(caption);
        var preview = new Image { Stretch = Stretch.Uniform, Margin = new Thickness(0, 15, 0, 15) }; Grid.SetRow(preview, 1); panel.Children.Add(preview);
        BitmapSource? result = null;
        void Refresh()
        {
            if (partner.SelectedItem is not Character other) return;
            result = CreateCompanionPhoto(first, firstOutfit, other, State.Outfits.GetValueOrDefault(other.Id, "original"), caption.Text, frames.SelectedIndex); preview.Source = result;
        }
        partner.SelectionChanged += (_, _) => Refresh(); frames.SelectionChanged += (_, _) => Refresh(); caption.TextChanged += (_, _) => Refresh();
        var footer = new DockPanel(); Grid.SetRow(footer, 2); panel.Children.Add(footer);
        var message = new TextBlock { Text = "PNG · 1200 × 1400", Foreground = CloudTheme.Muted, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 370 };
        var save = new Button { Content = "保存合照", Padding = new Thickness(19, 11, 19, 11) }; AutomationProperties.SetName(save, "保存合照"); DockPanel.SetDock(save, Dock.Right); footer.Children.Add(save); footer.Children.Add(message);
        var window = new Window { Title = "云朵伙伴 · 合照", Width = 640, Height = Math.Min(860, SystemParameters.WorkArea.Height - 40), MinWidth = 520, MinHeight = 580, Background = CloudTheme.Cream, Icon = CloudTheme.AppIcon, Content = panel, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        photoWindow = window; window.Closed += (_, _) => { if (ReferenceEquals(photoWindow, window)) photoWindow = null; CompanionActivity(); };
        save.Click += (_, _) =>
        {
            if (result is null) return;
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "PNG 图片|*.png", DefaultExt = ".png", FileName = $"云朵合照-{DateTime.Now:yyyyMMdd-HHmmss}.png", InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) };
            if (dialog.ShowDialog(window) != true) return;
            try { SaveCompanionPhoto(result, dialog.FileName); message.Text = "已保存合照"; AwardCompanion(1, "一起合照", "photo", 60); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { message.Text = "图片未保存，请选择可写入的位置。"; }
        };
        Refresh(); window.Show();
    }
    internal BitmapSource CreateCompanionPhoto(Character first, string firstOutfit, Character second, string secondOutfit, string caption, int frame)
    {
        var visual = new DrawingVisual(); using (var dc = visual.RenderOpen())
        {
            string edge = frame switch { 1 => "#F1D4E0", 2 => "#CFE5DA", _ => "#EEE3D9" };
            dc.DrawRectangle(CloudTheme.Brush(edge), null, new Rect(0, 0, 1200, 1400));
            dc.DrawRoundedRectangle(CloudTheme.Brush("#FFFCF9"), new Pen(Brushes.White, 6), new Rect(38, 38, 1124, 1324), 22, 22);
            dc.DrawRoundedRectangle(CloudTheme.Brush(frame == 2 ? "#EDF5EF" : "#FCF0F4"), null, new Rect(76, 76, 1048, 1094), 12, 12);
            void Label(string value, double y, double size, Brush? color = null)
            {
                var text = new FormattedText(value, CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight, new Typeface("Microsoft YaHei UI"), size, color ?? CloudTheme.Ink, 1) { MaxTextWidth = 960, TextAlignment = TextAlignment.Center };
                dc.DrawText(text, new Point(120, y));
            }
            Label("云朵伙伴", 112, 26, CloudTheme.Muted);
            // A single pair scale preserves both characters' source proportions.
            // Wide hair, skirts and tails must fit too, not just the reference body height.
            double FitHeight(Character character, string outfit)
            {
                if (character.FiveFor(outfit) is not { } art || !art.Poses.TryGetValue("photo", out var pose)) return double.PositiveInfinity;
                var sheet = art.Atlases[pose.Atlas]; var bitmap = Art.Frame(character, sheet, pose.Frame);
                return 240 * sheet.ReferenceHeightPixels / Math.Max(pose.FootX, bitmap.PixelWidth - pose.FootX);
            }
            double height = Math.Min(first.Category == CharacterStyles.Realistic ? 830 : 625, Math.Min(FitHeight(first, firstOutfit), FitHeight(second, secondOutfit)));
            void DrawPet(Character character, string outfit, double center)
            {
                if (character.FiveFor(outfit) is { } art && art.Poses.TryGetValue("photo", out var pose))
                {
                    var sheet = art.Atlases[pose.Atlas]; var bitmap = Art.Frame(character, sheet, pose.Frame); double scale = height / sheet.ReferenceHeightPixels;
                    dc.DrawImage(bitmap, new Rect(center - pose.FootX * scale, 1125 - pose.FootY * scale, bitmap.PixelWidth * scale, bitmap.PixelHeight * scale));
                }
                else
                {
                    var poseFrame = character.Resolve(outfit, "idle", 0); var bitmap = Art.Frame(character, poseFrame.Sprite, poseFrame.Frame); double scale = Math.Min(470d / bitmap.PixelWidth, height / bitmap.PixelHeight);
                    dc.DrawImage(bitmap, new Rect(center - bitmap.PixelWidth * scale / 2, 1125 - bitmap.PixelHeight * scale, bitmap.PixelWidth * scale, bitmap.PixelHeight * scale));
                }
            }
            dc.PushClip(new RectangleGeometry(new Rect(76, 185, 1048, 970)));
            DrawPet(first, firstOutfit, 353); DrawPet(second, secondOutfit, 847); dc.Pop();
            Label(first.Name.Split('·')[0].Trim() + "  ♡  " + second.Name.Split('·')[0].Trim(), 213, 30);
            Label(string.IsNullOrWhiteSpace(caption) ? "今天也一起" : caption, 1195, 30);
            Label(DateTime.Now.ToString("yyyy.MM.dd"), 1310, 18, CloudTheme.Muted);
        }
        var result = new RenderTargetBitmap(1200, 1400, 96, 96, PixelFormats.Pbgra32); result.Render(visual); result.Freeze(); return result;
    }
    internal static void SaveCompanionPhoto(BitmapSource image, string path)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using var file = File.Create(path); encoder.Save(file);
    }
}

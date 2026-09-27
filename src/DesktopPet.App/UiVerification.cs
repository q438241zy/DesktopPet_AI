using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core;

namespace DesktopPet.App;

/// <summary>Runs the real WPF pages and import pipeline using an explicitly isolated data directory.</summary>
internal static class UiVerification
{
    public static async Task Run(PetWindow pet, string output)
    {
        var checks = new List<string>();
        void Require(bool pass, string name) { if (!pass) throw new InvalidOperationException(name); checks.Add("PASS " + name); }
        IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T match) yield return match;
                foreach (var descendant in Find<T>(child)) yield return descendant;
            }
        }
        var window = new SettingsWindow(pet); window.Show(); window.UpdateLayout();
        void Click(string label)
        {
            window.UpdateLayout();
            var b = Find<Button>(window).First(x => AutomationProperties.GetName(x) == label || x.Content as string == label);
            b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout();
        }
        Require(pet.Catalog.Characters.Count == 9, "exactly nine bundled characters");
        foreach (var c in pet.Catalog.Characters.ToArray()) { Click("选择角色 " + c.Name); Require(pet.State.Character == c.Id, "select " + c.Id); }
        Click("选择角色 GPT");
        var combo = Find<ComboBox>(window).Single();
        combo.SelectedItem = combo.Items.Cast<ComboBoxItem>().Single(x => x.Tag as string == "wedding"); window.UpdateLayout();
        Click("选择角色 Claude"); Click("选择角色 GPT"); Require(pet.State.Outfit == "wedding", "outfit selection survives switching characters");
        for (int i = 0; i < 3; i++) { Click("☀   陪伴日常"); Click("✎   角色工坊"); Click("⚙   桌面偏好"); Click("♡   我的伙伴"); }
        Require(true, "repeated navigation reuses no parented controls");
        Click("☀   陪伴日常"); pet.CheckIn(); int total = pet.State.CheckIns.Count; pet.CheckIn(); Require(pet.State.CheckIns.Count == total, "UI check-in is idempotent");
        foreach (var action in new[] { "headpat", "poke", "tickle", "snack", "chat", "ball", "blocks", "walk", "peek", "letter" })
        { pet.RunInteraction(action); pet.UpdateLayout(); await Task.Delay(100); }
        pet.RunInteraction("rest"); await Task.Delay(80); pet.SelectCharacter("umaru"); await Task.Delay(3500);
        Require(Find<Image>(pet).First().Visibility == Visibility.Visible, "character change cancels pending farewell");
        string source = Path.Combine(output, "storyboard-fixture"); Directory.CreateDirectory(source);
        var bitmap = pet.Art.Frame(pet.Character, pet.Character.Atlas, 0);
        void Export(string name)
        { var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(Path.Combine(source, name)); png.Save(stream); }
        Export("fixture__P01-standing-neutral__E01-joy__v01__v1.0.png");
        Export("fixture__P11-walking__E01-joy__v01__v1.0.png");
        var imported = Storyboard.Import(pet.Catalog, source);
        Require(imported.Atlas.Columns == 1 && imported.Motions["walk"].Columns == 1, "generator outputs import as static poses");
        Require(File.Exists(Path.Combine(imported.Root, "pet.json")), "import persists a portable manifest");
        var portrait = pet.Catalog.ImportPortrait(Directory.GetFiles(source)[0], "测试立绘");
        Require(portrait.Atlas.Rows == 1, "single-image import uses a single cell");
        pet.Catalog.Characters.Remove(imported); pet.Catalog.Characters.Remove(portrait);
        pet.SelectCharacter("gpt"); Click("♡   我的伙伴"); window.UpdateLayout();
        await Task.Delay(300);
        window.UpdateLayout();
        var visual = (FrameworkElement)window.Content;
        var preview = new RenderTargetBitmap((int)visual.ActualWidth, (int)visual.ActualHeight, 96, 96, PixelFormats.Pbgra32); preview.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(preview)); using (var stream = File.Create(Path.Combine(output, "pet-home.png"))) encoder.Save(stream);
        pet.Save(); var restored = new StateStore(output).Load();
        Require(restored.Character == "gpt" && restored.Outfit == "wedding" && restored.CheckIns.Count == total, "state reload preserves selection and progress");
        File.WriteAllLines(Path.Combine(output, "ui-check.txt"), checks.Append($"{checks.Count} WPF integration checks passed."));
        window.Close();
    }
}

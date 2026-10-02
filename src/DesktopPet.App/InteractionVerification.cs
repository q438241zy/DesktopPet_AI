using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core;

namespace DesktopPet.App;

internal static class InteractionVerification
{
    public static async Task Run(PetWindow pet, string output)
    {
        var checks = new List<string>();
        void Require(bool pass, string message) { if (!pass) throw new InvalidOperationException(message); checks.Add("PASS " + message); }
        IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T typed) yield return typed;
                foreach (var item in Find<T>(child)) yield return item;
            }
        }
        async Task Until(Func<bool> test, string message)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (!test()) { if (timer.Elapsed.TotalSeconds > 7) throw new TimeoutException(message); await Task.Delay(30); }
        }
        void Capture(FrameworkElement visual, string name)
        {
            visual.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)visual.ActualWidth, (int)visual.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(Path.Combine(output, name + ".png")); png.Save(stream);
        }
        var proof = new Canvas { Width = 1600, Height = 660, Background = CloudTheme.Brush("#F5F5F7") };
        int proofColumn = 0;
        foreach (var adult in pet.Catalog.Characters.Where(c => c.Category == CharacterStyles.Realistic && PortraitRig.SupportsDance(c.Category, c.FamilyId)))
        {
            int proofRow = 0;
            foreach (string outfit in new[] { "original", "swim", "wedding" })
            {
                var art = adult.Resolve(outfit, "dance", 0);
                var view = new RigVisual(pet.Art.Frame(adult, art.Sprite, art.Frame), adult.FamilyId, outfit, danceRig: art.Sprite.DanceRig) { Width = 200, Height = 200 };
                view.Update(750); proof.Children.Add(view); Canvas.SetLeft(view, proofColumn * 200); Canvas.SetTop(view, proofRow * 220);
                var label = new TextBlock { Text = adult.FamilyId + " / " + outfit, FontSize = 11, Foreground = CloudTheme.Muted, Width = 200, TextAlignment = TextAlignment.Center };
                proof.Children.Add(label); Canvas.SetLeft(label, proofColumn * 200); Canvas.SetTop(label, proofRow * 220 + 202); proofRow++;
            }
            proofColumn++;
        }
        proof.Measure(new Size(proof.Width, proof.Height)); proof.Arrange(new Rect(0, 0, proof.Width, proof.Height));
        Capture(proof, "adult-dance-wardrobe");
        pet.IsHitTestVisible = false; pet.State.CheckIn(DateOnly.FromDateTime(DateTime.Now)); pet.State.Wander = false; pet.State.ReducedMotion = false; pet.ApplySettings();
        foreach (var character in pet.Catalog.Characters)
            foreach (string outfit in new[] { "original", "swim", "wedding" })
            {
                pet.SelectCharacter(character.Id); pet.State.Outfits[character.Id] = outfit; pet.ApplySettings();
                pet.Left = pet.WorkArea.Left + pet.WorkArea.Width / 2 - 280;
                pet.Top = pet.WorkArea.Bottom - 468;
                pet.RunInteraction("dance"); await Task.Delay(260);
                var sprite = Find<Image>(pet).Single();
                if (character.Category == CharacterStyles.Realistic)
                {
                    var expected = character.Resolve(outfit, "dance", 0);
                    Require(pet.IsDancing && pet.ActiveDance is { } visual && ReferenceEquals(visual.Texture, pet.Art.Frame(character, expected.Sprite, expected.Frame)), $"{character.Id}/{outfit}: dance retains selected adult outfit");
                    await Until(()=>pet.ActiveDance is { } animated && (animated.Pose.Bones[3].B-animated.Rig.Rest[3].B).Length>.00001 && sprite.Opacity==0,"dance intro must finish on a rendered frame");
                    var rig = pet.ActiveDance!;
                    Require((rig.Pose.Bones[3].B - rig.Rig.Rest[3].B).Length > .00001 && sprite.Opacity == 0, $"{character.Id}/{outfit}: skeletal hands move and rigid portrait is hidden");
                    pet.TapDance(); Require(pet.IsDancing, $"{character.Id}/{outfit}: tapping keeps dance active");
                }
                else Require(!pet.IsDancing && pet.ActiveDance is null && sprite.Opacity == pet.State.Opacity, $"{character.Id}/{outfit}: dance unavailable outside adult style");
                pet.RunInteraction("peek"); await Task.Delay(90);
                Require(!pet.IsDancing && pet.HideStage == HidePhase.Approach && pet.CurrentAction == "walk", $"{character.Id}/{outfit}: hide starts by walking, cancels dance");
                var walk = character.Resolve(outfit, "walk", 0).Sprite;
                Require(Enumerable.Range(0, walk.Columns * walk.Rows).Any(frame => ReferenceEquals(sprite.Source, pet.Art.Frame(character, walk, frame))), $"{character.Id}/{outfit}: hide walks in selected clothes");
                pet.Play("idle"); Require(pet.HideStage is null && ((Canvas)pet.Content).Clip is null, $"{character.Id}/{outfit}: replacement cancels hiding and clipping");
            }
        pet.SelectCharacter("deepseek-adult"); pet.State.Outfits[pet.State.Character] = "swim"; pet.ApplySettings();
        pet.Left = pet.WorkArea.Left + pet.WorkArea.Width / 2 - 280; pet.RunInteraction("dance");
        for (int i = 0; i < 5; i++) { await Task.Delay(350); Capture((FrameworkElement)pet.Content, "dance-adult-swim-" + i); }
        var meshImage = new RenderTargetBitmap(560, 680, 96, 96, PixelFormats.Pbgra32); meshImage.Render((FrameworkElement)pet.Content);
        byte[] meshPixels = new byte[560 * 680 * 4]; meshImage.CopyPixels(meshPixels, 560 * 4, 0);
        Require(meshPixels.Where((p, i) => i % 4 == 3 && p > 30).Count() > 1500, "skeletal mesh actually renders visible textured pixels");
        pet.State.Outfits[pet.State.Character] = "wedding"; pet.ApplySettings();
        Require(!pet.IsDancing && Find<Image>(pet).Single().RenderTransform is ScaleTransform, "changing clothes resets dance transforms");
        pet.RunInteraction("dance"); await Task.Delay(800); Capture((FrameworkElement)pet.Content, "dance-adult-wedding");
        pet.State.ReducedMotion = true; pet.ApplySettings();
        Require(!pet.IsDancing, "reduced motion cancels current dance");
        pet.RunInteraction("dance"); Require(!pet.IsDancing, "reduced motion prevents a new dance");
        pet.RunInteraction("peek"); Require(pet.HideStage is null, "reduced motion prevents a new hide journey");
        pet.State.ReducedMotion = false; pet.ApplySettings(); pet.RunInteraction("dance");
        await Task.Delay((int)PetDance.DurationMs + 200);
        Require(!pet.IsDancing && Find<Image>(pet).Single().RenderTransform is ScaleTransform, "dance finishes and restores normal transforms");
        foreach (double size in new[] { 120d, 280 })
            foreach (int side in new[] { -1, 1 })
            foreach (string edgeGroup in new[] { "root", "care", "play", "motions" })
            {
                pet.State.Size = size; pet.ApplySettings(); var area = pet.WorkArea;
                pet.Left = (side < 0 ? area.Left + size * .46 : area.Right - size * .46) - 280;
                pet.ShowMenu(edgeGroup); pet.UpdateLayout();
                Capture((FrameworkElement)pet.Content, $"menu-edge-{size}-{side}-{edgeGroup}");
                foreach (var button in Find<Button>(pet).Where(b => b.IsVisible))
                {
                    var point = button.TranslatePoint(new Point(), pet);
                    Require(point.X + pet.Left >= area.Left - 1 && point.X + pet.Left + button.ActualWidth <= area.Right + 1 && point.Y + pet.Top >= area.Top - 1 && point.Y + pet.Top + button.ActualHeight <= area.Bottom + 1, $"menu control on-screen: size {size}, side {side}, {edgeGroup}, {AutomationProperties.GetName(button)}");
                }
            }
        pet.State.Size = 200; pet.ApplySettings(); pet.SelectCharacter("whale"); pet.Left = pet.WorkArea.Left + pet.WorkArea.Width / 2 - 280;
        foreach (string group in new[] { "root", "care", "play", "motions" })
        {
            pet.ShowMenu(group); pet.UpdateLayout(); Capture((FrameworkElement)pet.Content, "menu-" + group);
            var labels = Find<Button>(pet).Select(AutomationProperties.GetName).ToArray();
            Require(labels.Contains("聊天") == (group == "root"), "chat is a first-level action only: " + group);
            if (group == "care") Require(new[] {"夸夸", "安抚", "哄睡"}.All(labels.Contains), "three additional care actions in the radial menu");
            Require(labels.Contains(group == "root" ? "收起" : "返回"), "radial menu has accessible navigation: " + group);
            Require(!labels.Contains("跳舞"), "chibi radial menu hides dancing: " + group);
            Require(!Find<TextBlock>(pet).Any(t => t.IsVisible && t.Text.Length > 0), "menu has no permanent text: " + group);
        }
        pet.Top -= 260; pet.ShowMenu("root"); pet.UpdateLayout(); Capture((FrameworkElement)pet.Content, "menu-full-circle");
        pet.Top = pet.WorkArea.Bottom - 468;
        pet.SelectCharacter("deepseek-adult"); pet.ShowMenu("play"); pet.UpdateLayout();
        var danceButton = Find<Button>(pet).Single(b => AutomationProperties.GetName(b) == "跳舞"); danceButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Require(pet.IsDancing, "menu dance button invokes real choreography");
        pet.Play("idle");
        var window = new SettingsWindow(pet) { IsHitTestVisible = false, ShowActivated = false }; window.Show();
        await Task.Delay(150); Capture((FrameworkElement)window.Content, "refined-home");
        foreach (string id in new[] { "whale", "deepseek-3d", "deepseek-adult" })
        {
            pet.SelectCharacter(id);
            Find<Button>(window).Single(b => AutomationProperties.GetName(b) == "陪伴日常").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout();
            Require(Find<Button>(window).Any(b => AutomationProperties.GetName(b) == "跳舞") == (pet.Character.Category == CharacterStyles.Realistic), "daily page shows dance for the merged style including legacy aliases: " + id);
        }
        Find<Button>(window).Single(b => AutomationProperties.GetName(b) == "我的伙伴").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout();
        window.Width = 920; window.Height = 650; window.UpdateLayout(); Capture((FrameworkElement)window.Content, "refined-home-compact");
        Require(Find<Button>(window).Where(b => AutomationProperties.GetName(b).StartsWith("分类 ")).All(b => b.ActualWidth > 65), "style selector stays usable at minimum window size");
        window.Width = 1120; window.Height = 850; window.UpdateLayout();
        foreach (var (label, file) in new[] { ("陪伴日常", "refined-life"), ("桌面偏好", "refined-preferences"), ("角色工坊", "refined-studio") })
        {
            Find<Button>(window).Single(b => AutomationProperties.GetName(b) == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout(); Capture((FrameworkElement)window.Content, file);
        }
        var dropdown = Find<ComboBox>(window).First(); dropdown.IsDropDownOpen = true; window.UpdateLayout(); await Task.Delay(150);
        var popup = (System.Windows.Controls.Primitives.Popup)dropdown.Template.FindName("PART_Popup", dropdown);
        Require(popup.IsOpen && popup.Child is FrameworkElement dropdownSurface && dropdownSurface.ActualWidth >= dropdown.ActualWidth - 1, "rounded studio dropdown opens at the field width");
        Capture((FrameworkElement)popup.Child, "refined-dropdown"); dropdown.SelectedIndex = 1; dropdown.IsDropDownOpen = false; window.UpdateLayout(); Require(dropdown.SelectedIndex == 1, "studio dropdown selection survives the new template");
        Find<Button>(window).Single(b => AutomationProperties.GetName(b) == "陪伴日常").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout();
        Find<Button>(window).Single(b => AutomationProperties.GetName(b) == "跳舞").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Require(pet.IsDancing, "settings dance tile starts dancing");
        Find<Button>(window).Single(b => AutomationProperties.GetName(b) == "结束互动").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Require(!pet.IsDancing, "settings stop cancels dancing");
        window.Close();
        foreach (var (id, outfit) in new[] { ("whale", "original"), ("deepseek-adult", "swim"), ("deepseek-adult", "wedding") })
            foreach (int side in new[] { -1, 1 })
            {
                pet.SelectCharacter(id); pet.State.Outfits[id] = outfit; pet.ApplySettings();
                var area = pet.WorkArea;
                pet.Left = (side < 0 ? area.Left + pet.State.Size * .46 + 8 : area.Right - pet.State.Size * .46 - 8) - 280;
                pet.BeginHide(side);
                // The phase clock can cross a boundary between dispatcher ticks. Wait for the
                // actual HWND position too before judging the rendered hidden state.
                await Until(() => pet.HideStage == HidePhase.Hidden
                    && side * (pet.Left + 280 - (side < 0 ? area.Left : area.Right)) > pet.State.Size / 2, "hide never reached edge");
                Require(side * (pet.Left + 280 - (side < 0 ? area.Left : area.Right)) > pet.State.Size / 2, $"{id}: fully hidden beyond requested edge {side}");
                Require(((Canvas)pet.Content).Clip is not null, $"{id}: clipping prevents spill into adjacent monitor");
                pet.Save(); var saved = new StateStore(output).Load(); Require(saved.Left + 280 >= area.Left && saved.Left + 280 <= area.Right, "saving while hidden restores inside desktop");
                await Until(() => pet.HideStage == HidePhase.Peek, "pet never peeked back"); await Task.Delay(600);
                Capture((FrameworkElement)pet.Content, $"hide-{id}-{side}");
                await Until(() => pet.HideStage is null, "pet did not return");
                Require(pet.Left + 280 >= area.Left && pet.Left + 280 <= area.Right && ((Canvas)pet.Content).Clip is null, $"{id}: returns safely on-screen {side}");
            }
        pet.BeginHide(-1); await Task.Delay(100); pet.StopInteraction();
        Require(pet.HideStage is null && ((Canvas)pet.Content).Clip is null, "escape/stop cancels the journey before it hides");
        pet.Left = pet.WorkArea.Left + pet.State.Size * .46 - 280; pet.BeginHide(-1);
        await Until(() => pet.HideStage == HidePhase.Hidden, "cancel-hidden setup");
        pet.StopInteraction(); Require(pet.Left + 280 >= pet.WorkArea.Left && pet.HideStage is null, "stop rescues a fully hidden pet");
        foreach (string id in new[] {"whale", "deepseek-adult"})
        foreach (string outfit in new[] {"original", "swim", "wedding"})
        foreach (int side in new[] {-1, 1})
        foreach (var button in new[] {MouseButton.Left, MouseButton.Right})
        {
            pet.SelectCharacter(id); pet.State.Outfits[id] = outfit; pet.ApplySettings();
            pet.Left = (side < 0 ? pet.WorkArea.Left + pet.State.Size*.46 : pet.WorkArea.Right - pet.State.Size*.46) - 280;
            pet.Top = pet.WorkArea.Bottom - 468;
            pet.BeginHide(side); await Until(() => pet.HideStage == HidePhase.Peek, "found setup"); await Task.Delay(550);
            var image = Find<Image>(pet).Single();
            image.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, button) { RoutedEvent = button == MouseButton.Left ? UIElement.MouseLeftButtonDownEvent : UIElement.MouseRightButtonUpEvent });
            Require(pet.HideStage is null && pet.CurrentAction == "happy" && pet.CurrentSpeech == "被你找到啦！", $"{id}/{outfit}/{side}/{button}: finds the peeking pet with the same response");
            Require(((Canvas)pet.Content).Clip is null && !pet.IsMenuOpen, "finding restores visibility without opening a menu");
            await Task.Delay(60);
            Require(pet.CurrentAction == "happy", "finding does not get overwritten by the journey timer");
        }
        pet.ShowMenu();
        Find<Button>(pet).Single(b => AutomationProperties.GetName(b) == "聊天").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Require(pet.ActiveChat is not null && pet.CurrentAction == "listen", "first-level chat invokes the embedded composer");
        pet.StopInteraction();
        pet.BeginHide(); pet.ToggleVisible(); pet.ToggleVisible();
        Require(pet.HideStage is null && pet.CurrentAction == "idle", "hiding/showing the application cancels the journey without an orphan walking loop");
        File.WriteAllLines(Path.Combine(output, "interaction-check.txt"), checks.Append($"{checks.Count} interaction checks passed."));
    }
}

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
        var walkObservations = new List<object>();
        void Require(bool pass, string name) { if (!pass) throw new InvalidOperationException(name); checks.Add("PASS " + name); }
        void ResumeRealtimeContinuations()
        {
            // Layout may yield at Background priority, but subsequent live-motion
            // awaits must not inherit that priority and wait behind every render.
            System.Threading.SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(pet.Dispatcher,System.Windows.Threading.DispatcherPriority.Normal));
        }
        IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T match) yield return match;
                foreach (var descendant in Find<T>(child)) yield return descendant;
            }
        }
        // Exercise the live dispatcher without stray desktop input changing test selections.
        pet.IsHitTestVisible = false;
        var window = new SettingsWindow(pet) { IsHitTestVisible = false, ShowActivated = false }; window.Show(); window.UpdateLayout();
        void Click(string label)
        {
            window.UpdateLayout();
            var b = Find<Button>(window).First(x => AutomationProperties.GetName(x) == label || x.Content as string == label);
            b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout();
        }
        async Task Capture(string name)
        {
            await Task.Delay(300); window.UpdateLayout();
            var visual = (FrameworkElement)window.Content;
            var preview = new RenderTargetBitmap((int)visual.ActualWidth, (int)visual.ActualHeight, 96, 96, PixelFormats.Pbgra32); preview.Render(visual);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(preview)); using var stream = File.Create(Path.Combine(output, name + ".png")); encoder.Save(stream);
        }
        async Task Until(Func<bool> condition, string context)
        {
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            while (!condition()) { if (timeout.Elapsed > TimeSpan.FromSeconds(4)) throw new TimeoutException($"WPF movement did not advance: {context}; selected={pet.State.Character}/{pet.State.Outfit}; left={pet.Left}; reduced={pet.State.ReducedMotion}; action={pet.CurrentAction}; frame={pet.DrawnFrame}; phase={pet.WalkPhaseMilliseconds}; compositor={pet.WalkUsesRendering}"); await Task.Delay(40); }
        }
        Require(pet.Catalog.Characters.Count == 16 && CharacterStyles.All.All(style => pet.Catalog.Characters.Count(c => c.Category == style) == 8), "eight AI companions in each of two styles");
        Require(Catalog.BuiltInFamilies.All(family => ReferenceEquals(pet.Catalog.Find(Catalog.VariantId(family, CharacterStyles.Realistic)), pet.Catalog.Find((family == "whale" ? "deepseek" : family) + "-3d"))), "legacy 3D IDs resolve to the merged companion");
        Require(!pet.Catalog.Characters.Any(c => c.Id == "umaru") && !Directory.Exists(Path.Combine(AppContext.BaseDirectory, "Assets", "Characters", "umaru")), "Umaru assets and catalog entry removed");
        Require(pet.Catalog.Find("umaru").Id == "whale", "old Umaru selection resolves to DeepSeek");
        foreach (var c in pet.Catalog.Characters.ToArray())
        {
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
            ResumeRealtimeContinuations();
            Click("分类 " + CloudTheme.CategoryName(c.Category)); Click("选择角色 " + c.Name); Require(pet.State.Character == c.Id, "select " + c.Id);
            if (c.Category == "chibi") continue;
            foreach (var (outfit, label) in Catalog.BuiltInWardrobe)
            {
                await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
                ResumeRealtimeContinuations();
                Click("选择服装 " + label);
                foreach (string action in new[] { "chat", "headpat" })
                {
                    var resolved = c.Resolve(outfit, action, 0);
                    var expected = pet.Art.Frame(c, resolved.Sprite, resolved.Frame);
                    pet.Play(action);
                    Require(pet.State.Outfit == outfit && ReferenceEquals(Find<Image>(pet).Single().Source, expected), $"{c.Id}/{outfit}: live {action} retains selected clothes");
                }
            }
        }
        Click("分类 Q版");
        Click("选择角色 GPT");
        Click("选择服装 婚纱");
        Click("选择角色 Claude"); Click("选择角色 GPT"); Require(pet.State.Outfit == "wedding", "outfit selection survives switching characters");
        for (int i = 0; i < 3; i++) { Click("陪伴日常"); Click("角色工坊"); Click("桌面偏好"); Click("风格预览"); Click("我的伙伴"); }
        Require(true, "repeated navigation reuses no parented controls");
        Click("陪伴日常"); pet.CheckIn(); int total = pet.State.CheckIns.Count; pet.CheckIn(); Require(pet.State.CheckIns.Count == total, "UI check-in is idempotent");
        foreach (var action in new[] { "headpat", "poke", "tickle", "snack", "chat", "ball", "blocks", "walk", "peek", "letter" })
        { pet.RunInteraction(action); pet.UpdateLayout(); await Task.Delay(100); }
        pet.ActiveChat?.Close();
        pet.RunInteraction("rest"); await Task.Delay(80); pet.SelectCharacter("whale"); await Task.Delay(3500);
        Require(Find<Image>(pet).First().Visibility == Visibility.Visible, "character change cancels pending farewell");
        pet.RunInteraction("rest"); await Task.Delay(3500); pet.SelectCharacter("whale");
        Require(Find<Image>(pet).First().Visibility == Visibility.Visible, "switching a fully resting character restores its image");

        string[] interactions = ["idle", "listen", "chat", "meal", "eat", "headpat", "poke", "tickle", "pickup", "shaken", "shaken-strong", "dizzy", "faint", "happy", "sad", "sleep", "pounce", "jump", "land", "kick", "think", "ball-ready", "anticipate", "ball-hit", "ball-miss", "build", "bonk", "peek", "curl", "farewell"];
        foreach (var c in pet.Catalog.Characters)
            foreach (string outfit in Catalog.BuiltInOutfits)
            {
                pet.SelectCharacter(c.Id); pet.State.Outfits[c.Id] = outfit; pet.ApplySettings();
                var ownSprites = outfit == "original"
                    ? new[] { c.Atlas }.Concat(c.Dizzy is null ? [] : new[] { c.Dizzy }).Concat(c.Motions.Values).ToArray()
                    : new[] { c.Outfits[outfit].Idle! }.Concat(c.Outfits[outfit].Motions.Values).ToArray();
                foreach (string interaction in interactions)
                {
                    foreach (double time in new[] { 0d, 160, 720, 1800 })
                        Require(ownSprites.Contains(c.Resolve(outfit, interaction, time).Sprite), $"{c.Id}/{outfit}: {interaction} at {time}ms stays in selected appearance");
                    pet.Play(interaction, duration: 60);
                    var resolved = c.Resolve(outfit, interaction, 0);
                    Require(ReferenceEquals(Find<Image>(pet).Single().Source, pet.Art.Frame(c, resolved.Sprite, resolved.Frame)), $"{c.Id}/{outfit}: live {interaction} shows selected art");
                }
                await Until(() => ReferenceEquals(Find<Image>(pet).Single().Source, pet.Art.Frame(c, outfit == "original" ? c.Atlas : c.Outfits[outfit].Idle!, 0)), $"{c.Id}/{outfit}: return to idle");
                Require(pet.State.Character == c.Id && pet.State.Outfit == outfit, $"{c.Id}/{outfit}: completion retains style and clothes");
            }

        foreach (var c in pet.Catalog.Characters)
            foreach (string outfit in Catalog.BuiltInOutfits)
                foreach (int direction in new[] { -1, 1 })
                {
                    pet.SelectCharacter(c.Id); pet.State.Outfits[c.Id] = outfit; pet.ApplySettings();
                    pet.Left = pet.WorkArea.Left + pet.WorkArea.Width / 2 - 280; double start = pet.Left;
                    var walkProbe = System.Diagnostics.Stopwatch.StartNew();
                    pet.StartWalk(false, direction); await Until(() => pet.CurrentAction == "walk" && (pet.Left - start) * direction > .5, $"{c.Id}/{outfit}, direction={direction}, start={start}");
                    var sprite = Find<Image>(pet).Single();
                    double scale = ((ScaleTransform)sprite.RenderTransform).ScaleX;
                    var walk = outfit == "original" ? c.Motions["walk"] : c.Outfits[outfit].Motions["walk"];
                    Require(scale == DesktopWalk.ScaleX(direction,walk.Facing), $"{c.Id}/{outfit}: travel and facing agree (requested={direction}, scale={scale}, action={pet.CurrentAction}, actual={pet.State.Character}/{pet.State.Outfit}, left={pet.Left}, start={start}, phase={pet.WalkPhaseMilliseconds}, frame={pet.DrawnFrame}, sourceFacing={walk.Facing}, waitedMs={walkProbe.Elapsed.TotalMilliseconds})");
                    Require(ReferenceEquals(sprite.Source, pet.Art.Frame(c, walk, pet.DrawnFrame)), $"{c.Id}/{outfit}: walk uses this exact style and clothing; action={pet.CurrentAction}, frame={pet.DrawnFrame}, appearance={pet.State.Character}/{pet.State.Outfit}");
                    if (c.Category != "chibi")
                    {
                        var idle = pet.Art.Frame(c, c.Resolve(outfit, "idle", 0).Sprite, 0);
                        double standingHeight = pet.State.Size * pet.Art.VisibleHeight(idle);
                        double walkingHeight = sprite.Height * pet.Art.VisibleHeight((BitmapSource)sprite.Source);
                        Require(Math.Abs(walkingHeight / standingHeight - 1) < .08, $"{c.Id}/{outfit}: walking preserves standing body scale");
                    }
                    if (direction == 1)
                    {
                        int first = pet.DrawnFrame;
                        double firstPhase = pet.WalkPhaseMilliseconds;
                        var observed = new HashSet<int> { first };
                        var nextFrame = new TaskCompletionSource<(int Frame, ImageSource? Source, double Phase, double ObservedMs)>(TaskCreationOptions.RunContinuationsAsynchronously);
                        var presentations = new Queue<object>();
                        void ObserveFrame(object? sender, EventArgs e)
                        {
                            if(e is RenderingEventArgs)
                            {
                                presentations.Enqueue(new { ms=walkProbe.Elapsed.TotalMilliseconds, action=pet.CurrentAction, character=pet.State.Character, outfit=pet.State.Outfit, frame=pet.DrawnFrame, phase=pet.WalkPhaseMilliseconds, left=pet.Left, scale=((ScaleTransform)sprite.RenderTransform).ScaleX });
                                if(presentations.Count>32)presentations.Dequeue();
                            }
                            if (e is not RenderingEventArgs || pet.CurrentAction != "walk" || pet.State.Character != c.Id || pet.State.Outfit != outfit) return;
                            observed.Add(pet.DrawnFrame);
                            if (pet.DrawnFrame != first)
                                nextFrame.TrySetResult((pet.DrawnFrame, sprite.Source, pet.WalkPhaseMilliseconds,walkProbe.Elapsed.TotalMilliseconds));
                        }
                        // Observe the frames presented by the live compositor. A delayed
                        // Task.Delay continuation can sample the same cell after a whole
                        // gait cycle, missing the intervening frames that were displayed.
                        CompositionTarget.Rendering += ObserveFrame;
                        (int Frame, ImageSource? Source, double Phase, double ObservedMs) presented;
                        try
                        {
                            if (await Task.WhenAny(nextFrame.Task, Task.Delay(TimeSpan.FromSeconds(4))) != nextFrame.Task)
                                throw new TimeoutException($"No different walking frame was presented: {c.Id}/{outfit}; frames={string.Join(',', observed)}; phase={firstPhase}->{pet.WalkPhaseMilliseconds}; left={pet.Left}; action={pet.CurrentAction}; compositor={pet.WalkUsesRendering}");
                            presented = await nextFrame.Task;
                        }
                        finally { CompositionTarget.Rendering -= ObserveFrame; }
                        Require(presented.Phase > firstPhase, $"{c.Id}/{outfit}: live walking clock advances with the presented pose");
                        Require(ReferenceEquals(presented.Source, pet.Art.Frame(c, walk, presented.Frame)), $"{c.Id}/{outfit}: observed walking frame keeps the same appearance");
                        // The bounded image cache can replace bitmap instances. Compare
                        // the currently drawn cell, rather than retaining stale references.
                        var currentSource=(BitmapSource)sprite.Source;
                        int currentFrame=pet.DrawnFrame;
                        string currentAction=pet.CurrentAction;
                        var expectedSource=pet.Art.Frame(c,walk,currentFrame);
                        bool exactSource=ReferenceEquals(currentSource,expectedSource);
                        if(!exactSource || currentAction!="walk")
                        {
                            object Fingerprint(BitmapSource bitmap)
                            {
                                int stride=(bitmap.PixelWidth*bitmap.Format.BitsPerPixel+7)/8;
                                byte[] pixels=new byte[stride*bitmap.PixelHeight];bitmap.CopyPixels(pixels,stride,0);
                                return new { bitmap.PixelWidth,bitmap.PixelHeight,bitmap.DpiX,bitmap.DpiY,format=bitmap.Format.ToString(),hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pixels)) };
                            }
                            var context=System.Threading.SynchronizationContext.Current;
                            var priorities=context?.GetType().GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                                .Where(field=>field.Name.Contains("priority",StringComparison.OrdinalIgnoreCase)).ToDictionary(field=>field.Name,field=>field.GetValue(context)?.ToString());
                            var diagnostic=new { requested=c.Id+"/"+outfit, actual=pet.State.Character+"/"+pet.State.Outfit, currentAction, currentFrame, phase=pet.WalkPhaseMilliseconds, left=pet.Left, elapsedMs=walkProbe.Elapsed.TotalMilliseconds, presentedFrame=presented.Frame,presentedPhase=presented.Phase,presented.ObservedMs, exactSource, synchronizationContext=context?.GetType().FullName, priorities, source=Fingerprint(currentSource),expected=Fingerprint(expectedSource),presentations };
                            File.WriteAllText(Path.Combine(output,"walk-frame-mismatch.json"),System.Text.Json.JsonSerializer.Serialize(diagnostic,Json.Options));
                        }
                        Require(exactSource && currentAction=="walk", $"{c.Id}/{outfit}: next walking frame keeps the same appearance (action={currentAction}, actual={pet.State.Character}/{pet.State.Outfit}, frame={currentFrame}, phase={pet.WalkPhaseMilliseconds}, presentedMs={presented.ObservedMs}, resumedMs={walkProbe.Elapsed.TotalMilliseconds}, exactSource={exactSource})");
                        walkObservations.Add(new { appearance=c.Id+"/"+outfit, firstFrame=first, presentedFrame=presented.Frame, currentFrame, currentAction, presentedMs=presented.ObservedMs, resumedMs=walkProbe.Elapsed.TotalMilliseconds, presentedPhase=presented.Phase, currentPhase=pet.WalkPhaseMilliseconds, exactSource });
                    }
                    double foot = pet.Top + Canvas.GetTop(sprite) + sprite.Height * pet.Art.GroundLine((BitmapSource)sprite.Source);
                    Require(Math.Abs(foot - pet.WorkArea.Bottom) < .1, $"{c.Id}/{outfit}: visible feet stay on desktop floor ({direction})");
                    if (c.FamilyId == "whale")
                    {
                        pet.UpdateLayout();
                        var proof = new RenderTargetBitmap(560, 500, 96, 96, PixelFormats.Pbgra32); proof.Render((Visual)pet.Content);
                        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(proof));
                        using var stream = File.Create(Path.Combine(output, $"walk-{c.Category}-{outfit}-{(direction < 0 ? "left" : "right")}.png")); png.Save(stream);
                    }
                    pet.Play("idle");
                }
        File.WriteAllText(Path.Combine(output,"ui-walk-observations.json"),System.Text.Json.JsonSerializer.Serialize(walkObservations,Json.Options));
        pet.SelectCharacter("whale"); pet.State.Outfits["whale"] = "original"; pet.ApplySettings();
        pet.Left = pet.WorkArea.Right - pet.State.Size * .46 - 280; double edge = pet.Left;
        pet.StartWalk(false, 1); await Until(() => pet.Left < edge - .5, "screen edge reversal");
        Require(((ScaleTransform)Find<Image>(pet).Single().RenderTransform).ScaleX == -1, "screen edge reverses both travel and facing");
        pet.Play("idle");
        pet.SelectCharacter("deepseek-adult");
        string source = Path.Combine(output, "storyboard-fixture"); Directory.CreateDirectory(source);
        var bitmap = pet.Art.Frame(pet.Character, pet.Character.Atlas, 0);
        void Export(string name)
        { var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(Path.Combine(source, name)); png.Save(stream); }
        Export("fixture__P01-standing-neutral__E01-joy__v01__v1.0.png");
        Export("fixture__P11-walking__E01-joy__v01__v1.0.png");
        var imported = Storyboard.Import(pet.Catalog, source);
        Require(imported.Atlas.Columns == 1 && imported.Motions["walk"].Columns == 1, "generator outputs import as static poses");
        Require(File.Exists(Path.Combine(imported.Root, "pet.json")), "import persists a portable manifest");
        var portrait = pet.Catalog.ImportPortrait(Directory.GetFiles(source)[0], "测试立绘", "adult");
        Require(portrait.Atlas.Rows == 1 && portrait.Category == CharacterStyles.Realistic, "legacy portrait import uses the merged category and a single cell");
        foreach (var still in new[] { imported, portrait })
        {
            pet.SelectCharacter(still.Id); double beforeStatic = pet.Left; pet.StartWalk(false, -1); await Task.Delay(250);
            Require(!pet.CanWalk && pet.Left == beforeStatic, "static import never pretends to walk: " + still.Id);
        }
        pet.Catalog.Characters.Remove(imported); pet.Catalog.Characters.Remove(portrait);
        pet.SelectCharacter("whale"); pet.State.Outfits["whale"] = "original"; pet.ApplySettings(); Click("我的伙伴"); Click("分类 Q版"); await Capture("pet-home");
        foreach (string family in Catalog.BuiltInFamilies)
            foreach (string outfit in Catalog.BuiltInOutfits)
            {
                pet.SelectCharacter(family); pet.State.Outfits[family] = outfit; pet.ApplySettings();
                foreach (string style in new[] { CharacterStyles.Realistic, CharacterStyles.Chibi })
                {
                    await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
                    ResumeRealtimeContinuations();
                    Click("分类 " + CloudTheme.CategoryName(style));
                    Require(pet.Character.FamilyId == family && pet.Character.Category == style && pet.State.Outfit == outfit, $"category selection applies {family}/{style}/{outfit} to live pet");
                }
            }
        pet.SelectCharacter("deepseek-adult"); pet.State.Outfits[pet.State.Character] = "swim"; pet.ApplySettings();
        pet.StartWalk(false, 1); pet.State.Outfits[pet.State.Character] = "wedding"; pet.ApplySettings();
        var bridal = pet.Art.Frame(pet.Character, pet.Character.Outfits["wedding"].Idle!, 0);
        await Task.Delay(220);
        Require(pet.State.Character == "deepseek-adult" && pet.State.Outfit == "wedding" && ReferenceEquals(Find<Image>(pet).Single().Source, bridal), "changing clothes mid-walk cancels old frames and keeps realistic style");
        pet.SelectCharacter("missing-realistic-variant");
        Require(pet.State.Character == "deepseek-adult", "a missing explicit selection never silently changes to chibi");
        foreach (string style in new[] { CharacterStyles.Chibi, CharacterStyles.Realistic })
        {
            pet.StartWalk(false, 1); pet.SelectStyle(style); await Task.Delay(220);
            var selected = pet.Art.Frame(pet.Character, pet.Character.Resolve("wedding", "chat", 0).Sprite, pet.DrawnFrame);
            Require(pet.CurrentAction == "chat" && pet.Character.Category == style && pet.State.Outfit == "wedding" && ReferenceEquals(Find<Image>(pet).Single().Source, selected), "style change mid-walk cancels old frames and preserves wedding: " + style);
        }
        Click("风格预览");
        foreach (string family in Catalog.BuiltInFamilies)
        {
            Click("对照角色 " + pet.Catalog.Find(family).Name);
            foreach (var (outfit, label) in Catalog.BuiltInWardrobe)
            {
                Click("对照服装 " + label);
                foreach (string style in CharacterStyles.All)
                {
                    string id = Catalog.VariantId(family, style);
                    var c = pet.Catalog.Find(id);
                    var expected = pet.Art.Frame(c, outfit == "original" ? c.Atlas : c.Outfits[outfit].Idle!, 0);
                    Require(Find<Image>(window).Any(image => ReferenceEquals(image.Source, expected)), $"gallery previews {id}/{outfit}");
                    Click("试看 " + CloudTheme.CategoryName(style));
                    Require(pet.State.Character == id && pet.State.Outfit == outfit, $"gallery applies {id}/{outfit}");
                }
                await Capture($"styles-{family}-{outfit}");
            }
        }
        pet.SelectCharacter("gpt-3d"); pet.State.Outfits[pet.State.Character] = "wedding"; pet.ApplySettings();
        pet.SelectCharacter("claude-adult"); pet.State.Outfits["claude-adult"] = "swim"; pet.ApplySettings();
        pet.SelectCharacter("gpt-3d");
        Require(pet.State.Character == "gpt-adult" && pet.State.Outfit == "wedding", "legacy 3D selection restores the merged companion's outfit");
        pet.SelectCharacter("claude-adult");
        Require(pet.State.Outfit == "swim", "merged companion outfit is restored independently");
        Click("我的伙伴"); Click("分类 3D真人"); await Capture("roster-realistic");
        Require(Find<Button>(window).Count(button => AutomationProperties.GetName(button).StartsWith("分类 ")) == 2, "settings shows exactly two style choices");
        pet.SelectCharacter("whale"); pet.State.Outfits["whale"] = "original"; pet.ApplySettings();
        Click("风格预览"); await Capture("deepseek-styles");
        Click("陪伴日常"); await Capture("cloud-life"); Click("角色工坊"); await Capture("cloud-studio"); Click("桌面偏好"); await Capture("cloud-settings");
        pet.Save(); var restored = new StateStore(output).Load();
        Require(restored.Character == "whale" && restored.Outfit == "original" && restored.CheckIns.Count == total, "state reload preserves selection and progress");
        Require(restored.Outfits["gpt-adult"] == "wedding" && restored.Outfits["claude-adult"] == "swim" && !restored.Outfits.ContainsKey("gpt-3d"), "state reload preserves merged companions' outfit selections without retired IDs");
        File.WriteAllLines(Path.Combine(output, "ui-check.txt"), checks.Append($"{checks.Count} WPF integration checks passed."));
        window.Close();
    }
}

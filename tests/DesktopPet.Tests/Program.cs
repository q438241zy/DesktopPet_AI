using System.Text.Json;
using DesktopPet.Core;

int passed = 0;
void Test(string name, Action run)
{
    try { run(); Console.WriteLine("PASS " + name); passed++; }
    catch (Exception ex) { Console.Error.WriteLine("FAIL " + name + ": " + ex); Environment.ExitCode = 1; }
}
void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}; got {actual}"); }
void Reject(Action run) { try { run(); } catch (InvalidDataException) { return; } throw new Exception("Invalid input was accepted"); }

Test("daily check-in is idempotent and a missed day preserves affection", () =>
{
    var state = new PetState(); var day = new DateOnly(2026, 9, 27);
    for (int i = 0; i < 7; i++) Equal(true, state.CheckIn(day.AddDays(i)));
    Equal(false, state.CheckIn(day)); Equal(7, state.CheckIns.Count); Equal(2, state.BondLevel);
    Equal(7, state.Streak(day.AddDays(6))); Equal(0, state.Streak(day.AddDays(9))); Equal(2, state.BondLevel);
    Equal(true, state.CheckIn(day.AddDays(9))); Equal(1, state.Streak(day.AddDays(9)));
});
Test("leap years, reunions and anniversaries use local calendar dates", () =>
{
    var state = new PetState { AdoptedAt = "2024-02-29", LastSeen = "2026-09-24" };
    Equal(3, state.DaysAway(new DateOnly(2026, 9, 27))); Equal(0, state.DaysAway(new DateOnly(2026, 9, 23)));
    Equal("相伴 4 周年", state.Anniversary(new DateOnly(2028, 2, 29)));
    Equal("相伴第 100 天", state.Anniversary(new DateOnly(2024, 6, 8)));
});
Test("missing outfit animations stay dressed through every interaction", () =>
{
    var pet = new Character { Dizzy = new("dizzy.webp", 1, 1), Motions = new() { ["walk"] = new("walk.webp"), ["eat"] = new("eat.webp"), ["headpat"] = new("pat.webp") },
        Outfits = new() { ["wedding"] = new() { Idle = new("dress.webp", 1, 1), Motions = new() { ["walk"] = new("dress-walk.webp") } } } };
    Equal("dress.webp", pet.Resolve("wedding", "idle", 0).Sprite.File);
    Equal("dress-walk.webp", pet.Resolve("wedding", "walk", 0).Sprite.File);
    foreach (string action in new[] { "meal", "eat", "chat", "pounce", "headpat", "poke", "tickle", "kick", "jump", "sleep", "dizzy", "faint", "sad", "happy", "pickup", "shaken", "shaken-strong", "farewell", "ball-hit", "ball-miss", "bonk", "peek", "curl", "think", "dance" })
        foreach (bool reduced in new[] { false, true })
            Equal((new Sprite("dress.webp", 1, 1), 0), pet.Resolve("wedding", action, 700, reduced));
    Equal(0, pet.Resolve("wedding", "walk", 700, true).Frame);
    Equal("eat.webp", pet.Resolve("original", "meal", 700).Sprite.File);
    Equal("dizzy.webp", pet.Resolve("original", "dizzy", 700).Sprite.File);
    Equal("walk.webp", pet.Resolve("removed-outfit", "walk", 700).Sprite.File);
});
Test("semantic fallback uses the current outfit before its pose atlas", () =>
{
    var pet = new Character { Motions = new() { ["poke"] = new("original-poke.png"), ["meal"] = new("original-meal.png") },
        Outfits = new() { ["swim"] = new() { Idle = new("swim.png"), Motions = new() { ["headpat"] = new("swim-pat.png"), ["eat"] = new("swim-eat.png"), ["jump"] = new("swim-jump.png"), ["chat"] = new("swim-chat.png") } } } };
    foreach (var (action, file) in new[] { ("poke", "swim-pat.png"), ("meal", "swim-eat.png"), ("tickle", "swim-jump.png"), ("kick", "swim-jump.png"), ("pounce", "swim-chat.png") })
        Equal(file, pet.Resolve("swim", action, 0).Sprite.File);
    foreach (var (action, frame) in new[] { ("idle", 0), ("happy", 1), ("sleep", 3), ("dizzy", 4), ("faint", 4), ("sad", 5) })
        Equal((new Sprite("swim.png"), frame), pet.Resolve("swim", action, 700));
    pet.Outfits["swim"].Motions["poke"] = new("swim-poke.png");
    Equal("swim-poke.png", pet.Resolve("swim", "poke", 0).Sprite.File);
});
Test("walking capability never borrows original clothes or mistakes poses for an animation", () =>
{
    var pet = new Character { Motions = new() { ["walk"] = new("original-walk.png") }, Outfits = new() {
        ["swim"] = new() { Idle = new("six-poses.png") },
        ["wedding"] = new() { Idle = new("dress.png", 1, 1), Motions = new() { ["walk"] = new("dress-walk.png", 1, 1) } } } };
    Equal(true, pet.CanWalk("original")); Equal(false, pet.CanWalk("swim")); Equal(false, pet.CanWalk("wedding"));
    pet.Outfits["swim"].Motions["walk"] = new("swim-walk.png"); Equal(true, pet.CanWalk("swim"));
    pet.Outfits["swim"].Idle = null;
    Equal("swim-walk.png", pet.Resolve("swim", "idle", 900).Sprite.File);
    Equal(0, pet.Resolve("swim", "idle", 900).Frame);
});
Test("portrait wardrobes stay selected through greetings and touch interactions", () =>
{
    var pet = new Character { Id = "claude-3d", Family = "claude", Atlas = new("portrait.png", 1, 1),
        Outfits = new() { ["swim"] = new() { Idle = new("swim.png", 1, 1) }, ["wedding"] = new() { Idle = new("wedding.png", 1, 1) } } };
    Equal("claude", pet.FamilyId);
    foreach (string outfit in new[] { "swim", "wedding" })
        foreach (string action in new[] { "idle", "chat", "headpat", "sleep", "pickup", "happy", "farewell" })
            Equal(outfit + ".png", pet.Resolve(outfit, action, 800).Sprite.File);
    Equal("portrait.png", pet.Resolve("original", "headpat", 800).Sprite.File);
    Equal("legacy", new Character { Id = "legacy" }.FamilyId);
});
Test("six-frame animation wraps at exact clip duration", () =>
{
    var clip = new Sprite("a.webp", 3, 2, [100, 200, 300, 400, 500, 600]);
    Equal(0, Motion.Frame(clip, 0)); Equal(1, Motion.Frame(clip, 100)); Equal(5, Motion.Frame(clip, 2099)); Equal(0, Motion.Frame(clip, 2100));
    Equal(0, Motion.Frame(new Sprite("single.png", 1, 1), 555));
});
Test("walking reverses inward at either edge without overshooting", () =>
{
    Equal((90d, -1), DesktopWalk.Step(50, 1, 40, 10, 90));
    Equal((10d, 1), DesktopWalk.Step(11, -1, 200, 10, 90));
    Equal((12d, 1), DesktopWalk.Step(10, 1, 2, 10, 90));
    Equal((30d, -1), DesktopWalk.Step(40, -1, 10, 10, 90));
    Equal((50d, 1), DesktopWalk.Step(50, 1, 10, 50, 50));
});
Test("the displayed facing follows travel and each source clip orientation", () =>
{
    Equal(-1d, DesktopWalk.ScaleX(-1, "right")); Equal(1d, DesktopWalk.ScaleX(1, "right"));
    Equal(1d, DesktopWalk.ScaleX(-1, "left")); Equal(-1d, DesktopWalk.ScaleX(1, "left"));
    Equal(44d, DesktopWalk.Speed(200, new Sprite("walk.png", 2, 1, [500, 500])));
});
Test("hide-and-seek reaches the nearest edge before disappearing and returns inward", () =>
{
    foreach (double origin in new[] { -1920d, 0, 2560 })
        foreach (int side in new[] { -1, 1 })
            foreach (double size in new[] { 120d, 200, 280 })
            {
                double center = origin + (side < 0 ? 400 : 1500);
                var journey = new EdgeHide(center, origin, origin + 1920, size, 80);
                Equal(side, journey.Side);
                var halfway = journey.At(journey.ApproachSeconds / 2);
                Equal(HidePhase.Approach, halfway.Phase); Equal(true, halfway.Walking); Equal(side, halfway.Direction);
                Equal(HidePhase.Hide, journey.At(journey.ApproachSeconds).Phase);
                var hidden = journey.At(journey.ApproachSeconds + 1);
                Equal(HidePhase.Hidden, hidden.Phase); Equal(true, (hidden.Center - journey.Edge) * side > size / 2);
                var peek = journey.At(journey.ApproachSeconds + 2.2);
                Equal(HidePhase.Peek, peek.Phase); Equal(-side, peek.Direction); Equal(true, Math.Abs(peek.Center - journey.Edge) < size / 2);
                var end = journey.At(journey.Duration + 1);
                Equal(HidePhase.Complete, end.Phase); Equal(journey.RestingCenter, end.Center); Equal(-side, end.Direction);
                Equal(true, end.Center >= origin && end.Center <= origin + 1920);
                for (double time = 0; time < journey.Duration; time += .025)
                    Equal(true, Math.Abs(journey.At(time + .025).Center - journey.At(time).Center) < size * .06);
            }
    Equal(1, new EdgeHide(100, 0, 1920, 200, 80, 1).Side);
    var atEdge = new EdgeHide(92, 0, 1920, 200, 80, -1); Equal(0d, atEdge.ApproachSeconds); Equal(HidePhase.Hide, atEdge.At(0).Phase);
});
Test("skeletal dance articulates limbs, preserves bone lengths and returns to rest", () =>
{
    foreach (string family in new[] { "whale", "gpt", "claude", "gemini", "grok", "qwen", "zhipu", "kimi" })
    {
        Equal(true, PortraitRig.SupportsDance("adult", family)); Equal(false, PortraitRig.SupportsDance("3d", family)); Equal(false, PortraitRig.SupportsDance("chibi", family));
        var rig = new PortraitRig(family, "original");
        foreach (double t in new[] { 0d, PetDance.DurationMs })
            Equal(true, rig.Skin(rig.Pose(t)).Zip(rig.Vertices).All(p => (p.First - p.Second).Length < .000001));
        for (double t = 0; t <= PetDance.DurationMs; t += 37)
        {
            var pose = rig.Pose(t); var quiet = rig.Pose(t, true);
            Equal(true, quiet.Bones.SequenceEqual(rig.Rest));
            var next = rig.Pose(t + 16);
            Equal(true, pose.Bones.Zip(next.Bones).All(p => (p.First.A - p.Second.A).Length < .025 && (p.First.B - p.Second.B).Length < .025));
            foreach (int bone in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 })
                Equal(true, Math.Abs((pose.Bones[bone].B - pose.Bones[bone].A).Length - (rig.Rest[bone].B - rig.Rest[bone].A).Length) < .003);
        }
        var moving = rig.Pose(750);
        Equal(true, (moving.Bones[3].B - rig.Rest[3].B).Length > .01);
        Equal(true, (moving.Bones[5].B - rig.Rest[5].B).Length > .01);
        Equal(true, (moving.Bones[8].A - rig.Rest[8].A).Length > .001);
        var dress = new PortraitRig(family, "wedding");
        Equal(true, dress.Skin(dress.Pose(750)).All(v => double.IsFinite(v.X) && double.IsFinite(v.Y)));
    }
});
Test("radial menus fit corners and negative monitors without overlapping buttons", () =>
{
    foreach (double origin in new[] { -1920d, 0, 2560 })
    foreach (double size in new[] { 120d, 200, 280 })
    foreach (int count in new[] { 6, 7 })
    foreach (double x in new[] { origin + size * .46, origin + 960, origin + 1920 - size * .46 })
    foreach (double y in new[] { size / 2 + 16, 500, 1080 - size / 2 })
    {
        var menu = RadialMenu.Place(count, new MenuPoint(x, y), size, new MenuBounds(origin, 0, origin + 1920, 1080));
        Equal(count, menu.Length);
        foreach (var p in menu) Equal(true, p.X >= origin + 22 && p.X <= origin + 1898 && p.Y >= 22 && p.Y <= 1058);
        for (int i = 0; i < count; i++) for (int j = i + 1; j < count; j++)
            Equal(true, Math.Sqrt(Math.Pow(menu[i].X - menu[j].X, 2) + Math.Pow(menu[i].Y - menu[j].Y, 2)) > 48);
    }
});
Test("dance taps score once per beat inside the timing window", () =>
{
    var dance = new PetDance(); Equal(true, dance.Tap(20)); Equal(false, dance.Tap(90));
    Equal(false, dance.Tap(250)); Equal(true, dance.Tap(410)); Equal(false, dance.Tap(515));
    Equal(true, dance.Tap(1000)); Equal(false, dance.Tap(-1)); Equal(false, dance.Tap(8000)); Equal(3, dance.Hits);
});
Test("all portrait gestures move independently, remain finite and ease back in both styles", () =>
{
    foreach (string category in new[] { "3d", "adult" })
    foreach (string family in new[] { "whale", "gpt", "claude", "gemini", "grok", "qwen", "zhipu", "kimi" })
    {
        Equal(true, PortraitRig.Supports(category, family));
        var rig = new PortraitRig(family, "wedding", category: category);
        foreach (string action in PortraitMotion.Actions)
        {
            double duration = PortraitMotion.Duration(action);
            var start = rig.MotionPose(action, 0, duration); Equal(true, start.Bones.SequenceEqual(rig.Rest));
            var end = rig.MotionPose(action, duration, duration, true); Equal(true, end.Bones.SequenceEqual(rig.Rest));
            if (action != "sleep") Equal(true, rig.MotionPose(action, duration, duration).Bones.SequenceEqual(rig.Rest));
            var active = rig.MotionPose(action, 900, duration);
            Equal(true, active.Bones.Zip(rig.Rest).Any(p => (p.First.A - p.Second.A).Length + (p.First.B - p.Second.B).Length > .0001));
            Equal(true, rig.Skin(active).All(p => double.IsFinite(p.X) && double.IsFinite(p.Y)));
            for (double t = 0; t < duration; t += 29)
            {
                var a = rig.MotionPose(action, t, duration); var b = rig.MotionPose(action, t + 16, duration);
                if (!a.Bones.Zip(b.Bones).All(p => (p.First.B - p.Second.B).Length < .04)) throw new Exception($"Joint discontinuity: {category}/{family}/{action}/{t}");
            }
        }
    }
    Equal("poke", PortraitMotion.TouchRegion("adult", .16)); Equal("poke", PortraitMotion.TouchRegion("3d", .19));
    Equal("headpat", PortraitMotion.TouchRegion("adult", .07)); Equal("tickle", PortraitMotion.TouchRegion("3d", .43));
    Equal("headpat", PortraitMotion.TouchRegion("chibi", .4));
});
Test("mixed collectibles draw ten sports and ten everyday items without repeats per round", () =>
{
    Equal(20, Collectibles.All.Count); Equal(20, Collectibles.All.Select(x => x.Id).Distinct().Count()); Equal(10, Collectibles.Sports.Count);
    var draws = new ItemDrawBag(Collectibles.All, new Random(12)); string last = "";
    for (int round = 0; round < 20; round++)
    {
        var prizes = Enumerable.Range(0, 20).Select(_ => draws.Draw()).ToArray();
        Equal(20, prizes.Select(x => x.Id).Distinct().Count()); Equal(10, prizes.Count(x => x.Kind == ItemKind.Sport));
        Equal(false, last == prizes[0].Id); last = prizes[^1].Id;
    }
    Equal("shell", Collectibles.FromSavedName("一枚贝壳")!.Id); Equal(true, Collectibles.Food.All(x => x.Edible));
    Equal(true, Collectibles.All.Any(x => x.Name == "米饭")); Equal(true, Collectibles.All.Any(x => x.Name == "面包"));
});
Test("all ten toys bounce inside the play area with distinct gravity and rebound", () =>
{
    foreach (var toy in Collectibles.Sports)
    {
        var flight = new ToyFlight(100, 100, -800, 400, 0);
        for (int tick = 0; tick < 300; tick++)
        {
            flight = ToyPhysics.Step(flight, toy, .033, 25, 520, 468);
            Equal(true, flight.X >= 25 && flight.X <= 520 - toy.Diameter && flight.Y <= 468 - toy.Diameter);
            Equal(true, double.IsFinite(flight.Vy) && double.IsFinite(flight.Angle));
        }
    }
    var floor = new ToyFlight(50, 467, 0, 300, 0);
    var basketball = ToyPhysics.Step(floor, Collectibles.Get("basketball"), .016, 0, 560, 468);
    var bowling = ToyPhysics.Step(floor, Collectibles.Get("bowling"), .016, 0, 560, 468);
    Equal(true, Math.Abs(basketball.Vy) > Math.Abs(bowling.Vy));
});
Test("fast throws use swept contact and misses remain misses", () =>
{
    Equal(true, BallPhysics.Hit(0, 100, 900, 100, 450, 100, 30));
    Equal(false, BallPhysics.Hit(0, 100, 900, 100, 450, 200, 30));
    Equal(true, BallPhysics.Hit(5, 5, 5, 5, 5, 5, 1));
});
Test("shake needs four recent reversals, then expires", () =>
{
    var shake = new ShakeTracker(); shake.Start(0, 0);
    shake.Move(160, 100); shake.Move(0, 200); Equal("shaken-strong", shake.Motion);
    Equal(false, shake.IsDizzy(200)); shake.Move(160, 300); shake.Move(0, 400); shake.Move(160, 500);
    Equal(true, shake.IsDizzy(500)); Equal(false, shake.IsDizzy(2100));
    shake.Start(0, 0); shake.Move(60, 200); shake.Move(0, 400); Equal("shaken", shake.Motion);
});
string root = Path.Combine(Path.GetTempPath(), "DesktopPet-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    Test("atomic saves preserve character, outfit, check-ins and treasure", () =>
    {
        var store = new StateStore(root); var state = new PetState { Character = "gpt", Outfits = new() { ["gpt"] = "wedding" }, Treasures = ["贝壳"] };
        state.CheckIn(new DateOnly(2026, 9, 27)); store.Save(state); var loaded = store.Load();
        Equal("gpt", loaded.Character); Equal("wedding", loaded.Outfit); Equal(1, loaded.CheckIns.Count); Equal("贝壳", loaded.Treasures[0]);
        Equal(false, File.Exists(Path.Combine(root, "state.json.tmp")));
    });
    Test("corrupt saves are retained instead of silently discarded", () =>
    {
        File.WriteAllText(Path.Combine(root, "state.json"), "{broken"); var store = new StateStore(root); var state = store.Load();
        Equal("whale", state.Character); Equal(true, store.Warning is not null);
        Equal("{broken", File.ReadAllText(Directory.GetFiles(root, "state-unreadable-*.json").Single()));
    });
    Test("import rejects path traversal, executable assets and invalid timing", () =>
    {
        Reject(() => Character.SafeFile(root, "../secret.png")); Reject(() => Character.SafeFile(root, "C:\\secret.png")); Reject(() => Character.SafeFile(root, "script.exe"));
        File.WriteAllBytes(Path.Combine(root, "atlas.png"), [0]);
        var c = new Character { Id = "test", Name = "test" };
        void Write() => File.WriteAllText(Path.Combine(root, "pet.json"), JsonSerializer.Serialize(c, Json.Options));
        Write(); Equal("test", Character.Load(root).Id);
        c.Atlas = new Sprite("atlas.png", 3, 2, [0, 0]); Write(); Reject(() => Character.Load(root));
        c.Atlas = new Sprite("atlas.png", 0, 0); Write(); Reject(() => Character.Load(root));
        c.Atlas = new Sprite("atlas.png", 1, 1, Facing: "up"); Write(); Reject(() => Character.Load(root));
        c.Atlas = new Sprite("atlas.png", 1, 1); c.Category = "unknown"; Write(); Reject(() => Character.Load(root));
        c.Category = "adult"; Write(); Equal("adult", Character.Load(root).Category);
        c.Outfits["empty"] = new Outfit { Name = "empty" }; Write(); Reject(() => Character.Load(root)); c.Outfits.Clear();
        c.Atlas = new Sprite("missing.png"); Write(); Reject(() => Character.Load(root));
    });
    Test("saved invalid values are bounded without accepting invalid dates", () =>
    {
        var state = new PetState { Size = double.NaN, Opacity = 50, Left = double.PositiveInfinity }; state.Validate();
        Equal(200d, state.Size); Equal(1d, state.Opacity); Equal<double?>(null, state.Left);
        state.CheckIns.Add("not-a-date"); Reject(state.Validate);
    });
}
finally { Directory.Delete(root, true); }
Console.WriteLine($"{passed} checks passed.");

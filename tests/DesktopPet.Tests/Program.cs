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
Test("only available outfit motions replace the original animation", () =>
{
    var pet = new Character { Motions = new() { ["walk"] = new("walk.webp"), ["eat"] = new("eat.webp"), ["headpat"] = new("pat.webp") },
        Outfits = new() { ["wedding"] = new() { Idle = new("dress.webp", 1, 1), Motions = new() { ["walk"] = new("dress-walk.webp") } } } };
    Equal("dress.webp", pet.Resolve("wedding", "idle", 0).Sprite.File);
    Equal("dress-walk.webp", pet.Resolve("wedding", "walk", 0).Sprite.File);
    Equal("eat.webp", pet.Resolve("wedding", "meal", 0).Sprite.File);
    Equal("pat.webp", pet.Resolve("wedding", "poke", 0).Sprite.File);
    Equal(3, pet.Resolve("wedding", "sleep", 0).Frame);
    Equal(0, pet.Resolve("wedding", "walk", 700, true).Frame);
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

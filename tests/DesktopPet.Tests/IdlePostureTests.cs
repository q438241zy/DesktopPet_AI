using System.Text.Json;
using DesktopPet.Core;

internal static class IdlePostureTests
{
    internal static void Run(Action<string, Action> test)
    {
        void Check(bool ok) { if (!ok) throw new Exception("Idle posture assertion failed"); }
        test("idle pose waits a full 30 seconds and does not catch up after a delayed tick", () =>
        {
            var clock = new IdlePostureClock(); clock.Reset(10);
            Check(!clock.Tick(30009, true) && clock.Pose == "stand"); Check(clock.Tick(30010, true) && clock.Pose == "sit");
            Check(!clock.Tick(60009, true)); Check(clock.Tick(150010, true) && clock.Pose == "stand");
            Check(!clock.Tick(150011, true)); Check(clock.Tick(180010, true));
        });
        test("interaction and suspension require a fresh idle interval", () =>
        {
            var clock = new IdlePostureClock("sit"); clock.Reset(0); Check(!clock.Tick(29000, false));
            Check(!clock.Tick(120000, true)); Check(!clock.Tick(149999, true)); Check(clock.Tick(150000, true));
            clock.Reset(170000); Check(!clock.Tick(199999, true)); Check(clock.Tick(200000, true));
        });
        test("legacy save adds posture preferences without discarding companionship or collections", () =>
        {
            var state = JsonSerializer.Deserialize<PetState>("""{"character":"gpt","outfits":{"gpt":"sports"},"treasures":["basketball"],"readStories":{},"checkIns":[],"adoptedAt":"2026-10-01"}""", Json.Options)!;
            state.Validate(); Check(state.Postures.Count == 0 && state.Character == "gpt" && state.Outfit == "sports" && state.Treasures.Single() == "basketball");
            state.Postures["gpt"] = "sit"; state.Postures["whale"] = "invalid";
            var copy = JsonSerializer.Deserialize<PetState>(JsonSerializer.Serialize(state, Json.Options), Json.Options)!;
            copy.Validate(); Check(copy.Postures["gpt"] == "sit" && copy.Postures["whale"] == "auto" && copy.Treasures.SequenceEqual(state.Treasures));
        });
        test("imported outfits never borrow the original character posture", () =>
        {
            var own = new Sprite("outfit.png", 1, 1); var c = new Character { Atlas = new("original.png"), Outfits = new() { ["custom"] = new() { Name = "Custom", Motions = new() { ["walk"] = own } } } };
            foreach (string pose in new[] { "stand", "sit" }) Check(IdlePosture.Resolve(c, "custom", pose).Sprite == own);
        });
    }
}

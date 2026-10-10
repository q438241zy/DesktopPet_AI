using System.Text.Json;
using DesktopPet.Core;

internal static class IdlePostureTests
{
    internal static void Run(Action<string, Action> test)
    {
        void Check(bool ok) { if (!ok) throw new Exception("Idle posture assertion failed"); }
        test("five idle poses change every 1–2 seconds, avoid repeats and delayed tick bursts", () =>
        {
            var clock = new IdlePostureClock("stand", new Random(3)); clock.Reset(10); var poses = new HashSet<string>{clock.Pose};
            for(int i=0;i<20;i++)
            { double next=clock.Next; string before=clock.Pose; Check(clock.Duration is >=1000 and <=2000); Check(!clock.Tick(next-1,true)); Check(clock.Tick(next,true)&&clock.Pose!=before); Check(!clock.Tick(next,true)); poses.Add(clock.Pose); }
            Check(poses.SetEquals(IdlePostureClock.Poses)); Check(clock.Tick(150010,true)); Check(!clock.Tick(150011,true));
        });
        test("interaction and suspension require a fresh idle interval", () =>
        {
            var clock = new IdlePostureClock("sit", new Random(8)); clock.Reset(0); Check(!clock.Tick(500, false));
            double held=clock.Progress(500); Check(held>0&&held<1&&clock.Progress(110000)==held);
            Check(!clock.Tick(120000, true)); Check(!clock.Tick(clock.Next-1, true)); Check(clock.Tick(clock.Next, true));
            clock.Reset(170000); Check(!clock.Tick(clock.Next-1, true)); Check(clock.Tick(clock.Next, true));
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

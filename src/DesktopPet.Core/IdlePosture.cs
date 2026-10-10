namespace DesktopPet.Core;

/// <summary>A separate clock: changing a pose never counts as user activity.</summary>
public sealed class IdlePostureClock(string initial = "stand", Random? random = null)
{
    public const double MinimumInterval = 1000, MaximumInterval = 2000;
    public static readonly string[] Poses = ["stand", "sit", "think", "stretch", "smile"];
    private readonly Random rng = random ?? Random.Shared;
    private readonly Queue<string> bag = new();
    public string Pose { get; private set; } = initial == "sit" ? "sit" : "stand";
    public double Next { get; private set; }
    public double Duration { get; private set; }
    private double started, heldProgress;
    private bool running;
    public double Progress(double now) => running ? Math.Clamp((now - started) / Duration, 0, .999999) : heldProgress;
    public void Reset(double now) { Duration = rng.Next(1000, 2001); started = now; Next = now + Duration; running = true; heldProgress = 0; }
    public bool Tick(double now, bool eligible)
    {
        if (!eligible) { heldProgress = Progress(now); running = false; return false; }
        if (!running) { Reset(now); return false; }
        if (now < Next) return false;
        if (bag.Count == 0)
        {
            var choices = Poses.Where(p => p != Pose).ToArray(); rng.Shuffle(choices);
            foreach (string p in choices) bag.Enqueue(p);
        }
        Pose = bag.Dequeue(); Reset(now);
        return true;
    }
}

public sealed record IdlePostureFrame(Sprite Sprite, int Frame, FivePose? Anchor = null);

/// <summary>The exact standing/seated source poses approved in CompanionV01.</summary>
public static class IdlePosture
{
    public static string Normalize(string? value) => value is "stand" or "sit" ? value : "auto";
    public static IdlePostureFrame Resolve(Character character, string outfit, string pose, double progress = 0)
    {
        var idle = character.Outfits.GetValueOrDefault(outfit)?.Idle ?? character.Atlas;
        if (pose == "stand" && character.MotionFor(outfit, "idle-stand") is { } stand) return new(stand, 0);
        if (pose is "think" or "stretch" && character.MotionFor(outfit, pose) is { } gesture)
        {
            if (pose == "think" && character.Category == CharacterStyles.Chibi && outfit is "swim" or "wedding") gesture = gesture with { IsolateCells = true };
            return new(gesture, Motion.Frame(gesture, Math.Clamp(progress, 0, .999999) * DesktopWalk.CycleMilliseconds(gesture)));
        }
        if (pose == "smile")
        {
            if ((character.Category == CharacterStyles.Realistic || outfit is "swim" or "wedding") && character.FiveFor(outfit) is { } smiling)
            { var happy = smiling.Poses["happy"]; return new(smiling.Atlases[happy.Atlas] with { IsolateCells = true }, happy.Frame, happy); }
            return new(idle, Math.Min(1, idle.Columns * idle.Rows - 1));
        }
        if (character.Category == CharacterStyles.Chibi && pose == "stand" && character.FiveFor(outfit) is { } five)
        {
            bool supportPose = outfit == "wedding"
                || outfit == "swim" && character.FamilyId is "gemini" or "qwen" or "zhipu" or "kimi"
                || outfit == "sports" && character.FamilyId is "gpt" or "claude" or "qwen";
            if (supportPose && character.MotionFor(outfit, "walk") is { } walk)
                return new(walk, walk.Frames is { Length: > 7 } order ? order[7] : Math.Min(7, walk.Columns * walk.Rows - 1));
            var neutral = five.Poses["neutral"];
            return new(five.Atlases[neutral.Atlas], neutral.Frame, neutral);
        }
        if (character.Category == CharacterStyles.Realistic && pose == "sit" && character.MotionFor(outfit, "curl") is { } curl)
            return new(curl with { IsolateCells = true }, curl.Frames?[0] ?? Math.Min(6, curl.Columns * curl.Rows - 1));
        // Imported packs without approved posture art retain their own outfit's idle.
        if (outfit != "original" && character.Outfits.TryGetValue(outfit, out var clothes) && clothes.Idle is null)
        { var fallback = character.Resolve(outfit, "idle", 0, true); return new(fallback.Sprite, fallback.Frame); }
        return new(idle, 0);
    }
}

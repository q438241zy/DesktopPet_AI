namespace DesktopPet.Core;

/// <summary>A separate clock: changing a pose never counts as user activity.</summary>
public sealed class IdlePostureClock(string initial = "stand")
{
    public const double Interval = 30000;
    public string Pose { get; private set; } = initial == "sit" ? "sit" : "stand";
    private double next;
    private bool running;
    public void Reset(double now) { next = now + Interval; running = true; }
    public bool Tick(double now, bool eligible)
    {
        if (!eligible) { running = false; return false; }
        if (!running) { Reset(now); return false; }
        if (now < next) return false;
        Pose = Pose == "stand" ? "sit" : "stand";
        next = now + Interval;
        return true;
    }
}

public sealed record IdlePostureFrame(Sprite Sprite, int Frame, FivePose? Anchor = null);

/// <summary>The exact standing/seated source poses approved in CompanionV01.</summary>
public static class IdlePosture
{
    public static string Normalize(string? value) => value is "stand" or "sit" ? value : "auto";
    public static IdlePostureFrame Resolve(Character character, string outfit, string pose)
    {
        var idle = character.Outfits.GetValueOrDefault(outfit)?.Idle ?? character.Atlas;
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

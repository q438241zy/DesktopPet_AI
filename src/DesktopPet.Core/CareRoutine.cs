namespace DesktopPet.Core;

public sealed record CareStep(string Motion, int Duration);
public readonly record struct CarePose(int Step, string Motion, double Elapsed, int Duration);

/// <summary>Shared timing for affectionate care, used by the desktop and its exported Demo.</summary>
public sealed record CareRoutine(string Key, string Title, string Message, bool FallsAsleep, CareStep[] Steps)
{
    public int Duration => Steps.Sum(step => step.Duration);
    public CarePose At(double elapsed)
    {
        double time = Math.Max(0, elapsed);
        for (int i = 0; i < Steps.Length; i++)
        {
            var step = Steps[i];
            if (time < step.Duration || i == Steps.Length - 1)
                return new(i, step.Motion, Math.Min(time, step.Duration), step.Duration);
            time -= step.Duration;
        }
        throw new InvalidOperationException("Care routine requires a step.");
    }

    public static readonly CareRoutine[] All = [
        new("praise", "夸夸", "今天也很棒，给你一个小小的鼓励。", false,
            [new("listen", 600), new("happy", 1900), new("headpat", 1800), new("idle", 500)]),
        new("comfort", "安抚", "慢慢来，我陪你缓一缓。", false,
            [new("think", 1000), new("headpat", 2400), new("listen", 800)]),
        new("lullaby", "哄睡", "安心休息吧，我就在这里。", true,
            [new("think", 800), new("curl", 1500), new("sleep", 3000)])
    ];
    public static CareRoutine? Find(string key) => All.FirstOrDefault(routine => routine.Key == key);
}

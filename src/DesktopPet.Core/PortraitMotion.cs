namespace DesktopPet.Core;

public sealed record RigGesture(double Turn = 0, double Head = 0, double Squat = 0, double Lift = 0,
    double Sway = 0, double Shoulders = 0, double LeftArm = 0, double LeftElbow = 0, double RightArm = 0,
    double RightElbow = 0, double LeftStep = 0, double LeftLift = 0, double RightStep = 0, double RightLift = 0,
    double Feed = 0, double Cheeks = 0, double Mouth = 0, double Cloth = 0);

/// <summary>Distinct, eased gestures over the selected portrait. Angles are radians and lengths are fractions of image height.</summary>
public static class PortraitMotion
{
    public static readonly string[] Actions = ["headpat", "poke", "tickle", "eat", "meal", "sleep", "curl", "chat", "happy", "sad",
        "think", "jump", "build", "kick", "nudge", "ball-ready", "anticipate", "ball-hit", "ball-miss", "bonk", "peek", "farewell", "pickup", "shaken", "shaken-strong", "dizzy", "faint", "pounce"];
    public static bool Supports(string action) => Actions.Contains(action);
    public static string TouchRegion(string category, double y) => category == "chibi"
        ? y < .52 ? "headpat" : y < .73 ? "poke" : "tickle"
        : y < .11 ? "headpat" : y < .22 ? "poke" : "tickle";
    public static double Duration(string action) => action switch { "meal" => 4200, "eat" => 3000, "jump" => 1320, "bonk" => 1900, "tickle" => 2200, "build" => 4400, "curl" => 5500, "think" => 6500, _ => 2600 };
    public static RigGesture At(string action, double milliseconds, double duration, bool reducedMotion = false)
    {
        if (reducedMotion) return new();
        double t = Math.Max(0, milliseconds) / 1000;
        double envelope = Smooth(Math.Clamp(t / .24, 0, 1));
        if (action != "sleep" && duration > 0) envelope *= Smooth(Math.Clamp((duration - milliseconds) / 320, 0, 1));
        double E(double n) => n * envelope;
        double wave = Math.Sin(t * 7), fast = Math.Sin(t * 17);
        double bite = .5 - .5 * Math.Cos(t * Math.PI * 2 / 1.2);
        return action switch
        {
            "headpat" => new(Head: E(.12 * (1 - Math.Cos(t * 8)) / 2), Squat: E(.008 * Math.Pow(wave, 2)), LeftElbow: E(.07 * wave)),
            "poke" => new(Head: E(.09 * Math.Sin(t * 9)), Turn: E(-.025 * Math.Sin(t * 9)), Cheeks: E(.19 * Math.Sin(t * 12)), LeftElbow: E(.06)),
            "tickle" => new(Turn: E(.065 * fast), Head: E(-.07 * fast), Squat: E(.016 * Math.Pow(wave, 2)), Shoulders: E(.009 * Math.Abs(fast)), LeftArm: E(-.12), RightArm: E(.15), RightElbow: E(.13 * fast)),
            "eat" or "meal" => new(Head: E(.035 * Math.Sin(t * 10)), Turn: E(.012 * wave), Feed: E(.85 * bite), Mouth: E(.07 * Math.Sin(t * 24)), RightElbow: E(.08)),
            "sleep" => new(Turn: E(.06), Head: E(.2 + .014 * Math.Sin(t * 1.8)), Squat: E(.032 + .004 * Math.Sin(t * 1.8)), LeftElbow: E(.14), RightArm: E(.1), RightElbow: E(.16)),
            "curl" => new(Turn: E(-.06), Head: E(.19), Squat: E(.055), LeftArm: E(-.13), LeftElbow: E(.34), RightArm: E(.15), RightElbow: E(.38)),
            "chat" or "farewell" => new(Head: E(.035 * wave), LeftArm: E(.06), LeftElbow: E(.23 * wave), Shoulders: E(.002 * wave)),
            "happy" => new(Head: E(.055 * wave), Lift: E(-.014 * Math.Pow(wave, 2)), LeftElbow: E(.16 * wave), RightArm: E(-.1)),
            "sad" or "ball-miss" => new(Head: E(.18), Turn: E(.025), Shoulders: E(-.012), LeftElbow: E(-.09)),
            "think" => new(Head: E(-.06), Turn: E(.015)),
            "jump" or "pounce" => new(Lift: E(-.105 * Math.Pow(Math.Max(0, Math.Sin(Math.PI * Math.Clamp((t - .2) / .95, 0, 1))), 2)), Squat: E(.018 * Math.Exp(-Math.Pow((t - .18) * 9, 2))), LeftArm: E(.14), RightArm: E(-.19), Head: E(-.055)),
            "build" => new(Turn: E(-.035 * wave), Head: E(.1), Squat: E(.016 * bite), RightArm: E(-.2 * bite), RightElbow: E(-.14 * bite)),
            "kick" => new(Turn: E(.075 * bite), Head: E(.05), RightStep: E(.06 * bite), RightLift: E(-.045 * bite), LeftElbow: E(.1)),
            "nudge" => new(Turn: E(-.065 * bite), LeftStep: E(-.06 * bite), LeftLift: E(-.025 * bite), RightArm: E(-.09)),
            "ball-ready" or "anticipate" => new(Turn: E(.025 * wave), Head: E(-.055), Squat: E(.012), LeftElbow: E(.11), RightArm: E(-.16), RightElbow: E(-.22)),
            "ball-hit" => new(Squat: E(.034), Head: E(-.09), Turn: E(.065), LeftElbow: E(.24), RightArm: E(-.2), RightElbow: E(-.32)),
            "bonk" => new(Head: E(.12 * Math.Exp(-Math.Pow((t - .49) / .14, 2))), Squat: E(.012 * Math.Exp(-Math.Pow((t - .49) / .14, 2)))),
            "peek" => new(Turn: E(.06 * Math.Sin(t * 3)), Head: E(-.12), Sway: E(.018 * Math.Sin(t * 3))),
            "pickup" => new(Lift: E(-.016), Head: E(-.07), LeftStep: E(-.009 * wave), LeftLift: E(-.016), RightArm: E(-.05)),
            "shaken" or "shaken-strong" => new(Turn: E((action == "shaken" ? .055 : .1) * fast), Head: E(-.12 * fast), LeftElbow: E(.18 * fast), RightArm: E(-.1 * fast)),
            "dizzy" => new(Turn: E(.085 * Math.Sin(t * 4)), Head: E(-.1 * Math.Sin(t * 5)), Squat: E(.024)),
            "faint" => new(Turn: E(.17), Head: E(.2), Squat: E(.072), RightElbow: E(.2)),
            _ => new()
        };
    }
    private static double Smooth(double t) => t * t * (3 - 2 * t);
}

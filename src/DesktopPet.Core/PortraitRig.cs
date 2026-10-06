namespace DesktopPet.Core;

public readonly record struct RigPoint(double X, double Y)
{
    public static RigPoint operator +(RigPoint a, RigPoint b) => new(a.X + b.X, a.Y + b.Y);
    public static RigPoint operator -(RigPoint a, RigPoint b) => new(a.X - b.X, a.Y - b.Y);
    public static RigPoint operator *(RigPoint a, double s) => new(a.X * s, a.Y * s);
    public double Length => Math.Sqrt(X * X + Y * Y);
    public RigPoint Rotate(double radians) => new(X * Math.Cos(radians) - Y * Math.Sin(radians), X * Math.Sin(radians) + Y * Math.Cos(radians));
}
public readonly record struct RigBone(RigPoint A, RigPoint B);
public sealed record RigPose(RigBone[] Bones, double ClothSway, double Cheeks = 0, double Mouth = 0);

/// <summary>Shared 2D skeleton for 3D-realistic portraits, measured in image-height units.</summary>
public sealed class PortraitRig
{
    public const int Columns = 48, Rows = 72;
    public int MeshColumns => Columns;
    public int MeshRows => Rows;
    public double Aspect { get; }
    public RigBone[] Rest { get; }
    public RigPoint[] Vertices { get; }
    private readonly double[][] weights;
    private readonly bool longDress;
    private readonly RigPoint hips, neck, head, ls, le, lw, rs, re, rw, lh, lk, la, rh, rk, ra;
    public static bool Supports(string category, string family) => CharacterStyles.Normalize(category) == CharacterStyles.Realistic && family is "whale" or "gpt" or "claude" or "gemini" or "grok" or "qwen" or "zhipu" or "kimi";
    public RigPoint MouthRest => head + new RigPoint(.005, .047);

    public PortraitRig(string family, string outfit, double aspect = 2d / 3, string category = CharacterStyles.Realistic)
    {
        Aspect = aspect; longDress = outfit == "wedding";
        // Per-family landmarks follow the standing portrait; each outfit retains that character's pose.
        double dx = family switch { "gemini" => .04, "zhipu" => .02, "qwen" => .01, _ => 0 };
        double dy = family switch { "grok" or "qwen" => -.015, "zhipu" => .017, _ => 0 };
        RigPoint P(double x, double y) => new((x + dx - .5) * aspect, y + dy - .5);
        hips = P(.52, .465); neck = P(.515, .185); head = P(.50, .105);
        ls = P(.408, .21); le = P(.368, .322); lw = P(.337, .224);
        rs = P(.623, .22); re = P(.682, .359); rw = P(.749, .471);
        lh = P(.471, .473); lk = P(.517, .65); la = P(.424, .86);
        rh = P(.56, .48); rk = P(.548, .695); ra = P(.527, .924);
        Rest = [new(hips, neck), new(neck, head), new(ls, le), new(le, lw), new(rs, re), new(re, rw),
            new(lh, lk), new(lk, la), new(la, la + new RigPoint(.01, .045)), new(rh, rk), new(rk, ra), new(ra, ra + new RigPoint(.015, .045))];
        Vertices = Enumerable.Range(0, (MeshColumns + 1) * (MeshRows + 1)).Select(i =>
            new RigPoint((i % (MeshColumns + 1) / (double)MeshColumns - .5) * aspect, i / (MeshColumns + 1) / (double)MeshRows - .5)).ToArray();
        weights = Vertices.Select(Weights).ToArray();
    }

    public RigPose MotionPose(string action, double elapsed, double duration, bool reducedMotion = false)
    {
        var g = PortraitMotion.At(action, elapsed, duration, reducedMotion);
        if (g == new RigGesture()) return new(Rest.ToArray(), 0);
        var root = hips + new RigPoint(g.Sway, g.Squat + g.Lift);
        RigPoint Body(RigPoint point) => root + (point - hips).Rotate(g.Turn);
        var n = Body(neck);
        var bones = new RigBone[12]; bones[0] = new(root, n); bones[1] = new(n, n + (head - neck).Rotate(g.Head));
        void Arm(int index, RigPoint shoulder, RigPoint elbow, RigPoint wrist, double upper, double lower)
        {
            var s = Body(shoulder) + new RigPoint(0, -g.Shoulders);
            var e = s + (elbow - shoulder).Rotate(g.Turn + upper);
            var w = e + (wrist - elbow).Rotate(g.Turn + upper + lower);
            if (index == 2 && g.Feed > 0)
            {
                var mouth = n + (MouthRest - neck).Rotate(g.Head);
                w = w * (1 - g.Feed) + (mouth + new RigPoint(-.008, .085)) * g.Feed;
                e = SolveKnee(s, w, (elbow - shoulder).Length, (wrist - elbow).Length, -1);
            }
            bones[index] = new(s, e); bones[index + 1] = new(e, w);
        }
        Arm(2, ls, le, lw, g.LeftArm, g.LeftElbow); Arm(4, rs, re, rw, g.RightArm, g.RightElbow);
        void Leg(int index, RigPoint hip, RigPoint knee, RigPoint ankle, double x, double lift)
        {
            var h = Body(hip); var a = ankle + new RigPoint(x, g.Lift + lift);
            var d = ankle - hip; var thigh = knee - hip;
            var k = SolveKnee(h, a, thigh.Length, (ankle - knee).Length, Math.Sign(d.X * thigh.Y - d.Y * thigh.X));
            bones[index] = new(h, k); bones[index + 1] = new(k, a); bones[index + 2] = new(a, a + Rest[index + 2].B - ankle);
        }
        Leg(6, lh, lk, la, g.LeftStep, g.LeftLift); Leg(9, rh, rk, ra, g.RightStep, g.RightLift);
        return new(bones, g.Cloth + g.Turn * .13, g.Cheeks, g.Mouth);
    }

    public RigPoint Anchor(RigPose pose, int bone, RigPoint point)
    {
        double rotation = Math.Atan2((pose.Bones[bone].B - pose.Bones[bone].A).Y, (pose.Bones[bone].B - pose.Bones[bone].A).X)
            - Math.Atan2((Rest[bone].B - Rest[bone].A).Y, (Rest[bone].B - Rest[bone].A).X);
        return pose.Bones[bone].A + (point - Rest[bone].A).Rotate(rotation);
    }

    public RigPoint[] Skin(RigPose pose)
    {
        var angles = pose.Bones.Select((b, i) => Math.Atan2((b.B - b.A).Y, (b.B - b.A).X)
            - Math.Atan2((Rest[i].B - Rest[i].A).Y, (Rest[i].B - Rest[i].A).X)).ToArray();
        var cosines = angles.Select(Math.Cos).ToArray(); var sines = angles.Select(Math.Sin).ToArray();
        return Vertices.Select((v,i) =>
        {
            var faceOffset = v - (MouthRest + new RigPoint(0, -.012));
            double influence = Math.Exp(-2 * (Math.Pow(faceOffset.X / .038, 2) + Math.Pow(faceOffset.Y / .031, 2)));
            var local = v + new RigPoint(-faceOffset.X * pose.Cheeks, .014 * pose.Mouth) * influence;
            var result = new RigPoint();
            for (int b = 0; b < Rest.Length; b++)
                if (weights[i][b] > 0)
                {
                    var d = local - Rest[b].A;
                    var rotated=new RigPoint(d.X*cosines[b]-d.Y*sines[b],d.X*sines[b]+d.Y*cosines[b]);
                    result += (pose.Bones[b].A+rotated)*weights[i][b];
                }
            if (longDress && v.Y > hips.Y)
            {
                // A long skirt stays one continuous surface rather than stretching into two trouser legs.
                double t = Math.Clamp((v.Y - hips.Y) / .38, 0, 1);
                var cloth = v + (pose.Bones[0].A - hips) * (1 - t * .5) + new RigPoint(pose.ClothSway * t, 0);
                double blend = Smooth(Math.Min(1, (v.Y - hips.Y) / .075)) * (1 - Smooth(Math.Clamp((v.Y - .39) / .07, 0, 1)));
                result = result * (1 - blend) + cloth * blend;
            }
            return result;
        }).ToArray();
    }

    private double[] Weights(RigPoint p)
    {
        var w = new double[Rest.Length];
        for (int i = 0; i < w.Length; i++)
        {
            double distance = Distance(p, Rest[i]);
            w[i] = 1 / Math.Pow(.009 + distance, 4);
            // Stop the near-crossing arms and legs from pulling the central face/chest texture sideways.
            if (p.Y < neck.Y + .02 && i != 1) w[i] *= .02;
            if (p.Y >= neck.Y + .02 && p.Y < hips.Y && p.X > ls.X + .025 && p.X < rs.X - .025 && i != 0) w[i] *= .06;
            if (p.Y > hips.Y + .10 && i < 6) w[i] *= .01;
        }
        double sum = w.Sum(); return w.Select(value => value / sum).ToArray();
    }
    private static double Distance(RigPoint p, RigBone bone)
    {
        var d = bone.B - bone.A; var v = p - bone.A;
        double t = Math.Clamp((d.X * v.X + d.Y * v.Y) / Math.Max(.000001, d.X * d.X + d.Y * d.Y), 0, 1);
        return (p - (bone.A + d * t)).Length;
    }
    private static RigPoint SolveKnee(RigPoint hip, RigPoint ankle, double thigh, double shin, double bendSide)
    {
        var d = ankle - hip; double length = Math.Clamp(d.Length, .0001, thigh + shin - .000001);
        var axis = d * (1 / d.Length);
        double along = (thigh * thigh - shin * shin + length * length) / (2 * length);
        double across = Math.Sqrt(Math.Max(0, thigh * thigh - along * along));
        var midpoint = hip + axis * along; var normal = new RigPoint(-axis.Y, axis.X) * across;
        return midpoint + normal * (bendSide == 0 ? -1 : bendSide);
    }
    private static double Smooth(double t) => t * t * (3 - 2 * t);
}

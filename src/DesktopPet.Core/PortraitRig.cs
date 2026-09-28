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

/// <summary>Shared 2D skeleton for the built-in 3D-style and adult portraits, measured in image-height units.</summary>
public sealed class PortraitRig
{
    public const int Columns = 48, Rows = 72;
    public int MeshColumns => danceRig is null?Columns:Columns*2;
    public int MeshRows => danceRig is null?Rows:Rows*2;
    public double Aspect { get; }
    public RigBone[] Rest { get; }
    public RigPoint[] Vertices { get; }
    private readonly double[][] weights;
    private readonly bool longDress;
    private readonly DanceRig? danceRig;
    private readonly RigPoint hips, neck, head, ls, le, lw, rs, re, rw, lh, lk, la, rh, rk, ra;
    public static bool Supports(string category, string family) => category is "adult" or "3d" && family is "whale" or "gpt" or "claude" or "gemini" or "grok" or "qwen" or "zhipu" or "kimi";
    public static bool SupportsDance(string category, string family) => category == "adult" && Supports(category, family);
    public RigPoint MouthRest => head + new RigPoint(.005, .047);

    public PortraitRig(string family, string outfit, double aspect = 2d / 3, string category = "adult", DanceRig? danceRig = null, bool[]? silhouette = null)
    {
        Aspect = aspect; this.danceRig = danceRig; longDress = danceRig?.FloorLength ?? outfit == "wedding";
        // Per-family landmarks follow the standing portrait; each outfit retains that character's pose.
        double dx = family switch { "gemini" => .04, "zhipu" => .02, "qwen" => .01, _ => 0 };
        double dy = family switch { "grok" or "qwen" => -.015, "zhipu" => .017, _ => 0 };
        RigPoint P(double x, double y) => new((x + dx - .5) * aspect, y + dy - .5 + (category == "3d" ? .022 * Math.Clamp((.9 - y) / .55, 0, 1) : 0));
        hips = P(.52, .465); neck = P(.515, .185); head = P(.50, .105);
        ls = P(.408, .21); le = P(.368, .322); lw = P(.337, .224);
        rs = P(.623, .22); re = P(.682, .359); rw = P(.749, .471);
        lh = P(.471, .473); lk = P(.517, .65); la = P(.424, .86);
        rh = P(.56, .48); rk = P(.548, .695); ra = P(.527, .924);
        RigPoint Joint(int i) => new((danceRig!.Joints[i].X-.5)*aspect,danceRig.Joints[i].Y-.5);
        if(danceRig is not null)
        {
            hips=Joint(0);neck=Joint(1);head=Joint(2);
            ls=Joint(3);le=Joint(4);lw=Joint(5);rs=Joint(6);re=Joint(7);rw=Joint(8);
            lh=Joint(9);lk=Joint(10);la=Joint(11);rh=Joint(12);rk=Joint(13);ra=Joint(14);
        }
        Rest = [new(hips, neck), new(neck, head), new(ls, le), new(le, lw), new(rs, re), new(re, rw),
            new(lh, lk), new(lk, la), new(la, la + new RigPoint(.01, .045)), new(rh, rk), new(rk, ra), new(ra, ra + new RigPoint(.015, .045))];
        if(danceRig is not null) { Rest[8]=new(la,Joint(15));Rest[11]=new(ra,Joint(16)); }
        Vertices = Enumerable.Range(0, (MeshColumns + 1) * (MeshRows + 1)).Select(i =>
            new RigPoint((i % (MeshColumns + 1) / (double)MeshColumns - .5) * aspect, i / (MeshColumns + 1) / (double)MeshRows - .5)).ToArray();
        weights = silhouette is not null && danceRig is not null
            ? SilhouetteSkinning.Bind(Vertices,Rest,silhouette,MeshColumns,MeshRows,ClothingSeeds(silhouette),danceRig.ArmRadius) : Vertices.Select(Weights).ToArray();
    }

    private bool[] ClothingSeeds(bool[] silhouette)
    {
        var seeds=new bool[Vertices.Length];
        int stride=MeshColumns+1,centre=(int)Math.Round((hips.X/Aspect+.5)*MeshColumns);
        double top=danceRig!.Waist-.5-.015;
        double bottom=Math.Min(danceRig.Hem-.5,Math.Max(lw.Y,rw.Y)+.09);
        double ArmX(RigPoint elbow,RigPoint wrist,double y)
        {
            double t=Math.Clamp((y-elbow.Y)/(wrist.Y-elbow.Y),0,1);
            return elbow.X+(wrist.X-elbow.X)*t;
        }
        for(int row=0;row<=MeshRows;row++)
        {
            int at=row*stride+centre;double y=Vertices[at].Y;
            if(y>=neck.Y-.03&&y<=top+.025)
                for(int x=0;x<=MeshColumns;x++)
                {
                    int i=row*stride+x;
                    if(silhouette[i]&&Vertices[i].X>ls.X+.018&&Vertices[i].X<rs.X-.018)seeds[i]=true;
                }
            if(y<top||y>bottom||!silhouette[at])continue;
            int left=centre,right=centre;
            while(left>0&&silhouette[row*stride+left-1])left--;
            while(right<MeshColumns&&silhouette[row*stride+right+1])right++;
            // Seed only a central span separated from both arm centre lines.
            // Rows joined by a veil or sleeve inherit from clear rows nearby.
            if(Vertices[row*stride+left].X<=ArmX(le,lw,y)+.022 ||
                Vertices[row*stride+right].X>=ArmX(re,rw,y)-.022)continue;
            for(int x=left;x<=right;x++)seeds[row*stride+x]=true;
        }
        return seeds;
    }

    public RigPose Pose(double milliseconds, bool reducedMotion = false)
    {
        if (danceRig is null || reducedMotion || milliseconds<=0 || milliseconds>=PetDance.DurationMs) return new(Rest.ToArray(), 0);
        var step = DanceSteps.At(milliseconds,longDress);
        double turn = -step.Sway*.22;
        var root = hips + new RigPoint(step.Sway,step.Bend);
        RigPoint Body(RigPoint p) => root + (p - hips).Rotate(turn);
        var n = Body(neck);
        var bones = new RigBone[12]; bones[0] = new(root, n); bones[1] = new(n, n + (head - neck).Rotate(-turn*.4+step.Bow*.035));
        void Arm(int index, RigPoint shoulder, RigPoint elbow, RigPoint wrist, double upper, double lower)
        {
            var s = Body(shoulder); var e = s + (elbow - shoulder).Rotate(turn + upper);
            var w = e + (wrist - elbow).Rotate(turn + upper + lower);
            bones[index] = new(s, e); bones[index + 1] = new(e, w);
        }
        Arm(2,ls,le,lw,step.LeftArm,step.LeftElbow);
        Arm(4,rs,re,rw,step.RightArm,step.RightElbow);
        void Leg(int index, RigPoint hip, RigPoint knee, RigPoint ankle, double offset, double lift)
        {
            var h = Body(hip);
            var a = ankle + new RigPoint(offset,lift);
            // This texture faces the camera: a bent knee travels in depth, not
            // sideways. Planar IK incorrectly made both knees bow out like a frog.
            double ratio=(knee-hip).Length/((knee-hip).Length+(ankle-knee).Length);
            var k=h+(a-h)*ratio+(knee-(hip+(ankle-hip)*ratio));
            bones[index] = new(h, k); bones[index + 1] = new(k, a);
            bones[index + 2] = new(a, a + (Rest[index + 2].B - ankle));
        }
        Leg(6,lh,lk,la,step.LeftX,step.LeftLift);Leg(9,rh,rk,ra,step.RightX,step.RightLift);
        return new(bones,-step.Sway*.1);
    }

    public RigPose MotionPose(string action, double elapsed, double duration, bool reducedMotion = false)
    {
        if (action == "dance") return Pose(elapsed, reducedMotion);
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
                    if(danceRig is not null && b is 6 or 7 or 9 or 10)
                    {
                        var axis=pose.Bones[b].B-pose.Bones[b].A;double length=axis.Length;
                        axis*=1/Math.Max(.0001,length);
                        double projected=length/(Rest[b].B-Rest[b].A).Length;
                        rotated+=axis*((rotated.X*axis.X+rotated.Y*axis.Y)*(projected-1));
                    }
                    result += (pose.Bones[b].A+rotated)*weights[i][b];
                }
            if (danceRig is { } dress && v.Y>dress.Waist-.5-.02 && v.Y<dress.Hem-.5+.03)
            {
                double top=dress.Waist-.5,bottom=dress.Hem-.5;
                double t=Math.Clamp((v.Y-top)/(bottom-top),0,1);
                double radius;
                if(dress.ClothWidths is { } widths)
                {
                    double band=t*4;int at=Math.Min(3,(int)band);
                    radius=Aspect*(widths[at]+(widths[at+1]-widths[at])*(band-at));
                }
                else radius=Aspect*(.19+(dress.SkirtWidth-.19)*t);
                double horizontal=1-Smooth(Math.Clamp((Math.Abs(v.X-hips.X)-radius)/.012,0,1));
                double vertical=Smooth(Math.Clamp((v.Y-top+.02)/.04,0,1))*(1-Smooth(Math.Clamp((v.Y-bottom)/.03,0,1)));
                // A high-low hem leaves real legs visible in the centre. Those
                // pixels follow the leg, while the side train remains cloth.
                if(dress.FrontHem is { } front)
                {
                    double opening=1-Smooth(Math.Clamp((Math.Abs(v.X-hips.X)-dress.OpeningWidth*Aspect+.012)/.024,0,1));
                    vertical*=1-opening*Smooth(Math.Clamp((v.Y-(front-.5))/.035,0,1));
                }
                if(longDress && v.Y>Math.Min(la.Y,ra.Y)-.015)
                {
                    double footDistance=Math.Min(Math.Abs(v.X-la.X),Math.Abs(v.X-ra.X));
                    horizontal*=Smooth(Math.Clamp((footDistance-.027)/.018,0,1));
                }
                var offset=pose.Bones[0].A-hips;
                var cloth=v+new RigPoint(offset.X*(1-(longDress?.45:0)*t)+pose.ClothSway*t,
                    offset.Y*(longDress?Math.Pow(1-t,2):1));
                double armWeight=weights[i][2]+weights[i][3]+weights[i][4]+weights[i][5];
                double blend=horizontal*vertical*(1-Smooth(Math.Min(1,armWeight/.2)));
                result=result*(1-blend)+cloth*blend;
            }
            else if (danceRig is null && longDress && v.Y > hips.Y)
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
        if(danceRig is not null) return DanceWeights(p);
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
    private double[] DanceWeights(RigPoint p)
    {
        // Smooth distance fields are essential: hard region ownership creates
        // adjacent vertices with different transforms and turns fingers into spikes.
        var w=new double[Rest.Length];
        double headRegion=1-Smooth(Math.Clamp((p.Y-neck.Y+.015)/.065,0,1));
        double chestX=Smooth(Math.Clamp((p.X-ls.X-.018)/.05,0,1))*
            (1-Smooth(Math.Clamp((p.X-rs.X+.068)/.05,0,1)));
        double chestY=Smooth(Math.Clamp((p.Y-neck.Y)/.05,0,1))*
            (1-Smooth(Math.Clamp((p.Y-hips.Y+.025)/.065,0,1)));
        double legs=Smooth(Math.Clamp((p.Y-hips.Y-.025)/.10,0,1));
        double right=Smooth(Math.Clamp((p.X-hips.X+.012)/.024,0,1));
        for(int i=0;i<w.Length;i++)
        {
            w[i]=1/Math.Pow(.012+Distance(p,Rest[i]),4);
            if(i!=1)w[i]*=1-.999*headRegion;
            if(i!=0)w[i]*=1-.97*chestX*chestY;
            if(i<6)w[i]*=1-.995*legs;
            if(i>=6&&i<=8)w[i]*=1-.998*legs*right;
            if(i>=9)w[i]*=1-.998*legs*(1-right);
        }
        double sum=w.Sum();for(int i=0;i<w.Length;i++)w[i]/=sum;
        double sole=Smooth(Math.Clamp((p.Y-Math.Max(la.Y,ra.Y)-.008)/.032,0,1));
        for(int i=0;i<w.Length;i++)w[i]*=1-sole;
        w[8]+=sole*(1-right);w[11]+=sole*right;
        return w;
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

namespace DesktopPet.Core;

/// <summary>Measured UV joints for one neutral, uncrossed dance texture.</summary>
public sealed record DanceRig(RigPoint[] Joints, double Waist, double Hem, double SkirtWidth,
    double[]? ClothWidths = null, double? FrontHem = null, double OpeningWidth = 0,double ArmRadius=0)
{
    private static readonly (int A,int B)[] Links=[(0,1),(1,2),(3,4),(4,5),(6,7),(7,8),(9,10),(10,11),(11,15),(12,13),(13,14),(14,16)];
    public bool FloorLength => Hem > .82;
    public bool IsValid => Joints is { Length:17 } && Joints.All(p=>double.IsFinite(p.X)&&double.IsFinite(p.Y)&&p.X is >=0 and <=1&&p.Y is >=0 and <=1)
        && Links.All(link=>(Joints[link.A]-Joints[link.B]).Length>.005)
        && Joints[5].Y>Joints[4].Y+.005 && Joints[8].Y>Joints[7].Y+.005
        && double.IsFinite(Waist) && Waist is >.2 and <.65 && double.IsFinite(Hem) && Hem>Waist && Hem<=1
        && double.IsFinite(SkirtWidth) && SkirtWidth is >0 and <=.5
        && (ClothWidths is null || ClothWidths.Length==5 && ClothWidths.All(w=>double.IsFinite(w)&&w is >0 and <=.6))
        && (FrontHem is null || double.IsFinite(FrontHem.Value) && FrontHem>=Waist && FrontHem<=Hem)
        && double.IsFinite(OpeningWidth) && OpeningWidth is >=0 and <.5
        && double.IsFinite(ArmRadius) && ArmRadius is >=0 and <=.05;
}

/// <summary>Sixteen counts: step-touch left/right, open arms, close and a soft bow.</summary>
public static class DanceSteps
{
    public readonly record struct Step(double LeftX,double RightX,double LeftLift,double RightLift,double Sway,double Bend,double LeftArm,double RightArm,double LeftElbow,double RightElbow,double Bow);
    // Feet move one at a time. A planted supporting foot never drifts with the torso.
    private static readonly double[] Left = [0,-.042,-.042,-.042,0,0,.052,0,0,-.035,-.035,-.035,0,0,0,0,0];
    private static readonly double[] Right = [0,0,-.052,0,0,.042,.042,.042,0,0,-.045,0,0,.03,0,0,0];
    private static readonly double[] ArmL = [0,.22,.15,.30,.12,.15,.34,.18,.22,.38,.42,.30,.18,.26,.12,.04,0];
    private static readonly double[] ArmR = [0,-.12,-.25,-.15,-.22,-.30,-.15,-.26,-.12,-.30,-.38,-.42,-.18,-.26,-.12,-.04,0];
    public static Step At(double milliseconds, bool floorLength=false)
    {
        double beat=Math.Clamp(milliseconds/PetDance.BeatMs,0,16);
        if(beat<=0||beat>=16)return new();
        int i=Math.Min(15,(int)beat); double t=beat-i,s=t*t*t*(t*(t*6-15)+10);
        double Lerp(double[] a)=>a[i]+(a[i+1]-a[i])*s;
        double envelope=Math.Min(1,Math.Min(beat,16-beat)); envelope=envelope*envelope*(3-2*envelope);
        double lift=Math.Pow(Math.Sin(Math.PI*t),2)*.013*envelope;
        double l=Lerp(Left),r=Lerp(Right),stride=floorLength?.55:1;
        double bow=beat>14?Math.Pow(Math.Sin((beat-14)*Math.PI/2),2):0;
        return new(l*stride,r*stride,Left[i]!=Left[i+1]?-lift*stride:0,Right[i]!=Right[i+1]?-lift*stride:0,
            (l+r)*.5*stride,(.014+.007*Math.Pow(Math.Sin(beat*Math.PI),2))*envelope+bow*.012,
            // A frontal single-layer texture supports a gentle arm arc; larger
            // rotations expose unseen underarm/veil areas and stretch the art.
            Lerp(ArmL)*.4,Lerp(ArmR)*.4,.055*Math.Sin(beat*Math.PI/4)*envelope,-.055*Math.Sin(beat*Math.PI/4+.4)*envelope,bow);
    }
}

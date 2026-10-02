namespace DesktopPet.Core;

public readonly record struct ClubPose(int A, int B, double Amount, int Count, bool Blowing);

/// <summary>The approved CloudClub choreography; effect emission shares the pose clock.</summary>
public static class ClubMotion
{
    public static readonly string[] Actions = ["comb", "wipe", "stretch", "bubbles", "stars", "butterfly"];
    public static bool HasPoses(string action) => action is "stars" or "bubbles" or "stretch";
    public static int Duration(string action) => action switch
    { "comb" => 4800, "wipe" => 4200, "stretch" => 5200, "bubbles" => 14400, "stars" => 9600, "butterfly" => 11500, _ => 2600 };
    public static string Title(string action) => action switch
    { "comb" => "梳头", "wipe" => "擦脸", "stretch" => "伸懒腰", "bubbles" => "吹泡泡", "stars" => "数星星", "butterfly" => "捉蝴蝶", _ => action };
    public static ClubPose Sample(string action, double elapsed)
    {
        double[] times = action switch
        {
            "stars" => [0,750,1700,2900,4300,5700,7950,9400],
            "bubbles" => [0,420,820,1120,1800,2400,2970,3600],
            "stretch" => [0,500,1000,1550,2700,3650,4350,5050],
            _ => throw new ArgumentException("This action has no dedicated sheet.", nameof(action))
        };
        elapsed = Math.Max(0, elapsed);
        double time = action == "bubbles" ? elapsed % 3600 : elapsed;
        int i=0; while(i<7 && time>=times[i+1]) i++;
        int next=Math.Min(i+1,7), offset=action=="bubbles"?8:action=="stretch"?16:0;
        double start=action=="stars" && i==5?7100:times[i];
        double t=next==i?0:Math.Clamp((time-start)/(times[next]-start),0,1);
        int count=action=="stars"?new[]{1800,3050,4450,5850,7000}.Count(at=>elapsed>=at):0;
        return new(offset+i,offset+next,t*t*(3-2*t),count,action=="bubbles" && time>=1120 && time<2250);
    }
}

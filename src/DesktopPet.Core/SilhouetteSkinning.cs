namespace DesktopPet.Core;

/// <summary>Bind through opaque pixels, so a nearby hand cannot grab a skirt across an air gap.</summary>
public static class SilhouetteSkinning
{
    public static double[][] Bind(RigPoint[] vertices,RigBone[] bones,bool[] opaque,int columns,int rows,bool[]? bodySeeds=null,double armRadius=0)
    {
        if(opaque.Length!=vertices.Length||vertices.Length!=(columns+1)*(rows+1)||!opaque.Any(x=>x)
            ||bodySeeds is not null&&bodySeeds.Length!=vertices.Length)
            throw new ArgumentException("The silhouette must match the mesh and contain visible pixels.");
        int stride=columns+1,count=vertices.Length;
        IEnumerable<int> Neighbours(int at)
        {
            int x=at%stride,y=at/stride;
            for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                if((dx!=0||dy!=0)&&x+dx>=0&&x+dx<=columns&&y+dy>=0&&y+dy<=rows)
                    yield return at+dy*stride+dx;
        }
        double Distance(RigPoint p,RigBone b)
        {
            var d=b.B-b.A;var q=p-b.A;
            double t=Math.Clamp((q.X*d.X+q.Y*d.Y)/Math.Max(1e-9,d.X*d.X+d.Y*d.Y),0,1);
            return (p-(b.A+d*t)).Length;
        }
        var weights=vertices.Select(_=>new double[bones.Length]).ToArray();
        for(int bone=0;bone<bones.Length;bone++)
        {
            var guide=bones[bone];
            if(bone is 3 or 5)guide=guide with { B=guide.B+(guide.B-guide.A)*(.08/(guide.B-guide.A).Length) };
            double nearest=vertices.Where((_,i)=>opaque[i]).Min(p=>Distance(p,guide));
            var distance=Enumerable.Repeat(double.PositiveInfinity,count).ToArray();
            var queue=new PriorityQueue<int,double>();
            for(int i=0;i<count;i++)if(opaque[i])
            {
                double d=Distance(vertices[i],guide);
                // Give the visible thickness of an arm a firm limb attachment.
                // Otherwise hair touching an elbow can pin the inner skin to the
                // torso and stretch that skin when even a small arc is played.
                if(armRadius>0 && bone is >=2 and <=5 && Distance(vertices[i],bones[bone])<=armRadius && bodySeeds?[i]!=true)d=0;
                // Wide clothing needs body seeds of its own: a narrow torso bone
                // alone lets nearby sleeves win the upper corners of a skirt.
                if(bone==0 && bodySeeds?[i]==true)d=0;
                if(d<=nearest+.013){distance[i]=d;queue.Enqueue(i,d);}
            }
            while(queue.TryDequeue(out int at,out double cost))
            {
                if(cost>distance[at])continue;
                foreach(int next in Neighbours(at))if(opaque[next])
                {
                    double d=cost+(vertices[next]-vertices[at]).Length;
                    if(d<distance[next]){distance[next]=d;queue.Enqueue(next,d);}
                }
            }
            for(int i=0;i<count;i++)if(opaque[i])weights[i][bone]=1/Math.Pow(.006+distance[i],4);
        }
        for(int i=0;i<count;i++)if(opaque[i])
        {
            double sum=weights[i].Sum();
            if(sum>0){for(int b=0;b<bones.Length;b++)weights[i][b]/=sum;}
            else weights[i][vertices[i].Y<bones[0].B.Y?1:0]=1;
        }
        // Transparent edge vertices inherit the closest silhouette region rather
        // than a competing limb. This preserves antialiased fingers and hems.
        var owners=Enumerable.Repeat(-1,count).ToArray();var fill=new PriorityQueue<int,double>();
        var distances=Enumerable.Repeat(double.PositiveInfinity,count).ToArray();
        for(int i=0;i<count;i++)if(opaque[i]){owners[i]=i;distances[i]=0;fill.Enqueue(i,0);}
        while(fill.TryDequeue(out int at,out double cost))
        {
            if(cost>distances[at])continue;
            foreach(int next in Neighbours(at))
            {
                double d=cost+(vertices[next]-vertices[at]).Length;
                if(d<distances[next]){distances[next]=d;owners[next]=owners[at];fill.Enqueue(next,d);}
            }
        }
        for(int i=0;i<count;i++)if(!opaque[i])weights[i]=weights[owners[i]].ToArray();
        return weights;
    }
}

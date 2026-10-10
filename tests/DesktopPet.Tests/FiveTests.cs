using DesktopPet.Core;

internal static class FiveTests
{
    internal static void Run(Action<string,Action> test)
    {
        void Check(bool value){if(!value)throw new Exception("Interaction contract failed.");}
        void Advance(FiveInteraction m,int ms){for(int i=0;i<ms;i+=10)m.Advance(Math.Min(10,ms-i));}
        test("five: contact requires user action and completes once",()=>
        {
            var m=new FiveInteraction(new());m.Start("highfive");Advance(m,5000);Check(m.HighCount==0);
            Check(m.HighFive());Check(!m.HighFive());Advance(m,470);Check(m.HighCount==0);Advance(m,10);Check(m.HighCount==1);Advance(m,2500);Check(m.HighCount==1);
        });
        test("five: both RPS hands reveal after three actual fist cycles",()=>
        {
            var m=new FiveInteraction(new(),new Random(4));m.Start("rps");Check(!m.Choose("wrong"));Check(m.Choose("paper"));Check(!m.Choose("rock"));
            var seen=new HashSet<string>();for(int t=0;t<1800;t+=20){seen.Add(m.Pose);Check(m.Pet is null);m.Advance(20);}
            Check(seen.SetEquals(new[]{"fist-up","fist-mid","fist-down"}));Check(m.Pet is not null && m.Outcome is null);Advance(m,800);Check(m.Outcome==FiveInteraction.Result("paper",m.Pet!)&&m.Rounds==1);
        });
        test("gift automatically hands over, holds, opens and collects exactly once",()=>
        {
            var s=new PetState();var m=new FiveInteraction(s,new Random(5));Check(m.Start("gift")&&m.Phase=="delivering");
            Advance(m,1140);Check(m.Phase=="delivering");Advance(m,10);Check(m.Phase=="holding");
            Advance(m,1000);Check(m.Phase=="opening"&&s.Treasures.Count==0);Advance(m,890);Check(s.Treasures.Count==0);
            m.Start("highfive");Advance(m,4000);Check(s.Treasures.Count==0&&s.GiftDrawRemaining.Count==20);
            var bag=new HashSet<string>();string? previous=null;
            for(int i=0;i<41;i++){m.Start("gift");Advance(m,3050);Check(m.Prize!=previous&&m.Phase=="opened");if(i<20)Check(bag.Add(m.Prize!));previous=m.Prize;Advance(m,8000);Check(m.Finished&&s.Treasures.Count==i+1);}
            Check(bag.Count==20&&s.Treasures.Count==41&&m.Revision==41);
        });
        test("ten stories randomly start, pause, fully read and persist without losing old collections",()=>
        {
            var root=Path.Combine(Environment.CurrentDirectory,".artifacts","core-story-"+Guid.NewGuid().ToString("N"));
            try
            {
                var store=new StateStore(root);var s=new PetState();var m=new FiveInteraction(s,new Random(7));m.Start("read");Check(m.Phase=="reading");
                var first=m.Story.Id;m.Start("read");Check(m.Story.Id!=first);Advance(m,2000);m.PauseRead();double time=m.Time;Advance(m,20000);Check(m.Time==time&&m.Sentence==0&&s.ReadStories.Count==0);
                m.PauseRead();Advance(m,8000);Check(m.Sentence>0);m.Start("highfive");Check(s.ReadStories.Count==0);
                Check(StoryLibrary.All.Count==10&&StoryLibrary.All.Select(x=>x.Id).Distinct().Count()==10);
                foreach(var story in StoryLibrary.All)
                {
                    Check(story.Sentences.Count is >=5 and <=10);m.Start("read");m.SelectStory(story.Id);m.BeginRead();
                    int spent=0;while(m.Phase!="finishing"&&spent<100000){Check(!s.ReadStories.ContainsKey(story.Id));m.Advance(10);spent+=10;}
                    Check(spent>=story.Sentences.Count*3600&&m.Phase=="finishing");Advance(m,840);Check(!s.ReadStories.ContainsKey(story.Id));Advance(m,10);Check(s.ReadStories[story.Id]==1);
                    Advance(m,20000);Check(s.ReadStories[story.Id]==1&&m.Finished);
                }
                store.Save(s);var restored=store.Load();Check(restored.ReadStories.Count==10&&restored.ReadStories.Values.All(n=>n==1));
                Check(new[]{"cloud-post","little-bell","star-seed"}.All(id=>restored.ReadStories.ContainsKey(id)));
            }
            finally{if(Directory.Exists(root))Directory.Delete(root,true);}
        });
        test("removed photo action cannot start or produce rewards",()=>
        {var s=new PetState();var m=new FiveInteraction(s);Check(!m.Start("photo")&&!FiveInteraction.Keys.Contains("photo")&&s.Treasures.Count==0);});
        test("butterfly stays nearby, turns smoothly and gait follows real travel",()=>
        {
            foreach(int side in new[]{-1,1})
            {
                var pursuit=new ButterflyPursuit(500,100,50,950,side);double previous=pursuit.Center(0),distance=0;var walk=new WalkPlayback();var clip=new Sprite("walk.png",4,3);
                for(int t=16;t<=11520;t+=16){double next=pursuit.Center(t);Check(next is >=418 and <=582);double delta=next-previous;Check(Math.Abs(delta)<2);distance+=Math.Abs(delta);walk.Travel(delta,200,clip);previous=next;}
                Check(Math.Abs(previous-500)<.001&&Math.Abs(walk.Milliseconds-distance/DesktopWalk.Speed(200,clip)*1000)<.001);
                foreach(int turn in new[]{2300,4600,6900,9200})Check(Math.Abs(pursuit.Center(turn+1)-pursuit.Center(turn))<.001);
            }
            var edge=new ButterflyPursuit(60,100,50,950,-1);for(int t=0;t<13000;t+=17)Check(edge.Center(t)>=50);
        });
    }
}

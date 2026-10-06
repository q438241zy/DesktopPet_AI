using DesktopPet.Core;

internal static class FiveTests
{
    internal static void Run(Action<string,Action> test)
    {
        void Check(bool value){if(!value)throw new Exception("Interaction contract failed.");}
        void Advance(FiveInteraction m,int ms){for(int i=0;i<ms;i+=20)m.Advance(Math.Min(20,ms-i));}
        test("five: contact requires user action and completes once",()=>
        {
            var m=new FiveInteraction(new());m.Start("highfive");Advance(m,5000);Check(m.HighCount==0);
            Check(m.HighFive());Check(!m.HighFive());Advance(m,460);Check(m.HighCount==0);Advance(m,20);Check(m.HighCount==1);Advance(m,2500);Check(m.HighCount==1);
        });
        test("five: both RPS hands reveal after three actual fist cycles",()=>
        {
            var m=new FiveInteraction(new(),new Random(4));m.Start("rps");Check(!m.Choose("wrong"));Check(m.Choose("paper"));Check(!m.Choose("rock"));
            var seen=new HashSet<string>();for(int t=0;t<1800;t+=20){seen.Add(m.Pose);Check(m.Pet is null);m.Advance(20);}
            Check(seen.SetEquals(new[]{"fist-up","fist-mid","fist-down"}));Check(m.Pet is not null && m.Outcome is null);Advance(m,800);Check(m.Outcome==FiveInteraction.Result("paper",m.Pet!)&&m.Rounds==1);
        });
        test("five: gift commits after opening; cancelled gifts consume no draw",()=>
        {
            var s=new PetState();var m=new FiveInteraction(s,new Random(5));m.Start("gift");Check(!m.Unwrap());Check(m.Deliver());Advance(m,650);Check(m.Unwrap());Advance(m,980);Check(s.Treasures.Count==0&&s.GiftDrawRemaining.Count==20);
            m.Start("photo");Advance(m,3000);Check(s.Treasures.Count==0&&s.GiftDrawRemaining.Count==20);
            var bag=new HashSet<string>();string? previous=null;
            for(int i=0;i<41;i++){m.Start("gift");m.Deliver();Advance(m,650);m.Unwrap();Advance(m,1000);Check(m.Prize!=previous);if(i<20)Check(bag.Add(m.Prize!));previous=m.Prize;}
            Check(bag.Count==20&&s.Treasures.Count==41&&m.Revision==41);
        });
        test("five: pause, finish, replay and story collection survive disk reload",()=>
        {
            var root=Path.Combine(Path.GetTempPath(),"pet-five-"+Guid.NewGuid().ToString("N"));
            try
            {
                var store=new StateStore(root);var s=new PetState();var m=new FiveInteraction(s);m.Start("read");m.BeginRead();Advance(m,2000);m.PauseRead();double time=m.Time;Advance(m,20000);Check(m.Time==time&&m.Sentence==0&&s.ReadStories.Count==0);
                m.NextSentence();Advance(m,700);Check(m.Sentence==1);m.Start("photo");Check(s.ReadStories.Count==0);
                foreach(var story in StoryLibrary.All)
                {
                    m.Start("read");m.SelectStory(story.Id);m.BeginRead();for(int i=0;i<story.Sentences.Count;i++){m.NextSentence();Advance(m,i==story.Sentences.Count-1?850:700);}Check(s.ReadStories[story.Id]==1);Advance(m,20000);Check(s.ReadStories[story.Id]==1);
                }
                store.Save(s);var restored=store.Load();Check(restored.ReadStories.Count==3&&restored.ReadStories.Values.All(n=>n==1));
            }
            finally{if(Directory.Exists(root))Directory.Delete(root,true);}
        });
        test("five: cancelled and completed photo countdowns do not create rewards",()=>
        {
            var s=new PetState();var m=new FiveInteraction(s);m.Start("photo");Check(!m.PhotoSaved());m.Shutter();Advance(m,2980);Check(m.Phase=="countdown");Advance(m,20);Check(m.Phase=="capture");Check(m.PhotoSaved());Check(!m.PhotoSaved());Check(m.Photos==1&&s.Treasures.Count==0&&s.ReadStories.Count==0);
        });
    }
}

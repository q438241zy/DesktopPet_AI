namespace DesktopPet.Core;

/// <summary>The approved interactions in both styles. Rewards commit only after a completed handoff/read.</summary>
public sealed class FiveInteraction(PetState state,Random? random=null)
{
    private readonly Random rng=random??Random.Shared;
    private string hiddenHand="rock";
    private string? pendingGift;
    private string pausedPhase="reading";
    private int pendingSentence;
    public static readonly string[] Keys=["highfive","rps","gift","read"];
    public static readonly string[] Hands=["rock","scissors","paper"];
    public static readonly string[] PoseNames=["neutral","high-prep","high-ready","high-contact","high-recoil","fist-up","fist-mid","fist-down","reveal-rock","reveal-scissors","reveal-paper","receive","gift","gift-empty","read","page","read-finish","photo","lift","happy"];
    public string Key { get; private set; }="highfive";
    public string Phase { get; private set; }="offer";
    public double Time { get; private set; }
    public int HighCount { get; private set; }
    public int Rounds { get; private set; }
    public string? Player { get; private set; }
    public string? Pet { get; private set; }
    public string? Outcome { get; private set; }
    public string? Prize { get; private set; }
    public string? VisiblePrize=>Prize??pendingGift;
    public bool NewPrize { get; private set; }
    public int Revision { get; private set; }
    public CompanionStory Story { get; private set; }=StoryLibrary.All[0];
    public int Sentence { get; private set; }
    public double SentenceDuration=>Math.Clamp(Story.Sentences[Sentence].Length*155+1100,3600,7600);
    public bool Finished => Key == "gift" && Phase == "opened" && Time >= 3050 || Key == "read" && Phase == "finished" && Time >= 2400;
    public bool Start(string key)
    {
        if(!Keys.Contains(key))return false;
        Key=key;Phase=key switch { "highfive"=>"offer","rps"=>"choose","gift"=>"delivering",_=>"reading" };
        Time=0;Player=Pet=Outcome=Prize=pendingGift=null;Sentence=pendingSentence=0;
        if (key == "read") { var stories = StoryLibrary.All.Where(s => s.Id != state.LastReadStory).ToArray(); Story = stories[rng.Next(stories.Length)]; state.LastReadStory = Story.Id; }
        hiddenHand=Hands[rng.Next(3)];return true;
    }
    public bool HighFive(){if(Key!="highfive"||Phase!="offer"||Time<750)return false;Phase="approach";Time=0;return true;}
    public bool Choose(string hand){if(Key!="rps"||Phase!="choose"||!Hands.Contains(hand))return false;Player=hand;Phase="countdown";Time=0;return true;}
    public bool Deliver(){if(Key!="gift"||Phase!="receive")return false;Phase="delivering";Time=0;return true;}
    public bool Unwrap()
    {
        if(Key!="gift"||Phase!="holding")return false;
        var candidates=state.GiftDrawRemaining.Count>0?state.GiftDrawRemaining:Collectibles.All.Select(x=>x.Id).Where(x=>x!=state.LastGift).ToList();
        pendingGift=candidates[rng.Next(candidates.Count)];Phase="opening";Time=0;return true;
    }
    public bool SelectStory(string id){if(Key!="read"||StoryLibrary.Find(id) is not {} story)return false;Story=story;state.LastReadStory=id;Sentence=pendingSentence=0;Phase="ready";Time=0;return true;}
    public bool BeginRead(){if(Key!="read"||Phase is not ("ready" or "finished"))return false;Sentence=0;Phase="reading";Time=0;return true;}
    public bool PauseRead()
    {
        if(Key!="read")return false;
        if(Phase=="paused"){Phase=pausedPhase;return true;}
        if(Phase is not ("reading" or "turning"))return false;pausedPhase=Phase;Phase="paused";return true;
    }
    public bool NextSentence()
    {
        if(Key!="read"||Phase is not ("reading" or "paused"))return false;
        Time=0;if(Sentence==Story.Sentences.Count-1)Phase="finishing";
        else {pendingSentence=Sentence+1;Phase="turning";}return true;
    }
    public void Advance(double ms)
    {
        if(Key=="read"&&Phase=="paused")return;
        Time+=Math.Clamp(double.IsFinite(ms)?ms:0,0,100);
        if(Key=="highfive")
        {
            if(Phase=="approach"&&Time>=480){HighCount++;Phase="contact";Time=0;}
            else if(Phase=="contact"&&Time>=1200){Phase="offer";Time=0;}
        }
        if(Key=="rps")
        {
            if(Phase=="countdown"&&Time>=1800){Pet=hiddenHand;Phase="shoot";Time=0;}
            else if(Phase=="shoot"&&Time>=800){Outcome=Result(Player!,Pet!);Rounds++;Phase="revealed";Time=0;}
        }
        if(Key=="gift"&&Phase=="delivering"&&Time>=1150){Phase="holding";Time=0;}
        if(Key=="gift"&&Phase=="holding"&&Time>=1000) Unwrap();
        if(Key=="gift"&&Phase=="opening"&&Time>=900)
        {
            Prize=pendingGift!;NewPrize=!state.Treasures.Any(x=>Collectibles.FromSavedName(x)?.Id==Prize);
            state.Treasures.Add(Collectibles.Get(Prize).Name);
            if(state.GiftDrawRemaining.Count==0)state.GiftDrawRemaining=Collectibles.All.Select(x=>x.Id).ToList();
            state.GiftDrawRemaining.Remove(Prize);state.LastGift=Prize;pendingGift=null;Revision++;Phase="opened";Time=0;
        }
        if(Key=="read")
        {
            if(Phase=="reading"&&Time>=SentenceDuration)NextSentence();
            else if(Phase=="turning"&&Time>=650){Sentence=pendingSentence;Phase="reading";Time=0;}
            else if(Phase=="finishing"&&Time>=850){state.ReadStories[Story.Id]=Math.Min(100000,state.ReadStories.GetValueOrDefault(Story.Id)+1);Revision++;Phase="finished";Time=0;}
        }
    }
    public static string Result(string player,string pet)=>player==pet?"draw":(player,pet) is ("rock","scissors") or ("scissors","paper") or ("paper","rock")?"win":"lose";
    public static string HandName(string? hand)=>hand switch {"rock"=>"石头","scissors"=>"剪刀","paper"=>"布",_=>""};
    public string Pose=>Key switch {
        "highfive"=>Phase=="approach"?(Time<340?"high-ready":"high-contact"):Phase=="contact"?(Time<280?"high-contact":Time<660?"high-recoil":"high-prep"):(Time<750?"high-prep":"high-ready"),
        "rps"=>Phase=="countdown"?Time%600<130?"fist-up":Time%600<250?"fist-mid":Time%600<380?"fist-down":Time%600<480?"fist-mid":"fist-up":Phase=="shoot"?(Time<200?"reveal-rock":"reveal-"+Pet):Phase=="revealed"?"reveal-"+Pet:"fist-up",
        "gift"=>Phase is "receive" or "delivering"?"receive":Phase=="holding"||Phase=="opening"&&Time<350?"gift":Phase=="opened"&&Time>=1750?"happy":"gift-empty",
        "read"=>Phase is "finished" or "finishing"?"read-finish":Phase=="turning"||Phase=="paused"&&pausedPhase=="turning"?"page":"read",
        _=>"neutral"};
    public string Speech=>Key switch {
        "highfive"=>Phase=="approach"?"来了！":Phase=="contact"?"啪！接住你的击掌。":Time<750?"我也把手举起来。":"把你的手递过来吧！",
        "rps"=>Phase=="choose"?"我想好了，一起猜拳！":Phase=="countdown"?$"{3-Math.Min(2,(int)(Time/600))}…":Phase=="shoot"?"一起出拳！":$"你：{HandName(Player)} · 我：{HandName(Pet)} · "+(Outcome=="win"?"你赢啦！":Outcome=="lose"?"这次我赢啦。":"平局，好有默契！"),
        "gift"=>Phase is "receive" or "delivering"?"送给我的？谢谢你。":Phase=="holding"?"我来拆开看看。":Phase=="opening"?"看看这次的小惊喜…":$"{Collectibles.Get(Prize!).Name}！"+(NewPrize?"新收藏！":"又多一份小心意。"),
        "read"=>Phase=="ready"?"选一篇小故事，一起读。":Phase=="finished"?"读完啦，故事已放进收藏。":Phase=="paused"?"歇一会儿，故事停在这里。":Phase=="finishing"?"故事读完了。":Story.Sentences[Sentence],
        _=>""};
}

using System.Text.Json;
using DesktopPet.Core;

int passed = 0;
void Test(string name, Action run)
{
    try { run(); Console.WriteLine("PASS " + name); passed++; }
    catch (Exception ex) { Console.Error.WriteLine("FAIL " + name + ": " + ex); Environment.ExitCode = 1; }
}
void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}; got {actual}"); }
void Reject(Action run) { try { run(); } catch (InvalidDataException) { return; } throw new Exception("Invalid input was accepted"); }
MembershipTests.Run(Test);
FiveTests.Run(Test);
CompanionTests.Run(Test);
AgendaTests.Run(Test);

Test("care tolerates a delayed frame and sleep belongs only to lullaby", () =>
{
    var lullaby = CareRoutine.Find("lullaby")!;
    Equal("think", lullaby.At(-200).Motion);
    Equal("curl", lullaby.At(800).Motion);
    Equal("sleep", lullaby.At(4100).Motion);
    Equal(1800d, lullaby.At(4100).Elapsed);
    Equal(3000d, lullaby.At(50000).Elapsed);
    Equal(true, lullaby.FallsAsleep);
    foreach (var routine in CareRoutine.All.Where(r => !r.FallsAsleep))
        Equal(false, routine.Steps.Any(step => step.Motion == "sleep"));
    Equal<CareRoutine?>(null, CareRoutine.Find("unknown"));
});

Test("walk gait follows actual travel and stands still through a boundary turn", () =>
{
    var clip = new Sprite("walk.png", 3, 4, Enumerable.Repeat(80,12).ToArray());
    var gait = new WalkPlayback();
    var first = gait.Advance(100, 1, .1, 240, clip, 20, 200);
    Equal(true, first.Center > 100); Equal(1, first.Direction);
    Equal(true, Math.Abs(gait.Milliseconds - 100) < .001);
    double timeToEdge = 1 / DesktopWalk.Speed(240, clip);
    var edge = gait.Advance(199, 1, timeToEdge, 240, clip, 20, 200);
    double arrivedPhase = gait.Milliseconds;
    Equal(200d, edge.Center); Equal(1, edge.Direction); Equal(true, arrivedPhase > 100);
    var hold = gait.Advance(edge.Center, edge.Direction, .08, 240, clip, 20, 200);
    Equal(200d, hold.Center); Equal(1, hold.Direction); Equal(arrivedPhase, gait.Milliseconds);
    var turn = gait.Advance(hold.Center, hold.Direction, .08, 240, clip, 20, 200);
    Equal(200d, turn.Center); Equal(-1, turn.Direction);
    var back = gait.Advance(turn.Center, turn.Direction, .04, 240, clip, 20, 200);
    Equal(true, back.Center < 200); Equal(-1d, DesktopWalk.ScaleX(back.Direction,"right"));
    Equal(true, Math.Abs(gait.Milliseconds - arrivedPhase - 40) < .001);
    gait.Reset(); Equal(0d,gait.Milliseconds); Equal(0d,gait.TurnRemaining);
});

Test("walk distance and stride do not depend on refresh rate or delayed frames", () =>
{
    var clip = new Sprite("walk.png", 3, 4, Enumerable.Repeat(80,12).ToArray());
    foreach (int hz in new[] { 30, 60, 75, 120, 144 })
    foreach (int direction in new[] { -1, 1 })
    {
        var gait = new WalkPlayback(); double center = 500;
        for (int i=0;i<hz*3;i++) center = gait.Advance(center,direction,1d/hz,200,clip,0,1000).Center;
        Equal(true, Math.Abs(center-(500+direction*DesktopWalk.Speed(200,clip)*3)) < 1e-7);
        Equal(true, Math.Abs(gait.Milliseconds-3000) < 1e-7);
    }
    var delayed = new WalkPlayback(); double x = 500;
    foreach(double dt in new[] { .008, .017, .09, .12, .015, .25, .01, .24, .25 })
        x = delayed.Advance(x,1,dt,200,clip,0,1000).Center;
    Equal(true,Math.Abs(x-500-DesktopWalk.Speed(200,clip))<1e-7);
    Equal(true,Math.Abs(delayed.Milliseconds-1000)<1e-7);
});

Test("measured walk stride controls full-cycle distance while legacy art keeps its speed", () =>
{
    var legacy = new Sprite("walk.png", 3, 4, Enumerable.Repeat(100,12).ToArray());
    var measured = legacy with { WalkStride=.48 };
    Equal(true,Math.Abs(DesktopWalk.Speed(200,legacy)-200*.22/1.2)<1e-9);
    Equal(80d,DesktopWalk.Speed(200,measured));
    var gait=new WalkPlayback();
    var step=gait.Advance(0,1,1.2,200,measured,-10000,10000);
    Equal(96d,step.Center);Equal(1200d,gait.Milliseconds);
});
Test("measured walk strides keep distance and pose time across refresh rates and delayed frames", () =>
{
    foreach(double stride in new[]{.1,.48,1.2})
    {
        var clip=new Sprite("walk.png",3,4,Enumerable.Repeat(100,12).ToArray(),WalkStride:stride);
        foreach(int hz in new[]{30,60,144})foreach(int direction in new[]{-1,1})
        {
            var gait=new WalkPlayback();double center=0;
            for(int i=0;i<hz;i++)center=gait.Advance(center,direction,1d/hz,200,clip,-10000,10000).Center;
            Equal(true,Math.Abs(center-direction*DesktopWalk.Speed(200,clip))<1e-7);
            Equal(true,Math.Abs(gait.Milliseconds-1000)<1e-7);
        }
        var delayed=new WalkPlayback();double x=0;
        foreach(double dt in new[]{.008,.017,.09,.12,.015,.25,.01,.24,.25})x=delayed.Advance(x,1,dt,200,clip,-10000,10000).Center;
        Equal(true,Math.Abs(x-DesktopWalk.Speed(200,clip))<1e-7);
        Equal(true,Math.Abs(delayed.Milliseconds-1000)<1e-7);
    }
});

Test("edge turns consume leftover time and preserve the arriving stride", () =>
{
    var clip = new Sprite("walk.png",3,4,Enumerable.Repeat(80,12).ToArray());
    (double Center,int Direction,double Phase) Travel(double dt)
    {
        var gait = new WalkPlayback(); double center=194; int direction=1;
        for(int i=0;i<(int)Math.Round(4/dt);i++)
            (center,direction)=gait.Advance(center,direction,dt,200,clip,20,200);
        return (center,direction,gait.Milliseconds);
    }
    var fast=Travel(.01); var slow=Travel(.2);
    Equal(fast.Direction,slow.Direction);
    Equal(true,Math.Abs(fast.Center-slow.Center)<1e-7);
    Equal(true,Math.Abs(fast.Phase-slow.Phase)<1e-7);
});

Test("daily check-in is idempotent and a missed day preserves affection", () =>
{
    var state = new PetState(); var day = new DateOnly(2026, 9, 27);
    for (int i = 0; i < 7; i++) Equal(true, state.CheckIn(day.AddDays(i)));
    Equal(false, state.CheckIn(day)); Equal(7, state.CheckIns.Count); Equal(2, state.BondLevel);
    Equal(7, state.Streak(day.AddDays(6))); Equal(0, state.Streak(day.AddDays(9))); Equal(2, state.BondLevel);
    Equal(true, state.CheckIn(day.AddDays(9))); Equal(1, state.Streak(day.AddDays(9)));
});
Test("leap years, reunions and anniversaries use local calendar dates", () =>
{
    var state = new PetState { AdoptedAt = "2024-02-29", LastSeen = "2026-09-24" };
    Equal(3, state.DaysAway(new DateOnly(2026, 9, 27))); Equal(0, state.DaysAway(new DateOnly(2026, 9, 23)));
    Equal("相伴 4 周年", state.Anniversary(new DateOnly(2028, 2, 29)));
    Equal("相伴第 100 天", state.Anniversary(new DateOnly(2024, 6, 8)));
});
Test("missing outfit animations stay dressed through every interaction", () =>
{
    var pet = new Character { Dizzy = new("dizzy.webp", 1, 1), Motions = new() { ["walk"] = new("walk.webp"), ["eat"] = new("eat.webp"), ["headpat"] = new("pat.webp") },
        Outfits = new() { ["wedding"] = new() { Idle = new("dress.webp", 1, 1), Motions = new() { ["walk"] = new("dress-walk.webp") } } } };
    Equal("dress.webp", pet.Resolve("wedding", "idle", 0).Sprite.File);
    Equal("dress-walk.webp", pet.Resolve("wedding", "walk", 0).Sprite.File);
    foreach (string action in new[] { "meal", "eat", "chat", "pounce", "headpat", "poke", "tickle", "kick", "jump", "sleep", "dizzy", "faint", "sad", "happy", "pickup", "shaken", "shaken-strong", "farewell", "ball-hit", "ball-miss", "bonk", "peek", "curl", "think" })
        foreach (bool reduced in new[] { false, true })
            Equal((new Sprite("dress.webp", 1, 1), 0), pet.Resolve("wedding", action, 700, reduced));
    Equal(0, pet.Resolve("wedding", "walk", 700, true).Frame);
    Equal("eat.webp", pet.Resolve("original", "meal", 700).Sprite.File);
    Equal("dizzy.webp", pet.Resolve("original", "dizzy", 700).Sprite.File);
    Equal("walk.webp", pet.Resolve("removed-outfit", "walk", 700).Sprite.File);
});
Test("semantic fallback uses the current outfit before its pose atlas", () =>
{
    var pet = new Character { Motions = new() { ["poke"] = new("original-poke.png"), ["meal"] = new("original-meal.png") },
        Outfits = new() { ["swim"] = new() { Idle = new("swim.png"), Motions = new() { ["headpat"] = new("swim-pat.png"), ["eat"] = new("swim-eat.png"), ["jump"] = new("swim-jump.png"), ["chat"] = new("swim-chat.png") } } } };
    foreach (var (action, file) in new[] { ("poke", "swim-pat.png"), ("meal", "swim-eat.png"), ("tickle", "swim-jump.png"), ("kick", "swim-jump.png"), ("pounce", "swim-chat.png") })
        Equal(file, pet.Resolve("swim", action, 0).Sprite.File);
    foreach (var (action, frame) in new[] { ("idle", 0), ("happy", 1), ("sleep", 3), ("dizzy", 4), ("faint", 4), ("sad", 5) })
        Equal((new Sprite("swim.png"), frame), pet.Resolve("swim", action, 700));
    pet.Outfits["swim"].Motions["poke"] = new("swim-poke.png");
    Equal("swim-poke.png", pet.Resolve("swim", "poke", 0).Sprite.File);
});
Test("portrait gesture fallback never substitutes jumping or talking for an unrelated action", () =>
{
    foreach (string category in new[] { "3d", "adult" })
    {
        var pet = new Character { Category = category, Atlas = new("portrait.png", 1, 1),
            Motions = new() { ["jump"] = new("jump.png"), ["chat"] = new("chat.png") },
            Outfits = new() { ["swim"] = new() { Idle = new("swim.png", 1, 1), Motions = new() { ["jump"] = new("swim-jump.png"), ["chat"] = new("swim-chat.png") } } } };
        foreach (string outfit in new[] { "original", "swim" })
            foreach (string action in new[] { "tickle", "kick", "pounce" })
            {
                Equal<Sprite?>(null, pet.MotionFor(outfit, action));
                Equal(outfit == "original" ? "portrait.png" : "swim.png", pet.Resolve(outfit, action, 600).Sprite.File);
            }
        pet.Outfits["swim"].Motions["tickle"] = new("drawn-tickle.png");
        Equal("drawn-tickle.png", pet.Resolve("swim", "tickle", 600).Sprite.File);
    }
});
Test("walking capability never borrows original clothes or mistakes poses for an animation", () =>
{
    var pet = new Character { Motions = new() { ["walk"] = new("original-walk.png") }, Outfits = new() {
        ["swim"] = new() { Idle = new("six-poses.png") },
        ["wedding"] = new() { Idle = new("dress.png", 1, 1), Motions = new() { ["walk"] = new("dress-walk.png", 1, 1) } } } };
    Equal(true, pet.CanWalk("original")); Equal(false, pet.CanWalk("swim")); Equal(false, pet.CanWalk("wedding"));
    pet.Outfits["swim"].Motions["walk"] = new("swim-walk.png"); Equal(true, pet.CanWalk("swim"));
    pet.Outfits["swim"].Idle = null;
    Equal("swim-walk.png", pet.Resolve("swim", "idle", 900).Sprite.File);
    Equal(0, pet.Resolve("swim", "idle", 900).Frame);
    pet.Outfits["swim"].Motions["walk"] = new("shared-sheet.png", 3, 3, Frames: [6, 6]);
    Equal(false, pet.CanWalk("swim"));
    Equal(6, pet.Resolve("swim", "idle", 900).Frame);
});
Test("portrait wardrobes stay selected through greetings and touch interactions", () =>
{
    var pet = new Character { Id = "claude-3d", Family = "claude", Atlas = new("portrait.png", 1, 1),
        Outfits = new() { ["swim"] = new() { Idle = new("swim.png", 1, 1) }, ["wedding"] = new() { Idle = new("wedding.png", 1, 1) } } };
    Equal("claude", pet.FamilyId);
    foreach (string outfit in new[] { "swim", "wedding" })
        foreach (string action in new[] { "idle", "chat", "headpat", "sleep", "pickup", "happy", "farewell" })
            Equal(outfit + ".png", pet.Resolve(outfit, action, 800).Sprite.File);
    Equal("portrait.png", pet.Resolve("original", "headpat", 800).Sprite.File);
    Equal("legacy", new Character { Id = "legacy" }.FamilyId);
});
Test("six-frame animation wraps at exact clip duration", () =>
{
    var clip = new Sprite("a.webp", 3, 2, [100, 200, 300, 400, 500, 600]);
    Equal(0, Motion.Frame(clip, 0)); Equal(1, Motion.Frame(clip, 100)); Equal(5, Motion.Frame(clip, 2099)); Equal(0, Motion.Frame(clip, 2100));
    Equal(0, Motion.Frame(new Sprite("single.png", 1, 1), 555));
});
Test("drawn contact clips select only their ordered cells and hold their final pose", () =>
{
    var meal = new Sprite("contact.png", 3, 3, [100, 200, 300, 100], Frames: [3, 4, 5, 3], Loop: false, BakedProps: true);
    Equal(3, Motion.Frame(meal, 0)); Equal(4, Motion.Frame(meal, 100)); Equal(5, Motion.Frame(meal, 300));
    Equal(3, Motion.Frame(meal, 600)); Equal(3, Motion.Frame(meal, 9999));
    var catchBall = meal with { Frames = [6, 7, 8], FrameMs = [100, 180, 600], BakedProps = false };
    Equal(6, Motion.Frame(catchBall, 0)); Equal(7, Motion.Frame(catchBall, 100)); Equal(8, Motion.Frame(catchBall, 280)); Equal(8, Motion.Frame(catchBall, 5000));
    var character = new Character { Motions = new() { ["meal"] = meal } };
    Equal(3, character.Resolve("original", "meal", 800, true).Frame);
});
Test("walking reverses inward at either edge without overshooting", () =>
{
    Equal((90d, -1), DesktopWalk.Step(50, 1, 40, 10, 90));
    Equal((10d, 1), DesktopWalk.Step(11, -1, 200, 10, 90));
    Equal((12d, 1), DesktopWalk.Step(10, 1, 2, 10, 90));
    Equal((30d, -1), DesktopWalk.Step(40, -1, 10, 10, 90));
    Equal((50d, 1), DesktopWalk.Step(50, 1, 10, 50, 50));
});
Test("the displayed facing follows travel and each source clip orientation", () =>
{
    Equal(-1d, DesktopWalk.ScaleX(-1, "right")); Equal(1d, DesktopWalk.ScaleX(1, "right"));
    Equal(1d, DesktopWalk.ScaleX(-1, "left")); Equal(-1d, DesktopWalk.ScaleX(1, "left"));
    Equal(44d, DesktopWalk.Speed(200, new Sprite("walk.png", 2, 1, [500, 500])));
});
Test("hide-and-seek reaches the nearest edge and waits indefinitely for discovery", () =>
{
    foreach (double origin in new[] { -1920d, 0, 2560 })
        foreach (int side in new[] { -1, 1 })
            foreach (double size in new[] { 120d, 200, 280 })
            {
                double center = origin + (side < 0 ? 400 : 1500);
                var journey = new EdgeHide(center, origin, origin + 1920, size, 80);
                Equal(side, journey.Side);
                var halfway = journey.At(journey.ApproachSeconds / 2);
                Equal(HidePhase.Approach, halfway.Phase); Equal(true, halfway.Walking); Equal(side, halfway.Direction);
                Equal(HidePhase.Hide, journey.At(journey.ApproachSeconds).Phase);
                var hidden = journey.At(journey.ApproachSeconds + 1);
                Equal(HidePhase.Hidden, hidden.Phase); Equal(true, (hidden.Center - journey.Edge) * side > size / 2);
                var peek = journey.At(journey.ApproachSeconds + 2.2);
                Equal(HidePhase.Peek, peek.Phase); Equal(-side, peek.Direction); Equal(true, Math.Abs(peek.Center - journey.Edge) < size / 2);
                var end = journey.At(journey.Duration + 1);
                Equal(HidePhase.Peek, end.Phase); Equal(peek.Center, end.Center); Equal(-side, end.Direction);
                Equal(end, journey.At(journey.Duration + 3600));
                for (double time = 0; time < journey.Duration; time += .025)
                    Equal(true, Math.Abs(journey.At(time + .025).Center - journey.At(time).Center) < size * .06);
            }
    Equal(1, new EdgeHide(100, 0, 1920, 200, 80, 1).Side);
    var atEdge = new EdgeHide(92, 0, 1920, 200, 80, -1); Equal(0d, atEdge.ApproachSeconds); Equal(HidePhase.Hide, atEdge.At(0).Phase);
});
Test("radial menus fit corners and negative monitors without overlapping buttons", () =>
{
    foreach (double origin in new[] { -1920d, 0, 2560 })
    foreach (double size in new[] { 120d, 200, 280 })
    foreach (int count in new[] { 6, 7 })
    foreach (double x in new[] { origin + size * .46, origin + 960, origin + 1920 - size * .46 })
    foreach (double y in new[] { size / 2 + 16, 500, 1080 - size / 2 })
    {
        var menu = RadialMenu.Place(count, new MenuPoint(x, y), size, new MenuBounds(origin, 0, origin + 1920, 1080));
        Equal(count, menu.Length);
        foreach (var p in menu) Equal(true, p.X >= origin + 22 && p.X <= origin + 1898 && p.Y >= 22 && p.Y <= 1058);
        for (int i = 0; i < count; i++) for (int j = i + 1; j < count; j++)
            Equal(true, Math.Sqrt(Math.Pow(menu[i].X - menu[j].X, 2) + Math.Pow(menu[i].Y - menu[j].Y, 2)) > 48);
    }
});
Test("club stages map nonsequential and repeated cells in a twenty-cell sheet", () =>
{
    foreach(var (key,offset,order) in new (string,int,int[])[]{
        ("stars",0,[0,1,2,3,4,5,6,7]),
        ("bubbles",8,[8,9,10,11,12,11,10,15]),
        ("stretch",16,[13,14,16,16,17,17,18,19])})
    {
        var clip=new Sprite("club.png",4,5,Frames:order);
        var legacy=new Sprite("legacy.png",4,6,Frames:Enumerable.Range(offset,8).ToArray());
        for(int t=0;t<=ClubMotion.Duration(key);t+=17)
        {
            var clock=ClubMotion.Sample(key,t);var mapped=ClubMotion.Sample(key,t,clip);
            Equal(order[clock.A-offset],mapped.A);Equal(order[clock.B-offset],mapped.B);
            Equal(true,mapped.A<20 && mapped.B<20);
            Equal(clock.Amount,mapped.Amount);Equal(clock.Count,mapped.Count);Equal(clock.Blowing,mapped.Blowing);
            Equal(clock,ClubMotion.Sample(key,t,legacy));
        }
    }
    var stretch=new Sprite("club.png",4,5,Frames:[13,14,16,16,17,17,18,19]);
    Equal(16,ClubMotion.Sample("stretch",1250,stretch).A);
    Equal(16,ClubMotion.Sample("stretch",1250,stretch).B);
    Reject(()=>ClubMotion.Sample("stretch",5100,new Sprite("club.png",4,5)));
    Reject(()=>ClubMotion.Sample("bubbles",1500,new Sprite("club.png",4,5,Frames:[8,9,10])));
});

Test("club poses count five before lowering and bubbles only blow after lifting", () =>
{
    foreach(double time in new[]{0d,400,800,1100}) Equal(false,ClubMotion.Sample("bubbles",time).Blowing);
    Equal(true,ClubMotion.Sample("bubbles",1400).Blowing);
    Equal(false,ClubMotion.Sample("bubbles",2600).Blowing);
    Equal(5,ClubMotion.Sample("stars",7050).Count);
    Equal(5,ClubMotion.Sample("stars",7050).A);
    Equal(0d,ClubMotion.Sample("stars",7050).Amount);
    foreach(string key in new[]{"stars","bubbles","stretch"})for(int t=0;t<ClubMotion.Duration(key);t+=17)
    { var pose=ClubMotion.Sample(key,t);Equal(true,pose.A>=0&&pose.B<24&&pose.Amount>=0&&pose.Amount<=1); }
});
Test("hidden pet menus fit the visible strip without button overlap", () =>
{
    foreach(var bounds in new[]{new MenuBounds(0,0,145,468),new MenuBounds(415,0,560,468)})
    foreach(int count in new[]{7,9,12})
    {
        var points=RadialMenu.Place(count,new MenuPoint(280,345),250,bounds);
        foreach(var p in points)Equal(true,p.X>=bounds.Left+22&&p.X<=bounds.Right-22&&p.Y>=22&&p.Y<=446);
        for(int i=0;i<count;i++)for(int j=i+1;j<count;j++)Equal(true,Math.Sqrt(Math.Pow(points[i].X-points[j].X,2)+Math.Pow(points[i].Y-points[j].Y,2))>48);
    }
});
Test("all portrait gestures move independently, remain finite and ease back in both styles", () =>
{
    foreach (string category in new[] { "3d", "adult" })
    foreach (string family in new[] { "whale", "gpt", "claude", "gemini", "grok", "qwen", "zhipu", "kimi" })
    {
        Equal(true, PortraitRig.Supports(category, family));
        var rig = new PortraitRig(family, "wedding", category: category);
        foreach (string action in PortraitMotion.Actions)
        {
            double duration = PortraitMotion.Duration(action);
            var start = rig.MotionPose(action, 0, duration); Equal(true, start.Bones.SequenceEqual(rig.Rest));
            var end = rig.MotionPose(action, duration, duration, true); Equal(true, end.Bones.SequenceEqual(rig.Rest));
            if (action != "sleep") Equal(true, rig.MotionPose(action, duration, duration).Bones.SequenceEqual(rig.Rest));
            // Bonk has one brief impact, synchronized with the illustrated hammer.
            var active = rig.MotionPose(action, action == "bonk" ? 490 : 900, duration);
            Equal(true, active.Bones.Zip(rig.Rest).Any(p => (p.First.A - p.Second.A).Length + (p.First.B - p.Second.B).Length > .0001));
            Equal(true, rig.Skin(active).All(p => double.IsFinite(p.X) && double.IsFinite(p.Y)));
            for (double t = 0; t < duration; t += 29)
            {
                var a = rig.MotionPose(action, t, duration); var b = rig.MotionPose(action, t + 16, duration);
                if (!a.Bones.Zip(b.Bones).All(p => (p.First.B - p.Second.B).Length < .04)) throw new Exception($"Joint discontinuity: {category}/{family}/{action}/{t}");
            }
        }
    }
    Equal("poke", PortraitMotion.TouchRegion("adult", .16)); Equal("poke", PortraitMotion.TouchRegion("3d", .19));
    Equal("headpat", PortraitMotion.TouchRegion("adult", .07)); Equal("tickle", PortraitMotion.TouchRegion("3d", .43));
    Equal("headpat", PortraitMotion.TouchRegion("chibi", .4));
});
Test("mixed collectibles draw ten sports and ten everyday items without repeats per round", () =>
{
    Equal(20, Collectibles.All.Count); Equal(20, Collectibles.All.Select(x => x.Id).Distinct().Count()); Equal(10, Collectibles.Sports.Count);
    var draws = new ItemDrawBag(Collectibles.All, new Random(12)); string last = "";
    for (int round = 0; round < 20; round++)
    {
        var prizes = Enumerable.Range(0, 20).Select(_ => draws.Draw()).ToArray();
        Equal(20, prizes.Select(x => x.Id).Distinct().Count()); Equal(10, prizes.Count(x => x.Kind == ItemKind.Sport));
        Equal(false, last == prizes[0].Id); last = prizes[^1].Id;
    }
    Equal("shell", Collectibles.FromSavedName("一枚贝壳")!.Id); Equal(true, Collectibles.Food.All(x => x.Edible));
    Equal(true, Collectibles.All.Any(x => x.Name == "米饭")); Equal(true, Collectibles.All.Any(x => x.Name == "面包"));
});
Test("all ten toys bounce inside the play area with distinct gravity and rebound", () =>
{
    foreach (var toy in Collectibles.Sports)
    {
        var flight = new ToyFlight(100, 100, -800, 400, 0);
        for (int tick = 0; tick < 300; tick++)
        {
            flight = ToyPhysics.Step(flight, toy, .033, 25, 520, 468);
            Equal(true, flight.X >= 25 && flight.X <= 520 - toy.Diameter && flight.Y <= 468 - toy.Diameter);
            Equal(true, double.IsFinite(flight.Vy) && double.IsFinite(flight.Angle));
        }
    }
    var floor = new ToyFlight(50, 467, 0, 300, 0);
    var basketball = ToyPhysics.Step(floor, Collectibles.Get("basketball"), .016, 0, 560, 468);
    var bowling = ToyPhysics.Step(floor, Collectibles.Get("bowling"), .016, 0, 560, 468);
    Equal(true, Math.Abs(basketball.Vy) > Math.Abs(bowling.Vy));
});
Test("fast throws use swept contact and misses remain misses", () =>
{
    Equal(true, BallPhysics.Hit(0, 100, 900, 100, 450, 100, 30));
    Equal(false, BallPhysics.Hit(0, 100, 900, 100, 450, 200, 30));
    Equal(true, BallPhysics.Hit(5, 5, 5, 5, 5, 5, 1));
});
Test("shake ignores pointer jitter, slow corrections and single-axis double counting", () =>
{
    var shake = new ShakeTracker(); shake.Start(0, 0, 0);
    for (int i = 1; i <= 12; i++) shake.Move(i % 2 == 0 ? 8 : -8, i % 2 == 0 ? 9 : -9, i * 40);
    Equal(false, shake.IsDizzy(480)); Equal("pickup", shake.Motion);
    shake.Start(0, 0, 0);
    for (int i = 1; i <= 10; i++) shake.Move(i * 80, i * 30, i * 50);
    Equal(false, shake.IsDizzy(500));
    shake.Start(0, 0, 0);
    for (int i = 1; i <= 10; i++) shake.Move(i % 2 == 0 ? 0 : 50, i * 450);
    Equal(false, shake.IsDizzy(4500));
    shake.Start(0, 0, 0);
    for (int i = 1; i <= 3; i++) shake.Move(i % 2 == 0 ? 0 : 100, i % 2 == 0 ? 0 : 100, i * 100);
    Equal(false, shake.IsDizzy(300)); // two diagonal reversals are still only two, not four.
    shake.Move(0, 0, 400); shake.Move(100, 100, 500); Equal(true, shake.IsDizzy(500));
    shake.Start(0, 0, 0);
    for (int i = 1; i <= 5; i++) shake.Move(0, i % 2 == 0 ? 0 : 100, i * 100);
    Equal(true, shake.IsDizzy(500)); // vertical shaking works as well as horizontal.
    shake.Start(0, 0, 0); shake.Move(160, 0, 100); shake.Move(156, 0, 105); shake.Move(159, 0, 110); shake.Move(0, 0, 200);
    Equal("shaken-strong", shake.Motion); // noise at an apex must not erase a real stroke.
    shake.Move(160, 0, 300); shake.Move(0, 0, 400); shake.Move(160, 0, 1200);
    Equal(false, shake.IsDizzy(1200)); // holding still between strokes starts a fresh gesture.
    shake.Start(0, 0, 1300); Equal(false, shake.IsDizzy(1300));
});
Test("shake needs four recent reversals, then expires", () =>
{
    var shake = new ShakeTracker(); shake.Start(0, 0);
    shake.Move(160, 100); shake.Move(0, 200); Equal("shaken-strong", shake.Motion);
    Equal(false, shake.IsDizzy(200)); shake.Move(160, 300); shake.Move(0, 400); shake.Move(160, 500);
    Equal(true, shake.IsDizzy(500)); Equal(false, shake.IsDizzy(2100));
    shake.Start(0, 0); shake.Move(60, 200); shake.Move(0, 400); Equal("shaken", shake.Motion);
});
ChatVerification.Run(Test);
Test("style merge preserves the worn outfit and progress and is safe to repeat", () =>
{
    var aliases = new Dictionary<string,string> { ["gpt-3d"]="gpt-adult", ["deepseek-3d"]="deepseek-adult", ["claude-3d"]="claude-adult" };
    var state = new PetState { Character="gpt-3d", Outfits=new() { ["gpt-3d"]="wedding", ["gpt-adult"]="swim", ["deepseek-3d"]="swim", ["claude-3d"]="swim", ["claude-adult"]="wedding", ["custom-3d"]="swim" }, Treasures=["贝壳"], Left=372, Top=215 };
    state.CheckIn(new DateOnly(2026,9,29)); string adopted=state.AdoptedAt;
    state.MigrateCharacters(aliases);
    Equal("gpt-adult",state.Character); Equal("wedding",state.Outfit);
    Equal("swim",state.Outfits["deepseek-adult"]); Equal("wedding",state.Outfits["claude-adult"]);
    Equal("swim",state.Outfits["custom-3d"]); Equal(false,state.Outfits.ContainsKey("gpt-3d"));
    Equal(1,state.CheckIns.Count); Equal("贝壳",state.Treasures.Single()); Equal(adopted,state.AdoptedAt);
    Equal<double?>(372,state.Left); Equal<double?>(215,state.Top);
    string migrated=JsonSerializer.Serialize(state,Json.Options);
    state.MigrateCharacters(aliases); Equal(migrated,JsonSerializer.Serialize(state,Json.Options));
    var missingOutfit=new PetState { Character="gpt-3d",Outfits=new() { ["gpt-adult"]="wedding" } };
    missingOutfit.MigrateCharacters(aliases); Equal("gpt-adult",missingOutfit.Character); Equal("original",missingOutfit.Outfit);
});
Test("jump has grounded preparation and landing with one continuous airborne arc", () =>
{
    foreach (double size in new[] { 120d, 200, 280 })
    {
        foreach (double time in new[] { 0d, 120, 240, 880, 1000, 1320 }) Equal(0d, PortraitChoreography.JumpHeight(time, size));
        Equal(true, Math.Abs(PortraitChoreography.JumpHeight(560, size) - size * .3) < .01);
        for (int t = 0; t <= 1320; t += 16)
        {
            Equal(0d, PortraitChoreography.JumpHeight(t, size, true));
            Equal(true, Math.Abs(PortraitChoreography.JumpHeight(t + 16, size) - PortraitChoreography.JumpHeight(t, size)) < 7);
        }
    }
});
Test("body impact sweeps fast throws while distant and overhead balls miss", () =>
{
    Equal(true, PortraitChoreography.HitsBody(0, 320, 560, 320, 280, 200, 268, 17));
    Equal(false, PortraitChoreography.HitsBody(0, 100, 560, 100, 280, 200, 268, 17));
    Equal(false, PortraitChoreography.HitsBody(35, 100, 35, 468, 280, 200, 268, 17));
});
string root = Path.Combine(Path.GetTempPath(), "DesktopPet-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    Test("atomic saves preserve character, outfit, check-ins and treasure", () =>
    {
        var store = new StateStore(root); var state = new PetState { Character = "gpt", Outfits = new() { ["gpt"] = "wedding" }, Treasures = ["贝壳"] };
        state.CheckIn(new DateOnly(2026, 9, 27)); store.Save(state); var loaded = store.Load();
        Equal("gpt", loaded.Character); Equal("wedding", loaded.Outfit); Equal(1, loaded.CheckIns.Count); Equal("贝壳", loaded.Treasures[0]);
        Equal(false, File.Exists(Path.Combine(root, "state.json.tmp")));
    });
    Test("corrupt saves are retained instead of silently discarded", () =>
    {
        File.WriteAllText(Path.Combine(root, "state.json"), "{broken"); var store = new StateStore(root); var state = store.Load();
        Equal("whale", state.Character); Equal(true, store.Warning is not null);
        Equal("{broken", File.ReadAllText(Directory.GetFiles(root, "state-unreadable-*.json").Single()));
    });
    Test("import rejects path traversal, executable assets and invalid timing", () =>
    {
        Reject(() => Character.SafeFile(root, "../secret.png")); Reject(() => Character.SafeFile(root, "C:\\secret.png")); Reject(() => Character.SafeFile(root, "script.exe"));
        File.WriteAllBytes(Path.Combine(root, "atlas.png"), [0]);
        var c = new Character { Id = "test", Name = "test" };
        void Write() => File.WriteAllText(Path.Combine(root, "pet.json"), JsonSerializer.Serialize(c, Json.Options));
        Write(); Equal("test", Character.Load(root).Id);
        c.Atlas = new Sprite("atlas.png", 3, 2, [0, 0]); Write(); Reject(() => Character.Load(root));
        c.Atlas = new Sprite("atlas.png", 0, 0); Write(); Reject(() => Character.Load(root));
        c.Atlas = new Sprite("atlas.png", 1, 1, Facing: "up"); Write(); Reject(() => Character.Load(root));
        c.Atlas = new Sprite("atlas.png", 1, 1, HeightRatios: [.49]); Write(); Equal(.49, Character.Load(root).Atlas.HeightRatios![0]);
        c.Atlas = new Sprite("atlas.png", 1, 1, HeightRatios: []); Write(); Reject(() => Character.Load(root));
        c.Atlas = new Sprite("atlas.png", 1, 1, HeightRatios: [0]); Write(); Reject(() => Character.Load(root));
        c.Atlas = new Sprite("atlas.png", 1, 1, FrameScaleFactors: [1.5]); Write(); Equal(1.5, Character.Load(root).Atlas.FrameScaleFactors![0]);
        foreach (double[] factors in new[] { Array.Empty<double>(), new[] {0d}, new[] {5d}, new[] {1d,1d} })
        { c.Atlas = c.Atlas with { FrameScaleFactors=factors }; Write(); Reject(() => Character.Load(root)); }
        c.Atlas = new Sprite("atlas.png", 4, 6, SeparationAlpha: 200, ReferenceHeightPixels: 280); Write();
        Equal(200, Character.Load(root).Atlas.SeparationAlpha); Equal(280d, Character.Load(root).Atlas.ReferenceHeightPixels);
        foreach(int alpha in new[]{248,252,255})
        { c.Atlas = c.Atlas with { SeparationAlpha=alpha }; Write(); Equal(alpha, Character.Load(root).Atlas.SeparationAlpha); }
        c.Atlas = c.Atlas with { SeparationAlpha=256 }; Write(); Reject(() => Character.Load(root));
        c.Atlas = c.Atlas with { SeparationAlpha=48, ReferenceHeightPixels=-1 }; Write(); Reject(() => Character.Load(root));
        foreach(double stride in new[]{0d,.1,.48,1.2})
        { c.Atlas=new Sprite("atlas.png",1,1,WalkStride:stride);Write();Equal(stride,Character.Load(root).Atlas.WalkStride); }
        foreach(double stride in new[]{-.1,.09,1.21})
        { c.Atlas=new Sprite("atlas.png",1,1,WalkStride:stride);Write();Reject(()=>Character.Load(root)); }
        c.Atlas = new Sprite("atlas.png", 1, 1); c.Category = "unknown"; Write(); Reject(() => Character.Load(root));
        foreach (string category in new[] { "3d", "adult", CharacterStyles.Realistic })
        { c.Category = category; Write(); Equal(CharacterStyles.Realistic, Character.Load(root).Category); }
        c.Outfits["empty"] = new Outfit { Name = "empty" }; Write(); Reject(() => Character.Load(root)); c.Outfits.Clear();
        c.Atlas = new Sprite("missing.png"); Write(); Reject(() => Character.Load(root));
    });
    Test("saved invalid values are bounded without accepting invalid dates", () =>
    {
        var state = new PetState { Size = double.NaN, Opacity = 50, Left = double.PositiveInfinity }; state.Validate();
        Equal(200d, state.Size); Equal(1d, state.Opacity); Equal<double?>(null, state.Left);
        state.CheckIns.Add("not-a-date"); Reject(state.Validate);
    });
}
finally { Directory.Delete(root, true); }
Console.WriteLine($"{passed} checks passed.");

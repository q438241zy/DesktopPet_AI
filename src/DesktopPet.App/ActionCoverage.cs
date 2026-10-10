using DesktopPet.Core;

namespace DesktopPet.App;

internal sealed record DemoAction(string Key, string Title, string Motion, int Duration = 2600);
internal sealed record CoverageRow(string Character, string Family, string Category, string Outfit, string Action,
    string Title, string Status, string Detail, string File, int Frames);

internal static class ActionCoverage
{
    internal static readonly DemoAction[] Actions = [
        .. PetActions.Five.Select(a=>new DemoAction(a.Key,a.Title,a.Key,a.Key=="gift"?6100:4000)),
        new("idle","待机","idle"), new("listen","聆听","listen"), new("thinking","思考 1 秒","thinking",1200),
        new("chat","聊天","chat"), new("checkin","吃饭","meal",4200), new("snack","吃零食","eat",3000),
        new("headpat","摸头","headpat"), new("poke","揉脸","poke"), new("tickle","挠痒","tickle",2200),
        .. CareRoutine.All.Select(routine => new DemoAction(routine.Key,routine.Title,"idle",routine.Duration)),
        .. ClubMotion.Actions.Select(key => new DemoAction(key,ClubMotion.Title(key),ClubMotion.HasPoses(key)?key:key=="butterfly"?"walk":"idle",ClubMotion.Duration(key))),
        new("think","发呆","think",3200), new("jump","跳跃与落地","jump",1400), new("curl","抱膝","curl",3000),
        new("rest","休息","sleep",3200), new("blocks","堆积木","build",4400), new("ball","等待投球","ball-ready"),
        new("ball-hit","球命中","ball-hit",1500), new("ball-miss","球未命中","ball-miss",1800),
        new("walk","散步","walk",3000), new("peek","走到边缘躲藏","walk",3200),
        new("found","找到啦","happy",2000),
        new("pickup","提起","pickup",1800), new("place","手动放置","idle",1200), new("drop","自然落下","pickup",1800),
        new("shake","摇晃提起","pickup-dizzy",3000), new("dizzy","头晕恢复","dizzy",3000),
        new("bonk","轻敲","bonk",1900)
    ];
    internal static CoverageRow Assess(Character c, string outfit, DemoAction action)
    {
        if(FiveInteraction.Keys.Contains(action.Key))
        {
            var five=c.FiveFor(outfit);
            string fiveDetail=action.Key switch {
                "highfive"=>"递出或拖动手掌，与角色掌心接触才计数",
                "rps"=>"双方三轮摇拳，同时亮出石头、剪刀或布",
                "gift"=>"使用者送礼后连续接过、拆开；20种随机物品，打开才收藏一次",
                "read"=>"十篇原创故事随机自动文字阅读、自动翻页、可暂停；读完收藏，完整体验见CompanionV01",
                _=>"当前服装的专用互动图稿"};
            return new(c.Id,c.FamilyId,c.Category,outfit,action.Key,action.Title,five is null?"缺少动作":"专用互动",fiveDetail,
                five is null?"":string.Join(";",five.Atlases.Values.Select(x=>x.File)),five?.Poses.Count??0);
        }
        string motion = action.Motion == "pickup-dizzy" ? "pickup" : action.Motion;
        var clip = c.MotionFor(outfit, motion);
        var art = c.Resolve(outfit, motion, 0);
        int count = clip?.Frames?.Distinct().Count() ?? (clip is null ? 0 : clip.Columns * clip.Rows);
        string status, detail;
        if (ClubMotion.Actions.Contains(action.Key))
        {
            status=ClubMotion.HasPoses(action.Key,clip)?"连续姿势":action.Key is "comb" or "wipe" or "butterfly"?"程序动作":"缺少动作";
            detail=action.Key switch {
                "stars"=>$"{count} 张独立指星与抬头姿势，连续衔接八个阶段并数到 5；点星星重新数",
                "bubbles"=>$"{count} 张独立举棒、送嘴、吹气与收手姿势，连续衔接八个阶段；泡泡从棒环出生，点击可戳破",
                "stretch"=>$"{count} 张独立准备、举手、上伸与放松姿势，身体与脚底比例固定",
                "comb"=>clip?.BakedProps==true?$"{count} 张握梳、贴发、向下梳理与收手姿势，手和梳子画在同一帧":"梳头道具编排，尚未接入专用手部画稿",
                "wipe"=>clip?.BakedProps==true?$"{count} 张拿毛巾、贴脸擦拭与放下姿势，手和毛巾画在同一帧":"擦脸道具编排，尚未接入专用手部画稿",
                _=>"主动小范围双向追蝶，转向与脚步跟随位移，结束后站定；点击可改变追逐方向" };
        }
        else if (CareRoutine.Find(action.Key) is { } routine)
        {
            bool available = routine.Steps.All(step => step.Motion is "idle" or "listen" or "happy" or "sleep"
                || c.MotionFor(outfit,step.Motion) is not null || PortraitRig.Supports(c.Category,c.FamilyId) && PortraitMotion.Supports(step.Motion));
            status = available ? "组合动作" : "缺少动作";
            detail = routine.Key switch { "praise" => "聆听、开心回应与摸头鼓励", "comfort" => "安静倾听、摸头安抚，再回到放松姿势", _ => "安静下来、蜷起、呼吸入睡；保持休息直到唤醒" };
        }
        else if (action.Key == "found")
        { status = "组合动作"; detail = "左键找到后走回桌面并开心回应；右键仅开关菜单，躲藏继续"; }
        else if (action.Key == "idle")
        { status = "组合动作"; detail = "自动模式每1–2秒轮换站、坐、托腮、伸展和笑脸；固定站坐仍可选，完整体验见CompanionV01"; }
        else if (action.Key is "listen" or "place")
        { status = "静态姿势"; detail = action.Key == "place" ? "普通松手停留在用户指定的位置，距底部 24px 内吸附；拖动速度和停留时间不影响结果" : clip is null ? "使用本外观的安静待机姿势" : "本外观专用聆听姿势"; }
        else if (action.Key == "dizzy")
        { status = "程序动作"; detail = c.Category == "chibi" ? "当前服装的晕眩姿势与头顶环绕星星，3 秒内恢复" : "当前服装轻微晕眩动作与头顶环绕星星，3 秒内恢复"; }
        else if (clip is not null)
        {
            bool alias = !(outfit == "original" ? c.Motions : c.Outfits[outfit].Motions).ContainsKey(motion);
            status = alias && action.Key != "thinking" ? "近似动作" : count > 1 ? "专用逐帧" : "专用姿势";
            detail = alias && action.Key != "thinking" ? "复用本外观的相近动作，尚未有独立动作图" : $"本外观 {count} 张独立姿势";
            if (action.Key == "thinking") detail += "，配合思考提示；至少 1 秒后才回答";
            if (action.Key == "drop") detail += "；按住 Shift 松手才下落，半空重抓可中断，普通松手停住；散步或躲藏也会先回到地面";
            if (action.Key == "shake") detail += "；连续摇晃触发头顶星星，身体仍保持提起";
            if (action.Key is "walk" or "peek") detail += "；按画面刷新连续移动，保留小数位置，脚步跟随实际位移";
            if (action.Key == "walk") detail += "；不受打卡限制，开启自动散步时落地缓冲后自动走动；主动休息保留睡眠";
        }
        else if (PortraitRig.Supports(c.Category,c.FamilyId) && PortraitMotion.Supports(action.Motion))
        { status = "程序动作"; detail = "当前服装的骨骼动作与反馈特效"; }
        else if (action.Key == "rest")
        { status = "静态姿势"; detail = "本外观睡姿配合轻微呼吸，缺少入睡过渡图"; }
        else { status = "缺少动作"; detail = "当前只保留本外观静态图与可用特效，不能算作完整动作"; }
        if (Membership.Requirements.TryGetValue(action.Key, out var minimum)) detail += Membership.TestingOpen ? "；测试阶段所有等级与访客开放" : $"；会员：{Membership.Name(minimum)}及以上开放";
        return new(c.Id,c.FamilyId,c.Category,outfit,action.Key,action.Title,status,detail,art.Sprite.File,count);
    }
    internal static CoverageRow[] All(Catalog catalog) =>
        (from c in catalog.Characters where Catalog.BuiltInFamilies.Contains(c.FamilyId)
         from outfit in Catalog.BuiltInOutfits from action in Actions select Assess(c,outfit,action)).ToArray();
}

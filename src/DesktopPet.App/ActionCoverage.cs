using DesktopPet.Core;

namespace DesktopPet.App;

internal sealed record DemoAction(string Key, string Title, string Motion, int Duration = 2600);
internal sealed record CoverageRow(string Character, string Family, string Category, string Outfit, string Action,
    string Title, string Status, string Detail, string File, int Frames);

internal static class ActionCoverage
{
    internal static readonly DemoAction[] Actions = [
        new("idle","待机","idle"), new("listen","聆听","listen"), new("thinking","思考 1 秒","thinking",1200),
        new("chat","聊天","chat"), new("checkin","早餐","meal",4200), new("snack","零食","eat",3000),
        new("headpat","摸头","headpat"), new("poke","揉脸","poke"), new("tickle","挠痒","tickle",2200),
        .. CareRoutine.All.Select(routine => new DemoAction(routine.Key,routine.Title,"idle",routine.Duration)),
        new("think","发呆","think",3200), new("jump","跳跃与落地","jump",1400), new("curl","抱膝","curl",3000),
        new("rest","休息","sleep",3200), new("blocks","堆积木","build",4400), new("ball","等待投球","ball-ready"),
        new("ball-hit","球命中","ball-hit",1500), new("ball-miss","球未命中","ball-miss",1800),
        new("walk","散步","walk",3000), new("peek","走到边缘躲藏","walk",3200),
        new("found","找到啦","happy",2000),
        new("pickup","提起","pickup",1800), new("place","手动放置","idle",1200), new("drop","自然落下","pickup",1800),
        new("shake","摇晃提起","pickup-dizzy",3000), new("dizzy","头晕恢复","dizzy",3000),
        new("bonk","轻敲","bonk",1900), new("dance","互动舞蹈","dance",(int)PetDance.DurationMs)
    ];
    internal static CoverageRow Assess(Character c, string outfit, DemoAction action)
    {
        string motion = action.Motion == "pickup-dizzy" ? "pickup" : action.Motion;
        var clip = c.MotionFor(outfit, motion);
        var art = c.Resolve(outfit, motion, 0);
        int count = clip?.Frames?.Distinct().Count() ?? (clip is null ? 0 : clip.Columns * clip.Rows);
        string status, detail;
        if (CareRoutine.Find(action.Key) is { } routine)
        {
            bool available = routine.Steps.All(step => step.Motion is "idle" or "listen" or "happy" or "sleep"
                || c.MotionFor(outfit,step.Motion) is not null || PortraitRig.Supports(c.Category,c.FamilyId) && PortraitMotion.Supports(step.Motion));
            status = available ? "组合动作" : "缺少动作";
            detail = routine.Key switch { "praise" => "聆听、开心回应与摸头鼓励", "comfort" => "安静倾听、摸头安抚，再回到放松姿势", _ => "安静下来、蜷起、呼吸入睡；保持休息直到唤醒" };
        }
        else if (action.Key == "dance")
        { status = !PortraitRig.SupportsDance(c.Category,c.FamilyId)?"不适用":clip?.DanceRig is not null?"程序动作":"缺少动作"; detail = status == "不适用" ? "按设计仅3D真人提供舞蹈" : "专用舞蹈底图与独立关节；16 拍侧步、点地、展臂和收势，支撑脚固定，长裙保持连贯"; }
        else if (action.Key == "found")
        { status = "静态姿势"; detail = "被你找到啦！左键和右键均结束躲藏、回到桌面并开心回应"; }
        else if (action.Key is "idle" or "listen" or "place")
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
        if (Membership.Requirements.TryGetValue(action.Key, out var minimum)) detail += $"；会员：{Membership.Name(minimum)}及以上开放";
        return new(c.Id,c.FamilyId,c.Category,outfit,action.Key,action.Title,status,detail,art.Sprite.File,count);
    }
    internal static CoverageRow[] All(Catalog catalog) =>
        (from c in catalog.Characters where Catalog.BuiltInFamilies.Contains(c.FamilyId)
         from outfit in new[] { "original","swim","wedding" } from action in Actions select Assess(c,outfit,action)).ToArray();
}

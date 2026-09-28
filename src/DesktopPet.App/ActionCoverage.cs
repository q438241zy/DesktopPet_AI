using DesktopPet.Core;

namespace DesktopPet.App;

internal sealed record DemoAction(string Key, string Title, string Motion, int Duration = 2600);
internal sealed record CoverageRow(string Character, string Family, string Category, string Outfit, string Action,
    string Title, string Status, string Detail, string File, int Frames);

internal static class ActionCoverage
{
    internal static readonly DemoAction[] Actions = [
        new("idle","待机","idle"), new("listen","聆听","listen"), new("thinking","思考 1 秒","thinking",1200),
        new("chat","回答","chat"), new("checkin","早餐","meal",4200), new("snack","零食","eat",3000),
        new("headpat","摸头","headpat"), new("poke","揉脸","poke"), new("tickle","挠痒","tickle",2200),
        new("think","发呆","think",3200), new("jump","跳跃与落地","jump",1400), new("curl","抱膝","curl",3000),
        new("rest","休息","sleep",3200), new("blocks","堆积木","build",4400), new("ball","等待投球","ball-ready"),
        new("ball-hit","球命中","ball-hit",1500), new("ball-miss","球未命中","ball-miss",1800),
        new("walk","散步","walk",3000), new("peek","走到边缘躲藏","walk",3200),
        new("pickup","提起","pickup",1800), new("place","手动放置","idle",1200), new("drop","自然落下","pickup",1800),
        new("bonk","轻敲","bonk",1900), new("dance","互动舞蹈","dance",3600)
    ];
    internal static CoverageRow Assess(Character c, string outfit, DemoAction action)
    {
        var clip = c.MotionFor(outfit, action.Motion);
        var art = c.Resolve(outfit, action.Motion, 0);
        int count = clip?.Frames?.Distinct().Count() ?? (clip is null ? 0 : clip.Columns * clip.Rows);
        string status, detail;
        if (action.Key == "dance")
        { status = PortraitRig.SupportsDance(c.Category,c.FamilyId) ? "程序动作" : "不适用"; detail = status == "不适用" ? "按设计仅真人版提供舞蹈" : "使用当前服装的骨骼程序舞蹈"; }
        else if (action.Key is "idle" or "listen" or "place")
        { status = "静态姿势"; detail = action.Key == "place" ? "手动放置后停留在用户指定的位置" : clip is null ? "使用本外观的安静待机姿势" : "本外观专用聆听姿势"; }
        else if (clip is not null)
        {
            bool alias = !(outfit == "original" ? c.Motions : c.Outfits[outfit].Motions).ContainsKey(action.Motion);
            status = alias && action.Key != "thinking" ? "近似动作" : count > 1 ? "专用逐帧" : "专用姿势";
            detail = alias && action.Key != "thinking" ? "复用本外观的相近动作，尚未有独立动作图" : $"本外观 {count} 张独立姿势";
            if (action.Key == "thinking") detail += "，配合思考提示；至少 1 秒后才回答";
            if (action.Key == "drop") detail += "；自然落下和手动放置分开处理";
        }
        else if (PortraitRig.Supports(c.Category,c.FamilyId) && PortraitMotion.Supports(action.Motion))
        { status = "程序动作"; detail = "当前服装的骨骼动作与反馈特效"; }
        else if (action.Key == "rest")
        { status = "静态姿势"; detail = "本外观睡姿配合轻微呼吸，缺少入睡过渡图"; }
        else { status = "缺少动作"; detail = "当前只保留本外观静态图与可用特效，不能算作完整动作"; }
        return new(c.Id,c.FamilyId,c.Category,outfit,action.Key,action.Title,status,detail,art.Sprite.File,count);
    }
    internal static CoverageRow[] All(Catalog catalog) =>
        (from c in catalog.Characters where Catalog.BuiltInFamilies.Contains(c.FamilyId)
         from outfit in new[] { "original","swim","wedding" } from action in Actions select Assess(c,outfit,action)).ToArray();
}

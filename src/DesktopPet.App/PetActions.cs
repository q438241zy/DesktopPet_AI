namespace DesktopPet.App;

internal sealed record PetAction(string Key, string Title, string Icon);

internal static class PetActions
{
    public static readonly PetAction[] Five=[new("highfive","击掌","highfive"),new("rps","猜拳","rps"),new("gift","拆礼物","gift"),new("read","翻书共读","read"),new("photo","合照","photo")];
    public static readonly PetAction[] Touches = [new("headpat", "摸头", "headpat"), new("poke", "揉脸", "poke"), new("tickle", "挠痒", "tickle")];
    public static readonly PetAction Chat = new("chat", "聊天", "chat");
    public static readonly PetAction[] Care = [new("checkin", "早餐", "breakfast"), .. Touches, new("snack", "零食", "snack"), new("praise", "夸夸", "praise"), new("comfort", "安抚", "comfort"), new("lullaby", "哄睡", "lullaby"), new("comb", "梳头", "comb"), new("wipe", "擦脸", "wipe"), new("stretch", "伸懒腰", "stretch")];
    public static readonly PetAction[] Play = [new("ball", "玩球", "ball"), new("blocks", "积木", "blocks"), new("walk", "散步", "walk"), new("peek", "躲藏", "peek"), new("bubbles", "吹泡泡", "bubbles"), new("stars", "数星星", "stars"), new("butterfly", "捉蝴蝶", "butterfly")];
    public static readonly PetAction[] Motions = [.. Five,new("think", "发呆", "think"), new("jump", "跳跃", "jump"), new("peek", "躲藏", "peek"), new("curl", "蜷起", "curl"), new("bonk", "轻敲", "bonk")];
    public static readonly PetAction[] Daily = [.. Five,.. Touches, Care[4], Chat, .. Care.Skip(5), .. Play, new("letter", "纪念卡", "letter"), new("rest", "休息", "moon")];
    public static PetAction[] Menu(string group)
    {
        PetAction[] items = group switch
        {
            "care" => [new("group:root", "返回", "back"), .. Care],
            "play" => [new("group:root", "返回", "back"), .. Play],
            "motions" => [new("group:root", "返回", "back"), .. Motions],
            _ => [Chat, new("group:care", "照顾", "heart"), new("group:play", "玩耍", "ball"), new("group:motions", "互动", "pose"), new("rest", "休息", "moon"), new("settings", "设置", "settings"), new("close", "收起", "close")]
        };
        return items.ToArray();
    }
}

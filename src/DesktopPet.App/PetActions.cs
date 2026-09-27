namespace DesktopPet.App;

internal sealed record PetAction(string Key, string Title, string Icon);

internal static class PetActions
{
    public static readonly PetAction[] Care = [new("checkin", "早餐", "sun"), new("headpat", "摸头", "headpat"), new("poke", "戳脸", "poke"), new("tickle", "挠痒", "tickle"), new("snack", "零食", "food"), new("chat", "聊天", "chat")];
    public static readonly PetAction[] Play = [new("ball", "玩球", "ball"), new("blocks", "积木", "blocks"), new("walk", "散步", "walk"), new("peek", "躲猫猫", "peek"), new("dance", "跳舞", "dance"), new("nudge", "推球", "nudge")];
    public static readonly PetAction[] Motions = [new("think", "发呆", "think"), new("jump", "跳跃", "jump"), new("peek-left", "左侧躲藏", "peek-left"), new("peek-right", "右侧躲藏", "peek-right"), new("curl", "蜷起", "curl"), new("bonk", "轻敲", "bonk")];
    public static readonly PetAction[] Daily = [Care[1], Care[2], Care[3], Care[4], Care[5], Play[0], Play[1], Play[2], Play[3], Play[4], new("letter", "纪念卡", "letter"), new("rest", "休息", "moon")];
    public static PetAction[] Menu(string group, bool canDance)
    {
        PetAction[] items = group switch
        {
            "care" => [new("group:root", "返回", "back"), .. Care],
            "play" => [new("group:root", "返回", "back"), .. Play],
            "motions" => [new("group:root", "返回", "back"), .. Motions],
            _ => [new("group:care", "照顾", "heart"), new("group:play", "玩耍", "ball"), new("group:motions", "动作", "spark"), Play[4], new("rest", "休息", "moon"), new("settings", "设置", "settings"), new("close", "收起", "close")]
        };
        return items.Where(e => e.Key != "dance" || canDance).ToArray();
    }
}

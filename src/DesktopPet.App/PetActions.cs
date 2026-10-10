namespace DesktopPet.App;

internal sealed record PetAction(string Key, string Title, string Icon);

internal static class PetActions
{
    public static readonly PetAction[] Five=[new("highfive","击掌","highfive"),new("rps","猜拳","rps"),new("gift","拆礼物","gift"),new("read","一起阅读","read")];
    public static readonly PetAction[] Touches = [new("headpat", "摸头", "headpat"), new("poke", "揉脸", "poke"), new("tickle", "挠痒", "tickle")];
    public static readonly PetAction Chat = new("chat", "聊天", "chat");
    public static readonly PetAction[] Care = [new("checkin", "吃饭", "breakfast"), .. Touches, new("snack", "吃零食", "snack"), new("praise", "夸夸", "praise"), new("comfort", "安抚", "comfort"), new("lullaby", "哄睡", "lullaby"), new("comb", "梳头", "comb"), new("wipe", "擦脸", "wipe"), new("stretch", "伸懒腰", "stretch")];
    public static readonly PetAction[] Play = [new("ball", "玩球", "ball"), new("blocks", "积木", "blocks"), new("walk", "散步", "walk"), new("peek", "躲藏", "peek"), new("bubbles", "吹泡泡", "bubbles"), new("stars", "数星星", "stars"), new("butterfly", "捉蝴蝶", "butterfly")];
    public static readonly PetAction[] Motions = [.. Five,new("think", "发呆", "think"), new("jump", "跳跃", "jump"), new("peek", "躲藏", "peek"), new("curl", "蜷起", "curl"), new("bonk", "轻敲", "bonk")];
    public static readonly PetAction[] Interactions = [..Care, new("rest", "休息", "moon")];
    public static readonly PetAction[] Games = [..Five, ..Play, ..Motions.Where(a => !Five.Any(f => f.Key == a.Key) && a.Key != "peek")];
    public static readonly PetAction[] Daily = [Chat, ..Interactions, ..Games, new("letter", "纪念卡", "letter")];
    public const int PageSize = 6;
    public static PetAction[] Menu(string group)
    {
        string[] parts = group.Split(':');
        string name = parts[0];
        PetAction[]? actions = name switch { "care" or "interact" => Interactions, "play" or "motions" => Games, _ => null };
        if (actions is not null)
        {
            int pages = (actions.Length + PageSize - 1) / PageSize;
            int page = parts.Length > 1 && int.TryParse(parts[1], out int n) ? Math.Clamp(n, 0, pages - 1) : 0;
            return [new("group:root", "返回", "back"), ..actions.Skip(page * PageSize).Take(PageSize),
                ..(pages > 1 ? new PetAction[] { new($"group:{name}:{(page + 1) % pages}", $"下一页 · {page + 1}/{pages}", "arrow") } : [])];
        }
        return [Chat, new("group:interact", "互动", "heart"), new("group:play", "玩耍", "ball"),
            new("settings", "设定", "settings"), new("close", "收起", "close")];
    }
}

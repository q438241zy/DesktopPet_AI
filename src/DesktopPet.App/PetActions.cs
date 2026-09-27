namespace DesktopPet.App;

internal sealed record PetAction(string Key, string Title, string Icon, string Hint);

internal static class PetActions
{
    public static readonly PetAction[] Care = [new("checkin", "吃早饭", "sun", "每天相伴的第一件事"), new("headpat", "摸摸头", "headpat", "轻轻摸摸它"), new("poke", "戳戳脸", "poke", "软软的脸颊"), new("tickle", "挠痒痒", "tickle", "一起笑一会儿"), new("snack", "喂零食", "food", "来一份小点心"), new("chat", "聊聊天", "chat", "听听它想说什么")];
    public static readonly PetAction[] Play = [new("ball", "一起玩球", "ball", "拖动小球，松手抛出"), new("blocks", "搭积木", "blocks", "一起搭一座小塔"), new("walk", "散步寻宝", "walk", "沿桌边寻找小礼物"), new("peek", "躲猫猫", "peek", "走到屏幕边缘再藏起来"), new("dance", "一起跳舞", "dance", "点击宠物，跟上 16 拍"), new("nudge", "轻推小球", "nudge", "轻轻把球推开")];
    public static readonly PetAction[] Motions = [new("think", "想一想", "think", "留一点发呆时间"), new("jump", "跳一下", "jump", "轻轻跃起"), new("peek-left", "左边躲藏", "peek-left", "走到左边再探头"), new("peek-right", "右边躲藏", "peek-right", "走到右边再探头"), new("curl", "蜷起来", "curl", "抱成小小一团"), new("bonk", "软锤轻敲", "bonk", "轻轻敲一下")];
    public static readonly PetAction[] Favorites = [Care[1], Care[4], Play[4], Play[3], Play[2], new("rest", "歇一会儿", "moon", "挥手后安静休息")];
    public static readonly PetAction[] Daily = [Care[1], Care[2], Care[3], Care[4], Care[5], Play[0], Play[1], Play[2], Play[3], Play[4], new("letter", "纪念卡片", "letter", "收藏相伴的时光"), Favorites[5]];
}

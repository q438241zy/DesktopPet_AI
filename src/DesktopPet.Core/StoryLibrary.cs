namespace DesktopPet.Core;

public sealed record CompanionStory(string Id,string Title,IReadOnlyList<string> Sentences);

/// <summary>Original short stories. A completed read earns one persistent book, never a timer-only reward.</summary>
public static class StoryLibrary
{
    public static IReadOnlyList<CompanionStory> All { get; } = Array.AsReadOnly(new[] {
        new CompanionStory("cloud-post","云朵邮差",new[] {
            "小鲸在清晨收到一封没有地址的信。", "信里写着：“请把今天的快乐送给一个朋友。”",
            "她把信放进小包，沿着软软的云路出发。", "路边的小兔正在发愁，因为风吹走了她的花帽子。",
            "小鲸踮起脚，从树枝上取下帽子，轻轻递给小兔。", "小兔开心地笑了，邀请她一起吃刚烤好的小饼干。",
            "小鲸这才明白，快乐的地址，就是朋友的笑容。" }),
        new CompanionStory("little-bell","丢失的小铃铛",new[] {
            "小猫最喜欢的铃铛不见了，她在窗边找了很久。", "小鲸听见叹气声，便拿着小灯过来帮忙。",
            "她们先看了桌子下面，又翻了柔软的坐垫。", "风吹动窗帘，角落里忽然响起一声轻轻的叮当。",
            "原来铃铛滚进了装毛线的小篮子。", "小猫把铃铛系好，尾巴开心地摇来摇去。",
            "为了感谢小鲸，她织了一条蓝色的小围巾。", "从那以后，每次铃铛响起，两位朋友都会想起这一天。" }),
        new CompanionStory("star-seed","星星种子",new[] {
            "小鲸捡到一颗亮晶晶的种子，决定把它种在窗边。", "她每天给种子一点水，再轻轻说一声早安。",
            "过了几天，泥土里冒出一片小小的叶子。", "一个下雨的夜晚，小鲸担心叶子会冷，替它撑起小伞。",
            "第二天，叶子中间开出一朵像星星一样的小花。", "小鲸没有摘下花，而是把花盆搬到朋友们都能看见的地方。",
            "星星花慢慢长大，把窗边照得暖暖的。", "小鲸笑着说，原来耐心和关心，也能种出光。" })
    });
    public static CompanionStory? Find(string? id)=>All.FirstOrDefault(s=>s.Id==id);
}

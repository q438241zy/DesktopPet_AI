using System.Text.Json;

namespace DesktopPet.Core;

public static class CompanionPersonas
{
    private static readonly JsonDocument Data = JsonDocument.Parse(typeof(CompanionPersonas).Assembly.GetManifestResourceStream("DesktopPet.Core.Data.companion-personas.json")!);
    private static readonly JsonElement Characters = Data.RootElement.GetProperty("characters");
    public static string Family(string idOrName)
    {
        string clean = idOrName.Split('·')[0].Trim();
        foreach (var entry in Characters.EnumerateObject())
            if (clean.Equals(entry.Name, StringComparison.OrdinalIgnoreCase) || clean.StartsWith(entry.Name + "-", StringComparison.OrdinalIgnoreCase) || clean.Equals(entry.Value.GetProperty("name").GetString(), StringComparison.OrdinalIgnoreCase)) return entry.Name;
        return "whale";
    }
    public static JsonElement Get(string id) => Characters.GetProperty(Family(id));
    public static string Text(string id, string key) => Get(id).GetProperty(key).GetString()!;
    public static string[] Traits(string id) => Get(id).GetProperty("traits").EnumerateArray().Select(e => e.GetString()!).ToArray();
    public static JsonElement Profile(string id) => Get(id).GetProperty("profile");
    private static bool Has(string value, params string[] terms) => terms.Any(t => value.Contains(t, StringComparison.OrdinalIgnoreCase));
    public static string Intent(string text) => Has(text, "晚安", "再见", "再見", "拜拜") ? "night" : Has(text, "累", "困", "休息", "疲倦") ? "tired" : Has(text, "难过", "難過", "伤心", "傷心", "不开心", "不開心", "烦", "煩") ? "sad" : Has(text, "工作", "加班", "任务", "任務", "作业", "作業", "上班", "忙") ? "work" : Has(text, "完成", "成功", "开心", "開心", "高兴") ? "good" : Has(text, "谢谢", "謝謝", "thanks") ? "thanks" : Has(text, "你好", "hello", "嗨", "早安") ? "hello" : "fallback";
    public static string Reply(string id, IReadOnlyList<ChatMessage> messages, int affinity = 0)
    {
        var p = Get(id); var users = messages.Where(m => m.Role == "user").Select(m => m.Content).ToArray(); string text = users.LastOrDefault() ?? "", previous = users.Length > 1 ? users[^2] : "";
        if (Has(text, "你是谁", "你是誰", "名字")) return $"我是 {Text(id, "name")}，这里的桌面伙伴。{Text(id, "description")}";
        if (Has(text, "你喜欢", "你喜歡", "妳喜欢", "妳喜歡", "爱好", "愛好", "喜欢什么", "喜歡什麼")) return Profile(id).GetProperty("favorite").GetString()!;
        if (Has(text, "性格", "介绍自己", "介紹自己", "自我介绍", "自我介紹")) return Profile(id).GetProperty("intro").GetString()!;
        if (affinity <= -40 && Has(text, "逗", "打你", "捉弄", "敲")) return Text(id, "distant");
        string topic = Intent(text); if (topic == "fallback" && Has(text, "好", "嗯", "继续", "繼續", "然后", "然後") && Intent(previous) is "work" or "tired") topic = Intent(previous);
        var value = p.GetProperty(topic); int turn = users.SkipLast(1).Count(s => Intent(s) == topic);
        string answer = value.ValueKind == JsonValueKind.Array ? value[Math.Min(turn, value.GetArrayLength() - 1)].GetString()! : value.GetString()!;
        answer = answer.Replace("{topic}", text.Length > 24 ? text[..24] + "…" : text);
        if (affinity >= 60 && topic == "hello") answer += " 又见到熟悉的你啦。";
        return affinity <= -40 && topic == "hello" ? Text(id, "distant") : answer;
    }
    public static string Prompt(string id, int affinity = 0)
    {
        var profile = Profile(id);
        string about = string.Join("", profile.GetProperty("about").EnumerateArray().Select(e => e.GetString()));
        string likes = string.Join("、", profile.GetProperty("likes").EnumerateArray().Select(e => e[0].GetString() + "（" + e[1].GetString() + "）"));
        return $"你是用户设定的桌面伙伴 {Text(id, "name")}，不是对应厂商的官方模型或真人。性格：{string.Join('、', Traits(id))}。{Text(id, "tone")} 角色说明：{about} 喜欢的东西：{likes}。相处方式：{profile.GetProperty("together").GetString()} 保持与档案一致，不因换衣或Q版/3D真人改变性格。用自然简短的中文接续上下文，通常1至3句话。不声称执行了未执行的桌面操作。当前关系值{affinity}，范围-100到100；高好感可以更熟悉，负好感只表达温和边界，不侮辱或操控用户。关系分数不改变事实、能力或安全边界。";
    }
    public static string Reminder(string id, int index)
    {
        var tasks = Data.RootElement.GetProperty("reminderTasks"); return Text(id, "remind").Replace("{task}", tasks[Math.Abs(index % tasks.GetArrayLength())].GetString());
    }
}

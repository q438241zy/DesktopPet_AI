using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DesktopPet.Core;

public sealed record ChatMessage(string Role, string Content);
public sealed record ChatOptions(string Endpoint = "", string Model = "");

/// <summary>Explicit local conversation, or an optional user-configured chat-completions service.</summary>
public static class CompanionChat
{
    public static async Task<string> ReplyAfterThinkingAsync(HttpClient client, ChatOptions options, string apiKey,
        IReadOnlyList<ChatMessage> messages, string name, CancellationToken cancellationToken)
    {
        // Start transport and the visible thinking period together. A fast API
        // response waits one second; a slow service does not incur another second.
        var minimum = Task.Delay(1000, cancellationToken);
        var reply = ReplyAsync(client, options, apiKey, messages, name, cancellationToken);
        await Task.WhenAll(minimum, reply);
        cancellationToken.ThrowIfCancellationRequested();
        return await reply;
    }
    public static Uri Endpoint(string value)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) || uri.UserInfo.Length > 0
            || uri.Query.Length > 0 || uri.Fragment.Length > 0
            || uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback))
            throw new ArgumentException("API 地址需要使用 HTTPS；本机服务可以使用 HTTP。");
        return uri.AbsolutePath.TrimEnd('/').EndsWith("/chat/completions", StringComparison.Ordinal)
            ? uri : new Uri(uri.AbsoluteUri.TrimEnd('/') + "/chat/completions");
    }

    public static string LocalReply(IReadOnlyList<ChatMessage> messages, string name)
    {
        var user = messages.Where(m => m.Role == "user").Select(m => m.Content).ToArray();
        string latest = user.LastOrDefault() ?? "", earlier = string.Join(' ', user.TakeLast(4).SkipLast(1));
        bool Has(string text, params string[] words) => words.Any(w => text.Contains(w, StringComparison.OrdinalIgnoreCase));
        if (Has(latest, "再见", "再見", "晚安", "拜拜")) return "好，先去休息吧。我会安静地待在桌边，等你下次来找我。";
        if (Has(latest, "你是谁", "你是誰", "名字")) return $"我是{name.Split('·')[0].Trim()}，你的桌边伙伴。现在是本机对话，我们可以聊聊今天，或者一起编个小故事。";
        if (Has(latest, "故事", "接着", "接著", "后来", "後來", "继续", "繼續") || Has(earlier, "故事") && Has(latest, "好", "嗯", "想", "听", "聽"))
        {
            int chapters = messages.Count(m => m.Role == "assistant" && (m.Content.Contains("小云朵") || m.Content.Contains("小灯")));
            return chapters switch
            {
                0 => "一朵小云朵第一次值夜班，怕自己挡住星星，便缩成了小小一团。路过的月亮递给它一盏小灯。你猜灯里装着什么？",
                1 => "小灯里装着白天收集来的笑声。小云朵轻轻一晃，迷路的小鸟就听见了家的方向。它也终于知道，安静的陪伴一样有用。还想听后面吗？",
                _ => "天亮时，小云朵把小灯放在窗边，留了一张纸条：累的时候，可以在这里停一会儿。故事先停在这个暖暖的早晨吧。你今天有没有遇到一件小小的好事？"
            };
        }
        if (Has(latest, "工作", "加班", "上班", "作业", "作業", "任务", "任務"))
            return Has(earlier, "累", "烦", "煩", "忙") ? "原来是工作堆在一起了。我们先挑一件最小、最容易结束的事，剩下的慢慢来。你想先整理一下，还是先歇五分钟？" : "今天的事情很多吗？可以先说一件最挂心的，我陪你把它理清楚。";
        if (Has(latest, "累", "困", "休息", "歇"))
            return Has(earlier, "累", "工作", "加班") ? "那就先歇一会儿。放松肩膀，喝口水，不用急着回复我。想听个短短的小故事也可以。" : "辛苦了。今天是事情太多，还是单纯有点没精神？我在这里听你说。";
        if (Has(latest, "难过", "難過", "不开心", "不開心", "烦", "煩")) return "听起来今天不太顺。你愿意说说发生了什么吗？不用急着想办法，我们先把话说出来。";
        if (Has(latest, "开心", "開心", "成功", "完成", "好事")) return "这件事值得好好高兴一下。是哪一个瞬间让你最开心？";
        if (Has(latest, "谢谢", "謝謝")) return "不客气，能陪你聊一会儿就很好。接下来想聊今天，还是听个小故事？";
        if (Has(latest, "你好", "嗨", "hello", "hi")) return user.Length > 1 ? "我还在呀。刚才的话题要接着聊，还是换个轻松的话题？" : "你好呀，我正好在等你。今天过得怎么样？";
        return (user.Length % 4) switch
        {
            1 => "我在听。今天有什么想分享的小事吗？开心的、烦心的都可以。",
            2 => "嗯，可以再说一点。你最在意的是哪一部分？",
            3 => "听见啦。你想继续讲，我就陪你聊；想换个心情，我们也可以听个小故事。",
            _ => "我们慢慢来。现在更想聊今天的事情，还是一起安静地休息一会儿？"
        };
    }

    public static async Task<string> ReplyAsync(HttpClient client, ChatOptions options, string apiKey,
        IReadOnlyList<ChatMessage> messages, string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint)) return LocalReply(messages, name);
        if (string.IsNullOrWhiteSpace(options.Model)) throw new ArgumentException("请填写模型名称。");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        var requestToken = deadline.Token;
        var context = new[] { new ChatMessage("system", $"你是桌面伙伴{name}。用自然、简短的中文接续聊天。不冒充对应厂商官方模型，不声称执行了未执行的桌面操作。") }
            .Concat(messages.Where(m => m.Role is "user" or "assistant").TakeLast(24));
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint(options.Endpoint));
        if (!string.IsNullOrWhiteSpace(apiKey)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        request.Content = new StringContent(JsonSerializer.Serialize(new { model = options.Model.Trim(), messages = context.Select(m => new { role = m.Role, content = m.Content }), stream = false }), Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, requestToken);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"模型服务返回 {(int)response.StatusCode}。请检查地址、模型与金钥。");
        await using var body = await response.Content.ReadAsStreamAsync(requestToken);
        using var buffer = new MemoryStream(); byte[] chunk = new byte[8192];
        int length;
        while ((length = await body.ReadAsync(chunk, requestToken)) > 0)
        {
            if (buffer.Length + length > 1024 * 1024) throw new InvalidDataException("模型回复过长，请重试。");
            buffer.Write(chunk, 0, length);
        }
        using var json = JsonDocument.Parse(buffer.ToArray());
        if (json.RootElement.ValueKind != JsonValueKind.Object || !json.RootElement.TryGetProperty("choices", out var choices)
            || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0 || choices[0].ValueKind != JsonValueKind.Object
            || !choices[0].TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object || !message.TryGetProperty("content", out var content)
            || content.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(content.GetString()))
            throw new InvalidDataException("模型没有返回文字回复。");
        return content.GetString()!;
    }
}

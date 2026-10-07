using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DesktopPet.Core;

public sealed record ChatProvider(string Id, string Name, string Base, string Protocol);

/// <summary>Native equivalents of the approved Demo adapters; see docs/companion-api-providers.md.</summary>
public static class CompanionProviders
{
    public static IReadOnlyList<ChatProvider> All { get; } = new[] {
        new ChatProvider("openai", "GPT · OpenAI", "https://api.openai.com/v1", "chat"),
        new ChatProvider("claude", "Claude · Anthropic", "https://api.anthropic.com/v1", "messages"),
        new ChatProvider("gemini", "Gemini · Google", "https://generativelanguage.googleapis.com/v1beta", "gemini"),
        new ChatProvider("grok", "Grok · xAI", "https://api.x.ai/v1", "chat"),
        new ChatProvider("deepseek", "DeepSeek", "https://api.deepseek.com", "chat"),
        new ChatProvider("qwen", "Qwen · 阿里云百炼", "https://dashscope.aliyuncs.com/compatible-mode/v1", "chat"),
        new ChatProvider("glm", "GLM · 智谱", "https://open.bigmodel.cn/api/paas/v4", "chat"),
        new ChatProvider("kimi", "Kimi · Moonshot", "https://api.moonshot.ai/v1", "chat") };
    public static string InferProvider(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) return "local";
        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            foreach (var p in All) if (uri.Host.Equals(new Uri(p.Base).Host, StringComparison.OrdinalIgnoreCase)) return p.Id;
            if (uri.Host == "api.moonshot.cn") return "kimi";
            if (uri.Host.EndsWith(".aliyuncs.com", StringComparison.OrdinalIgnoreCase)) return "qwen";
        }
        return "openai";
    }
    public static ChatProvider Find(string id) => All.FirstOrDefault(p => p.Id == id) ?? throw new ArgumentException("请选择支持的模型接口。");
    public static Uri Endpoint(string value, string provider = "openai", string model = "")
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0
            || uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback) || value.Contains('{') || value.Contains('}'))
            throw new ArgumentException("API 地址需要使用 HTTPS；本机服务可以使用 HTTP。请填入完整地址。");
        var spec = Find(provider); string path = uri.AbsolutePath.TrimEnd('/');
        if (spec.Protocol == "gemini")
        {
            if (Regex.IsMatch(path, @"/(chat/completions|messages|responses)$")) throw new ArgumentException("Gemini 使用 generateContent 接口。");
            var full = Regex.Match(path, @"/models/([^/]+):generateContent$"); string name = model.Trim().Replace("models/", "");
            if (full.Success) { if (name.Length > 0 && Uri.UnescapeDataString(full.Groups[1].Value) != name) throw new ArgumentException("地址中的模型与模型名称不一致。"); }
            else
            {
                if (!Regex.IsMatch(name, @"^[A-Za-z0-9._-]+$") || path.Contains(':') || Regex.IsMatch(path, @"/models/.+")) throw new ArgumentException("请填写 Gemini 基础地址和有效模型名称。");
                path = Regex.Replace(path.Length == 0 ? "/v1beta" : path, @"/models$", "") + "/models/" + Uri.EscapeDataString(name) + ":generateContent";
            }
        }
        else if (spec.Protocol == "messages")
        {
            if (Regex.IsMatch(path, @"/(chat/completions|responses)$")) throw new ArgumentException("Claude 使用 /v1/messages 接口。");
            if (path.Length == 0) path = "/v1"; if (!path.EndsWith("/messages", StringComparison.Ordinal)) path += "/messages";
        }
        else
        {
            if (Regex.IsMatch(path, @"/(responses|messages)$|:generateContent$")) throw new ArgumentException("此服务使用 Chat Completions 接口。");
            if (path.Length == 0) path = new Uri(spec.Base).AbsolutePath.TrimEnd('/');
            if (!path.EndsWith("/chat/completions", StringComparison.Ordinal)) path += "/chat/completions";
        }
        return new UriBuilder(uri) { Path = path }.Uri;
    }
    public static HttpRequestMessage Request(ChatOptions options, string key, IReadOnlyList<ChatMessage> messages, string system)
    {
        var provider = Find(options.EffectiveProvider); string model = options.Model.Trim();
        if (model.Length == 0) throw new ArgumentException("请填写模型名称。");
        var url = Endpoint(options.Endpoint, provider.Id, model); key = key.Trim();
        if (key.Length == 0 && !url.IsLoopback) throw new ArgumentException("请填写本次会话的 API Key。");
        if (key.Any(c => c <= 32 || c >= 127)) throw new ArgumentException("API Key 格式不正确，请重新粘贴。");
        var history = new List<ChatMessage>();
        foreach (var m in messages.Where(m => m.Role is "user" or "assistant" && !string.IsNullOrWhiteSpace(m.Content)).TakeLast(24))
        {
            if (history.Count == 0 && m.Role != "user") continue;
            if (history.Count > 0 && history[^1].Role == m.Role) history[^1] = history[^1] with { Content = history[^1].Content + "\n" + m.Content }; else history.Add(m);
        }
        var request = new HttpRequestMessage(HttpMethod.Post, url); object body;
        if (provider.Protocol == "messages")
        {
            if (key.Length > 0) request.Headers.Add("x-api-key", key); request.Headers.Add("anthropic-version", "2023-06-01");
            if (!string.IsNullOrWhiteSpace(options.Workspace)) request.Headers.Add("anthropic-workspace-id", options.Workspace.Trim());
            body = new { model, system, messages = history.Select(m => new { role = m.Role, content = m.Content }), max_tokens = 1024, stream = false };
        }
        else if (provider.Protocol == "gemini")
        {
            if (key.Length > 0) request.Headers.Add("x-goog-api-key", key);
            body = new { systemInstruction = new { parts = new[] { new { text = system } } }, contents = history.Select(m => new { role = m.Role == "assistant" ? "model" : "user", parts = new[] { new { text = m.Content } } }) };
        }
        else
        {
            if (key.Length > 0) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            body = new { model, messages = new[] { new ChatMessage(provider.Id == "openai" ? "developer" : "system", system) }.Concat(history).Select(m => new { role = m.Role, content = m.Content }), stream = false };
        }
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"); return request;
    }
    private static JsonElement Get(JsonElement value, string key) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var child) ? child : default;
    private static JsonElement First(JsonElement value) => value.ValueKind == JsonValueKind.Array && value.GetArrayLength() > 0 ? value[0] : default;
    private static string String(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString()! : "";
    public static string ResponseText(string provider, JsonElement json)
    {
        string protocol = Find(provider).Protocol, text = "";
        if (Get(json, "error").ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null) throw new InvalidDataException("模型服务未完成回复。");
        var parts = protocol == "messages" ? Get(json, "content") : protocol == "gemini" ? Get(Get(First(Get(json, "candidates")), "content"), "parts") : Get(Get(First(Get(json, "choices")), "message"), "content");
        if (parts.ValueKind == JsonValueKind.String && protocol == "chat") text = parts.GetString()!;
        else if (parts.ValueKind == JsonValueKind.Array)
            text = string.Join("\n", parts.EnumerateArray().Where(p => protocol == "gemini" ? Get(p, "thought").ValueKind != JsonValueKind.True : String(Get(p, "type")) == "text").Select(p => String(Get(p, "text"))).Where(s => s.Length > 0));
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidDataException("模型没有返回文字回复。"); return text.Trim();
    }
    public static async Task<string> SendAsync(HttpClient client, ChatOptions options, string key, IReadOnlyList<ChatMessage> messages, string system, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); deadline.CancelAfter(TimeSpan.FromSeconds(45));
        using var request = Request(options, key, messages, system);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"模型服务返回 {(int)response.StatusCode}。请检查地址、模型与 API Key。");
        await using var body = await response.Content.ReadAsStreamAsync(deadline.Token); using var buffer = new MemoryStream(); byte[] chunk = new byte[8192]; int length;
        while ((length = await body.ReadAsync(chunk, deadline.Token)) > 0) { if (buffer.Length + length > 1024 * 1024) throw new InvalidDataException("模型回复过长，请重试。"); buffer.Write(chunk, 0, length); }
        using var json = JsonDocument.Parse(buffer.ToArray()); return ResponseText(options.EffectiveProvider, json.RootElement);
    }
}

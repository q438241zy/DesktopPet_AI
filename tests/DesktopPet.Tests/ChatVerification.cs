using System.Net;
using System.Text.Json;
using DesktopPet.Core;

internal static class ChatVerification
{
    public static void Run(Action<string, Action> test)
    {
        void Require(bool ok) { if (!ok) throw new Exception("Chat contract failed"); }
        test("local conversation carries the preceding topic without making a network request", () =>
        {
            using var client = new HttpClient(new Handler((_, _) => throw new Exception("Local mode used the network")));
            var history = new List<ChatMessage>();
            foreach (string text in new[] { "你好", "今天好累", "工作有点多" })
            {
                history.Add(new("user", text));
                history.Add(new("assistant", CompanionChat.ReplyAsync(client, new(), "", history, "DeepSeek", default).GetAwaiter().GetResult()));
            }
            Require(history[^1].Content.Contains("原来是工作") && history.Where(m => m.Role == "assistant").Select(m => m.Content).Distinct().Count() == 3);
            history.Add(new("user", "想听故事")); history.Add(new("assistant", CompanionChat.LocalReply(history, "DeepSeek")));
            history.Add(new("user", "继续")); string second = CompanionChat.LocalReply(history, "DeepSeek"); history.Add(new("assistant", second));
            history.Add(new("user", "后来呢")); string third = CompanionChat.LocalReply(history, "DeepSeek");
            Require(second.Contains("笑声") && third.Contains("天亮"));
        });
        test("configured chat sends the conversation and returns the service reply", () =>
        {
            int calls = 0;
            using var client = new HttpClient(new Handler(async (request, token) =>
            {
                calls++; Require(request.Method == HttpMethod.Post && request.RequestUri!.AbsoluteUri == "http://localhost:1234/v1/chat/completions");
                Require(request.Headers.Authorization?.ToString() == "Bearer test-only-key");
                using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
                Require(payload.RootElement.GetProperty("model").GetString() == "mock-model");
                var turns = payload.RootElement.GetProperty("messages");
                Require(turns.GetArrayLength() == 4 && turns[0].GetProperty("role").GetString() == "system"
                    && turns[2].GetProperty("content").GetString() == "你好呀" && turns[3].GetProperty("content").GetString() == "接着聊");
                return Reply("{\"choices\":[{\"message\":{\"content\":\"模型的实际回复\"}}]}");
            }));
            string reply = CompanionChat.ReplyAsync(client, new("http://localhost:1234/v1", "mock-model"), "test-only-key",
                [new("user", "你好"), new("assistant", "你好呀"), new("user", "接着聊")], "DeepSeek", default).GetAwaiter().GetResult();
            Require(calls == 1 && reply == "模型的实际回复");
        });
        test("chat bounds history and accepts a complete endpoint without appending it twice", () =>
        {
            using var client = new HttpClient(new Handler(async (request, token) =>
            {
                Require(request.RequestUri!.AbsolutePath == "/v1/chat/completions" && request.Headers.Authorization is null);
                using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
                var turns = payload.RootElement.GetProperty("messages"); Require(turns.GetArrayLength() == 25 && turns[1].GetProperty("content").GetString() == "16");
                return Reply("{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}");
            }));
            var history = Enumerable.Range(0, 40).Select(i => new ChatMessage(i % 2 == 0 ? "user" : "assistant", i.ToString())).ToArray();
            CompanionChat.ReplyAsync(client, new("https://example.invalid/v1/chat/completions", "mock"), "", history, "pet", default).GetAwaiter().GetResult();
        });
        test("bad endpoint and model settings fail before transport", () =>
        {
            using var client = new HttpClient(new Handler((_, _) => throw new Exception("Unexpected network request")));
            foreach (string address in new[] { "file:///c:/secret", "http://example.invalid/v1", "https://key@example.invalid", "https://example.invalid/?key=secret" })
            {
                bool rejected = false; try { CompanionChat.Endpoint(address); } catch (ArgumentException) { rejected = true; } Require(rejected);
            }
            bool noModel = false; try { CompanionChat.ReplyAsync(client, new("https://example.invalid/v1"), "", [], "pet", default).GetAwaiter().GetResult(); } catch (ArgumentException) { noModel = true; } Require(noModel);
        });
        test("malformed or empty service responses fail without an invented reply", () =>
        {
            foreach (string content in new[] { "{}", "[]", "{\"choices\":null}", "{\"choices\":[{\"message\":null}]}", "{\"choices\":[{\"message\":{\"content\":\"\"}}]}" })
            {
                using var client = new HttpClient(new Handler((_, _) => Task.FromResult(Reply(content))));
                bool rejected = false; try { CompanionChat.ReplyAsync(client, new("https://example.invalid/v1", "mock"), "", [], "pet", default).GetAwaiter().GetResult(); } catch (InvalidDataException) { rejected = true; } Require(rejected);
            }
            using var failed = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("private server details") })));
            bool safeError = false; try { CompanionChat.ReplyAsync(failed, new("https://example.invalid/v1", "mock"), "", [], "pet", default).GetAwaiter().GetResult(); }
            catch (HttpRequestException ex) { safeError = ex.Message.Contains("401") && !ex.Message.Contains("private"); } Require(safeError);
        });
        test("stopping a remote reply cancels its transport", () =>
        {
            using var cancellation = new CancellationTokenSource();
            using var client = new HttpClient(new Handler(async (_, token) => { cancellation.Cancel(); await Task.Delay(5000, token); return Reply("{}"); }));
            bool stopped = false; try { CompanionChat.ReplyAsync(client, new("https://example.invalid/v1", "mock"), "", [], "pet", cancellation.Token).GetAwaiter().GetResult(); } catch (OperationCanceledException) { stopped = true; } Require(stopped);
        });
    }
    private static HttpResponseMessage Reply(string content) => new(HttpStatusCode.OK) { Content = new StringContent(content) };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken); }
}

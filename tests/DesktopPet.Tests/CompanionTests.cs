using System.Net;
using System.Text.Json;
using DesktopPet.Core;

internal static class CompanionTests
{
    public static void Run(Action<string, Action> test)
    {
        void Require(bool value, string reason) { if (!value) throw new Exception(reason); }
        test("companion state migrates legacy saves without inventing affection or time", () =>
        {
            var state = JsonSerializer.Deserialize<PetState>("{\"Character\":\"gpt\",\"Outfits\":{\"gpt\":\"sports\"},\"CheckIns\":[\"2026-10-06\"],\"Treasures\":[\"面包\"],\"ReadStories\":{\"cloud-post\":2}}")!;
            state.Validate(); Require(state.Character == "gpt" && state.Outfit == "sports" && state.CheckIns.Count == 1 && state.Treasures.Count == 1 && state.ReadStories["cloud-post"] == 2, "existing fields changed");
            Require(state.Companion("gpt").Score == 0 && state.Companion("gpt").Seconds == 0 && state.MakeupCards == 0 && !state.WorkModeEnabled, "invented migration progress");
            Require(state.CalendarView == "month" && state.AutoHide, "new defaults");
        });
        test("relationship limits, cooldowns, family separation and saved time", () =>
        {
            var state = new PetState(); var now = new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.FromHours(8));
            Require(state.Affect("gpt", 3, "check", now, "check", TimeSpan.FromMinutes(1)) == 3, "first award");
            Require(state.Affect("gpt", 3, "check", now.AddSeconds(30), "check", TimeSpan.FromMinutes(1)) == 0, "cooldown");
            Require(state.Affect("gpt", 100, "care", now, "care", TimeSpan.Zero) == 17 && state.Companion("gpt").Score == 20, "daily cap");
            Require(state.Companion("whale").Score == 0, "other family changed");
            for (int i = 1; i <= 5; i++) state.Affect("gpt", 20, "day", now.AddDays(i), "day", TimeSpan.Zero);
            Require(state.Companion("gpt").Score == 100, "upper bound"); state.Affect("gpt", -300, "rough", now, "rough", TimeSpan.Zero); Require(state.Companion("gpt").Score == -100, "lower bound");
            for (int i = 0; i < 300; i++) state.Accompany("whale", 1, now.AddSeconds(i));
            Require(state.Companion("whale").Seconds == 300 && state.Companion("whale").Score == 1 && state.Companion("gpt").Seconds == 0, "time per character");
            var restored = JsonSerializer.Deserialize<PetState>(JsonSerializer.Serialize(state))!; restored.Validate(); Require(restored.Companion("whale").Seconds == 300 && restored.Companion("gpt").Score == -100, "persistence");
        });
        test("makeup consumes exactly one card without relationship or time awards", () =>
        {
            var state = new PetState(); var today = new DateOnly(2026, 10, 7); var past = today.AddDays(-1);
            Require(!state.Makeup(past, today), "default has no cards"); state.MakeupCards = 1;
            Require(!state.Makeup(today, today) && !state.Makeup(today.AddDays(1), today), "future and today rejected");
            Require(state.Makeup(past, today) && state.MakeupCards == 0 && state.MakeupCheckIns.Contains("2026-10-06"), "past makeup");
            Require(!state.Makeup(past, today) && state.Companions.Count == 0 && state.Treasures.Count == 0, "duplicate or invented rewards");
        });
        test("calendar uses Monday weeks through year and leap-month boundaries", () =>
        {
            var date = new DateOnly(2027, 1, 1); var week = CompanionCalendar.Days(date, true);
            Require(week.Count == 7 && week[0] == new DateOnly(2026, 12, 28) && week[^1] == new DateOnly(2027, 1, 3), "year boundary");
            var month = CompanionCalendar.Days(new(2028, 2, 15), false); Require(month.Count % 7 == 0 && month.Count(x => x is not null) == 29 && month.Contains(new DateOnly(2028, 2, 29)), "leap month");
            Require(CompanionCalendar.Start(new(2026, 10, 11), true) == new DateOnly(2026, 10, 5), "Sunday start");
        });
        test("work clock waits the interval, skips missed alerts and preserves paused time", () =>
        {
            var clock = new WorkReminderClock(); Require(!clock.Tick(9999999), "default off"); clock.Start(0, 10);
            Require(!clock.Tick(599999) && clock.Tick(600000) && !clock.Tick(600001), "one per interval");
            Require(clock.Tick(2400010) && clock.Next == 3000000 && !clock.Tick(2400011), "no replay storm");
            clock.Pause(2500000); Require(!clock.Tick(9000000), "hidden paused"); clock.Resume(9000000); Require(clock.Next == 9500000, "remaining time");
            clock.Stop(); Require(!clock.Tick(10000000), "disabled stops"); clock.Start(10000000, 1); Require(clock.Next == 10060000, "interval reset");
        });
        test("eight identities have complete descriptions, distinct speech and six reminders", () =>
        {
            var answers = new HashSet<string>();
            foreach (string family in new[] { "whale", "gpt", "claude", "gemini", "grok", "qwen", "zhipu", "kimi" })
            {
                var p = CompanionPersonas.Profile(family); Require(p.GetProperty("about").GetArrayLength() >= 2 && p.GetProperty("likes").GetArrayLength() == 4, family + " profile");
                Require(CompanionPersonas.Family(CompanionPersonas.Text(family, "name")) == family, family + " identity");
                answers.Add(CompanionPersonas.Reply(family, [new("user", "你好")]));
                Require(Enumerable.Range(0, 6).Select(i => CompanionPersonas.Reminder(family, i)).Distinct().Count() == 6, family + " reminders");
                Require(CompanionPersonas.Prompt(family).Contains(p.GetProperty("likes")[0][0].GetString()!), family + " prompt matches profile");
                Require(CompanionPersonas.Reply(family, [new("user", "你喜欢什么")]) == p.GetProperty("favorite").GetString(), family + " likes reply");
            }
            Require(answers.Count == 8, "speech not distinct");
        });
        test("story context yields to the companion identity and likes on the next question", () =>
        {
            foreach (string family in new[] { "whale", "gpt", "claude", "gemini", "grok", "qwen", "zhipu", "kimi" })
            {
                var story = new List<ChatMessage> { new("user", "讲个故事"), new("assistant", "小云朵接过了小灯。") };
                Require(CompanionChat.LocalReply([.. story, new("user", "继续")], family).Contains("笑声"), "story continuation");
                Require(CompanionChat.LocalReply([.. story, new("user", "你是谁")], family).Contains(CompanionPersonas.Text(family, "name")), family + " display name");
                Require(CompanionChat.LocalReply([.. story, new("user", "你喜欢什么")], family) == CompanionPersonas.Profile(family).GetProperty("favorite").GetString(), family + " restored persona");
            }
        });
        test("all eight native providers send the official protocol and parse text only", () =>
        {
            foreach (var provider in CompanionProviders.All)
            {
                var options = new ChatOptions(provider.Base, "demo-model", provider.Id);
                using var client = new HttpClient(new Handler(async request =>
                {
                    using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync()); var json = payload.RootElement; string response;
                    Require(request.RequestUri!.Query.Length == 0, "key in URL");
                    if (provider.Protocol == "messages")
                    {
                        Require(request.RequestUri.AbsolutePath == "/v1/messages" && request.Headers.GetValues("x-api-key").Single() == "test-only-key" && request.Headers.GetValues("anthropic-version").Single() == "2023-06-01", "Claude headers and route");
                        Require(json.GetProperty("system").GetString()!.Contains("GPT") && json.GetProperty("max_tokens").GetInt32() > 0, "Claude system");
                        response = "{\"content\":[{\"type\":\"thinking\",\"thinking\":\"hidden\"},{\"type\":\"text\",\"text\":\"reply\"}]}";
                    }
                    else if (provider.Protocol == "gemini")
                    {
                        Require(request.RequestUri.AbsolutePath.EndsWith("/models/demo-model:generateContent") && request.Headers.GetValues("x-goog-api-key").Single() == "test-only-key", "Gemini route");
                        Require(json.GetProperty("contents")[1].GetProperty("role").GetString() == "model" && json.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString()!.Contains("GPT"), "Gemini prompt");
                        response = "{\"candidates\":[{\"content\":{\"parts\":[{\"thought\":true,\"text\":\"hidden\"},{\"text\":\"reply\"}]}}]}";
                    }
                    else
                    {
                        Require(request.RequestUri.AbsolutePath.EndsWith("/chat/completions") && request.Headers.Authorization?.Parameter == "test-only-key", "chat route");
                        Require(json.GetProperty("messages")[0].GetProperty("role").GetString() == (provider.Id == "openai" ? "developer" : "system"), "chat role");
                        response = "{\"choices\":[{\"message\":{\"content\":\"reply\"}}]}";
                    }
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) };
                }));
                var answer = CompanionChat.ReplyAsync(client, options, "test-only-key", [new("user", "hi"), new("assistant", "hello"), new("user", "你好")], "gpt", default).GetAwaiter().GetResult(); Require(answer == "reply", provider.Name + " response");
            }
        });
        test("provider validation, service errors and credentials do not silently fall back", () =>
        {
            void Invalid(Action call) { try { call(); } catch (ArgumentException) { return; } throw new Exception("invalid configuration accepted"); }
            Invalid(() => CompanionProviders.Endpoint("https://example.invalid/v1/messages", "openai"));
            Invalid(() => CompanionProviders.Endpoint("https://example.invalid/v1/chat/completions", "claude"));
            Invalid(() => CompanionProviders.Endpoint("https://example.invalid/v1beta/models/other:generateContent", "gemini", "model"));
            Invalid(() => CompanionProviders.Request(new("https://example.invalid/v1", "model", "openai"), "", [], "prompt"));
            try { CompanionProviders.Request(new("https://example.invalid/v1", "model", "openai"), "private\r\ncredential", [], "prompt"); throw new Exception("bad key accepted"); }
            catch (ArgumentException ex) { Require(!ex.Message.Contains("private"), "malformed key was echoed in error"); }
            Require(!JsonSerializer.Serialize(new ChatOptions("https://example.invalid", "m", "claude")).Contains("Key"), "key persistence");
            foreach (var provider in CompanionProviders.All)
            {
                using var json = JsonDocument.Parse("{\"error\":{\"message\":\"secret details\"}}");
                bool rejected = false; try { CompanionProviders.ResponseText(provider.Id, json.RootElement); } catch (InvalidDataException ex) { rejected = !ex.Message.Contains("secret"); } Require(rejected, provider.Id + " error");
            }
        });
    }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request); }
}

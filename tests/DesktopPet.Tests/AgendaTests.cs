using System.Net;
using System.Text.Json;
using DesktopPet.Core;

internal static class AgendaTests
{
    public static void Run(Action<string, Action> test)
    {
        void Require(bool ok, string why) { if (!ok) throw new Exception(why); }
        void Reject(Action action) { try { action(); } catch (Exception ex) when (ex is IOException or InvalidDataException) { return; } throw new Exception("invalid event accepted"); }
        var now = PetAgenda.AtLocal("2026-10-09T10:00", PetAgenda.Zone);
        AgendaDraft Draft(string title = "项目会议", string local = "2026-10-09T15:00", string repeat = "none", int lead = 10) => new(title, local, lead, repeat, PetAgenda.Zone, "笑着提醒：一起从容开始吧。");
        void WithStore(Action<AgendaStore, string> work)
        {
            string root = Path.Combine(Path.GetTempPath(), "agenda-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try { work(new AgendaStore(root), root); } finally { Directory.Delete(root, true); }
        }
        test("agenda strict AI contract rejects absent fields, invalid and past times", () =>
        {
            string Good(AgendaDraft d) => JsonSerializer.Serialize(new AgendaReply("event", "请确认", d), Json.Options);
            Require(PetAgenda.Parse(Good(Draft()), now).Event?.Title == "项目会议", "valid draft");
            foreach (string bad in new[] { "plain text", "{}", "{\"kind\":\"event\",\"reply\":\"ok\",\"event\":{}}", "null", "[]", Good(Draft(local: "2026-02-30T10:00")), Good(Draft(local: "2026-10-09T09:00")), Good(Draft(lead: 999)), Good(Draft(repeat: "monthly")), Good(Draft(local: "2026-10-10T15:00", repeat: "weekdays")) }) Reject(() => PetAgenda.Parse(bad, now));
            Require(PetAgenda.Parse("{\"kind\":\"clarify\",\"reply\":\"几点？\",\"event\":null}", now).Event is null, "clarify never draft");
            Reject(() => PetAgenda.Validate(Draft() with { Zone = "other zone" }, now));
        });
        test("agenda confirmation, duplicates, edits and isolated durable storage", () => WithStore((db, root) =>
        {
            var draft = PetAgenda.Validate(Draft(), now); Require(!File.Exists(Path.Combine(root, "agenda.json")), "validation saved without consent");
            var e = db.Put(draft.Draft, "gpt", now); Require(new AgendaStore(root).Events.Single() == e, "reload");
            Reject(() => db.Put(Draft(), "whale", now));
            var edit = db.Put(Draft("复核会议"), "kimi", now, e.Id); Require(edit.Id == e.Id && edit.Family == "gpt", "owner or id changed");
            Require(!File.ReadAllText(Path.Combine(root, "agenda.json")).Contains("apiKey") && !File.Exists(Path.Combine(root, "state.json")), "calendar polluted other data");
            db.Cancel(edit); Reject(() => db.Put(Draft(), "gpt", now, e.Id));
        }));
        test("agenda exact deadline, unread catchup and duplicate completion protection", () => WithStore((db, root) =>
        {
            var e = db.Put(Draft(), "gpt", now);
            Require(db.Due(e.RemindAt.AddTicks(-1)).Count == 0 && db.Due(e.RemindAt).Count == 1, "deadline");
            Require(db.Deliver(e, e.RemindAt) && !db.Deliver(e, e.RemindAt), "duplicate delivery");
            var restored = new AgendaStore(root); Require(restored.Due(now).Count == 1, "clock rollback lost unread");
            Require(restored.Finish(e, now.AddDays(5)) && !restored.Finish(e, now.AddDays(5)), "finish twice");
            Require(new AgendaStore(root).Events.Single().Status == "done", "done persistence");
        }));
        test("agenda snooze survives restart and retains original event time", () => WithStore((db, root) =>
        {
            var e = db.Put(Draft(), "claude", now); db.Deliver(e, e.RemindAt); Require(db.Snooze(e, e.RemindAt), "snooze");
            var restored = new AgendaStore(root); var snoozed = restored.Events.Single();
            Require(snoozed.StartAt == e.StartAt && snoozed.RemindAt == e.RemindAt.AddMinutes(10) && restored.Due(e.RemindAt).Count == 0, "snoozed deadline");
            Require(restored.Due(snoozed.RemindAt).Count == 1 && !restored.Snooze(e, e.RemindAt), "boundary and duplicate");
        }));
        test("agenda repeats skip missed days, weekends and old callbacks", () => WithStore((db, _) =>
        {
            foreach (string repeat in new[] { "daily", "weekdays", "weekly" })
            {
                var e = db.Put(Draft(repeat, repeat: repeat), "qwen", now); db.Deliver(e, e.RemindAt);
                var after = PetAgenda.AtLocal("2026-11-08T16:00", PetAgenda.Zone); Require(db.Finish(e, after), "first completion");
                var next = db.Events.Single(x => x.Id == e.Id); Require(next.RemindAt > after && next.StartAt.ToLocalTime().Hour == 15, "repeat cadence");
                if (repeat == "weekdays") Require(next.StartAt.ToLocalTime().DayOfWeek == DayOfWeek.Monday, "weekend");
                if (repeat == "weekly") Require(next.StartAt.ToLocalTime().DayOfWeek == DayOfWeek.Friday, "weekly day");
                Require(!db.Finish(e, after) && !db.Snooze(e, after) && !db.Cancel(e), "stale occurrence touched next");
            }
        }));
        test("agenda DST ambiguity and changed timezones require explicit correction", () =>
        {
            Reject(() => PetAgenda.AtLocal("2026-03-08T02:30", "America/New_York"));
            Reject(() => PetAgenda.AtLocal("2026-11-01T01:30", "America/New_York"));
            var e = new AgendaEntry(Guid.NewGuid().ToString(), "test", now, 0, "daily", "America/New_York", "gpt", "", now);
            if (!PetAgenda.SameZone(e.Zone, PetAgenda.Zone)) Reject(() => PetAgenda.Next(e, now));
        });
        test("agenda failed disk writes leave memory and the previous file unchanged", () => WithStore((db, root) =>
        {
            var e = db.Put(Draft(), "gpt", now); byte[] before = File.ReadAllBytes(Path.Combine(root, "agenda.json"));
            Directory.CreateDirectory(Path.Combine(root, "agenda.json.bak"));
            Reject(() => db.Deliver(e, e.RemindAt));
            Require(db.Events.Single().Status == "scheduled" && File.ReadAllBytes(Path.Combine(root, "agenda.json")).SequenceEqual(before), "failed save advanced memory or destroyed file");
        }));
        test("agenda corrupt storage is preserved and cannot be overwritten", () => WithStore((_, root) =>
        {
            var path = Path.Combine(root, "agenda.json"); File.WriteAllText(path, "{broken"); var db = new AgendaStore(root);
            Require(db.LoadError is not null, "corruption ignored"); Reject(() => db.Put(Draft(), "gpt", now)); Require(File.ReadAllText(path) == "{broken", "destroyed corrupt original");
        }));
        test("agenda eight native providers carry the JSON contract without provider-specific parameters", () =>
        {
            foreach (var spec in CompanionProviders.All)
            {
                using var client = new HttpClient(new Handler(async request =>
                {
                    string body = await request.Content!.ReadAsStringAsync(); using var json = JsonDocument.Parse(body); var r = json.RootElement;
                    string system = spec.Protocol == "messages" ? r.GetProperty("system").GetString()! : spec.Protocol == "gemini" ? r.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString()! : r.GetProperty("messages")[0].GetProperty("content").GetString()!;
                    Require(system.Contains("只输出一个 JSON 对象") && system.Contains(PetAgenda.Zone) && system.Contains("GPT") && !body.Contains("response_format"), "contract or identity missing " + spec.Id);
                    string result = "{\"kind\":\"clarify\",\"reply\":\"明天几点提醒你？\",\"event\":null}";
                    object payload = spec.Protocol == "messages" ? new { content = new[] { new { type = "text", text = result } } } : spec.Protocol == "gemini" ? (object)new { candidates = new[] { new { content = new { parts = new[] { new { text = result } } } } } } : new { choices = new[] { new { message = new { content = result } } } };
                    return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(payload)) };
                }));
                var clock = System.Diagnostics.Stopwatch.StartNew(); var reply = PetAgenda.ReplyAsync(client, new(spec.Base, "test-model", spec.Id), "test-only-key", [new("user", "明天提醒我")], "gpt", 0, default).GetAwaiter().GetResult();
                Require(reply.Kind == "clarify" && reply.Event is null && clock.ElapsedMilliseconds >= 980, "thinking or clarify " + spec.Id);
            }
        });
        test("agenda offline requests, malformed responses and cancellation never produce a draft", () =>
        {
            using var offline = new HttpClient(new Handler(_ => throw new Exception("offline called network")));
            var local = PetAgenda.ReplyAsync(offline, new(), "", [new("user", "明天提醒我开会")], "gpt", 0, default).GetAwaiter().GetResult(); Require(local.Event is null && local.Reply.Contains("接入 AI"), "offline fake parse");
            var distant = PetAgenda.ReplyAsync(offline, new(), "", [new("user", "你好")], "gpt", -100, default).GetAwaiter().GetResult();
            Require(distant.Reply == CompanionPersonas.Reply("gpt", [new("user", "你好")], -100), "offline affection-aware tone changed");
            using var client = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"bad json\"}}]}") })));
            Reject(() => PetAgenda.ReplyAsync(client, new("https://example.invalid/v1", "test-model"), "test-only-key", [new("user", "记日程")], "gpt", 0, default).GetAwaiter().GetResult());
            using var cancel = new CancellationTokenSource(50); bool stopped = false;
            try { PetAgenda.ReplyAsync(offline, new(), "", [new("user", "你好")], "gpt", 0, cancel.Token).GetAwaiter().GetResult(); } catch (OperationCanceledException) { stopped = true; }
            Require(stopped, "cancel failed");
        });
    }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request); }
}

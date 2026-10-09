using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core;

namespace DesktopPet.App;

/// <summary>WPF and real loopback HTTP only; never reads daily user data or a real API credential.</summary>
internal static class AgendaVerification
{
    public static async Task Run(PetWindow pet, string output, bool reload)
    {
        Directory.CreateDirectory(output); var checks = new List<string>(); string report = Path.Combine(output, reload ? "agenda-reload-check.txt" : "agenda-check.txt");
        void Require(bool value, string why) { if (!value) throw new InvalidOperationException(why); checks.Add("PASS " + why); File.WriteAllLines(report, checks); }
        IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var c = VisualTreeHelper.GetChild(root, i); if (c is T found) yield return found; foreach (var n in Find<T>(c)) yield return n; }
        }
        void Click(FrameworkElement root, string name)
        { root.UpdateLayout(); var b = Find<Button>(root).Single(x => AutomationProperties.GetName(x) == name); Require(b.IsEnabled, "enabled " + name); b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); root.UpdateLayout(); }
        void Fill(string name, string text) { pet.Chat.UpdateLayout(); Find<TextBox>(pet.Chat).Single(t => AutomationProperties.GetName(t) == name).Text = text; }
        async Task Until(Func<bool> condition, string why)
        { var time = Stopwatch.StartNew(); while (!condition()) { if (time.Elapsed.TotalSeconds > 8) throw new TimeoutException(why); await Task.Delay(45); } }
        void Capture(FrameworkElement view, string name)
        {
            view.UpdateLayout(); var image = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32); image.Render(view);
            PetWindow.SaveCompanionPhoto(image, Path.Combine(output, name + ".png"));
        }
        pet.IsHitTestVisible = false; pet.State.Wander = false; pet.State.AutoHide = false; pet.ConfigureWork(false, 10); pet.StopInteraction();
        if (reload)
        {
            await Until(() => pet.AgendaNotice is not null, "startup unread catchup");
            Require(pet.Agenda.Events.Single(e => e.Title == "重启补提醒").Status == "notified", "separate process reload retains unread occurrence");
            Require(Find<TextBlock>(pet.AgendaNotice!).Any(t => t.Text == "重启补提醒"), "startup automatically shows saved unread event");
            Click(pet.AgendaNotice!, "已完成"); Require(new AgendaStore(output).Events.Single(e => e.Title == "重启补提醒").Status == "done", "completion survives second process");
            File.AppendAllText(report, $"PASS {checks.Count} native agenda reload checks.\n"); return;
        }
        Require(pet.Agenda.Events.Count == 0, "new isolated calendar empty");
        pet.SelectCharacter("gpt"); pet.OpenAgenda(); var settings = Application.Current.Windows.OfType<SettingsWindow>().Single(); await Task.Delay(100);
        Require(Find<Button>(settings).Any(b => AutomationProperties.GetName(b) == "宠物行事历"), "native sidebar has agenda");
        Require(Find<Button>(settings).Count(b => AutomationProperties.GetName(b).StartsWith("日程 ")) == 7, "native week calendar");
        Click(settings, "月历"); Require(Find<Button>(settings).Count(b => AutomationProperties.GetName(b).StartsWith("日程 ")) == DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month), "native month calendar");
        Click(settings, "周历"); Capture(settings, "01-agenda-empty");
        pet.Chat.SaveOptions(new(), ""); pet.OpenChat(); var clock = Stopwatch.StartNew(); var off = pet.Chat.SendText("明天提醒我开会"); await Task.Delay(200);
        Require(pet.CurrentAction == "thinking", "offline reminder request visibly thinks"); await off;
        Require(clock.ElapsedMilliseconds >= 980 && pet.Chat.ReplyText.Contains("接入 AI") && !pet.Chat.HasAgendaDraft && pet.Agenda.Events.Count == 0, "offline is explicit, no invented saved event");
        using var server = new Loopback(); Require(pet.Chat.SaveOptions(new(server.Address, "mock-model", "openai"), "test-only-key") is null, "loopback API configured");
        var draft = new AgendaDraft("复核会议", PetAgenda.Local(DateTimeOffset.Now.AddHours(3)), 10, "none", PetAgenda.Zone, "微笑着提醒你，我们一起从容开始吧。");
        server.Content = JsonSerializer.Serialize(new AgendaReply("clarify", "你想让我几点提醒？", null), Json.Options);
        await pet.Chat.SendText("明天提醒我开会"); Require(!pet.Chat.HasAgendaDraft && pet.Chat.ReplyText.Contains("几点") && pet.Agenda.Events.Count == 0, "AI clarification stays in same chat");
        server.Content = JsonSerializer.Serialize(new AgendaReply("event", "看看这张日程卡的时间对不对。", draft), Json.Options);
        clock.Restart(); var waiting = pet.Chat.SendText("下午开会，提前十分钟"); await Task.Delay(180); Require(!pet.Chat.HasAgendaDraft && pet.CurrentAction == "thinking", "API minimum thinking before card"); await waiting;
        Require(clock.ElapsedMilliseconds >= 980 && pet.Chat.HasAgendaDraft && pet.Agenda.Events.Count == 0 && Window.GetWindow(pet.Chat) == pet, "editable draft embedded above pet; not saved");
        await Task.Delay(180); pet.LayoutChat(true); pet.UpdateLayout();
        Require(Canvas.GetTop(pet.Chat) + pet.Chat.ActualHeight <= 468 - pet.State.Size * .875 + 2, "confirmation stays above pet instead of covering face");
        Require(Find<Button>(pet.Chat).Single(b => AutomationProperties.GetName(b) == "确认保存日程").IsVisible, "confirmation button visible in compact card");
        Capture(pet, "02-inline-confirm");
        Fill("日期时间 · yyyy-MM-dd HH:mm", "2020-01-01 10:00"); Click(pet.Chat, "确认保存日程"); Require(pet.Chat.HasAgendaDraft && pet.Agenda.Events.Count == 0, "invalid edited date stays unsaved");
        Fill("日期时间 · yyyy-MM-dd HH:mm", draft.StartLocal.Replace('T', ' ')); Fill("事情", "确认后的会议"); Click(pet.Chat, "确认保存日程");
        Require(!pet.Chat.HasAgendaDraft && pet.Agenda.Events.Single().Title == "确认后的会议" && new AgendaStore(output).Events.Count == 1, "click commits exactly once to disk");
        Require(!File.ReadAllText(Path.Combine(output, "agenda.json")).Contains("test-only-key"), "no credential in calendar");
        pet.OpenAgenda(); Capture(settings, "03-calendar-saved");
        var original = pet.Agenda.Events.Single(); byte[] before = File.ReadAllBytes(Path.Combine(output, "agenda.json"));
        pet.ShowAgendaReminder(original, true); Capture(pet.AgendaNotice!, "04-preview-q");
        Require(!pet.AgendaNotice!.ShowActivated && pet.AgendaNotice.Topmost, "reminder appears without activation"); Click(pet.AgendaNotice, "结束预览");
        Require(File.ReadAllBytes(Path.Combine(output, "agenda.json")).SequenceEqual(before), "preview never delivers or completes");
        pet.SelectCharacter("kimi"); pet.EditAgenda(original); Require(pet.Character.FamilyId == "gpt" && pet.Chat.HasAgendaDraft, "editing restores responsible family");
        Click(pet.Chat, "暂不保存日程"); Require(pet.Agenda.Events.Single() == original, "edit cancel preserves event");
        foreach (int size in new[] { 120, 280 })
        {
            pet.State.Size = size; pet.ApplySettings(); pet.EditAgenda(original); await Task.Delay(120); pet.UpdateLayout();
            double head = (double)typeof(PetWindow).GetProperty("PetTop", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pet)!;
            Capture(pet, "inline-size-" + size);
            Require(Canvas.GetTop(pet.Chat) + pet.Chat.ActualHeight <= head + 2, $"inline card size {size}: top={Canvas.GetTop(pet.Chat):F1}, height={pet.Chat.ActualHeight:F1}, head={head:F1}, windowTop={pet.Top:F1}, area={pet.WorkArea}");
            Click(pet.Chat, "暂不保存日程");
        }
        pet.State.Size = 200; pet.ApplySettings(); pet.OpenChat();
        server.Content = "malformed JSON"; await pet.Chat.SendText("错误响应"); Require(!pet.Chat.HasAgendaDraft && pet.Chat.ReplyText.Contains("没有保存"), "malformed model response stays safe");
        server.Content = JsonSerializer.Serialize(new AgendaReply("event", "请确认", draft), Json.Options); server.Delay = 500;
        var cancelled = pet.Chat.SendText("取消这次请求"); await Task.Delay(60); pet.SelectCharacter("whale"); await cancelled;
        Require(!pet.Chat.HasAgendaDraft && pet.Agenda.Events.Count == 1, "switching pet cancels stale card"); server.Delay = 0;
        pet.OpenChat(); await pet.Chat.SendText("再整理一件"); Require(pet.Chat.HasAgendaDraft, "new family can propose"); pet.StopInteraction(); Require(!pet.Chat.HasAgendaDraft, "closing chat discards unconfirmed draft");
        // All unchanged art combinations must keep the responsible pet's current outfit smile.
        foreach (string family in Catalog.BuiltInFamilies)
        foreach (string style in CharacterStyles.All)
        foreach (string outfit in Catalog.BuiltInOutfits)
        {
            string id = Catalog.VariantId(family, style); pet.SelectCharacter(id); pet.State.Outfits[id] = outfit; pet.ApplySettings();
            var e = original with { Family = family }; pet.ShowAgendaReminder(e, true); var c = pet.Catalog.Find(id); var frame = c.Resolve(outfit, "happy", 0);
            Require(Find<Image>(pet.AgendaNotice!).Any(i => ReferenceEquals(i.Source, pet.Art.Frame(c, frame.Sprite, frame.Frame)) && i.Stretch == Stretch.Uniform), $"smile keeps identity, proportions and outfit {id}/{outfit}");
            if (family == "gpt" && style == CharacterStyles.Realistic && outfit == "sports") Capture(pet.AgendaNotice!, "05-preview-realistic");
            Click(pet.AgendaNotice!, "结束预览");
        }
        settings.Close(); pet.Chat.SaveOptions(new(), ""); pet.StopInteraction();
        var second = pet.Agenda.Put(draft with { Title = "紧接着的日程", StartLocal = PetAgenda.Local(original.StartAt.AddMinutes(1)) }, "kimi", DateTimeOffset.UtcNow);
        pet.AgendaNow = () => second.StartAt.AddMinutes(1); pet.ToggleVisible(); Require(!pet.IsVisible, "pet hidden before deadline");
        await Until(() => pet.AgendaNotice is not null, "independent timer delivers while hidden");
        Require(pet.Agenda.Events.Single(e => e.Id == original.Id).Status == "notified" && !pet.IsVisible, "real dispatcher delivers without unhiding or moving pet");
        var notice = pet.AgendaNotice; pet.PollAgenda(); Require(ReferenceEquals(notice, pet.AgendaNotice), "poll deduplicates open reminder");
        Capture(pet.AgendaNotice!, "06-hidden-due"); Click(pet.AgendaNotice!, "10 分钟后");
        Require(pet.Agenda.Events.Single(e => e.Id == original.Id).SnoozeUntil == pet.AgendaNow().AddMinutes(10), "snooze persists");
        Require(Find<TextBlock>(pet.AgendaNotice!).Any(t => t.Text == second.Title), "next queued event shown immediately"); Click(pet.AgendaNotice!, "已完成");
        Require(pet.AgendaNotice is null && new AgendaStore(output).Events.Single(e => e.Id == second.Id).Status == "done", "completion durable and queue drained");
        var wakeAt = pet.AgendaNow().AddMinutes(11); pet.AgendaNow = () => wakeAt;
        typeof(PetWindow).GetMethod("QueueAgendaCheck", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(pet, null);
        await Until(() => pet.AgendaNotice is not null, "resume/clock change catchup path"); Click(pet.AgendaNotice!, "已完成");
        Require(pet.Agenda.Due(wakeAt).Count == 0, "resume catchup completes only outstanding occurrence");
        pet.ToggleVisible(); pet.AgendaNow = () => DateTimeOffset.UtcNow; pet.SelectCharacter("gpt"); pet.OpenChat();
        var final = pet.Agenda.Put(draft with { Title = "重启补提醒" }, "gpt", DateTimeOffset.UtcNow); pet.Agenda.Deliver(final, final.RemindAt);
        // Leave one durable unread record for a genuinely new process to find.
        Require(new AgendaStore(output).Events.Single(e => e.Id == final.Id).Status == "notified", "restart fixture is durable unread event");
        Require(server.Requests >= 5 && server.Contracts == server.Requests, "all loopback requests have agenda contract");
        File.AppendAllText(report, $"PASS {checks.Count} native agenda checks; real paid API calls: 0.\n");
    }
    private sealed class Loopback : IDisposable
    {
        private readonly HttpListener listener = new();
        public string Address { get; }
        public string Content { get; set; } = "";
        public int Delay { get; set; }
        public int Requests { get; private set; }
        public int Contracts { get; private set; }
        public Loopback()
        {
            var port = new TcpListener(IPAddress.Loopback, 0); port.Start(); int number = ((IPEndPoint)port.LocalEndpoint).Port; port.Stop();
            Address = $"http://localhost:{number}/v1"; listener.Prefixes.Add($"http://localhost:{number}/"); listener.Start(); _ = Serve();
        }
        private async Task Serve()
        {
            while (listener.IsListening)
            {
                try
                {
                    var c = await listener.GetContextAsync(); using var reader = new StreamReader(c.Request.InputStream); string body = await reader.ReadToEndAsync(); Requests++;
                    using var json = JsonDocument.Parse(body); if (json.RootElement.GetProperty("messages")[0].GetProperty("content").GetString()!.Contains("只输出一个 JSON 对象")) Contracts++;
                    string reply = Content; int wait = Delay; if (wait > 0) await Task.Delay(wait);
                    byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = reply } } } }));
                    c.Response.ContentType = "application/json"; c.Response.ContentLength64 = bytes.Length; await c.Response.OutputStream.WriteAsync(bytes); c.Response.Close();
                }
                catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or IOException) { if (!listener.IsListening) return; }
            }
        }
        public void Dispose() { listener.Close(); }
    }
}

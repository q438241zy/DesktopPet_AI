using System.Text.Json;
using DesktopPet.Core;

internal static class MembershipTests
{
    internal static void Run(Action<string, Action> test)
    {
        const string password = "a gentle cloud password 2026";
        string Temporary() { var root = Path.Combine(Path.GetTempPath(), "DesktopPet-member-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); return root; }
        void Check(bool pass) { if (!pass) throw new Exception("Membership assertion failed."); }
        void InStore(Action<string, LocalAccountService> run)
        { string root = Temporary(); try { run(root, new LocalAccountService(root)); } finally { Directory.Delete(root, true); } }
        test("testing opens five tiers and guests while planned chat starts at gold", () =>
        {
            foreach (var tier in Membership.Tiers) Check(Membership.Access("chat", tier).Allowed);
            Check(Membership.TestingOpen && Membership.Access("chat", null).Allowed);
            Check(Membership.Tiers.Count == 5 && Membership.Name(Membership.Tiers[0]) == "黑金" && Membership.Name(Membership.Tiers[^1]) == "黄铜");
            foreach (var tier in Membership.Tiers) Check(Membership.PlannedAccess("chat", tier).Allowed == (tier >= MembershipTier.Gold));
            Check(!Membership.PlannedAccess("chat", null).Allowed && !Membership.PlannedAccess("chat", (MembershipTier)90).Allowed);
        });
        test("unannounced pose grades do not remove existing actions", () =>
        {
            foreach (MembershipTier? tier in Membership.Tiers.Select(t => (MembershipTier?)t).Append(null))
            foreach (string action in new[] { "headpat", "poke", "tickle", "snack", "walk", "dance", "praise", "comfort", "lullaby", "jump", "rest", "pickup" })
                Check(Membership.Access(action, tier).Allowed);
        });
        test("registration defaults to brass and persists a salted hash without passwords", () => InStore((root, service) =>
        {
            Check(service.RegisterAsync("cloud_one", "云朵", password, password).GetAwaiter().GetResult().Success);
            Check(service.CurrentAccount is { Tier: MembershipTier.Brass });
            string saved = File.ReadAllText(Path.Combine(root, "member-accounts.json")); Check(!saved.Contains(password));
            Check(service.RegisterAsync("cloud_two", "第二朵云", password, password).GetAwaiter().GetResult().Success);
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "member-accounts.json")));
            var rows = doc.RootElement.GetProperty("accounts");
            Check(rows[0].GetProperty("salt").GetString() != rows[1].GetProperty("salt").GetString());
            Check(rows[0].GetProperty("passwordHash").GetString() != rows[1].GetProperty("passwordHash").GetString());
            Check(rows[0].GetProperty("iterations").GetInt32() == 600000);
        }));
        test("registration rejects invalid fields and duplicate case-insensitive accounts", () => InStore((root, service) =>
        {
            foreach (var (name, nick, pass, confirm) in new[] { ("../bad", "云", password, password), ("good", "", password, password), ("good", "云", "short", "short"), ("good", "云", password, password + "x") })
                Check(!service.RegisterAsync(name, nick, pass, confirm).GetAwaiter().GetResult().Success);
            Check(!File.Exists(Path.Combine(root, "member-accounts.json")) && service.CurrentAccount is null);
            Check(service.RegisterAsync("Cloud_One", "云朵", password, password).GetAwaiter().GetResult().Success);
            Check(!service.RegisterAsync("cloud_ONE", "另一个", password, password).GetAwaiter().GetResult().Success);
        }));
        test("restart requires login and logout preserves credentials and pet progress", () => InStore((root, service) =>
        {
            var state = new PetState { Character = "gpt", Outfits = new() { ["gpt"] = "wedding" }, CheckIns = ["2026-10-02"], Treasures = ["面包"] };
            new StateStore(root).Save(state); string original = File.ReadAllText(Path.Combine(root, "state.json"));
            Check(service.RegisterAsync("cloud_one", "云朵", password, password).GetAwaiter().GetResult().Success);
            var restarted = new LocalAccountService(root); Check(restarted.CurrentAccount is null);
            Check(!restarted.LoginAsync("cloud_one", password + "x").GetAwaiter().GetResult().Success);
            Check(!restarted.LoginAsync("unknown", password).GetAwaiter().GetResult().Success);
            Check(restarted.CurrentAccount is null && restarted.LoginAsync("CLOUD_ONE", password).GetAwaiter().GetResult().Success);
            restarted.Logout(); Check(restarted.CurrentAccount is null && File.ReadAllText(Path.Combine(root, "state.json")) == original);
            Check(restarted.LoginAsync("cloud_one", password).GetAwaiter().GetResult().Success);
        }));
        test("corrupt account data is never overwritten by login or registration", () => InStore((root, service) =>
        {
            string path = Path.Combine(root, "member-accounts.json"); File.WriteAllText(path, "{broken-member-data");
            Check(!service.RegisterAsync("cloud_one", "云朵", password, password).GetAwaiter().GetResult().Success);
            Check(!service.LoginAsync("cloud_one", password).GetAwaiter().GetResult().Success);
            Check(File.ReadAllText(path) == "{broken-member-data" && service.CurrentAccount is null);
        }));
        test("simultaneous registration cannot create duplicate accounts", () => InStore((root, service) =>
        {
            var results = Task.WhenAll(service.RegisterAsync("cloud_one", "云朵", password, password), service.RegisterAsync("CLOUD_ONE", "云朵", password, password)).GetAwaiter().GetResult();
            Check(results.Count(r => r.Success) == 1);
        }));
        test("repeated wrong passwords are throttled and recover after a minute", () =>
        {
            string root = Temporary(); var clock = new ManualTime(); var service = new LocalAccountService(root, clock);
            try
            {
                Check(service.RegisterAsync("cloud_one", "云朵", password, password).GetAwaiter().GetResult().Success); service.Logout();
                for (int i = 0; i < 5; i++) Check(!service.LoginAsync("cloud_one", "wrong").GetAwaiter().GetResult().Success);
                Check(!service.LoginAsync("cloud_one", password).GetAwaiter().GetResult().Success);
                clock.Now = clock.Now.AddMinutes(1); Check(service.LoginAsync("cloud_one", password).GetAwaiter().GetResult().Success);
            }
            finally { Directory.Delete(root, true); }
        });
    }
    private sealed class ManualTime : TimeProvider
    { public DateTimeOffset Now { get; set; } = DateTimeOffset.Parse("2026-10-02T00:00:00Z"); public override DateTimeOffset GetUtcNow() => Now; }
}

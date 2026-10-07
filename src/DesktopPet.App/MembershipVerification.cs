using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core;

namespace DesktopPet.App;

internal static class MembershipVerification
{
    // Test levels exist only in this verifier and are never written to the account store.
    internal static IAccountService CreateFixture(string root) => new Fixture(root);
    private sealed class Fixture : IAccountService
    {
        private readonly LocalAccountService local;
        private bool fullAccess = true;
        private MembershipTier? tier;
        private readonly MemberAccount proof = new("native-verification", "verification", "验证伙伴", MembershipTier.BlackGold, DateTimeOffset.UtcNow);
        public Fixture(string root) { local = new(root); local.Changed += () => Changed?.Invoke(); }
        public MemberAccount? CurrentAccount => fullAccess ? proof : local.CurrentAccount is { } account ? account with { Tier = tier ?? account.Tier } : null;
        public event Action? Changed;
        public Task<AccountResult> RegisterAsync(string username, string nickname, string password, string confirmation) => local.RegisterAsync(username, nickname, password, confirmation);
        public Task<AccountResult> LoginAsync(string username, string password) => local.LoginAsync(username, password);
        public void Logout() { fullAccess = false; tier = null; local.Logout(); }
        internal void SetTier(MembershipTier value) { tier = value; Changed?.Invoke(); }
        internal void UseFullAccess() { fullAccess = true; tier = null; Changed?.Invoke(); }
    }

    internal static async Task Run(PetWindow pet, string output)
    {
        Directory.CreateDirectory(output); var checks = new List<string>();
        void Require(bool value, string name) { if (!value) throw new InvalidOperationException(name); checks.Add("PASS " + name); }
        IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T result) yield return result;
                foreach (var item in Find<T>(child)) yield return item;
            }
        }
        async Task Until(Func<bool> predicate)
        { var elapsed = Stopwatch.StartNew(); while (!predicate()) { if (elapsed.Elapsed.TotalSeconds > 10) throw new TimeoutException("Member UI did not finish."); await Task.Delay(25); } }
        void Capture(FrameworkElement element, string name)
        {
            element.UpdateLayout(); var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth), (int)Math.Ceiling(element.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(element); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(Path.Combine(output, name + ".png")); encoder.Save(stream);
        }
        var fixture = pet.Accounts as Fixture ?? throw new InvalidOperationException("Membership checks need an isolated provider.");
        pet.IsHitTestVisible = false; pet.State.Wander = false; pet.ApplySettings(); fixture.Logout();
        pet.OpenSettings(); var window = Application.Current.Windows.OfType<SettingsWindow>().Single(); window.IsHitTestVisible = false; window.ShowActivated = false; window.UpdateLayout();
        void Click(string name)
        { var button = Find<Button>(window).Single(b => AutomationProperties.GetName(b) == name); button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout(); }
        TextBox Input(string name) => Find<TextBox>(window).Single(b => AutomationProperties.GetName(b) == name);
        PasswordBox Secret(string name) => Find<PasswordBox>(window).Single(b => AutomationProperties.GetName(b) == name);
        string Message() => Find<TextBlock>(window).Single(b => AutomationProperties.GetName(b) == "账号提示").Text;
        string savedProgress = JsonSerializer.Serialize(new { pet.State.Character, pet.State.Outfits, pet.State.CheckIns, pet.State.Treasures, pet.State.ReadStories }, Json.Options);
        Require(!Find<Button>(window).Any(b => AutomationProperties.GetName(b) == "注册 / 登录"), "home no longer contains membership upsell");
        Capture(window, "membership-home"); Click("会员中心");
        Require(Find<Button>(window).Any(b => AutomationProperties.GetName(b) == "登录分页") && !Find<Border>(window).Any(b => AutomationProperties.GetName(b).Contains("会员权益")), "member center is a login/register form");
        Click("注册分页"); Capture(window, "membership-register");
        const string password = "native cloud account password 2026";
        string username = "cloud_" + Guid.NewGuid().ToString("N")[..10];
        Input("注册账号").Text = username; Input("注册昵称").Text = "小云朵"; Secret("注册密码").Password = password; Secret("确认密码").Password = "wrong";
        Click("注册并登录"); await Until(() => Message() != "正在注册…");
        Require(pet.Accounts.CurrentAccount is null && Message().Contains("不一致"), "mismatched confirmation is rejected by the real form");
        Secret("注册密码").Password = password; Secret("确认密码").Password = password; Click("注册并登录"); await Until(() => pet.Accounts.CurrentAccount is not null);
        window.UpdateLayout(); Require(pet.Accounts.CurrentAccount is { Tier: MembershipTier.Brass, Nickname: "小云朵" }, "actual registration logs in as brass");
        Require(JsonSerializer.Serialize(new { pet.State.Character, pet.State.Outfits, pet.State.CheckIns, pet.State.Treasures, pet.State.ReadStories }, Json.Options) == savedProgress, "registration preserves character, outfit, check-ins and collectibles");
        Require(!File.ReadAllText(Path.Combine(output, "member-accounts.json")).Contains(password), "registered password is not stored as plaintext");
        Capture(window, "membership-brass");
        Click("退出登录"); Click("登录分页"); Input("登录账号").Text = username; Secret("登录密码").Password = "incorrect"; Click("登录");
        await Until(() => Message() != "正在登录…"); Require(pet.Accounts.CurrentAccount is null && Message().Contains("不正确"), "wrong password does not authenticate");
        Secret("登录密码").Password = password; Click("登录"); await Until(() => pet.Accounts.CurrentAccount is not null);
        Require(pet.Accounts.CurrentAccount!.Username == username, "actual login reopens the registered account");
        foreach (var tier in Membership.Tiers)
        {
            fixture.SetTier(tier); bool allowed = Membership.TestingOpen || tier >= MembershipTier.Gold;
            pet.ShowMenu(); pet.UpdateLayout();
            var chat = Find<Button>(pet).Single(b => AutomationProperties.GetName(b) == "聊天");
            Require(Find<MemberLock>(chat).Any() == !allowed, tier + ": root chat uses a cute lock only when gated");
            Require(allowed || AutomationProperties.GetHelpText(chat).Contains("黄金"), tier + ": keyboard and accessible names expose the requirement");
            if (tier == MembershipTier.Brass) Capture((FrameworkElement)pet.Content, "membership-test-open-circle");
            foreach (string id in new[] { "whale", "deepseek-adult" })
            foreach (string outfit in Catalog.BuiltInOutfits)
            {
                pet.SelectCharacter(id); pet.State.Outfits[id] = outfit; pet.ApplySettings();
                pet.ShowMenu(); pet.UpdateLayout();
                Find<Button>(pet).Single(b => AutomationProperties.GetName(b) == "聊天").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require((pet.ActiveChat is not null) == allowed, $"{tier}/{id}/{outfit}: real click respects membership");
                pet.StopInteraction(); pet.OpenChat(); Require((pet.ActiveChat is not null) == allowed, $"{tier}/{id}/{outfit}: direct chat cannot bypass membership");
                Require(pet.State.Character == id && pet.State.Outfit == outfit, $"{tier}/{id}/{outfit}: access does not change appearance");
                pet.StopInteraction();
            }
            if (allowed)
            {
                pet.OpenChat(); var response = pet.Chat.SendText("你好"); await Task.Delay(250);
                Require(pet.Chat.IsThinking && pet.CurrentAction == "thinking", tier + ": entitled chat still thinks for a second");
                await response; Require(pet.Chat.History.Count >= 2 && pet.Chat.ReplyText.Length > 4, tier + ": entitled local chat returns a real reply");
                pet.StopInteraction();
            }
            else
            {
                int previous = pet.Chat.History.Count; await pet.Chat.SendText("不能绕过权限");
                Require(pet.Chat.History.Count == previous && !pet.Chat.IsThinking, tier + ": denied submit performs no chat work");
            }
        }
        fixture.SetTier(MembershipTier.Gold); pet.OpenChat(); var pending = pet.Chat.SendText("晚安"); await Task.Delay(100); fixture.Logout(); await pending;
        Require(pet.ActiveChat is null && pet.Chat.History.Count == 0 && !pet.Chat.IsThinking, "logout cancels pending chat and clears account conversation");
        pet.OpenChat();Require(pet.Accounts.CurrentAccount is null && pet.ActiveChat is not null,"guest chat is available during testing without creating an account");
        await pet.Chat.SendText("你好");Require(pet.Chat.History.Count==2 && pet.Chat.ReplyText.Length>4,"guest receives the default local conversation");pet.StopInteraction();
        fixture.UseFullAccess(); window.ShowMembership(); Capture(window, "membership-blackgold");
        window.Close(); pet.StopInteraction();
        File.WriteAllLines(Path.Combine(output, "membership-check.txt"), checks.Append($"PASS {checks.Count} membership, actual registration/login, tier access and account isolation checks."));
    }
}

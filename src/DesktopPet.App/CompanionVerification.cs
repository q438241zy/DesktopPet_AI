using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopPet.Core;

namespace DesktopPet.App;

/// <summary>Runs real WPF controls and timers against the required isolated --data-dir.</summary>
internal static class CompanionVerification
{
    public static async Task Run(PetWindow pet, string output)
    {
        Directory.CreateDirectory(output); var checks = new List<string>();
        void Require(bool ok, string name) { if (!ok) throw new InvalidOperationException(name); checks.Add("PASS " + name); File.WriteAllLines(Path.Combine(output, "companion-check.txt"), checks); }
        IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var item = VisualTreeHelper.GetChild(root, i); if (item is T result) yield return result; foreach (var nested in Find<T>(item)) yield return nested; }
        }
        async Task Until(Func<bool> predicate, string name, int seconds = 6)
        { var timer = Stopwatch.StartNew(); while (!predicate()) { if (timer.Elapsed.TotalSeconds > seconds) throw new TimeoutException(name); await Task.Delay(40); } }
        void Capture(FrameworkElement view, string name)
        {
            view.UpdateLayout(); var bitmap = new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth), (int)Math.Ceiling(view.ActualHeight), 96, 96, PixelFormats.Pbgra32); bitmap.Render(view); PetWindow.SaveCompanionPhoto(bitmap, Path.Combine(output, name + ".png"));
        }
        pet.Accounts.Logout(); pet.IsHitTestVisible = false; pet.State.AutoHide = false; pet.State.Wander = false; pet.State.ReducedMotion = false; pet.State.CheckIns.Clear(); pet.State.MakeupCards = 0;
        pet.SelectCharacter("gpt"); pet.State.Outfits["gpt"] = "original"; pet.ApplySettings(); pet.StopInteraction(); pet.OpenSettings();
        var window = Application.Current.Windows.OfType<SettingsWindow>().Single(); window.IsHitTestVisible = false; window.ShowActivated = false; window.UpdateLayout();
        void Click(string name) { window.UpdateLayout(); var b = Find<Button>(window).Single(b => AutomationProperties.GetName(b) == name); Require(b.IsEnabled, "enabled " + name); b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout(); }
        var labels = Find<Button>(window).Select(AutomationProperties.GetName).ToArray();
        Require(new[] { "我的伙伴", "陪伴日常", "会员中心", "设定" }.All(labels.Contains) && !labels.Any(s => s is "角色工坊" or "桌面偏好" or "注册 / 登录"), "four approved navigation destinations, no workshop or home membership");
        Require(Find<Image>(window).Count() >= 9 && labels.Contains("选择服装 短袖运动服"), "original hero with outfits follows eight character cards"); Capture(window, "home");
        var card = Find<Button>(window).Single(b => AutomationProperties.GetName(b) == "选择角色 GPT"); card.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right) { RoutedEvent = UIElement.PreviewMouseRightButtonUpEvent }); window.UpdateLayout();
        Require(Find<Button>(window).Any(b => AutomationProperties.GetName(b) == "我们的默契"), "right click opens profile tabs");
        foreach (string family in Catalog.BuiltInFamilies)
        {
            window.ShowProfile(family); window.UpdateLayout(); Click("说明");
            foreach (var like in CompanionPersonas.Profile(family).GetProperty("likes").EnumerateArray()) Require(Find<TextBlock>(window).Any(t => t.Text == like[0].GetString()), family + " likes match offline and API persona");
            Click("风格预览");
            foreach (string outfit in Catalog.BuiltInOutfits)
            {
                Click(outfit switch { "original" => "原装", "sports" => "运动服", "swim" => "泳装", _ => "婚纱" });
                foreach (string style in CharacterStyles.All)
                {
                    string id = Catalog.VariantId(family, style); var c = pet.Catalog.Find(id); var frame = c.Resolve(outfit, "idle", 0);
                    Require(Find<Image>(window).Any(i => ReferenceEquals(i.Source, pet.Art.Frame(c, frame.Sprite, frame.Frame))), $"profile original PNG {id}/{outfit}");
                    Click("陪伴我 · " + CloudTheme.CategoryName(style)); Require(pet.State.Character == id && pet.State.Outfit == outfit, $"test-open selection {id}/{outfit}");
                }
            }
        }
        window.ShowProfile("gpt"); window.UpdateLayout(); Capture(window, "profile-bond"); Click("说明"); Capture(window, "profile-about");
        pet.SelectCharacter("gpt"); pet.State.Outfits["gpt"] = "original"; pet.ApplySettings();
        Click("陪伴日常"); var today = DateOnly.FromDateTime(DateTime.Now); Click($"{today:yyyy-MM-dd} 今天打卡");
        Require(pet.State.CheckedIn(today) && pet.State.Companion("gpt").Score >= 3, "calendar today grants only one check-in");
        Require(Find<Button>(window).Any(b => AutomationProperties.GetName(b) == $"{today:yyyy-MM-dd} 已打卡") && pet.CurrentAction == "happy", "calendar click immediately marks date and smiles");
        int count = pet.State.CheckIns.Count; pet.CheckIn(); Require(pet.State.CheckIns.Count == count, "duplicate day is idempotent");
        Click("周历"); Require(pet.State.CalendarView == "week" && Find<Button>(window).Count(b => AutomationProperties.GetName(b).StartsWith(today.Year.ToString() + "-")) == 7, "week calendar has seven dates"); Capture(window, "calendar-week");
        Click("上一周"); Click("月历"); Require(pet.State.CalendarView == "month", "month view switches on browsed date"); Click("今天"); Capture(window, "calendar-month");
        var old = today.AddDays(-1); pet.State.CheckIns.Remove(old.ToString("yyyy-MM-dd")); window.RefreshLife(); Click($"{old:yyyy-MM-dd} 补签"); Require(!pet.State.CheckedIn(old), "no-card calendar click cannot makeup");
        double beforeTime = pet.State.Companion("gpt").Seconds; int beforeScore = pet.State.Companion("gpt").Score; pet.State.MakeupCards = 1; Require(pet.MakeupCheckIn(old) && pet.State.MakeupCards == 0 && pet.State.Companion("gpt").Score == beforeScore && pet.State.Companion("gpt").Seconds == beforeTime, "confirmed makeup does not invent bond or time");
        pet.State.Treasures.AddRange(["篮球", "面包", "贝壳"]); pet.State.ReadStories["cloud-post"] = 1; window.RefreshLife();
        Click("球区"); Click("玩收藏 篮球"); Require(pet.ActiveToy.Id == "basketball", "owned toy remains playable from collection");
        Click("食物区"); Require(Find<TextBlock>(window).Any(t => t.Text.Contains("贝壳")), "legacy nonfood collection is retained"); Click("故事区"); Require(Find<Button>(window).Any(b => AutomationProperties.GetName(b) == "再读一次 · 云朵邮差"), "read story becomes a collectible");
        Require(!Find<Button>(window).Any(b => AutomationProperties.GetName(b).Contains("礼物")), "daily page has no gift draw"); Capture(window, "collection-stories");
        Click("会员中心"); Require(Find<Button>(window).Any(b => AutomationProperties.GetName(b) == "注册分页"), "membership login and register tabs"); Capture(window, "member-login");
        Click("设定"); var provider = Find<ComboBox>(window).Single(b => AutomationProperties.GetName(b) == "模型服务"); Require(provider.Items.Count == 9, "eight APIs plus offline");
        foreach (var spec in CompanionProviders.All)
        {
            provider.SelectedItem = provider.Items.Cast<ComboBoxItem>().Single(i => (string)i.Tag == spec.Id); window.UpdateLayout();
            var address = Find<TextBox>(window).Single(b => AutomationProperties.GetName(b) == "API 地址"); Require(address.Text == spec.Base, spec.Id + " default base");
            var secret = Find<PasswordBox>(window).Single(b => AutomationProperties.GetName(b) == "API Key（仅本次会话）"); secret.Password = "test-only-key"; address.Text += "/"; Require(secret.Password.Length == 0, spec.Id + " changed host clears key");
        }
        provider.SelectedIndex = 0; Click("保存接口设定"); Require(pet.Chat.Options.IsLocal && !File.ReadAllText(Path.Combine(output, "chat-settings.json")).Contains("test-only-key"), "local saved without key");
        var toggle = Find<ToggleButton>(window).Single(b => AutomationProperties.GetName(b) == "工作模式开关"); Require(toggle.IsChecked != true && !pet.WorkReminderEnabled, "work mode starts off"); toggle.IsChecked = true; toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout();
        var minutes = Find<TextBox>(window).Single(b => AutomationProperties.GetName(b) == "提醒间隔分钟"); minutes.Text = "10"; Click("保存间隔"); Require(pet.WorkReminderEnabled && pet.State.WorkMinutes == 10, "work mode saves ten minutes");
        Click("试一下提醒"); await Until(() => Application.Current.Windows.OfType<Window>().Any(w => w.Title == "云朵伙伴 · 工作提醒"), "work reminder");
        var reminder = Application.Current.Windows.OfType<Window>().Single(w => w.Title == "云朵伙伴 · 工作提醒"); var smile = pet.Character.Resolve(pet.State.Outfit, "happy", 0);
        Require(Find<Image>(reminder).Any(i => ReferenceEquals(i.Source, pet.Art.Frame(pet.Character, smile.Sprite, smile.Frame))), "reminder uses current outfit smile"); Capture(reminder, "work-reminder");
        toggle.IsChecked = false; toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Require(!pet.WorkReminderEnabled && !reminder.IsVisible, "disabling work mode closes reminder"); Capture(window, "settings");
        pet.Chat.ResetSession();
        foreach (string family in Catalog.BuiltInFamilies)
        {
            pet.SelectCharacter(family); pet.OpenChat(); var time = Stopwatch.StartNew(); var reply = pet.Chat.SendText("你喜欢什么"); await Task.Delay(200); Require(pet.Chat.IsThinking && pet.CurrentAction == "thinking", family + " thinking pose"); await reply;
            Require(time.ElapsedMilliseconds >= 990 && pet.Chat.ReplyText == CompanionPersonas.Profile(family).GetProperty("favorite").GetString(), family + " actual offline reply after one second");
        }
        pet.SelectCharacter("gpt"); pet.OpenChat(); Require(pet.Chat.History.Count == 2, "family conversation restored independently"); int previousHistory = pet.Chat.History.Count;
        var pending = pet.Chat.SendText("工作好多"); await Task.Delay(50); pet.SelectCharacter("claude"); await pending; pet.OpenChat(); Require(pet.Chat.History.Count == 2 && !pet.Chat.IsThinking, "switch character cancels stale response");
        pet.SelectCharacter("gpt"); pet.OpenChat(); Require(pet.Chat.History.Count == previousHistory + 1, "cancelled request never adds an assistant answer"); Capture(pet, "gpt-inline-chat"); pet.StopInteraction();
        for (int i = 0; i < 6; i++) pet.Touch(.4);
        Require(pet.State.Companion("gpt").History.Last() is { Label: "连续逗弄", Delta: -2 } && pet.CurrentSpeech == CompanionPersonas.Text("gpt", "distant"), "six rapid touches lower affinity and express a gentle boundary"); pet.StopInteraction();
        foreach (string style in CharacterStyles.All)
        foreach (string outfit in Catalog.BuiltInOutfits)
        {
            var first = pet.Catalog.Find(Catalog.VariantId("gpt", style)); var second = pet.Catalog.Find(Catalog.VariantId("whale", style));
            var photo = pet.CreateCompanionPhoto(first, outfit, second, outfit, "今天也一起，收集一朵温柔的云", style == CharacterStyles.Chibi ? 1 : 2);
            Require(photo.PixelWidth == 1200 && photo.PixelHeight == 1400, "framed dual photo " + style + "/" + outfit); PetWindow.SaveCompanionPhoto(photo, Path.Combine(output, $"photo-{style}-{outfit}.png"));
        }
        pet.RunInteraction("photo"); await Until(() => Application.Current.Windows.OfType<Window>().Any(w => w.Title == "云朵伙伴 · 合照"), "photo window"); var photoWindow = Application.Current.Windows.OfType<Window>().Single(w => w.Title == "云朵伙伴 · 合照"); Require(Find<ComboBox>(photoWindow).First(b => AutomationProperties.GetName(b) == "合照伙伴").Items.Count == 7, "photo permits seven other companions"); Capture(photoWindow, "photo-dialog"); photoWindow.Close();
        Click("陪伴日常"); Click("周历"); pet.Save(); var restored = new StateStore(output).Load(); Require(restored.CalendarView == "week" && restored.MakeupCards == 0 && restored.CheckedIn(old) && restored.Treasures.Contains("贝壳") && restored.ReadStories["cloud-post"] == 1, "all progress survives reload");
        window.Close(); pet.SelectCharacter("gpt"); pet.State.Outfits["gpt"] = "original"; pet.ApplySettings(); pet.StopInteraction();
        var workClock = (WorkReminderClock)typeof(PetWindow).GetField("workClock", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(pet)!;
        pet.ConfigureWork(true, 1); workClock.Start(-60000, 1); await Until(() => Application.Current.Windows.OfType<Window>().Any(w => w.Title == "云朵伙伴 · 工作提醒"), "live timer reminder"); Require(true, "live WPF timer delivers due reminder"); pet.ConfigureWork(false, 10);
        pet.Left = pet.WorkArea.Left + pet.State.Size * .46 - 280; pet.Top = pet.WorkArea.Bottom - 468; pet.StopInteraction(); pet.State.AutoHide = true;
        typeof(PetWindow).GetField("lastCompanionActivity", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(pet, -61000d);
        await Until(() => pet.HideStage is not null, "idle minute auto hide"); Require(true, "live timer triggers idle-minute hide");
        // Keep the chosen edge close so the test observes the held phase, not a long approach.
        pet.BeginHide(-1); await Until(() => pet.HideStage == HidePhase.Peek, "hide peek", 10); await Task.Delay(11000);
        Require(pet.HideStage == HidePhase.Peek, "hide stays past ten seconds awaiting user");
        pet.InputSurface.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right) { RoutedEvent = UIElement.MouseRightButtonUpEvent }); Require(pet.HideStage == HidePhase.Peek && pet.IsMenuOpen, "right click does not find hidden pet");
        pet.InputSurface.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent }); Require(pet.HideStage == HidePhase.Return, "left click starts found return"); await Until(() => pet.HideStage is null, "found return");
        Require(pet.CurrentSpeech == "被你找到啦！", "found greeting after return");
        File.WriteAllLines(Path.Combine(output, "companion-check.txt"), checks.Append($"PASS {checks.Count} native Companion v0.4 checks."));
    }
}

using System.Windows;
using DesktopPet.Core;

namespace DesktopPet.App;

public partial class App : Application
{
    private Mutex? singleton;
    internal static string? MotionLog { get; private set; }
    public static string DataRoot { get; private set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopPetAI");
    private async void OnStartup(object sender, StartupEventArgs e)
    {
        int membershipIndex = Array.IndexOf(e.Args, "--export-membership");
        if (membershipIndex >= 0 && membershipIndex + 1 < e.Args.Length)
        {
            var rules = new { testingOpen = Membership.TestingOpen, requirements = Membership.Requirements.ToDictionary(p => p.Key, p => (int)p.Value), tiers = Membership.Tiers.Select(t => new { value = (int)t, name = Membership.Name(t) }) };
            File.WriteAllText(Path.GetFullPath(e.Args[membershipIndex + 1]), System.Text.Json.JsonSerializer.Serialize(rules, Json.Options)); Shutdown(0); return;
        }
        int iconIndex = Array.IndexOf(e.Args, "--export-icon");
        if (iconIndex >= 0 && iconIndex + 1 < e.Args.Length) { CloudTheme.WriteIcon(Path.GetFullPath(e.Args[iconIndex + 1])); Shutdown(0); return; }
        int dataIndex = Array.IndexOf(e.Args, "--data-dir");
        if (dataIndex >= 0 && dataIndex + 1 < e.Args.Length) DataRoot = Path.GetFullPath(e.Args[dataIndex + 1]);
        int motionLogIndex = Array.IndexOf(e.Args, "--motion-log");
        if (motionLogIndex >= 0 && motionLogIndex + 1 < e.Args.Length) MotionLog = Path.GetFullPath(e.Args[motionLogIndex + 1]);
        if ((e.Args.Any(a => a.StartsWith("--verify-")) || e.Args.Contains("--export-demo")) && (dataIndex < 0 || dataIndex + 1 >= e.Args.Length))
        { MessageBox.Show("--verify-ui 必须指定独立的 --data-dir。"); Shutdown(1); return; }
        if (e.Args.Contains("--verify-assets"))
        {
            try
            {
                var catalog = new Catalog(DataRoot);
                int count = 0;
                foreach (var c in catalog.Characters)
                    foreach (var s in c.Sprites()) { ArtCache.Validate(c, s); count++; }
                Directory.CreateDirectory(DataRoot);
                File.WriteAllText(Path.Combine(DataRoot, "asset-check.txt"), $"PASS: {catalog.Characters.Count} characters, {count} image definitions decoded.\n");
                Shutdown(0);
            }
            catch (Exception ex) { Directory.CreateDirectory(DataRoot); File.WriteAllText(Path.Combine(DataRoot, "asset-check.txt"), ex.ToString()); Shutdown(1); }
            return;
        }
        singleton = new Mutex(true, "Local\\DesktopPetAI-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(DataRoot)))[..16], out bool created);
        if (!created) { MessageBox.Show("桌面宠物已经在运行。请从系统托盘打开设置，或按 Ctrl+Alt+S。", "DesktopPet"); Shutdown(); return; }
        DispatcherUnhandledException += (_, args) =>
        {
            Directory.CreateDirectory(DataRoot);
            File.AppendAllText(Path.Combine(DataRoot, "error.log"), $"{DateTimeOffset.Now:O} {args.Exception}\n");
            MessageBox.Show("桌面宠物遇到错误，日志已保存在本地数据目录。\n" + args.Exception.Message, "DesktopPet");
            args.Handled = true;
            Shutdown(1);
        };
        // Only isolated native checks receive test levels. A normal launch always uses the real local provider.
        IAccountService? accounts = e.Args.Any(a => a.StartsWith("--verify-"))
            ? MembershipVerification.CreateFixture(DataRoot) : null;
        var pet = new PetWindow(new StateStore(DataRoot), new Catalog(DataRoot), accounts);
        if (e.Args.Contains("--ui-test")) pet.ShowInTaskbar = true;
        MainWindow = pet;
        pet.Show();
        if (e.Args.Contains("--verify-idle-postures"))
        {
            try { await IdlePostureVerification.Run(pet, DataRoot); Shutdown(0); }
            catch (Exception ex) { Directory.CreateDirectory(DataRoot); File.WriteAllText(Path.Combine(DataRoot, "idle-posture-error.txt"), ex.ToString()); Shutdown(1); }
            return;
        }
        if (e.Args.Contains("--verify-idle-hide"))
        {
            try { await IdleHideVerification.Run(pet, DataRoot); Shutdown(0); }
            catch (Exception ex) { Directory.CreateDirectory(DataRoot); File.WriteAllText(Path.Combine(DataRoot, "idle-hide-error.txt"), ex.ToString()); Shutdown(1); }
            return;
        }
        if (e.Args.Contains("--verify-agenda"))
        {
            try { await AgendaVerification.Run(pet, DataRoot, e.Args.Contains("--agenda-reload")); Shutdown(0); }
            catch (Exception ex) { Directory.CreateDirectory(DataRoot); File.WriteAllText(Path.Combine(DataRoot, "agenda-error.txt"), ex.ToString()); Shutdown(1); }
            return;
        }
        if (e.Args.Contains("--verify-companion"))
        {
            try { await CompanionVerification.Run(pet, DataRoot); Shutdown(0); }
            catch (Exception ex) { Directory.CreateDirectory(DataRoot); File.WriteAllText(Path.Combine(DataRoot, "companion-error.txt"), ex.ToString()); Shutdown(1); }
            return;
        }
        if(e.Args.Contains("--verify-system-pointer"))
        {
            try{await SystemPointerVerification.Run(pet,DataRoot,e.Args.Contains("--pointer-all"));Shutdown(0);}
            catch(Exception ex){Directory.CreateDirectory(DataRoot);File.WriteAllText(Path.Combine(DataRoot,"system-pointer-error.txt"),ex.ToString());Shutdown(1);}
            return;
        }
        if(e.Args.Contains("--verify-native-pointer"))
        {
            try{await NativePointerVerification.Run(pet,DataRoot,e.Args.Contains("--pointer-all"));Shutdown(0);}
            catch(Exception ex){Directory.CreateDirectory(DataRoot);File.WriteAllText(Path.Combine(DataRoot,"native-pointer-error.txt"),ex.ToString());Shutdown(1);}
            return;
        }
        if(e.Args.Contains("--verify-walk-interrupt"))
        {
            try{await WalkInterruptVerification.Run(pet,DataRoot);Shutdown(0);}
            catch(Exception ex){Directory.CreateDirectory(DataRoot);File.WriteAllText(Path.Combine(DataRoot,"walk-interrupt-error.txt"),ex.ToString());Shutdown(1);}
            return;
        }
        if (e.Args.Contains("--settings")) pet.OpenSettings();
        if(e.Args.Contains("--verify-five"))
        {
            try{await FiveVerification.Run(pet,DataRoot,e.Args.Contains("--five-available"));Shutdown(0);}
            catch(Exception ex){Directory.CreateDirectory(DataRoot);File.WriteAllText(Path.Combine(DataRoot,"five-error.txt"),ex.ToString());Shutdown(1);}
            return;
        }
        if (e.Args.Contains("--export-demo"))
        {
            try { await DemoExporter.Run(pet, DataRoot); Shutdown(0); }
            catch (Exception ex) { Directory.CreateDirectory(DataRoot); File.WriteAllText(Path.Combine(DataRoot,"demo-error.txt"),ex.ToString()); Shutdown(1); }
            return;
        }
        if (e.Args.Contains("--verify-sports") || e.Args.Contains("--verify-club") || e.Args.Contains("--verify-ui") || e.Args.Contains("--verify-interface") || e.Args.Contains("--verify-interactions") || e.Args.Contains("--verify-details") || e.Args.Contains("--verify-contacts") || e.Args.Contains("--verify-poses") || e.Args.Contains("--verify-placement") || e.Args.Contains("--verify-polish") || e.Args.Contains("--verify-chibi") || e.Args.Contains("--verify-shake") || e.Args.Contains("--verify-walk") || e.Args.Contains("--verify-floor-contact") || e.Args.Contains("--verify-scale") || e.Args.Contains("--verify-care") || e.Args.Contains("--verify-membership"))
        {
            try
            {
                int appearanceIndex = Array.IndexOf(e.Args, "--appearance");
                string? appearance = appearanceIndex < 0 ? null : appearanceIndex + 1 < e.Args.Length
                    ? e.Args[appearanceIndex + 1] : throw new ArgumentException("--appearance 需要指定角色与服装。");
                if (e.Args.Contains("--verify-ui") || e.Args.Contains("--verify-membership")) await MembershipVerification.Run(pet, DataRoot);
                if (e.Args.Contains("--verify-sports")) await SportsVerification.Run(pet,DataRoot,e.Args.Contains("--sports-sampling-only"),appearance);
                if (e.Args.Contains("--verify-club")) await ClubVerification.Run(pet, DataRoot, e.Args.Contains("--club-pilot"),appearance);
                if (e.Args.Contains("--verify-scale")) await ScaleVerification.Run(pet, DataRoot,e.Args.Contains("--scale-baseline"));
                if (e.Args.Contains("--verify-ui") || e.Args.Contains("--verify-care")) await CareVerification.Run(pet, DataRoot);
                if (e.Args.Contains("--verify-floor-contact")) await TaskbarContactVerification.Run(pet, DataRoot);
                if (e.Args.Contains("--verify-ui") || e.Args.Contains("--verify-interface")) await UiVerification.Run(pet, DataRoot);
                if (e.Args.Contains("--verify-ui") || e.Args.Contains("--verify-interactions")) await InteractionVerification.Run(pet, DataRoot);
                if (e.Args.Contains("--verify-ui") || e.Args.Contains("--verify-details")) await DetailVerification.Run(pet, DataRoot);
                if (e.Args.Contains("--verify-ui") || e.Args.Contains("--verify-placement")) await PlacementVerification.Run(pet, DataRoot);
                if (e.Args.Contains("--verify-ui") || e.Args.Contains("--verify-contacts")) await ContactVerification.Run(pet, DataRoot, e.Args.Contains("--contact-pilot"), e.Args.Contains("--contact-available"), appearance);
                if (e.Args.Contains("--verify-ui") || e.Args.Contains("--verify-poses")) await ChoreographyVerification.Run(pet, DataRoot, e.Args.Contains("--pose-pilot"), e.Args.Contains("--pose-available"), appearance);
                if (e.Args.Contains("--verify-ui") || e.Args.Contains("--verify-polish")) await PolishVerification.Run(pet, DataRoot);
                if (e.Args.Contains("--verify-ui") || e.Args.Contains("--verify-chibi")) await ChibiVerification.Run(pet, DataRoot, appearance);
                if (e.Args.Contains("--verify-ui") || e.Args.Contains("--verify-shake")) await ShakeVerification.Run(pet, DataRoot);
                if (e.Args.Contains("--verify-ui") || e.Args.Contains("--verify-walk")) await WalkVerification.Run(pet, DataRoot);
                Shutdown(0);
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(DataRoot, "ui-check.txt"), ex.ToString()); Shutdown(1); }
        }
    }
    protected override void OnExit(ExitEventArgs e) { singleton?.Dispose(); base.OnExit(e); }
}

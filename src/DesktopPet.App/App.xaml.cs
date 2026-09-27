using System.Windows;
using DesktopPet.Core;

namespace DesktopPet.App;

public partial class App : Application
{
    private Mutex? singleton;
    public static string DataRoot { get; private set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopPetAI");
    private async void OnStartup(object sender, StartupEventArgs e)
    {
        int iconIndex = Array.IndexOf(e.Args, "--export-icon");
        if (iconIndex >= 0 && iconIndex + 1 < e.Args.Length) { CloudTheme.WriteIcon(Path.GetFullPath(e.Args[iconIndex + 1])); Shutdown(0); return; }
        int dataIndex = Array.IndexOf(e.Args, "--data-dir");
        if (dataIndex >= 0 && dataIndex + 1 < e.Args.Length) DataRoot = Path.GetFullPath(e.Args[dataIndex + 1]);
        if ((e.Args.Contains("--verify-ui") || e.Args.Contains("--verify-interactions") || e.Args.Contains("--verify-details") || e.Args.Contains("--verify-contacts") || e.Args.Contains("--verify-poses")) && (dataIndex < 0 || dataIndex + 1 >= e.Args.Length))
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
        var pet = new PetWindow(new StateStore(DataRoot), new Catalog(DataRoot));
        if (e.Args.Contains("--ui-test")) pet.ShowInTaskbar = true;
        MainWindow = pet;
        pet.Show();
        if (e.Args.Contains("--settings")) pet.OpenSettings();
        if (e.Args.Contains("--verify-ui") || e.Args.Contains("--verify-interactions") || e.Args.Contains("--verify-details") || e.Args.Contains("--verify-contacts") || e.Args.Contains("--verify-poses"))
        {
            try
            {
                int appearanceIndex = Array.IndexOf(e.Args, "--appearance");
                string? appearance = appearanceIndex < 0 ? null : appearanceIndex + 1 < e.Args.Length
                    ? e.Args[appearanceIndex + 1] : throw new ArgumentException("--appearance 需要指定角色与服装。");
                if (e.Args.Contains("--verify-ui")) await UiVerification.Run(pet, DataRoot);
                if (e.Args.Contains("--verify-ui") || e.Args.Contains("--verify-interactions")) await InteractionVerification.Run(pet, DataRoot);
                if (e.Args.Contains("--verify-ui") || e.Args.Contains("--verify-details")) await DetailVerification.Run(pet, DataRoot);
                if (e.Args.Contains("--verify-ui") || e.Args.Contains("--verify-contacts")) await ContactVerification.Run(pet, DataRoot, e.Args.Contains("--contact-pilot"), e.Args.Contains("--contact-available"), appearance);
                if (e.Args.Contains("--verify-ui") || e.Args.Contains("--verify-poses")) await ChoreographyVerification.Run(pet, DataRoot, e.Args.Contains("--pose-pilot"), e.Args.Contains("--pose-available"), appearance);
                Shutdown(0);
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(DataRoot, "ui-check.txt"), ex.ToString()); Shutdown(1); }
        }
    }
    protected override void OnExit(ExitEventArgs e) { singleton?.Dispose(); base.OnExit(e); }
}

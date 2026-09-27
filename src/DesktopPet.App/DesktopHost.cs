using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DesktopPet.App;

/// <summary>Native tray recovery and hotkeys adapted from UsageDashboard_AI.</summary>
public sealed class DesktopHost : IDisposable
{
    private readonly nint handle;
    private readonly HwndSource source;
    private readonly List<int> registered = [];
    private readonly System.Windows.Forms.NotifyIcon tray;
    public event Action<int>? Pressed;
    public List<string> Warnings { get; } = [];
    public DesktopHost(Window window, Action settings, Action toggle, Action quit)
    {
        handle = new WindowInteropHelper(window).Handle;
        source = HwndSource.FromHwnd(handle);
        source.AddHook(Hook);
        (int Id, uint Key, string Name)[] keys = [(1, 0x55, "Ctrl+Alt+U"), (2, 0x4C, "Ctrl+Alt+L"), (3, 0x53, "Ctrl+Alt+S")];
        foreach (var key in keys)
        {
            if (RegisterHotKey(handle, key.Id, 0x4003, key.Key)) registered.Add(key.Id);
            else Warnings.Add($"{key.Name} 被其他程序占用；可使用托盘菜单。");
        }
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("打开宠物之家", null, (_, _) => window.Dispatcher.Invoke(settings));
        menu.Items.Add("显示 / 隐藏宠物", null, (_, _) => window.Dispatcher.Invoke(toggle));
        menu.Items.Add("解除鼠标穿透", null, (_, _) => window.Dispatcher.Invoke(() => Pressed?.Invoke(2)));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("退出桌面宠物", null, (_, _) => window.Dispatcher.Invoke(quit));
        tray = new System.Windows.Forms.NotifyIcon { Text = "DesktopPet · 桌边伙伴", Icon = System.Drawing.SystemIcons.Application, Visible = true, ContextMenuStrip = menu };
        tray.DoubleClick += (_, _) => window.Dispatcher.Invoke(settings);
    }
    public void ClickThrough(bool enabled)
    {
        var style = GetWindowLongPtr(handle, -20).ToInt64();
        SetWindowLongPtr(handle, -20, new nint(enabled ? style | 0x20 : style & ~0x20));
    }
    public Rect WorkArea(Window window)
    {
        var screen = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
        var transform = PresentationSource.FromVisual(window)?.CompositionTarget?.TransformFromDevice ?? System.Windows.Media.Matrix.Identity;
        var a = transform.Transform(new Point(screen.Left, screen.Top));
        var b = transform.Transform(new Point(screen.Right, screen.Bottom));
        return new Rect(a, b);
    }
    private nint Hook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    { if (msg == 0x0312) { Pressed?.Invoke(wParam.ToInt32()); handled = true; } return 0; }
    public void Dispose()
    {
        foreach (int id in registered) UnregisterHotKey(handle, id);
        source.RemoveHook(Hook);
        tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Dispose();
    }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(nint hWnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint hWnd, int id);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hWnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLongPtr(nint hWnd, int index, nint value);
}

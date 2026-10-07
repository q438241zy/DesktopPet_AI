using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace DesktopPet.App;

/// <summary>Native tray recovery and hotkeys adapted from UsageDashboard_AI.</summary>
public sealed class DesktopHost : IDisposable
{
    private readonly nint handle;
    private readonly HwndSource source;
    private readonly List<int> registered = [];
    private readonly System.Windows.Forms.NotifyIcon tray;
    private readonly System.Drawing.Icon icon;
    public event Action<int>? Pressed;
    internal event Func<int, Point, long, bool>? PointerInput;
    internal event Action? PointerCancelled;
    internal event Action<int>? PointerTrace;
    internal event Action<string>? PointerRecoveryTrace;
    internal event Func<Point,bool>? BodyHit;
    internal event Func<bool>? RecoverWalkingPointer;
    internal event Func<bool>? OwnsPointer;
    internal event Action? RightClick;
    private readonly DispatcherTimer pointerTimer;
    private bool leftWasDown;
    private NativePoint lastPointer;
    private int? recoveredDownTime, recoveredUpTime;
    private bool rightWasDown, rightGesture;
    private int? recoveredRightUpTime;
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
        menu.Items.Add("打开云朵伙伴", null, (_, _) => window.Dispatcher.Invoke(settings));
        menu.Items.Add("显示 / 隐藏宠物", null, (_, _) => window.Dispatcher.Invoke(toggle));
        menu.Items.Add("解除鼠标穿透", null, (_, _) => window.Dispatcher.Invoke(() => Pressed?.Invoke(2)));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("退出桌面宠物", null, (_, _) => window.Dispatcher.Invoke(quit));
        icon = new System.Drawing.Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "cloud.ico"));
        tray = new System.Windows.Forms.NotifyIcon { Text = "DesktopPet · 云朵伙伴", Icon = icon, Visible = true, ContextMenuStrip = menu };
        tray.DoubleClick += (_, _) => window.Dispatcher.Invoke(settings);
        leftWasDown = (GetAsyncKeyState(1) & 0x8000) != 0;
        rightWasDown = (GetAsyncKeyState(2) & 0x8000) != 0;
        // The sampler is deliberately above Render: expensive pose updates must
        // not delay observing a short press until after the button is released.
        pointerTimer = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromMilliseconds(8) };
        pointerTimer.Tick += (_, _) => RecoverPointer();
        window.IsVisibleChanged += (_, _) =>
        {
            leftWasDown=LeftPointerDown;
            rightWasDown=(GetAsyncKeyState(2)&0x8000)!=0;rightGesture=false;
            if(window.IsVisible)pointerTimer.Start();else pointerTimer.Stop();
        };
        if(window.IsVisible)pointerTimer.Start();
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
    {
        if(msg is 0x0021 or 0x0201 or 0x0202 or 0x0203 or 0x00a1 or 0x00a2 or 0x0215)PointerTrace?.Invoke(msg);
        if (msg == 0x0312) { Pressed?.Invoke(wParam.ToInt32()); handled = true; }
        else if (msg is 0x0200 or 0x0201 or 0x0202 or 0x0203)
        {
            // Recovery may precede delivery of the original queued message.
            // Compare OS timestamps so that same press/release cannot replay
            // as a second touch or stop the next walk after the user lets go.
            int messageTime=GetMessageTime();
            int? recovered=msg is 0x0201 or 0x0203 ? recoveredDownTime : msg==0x0202 ? recoveredUpTime : null;
            if(recovered is { } time && unchecked(messageTime-time)<=0) { handled=true;return 0; }
            if(msg==0x0202 && LeftPointerDown) { handled=true;return 0; }
            // A queued move's client coordinates can precede an HWND rebase.
            // The live cursor is authoritative for captured motion/release.
            bool located = GetCursorPos(out var point);
            if(located)
                handled=PointerInput?.Invoke(msg,new Point(point.X,point.Y),wParam.ToInt64())??false;
        }
        else if(msg==0x0205)
        {
            if(rightGesture)
            {
                if((GetAsyncKeyState(2)&0x8000)==0)FinishRightClick();
                handled=true;
            }
            else if(recoveredRightUpTime is {} time && unchecked(GetMessageTime()-time)<=0)handled=true;
        }
        else if(msg is 0x0215 or 0x001f) { rightGesture=false;PointerCancelled?.Invoke(); }
        return 0;
    }
    private void RecoverPointer()
    {
        RecoverRightPointer();
        // A moving layered HWND can lose the queued button message between
        // hit-testing and dispatch. Recover only a fresh press over our own
        // character; a held button dragged in from another app is never ours.
        bool down = (GetAsyncKeyState(1) & 0x8000) != 0;
        bool fresh = down && !leftWasDown;
        leftWasDown = down;
        bool owned = OwnsPointer?.Invoke() == true;
        if(fresh)PointerRecoveryTrace?.Invoke($"pointer-sample-owned-{owned}-walking-{RecoverWalkingPointer?.Invoke()==true}");
        if (!owned && (!fresh || RecoverWalkingPointer?.Invoke() != true)) return;
        if (!GetCursorPos(out var cursor)) return;
        var point = new Point(cursor.X,cursor.Y);
        long buttons = (down ? 1 : 0) | ((GetAsyncKeyState(0x10) & 0x8000) != 0 ? 4 : 0);
        if (owned)
        {
            if (!down) { recoveredUpTime=Environment.TickCount;PointerInput?.Invoke(0x0202,point,buttons); }
            else if (GetCapture()==handle && (cursor.X!=lastPointer.X || cursor.Y!=lastPointer.Y))
                PointerInput?.Invoke(0x0200,point,buttons);
        }
        else
        {
            bool ownsWindow=WindowFromPoint(cursor)==handle, hitsBody=BodyHit?.Invoke(point)==true;
            PointerRecoveryTrace?.Invoke($"pointer-sample-window-{ownsWindow}-body-{hitsBody}");
            if(ownsWindow && hitsBody) { recoveredDownTime=Environment.TickCount;PointerInput?.Invoke(0x0201,point,buttons); }
        }
        lastPointer = cursor;
    }
    private void RecoverRightPointer()
    {
        bool down=(GetAsyncKeyState(2)&0x8000)!=0,fresh=down&&!rightWasDown;
        rightWasDown=down;
        if(rightGesture) { if(!down)FinishRightClick();return; }
        if(!fresh || RecoverWalkingPointer?.Invoke()!=true || !GetCursorPos(out var at))return;
        if(WindowFromPoint(at)==handle && BodyHit?.Invoke(new Point(at.X,at.Y))==true)rightGesture=true;
    }
    private void FinishRightClick()
    {
        rightGesture=false;recoveredRightUpTime=Environment.TickCount;
        RightClick?.Invoke();
    }
    internal void CapturePointer() => SetCapture(handle);
    internal bool LeftPointerDown => (GetAsyncKeyState(1) & 0x8000) != 0;
    internal bool ShiftDown => (GetAsyncKeyState(0x10) & 0x8000) != 0;
    internal void ReleasePointer() { if(GetCapture()==handle)ReleaseCapture(); }
    public void Dispose()
    {
        pointerTimer.Stop();
        foreach (int id in registered) UnregisterHotKey(handle, id);
        source.RemoveHook(Hook);
        tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Dispose(); icon.Dispose();
    }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(nint hWnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint hWnd, int id);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hWnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern nint SetWindowLongPtr(nint hWnd, int index, nint value);
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X,Y; }
    [DllImport("user32.dll")] private static extern nint SetCapture(nint hwnd);
    [DllImport("user32.dll")] private static extern nint GetCapture();
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(NativePoint point);
    [DllImport("user32.dll")] private static extern int GetMessageTime();
}

using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using DesktopPet.Core;

namespace DesktopPet.App;

/// <summary>A transparent native desktop surface; all transient interactions belong to this window.</summary>
public sealed class PetWindow : Window
{
    public PetState State { get; }
    public Catalog Catalog { get; }
    public ArtCache Art { get; } = new();
    public Character Character => Catalog.Find(State.Character);
    private readonly StateStore store;
    private readonly Canvas surface = new();
    private readonly Image sprite = new() { Stretch = Stretch.Uniform, Cursor = Cursors.Hand, Focusable = true };
    private readonly Canvas effects = new() { IsHitTestVisible = false };
    private readonly Canvas menu = new();
    private readonly TextBlock speech = new() { TextWrapping = TextWrapping.Wrap, FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(66, 56, 52)) };
    private readonly Border bubble;
    private readonly Button restore = new() { Content = "☾", Width = 48, Height = 48, Padding = new Thickness(4), Margin = new Thickness(0), ToolTip = "叫醒宠物" };
    private readonly Ellipse ball = new() { Width = 30, Height = 30, Fill = new SolidColorBrush(Color.FromRgb(239, 166, 92)), Stroke = Brushes.White, StrokeThickness = 2, Cursor = Cursors.Hand };
    private readonly DispatcherTimer timer;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly ShakeTracker shakes = new();
    private readonly ScaleTransform facing = new(1, 1);
    private DanceVisual? danceVisual;
    private EdgeHide? hideJourney;
    private Rect hideArea;
    private double hideStarted;
    private PetDance? dance;
    private int? danceFeedbackBeat;
    internal HidePhase? HideStage => hideJourney?.At((Now - hideStarted) / 1000).Phase;
    internal bool IsDancing => dance is not null;
    internal DanceVisual? ActiveDance => danceVisual;
    internal bool CanDance => DanceRig.Supports(Character.Category, Character.FamilyId);
    internal string CurrentAction => action;
    private DesktopHost? desktop;
    private SettingsWindow? settings;
    private string action = "idle";
    private double actionStarted, actionUntil, speechUntil, lastTick, lastInteraction, lastDizzy = double.NegativeInfinity;
    private Action? onMotionEnd;
    private bool dragging, pressed, resting, roaming, closing, holdingBall, flyingBall, ballHit, clickThrough;
    private Point downScreen, downWindow, ballPrevious;
    private double ballX, ballY, ballVx, ballVy, ballSampleTime, ballAge, roamDeadline, nextPeek;
    private int direction = -1;
    private string? prop;
    private double propStart;
    private DateOnly day;
    private const double CenterX = 280, FloorY = 468;
    private double groundLine = .875;
    private double PetTop => FloorY - (double.IsFinite(sprite.Height) ? sprite.Height : State.Size) * groundLine;
    private double Now => clock.Elapsed.TotalMilliseconds;

    public PetWindow(StateStore store, Catalog catalog)
    {
        this.store = store; Catalog = catalog; State = store.Load();
        State.Character = catalog.Find(State.Character).Id;
        Title = "DesktopPet · 桌边伙伴"; Icon = CloudTheme.AppIcon; Width = 560; Height = 680;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; AllowsTransparency = true;
        Background = Brushes.Transparent; ShowInTaskbar = false; Topmost = State.Topmost;
        Content = surface;
        sprite.RenderTransform = facing;
        restore.Content = CloudTheme.Icon("moon", 24);
        surface.Children.Add(effects); surface.Children.Add(sprite); surface.Children.Add(menu);
        bubble = new Border { Background = CloudTheme.Brush("#F8FCFF"), BorderBrush = CloudTheme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(20), Padding = new Thickness(15, 10, 15, 10), Width = 238, Child = speech, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        surface.Children.Add(bubble); surface.Children.Add(restore); surface.Children.Add(ball);
        restore.Visibility = Visibility.Collapsed; ball.Visibility = Visibility.Collapsed;
        AutomationProperties.SetName(sprite, "桌面宠物：点击摸头，拖动抱起，右键互动");
        sprite.MouseLeftButtonDown += PetDown; sprite.MouseMove += PetMove; sprite.MouseLeftButtonUp += PetUp;
        sprite.MouseRightButtonUp += (_, e) => { if (menu.Children.Count > 0) menu.Children.Clear(); else ShowMenu(); e.Handled = true; };
        sprite.LostMouseCapture += (_, _) => { if (pressed) CancelInput(); };
        sprite.KeyDown += (_, e) => { if (e.Key is Key.Enter or Key.Space) { if (dance is not null) TapDance(); else Touch(.3); e.Handled = true; } else if (e.Key == Key.F10 && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) { ShowMenu("root", true); e.Handled = true; } };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { StopInteraction(); e.Handled = true; } };
        restore.Click += (_, _) => RestorePet();
        ball.MouseLeftButtonDown += (_, e) => { holdingBall = true; flyingBall = false; ballPrevious = e.GetPosition(this); ballSampleTime = Now; ballVx = ballVy = 0; ball.CaptureMouse(); e.Handled = true; };
        ball.MouseMove += BallMove;
        ball.MouseLeftButtonUp += (_, e) => { if (!holdingBall) return; holdingBall = false; ball.ReleaseMouseCapture(); flyingBall = true; ballAge = 0; ballHit = false; Say("接住！", 1200); e.Handled = true; };
        ball.LostMouseCapture += (_, _) => { if (holdingBall) CancelInput(); };
        Deactivated += (_, _) => { CancelInput(); menu.Children.Clear(); };
        SourceInitialized += (_, _) => InitializeDesktop();
        Loaded += (_, _) => Welcome();
        timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(33) };
        timer.Tick += (_, _) => Tick(); timer.Start();
        Closed += (_, _) => { closing = true; timer.Stop(); CancelInput(); settings?.Close(); desktop?.Dispose(); Save(); };
        day = DateOnly.FromDateTime(DateTime.Now);
        ApplySettings(false);
    }
    private void InitializeDesktop()
    {
        desktop = new DesktopHost(this, OpenSettings, ToggleVisible, () => Application.Current.Shutdown());
        desktop.Pressed += id => { if (id == 1) ToggleVisible(); if (id == 2) { SetClickThrough(false); if (!IsVisible) Show(); if (resting) RestorePet(); } if (id == 3) OpenSettings(); };
        var area = desktop.WorkArea(this);
        Left = State.Left ?? area.Right - Width + 36;
        Top = State.Top ?? area.Bottom - FloorY;
        Constrain();
    }
    private void Welcome()
    {
        int away = State.DaysAway(day);
        string? anniversary = State.Anniversary(day);
        State.LastSeen = day.ToString("yyyy-MM-dd"); Save();
        if (away >= 3) { prop = "blocks"; propStart = Now; Play("pounce", $"{Character.Name}等到你啦，欢迎回来。", 3100); }
        else if (anniversary is not null && State.LastCelebration != State.LastSeen) { State.LastCelebration = State.LastSeen; Celebrate(anniversary); Save(); }
        else if (!State.CheckedIn(day)) { SetAction("sleep"); Say("点我打卡，一起吃早饭 ☀", 5500); }
        else Play("chat", "今天也陪你一起。右键找我玩。", 2600);
        if (store.Warning is { } warning) Say(warning, 6500);
    }
    public void Save()
    {
        State.Left = hideJourney is { } journey ? journey.RestingCenter - CenterX : Left; State.Top = Top;
        try { store.Save(State); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Say("存档未能保存：" + ex.Message, 6500); }
    }
    public void SelectCharacter(string id)
    {
        var selected = Catalog.FindExact(id);
        if (selected is null) { Say("这个形象暂不可用，请重新选择。", 3000); return; }
        CancelInput(); ClearTransient(); resting = false; restore.Visibility = Visibility.Collapsed;
        sprite.Visibility = Visibility.Visible;
        State.Character = selected.Id; Art.Clear(); ApplySettings();
        Play("chat", $"你好，我是{Character.Name}。", 2600);
    }
    public void SelectStyle(string category)
    {
        if (Character.Category == category) return;
        var variant = Catalog.Characters.FirstOrDefault(c => c.FamilyId == Character.FamilyId && c.Category == category);
        if (variant is null) return;
        string outfit = State.Outfit;
        State.Outfits[variant.Id] = variant.Outfits.ContainsKey(outfit) ? outfit : "original";
        SelectCharacter(variant.Id);
    }
    public void ApplySettings(bool save = true)
    {
        State.Validate(); Topmost = State.Topmost;
        sprite.Width = sprite.Height = State.Size; sprite.Opacity = State.Opacity;
        Canvas.SetLeft(sprite, CenterX - State.Size / 2); Canvas.SetTop(sprite, PetTop);
        Canvas.SetLeft(bubble, CenterX - 119); Canvas.SetTop(bubble, Math.Max(8, PetTop - 78));
        Canvas.SetLeft(restore, CenterX - 24); Canvas.SetTop(restore, FloorY - 48);
        if (State.ReducedMotion) { roaming = false; flyingBall = false; ball.Visibility = Visibility.Collapsed; }
        if (save) { CancelInput(); ClearTransient(); SetAction("idle"); Save(); }
        Render();
    }
    public void SetClickThrough(bool enabled) { clickThrough = enabled; desktop?.ClickThrough(enabled); Say(enabled ? "已穿透鼠标。Ctrl+Alt+L 或托盘可恢复。" : "可以继续找我玩啦。", 4000); }
    public bool IsClickThrough => clickThrough;
    public void OpenSettings()
    {
        if (settings is not null) { settings.Activate(); return; }
        settings = new SettingsWindow(this); settings.Closed += (_, _) => settings = null; settings.Show();
    }
    public void ToggleVisible()
    {
        CancelInput(); ClearTransient(); SetAction("idle");
        if (IsVisible) { Hide(); timer.Stop(); } else { Show(); lastTick = Now; timer.Start(); Render(); }
    }
    private void Say(string message, double duration = 2800)
    { speech.Text = message; speechUntil = Now + duration; bubble.Visibility = Visibility.Visible; }
    private void SetAction(string next, double duration = 0, Action? completed = null)
    { action = next; actionStarted = Now; actionUntil = duration > 0 ? Now + duration : 0; onMotionEnd = completed; }
    public void Play(string next, string? message = null, double duration = 2600)
    {
        if (resting) RestorePet();
        CancelChoreography();
        menu.Children.Clear(); roaming = false; lastInteraction = Now;
        SetAction(next, duration);
        if (message is not null) Say(message, duration + 400);
        Render();
    }
    public void CheckIn()
    {
        day = DateOnly.FromDateTime(DateTime.Now);
        int before = State.BondLevel;
        if (!State.CheckIn(day)) { Say("今天已经吃过早饭啦，明天再一起。", 3200); return; }
        ClearTransient(); Play("meal", $"早安！已相伴 {State.CheckIns.Count} 天 · 连续 {State.Streak(day)} 天", 3500);
        prop = "food"; propStart = Now;
        if (State.BondLevel > before || new[] { 50, 150, 250, 300, 500, 1000 }.Contains(State.CheckIns.Count))
        { prop = "confetti"; Say($"我们成为「{State.BondName}」啦！", 4000); }
        Save(); settings?.RefreshStatus();
    }
    private void Touch(double fraction)
    {
        if (!State.CheckedIn(day)) { CheckIn(); return; }
        ClearTransient();
        if (fraction < .52) Play("headpat", "再摸摸～");
        else if (fraction < .73) Play("poke", "脸颊软软的，对吧？", 1700);
        else Play("tickle", "哈哈，好痒呀！", 1600);
    }
    private Point ScreenPoint(MouseEventArgs e)
    {
        var p = PointToScreen(e.GetPosition(this));
        return PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice.Transform(p) ?? p;
    }
    private void PetDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (hideJourney is not null) { ClearTransient(); Play("happy", "被你找到啦！", 2000); e.Handled = true; return; }
        if (roaming) { roaming = false; SetAction("idle"); Render(); }
        lastInteraction = Now;
        menu.Children.Clear(); sprite.Focus(); pressed = true; dragging = false;
        downScreen = ScreenPoint(e); downWindow = new Point(Left, Top); shakes.Start(downScreen.X, Now);
        sprite.CaptureMouse(); e.Handled = true;
    }
    private void PetMove(object sender, MouseEventArgs e)
    {
        if (!pressed) return;
        var screen = ScreenPoint(e); var delta = screen - downScreen;
        if (!dragging && delta.Length < 8) return;
        if (!dragging) { dragging = true; ClearTransient(); roaming = false; SetAction("pickup"); }
        Left = downWindow.X + delta.X; Top = downWindow.Y + delta.Y;
        shakes.Move(screen.X, Now);
        var motion = Now - shakes.LastReversal < 650 ? shakes.Motion : "pickup";
        if (action != motion) SetAction(motion);
        Render();
    }
    private void PetUp(object sender, MouseButtonEventArgs e)
    {
        if (!pressed) return;
        bool wasDrag = dragging; pressed = dragging = false; sprite.ReleaseMouseCapture();
        if (wasDrag)
        {
            Constrain(); Save();
            if (shakes.IsDizzy(Now))
            {
                bool faint = Now - lastDizzy < 13100; lastDizzy = Now;
                Play(faint ? "faint" : "dizzy", faint ? "让我躺一小会儿……" : "转圈圈了……", faint ? 4500 : 3100);
                prop = "stars"; propStart = Now;
            }
            else { Play("happy", "在这里陪你。", 1200); if (State.CheckedIn(day) && Math.Abs(Top + FloorY - WorkArea.Bottom) < 35 && !State.ReducedMotion) StartWalk(false); }
        }
        else if (dance is not null) TapDance();
        else Touch(e.GetPosition(sprite).Y / sprite.Height);
        e.Handled = true;
    }
    internal Rect WorkArea => desktop?.WorkArea(this) ?? SystemParameters.WorkArea;
    private void Constrain()
    {
        var area = WorkArea;
        Left = Math.Clamp(Left, area.Left - CenterX + State.Size * .46, area.Right - CenterX - State.Size * .46);
        Top = Math.Clamp(Top, area.Top - PetTop + 90, area.Bottom - FloorY);
    }
    private void CancelInput()
    {
        bool hadDrag = dragging;
        pressed = dragging = holdingBall = false;
        if (sprite.IsMouseCaptured) sprite.ReleaseMouseCapture();
        if (ball.IsMouseCaptured) ball.ReleaseMouseCapture();
        if (hadDrag) { Constrain(); SetAction("idle"); if (!closing) Save(); }
    }
    private void ClearTransient()
    { CancelChoreography(); roaming = false; menu.Children.Clear(); effects.Children.Clear(); prop = null; flyingBall = holdingBall = false; ball.Visibility = Visibility.Collapsed; bubble.Visibility = Visibility.Collapsed; onMotionEnd = null; }

    private void CancelChoreography()
    {
        if (hideJourney is not null)
        {
            // Use the departure monitor: the mostly hidden HWND may overlap its neighbour.
            Left = Math.Clamp(Left, hideArea.Left - CenterX + State.Size * .46, hideArea.Right - CenterX - State.Size * .46);
            Top = hideArea.Bottom - FloorY;
            hideJourney = null; surface.Clip = null;
        }
        dance = null; danceFeedbackBeat = null;
        if (danceVisual is not null) { surface.Children.Remove(danceVisual); danceVisual = null; }
        sprite.Opacity = State.Opacity; sprite.RenderTransform = facing;
    }

    internal void ShowMenu(string group = "root", bool keyboard = false)
    {
        ClearTransient(); roaming = false; lastInteraction = Now; SetAction("idle"); Render();
        var entries = PetActions.Menu(group, CanDance);
        var area = WorkArea;
        var positions = RadialMenu.Place(entries.Length, new MenuPoint(CenterX, PetTop + State.Size * .5), State.Size,
            new MenuBounds(Math.Max(0, area.Left - Left), Math.Max(0, area.Top - Top), Math.Min(Width, area.Right - Left), Math.Min(Height, area.Bottom - Top)));
        var buttons = new List<Button>();
        foreach (var (entry, i) in entries.Select((entry, i) => (entry, i)))
        {
            var button = new Button { Content = CloudTheme.Icon(entry.Icon, 23), ToolTip = entry.Title,
                Style = (Style)FindResource("RadialAction"), Foreground = entry.Key.StartsWith("group:") ? CloudTheme.Blue : CloudTheme.Ink };
            AutomationProperties.SetName(button, entry.Title);
            ToolTipService.SetInitialShowDelay(button, 300); ToolTipService.SetShowDuration(button, 2500);
            button.Click += (_, _) =>
            {
                if (entry.Key.StartsWith("group:")) ShowMenu(entry.Key[6..], keyboard || (button.IsKeyboardFocused && !button.IsMouseOver));
                else if (entry.Key == "close") { menu.Children.Clear(); sprite.Focus(); }
                else if (entry.Key == "settings") { menu.Children.Clear(); OpenSettings(); }
                else RunInteraction(entry.Key);
            };
            button.PreviewKeyDown += (_, e) =>
            {
                if (e.Key is Key.Left or Key.Up or Key.Right or Key.Down)
                { buttons[(i + (e.Key is Key.Left or Key.Up ? -1 : 1) + buttons.Count) % buttons.Count].Focus(); e.Handled = true; }
                else if (e.Key == Key.Back) { ShowMenu("root", true); e.Handled = true; }
            };
            buttons.Add(button); menu.Children.Add(button);
            Canvas.SetLeft(button, positions[i].X - RadialMenu.ButtonSize / 2); Canvas.SetTop(button, positions[i].Y - RadialMenu.ButtonSize / 2);
        }
        if (keyboard) buttons[0].Focus();
    }
    public void RunInteraction(string key)
    {
        CancelInput(); ClearTransient();
        switch (key)
        {
            case "checkin": CheckIn(); break;
            case "snack": Snack(); break;
            case "ball": TakeBall(); break;
            case "blocks": BuildBlocks(); break;
            case "walk": StartWalk(true); break;
            case "rest": Rest(); break;
            case "peek": Peek(); break;
            case "peek-left": Peek(-1); break;
            case "peek-right": Peek(1); break;
            case "dance": StartDance(); break;
            case "nudge": NudgeBall(); break;
            case "letter": Celebrate($"相伴 {State.CheckIns.Count} 天"); break;
            default: Play(key); break;
        }
    }
    public void StopInteraction() { CancelInput(); ClearTransient(); SetAction("idle"); lastInteraction = Now; Render(); }
    private void Snack() { ClearTransient(); Play("eat", "啊呜，点心真好吃！", 2600); prop = "food"; propStart = Now; }
    private void BuildBlocks() { ClearTransient(); Play("think", "一块、两块……搭一座小塔！", 3300); prop = "blocks"; propStart = Now; onMotionEnd = () => Play("kick", "嘿！积木滚开啦。", 2300); }
    private void Celebrate(string label) { ClearTransient(); Play("chat", label + "。这封信送给你 ♡", 5200); prop = "letter"; propStart = Now; }
    private void Peek() => BeginHide();
    private void Peek(int side) => BeginHide(side);
    internal void BeginHide(int? side = null)
    {
        ClearTransient(); if (resting) RestorePet();
        if (State.ReducedMotion) { SetAction("idle"); Say("减少动态效果已开启，先在这里陪你。", 2600); return; }
        if (!CanWalk) { SetAction("idle"); Say("这套外观还没有行走动画，暂时不能走到边边躲藏。", 3200); return; }
        hideArea = WorkArea; Top = hideArea.Bottom - FloorY; Constrain();
        var clip = Character.Resolve(State.Outfit, "walk", 0).Sprite;
        hideJourney = new EdgeHide(Left + CenterX, hideArea.Left, hideArea.Right, State.Size, DesktopWalk.Speed(State.Size, clip) * 1.7, side);
        hideStarted = Now; direction = hideJourney.Side; SetAction("walk"); lastInteraction = Now;
        Say("我要去边边藏好，等你来找。", 2200); Render();
    }
    private void StartDance()
    {
        ClearTransient(); SetAction("idle");
        if (!CanDance || State.ReducedMotion) { Render(); return; }
        if (resting) RestorePet();
        var art = Character.Resolve(State.Outfit, "idle", 0);
        danceVisual = new DanceVisual(Art.Frame(Character, art.Sprite, art.Frame), Character.FamilyId, State.Outfit);
        surface.Children.Insert(surface.Children.IndexOf(sprite) + 1, danceVisual);
        dance = new PetDance(); lastInteraction = Now;
        SetAction("dance", PetDance.DurationMs, CancelChoreography);
        if (IsHitTestVisible) Activate(); sprite.Focus(); Render();
    }
    internal void TapDance()
    {
        if (dance is not null && dance.Tap(Now - actionStarted)) danceFeedbackBeat = PetDance.BeatAt(Now - actionStarted);
    }
    private void Rest()
    {
        ClearTransient(); Play("farewell", "我去歇一会儿，想我就点月亮。", 3200);
        onMotionEnd = () => { resting = true; sprite.Visibility = Visibility.Collapsed; restore.Visibility = Visibility.Visible; bubble.Visibility = Visibility.Collapsed; nextPeek = Now + Random.Shared.Next(1, 4) * 60000; SetAction("idle"); };
    }
    private void RestorePet()
    {
        resting = false; restore.Visibility = Visibility.Collapsed; sprite.Visibility = Visibility.Visible;
        Play("chat", "我回来啦。", 2700);
    }
    internal bool CanWalk => Character.CanWalk(State.Outfit);
    internal void StartWalk(bool explore, int? initialDirection = null)
    {
        if (State.ReducedMotion) { Say("已开启减少动态效果。", 2500); return; }
        if (!State.CheckedIn(day)) { Say("先点我打卡吃早饭，再一起散步吧。", 3000); return; }
        var clip = Character.Resolve(State.Outfit, "walk", 0).Sprite;
        if (!CanWalk || clip.Columns * clip.Rows < 2) { Say("这套外观目前是静态立绘，逐帧行走动作尚未制作。", 3500); return; }
        ClearTransient(); if (resting) RestorePet();
        Top = WorkArea.Bottom - FloorY; Constrain(); roaming = true; direction = initialDirection ?? (Random.Shared.Next(2) == 0 ? -1 : 1);
        roamDeadline = Now + (explore ? 9000 : 15000); SetAction("walk"); lastInteraction = Now;
        if (explore) { prop = "explore"; propStart = Now; Say("去桌边找点小惊喜。", 2200); }
        Render();
    }
    private void TakeBall()
    {
        ClearTransient(); Play("chat", "拖动小球后松开，抛给我吧。", 2000);
        ballX = CenterX - State.Size * .7; ballY = FloorY - 30; ballVx = ballVy = 0; ball.Visibility = Visibility.Visible; PlaceBall();
    }
    private void NudgeBall() { TakeBall(); ballVx = -90; ballVy = -60; flyingBall = true; ballHit = true; ballAge = 0; Play("poke", "悄悄推走～", 1800); }
    private void BallMove(object sender, MouseEventArgs e)
    {
        if (!holdingBall) return;
        var p = e.GetPosition(this); double dt = Math.Max(1, Now - ballSampleTime) / 1000;
        ballVx = Math.Clamp((p.X - ballPrevious.X) / dt, -1800, 1800); ballVy = Math.Clamp((p.Y - ballPrevious.Y) / dt, -1800, 1800);
        ballX = Math.Clamp(p.X - 15, 0, Width - 30); ballY = Math.Clamp(p.Y - 15, 0, FloorY - 30);
        ballPrevious = p; ballSampleTime = Now; PlaceBall(); e.Handled = true;
    }
    private void PlaceBall() { Canvas.SetLeft(ball, ballX); Canvas.SetTop(ball, ballY); }
    private void Tick()
    {
        double now = Now, dt = Math.Clamp((now - lastTick) / 1000, 0, .05); lastTick = now;
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (day != today) { day = today; State.LastSeen = day.ToString("yyyy-MM-dd"); Save(); }
        if (actionUntil > 0 && now >= actionUntil)
        {
            var completion = onMotionEnd; onMotionEnd = null; actionUntil = 0; SetAction("idle"); completion?.Invoke();
        }
        if (now > speechUntil) bubble.Visibility = Visibility.Collapsed;
        if (dragging && now - shakes.LastReversal >= 650 && action != "pickup") SetAction("pickup");
        if (hideJourney is { } journey)
        {
            var pose = journey.At((now - hideStarted) / 1000);
            Left = pose.Center - CenterX; Top = hideArea.Bottom - FloorY; direction = pose.Direction;
            string motion = pose.Walking ? "walk" : "idle";
            if (action != motion) SetAction(motion);
            surface.Clip = new RectangleGeometry(new Rect(hideArea.Left - Left, hideArea.Top - Top, hideArea.Width, hideArea.Height));
            if (pose.Phase != HidePhase.Approach) bubble.Visibility = Visibility.Collapsed;
            if (pose.Phase == HidePhase.Complete) { hideJourney = null; surface.Clip = null; SetAction("idle"); Say("我回来啦，有没有找到我？", 2200); lastInteraction = now; Save(); }
        }
        if (roaming && menu.Children.Count == 0)
        {
            var area = WorkArea;
            var clip = Character.Resolve(State.Outfit, "walk", now - actionStarted).Sprite;
            var step = DesktopWalk.Step(Left + CenterX, direction, DesktopWalk.Speed(State.Size, clip) * dt, area.Left + State.Size * .46, area.Right - State.Size * .46);
            Left = step.Center - CenterX; direction = step.Direction;
            Top = area.Bottom - FloorY;
            if (now >= roamDeadline)
            {
                roaming = false;
                if (prop == "explore")
                {
                    string[] treasures = ["一枚贝壳", "一颗小星星", "一片四叶草", "一块圆石头"];
                    string treasure = treasures[Random.Shared.Next(treasures.Length)]; State.Treasures.Add(treasure);
                    prop = "treasure"; propStart = Now; Play("headpat", $"捡到{treasure}，送给你！", 3200); Save();
                }
                else SetAction("idle");
                lastInteraction = now;
            }
        }
        if (action == "idle" && hideJourney is null && !pressed && !resting && menu.Children.Count == 0)
        {
            if (!State.CheckedIn(day)) SetAction("sleep");
            else if (State.Wander && CanWalk && !State.ReducedMotion && now - lastInteraction > 45000 && Math.Abs(Top + FloorY - WorkArea.Bottom) < 28) StartWalk(false);
        }
        if (resting && now >= nextPeek)
        {
            nextPeek = now + Random.Shared.Next(1, 4) * 60000;
            if (!State.ReducedMotion) { sprite.Visibility = Visibility.Visible; SetAction("peek", 2900, () => sprite.Visibility = Visibility.Collapsed); }
        }
        if (flyingBall)
        {
            double x0 = ballX + 15, y0 = ballY + 15;
            ballVy += 850 * dt; ballX += ballVx * dt; ballY += ballVy * dt; ballAge += dt;
            if (ballY > FloorY - 30) { ballY = FloorY - 30; ballVy = -Math.Abs(ballVy) * .6; ballVx *= .86; }
            if (ballX < 0 || ballX > Width - 30) { ballX = Math.Clamp(ballX, 0, Width - 30); ballVx *= -.7; }
            if (!ballHit && BallPhysics.Hit(x0, y0, ballX + 15, ballY + 15, CenterX, PetTop + State.Size * .6, State.Size * .28 + 15))
            { ballHit = true; Play("ball-hit", "哎呀！接到啦。", 1800); ballVx *= -.65; ballVy = -210; }
            if (ballAge > 4) { flyingBall = false; ball.Visibility = Visibility.Collapsed; if (!ballHit) Play("ball-miss", "差一点，下次再来！", 2000); }
            PlaceBall();
        }
        Render(); DrawEffects(now);
        bool moving = dragging || holdingBall || flyingBall || roaming || hideJourney is not null || prop is not null || actionUntil > 0;
        timer.Interval = TimeSpan.FromMilliseconds(moving ? 33 : 200);
    }
    private void Render()
    {
        var art = Character.Resolve(State.Outfit, dance is null ? action : "idle", (Now - actionStarted) * (hideJourney is not null && action == "walk" ? 1.7 : 1), State.ReducedMotion);
        var frame = Art.Frame(Character, art.Sprite, art.Frame);
        sprite.Source = frame;
        double size = State.Size;
        if (action == "walk")
        {
            // Match upright portraits to their walk; Q outfits keep the original walk's body scale.
            var reference = Character.Atlas.Columns * Character.Atlas.Rows == 1
                ? Character.Resolve(State.Outfit, "idle", 0).Sprite : Character.MotionFor("original", "walk");
            if (reference is not null)
            {
                var calibration = Art.Frame(Character, reference, 0);
                size *= Math.Clamp(Art.SheetHeight(calibration) / Math.Max(.1, Art.SheetHeight(frame)), .7, 1.5);
            }
        }
        sprite.Width = sprite.Height = size;
        groundLine = action == "walk" || Character.Demo ? Art.GroundLine(frame) : .875;
        Canvas.SetTop(sprite, PetTop); Canvas.SetTop(bubble, Math.Max(8, PetTop - 78));
        sprite.RenderTransformOrigin = new Point(.5, .5);
        facing.ScaleX = action is "walk" or "peek" ? DesktopWalk.ScaleX(direction, art.Sprite.Facing) : 1;
        Canvas.SetLeft(sprite, CenterX - size * (.5 + (action == "walk" ? facing.ScaleX * (Art.HorizontalAnchor(frame) - .5) : 0)));
        sprite.RenderTransform = facing;
        if (danceVisual is not null)
        {
            sprite.Opacity = 0;
            danceVisual.Width = danceVisual.Height = size; danceVisual.Opacity = State.Opacity;
            Canvas.SetLeft(danceVisual, CenterX - size / 2); Canvas.SetTop(danceVisual, PetTop);
            danceVisual.Update(Now - actionStarted);
        }
    }
    private void DrawEffects(double now)
    {
        effects.Children.Clear();
        if (dance is not null)
        {
            int beat = PetDance.BeatAt(now - actionStarted);
            for (int i = 0; i < 4; i++)
            {
                var dot = new Ellipse { Width = i == beat % 4 ? 9 : 5, Height = i == beat % 4 ? 9 : 5, Fill = i == beat % 4 ? (danceFeedbackBeat == beat ? CloudTheme.Brush("#34C759") : CloudTheme.Blue) : CloudTheme.Brush("#C3D1E3") };
                effects.Children.Add(dot); Canvas.SetLeft(dot, CenterX - 30 + i * 18); Canvas.SetTop(dot, PetTop - 10 - (i == beat % 4 ? 2 : 0));
            }

        }
        if (prop is null) return;
        double t = (now - propStart) / 1000;
        if (t > 6) { prop = null; return; }
        void Label(string text, double x, double y, double size, Brush? color = null)
        {
            var item = new TextBlock { Text = text, FontSize = size, Foreground = color ?? Brushes.Peru, FontFamily = new FontFamily("Segoe UI Emoji"), Opacity = Math.Clamp(6 - t, 0, 1) };
            effects.Children.Add(item); Canvas.SetLeft(item, x); Canvas.SetTop(item, y);
        }
        if (prop == "food" && t < 2.5) Label("🥐", CenterX + 30 - Math.Min(t, 1) * 25, PetTop + State.Size * .57, 29);
        if (prop == "stars") for (int i = 0; i < 3; i++) Label("✦", CenterX - 45 + i * 38, PetTop + 14 + Math.Sin(t * 3 + i) * 8, 23, Brushes.Goldenrod);
        if (prop == "blocks")
            for (int i = 0; i < Math.Min(5, (int)(t / .45) + 1); i++)
            {
                double kick = Math.Clamp((t - 3.3) * 1.6, 0, 1);
                var block = new Rectangle { Width = 23, Height = 23, RadiusX = 4, RadiusY = 4, Fill = new SolidColorBrush(new[] { Color.FromRgb(225, 159, 130), Color.FromRgb(139, 171, 154), Color.FromRgb(164, 161, 200) }[i % 3]), Opacity = Math.Clamp(6 - t, 0, 1), RenderTransform = new RotateTransform(kick * (i % 2 == 0 ? 45 : -60)) };
                effects.Children.Add(block); Canvas.SetLeft(block, CenterX + State.Size * .45 + (i % 2) * 24 + kick * (i - 2) * 18); Canvas.SetTop(block, FloorY - 24 - (1 - kick) * i * 23);
            }
        if (prop == "treasure") Label("✧", CenterX + State.Size * .45, FloorY - 44, 38, Brushes.Goldenrod);
        if (prop == "letter")
        {
            var letter = new Border { Width = 200, Background = new SolidColorBrush(Color.FromRgb(255, 248, 230)), BorderBrush = Brushes.Tan, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(12), Child = new TextBlock { Text = t < 1 ? "✉ 给你的信" : $"♡  谢谢你一直在\n{day:yyyy.MM.dd}\n{State.BondName} · {Character.Name}", TextAlignment = TextAlignment.Center, FontSize = 13, LineHeight = 22 }, Opacity = Math.Clamp(6 - t, 0, 1) };
            effects.Children.Add(letter); Canvas.SetLeft(letter, CenterX - 100); Canvas.SetTop(letter, PetTop - 100);
            bubble.Visibility = Visibility.Collapsed;
        }
        if (prop == "confetti") for (int i = 0; i < 18; i++) Label(i % 2 == 0 ? "•" : "✦", CenterX - 120 + i * 14, PetTop - 50 + (t * 40 + i * 9) % 150, 15, i % 2 == 0 ? Brushes.Salmon : Brushes.Goldenrod);
    }
}

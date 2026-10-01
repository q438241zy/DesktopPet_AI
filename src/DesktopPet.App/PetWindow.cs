using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
    private readonly InteractionFeedback effects = new() { Width = 560, Height = 680, IsHitTestVisible = false };
    private readonly Canvas menu = new();
    private readonly TextBlock speech = new() { TextWrapping = TextWrapping.Wrap, FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(66, 56, 52)) };
    private readonly Border bubble;
    private readonly ItemVisual ball = new() { Cursor = Cursors.Hand, RenderTransformOrigin = new Point(.5, .5) };
    private readonly RotateTransform ballSpin = new();
    private readonly ItemDrawBag toys = new(Collectibles.Sports), finds = new(Collectibles.All), snacks = new(Collectibles.Food);
    private Collectible activeToy = Collectibles.Get("basketball"), activeFood = Collectibles.Get("bread");
    private Collectible? activePrize;
    internal Collectible ActiveToy => activeToy;
    internal Collectible? LatestFind => activePrize;
    internal bool IsResting => resting;
    internal bool IsExploring => exploring;
    internal RigVisual? ActiveMotion => danceVisual;
    internal string EffectKey => effects.EffectKey;
    internal bool DrawsExtraFood => effects.DrawsFood;
    internal bool DrawsExtraHammer => effects.DrawsHammer;
    internal bool HasDizzyStars => effects.DrawsDizzyStars;
    internal bool UsingDrawnAction { get; private set; }
    internal int DrawnFrame { get; private set; }
    internal Point? HandTarget { get; private set; }
    internal bool HasCaughtBall => caughtBall;
    internal bool BallWasHit => ballHit;
    internal bool ToyInFlight => flyingBall;
    internal double AirborneOffset { get; private set; }
    internal bool IsDropping => dropping;
    internal bool LiftedFromTaskbar => liftedFromTaskbar;
    internal InlineChat? ActiveChat => chatWindow?.Visibility == Visibility.Visible ? chatWindow : null;
    internal InlineChat Chat => chatWindow ??= CreateChat();
    private bool bakedProps;
    private double handSpan;
    private double BallSize => activeToy.Diameter;
    private readonly DispatcherTimer timer;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly ScaleTransform facing = new(1, 1);
    private readonly WalkPlayback walkPlayback = new();
    private readonly TranslateTransform walkOffset = new();
    private double walkCenter;
    private bool renderingWalk;
    private TimeSpan lastPresentation = TimeSpan.MinValue;
    private readonly ShakeTracker shake = new();
    private bool liftActive;
    private double dizzyStarted, dizzyUntil, observedReversal = double.NegativeInfinity;
    private RigVisual? danceVisual;
    private EdgeHide? hideJourney;
    private Rect hideArea;
    private double hideStarted;
    private PetDance? dance;
    private int? danceFeedbackBeat;
    internal HidePhase? HideStage => hideJourney?.At((Now - hideStarted) / 1000).Phase;
    internal bool IsDancing => dance is not null;
    internal RigVisual? ActiveDance => dance is null ? null : danceVisual;
    internal bool CanDance => PortraitRig.SupportsDance(Character.Category, Character.FamilyId) && Character.MotionFor(State.Outfit,"dance")?.DanceRig is not null;
    internal string CurrentAction => action;
    private DesktopHost? desktop;
    private SettingsWindow? settings;
    private InlineChat? chatWindow;
    private bool conversationActive, liftedFromTaskbar, dropping;
    private double dropSpeed;
    private Action? afterDrop;
    private string action = "idle";
    private double actionStarted, actionUntil, speechUntil, lastTick, lastInteraction;
    private Action? onMotionEnd;
    private bool dragging, pressed, resting, roaming, exploring, closing, holdingBall, flyingBall, ballHit, caughtBall, clickThrough;
    private Point downScreen, downWindow, ballPrevious;
    private double ballX, ballY, ballVx, ballVy, ballSampleTime, ballStarted, roamDeadline;
    private int direction = -1;
    private string? prop;
    private double propStart;
    private DateOnly day;
    private const double CenterX = 280, FloorY = 468;
    private double groundLine = .875;
    private double PetTop => FloorY - (double.IsFinite(sprite.Height) ? sprite.Height : State.Size) * groundLine;
    private double? previewClock;
    private double Now => previewClock ?? clock.Elapsed.TotalMilliseconds;
    internal void BeginPreview() { previewClock = 0; UpdateWalkClock(); timer.Stop(); IsHitTestVisible = false; }
    internal void EndPreview() { previewClock = null; lastTick = Now; StopInteraction(); if (!closing && IsVisible) timer.Start(); }
    internal double WalkPhaseMilliseconds => walkPlayback.Milliseconds;
    internal bool WalkUsesRendering => renderingWalk;
    internal void AdvancePreview(double elapsed) { previewClock = elapsed; Tick(); }
    internal void PreviewMotion(string motion, double elapsed, int duration)
    {
        previewClock = elapsed; action = motion == "pickup-dizzy" ? "pickup" : motion; actionStarted = 0; actionUntil = duration;
        dizzyStarted = 0; dizzyUntil = motion == "pickup-dizzy" ? duration : 0;
        direction = 1;
        sprite.Visibility = Visibility.Visible;
        prop = motion is "eat" or "meal" ? "food" : motion == "build" ? "blocks" : null;
        activeFood = Collectibles.Get(motion == "meal" ? "rice" : "bread"); propStart = 0;
        bubble.Visibility = Visibility.Collapsed;
        if (motion == "jump" && elapsed >= 880 && Character.MotionFor(State.Outfit,"land") is not null)
        { action = "land"; actionStarted = 880; }
        ball.Visibility = motion == "ball-ready" ? Visibility.Visible : Visibility.Collapsed;
        if (ball.Visibility == Visibility.Visible) { ball.Item = activeToy; ball.Width = ball.Height = BallSize; ballX=CenterX-State.Size*.45-BallSize/2; ballY=FloorY-BallSize; PlaceBall(); }
        Render();
    }
    internal void PreviewDance(double elapsed)
    { previewClock=elapsed; dance ??= new PetDance(); action="dance"; actionStarted=0; actionUntil=PetDance.DurationMs; Render(); }

    public PetWindow(StateStore store, Catalog catalog)
    {
        this.store = store; Catalog = catalog; State = store.Load();
        State.MigrateCharacters(Catalog.LegacyAliases);
        State.Character = catalog.Find(State.Character).Id;
        Title = "DesktopPet · 桌边伙伴"; Icon = CloudTheme.AppIcon; Width = 560; Height = 680;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; AllowsTransparency = true;
        Background = Brushes.Transparent; ShowInTaskbar = false; Topmost = State.Topmost;
        Content = surface;
        surface.RenderTransform = walkOffset;
        sprite.RenderTransform = facing;
        ball.RenderTransform = ballSpin;
        surface.Children.Add(sprite); surface.Children.Add(effects); surface.Children.Add(menu);
        bubble = new Border { Background = CloudTheme.Cream, BorderBrush = CloudTheme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(20), Padding = new Thickness(15, 10, 15, 10), Width = 238, Child = speech, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        surface.Children.Add(bubble); surface.Children.Add(ball);
        ball.Visibility = Visibility.Collapsed;
        AutomationProperties.SetName(sprite, "桌面宠物：点击摸头，拖动抱起，松手放置，按住 Shift 松手下落，来回摇晃会头晕，右键互动");
        sprite.MouseLeftButtonDown += PetDown; sprite.MouseMove += PetMove; sprite.MouseLeftButtonUp += PetUp;
        sprite.MouseRightButtonUp += (_, e) => { if (menu.Children.Count > 0) menu.Children.Clear(); else ShowMenu(); e.Handled = true; };
        sprite.LostMouseCapture += (_, _) => { if (pressed) CancelInput(); };
        sprite.KeyDown += (_, e) => { if (e.Key is Key.Enter or Key.Space) { if (dance is not null) TapDance(); else Touch(.3); e.Handled = true; } else if (e.Key == Key.F10 && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) { ShowMenu("root", true); e.Handled = true; } };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { StopInteraction(); e.Handled = true; } };
        ball.MouseLeftButtonDown += (_, e) =>
        {
            double centerX = ballX + ball.Width / 2, centerY = ballY + ball.Height / 2;
            caughtBall = false; ball.Width = ball.Height = BallSize; ballX = centerX - BallSize / 2; ballY = centerY - BallSize / 2;
            holdingBall = true; flyingBall = false; ballPrevious = e.GetPosition(this); ballSampleTime = Now; ballVx = ballVy = 0; ball.CaptureMouse(); PlaceBall(); e.Handled = true;
        };
        ball.MouseMove += BallMove;
        ball.MouseLeftButtonUp += (_, e) => { if (!holdingBall) return; holdingBall = false; ball.ReleaseMouseCapture(); ThrowToy(ballX, ballY, ballVx, ballVy); e.Handled = true; };
        ball.LostMouseCapture += (_, _) => { if (holdingBall) CancelInput(); };
        Deactivated += (_, _) => { CancelInput(); menu.Children.Clear(); };
        SourceInitialized += (_, _) => InitializeDesktop();
        Loaded += (_, _) => Welcome();
        RenderOptions.SetBitmapScalingMode(sprite, BitmapScalingMode.HighQuality);
        timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) => Tick(); timer.Start();
        Closed += (_, _) => { closing = true; UpdateWalkClock(); timer.Stop(); CancelInput(); CancelChoreography(); chatWindow?.Dispose(); settings?.Close(); desktop?.Dispose(); Save(); };
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
        else if (!State.CheckedIn(day)) { SetAction("idle"); Render(); Say("点我打卡，一起吃早饭 ☀", 5500); }
        else Play("chat", "今天也陪你一起。右键找我玩。", 2600);
        if (actionUntil > 0) onMotionEnd += ResumeFloorWalk;
        else ResumeFloorWalk();
        if (store.Warning is { } warning) Say(warning, 6500);
    }
    public void Save()
    {
        State.Left = hideJourney is { } journey ? journey.RestingCenter - CenterX : Left + walkOffset.X; State.Top = Top;
        try { store.Save(State); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Say("存档未能保存：" + ex.Message, 6500); }
    }
    public void SelectCharacter(string id)
    {
        var selected = Catalog.FindExact(id);
        if (selected is null) { Say("这个形象暂不可用，请重新选择。", 3000); return; }
        bool keepChat = ActiveChat is not null;
        CancelInput(); ClearTransient(keepChat); resting = false;
        sprite.Visibility = Visibility.Visible;
        State.Character = selected.Id; ApplySettings(false); Save();
        chatWindow?.SetCompanion();
        if (keepChat) { conversationActive = true; SetAction(Chat.IsThinking ? "thinking" : "listen"); Render(); }
        else Play("chat", $"你好，我是{Character.Name}。", 2600);
    }
    public void SelectStyle(string category)
    {
        category = CharacterStyles.Normalize(category);
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
    internal void OpenChat()
    {
        if (clickThrough) { clickThrough = false; desktop?.ClickThrough(false); }
        ClearTransient(); conversationActive = true; SetAction("listen"); Render();
        Chat.Open(); LayoutChat(true);
    }
    private InlineChat CreateChat() { var chat = new InlineChat(this); surface.Children.Add(chat); Panel.SetZIndex(chat, 30); return chat; }
    internal void LayoutChat(bool makeRoom = false)
    {
        if (ActiveChat is not { } chat) return;
        chat.Measure(new Size(chat.Width, double.PositiveInfinity));
        double height = chat.DesiredSize.Height, head = PetTop - AirborneOffset;
        var area = WorkArea;
        if (makeRoom) Top = Math.Min(area.Bottom - FloorY, Math.Max(Top, area.Top + height + 14 - head));
        Canvas.SetLeft(chat, Math.Clamp(CenterX - chat.Width / 2, Math.Max(4, area.Left - Left + 4), Math.Max(4, Math.Min(Width - chat.Width - 4, area.Right - Left - chat.Width - 4))));
        Canvas.SetTop(chat, Math.Max(Math.Max(4, area.Top - Top + 4), head - height - 12));
    }
    internal void ConversationThinking() { ClearTransient(true); conversationActive = true; SetAction("thinking"); Render(); }
    internal void ConversationListen() { if (conversationActive) { SetAction("listen"); Render(); } }
    internal void ConversationReply(string text)
    {
        if (!conversationActive) return;
        SetAction("chat", Math.Clamp(text.Length * 85, 1800, 6500), ConversationListen); Render();
    }
    internal void EndConversation() { if (conversationActive) { conversationActive = false; SetAction("idle"); Render(); } }
    public void ToggleVisible()
    {
        CancelInput(); ClearTransient(); SetAction("idle");
        if (IsVisible) { Hide(); UpdateWalkClock(); timer.Stop(); } else { Show(); lastTick = Now; timer.Start(); Render(); }
    }
    private void Say(string message, double duration = 2800)
    { speech.Text = message; speechUntil = Now + duration; bubble.Visibility = Visibility.Visible; }
    private void SetAction(string next, double duration = 0, Action? completed = null)
    { action = next; actionStarted = Now; actionUntil = duration > 0 ? Now + duration : 0; onMotionEnd = completed; if (next == "walk") walkPlayback.Reset(); timer.Interval = TimeSpan.FromMilliseconds(16); }
    private void UpdateWalkClock()
    {
        bool useRendering = !closing && previewClock is null && IsVisible && (roaming || hideJourney is not null || dance is not null);
        if (useRendering == renderingWalk) return;
        renderingWalk = useRendering; lastTick = Now; lastPresentation = TimeSpan.MinValue;
        if (useRendering) { timer.Stop(); CompositionTarget.Rendering += RenderWalk; }
        else
        {
            CompositionTarget.Rendering -= RenderWalk;
            if (!closing && previewClock is null && IsVisible) timer.Start();
        }
    }
    private void RenderWalk(object? sender, EventArgs e)
    {
        if (e is not RenderingEventArgs frame || frame.RenderingTime == lastPresentation) return;
        lastPresentation = frame.RenderingTime;
        Tick();
    }
    private void PlaceWalk(double center)
    {
        // HWND coordinates are whole physical pixels. Integrate the logical
        // center independently and render the remainder inside the window.
        double desiredLeft = center - CenterX, dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        Left = Math.Round(desiredLeft * dpi) / dpi;
        walkOffset.X = desiredLeft - Left;
    }
    public void Play(string next, string? message = null, double duration = 2600)
    {
        resting = false; sprite.Visibility = Visibility.Visible;
        CancelChoreography();
        menu.Children.Clear(); roaming = false; lastInteraction = Now;
        SetAction(next, duration);
        if (message is not null) Say(message, duration + 400);
        Render();
    }
    public void CheckIn()
    {
        day = DateOnly.FromDateTime(DateTime.Now);
        int before = State.BondLevel; bool added = State.CheckIn(day);
        ClearTransient(); activeFood = Collectibles.Get("rice");
        Play("meal", added ? "开饭啦。" : "再吃一点。", PortraitMotion.Duration("meal"));
        prop = "food"; propStart = Now;
        if (added && (State.BondLevel > before || new[] { 50, 150, 250, 300, 500, 1000 }.Contains(State.CheckIns.Count)))
        { prop = "confetti"; Say($"我们成为「{State.BondName}」啦！", 4000); }
        Save(); settings?.RefreshStatus();
    }
    internal void Touch(double fraction)
    {
        if (resting) { RestorePet(); return; }
        if (!State.CheckedIn(day)) { CheckIn(); return; }
        ClearTransient();
        string motion = PortraitMotion.TouchRegion(Character.Category, fraction);
        Play(motion, motion switch { "headpat" => "再摸摸～", "poke" => "软软的。", _ => "哈哈，好痒！" }, PortraitMotion.Duration(motion));
    }
    private Point ScreenPoint(MouseEventArgs e)
    {
        var p = PointToScreen(e.GetPosition(this));
        return PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice.Transform(p) ?? p;
    }
    private void PetDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (resting) { RestorePet(); e.Handled = true; return; }
        if (hideJourney is not null) { ClearTransient(); Play("happy", "被你找到啦！", 2000); e.Handled = true; return; }
        if (dropping) { CancelChoreography(); Render(); Save(); }
        if (roaming) { roaming = false; SetAction("idle"); Render(); }
        lastInteraction = Now;
        menu.Children.Clear(); sprite.Focus(); pressed = true; dragging = false;
        downScreen = ScreenPoint(e); downWindow = new Point(Left + walkOffset.X, Top);
        sprite.CaptureMouse(); e.Handled = true;
    }
    private void PetMove(object sender, MouseEventArgs e)
    {
        if (!pressed) return;
        var screen = ScreenPoint(e); var delta = screen - downScreen;
        if (!dragging && delta.Length < 8) return;
        if (!dragging) { dragging = true; BeginLift(); }
        MoveLift(downWindow.X + delta.X, downWindow.Y + delta.Y);
        e.Handled = true;
    }
    internal void MoveLift(double left, double top)
    {
        if (liftActive)
        {
            shake.Move(left, top, Now);
            if (shake.IsDizzy(Now) && shake.LastReversal > observedReversal)
            {
                if (dizzyUntil <= Now) dizzyStarted = Now;
                dizzyUntil = Now + ShakeTracker.RecoveryMilliseconds;
                observedReversal = shake.LastReversal;
            }
        }
        walkOffset.X = 0; Left = left; Top = top;
        if (action != "pickup") SetAction("pickup");
        Render();
    }
    private void PetUp(object sender, MouseButtonEventArgs e)
    {
        if (!pressed) return;
        bool wasDrag = dragging, dropOnRelease = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        pressed = dragging = false; sprite.ReleaseMouseCapture();
        if (wasDrag)
        {
            ReleaseLift(dropOnRelease);
        }
        else if (dance is not null) TapDance();
        else Touch(e.GetPosition(sprite).Y / sprite.Height);
        e.Handled = true;
    }
    internal Rect WorkArea => desktop?.WorkArea(this) ?? SystemParameters.WorkArea;
    internal void BeginLift()
    {
        bool fromEdge = Math.Abs(Top + FloorY - WorkArea.Bottom) < 28;
        double liftedFor = action == "pickup" ? Math.Max(0, Now - actionStarted) : 0;
        ClearTransient(); liftedFromTaskbar = fromEdge; roaming = false; liftActive = true;
        shake.Start(Left, Top, Now); SetAction("pickup"); actionStarted -= liftedFor; Render();
    }
    internal void ReleaseLift(bool dropOnRelease = false)
    {
        Constrain();
        liftActive = false;
        dropping = liftedFromTaskbar = false; afterDrop = null;
        bool atFloor = WorkArea.Bottom - FloorY - Top <= 24;
        if (atFloor) Top = WorkArea.Bottom - FloorY;
        if (dropOnRelease && !atFloor)
        {
            ResetShake(); DropToFloor();
        }
        else if (dizzyUntil > Now) { Play("dizzy", duration: dizzyUntil - Now); if (atFloor) onMotionEnd = ResumeFloorWalk; }
        else if (atFloor) { Play("land", duration: 410); onMotionEnd = ResumeFloorWalk; }
        else { SetAction("idle"); Render(); }
        lastInteraction = Now; Save();
    }
    internal void DropToFloor(Action? then = null)
    {
        Constrain();
        if (State.ReducedMotion || WorkArea.Bottom - FloorY - Top < 1)
        {
            dropping = liftedFromTaskbar = false; afterDrop = null;
            Top = WorkArea.Bottom - FloorY; SetAction("idle"); Render(); Save();
            (then ?? ResumeFloorWalk).Invoke(); return;
        }
        dropping = true; dropSpeed = 30; liftedFromTaskbar = false; afterDrop = then;
        double liftedFor = action == "pickup" ? Math.Max(0, Now - actionStarted) : 0;
        SetAction("pickup"); actionStarted -= liftedFor; Render();
    }
    private void Constrain()
    {
        var area = WorkArea;
        Left = Math.Clamp(Left, area.Left - CenterX + State.Size * .46, area.Right - CenterX - State.Size * .46);
        Top = Math.Clamp(Top, area.Top - PetTop + 90, area.Bottom - FloorY);
    }
    private void CancelInput()
    {
        bool hadDrag = dragging;
        ResetShake();
        pressed = dragging = holdingBall = false;
        if (sprite.IsMouseCaptured) sprite.ReleaseMouseCapture();
        if (ball.IsMouseCaptured) ball.ReleaseMouseCapture();
        if (hadDrag) { dropping = liftedFromTaskbar = false; afterDrop = null; Constrain(); SetAction("idle"); if (!closing) Save(); }
    }
    private void ClearTransient(bool keepChat = false)
    { ResetShake(); CancelChoreography(); if (!keepChat) chatWindow?.Close(); resting = roaming = exploring = conversationActive = liftedFromTaskbar = false; sprite.Visibility = Visibility.Visible; menu.Children.Clear(); effects.Clear(); prop = null; flyingBall = holdingBall = caughtBall = false; ball.Visibility = Visibility.Collapsed; bubble.Visibility = Visibility.Collapsed; onMotionEnd = null; }

    private void ResetShake()
    { liftActive = false; dizzyUntil = 0; observedReversal = double.NegativeInfinity; shake.Start(0, 0, Now); }

    private void CancelChoreography()
    {
        dropping = false; afterDrop = null;
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
            var button = new Button { Content = new LineIcon { Glyph = entry.Icon, Soft = true, Width = 25, Height = 25, IsHitTestVisible = false, Focusable = false }, ToolTip = entry.Title,
                Style = (Style)FindResource("RadialAction") };
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
            case "jump":
                if (Character.Category != "chibi" && Character.MotionFor(State.Outfit, "land") is not null)
                {
                    Play("jump", duration: 880);
                    // Landing has its own clock, so a delayed desktop frame cannot
                    // skip straight from the airborne pose to standing idle.
                    onMotionEnd = () => Play("land", duration: 410);
                }
                else Play("jump", duration: PortraitMotion.Duration("jump"));
                break;
            case "chat": OpenChat(); break;
            case "peek": Peek(); break;
            case "peek-left": Peek(-1); break;
            case "peek-right": Peek(1); break;
            case "dance": StartDance(); break;
            case "nudge": TakeBall(); break;
            case "letter": Celebrate($"相伴 {State.CheckIns.Count} 天"); break;
            default: Play(key, duration: PortraitMotion.Duration(key)); break;
        }
    }
    public void StopInteraction() { CancelInput(); ClearTransient(); SetAction("idle"); lastInteraction = Now; Render(); }
    private void Snack() { ClearTransient(); activeFood = Character.MotionFor(State.Outfit, "eat")?.BakedProps == true ? Collectibles.Get("bread") : snacks.Draw(); Play("eat", Character.MotionFor(State.Outfit,"eat")?.BakedProps == true ? "啊呜，好吃。" : activeFood.Name + "，啊呜。", PortraitMotion.Duration("eat")); prop = "food"; propStart = Now; }
    private void BuildBlocks() { ClearTransient(); Play("build", duration: PortraitMotion.Duration("build")); prop = "blocks"; propStart = Now; onMotionEnd = () => prop = null; }
    private void Celebrate(string label) { ClearTransient(); Play("chat", label + "。这封信送给你 ♡", 5200); prop = "letter"; propStart = Now; }
    private void Peek() => BeginHide();
    private void Peek(int side) => BeginHide(side);
    internal void BeginHide(int? side = null)
    {
        ClearTransient();
        if (State.ReducedMotion) { SetAction("idle"); Say("减少动态效果已开启，先在这里陪你。", 2600); return; }
        if (!CanWalk) { SetAction("idle"); Say("这套外观还没有行走动画，暂时不能走到边边躲藏。", 3200); return; }
        if (!State.ReducedMotion && WorkArea.Bottom - FloorY - Top > 1) { DropToFloor(() => BeginHide(side)); return; }
        hideArea = WorkArea; Top = hideArea.Bottom - FloorY; Constrain();
        var clip = Character.Resolve(State.Outfit, "walk", 0).Sprite;
        hideJourney = new EdgeHide(Left + walkOffset.X + CenterX, hideArea.Left, hideArea.Right, State.Size, DesktopWalk.Speed(State.Size, clip) * 1.7, side);
        hideStarted = Now; direction = hideJourney.Side; SetAction("walk"); lastInteraction = Now;
        Say("我要去边边藏好，等你来找。", 2200); Render();
    }
    private void StartDance()
    {
        ClearTransient(); SetAction("idle");
        if (!CanDance || State.ReducedMotion) { Render(); return; }
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
        ClearTransient(); Play("curl", duration: 1500); resting = true;
        onMotionEnd = () => { SetAction("sleep"); bubble.Visibility = Visibility.Collapsed; };
    }
    private void RestorePet()
    {
        ClearTransient(); Play("farewell", "睡醒啦。", 1800);
        onMotionEnd = ResumeFloorWalk;
    }
    internal bool CanWalk => Character.CanWalk(State.Outfit);
    private void ResumeFloorWalk()
    {
        if (State.Wander && !State.ReducedMotion && CanWalk && action == "idle" && !pressed && !liftActive && !resting && !conversationActive && menu.Children.Count == 0 && Math.Abs(Top + FloorY - WorkArea.Bottom) < 1)
            StartWalk(false);
    }
    internal void StartWalk(bool explore, int? initialDirection = null)
    {
        if (State.ReducedMotion) { Say("已开启减少动态效果。", 2500); return; }
        var clip = Character.Resolve(State.Outfit, "walk", 0).Sprite;
        if (!CanWalk || clip.Columns * clip.Rows < 2) { Say("这套外观目前是静态立绘，逐帧行走动作尚未制作。", 3500); return; }
        ClearTransient();
        if (WorkArea.Bottom - FloorY - Top > 1) { DropToFloor(() => StartWalk(explore, initialDirection)); return; }
        Top = WorkArea.Bottom - FloorY; Constrain(); roaming = true; direction = initialDirection ?? (Random.Shared.Next(2) == 0 ? -1 : 1);
        walkCenter = Left + walkOffset.X + CenterX; lastTick = Now;
        exploring = explore; roamDeadline = Now + (explore ? 9000 : 15000); SetAction("walk"); lastInteraction = Now;
        if (explore) Say("去找点小惊喜。", 1800);
        Render();
    }
    private void TakeBall() => PlayWithToy(toys.Draw().Id);
    internal void PlayWithToy(string id)
    {
        var selected = Collectibles.All.FirstOrDefault(i => i.Id == id && i.Kind == ItemKind.Sport);
        if (selected is null) return;
        ClearTransient(); activeToy = selected; ball.Item = selected; ball.Width = ball.Height = BallSize; ballSpin.Angle = 0;
        Play("ball-ready", selected.Name + "，接着！", 2200);
        var area = WorkArea;
        double minX = Math.Max(0, area.Left - Left), maxX = Math.Min(Width, area.Right - Left);
        ballX = Math.Clamp(CenterX - State.Size * .45 - BallSize / 2, minX, Math.Max(minX, maxX - BallSize));
        ballY = FloorY - BallSize; ballVx = ballVy = 0; ball.Visibility = Visibility.Visible; PlaceBall();
    }
    internal void ThrowToy(double x, double y, double vx, double vy)
    {
        caughtBall = false; ball.Width = ball.Height = BallSize; ballX = x; ballY = y; ballVx = vx; ballVy = vy;
        flyingBall = true; ball.Visibility = Visibility.Visible; ballStarted = Now; ballHit = false; Play("anticipate", duration: 4300); PlaceBall();
    }
    private void FinishExploration()
    {
        exploring = false; var found = finds.Draw(); activePrize = found;
        State.Treasures.Add(found.Name); Play("happy", "找到" + found.Name + "！", 2700);
        prop = "treasure"; propStart = Now; Save(); settings?.RefreshLife();
    }
    private void BallMove(object sender, MouseEventArgs e)
    {
        if (!holdingBall) return;
        var p = e.GetPosition(this); double dt = Math.Max(1, Now - ballSampleTime) / 1000;
        ballVx = Math.Clamp((p.X - ballPrevious.X) / dt, -1800, 1800); ballVy = Math.Clamp((p.Y - ballPrevious.Y) / dt, -1800, 1800);
        var area = WorkArea;
        double minX = Math.Max(0, area.Left - Left), maxX = Math.Min(Width, area.Right - Left);
        double minY = Math.Max(0, area.Top - Top);
        ballX = Math.Clamp(p.X - BallSize / 2, minX, Math.Max(minX, maxX - BallSize));
        ballY = Math.Clamp(p.Y - BallSize / 2, minY, Math.Max(minY, FloorY - BallSize));
        ballPrevious = p; ballSampleTime = Now; PlaceBall(); e.Handled = true;
    }
    private void PlaceBall() { Canvas.SetLeft(ball, ballX); Canvas.SetTop(ball, ballY); }
    private void Tick()
    {
        double now = Now, walkDt = Math.Clamp((now - lastTick) / 1000, 0, .25), dt = Math.Min(walkDt, .05); lastTick = now;
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (day != today) { day = today; State.LastSeen = day.ToString("yyyy-MM-dd"); Save(); }
        if (actionUntil > 0 && now >= actionUntil)
        {
            var completion = onMotionEnd; onMotionEnd = null; actionUntil = 0; SetAction("idle"); completion?.Invoke();
        }
        if (now > speechUntil) bubble.Visibility = Visibility.Collapsed;
        if (dropping)
        {
            dropSpeed += 1050 * dt; Top = Math.Min(WorkArea.Bottom - FloorY, Top + dropSpeed * dt);
            if (Top + FloorY >= WorkArea.Bottom - .1)
            { var completion = afterDrop ?? ResumeFloorWalk; afterDrop = null; dropping = liftedFromTaskbar = false; Play("land", duration: 410); onMotionEnd = completion; Save(); }
        }
        if (dragging && action != "pickup") SetAction("pickup");
        if (hideJourney is { } journey)
        {
            var pose = journey.At((now - hideStarted) / 1000);
            PlaceWalk(pose.Center); Top = hideArea.Bottom - FloorY; direction = pose.Direction;
            string motion = pose.Walking ? "walk" : "idle";
            if (action != motion) SetAction(motion);
            surface.Clip = new RectangleGeometry(new Rect(hideArea.Left - Left - walkOffset.X, hideArea.Top - Top, hideArea.Width, hideArea.Height));
            if (pose.Phase != HidePhase.Approach) bubble.Visibility = Visibility.Collapsed;
            if (pose.Phase == HidePhase.Complete) { hideJourney = null; surface.Clip = null; SetAction("idle"); Say("我回来啦，有没有找到我？", 2200); lastInteraction = now; Save(); }
        }
        if (roaming && menu.Children.Count == 0)
        {
            var area = WorkArea;
            var clip = Character.Resolve(State.Outfit, "walk", now - actionStarted).Sprite;
            var step = walkPlayback.Advance(walkCenter, direction, walkDt, State.Size, clip, area.Left + State.Size * .46, area.Right - State.Size * .46);
            walkCenter = step.Center; PlaceWalk(walkCenter); direction = step.Direction;
            Top = area.Bottom - FloorY;
            if (now >= roamDeadline)
            {
                roaming = false;
                if (exploring) FinishExploration();
                else SetAction("idle");
                lastInteraction = now;
            }
        }
        if (action == "idle" && hideJourney is null && !pressed && !resting && menu.Children.Count == 0)
        {
            if (State.Wander && CanWalk && !State.ReducedMotion && now - lastInteraction > 45000 && Math.Abs(Top + FloorY - WorkArea.Bottom) < 1) StartWalk(false);
        }
        if (flyingBall)
        {
            double x0 = ballX + BallSize / 2, y0 = ballY + BallSize / 2;
            var area = WorkArea;
            var step = ToyPhysics.Step(new ToyFlight(ballX, ballY, ballVx, ballVy, ballSpin.Angle), activeToy, dt,
                Math.Max(0, area.Left - Left), Math.Min(Width, area.Right - Left), FloorY);
            ballX = step.X; ballY = step.Y; ballVx = step.Vx; ballVy = step.Vy; ballSpin.Angle = step.Angle;
            var hitFrame = (System.Windows.Media.Imaging.BitmapSource)sprite.Source;
            double bodyHeight = sprite.Height * Art.VisibleHeight(hitFrame);
            if (!ballHit && PortraitChoreography.HitsBody(x0, y0, ballX + BallSize / 2, ballY + BallSize / 2, CenterX, FloorY - bodyHeight, bodyHeight, BallSize / 2))
            {
                ballHit = true;
                Play("ball-hit", "哎呀，打到啦。", 1300); ballVx *= -.65; ballVy = -160;
            }
            // Lifetime follows real elapsed time even if the UI is busy; the bounded
            // physics step must not leave the pet waiting after anticipation expires.
            if (now - ballStarted > 4000) { flyingBall = false; ball.Visibility = Visibility.Collapsed; if (!ballHit) Play("ball-miss", "没碰到我，再试试。", 1600); }
            PlaceBall();
        }
        Render();
        bool moving = dragging || liftActive || dropping || holdingBall || flyingBall || roaming || hideJourney is not null || prop is not null || actionUntil > 0 || danceVisual is not null || action == "thinking";
        timer.Interval = TimeSpan.FromMilliseconds(action == "sleep" ? 80 : moving ? 16 : 200);
    }
    private void Render()
    {
        double elapsed = roaming ? walkPlayback.Milliseconds : (Now - actionStarted) * (hideJourney is not null && action == "walk" ? 1.7 : 1);
        var art = Character.Resolve(State.Outfit, dance is null ? action : "dance", elapsed, State.ReducedMotion);
        var frame = Art.Frame(Character, art.Sprite, art.Frame);
        sprite.Source = frame;
        UsingDrawnAction = dance is null && action != "walk" && Character.MotionFor(State.Outfit, action) is not null;
        DrawnFrame = art.Frame; bakedProps = art.Sprite.BakedProps;
        double size = State.Size;
        if(dance is not null)
        {
            var idle=Character.Resolve(State.Outfit,"idle",0);
            size*=Art.VisibleHeight(Art.Frame(Character,idle.Sprite,idle.Frame))/Art.VisibleHeight(frame);
        }
        if (action == "walk" || UsingDrawnAction && (Character.Category != "chibi" || art.Sprite.HeightRatios is not null || art.Sprite.ReferenceHeightPixels > 0))
        {
            // Scale each appearance to its own idle silhouette, including Q wardrobes.
            var reference = Character.Resolve(State.Outfit, "idle", 0).Sprite;
            if (reference is not null)
            {
                var calibration = Art.Frame(Character, reference, 0);
                if (art.Sprite.ReferenceHeightPixels > 0)
                    size *= Art.VisibleHeight(calibration) * Math.Max(frame.PixelWidth,frame.PixelHeight) / art.Sprite.ReferenceHeightPixels;
                else
                {
                    size *= action == "walk" ? Art.VisibleHeight(calibration) * Art.PoseScale(frame)
                        : Math.Clamp(Art.VisibleHeight(calibration) / Math.Max(.1, Art.VisibleHeight(frame)), .6, 2.2);
                    size *= art.Sprite.HeightRatios?[art.Frame] ?? 1;
                }
            }
        }
        if (action == "sleep") size *= PortraitChoreography.Breath(Now - actionStarted, State.ReducedMotion);
        sprite.Width = sprite.Height = size;
        groundLine = Art.GroundLine(frame);
        AirborneOffset = UsingDrawnAction && action == "jump" ? PortraitChoreography.JumpHeight(Now - actionStarted, State.Size, State.ReducedMotion) : 0;
        Canvas.SetTop(sprite, PetTop - AirborneOffset); Canvas.SetTop(bubble, Math.Max(8, PetTop - AirborneOffset - 78));
        sprite.RenderTransformOrigin = new Point(.5, .5);
        facing.ScaleX = action is "walk" or "peek" ? DesktopWalk.ScaleX(direction, art.Sprite.Facing) : 1;
        Canvas.SetLeft(sprite, CenterX - size * (.5 + (action == "walk" ? facing.ScaleX * (Art.HorizontalAnchor(frame) - .5) : UsingDrawnAction && (Character.Category != "chibi" || art.Sprite.IsolateCells) ? Art.HorizontalAnchor(frame) - .5 : 0)));
        sprite.RenderTransform = facing;
        HandTarget = null;
        if (art.Sprite.Hands?[art.Frame] is { } contact)
        {
            double visibleHeight = size * Art.VisibleHeight(frame);
            HandTarget = new Point(CenterX + contact.Offset * visibleHeight, FloorY - visibleHeight * (1 - contact.Height));
            handSpan = visibleHeight * contact.Span;
        }
        bool animate = !UsingDrawnAction && !State.ReducedMotion && PortraitRig.Supports(Character.Category, Character.FamilyId)
            && (dance is not null || PortraitMotion.Supports(action));
        if (animate)
        {
            if (danceVisual is null || !ReferenceEquals(danceVisual.Texture, frame))
            {
                if (danceVisual is not null) surface.Children.Remove(danceVisual);
                danceVisual = new RigVisual(frame, Character.FamilyId, State.Outfit, Character.Category, dance is not null?art.Sprite.DanceRig:null);
                surface.Children.Insert(surface.Children.IndexOf(sprite) + 1, danceVisual);
            }
            sprite.Opacity = 0;
            danceVisual.Width = danceVisual.Height = size; danceVisual.Opacity = State.Opacity;
            Canvas.SetLeft(danceVisual, CenterX - size*(dance is not null?Art.HorizontalAnchor(frame):.5)); Canvas.SetTop(danceVisual, PetTop);
            danceVisual.UpdateMotion(action, Now - actionStarted, actionUntil > 0 ? actionUntil - actionStarted : 0);
            if(dance is not null)
            {
                double phase=Now-actionStarted;
                double fade=Math.Clamp(Math.Min(phase,PetDance.DurationMs-phase)/220,0,1);
                fade=fade*fade*(3-2*fade);
                danceVisual.Opacity=State.Opacity*fade;
                if(fade<1)
                {
                    var idle=Character.Resolve(State.Outfit,"idle",0);
                    var restFrame=Art.Frame(Character,idle.Sprite,idle.Frame);
                    sprite.Source=restFrame;sprite.Width=sprite.Height=State.Size;
                    Canvas.SetLeft(sprite,CenterX-State.Size/2);
                    Canvas.SetTop(sprite,FloorY-State.Size*Art.GroundLine(restFrame));
                    sprite.Opacity=State.Opacity*(1-fade);
                }
            }
        }
        else
        {
            if (danceVisual is not null) { surface.Children.Remove(danceVisual); danceVisual = null; }
            sprite.Opacity = State.Opacity;
        }
        if (caughtBall && HandTarget is { } hands)
        {
            double heldSize = Math.Clamp(handSpan * activeToy.Diameter / 34, 12, BallSize);
            ball.Width = ball.Height = heldSize; ballX = hands.X - heldSize / 2; ballY = hands.Y - heldSize / 2; PlaceBall();
        }
        DrawEffects(Now); LayoutChat(); UpdateWalkClock();
    }
    private void DrawEffects(double now)
    {
        if (prop is not null && now - propStart > 6000) prop = null;
        Point head = new(CenterX, PetTop + State.Size * (Character.Category == "chibi" ? .23 : .05));
        Point mouth = new(CenterX, PetTop + State.Size * (Character.Category == "chibi" ? .48 : .17));
        Point hand = new(CenterX - State.Size * .13, PetTop + State.Size * .3);
        Point body = new(CenterX, PetTop + State.Size * .43);
        if (UsingDrawnAction && Character.Category != "chibi" && sprite.Source is System.Windows.Media.Imaging.BitmapSource drawn)
        {
            double h = sprite.Height * Art.VisibleHeight(drawn), top = FloorY - h;
            top -= AirborneOffset;
            head = new(CenterX, top + h * .07); mouth = new(CenterX, top + h * .16); body = new(CenterX, top + h * .43);
        }
        else if (UsingDrawnAction && Character.Category == "chibi" && sprite.Source is System.Windows.Media.Imaging.BitmapSource chibi)
        {
            double h = sprite.Height * Art.VisibleHeight(chibi), top = FloorY - h - AirborneOffset;
            head = new(CenterX, top + h * .25); mouth = new(CenterX, top + h * .5); body = new(CenterX, top + h * .77);
        }
        if (danceVisual is { } visual)
        {
            Point Map(RigPoint p) => new(Canvas.GetLeft(visual)+(.5+p.X)*visual.Width,Canvas.GetTop(visual)+(.5+p.Y)*visual.Height);
            head = Map(visual.Rig.Anchor(visual.Pose, 1, visual.Rig.Rest[1].B + new RigPoint(0, -.07)));
            mouth = Map(visual.Rig.Anchor(visual.Pose, 1, visual.Rig.MouthRest));
            hand = Map(visual.Pose.Bones[3].B + (visual.Pose.Bones[3].B - visual.Pose.Bones[3].A) * .35);
            body = Map(visual.Pose.Bones[0].A + new RigPoint(0, -.07));
        }
        effects.Opacity = State.Opacity;
        if (action == "bonk" && !bakedProps && effects.Hammer is null)
            effects.Hammer = Art.Frame(Catalog.Find("whale"), new Sprite("motions/bonk.webp", 1, 1, Cells: [new SpriteCell(65, 26, 107, 113)]), 0);
        var area = WorkArea;
        var bounds = new Rect(new Point(Math.Max(0, area.Left - Left), Math.Max(0, area.Top - Top)),
            new Point(Math.Min(Width, area.Right - Left), Math.Min(Height, area.Bottom - Top)));
        effects.Update(new FeedbackFrame(action, now - actionStarted, actionUntil > 0 ? actionUntil - actionStarted : 0, State.Size,
            head, mouth, HandTarget ?? hand, body, new Point(CenterX, FloorY), bounds, State.ReducedMotion, prop, now - propStart, activeFood, activePrize, bakedProps,
            dance is not null ? PetDance.BeatAt(now - actionStarted) : null, danceFeedbackBeat == PetDance.BeatAt(now - actionStarted), AirborneOffset, liftedFromTaskbar, UsingDrawnAction,
            now < dizzyUntil ? now - dizzyStarted : -1, Math.Max(0, dizzyUntil - now)));
        if (prop == "letter") bubble.Visibility = Visibility.Collapsed;
    }
}

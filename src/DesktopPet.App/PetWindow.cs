using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopPet.Core;

namespace DesktopPet.App;

/// <summary>A transparent native desktop surface; all transient interactions belong to this window.</summary>
public sealed partial class PetWindow : Window
{
    public PetState State { get; }
    public Catalog Catalog { get; }
    public ArtCache Art { get; } = new();
    internal IAccountService Accounts { get; }
    internal MemberAccess AccessTo(string feature) => Membership.Access(feature, Accounts.CurrentAccount?.Tier);
    public Character Character => Catalog.Find(State.Character);
    private readonly StateStore store;
    private readonly Canvas surface = new();
    private readonly Image sprite = new() { Stretch = Stretch.Uniform, Cursor = Cursors.Hand, Focusable = true };
    // Animation may hide or replace the rendered image. Keep pointer ownership on
    // a stable, visible-to-hit-testing layer above every character renderer.
    // Alpha zero is hit-testable in WPF but passes through a layered HWND in
    // Windows. One alpha unit supplies a native hit surface only over the pet.
    private readonly Border petInput = new() { Background = new SolidColorBrush(Color.FromArgb(1,255,255,255)), Cursor = Cursors.Hand, Focusable = true };
    internal FrameworkElement InputSurface => petInput;
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
    internal RigVisual? ActiveMotion => motionVisual;
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
    private RigVisual? motionVisual;
    private EdgeHide? hideJourney;
    private Rect hideArea;
    private double hideStarted;
    private double? foundStarted;
    private double foundCenter, foundDuration;
    internal HidePhase? HideStage => foundStarted is not null ? HidePhase.Return : hideJourney?.At((Now - hideStarted) / 1000).Phase;
    internal string CurrentAction => action;
    internal bool IsMenuOpen => menu.Children.Count > 0;
    private DesktopHost? desktop;
    private SettingsWindow? settings;
    private InlineChat? chatWindow;
    private bool conversationActive, liftedFromTaskbar, dropping;
    private double dropSpeed;
    private Action? afterDrop;
    private string action = "idle";
    private double actionStarted, actionUntil, speechUntil, lastTick, lastInteraction;
    private double lastMotionTrace;
    private Action? onMotionEnd;
    private bool dragging, pressed, resting, roaming, exploring, closing, holdingBall, flyingBall, ballHit, caughtBall, clickThrough;
    private bool nativePointerOwned;
    private Point downScreen, downWindow, ballPrevious;
    private double ballX, ballY, ballVx, ballVy, ballSampleTime, ballStarted, roamDeadline;
    private int direction = -1;
    private int nextTouch;
    private CareRoutine? careRoutine;
    private double careStarted;
    private int careStep = -1;
    internal string? CurrentCare => careRoutine?.Key;
    internal string CurrentSpeech => speech.Text;
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
        if(clubAction is not null)ClearClub();
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
    internal void PreviewCare(CareRoutine routine, double elapsed)
    {
        var pose = routine.At(elapsed);
        PreviewMotion(pose.Motion, pose.Elapsed, pose.Duration);
    }

    public PetWindow(StateStore store, Catalog catalog, IAccountService? accounts = null)
    {
        this.store = store; Catalog = catalog; State = store.Load();
        Accounts = accounts ?? new LocalAccountService(store.Root);
        Accounts.Changed += AccountChanged;
        Closed += (_, _) => Accounts.Changed -= AccountChanged;
        State.MigrateCharacters(Catalog.LegacyAliases);
        State.Character = catalog.Find(State.Character).Id;
        Title = "DesktopPet · 桌边伙伴"; Icon = CloudTheme.AppIcon; Width = 560; Height = 680;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; AllowsTransparency = true;
        Background = Brushes.Transparent; ShowInTaskbar = false; Topmost = State.Topmost;
        Content = surface;
        surface.RenderTransform = walkOffset;
        sprite.RenderTransform = facing;
        ball.RenderTransform = ballSpin;
        surface.Children.Add(sprite); surface.Children.Add(petInput); surface.Children.Add(effects); surface.Children.Add(menu);
        bubble = new Border { Background = CloudTheme.Cream, BorderBrush = CloudTheme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(20), Padding = new Thickness(15, 10, 15, 10), Width = 238, Child = speech, IsHitTestVisible = false, Visibility = Visibility.Collapsed };
        surface.Children.Add(bubble); surface.Children.Add(ball);
        InitializeClub(); InitializeFive();
        ball.Visibility = Visibility.Collapsed;
        AutomationProperties.SetName(sprite, "桌面宠物：点击轮换摸头、揉脸、挠痒，拖动抱起，松手放置，按住 Shift 松手下落，来回摇晃会头晕，右键聊天与互动");
        AutomationProperties.SetName(petInput, AutomationProperties.GetName(sprite));
        petInput.MouseLeftButtonDown += PetDown; petInput.MouseMove += PetMove; petInput.MouseLeftButtonUp += PetUp;
        petInput.MouseRightButtonUp += (_, e) => { HandleRightClick(); e.Handled = true; };
        petInput.LostMouseCapture += (_, _) => { if (pressed) CancelInput(true); };
        petInput.KeyDown += (_, e) => { if (e.Key is Key.Enter or Key.Space) { Touch(.3); e.Handled = true; } else if (e.Key == Key.F10 && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) { ShowMenu("root", true); e.Handled = true; } };
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
        Deactivated += (_, _) => { CancelInput(true); menu.Children.Clear(); };
        SourceInitialized += (_, _) => InitializeDesktop();
        Loaded += (_, _) => Welcome();
        RenderOptions.SetBitmapScalingMode(sprite, BitmapScalingMode.HighQuality);
        timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) =>
        {
            // A layered desktop window can stop receiving compositor callbacks
            // temporarily. Keep the action clock alive without double-stepping
            // normal frames; the shared lastTick accounts for either source.
            if (!renderingWalk || Now - lastTick >= 50) Tick();
        };
        timer.Start();
        Closed += (_, _) => { closing = true; UpdateWalkClock(); timer.Stop(); CancelInput(); CancelChoreography(); chatWindow?.Dispose(); settings?.Close(); desktop?.Dispose(); Save(); };
        day = DateOnly.FromDateTime(DateTime.Now);
        ApplySettings(false);
        InitializeCompanion();
        InitializeAgenda();
        TraceMotion("created");
    }
    private void InitializeDesktop()
    {
        desktop = new DesktopHost(this, OpenSettings, ToggleVisible, () => Application.Current.Shutdown());
        desktop.PointerInput += NativePointer;
        desktop.BodyHit += point => !clickThrough && IsHitTestVisible && IsVisible && ReferenceEquals(InputHitTest(PointFromScreen(point)),petInput);
        desktop.RecoverWalkingPointer += () => roaming && !clickThrough && IsHitTestVisible && IsVisible;
        desktop.OwnsPointer += () => nativePointerOwned;
        desktop.RightClick += HandleRightClick;
        if(App.MotionLog is not null)desktop.PointerRecoveryTrace += TraceMotion;
        if(App.MotionLog is not null)desktop.PointerTrace += message => TraceMotion("window-message-"+message.ToString("x4"));
        desktop.PointerCancelled += () => { if(nativePointerOwned)CancelInput(true); };
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
        else if (!State.CheckedIn(day)) { SetAction("idle"); Render(); Say("右键照顾，一起吃早饭 ☀", 5500); }
        else Play("chat", "今天也陪你一起。右键找我玩。", 2600);
        if (actionUntil > 0) onMotionEnd += ResumeFloorWalk;
        else ResumeFloorWalk();
        if (store.Warning is { } warning) Say(warning, 6500);
        TraceMotion("welcome");
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
        CompanionActivity(); DismissWorkReminder(); chatWindow?.CancelResponse(); recentTouches.Clear();
        bool keepChat = ActiveChat is not null;
        CancelInput(); ClearTransient(keepChat); resting = false;
        sprite.Visibility = Visibility.Visible;
        State.Character = selected.Id; ApplySettings(false); Save();
        ResetIdlePosture();
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
        ResetIdlePosture();
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
        CompanionActivity();
        if (settings is not null) { settings.Activate(); return; }
        settings = new SettingsWindow(this); settings.Closed += (_, _) => { settings = null; CompanionActivity(); }; settings.Show();
    }
    internal void OpenChat()
    {
        if (!RequireMembership("chat")) return;
        if (clickThrough) { clickThrough = false; desktop?.ClickThrough(false); }
        ClearTransient(); conversationActive = true; SetAction("listen"); Render();
        Chat.Open(); LayoutChat(true);
    }
    internal void OpenMembership()
    { OpenSettings(); settings!.ShowMembership(); }
    private bool RequireMembership(string feature)
    {
        var access = AccessTo(feature);
        if (access.Allowed) return true;
        menu.Children.Clear(); Say(access.Hint + "，去会员中心看看吧。", 4000); OpenMembership(); return false;
    }
    private void AccountChanged()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(AccountChanged); return; }
        StopInteraction(); DismissWorkReminder(); chatWindow?.ResetSession(); settings?.RefreshMembership();
    }
    private InlineChat CreateChat() { var chat = new InlineChat(this); surface.Children.Add(chat); Panel.SetZIndex(chat, 30); return chat; }
    internal void LayoutChat(bool makeRoom = false)
    {
        if (ActiveChat is not { } chat) return;
        if (chat.Child is ScrollViewer chatScroll) chatScroll.MaxHeight = Math.Max(120, Math.Min(390, PetTop - AirborneOffset - 32));
        chat.Measure(new Size(chat.Width, double.PositiveInfinity));
        double height = chat.DesiredSize.Height, head = PetTop - AirborneOffset;
        var area = WorkArea;
        if (makeRoom || chat.HasAgendaDraft) Top = Math.Min(area.Bottom - FloorY, Math.Max(Top, area.Top + height + 14 - head));
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
    { action = next; actionStarted = Now; actionUntil = duration > 0 ? Now + duration : 0; onMotionEnd = completed; ResetIdlePosture(); if (next == "walk") walkPlayback.Reset(); timer.Interval = TimeSpan.FromMilliseconds(16); }
    private void UpdateWalkClock()
    {
        bool useRendering = !closing && previewClock is null && IsVisible && (five is not null || roaming || hideJourney is not null || clubAction is not null || authoredVisual is not null && actionUntil>0 && !State.ReducedMotion);
        if (useRendering == renderingWalk) return;
        renderingWalk = useRendering; lastTick = Now; lastPresentation = TimeSpan.MinValue;
        if (useRendering) { timer.Start(); CompositionTarget.Rendering += RenderWalk; }
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
    public void CheckIn(bool meal = true)
    {
        CompanionActivity();
        day = DateOnly.FromDateTime(DateTime.Now);
        string before = State.Companion(Character.FamilyId).Name; bool added = State.CheckIn(day);
        if (added) AwardCompanion(3, "一起打卡", "checkin-" + day.ToString("yyyy-MM-dd"), 86400);
        ClearTransient();
        if (meal) { activeFood = Collectibles.Get("rice"); Play("meal", added ? "开饭啦。" : "再吃一点。", PortraitMotion.Duration("meal")); prop = "food"; propStart = Now; }
        else Play("happy", added ? "今天也一起。" : "今天已经打过卡啦。", 2400);
        if (added && State.Companion(Character.FamilyId).Name != before)
        { prop = "confetti"; Say("我们又更默契了一点。", 3000); }
        else if (added && new[] { 50, 150, 250, 300, 500, 1000 }.Contains(State.CheckIns.Count))
        { prop = "confetti"; Say($"一起打卡 {State.CheckIns.Count} 天啦。", 3000); }
        Save(); settings?.RefreshLife();
    }
    internal void Touch(double fraction)
    {
        if(five is not null)return;
        if (resting) { RestorePet(); return; }
        if (FindHiddenPet()) return;
        string motion = PetActions.Touches[nextTouch].Key;
        if (RequireMembership(motion)) { nextTouch = (nextTouch + 1) % PetActions.Touches.Length; RunTouch(motion); }
    }
    private void RunTouch(string motion)
    {
        if (!TouchCompanion()) return;
        ClearTransient();
        Play(motion, motion switch { "headpat" => "再摸摸～", "poke" => "软软的。", _ => "哈哈，好痒！" }, PortraitMotion.Duration(motion));
    }
    private bool FindHiddenPet()
    {
        if (hideJourney is not {} journey) return false;
        if(foundStarted is not null) return true;
        CompanionActivity(); AwardCompanion(1, "找到伙伴", "find", 60);
        menu.Children.Clear();foundStarted=Now;foundCenter=Left+walkOffset.X+CenterX;
        foundDuration=Math.Max(260,Math.Abs(foundCenter-journey.RestingCenter)/DesktopWalk.Speed(State.Size,Character.MotionFor(State.Outfit,"walk")!)*1000);
        SetAction("walk");Say("被你找到啦！",foundDuration+2000);Render();
        return true;
    }
    internal void HandleRightClick()
    {
        CompanionActivity();
        if (menu.Children.Count > 0) menu.Children.Clear(); else ShowMenu();
    }
    private Point ScreenPoint(MouseEventArgs e)
    {
        // Read screen coordinates before the moving HWND or the pose changes;
        // window-relative mouse positions can be stale after a walking frame.
        var cursor = System.Windows.Forms.Cursor.Position;
        var p = new Point(cursor.X, cursor.Y);
        return PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice.Transform(p) ?? p;
    }
    private void PetDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (BeginPointerGesture(ScreenPoint(e))) petInput.CaptureMouse();
        e.Handled = true;
    }
    private bool NativePointer(int message, Point devicePoint, long buttons)
    {
        if(clickThrough || !IsHitTestVisible || !IsVisible)return false;
        if(message is 0x0201 or 0x0203)
        {
            if (nativePointerOwned) return true;
            // Menus, chat, toys and the user's high-five hand retain their own
            // routed input. This path owns only the character's input surface.
            if(!ReferenceEquals(InputHitTest(PointFromScreen(devicePoint)),petInput)){TraceMotion("native-pointer-down-outside-input");return false;}
            nativePointerOwned=true;
            var logical=PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice.Transform(devicePoint)??devicePoint;
            // Register capture with WPF as well as Windows. Otherwise the WPF
            // mouse provider can release an unfamiliar native capture when a
            // delayed move arrives after the recovered press.
            if(BeginPointerGesture(logical) && !petInput.CaptureMouse())desktop?.CapturePointer();
            TraceMotion("native-pointer-down");
            return true;
        }
        if(!nativePointerOwned)return false;
        if(message==0x0200)
        {
            if((buttons&1)==0)
            {
                // A move queued before a recovered press can still carry the
                // old button mask. It must not cancel the newly captured drag.
                if(desktop?.LeftPointerDown!=true)
                    NativePointer(0x0202,devicePoint,desktop?.ShiftDown==true?4:0);
                return true;
            }
            var logical=PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice.Transform(devicePoint)??devicePoint;
            MovePointerGesture(logical);
            return true;
        }
        if(message==0x0202)
        {
            nativePointerOwned=false;
            var local=petInput.PointFromScreen(devicePoint);
            EndPointerGesture((buttons&4)!=0,local.Y/Math.Max(1,petInput.ActualHeight));
            desktop?.ReleasePointer();
            TraceMotion("native-pointer-up");
            return true;
        }
        return false;
    }
    internal bool BeginPointerGesture(Point screen)
    {
        CompanionActivity();
        if (resting) { RestorePet(); return false; }
        if (FindHiddenPet()) return false;
        if (dropping) { CancelChoreography(); Render(); Save(); }
        if (roaming) { roaming = exploring = false; onMotionEnd = null; SetAction("idle"); Render(); }
        lastInteraction = Now;
        menu.Children.Clear(); petInput.Focus(); pressed = true; dragging = false;
        downScreen = screen; downWindow = new Point(Left + walkOffset.X, Top);
        if(App.MotionLog is not null)TraceMotion($"gesture-origin-{screen.X:0.##},{screen.Y:0.##}");
        TraceMotion("pointer-down");
        return true;
    }
    private void PetMove(object sender, MouseEventArgs e)
    {
        if (!pressed || e.LeftButton != MouseButtonState.Pressed) return;
        if (MovePointerGesture(ScreenPoint(e))) e.Handled = true;
    }
    internal bool MovePointerGesture(Point screen)
    {
        if (!pressed) return false;
        var delta = screen - downScreen;
        if(App.MotionLog is not null && !dragging && delta.Length>=8)TraceMotion($"gesture-drag-{screen.X:0.##},{screen.Y:0.##}-delta-{delta.X:0.##},{delta.Y:0.##}");
        if (!dragging && delta.Length < 8) return false;
        if (!dragging) { dragging = true; BeginLift(); }
        MoveLift(downWindow.X + delta.X, downWindow.Y + delta.Y);
        return true;
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
        if (EndPointerGesture(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift), e.GetPosition(petInput).Y / petInput.Height)) e.Handled = true;
    }
    internal bool EndPointerGesture(bool dropOnRelease, double touchFraction = .3)
    {
        if (!pressed) return false;
        bool wasDrag = dragging;
        pressed = dragging = false; petInput.ReleaseMouseCapture();
        if (wasDrag)
        {
            ReleaseLift(dropOnRelease);
        }
        else Touch(touchFraction);
        return true;
    }
    internal Rect WorkArea => desktop?.WorkArea(this) ?? SystemParameters.WorkArea;
    internal void BeginLift()
    {
        bool fromEdge = Math.Abs(Top + FloorY - WorkArea.Bottom) < 28;
        double liftedFor = action == "pickup" ? Math.Max(0, Now - actionStarted) : 0;
        ClearTransient(); liftedFromTaskbar = fromEdge; roaming = false; liftActive = true;
        shake.Start(Left, Top, Now); SetAction("pickup"); actionStarted -= liftedFor; Render();
        TraceMotion("lift");
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
        TraceMotion("release");
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
    internal void CancelInput(bool settleOnFloor = false)
    {
        bool hadDrag = dragging || liftActive;
        nativePointerOwned=false;
        ResetShake();
        pressed = dragging = holdingBall = false;
        if (petInput.IsMouseCaptured) petInput.ReleaseMouseCapture();
        if (ball.IsMouseCaptured) ball.ReleaseMouseCapture();
        desktop?.ReleasePointer();
        if (hadDrag)
        {
            dropping = liftedFromTaskbar = false; afterDrop = null;
            if (settleOnFloor && !closing) ReleaseLift();
            else { Constrain(); SetAction("idle"); if (!closing) Save(); }
        }
        if (hadDrag) TraceMotion("capture-cancelled");
    }
    private void ClearTransient(bool keepChat = false)
    { careRoutine = null; careStep = -1; ResetShake(); CancelChoreography(); if (!keepChat) chatWindow?.Close(); resting = roaming = exploring = conversationActive = liftedFromTaskbar = false; sprite.Visibility = Visibility.Visible; menu.Children.Clear(); effects.Clear(); prop = null; flyingBall = holdingBall = caughtBall = false; ball.Visibility = Visibility.Collapsed; bubble.Visibility = Visibility.Collapsed; onMotionEnd = null; }

    private void ResetShake()
    { liftActive = false; dizzyUntil = 0; observedReversal = double.NegativeInfinity; shake.Start(0, 0, Now); }

    private void CancelChoreography()
    {
        ClearFive(); ClearClub(); ClearAuthoredVisual();
        HandTarget = null; handSpan = 0;
        foundStarted=null;
        dropping = false; afterDrop = null;
        if (hideJourney is not null)
        {
            // Use the departure monitor: the mostly hidden HWND may overlap its neighbour.
            Left = Math.Clamp(Left, hideArea.Left - CenterX + State.Size * .46, hideArea.Right - CenterX - State.Size * .46);
            Top = hideArea.Bottom - FloorY;
            hideJourney = null; surface.Clip = null;
        }
        if (motionVisual is not null) { surface.Children.Remove(motionVisual); motionVisual = null; }
        sprite.Opacity = State.Opacity; sprite.RenderTransform = facing;
    }

    internal void ShowMenu(string group = "root", bool keyboard = false)
    {
        // Opening/navigating the menu is presentation only. Keep action clocks,
        // movement, chat, toys and edge-hide choreography running unchanged.
        menu.Children.Clear();
        var entries = PetActions.Menu(group);
        var area = WorkArea;
        var positions = RadialMenu.Place(entries.Length, new MenuPoint(CenterX, PetTop + State.Size * .5), State.Size,
            new MenuBounds(Math.Max(0, area.Left - Left), Math.Max(0, area.Top - Top), Math.Min(Width, area.Right - Left), Math.Min(Height, area.Bottom - Top)));
        var buttons = new List<Button>();
        foreach (var (entry, i) in entries.Select((entry, i) => (entry, i)))
        {
            var access = AccessTo(entry.Key);
            var button = new Button { Content = MemberVisual.ActionIcon(entry.Icon, !access.Allowed, 25), ToolTip = access.Allowed ? entry.Title : entry.Title + " · " + access.Hint,
                Style = (Style)FindResource("RadialAction") };
            AutomationProperties.SetName(button, entry.Title);
            if (!access.Allowed) AutomationProperties.SetHelpText(button, access.Hint);
            ToolTipService.SetInitialShowDelay(button, 300); ToolTipService.SetShowDuration(button, 2500);
            button.Click += (_, _) =>
            {
                if (entry.Key.StartsWith("group:")) ShowMenu(entry.Key[6..], keyboard || (button.IsKeyboardFocused && !button.IsMouseOver));
                else if (entry.Key == "close") { menu.Children.Clear(); petInput.Focus(); }
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
    private void LayoutMenu()
    {
        if(menu.Children.Count==0)return;
        var area=hideJourney is null?WorkArea:hideArea;
        var positions=RadialMenu.Place(menu.Children.Count,new MenuPoint(CenterX,PetTop+State.Size*.5),State.Size,
            new MenuBounds(Math.Max(0,area.Left-Left-walkOffset.X),Math.Max(0,area.Top-Top),Math.Min(Width,area.Right-Left-walkOffset.X),Math.Min(Height,area.Bottom-Top)));
        for(int i=0;i<positions.Length;i++) { Canvas.SetLeft(menu.Children[i],positions[i].X-RadialMenu.ButtonSize/2);Canvas.SetTop(menu.Children[i],positions[i].Y-RadialMenu.ButtonSize/2); }
    }
    public void RunInteraction(string key)
    {
        if (!RequireMembership(key)) return;
        CompanionActivity();
        CancelInput(); ClearTransient();
        switch (key)
        {
            case "highfive":case "rps":case "gift":case "read":StartFive(key);break;
            case "photo": OpenCompanionPhoto(); break;
            case "checkin": CheckIn(); break;
            case "snack": Snack(); break;
            case "headpat": case "poke": case "tickle": RunTouch(key); break;
            case "praise": case "comfort": case "lullaby": StartCare(CareRoutine.Find(key)!); break;
            case "comb": case "wipe": case "stretch": case "bubbles": case "stars": case "butterfly": StartClubAction(key); break;
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
            case "nudge": TakeBall(); break;
            case "letter": Celebrate($"相伴 {State.CheckIns.Count} 天"); break;
            default: Play(key, duration: PortraitMotion.Duration(key)); break;
        }
    }
    public void StopInteraction() { CompanionActivity(); CancelInput(); ClearTransient(); SetAction("idle"); lastInteraction = Now; Render(); }
    private void Snack() { ClearTransient(); activeFood = Character.MotionFor(State.Outfit, "eat")?.BakedProps == true ? Collectibles.Get("bread") : snacks.Draw(); Play("eat", Character.MotionFor(State.Outfit,"eat")?.BakedProps == true ? "啊呜，好吃。" : activeFood.Name + "，啊呜。", PortraitMotion.Duration("eat")); prop = "food"; propStart = Now; }
    private void BuildBlocks() { ClearTransient(); Play("build", duration: PortraitMotion.Duration("build")); prop = "blocks"; propStart = Now; onMotionEnd = () => prop = null; }
    private void Celebrate(string label) { ClearTransient(); Play("chat", label + "。这封信送给你 ♡", 5200); prop = "letter"; propStart = Now; }
    private void StartCare(CareRoutine routine)
    {
        careRoutine = routine; careStarted = Now; careStep = -1; lastInteraction = Now;
        UpdateCare(Now); Say(routine.Message, routine.Duration); Render();
    }
    private void UpdateCare(double now)
    {
        if (careRoutine is not { } routine) return;
        double elapsed = now - careStarted;
        if (elapsed >= routine.Duration)
        {
            AwardCompanion(1, "温柔照顾", "care", 30);
            careRoutine = null; careStep = -1; resting = routine.FallsAsleep;
            SetAction(resting ? "sleep" : "idle"); lastInteraction = now;
            if (resting) bubble.Visibility = Visibility.Collapsed;
            return;
        }
        var pose = routine.At(elapsed);
        if (pose.Step == careStep) return;
        careStep = pose.Step; SetAction(pose.Motion, pose.Duration - pose.Elapsed); actionStarted = now - pose.Elapsed;
    }
    private void Peek() => BeginHide();
    private void Peek(int side) => BeginHide(side);
    internal void BeginHide(int? side = null)
    {
        side ??= Random.Shared.Next(2) == 0 ? -1 : 1;
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
        TraceMotion("resume-floor-walk");
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
        double now = Now, elapsedSeconds = Math.Max(0, (now - lastTick) / 1000), walkDt = Math.Min(elapsedSeconds, .25), dt = Math.Min(walkDt, .05); lastTick = now;
        var today = DateOnly.FromDateTime(DateTime.Now);
        if (day != today) { day = today; State.LastSeen = day.ToString("yyyy-MM-dd"); Save(); }
        UpdateCare(now);
        TickCompanion(elapsedSeconds);
        TickClub(walkDt);
        TickFive(walkDt*1000);
        LayoutMenu();
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
            bool found=foundStarted is not null;
            if(foundStarted is {} foundAt)
            {
                double progress=Math.Clamp((now-foundAt)/foundDuration,0,1);
                pose=new HidePose(foundCenter+(journey.RestingCenter-foundCenter)*progress,Math.Sign(journey.RestingCenter-foundCenter),progress<1?HidePhase.Return:HidePhase.Complete,progress<1);
            }
            PlaceWalk(pose.Center); Top = hideArea.Bottom - FloorY; direction = pose.Direction;
            string motion = pose.Walking ? "walk" : "idle";
            if (action != motion) SetAction(motion);
            surface.Clip = new RectangleGeometry(new Rect(hideArea.Left - Left - walkOffset.X, hideArea.Top - Top, hideArea.Width, hideArea.Height));
            if (pose.Phase != HidePhase.Approach) bubble.Visibility = Visibility.Collapsed;
            if (pose.Phase == HidePhase.Complete) { hideJourney = null; foundStarted=null; surface.Clip = null; SetAction(found?"happy":"idle",found?2000:0); Say(found?"被你找到啦！":"我回来啦，有没有找到我？", 2200); lastInteraction = now; Save(); }
        }
        if (roaming)
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
        TickIdlePostures(); Render();
        if (App.MotionLog is not null && now - lastMotionTrace >= 1000) { lastMotionTrace = now; TraceMotion("tick"); }
        bool moving = five is not null || dragging || liftActive || dropping || holdingBall || flyingBall || roaming || hideJourney is not null || prop is not null || actionUntil > 0 || motionVisual is not null || action == "thinking";
        timer.Interval = TimeSpan.FromMilliseconds(action == "sleep" ? 80 : moving ? 16 : 200);
    }
    private void TraceMotion(string eventName)
    {
        if (App.MotionLog is not { } path) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string line = JsonSerializer.Serialize(new { eventName, time = DateTimeOffset.Now, ms = Now, App.DataRoot,
                IsLoaded, IsVisible, action, direction, facing = facing.ScaleX, State.Character, State.Outfit, State.Wander, State.ReducedMotion, State.LastSeen,
                left = double.IsFinite(Left) ? (double?)Left : null, top = double.IsFinite(Top) ? (double?)Top : null,
                floor = WorkArea.Bottom - FloorY, pressed, dragging, liftActive, dropping, resting, roaming,
                conversationActive, menuCount = menu.Children.Count, renderingWalk, timerRunning = timer.IsEnabled, DrawnFrame }) + Environment.NewLine;
            if (File.Exists(path) && new FileInfo(path).Length > 2 * 1024 * 1024) File.WriteAllText(path, line);
            else File.AppendAllText(path, line);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
    private void Render()
    {
        IsRenderingIdlePosture = false;
        if(RenderFivePose()) { UpdatePetInput();return; }
        if (RenderClubPose()) { UpdatePetInput();return; }
        if (RenderIdlePosture()) return;
        double elapsed = roaming ? walkPlayback.Milliseconds : (Now - actionStarted) * (hideJourney is not null && foundStarted is null && action == "walk" ? 1.7 : 1);
        string renderAction=clubAction=="butterfly" ? Math.Abs(butterflyTarget-(Left+walkOffset.X+CenterX))>2 && !State.ReducedMotion ? "walk" : "ball-ready" : action;
        if (clubAction=="butterfly" && renderAction=="walk") elapsed=walkPlayback.Milliseconds;
        var art = Character.Resolve(State.Outfit, renderAction, elapsed, State.ReducedMotion);
        var frame = Art.Frame(Character, art.Sprite, art.Frame);
        sprite.Source = frame;
        UsingDrawnAction = renderAction != "walk" && Character.MotionFor(State.Outfit, renderAction) is not null;
        DrawnFrame = art.Frame; bakedProps = art.Sprite.BakedProps;
        double size = State.Size;
        if (art.Sprite.ReferenceHeightPixels > 0 || renderAction == "walk" || UsingDrawnAction && (Character.Category != "chibi" || art.Sprite.HeightRatios is not null))
        {
            // Authored reference pixels keep one anatomical scale for an entire
            // sheet. Bent knees, raised hands and props must not resize the body.
            var reference = Character.Resolve(State.Outfit, "idle", 0).Sprite;
            if (reference is not null)
            {
                var calibration = Art.Frame(Character, reference, 0);
                if (art.Sprite.ReferenceHeightPixels > 0)
                    size *= Art.VisibleHeight(calibration) * Math.Max(frame.PixelWidth,frame.PixelHeight) / art.Sprite.ReferenceHeightPixels;
                else
                {
                    size *= renderAction == "walk" ? Art.VisibleHeight(calibration) * Art.PoseScale(frame)
                        : Math.Clamp(Art.VisibleHeight(calibration) / Math.Max(.1, Art.VisibleHeight(frame)), .6, 2.2);
                    size *= art.Sprite.HeightRatios?[art.Frame] ?? 1;
                }
            }
        }
        size *= art.Sprite.FrameScaleFactors?[art.Frame] ?? 1;
        if (action == "sleep") size *= PortraitChoreography.Breath(Now - actionStarted, State.ReducedMotion);
        sprite.Width = sprite.Height = size;
        groundLine = Art.GroundLine(frame);
        AirborneOffset = UsingDrawnAction && action == "jump" ? PortraitChoreography.JumpHeight(Now - actionStarted, State.Size, State.ReducedMotion) : 0;
        Canvas.SetTop(sprite, PetTop - AirborneOffset); Canvas.SetTop(bubble, Math.Max(8, PetTop - AirborneOffset - 78));
        sprite.RenderTransformOrigin = new Point(.5, .5);
        facing.ScaleX = renderAction is "walk" or "peek" ? DesktopWalk.ScaleX(direction, art.Sprite.Facing) : 1;
        Canvas.SetLeft(sprite, CenterX - size * (.5 + (renderAction == "walk" ? facing.ScaleX * (Art.HorizontalAnchor(frame) - .5) : UsingDrawnAction && (Character.Category != "chibi" || art.Sprite.IsolateCells) ? Art.HorizontalAnchor(frame) - .5 : 0)));
        sprite.RenderTransform = facing;
        HandTarget = null;
        if (art.Sprite.Hands?[art.Frame] is { } contact)
        {
            double visibleHeight = size * Art.VisibleHeight(frame);
            HandTarget = new Point(CenterX + contact.Offset * visibleHeight, FloorY - visibleHeight * (1 - contact.Height));
            handSpan = visibleHeight * contact.Span;
        }
        bool animate = !UsingDrawnAction && !State.ReducedMotion && PortraitRig.Supports(Character.Category, Character.FamilyId)
            && PortraitMotion.Supports(action);
        if (animate)
        {
            if (motionVisual is null || !ReferenceEquals(motionVisual.Texture, frame))
            {
                if (motionVisual is not null) surface.Children.Remove(motionVisual);
                motionVisual = new RigVisual(frame, Character.FamilyId, State.Outfit, Character.Category);
                surface.Children.Insert(surface.Children.IndexOf(sprite) + 1, motionVisual);
            }
            sprite.Opacity = 0;
            motionVisual.Width = motionVisual.Height = size; motionVisual.Opacity = State.Opacity;
            Canvas.SetLeft(motionVisual, CenterX - size*.5); Canvas.SetTop(motionVisual, PetTop);
            motionVisual.UpdateMotion(action, Now - actionStarted, actionUntil > 0 ? actionUntil - actionStarted : 0);
        }
        else
        {
            if (motionVisual is not null) { surface.Children.Remove(motionVisual); motionVisual = null; }
            sprite.Opacity = State.Opacity;
        }
        RenderAuthoredMotion(art.Sprite,renderAction,elapsed);
        UpdatePetInput();
        if (caughtBall && HandTarget is { } hands)
        {
            double heldSize = Math.Clamp(handSpan * activeToy.Diameter / 34, 12, BallSize);
            ball.Width = ball.Height = heldSize; ballX = hands.X - heldSize / 2; ballY = hands.Y - heldSize / 2; PlaceBall();
        }
        DrawEffects(Now); LayoutChat(); UpdateWalkClock();
        if (clubAction is { } club)
        {
            if (renderAction=="walk") facing.ScaleX=DesktopWalk.ScaleX(direction,art.Sprite.Facing);
            RenderClubFeedback(club,Now-clubStarted,State.Size*Art.VisibleHeight(Art.Frame(Character,Character.Resolve(State.Outfit,"idle",0).Sprite,0)));
        }
    }
    private void UpdatePetInput()
    {
        double visibleHeight=sprite.Source is System.Windows.Media.Imaging.BitmapSource image?Art.VisibleHeight(image):1;
        petInput.Width=sprite.Width;petInput.Height=sprite.Height*visibleHeight;
        petInput.Visibility=sprite.Visibility;
        petInput.RenderTransformOrigin=sprite.RenderTransformOrigin;
        petInput.RenderTransform=sprite.RenderTransform;
        Canvas.SetLeft(petInput,Canvas.GetLeft(sprite));
        // Exclude image padding below the feet: the taskbar must still receive
        // clicks in its own area, even when the character stands on its edge.
        Canvas.SetTop(petInput,Canvas.GetTop(sprite)+sprite.Height*(groundLine-visibleHeight));
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
        if (motionVisual is { } visual)
        {
            Point Map(RigPoint p) => new(Canvas.GetLeft(visual)+(.5+p.X)*visual.Width,Canvas.GetTop(visual)+(.5+p.Y)*visual.Height);
            head = Map(visual.Rig.Anchor(visual.Pose, 1, visual.Rig.Rest[1].B + new RigPoint(0, -.07)));
            mouth = Map(visual.Rig.Anchor(visual.Pose, 1, visual.Rig.MouthRest));
            hand = Map(visual.Pose.Bones[3].B + (visual.Pose.Bones[3].B - visual.Pose.Bones[3].A) * .35);
            body = Map(visual.Pose.Bones[0].A + new RigPoint(0, -.07));
        }
        head=AuthoredEffectAnchor("head")??head;
        mouth=AuthoredEffectAnchor("mouth")??mouth;
        body=AuthoredEffectAnchor("body")??body;
        effects.Opacity = State.Opacity;
        if (action == "bonk" && !bakedProps && effects.Hammer is null)
            effects.Hammer = Art.Frame(Catalog.Find("whale"), new Sprite("motions/bonk.webp", 1, 1, Cells: [new SpriteCell(65, 26, 107, 113)]), 0);
        var area = WorkArea;
        var bounds = new Rect(new Point(Math.Max(0, area.Left - Left), Math.Max(0, area.Top - Top)),
            new Point(Math.Min(Width, area.Right - Left), Math.Min(Height, area.Bottom - Top)));
        effects.Update(new FeedbackFrame(action, now - actionStarted, actionUntil > 0 ? actionUntil - actionStarted : 0, State.Size,
            head, mouth, HandTarget ?? hand, body, new Point(CenterX, FloorY), bounds, State.ReducedMotion, prop, now - propStart, activeFood, activePrize, bakedProps,
            AirborneOffset, liftedFromTaskbar, UsingDrawnAction,
            now < dizzyUntil ? now - dizzyStarted : -1, Math.Max(0, dizzyUntil - now)));
        if (prop == "letter") bubble.Visibility = Visibility.Collapsed;
    }
}

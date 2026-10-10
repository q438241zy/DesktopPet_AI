using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DesktopPet.Core;

namespace DesktopPet.App;

/// <summary>The composer and reply are children of the pet surface, never another HWND.</summary>
internal sealed partial class InlineChat : Border, IDisposable
{
    private readonly PetWindow pet;
    private readonly bool agendaMode;
    private readonly List<ChatMessage> history = [];
    private readonly Dictionary<string, List<ChatMessage>> conversations = [];
    private readonly TextBox input = new() { MinHeight = 36, MaxHeight = 70, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxLength = 4000, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBlock reply = new() { TextWrapping = TextWrapping.Wrap, FontSize = 13, LineHeight = 20, Foreground = CloudTheme.Ink };
    private readonly Button send = new() { Content = "发送", Padding = new Thickness(10, 5, 10, 5), MinWidth = 48 };
    private readonly Button stop = new() { Content = "停止", Visibility = Visibility.Collapsed, Padding = new Thickness(10, 5, 10, 5) };
    private readonly HttpClient client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(45) };
    private CancellationTokenSource? pending;
    private string family = "", apiKey = "";
    private bool disposed;
    private int sessionGeneration;
    internal ChatOptions Options { get; private set; } = new();
    internal IReadOnlyList<ChatMessage> History => history;
    internal bool IsThinking => pending is { IsCancellationRequested: false };
    internal string ReplyText => reply.Text;
    internal bool HasDraft => !string.IsNullOrWhiteSpace(input.Text) || agendaEditor.Visibility == Visibility.Visible;
    internal bool CanGenerate => !Options.IsLocal && (!string.IsNullOrWhiteSpace(apiKey) || Uri.TryCreate(Options.Endpoint, UriKind.Absolute, out var uri) && uri.IsLoopback);
    private string OptionsPath => Path.Combine(App.DataRoot, "chat-settings.json");

    public InlineChat(PetWindow pet, bool agendaMode = false)
    {
        this.pet = pet; this.agendaMode = agendaMode;
        Width = 306; Padding = new Thickness(13); CornerRadius = new CornerRadius(18);
        Background = CloudTheme.Cream; BorderBrush = CloudTheme.Line; BorderThickness = new Thickness(1);
        Visibility = Visibility.Collapsed;
        var root = new StackPanel(); Child = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = 360 }; root.Children.Add(chatConversation);
        var row = new DockPanel();
        var close = new Button { Content = CloudTheme.Icon("close", 13), Width = 24, Height = 24, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), ToolTip = "收起聊天" };
        AutomationProperties.SetName(close, "收起聊天"); DockPanel.SetDock(close, Dock.Right); row.Children.Add(close);
        row.Children.Add(new ScrollViewer { Content = reply, MaxHeight = 110, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 0, 5, 10) }); chatConversation.Children.Add(row);
        var composer = new DockPanel(); chatConversation.Children.Add(composer);
        var buttons = new StackPanel { Margin = new Thickness(7, 0, 0, 0) }; buttons.Children.Add(send); buttons.Children.Add(stop);
        DockPanel.SetDock(buttons, Dock.Right); composer.Children.Add(buttons); composer.Children.Add(input);
        if (agendaMode) root.Children.Add(agendaEditor);
        AutomationProperties.SetName(this, agendaMode ? "行事历专用输入" : "角色头顶聊天"); AutomationProperties.SetName(input, agendaMode ? "日程内容" : "聊天内容"); AutomationProperties.SetName(send, agendaMode ? "整理日程" : "发送聊天");
        if (agendaMode) { close.Visibility = Visibility.Collapsed; send.Content = "整理"; }
        close.Click += (_, _) => pet.StopInteraction();
        stop.Click += (_, _) => pending?.Cancel();
        send.Click += async (_, _) => await SendText(input.Text);
        input.PreviewKeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) { e.Handled = true; await SendText(input.Text); }
        };
        try { if (File.Exists(OptionsPath)) Options = JsonSerializer.Deserialize<ChatOptions>(File.ReadAllText(OptionsPath)) ?? new(); }
        catch (Exception ex) when (ex is IOException or JsonException) { reply.Text = "聊天设置读取失败，请到设定重新填写。"; }
        input.TextChanged += (_, _) => pet.CompanionActivity();
        // The confirmation card is laid out after async replies. Re-anchor after
        // its actual height changes, including when the user changes pet size.
        SizeChanged += (_, _) => Dispatcher.BeginInvoke(() => { if (!disposed && Visibility == Visibility.Visible) pet.LayoutChat(true); });
        SetCompanion();
    }
    internal void Open()
    {
        if (!pet.AccessTo("chat").Allowed) return;
        SetCompanion(); Visibility = Visibility.Visible;
        if (pet.IsHitTestVisible && !agendaMode) { pet.Activate(); input.Focus(); Keyboard.Focus(input); }
    }
    internal void Close()
    {
        CancelResponse(); Visibility = Visibility.Collapsed;
        if (!agendaMode || pet.ActiveChat is null) pet.EndConversation();
    }
    internal void CancelResponse()
    {
        sessionGeneration++; pending?.Cancel(); pending = null; send.Visibility = Visibility.Visible; stop.Visibility = Visibility.Collapsed; ClearAgendaDraft();
    }
    internal void ResetSession()
    {
        Close(); history.Clear(); conversations.Clear(); input.Clear(); reply.Text = CompanionPersonas.Text(pet.Character.FamilyId, "hello"); apiKey = "";
    }
    internal void SetCompanion()
    {
        if (family == pet.Character.FamilyId) return;
        CancelResponse(); if (family.Length > 0) conversations[family] = history.ToList(); family = pet.Character.FamilyId; history.Clear();
        if (conversations.TryGetValue(family, out var saved)) history.AddRange(saved);
        input.Clear(); reply.Text = history.LastOrDefault(m => m.Role == "assistant")?.Content ?? (agendaMode ? "把事情和时间告诉我。" : CompanionPersonas.Text(family, "hello"));
    }
    internal string? SaveOptions(ChatOptions options, string key)
    {
        try
        {
            options = new(options.Endpoint.Trim(), options.Model.Trim(), options.EffectiveProvider, options.Workspace.Trim());
            if (!options.IsLocal) { CompanionProviders.Endpoint(options.Endpoint, options.EffectiveProvider, options.Model); if (options.Model.Length == 0) throw new ArgumentException("请填写模型名称。"); }
            Directory.CreateDirectory(App.DataRoot); File.WriteAllText(OptionsPath, JsonSerializer.Serialize(options));
            CancelResponse(); pet.ConversationListen(); pet.DismissWorkReminder(); Options = options; apiKey = options.IsLocal ? "" : key; return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return ex.Message; }
    }
    internal FrameworkElement SettingsPanel()
    {
        var panel = new StackPanel(); var provider = new ComboBox { MinHeight = 36 }; provider.Items.Add(new ComboBoxItem { Content = "本机对话", Tag = "local" });
        foreach (var spec in CompanionProviders.All) provider.Items.Add(new ComboBoxItem { Content = spec.Name, Tag = spec.Id });
        provider.SelectedItem = provider.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == Options.EffectiveProvider) ?? provider.Items[0];
        AutomationProperties.SetName(provider, "模型服务"); panel.Children.Add(TextLabel("模型服务")); panel.Children.Add(provider);
        var fields = new StackPanel(); panel.Children.Add(fields);
        var endpoint = new TextBox { Text = Options.Endpoint }; var model = new TextBox { Text = Options.Model }; var key = new PasswordBox { Password = apiKey }; var workspace = new TextBox { Text = Options.Workspace };
        StackPanel Field(string label, Control box) { var row = new StackPanel(); row.Children.Add(TextLabel(label)); AutomationProperties.SetName(box, label); row.Children.Add(box); fields.Children.Add(row); return row; }
        Field("API 地址", endpoint); Field("模型名称", model); Field("API Key（仅本次会话）", key); var workspaceRow = Field("Workspace ID（可选）", workspace);
        var status = new TextBlock { Foreground = CloudTheme.Muted, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap };
        var buttons = new WrapPanel(); var save = new Button { Content = "保存设定", Margin = new Thickness(0, 12, 10, 0) }; var test = new Button { Content = "测试连接", Margin = new Thickness(0, 12, 0, 0) };
        AutomationProperties.SetName(save, "保存接口设定"); AutomationProperties.SetName(test, "测试模型连接"); buttons.Children.Add(save); buttons.Children.Add(test); panel.Children.Add(buttons); panel.Children.Add(status);
        CancellationTokenSource? testing = null; int generation = 0;
        string Selected() => (string)((ComboBoxItem)provider.SelectedItem).Tag;
        ChatOptions Read() => Selected() == "local" ? new(Provider: "local") : new(endpoint.Text, model.Text, Selected(), workspace.Text);
        void CancelTest() { generation++; testing?.Cancel(); test.IsEnabled = true; }
        void Fields() { fields.Visibility = test.Visibility = Selected() == "local" ? Visibility.Collapsed : Visibility.Visible; workspaceRow.Visibility = Selected() == "claude" ? Visibility.Visible : Visibility.Collapsed; }
        provider.SelectionChanged += (_, _) => { CancelTest(); endpoint.Text = Selected() == "local" ? "" : CompanionProviders.Find(Selected()).Base; model.Clear(); key.Clear(); workspace.Clear(); status.Text = "尚未保存"; Fields(); };
        endpoint.TextChanged += (_, _) => { CancelTest(); key.Clear(); status.Text = "尚未保存"; }; model.TextChanged += (_, _) => CancelTest(); key.PasswordChanged += (_, _) => CancelTest(); workspace.TextChanged += (_, _) => CancelTest();
        save.Click += (_, _) => { CancelTest(); status.Text = SaveOptions(Read(), key.Password) ?? "已保存"; };
        test.Click += async (_, _) =>
        {
            CancelTest(); var cancellation = new CancellationTokenSource(); testing = cancellation; int turn = generation; test.IsEnabled = false; status.Text = "正在连接…";
            try { await CompanionProviders.SendAsync(client, Read(), key.Password, [new("user", "请只回复：连接成功")], "这是连接测试，只回复连接成功。", cancellation.Token); if (generation == turn) status.Text = "连接成功"; }
            catch (OperationCanceledException) { if (generation == turn) status.Text = "连接超时，请重试。"; }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidDataException or IOException or ArgumentException or FormatException) { if (generation == turn) status.Text = ex.Message; }
            finally { if (ReferenceEquals(testing, cancellation)) { testing = null; test.IsEnabled = true; } cancellation.Dispose(); }
        };
        panel.Unloaded += (_, _) => CancelTest(); Fields(); return panel;
        static TextBlock TextLabel(string text) => new() { Text = text, Foreground = CloudTheme.Muted, Margin = new Thickness(0, 9, 0, 4), FontSize = 12 };
    }
    internal Task<string> GenerateReminder(string family, int index, CancellationToken token) => CompanionProviders.SendAsync(client, Options, apiKey,
        [new("user", "工作提醒时间到了，请笑着用一句简短的话提醒我休息。主题：" + CompanionPersonas.Reminder(family, index))], CompanionPersonas.Prompt(family, pet.State.Companion(family).Score), token);
    internal async Task SendText(string text)
    {
        if (!pet.AccessTo("chat").Allowed || pending is not null || disposed || Visibility != Visibility.Visible || string.IsNullOrWhiteSpace(text)) return;
        text = text.Trim(); if (text.Length > 4000) text = text[..4000];
        SetCompanion();
        string turnFamily = family; int turnSession = sessionGeneration, sharedSession = pet.Chat.sessionGeneration;
        var service = agendaMode ? pet.Chat : this; var turnOptions = service.Options; string turnKey = service.apiKey;
        ClearAgendaDraft(); history.Add(new("user", text)); input.Clear(); reply.Text = "…";
        var cancellation = new CancellationTokenSource(); pending = cancellation;
        send.Visibility = Visibility.Collapsed; stop.Visibility = Visibility.Visible; pet.ConversationThinking();
        try
        {
            AgendaReply? result = null;
            string answer;
            if (agendaMode) { result = await PetAgenda.ReplyAsync(client, turnOptions, turnKey, history.ToArray(), turnFamily, pet.State.Companion(turnFamily).Score, cancellation.Token); answer = result.Reply; }
            else answer = await CompanionChat.ReplyAfterThinkingAsync(client, turnOptions, turnKey, history.ToArray(), turnFamily, cancellation.Token, pet.State.Companion(turnFamily).Score);
            if (disposed || turnFamily != family || turnFamily != pet.Character.FamilyId || turnSession != sessionGeneration || sharedSession != pet.Chat.sessionGeneration || cancellation.IsCancellationRequested || Visibility != Visibility.Visible) return;
            history.Add(new("assistant", answer)); if (history.Count > 48) history.RemoveRange(0, history.Count - 48);
            reply.Text = answer; pet.AwardCompanion(1, "聊了几句", "chat", 60); pet.ConversationReply(answer);
            if (result?.Event is { } draft) ShowAgendaDraft(draft);
        }
        catch (OperationCanceledException) { if (!disposed && turnFamily == family && turnSession == sessionGeneration) { reply.Text = cancellation.IsCancellationRequested ? "已停止" : "回复超时，请重试。"; pet.ConversationListen(); } }
        catch (ObjectDisposedException) when (disposed) { }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidDataException or IOException or ArgumentException or FormatException)
        { if (!disposed && turnFamily == family && turnSession == sessionGeneration) { reply.Text = ex.Message; pet.ConversationListen(); } }
        finally
        {
            if (ReferenceEquals(pending, cancellation)) pending = null;
            cancellation.Dispose();
            if (!disposed && pending is null) { send.Visibility = Visibility.Visible; stop.Visibility = Visibility.Collapsed; pet.LayoutChat(); }
        }
    }
    public void Dispose() { disposed = true; pending?.Cancel(); client.Dispose(); }
}

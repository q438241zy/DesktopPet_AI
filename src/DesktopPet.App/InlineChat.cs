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
internal sealed class InlineChat : Border, IDisposable
{
    private readonly PetWindow pet;
    private readonly List<ChatMessage> history = [];
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
    private string OptionsPath => Path.Combine(App.DataRoot, "chat-settings.json");

    public InlineChat(PetWindow pet)
    {
        this.pet = pet;
        Width = 306; Padding = new Thickness(13); CornerRadius = new CornerRadius(18);
        Background = CloudTheme.Cream; BorderBrush = CloudTheme.Line; BorderThickness = new Thickness(1);
        Visibility = Visibility.Collapsed;
        var root = new StackPanel(); Child = root;
        var row = new DockPanel();
        var close = new Button { Content = CloudTheme.Icon("close", 13), Width = 24, Height = 24, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), ToolTip = "收起聊天" };
        AutomationProperties.SetName(close, "收起聊天"); DockPanel.SetDock(close, Dock.Right); row.Children.Add(close);
        row.Children.Add(new ScrollViewer { Content = reply, MaxHeight = 110, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 0, 5, 10) }); root.Children.Add(row);
        var composer = new DockPanel(); root.Children.Add(composer);
        var buttons = new StackPanel { Margin = new Thickness(7, 0, 0, 0) }; buttons.Children.Add(send); buttons.Children.Add(stop);
        DockPanel.SetDock(buttons, Dock.Right); composer.Children.Add(buttons); composer.Children.Add(input);
        AutomationProperties.SetName(this, "角色头顶聊天"); AutomationProperties.SetName(input, "聊天内容"); AutomationProperties.SetName(send, "发送聊天");
        close.Click += (_, _) => pet.StopInteraction();
        stop.Click += (_, _) => pending?.Cancel();
        send.Click += async (_, _) => await SendText(input.Text);
        input.PreviewKeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) { e.Handled = true; await SendText(input.Text); }
        };
        try { if (File.Exists(OptionsPath)) Options = JsonSerializer.Deserialize<ChatOptions>(File.ReadAllText(OptionsPath)) ?? new(); }
        catch (Exception ex) when (ex is IOException or JsonException) { reply.Text = "聊天设置读取失败，请到桌面偏好重新填写。"; }
        SetCompanion();
    }
    internal void Open()
    {
        if (!pet.AccessTo("chat").Allowed) return;
        SetCompanion(); Visibility = Visibility.Visible;
        if (pet.IsHitTestVisible) { pet.Activate(); input.Focus(); Keyboard.Focus(input); }
    }
    internal void Close()
    {
        pending?.Cancel(); Visibility = Visibility.Collapsed;
        pet.EndConversation();
    }
    internal void ResetSession()
    {
        sessionGeneration++; Close(); history.Clear(); input.Clear(); reply.Text = "今天想聊些什么？"; apiKey = "";
    }
    internal void SetCompanion()
    {
        if (family == pet.Character.FamilyId) return;
        pending?.Cancel(); family = pet.Character.FamilyId; history.Clear(); input.Clear();
        reply.Text = "今天想聊些什么？";
    }
    internal string? SaveOptions(ChatOptions options, string key)
    {
        try
        {
            options = new(options.Endpoint.Trim(), options.Model.Trim());
            if (options.Endpoint.Length > 0) { CompanionChat.Endpoint(options.Endpoint); if (options.Model.Length == 0) throw new ArgumentException("请填写模型名称。"); }
            Directory.CreateDirectory(App.DataRoot); File.WriteAllText(OptionsPath, JsonSerializer.Serialize(options));
            Options = options; apiKey = key; return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return ex.Message; }
    }
    internal FrameworkElement SettingsPanel()
    {
        var panel = new StackPanel();
        var endpoint = new TextBox { Text = Options.Endpoint }; var model = new TextBox { Text = Options.Model }; var key = new PasswordBox { Password = apiKey };
        void Field(string label, Control box) { panel.Children.Add(new TextBlock { Text = label, Foreground = CloudTheme.Muted, Margin = new Thickness(0, 9, 0, 4), FontSize = 12 }); AutomationProperties.SetName(box, label); panel.Children.Add(box); }
        Field("API 地址（留空使用本机对话）", endpoint); Field("模型", model); Field("API 金钥（仅本次使用）", key);
        var status = new TextBlock { Foreground = CloudTheme.Muted, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap };
        var save = new Button { Content = "保存聊天设置", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 12, 0, 0) };
        save.Click += (_, _) => status.Text = SaveOptions(new(endpoint.Text, model.Text), key.Password) ?? "已保存";
        panel.Children.Add(save); panel.Children.Add(status); return panel;
    }
    internal async Task SendText(string text)
    {
        if (!pet.AccessTo("chat").Allowed || pending is not null || disposed || Visibility != Visibility.Visible || string.IsNullOrWhiteSpace(text)) return;
        text = text.Trim(); if (text.Length > 4000) text = text[..4000];
        string turnFamily = family; int turnSession = sessionGeneration; var turnOptions = Options; string turnKey = apiKey;
        history.Add(new("user", text)); input.Clear(); reply.Text = "…";
        var cancellation = new CancellationTokenSource(); pending = cancellation;
        send.Visibility = Visibility.Collapsed; stop.Visibility = Visibility.Visible; pet.ConversationThinking();
        try
        {
            string answer = await CompanionChat.ReplyAfterThinkingAsync(client, turnOptions, turnKey, history.ToArray(), pet.Character.Name, cancellation.Token);
            if (disposed || turnFamily != family || turnSession != sessionGeneration || cancellation.IsCancellationRequested || Visibility != Visibility.Visible) return;
            history.Add(new("assistant", answer)); if (history.Count > 48) history.RemoveRange(0, history.Count - 48);
            reply.Text = answer; pet.ConversationReply(answer);
        }
        catch (OperationCanceledException) { if (!disposed && turnFamily == family && turnSession == sessionGeneration) { reply.Text = cancellation.IsCancellationRequested ? "已停止" : "回复超时，请重试。"; pet.ConversationListen(); } }
        catch (ObjectDisposedException) when (disposed) { }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or IOException or ArgumentException or FormatException)
        { if (!disposed && turnFamily == family && turnSession == sessionGeneration) { reply.Text = ex.Message; pet.ConversationListen(); } }
        finally
        {
            if (ReferenceEquals(pending, cancellation)) pending = null;
            cancellation.Dispose();
            if (!disposed) { send.Visibility = Visibility.Visible; stop.Visibility = Visibility.Collapsed; pet.LayoutChat(); }
        }
    }
    public void Dispose() { disposed = true; pending?.Cancel(); client.Dispose(); }
}

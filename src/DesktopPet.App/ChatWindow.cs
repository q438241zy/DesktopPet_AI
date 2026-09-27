using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DesktopPet.Core;

namespace DesktopPet.App;

internal sealed class ChatWindow : Window
{
    private readonly PetWindow pet;
    private readonly List<ChatMessage> history = [];
    private readonly StackPanel messages = new() { Margin = new Thickness(18, 14, 18, 10) };
    private readonly TextBox input = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 44, MaxHeight = 110, MaxLength = 4000, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBox endpoint = new(), model = new();
    private readonly PasswordBox apiKey = new();
    private readonly TextBlock subtitle = new() { Foreground = CloudTheme.Muted, FontSize = 11 }, status = new() { Foreground = CloudTheme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(20, 2, 20, 8) };
    private readonly TextBlock heading = new() { FontSize = 18, FontWeight = FontWeights.SemiBold };
    private readonly Button send = new() { Content = "发送", MinWidth = 65 }, stop = new() { Content = "停止", Visibility = Visibility.Collapsed };
    private readonly ScrollViewer scroll;
    private CancellationTokenSource? pending;
    private string family = "";
    private bool closed;
    private readonly HttpClient client = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(45) };
    private string OptionsPath => Path.Combine(App.DataRoot, "chat-settings.json");
    internal IReadOnlyList<ChatMessage> History => history;

    public ChatWindow(PetWindow pet)
    {
        this.pet = pet; Title = "云朵伙伴 · 聊天"; Icon = CloudTheme.AppIcon;
        Width = 440; Height = 620; MinWidth = 360; MinHeight = 460; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = new FontFamily("Segoe UI Variable, Microsoft YaHei UI"); FontSize = 13; Foreground = CloudTheme.Ink; Background = CloudTheme.Brush("#FAFAFC");
        var root = new DockPanel { Background = Background }; Content = root;
        var top = new StackPanel { Margin = new Thickness(20, 18, 20, 8) }; top.Children.Add(heading); top.Children.Add(subtitle); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        var options = new StackPanel { Margin = new Thickness(0, 10, 0, 8) };
        void Field(string label, Control field) { options.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 8, 0, 4), Foreground = CloudTheme.Muted, FontSize = 11 }); options.Children.Add(field); AutomationProperties.SetName(field, label); }
        Field("API 地址（留空使用本机对话）", endpoint); Field("模型", model); Field("API 金钥（仅本次使用）", apiKey);
        var save = new Button { Content = "保存设置", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) }; save.Click += (_, _) => SaveOptions(); options.Children.Add(save);
        top.Children.Add(new Expander { Header = "聊天设置", Content = options, Margin = new Thickness(0, 12, 0, 0) });
        var bottom = new StackPanel(); DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom); bottom.Children.Add(status);
        var composer = new DockPanel { Margin = new Thickness(16, 0, 16, 16) }; bottom.Children.Add(composer);
        var controls = new StackPanel { Margin = new Thickness(8, 0, 0, 0) }; controls.Children.Add(send); controls.Children.Add(stop); DockPanel.SetDock(controls, Dock.Right); composer.Children.Add(controls); composer.Children.Add(input);
        AutomationProperties.SetName(input, "聊天内容"); AutomationProperties.SetName(send, "发送聊天");
        scroll = new ScrollViewer { Content = messages, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; root.Children.Add(scroll);
        send.Click += async (_, _) => await SendText(input.Text); stop.Click += (_, _) => pending?.Cancel();
        input.PreviewKeyDown += async (_, e) => { if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) { e.Handled = true; await SendText(input.Text); } };
        try { if (File.Exists(OptionsPath)) { var saved = JsonSerializer.Deserialize<ChatOptions>(File.ReadAllText(OptionsPath)); endpoint.Text = saved?.Endpoint ?? ""; model.Text = saved?.Model ?? ""; } } catch (Exception ex) when (ex is IOException or JsonException) { status.Text = "聊天设置读取失败，可以重新填写。"; }
        SetCompanion(); Loaded += (_, _) => input.Focus();
        Closed += (_, _) => { closed = true; pending?.Cancel(); client.Dispose(); pet.EndConversation(); };
    }
    private ChatOptions Options => new(endpoint.Text.Trim(), model.Text.Trim());
    private bool SaveOptions()
    {
        try
        {
            if (Options.Endpoint.Length > 0) { CompanionChat.Endpoint(Options.Endpoint); if (Options.Model.Length == 0) throw new ArgumentException("请填写模型名称。"); }
            Directory.CreateDirectory(App.DataRoot); File.WriteAllText(OptionsPath, JsonSerializer.Serialize(Options));
            subtitle.Text = Options.Endpoint.Length == 0 ? "本机对话" : Options.Model; status.Text = ""; return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { status.Text = ex.Message; return false; }
    }
    internal void SetCompanion()
    {
        if (family != pet.Character.FamilyId) { pending?.Cancel(); history.Clear(); messages.Children.Clear(); family = pet.Character.FamilyId; AddBubble("你好呀。今天想聊些什么？", false); }
        heading.Text = pet.Character.Name.Split('·')[0].Trim(); subtitle.Text = Options.Endpoint.Length == 0 ? "本机对话" : Options.Model;
    }
    private void AddBubble(string text, bool user)
    {
        var label = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = CloudTheme.Ink, LineHeight = 21 };
        messages.Children.Add(new Border { Child = label, MaxWidth = 335, HorizontalAlignment = user ? HorizontalAlignment.Right : HorizontalAlignment.Left, Background = user ? CloudTheme.Pale : Brushes.White, BorderBrush = CloudTheme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(16), Padding = new Thickness(13, 10, 13, 10), Margin = new Thickness(0, 0, 0, 12) });
        scroll.UpdateLayout(); scroll.ScrollToEnd();
    }
    internal async Task SendText(string text)
    {
        if (pending is not null || string.IsNullOrWhiteSpace(text) || !SaveOptions()) return;
        text = text.Trim(); if (text.Length > 4000) text = text[..4000];
        var turnOptions = Options; string turnKey = apiKey.Password;
        string turnFamily = family; history.Add(new("user", text)); AddBubble(text, true); input.Clear();
        var cancellation = new CancellationTokenSource(); pending = cancellation; send.IsEnabled = false; stop.Visibility = Visibility.Visible;
        status.Text = Options.Endpoint.Length == 0 ? "" : "正在回复…"; pet.BeginConversation();
        try
        {
            if (turnOptions.Endpoint.Length == 0) await Task.Delay(360, cancellation.Token);
            string reply = await CompanionChat.ReplyAsync(client, turnOptions, turnKey, history.ToArray(), pet.Character.Name, cancellation.Token);
            if (closed || turnFamily != family || cancellation.IsCancellationRequested) return;
            history.Add(new("assistant", reply)); if (history.Count > 48) history.RemoveRange(0, history.Count - 48);
            AddBubble(reply, false); status.Text = ""; pet.ConversationReply(reply);
        }
        catch (OperationCanceledException) { if (!closed && turnFamily == family) { status.Text = cancellation.IsCancellationRequested ? "已停止" : "回复超时，请重试。"; pet.ConversationListen(); } }
        catch (ObjectDisposedException) when (closed) { }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or IOException or ArgumentException or FormatException)
        { if (!closed && turnFamily == family) { status.Text = ex.Message; pet.ConversationListen(); } }
        finally { pending = null; cancellation.Dispose(); if (!closed) { send.IsEnabled = true; stop.Visibility = Visibility.Collapsed; input.Focus(); } }
    }
}

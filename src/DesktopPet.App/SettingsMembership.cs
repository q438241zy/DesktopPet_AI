using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopPet.Core;

namespace DesktopPet.App;

public sealed partial class SettingsWindow
{
    private bool registerAccount;
    internal void ShowMembership() => Navigate("members");
    internal void RefreshMembership() { if (page is "members" or "partners" or "life" or "preferences") Rebuild(); }
    private void MemberHome()
    {
        var row = new DockPanel();
        var open = MakeButton(pet.Accounts.CurrentAccount is null ? "注册 / 登录" : "查看会员权益", ShowMembership, "member");
        open.Padding = new Thickness(14, 9, 14, 9); open.VerticalAlignment = VerticalAlignment.Center;
        DockPanel.SetDock(open, Dock.Right); row.Children.Add(open);
        var summary = new StackPanel { Margin = new Thickness(0, 0, 16, 0) }; row.Children.Add(summary);
        var account = pet.Accounts.CurrentAccount;
        summary.Children.Add(Text(account is null ? "云朵会员" : account.Nickname + " · " + Membership.Name(account.Tier), 17));
        summary.Children.Add(Text(account is null ? "本机账号 · 注册后从黄铜开始" : "本机账号 · " + account.Username, 11, true));
        var levels = new WrapPanel(); summary.Children.Add(levels);
        foreach (var tier in Membership.Tiers)
        {
            var colors = MemberVisual.Colors(tier); var badge = CloudTheme.Badge(Membership.Name(tier), colors.Surface);
            ((TextBlock)badge.Child).Foreground = colors.Ink; levels.Children.Add(badge);
        }
        var card = Card(row, CloudTheme.Sky("#FFF1F6", "#FFFAF5")); card.Padding = new Thickness(19, 15, 19, 15); card.Margin = new Thickness(0, 5, 0, 10); content.Children.Add(card);
    }
    private void Members()
    {
        Heading("", "云朵会员", "从一次相遇，慢慢走近。这里可以注册、登录并查看会员权益。");
        var tiers = new System.Windows.Controls.Primitives.UniformGrid { Columns = 5, Margin = new Thickness(-4, 8, -4, 5) };
        foreach (var tier in Membership.Tiers)
        {
            var colors = MemberVisual.Colors(tier); var stack = new StackPanel();
            stack.Children.Add(new LineIcon { Glyph = "member", Width = 26, Height = 26, Foreground = colors.Ink, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 11) });
            stack.Children.Add(new TextBlock { Text = Membership.Name(tier), Foreground = colors.Ink, FontSize = 18, FontWeight = FontWeights.SemiBold });
            stack.Children.Add(new TextBlock { Text = tier == MembershipTier.BlackGold ? "全部姿态开放" : "姿态权益待公布", Foreground = colors.Ink, FontSize = 10, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap });
            var chat = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            bool chatOpen = Membership.Access("chat", tier).Allowed;
            if (!chatOpen) chat.Children.Add(new MemberLock { Width = 14, Height = 16, Margin = new Thickness(0, 0, 5, 0) });
            chat.Children.Add(new TextBlock { Text = chatOpen ? "聊天开放" : "聊天未开放", Foreground = colors.Ink, FontSize = 10, VerticalAlignment = VerticalAlignment.Center }); stack.Children.Add(chat);
            var tile = new Border { Background = colors.Surface, CornerRadius = new CornerRadius(19), Padding = new Thickness(13, 16, 10, 14), Margin = new Thickness(4), Child = stack,
                BorderBrush = pet.Accounts.CurrentAccount?.Tier == tier ? CloudTheme.Blue : Brushes.Transparent, BorderThickness = new Thickness(1.5) };
            AutomationProperties.SetName(tile, Membership.Name(tier) + "会员权益"); tiers.Children.Add(tile);
        }
        content.Children.Add(tiers);
        content.Children.Add(Text("聊天从黄金开始开放；其他等级的姿态权益等待公布。", 11, true));
        if (pet.Accounts.CurrentAccount is { } account)
        {
            var panel = new DockPanel(); var logout = MakeButton("退出登录", () => pet.Accounts.Logout());
            logout.Padding = new Thickness(14, 8, 14, 8); logout.VerticalAlignment = VerticalAlignment.Center; DockPanel.SetDock(logout, Dock.Right); panel.Children.Add(logout);
            var profile = new StackPanel(); profile.Children.Add(Text(account.Nickname + " · " + Membership.Name(account.Tier), 21));
            profile.Children.Add(Text("本机账号  " + account.Username, 12, true)); profile.Children.Add(Text("已登录 · 本次登录期间有效", 11, true)); panel.Children.Add(profile);
            content.Children.Add(Card(panel, CloudTheme.Sky()));
        }
        else AccountForm();
        var access = pet.AccessTo("chat");
        var preview = new DockPanel(); preview.Children.Add(MemberVisual.ActionIcon("chat", !access.Allowed, 28));
        var details = new StackPanel { Margin = new Thickness(16, 0, 0, 0) }; details.Children.Add(Text("陪伴聊天", 16));
        details.Children.Add(Text(access.Allowed ? "已开放 · 在伙伴头顶聊聊今天。" : "黄金、白金、黑金会员开放。", 12, true)); preview.Children.Add(details);
        content.Children.Add(Card(preview));
    }
    private void AccountForm()
    {
        var panel = new StackPanel { MaxWidth = 490, HorizontalAlignment = HorizontalAlignment.Left };
        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
        foreach (bool registration in new[] { false, true })
        {
            var tab = MakeButton(registration ? "注册新账号" : "已有账号登录", () => { registerAccount = registration; Rebuild(); });
            tab.Background = registerAccount == registration ? CloudTheme.Pale : Brushes.Transparent; tab.BorderThickness = new Thickness(0); tab.Padding = new Thickness(13, 8, 13, 8); tab.Margin = new Thickness(0, 0, 8, 0); tabs.Children.Add(tab);
        }
        panel.Children.Add(tabs); panel.Children.Add(Text("本机账号", 12, true));
        var username = new TextBox { MinWidth = 340, MaxLength = 24, Height = 40 };
        var nickname = new TextBox { MaxLength = 20, Height = 40 };
        var password = new PasswordBox { MaxLength = 128, Height = 40, Style = (Style)FindResource("MemberPasswordBox") };
        var confirmation = new PasswordBox { MaxLength = 128, Height = 40, Style = (Style)FindResource("MemberPasswordBox") };
        void Field(string label, string automationName, Control control)
        { panel.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = muted, Margin = new Thickness(0, 0, 0, 5) }); AutomationProperties.SetName(control, automationName); control.Margin = new Thickness(0, 0, 0, 11); panel.Children.Add(control); }
        bool registering = registerAccount;
        Field("账号", registering ? "注册账号" : "登录账号", username);
        if (registering) Field("昵称", "注册昵称", nickname);
        Field("密码", registering ? "注册密码" : "登录密码", password);
        if (registering) { Field("确认密码", "确认密码", confirmation); panel.Children.Add(Text("账号 4–24 位；密码 15–128 个字符，支持空格。", 11, true)); }
        var message = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = CloudTheme.Blue, FontSize = 12, Margin = new Thickness(0, 0, 0, 10), MaxWidth = 400 };
        AutomationProperties.SetName(message, "账号提示"); panel.Children.Add(message);
        async Task Submit()
        {
            panel.IsEnabled = false; message.Text = registering ? "正在注册…" : "正在登录…";
            string enteredPassword = password.Password, repeatedPassword = confirmation.Password; password.Clear(); confirmation.Clear();
            try
            {
                var result = registering
                    ? await pet.Accounts.RegisterAsync(username.Text, nickname.Text, enteredPassword, repeatedPassword)
                    : await pet.Accounts.LoginAsync(username.Text, enteredPassword);
                message.Text = result.Message; if (result.Success && IsVisible) RefreshMembership();
            }
            finally { panel.IsEnabled = true; }
        }
        var submit = MakeButton(registering ? "注册并登录" : "登录", async () => await Submit(), "member");
        submit.HorizontalAlignment = HorizontalAlignment.Left; submit.Padding = new Thickness(18, 10, 18, 10); panel.Children.Add(submit);
        password.KeyDown += async (_, e) => { if (e.Key == System.Windows.Input.Key.Enter && !registering && panel.IsEnabled) { e.Handled = true; await Submit(); } };
        confirmation.KeyDown += async (_, e) => { if (e.Key == System.Windows.Input.Key.Enter && panel.IsEnabled) { e.Handled = true; await Submit(); } };
        content.Children.Add(Card(panel));
    }
}

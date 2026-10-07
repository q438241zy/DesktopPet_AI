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
    private void Members()
    {
        Heading("", "会员中心", "");
        if (pet.Accounts.CurrentAccount is { } account)
        {
            var panel = new StackPanel { MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Center };
            panel.Children.Add(Text(account.Nickname, 22)); panel.Children.Add(Text(account.Username, 12, true));
            panel.Children.Add(CloudTheme.Badge(Membership.Name(account.Tier), MemberVisual.Colors(account.Tier).Surface));
            panel.Children.Add(MakeButton("退出登录", () => pet.Accounts.Logout())); content.Children.Add(Card(panel));
        }
        else AccountForm();
    }
    private void AccountForm()
    {
        var panel = new StackPanel { MaxWidth = 440, HorizontalAlignment = HorizontalAlignment.Center };
        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
        foreach (bool registration in new[] { false, true })
        {
            var tab = MakeButton(registration ? "注册" : "登录", () => { registerAccount = registration; Rebuild(); });
            AutomationProperties.SetName(tab, registration ? "注册分页" : "登录分页");
            tab.Background = registerAccount == registration ? CloudTheme.Pale : Brushes.Transparent; tab.BorderThickness = new Thickness(0); tab.Padding = new Thickness(13, 8, 13, 8); tab.Margin = new Thickness(0, 0, 8, 0); tabs.Children.Add(tab);
        }
        panel.Children.Add(new CloudIcon { Width = 64, Height = 58, HorizontalAlignment = HorizontalAlignment.Center }); panel.Children.Add(Text("云朵会员", 22)); panel.Children.Add(tabs);
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

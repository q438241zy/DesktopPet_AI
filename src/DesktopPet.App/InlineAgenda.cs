using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using DesktopPet.Core;

namespace DesktopPet.App;

internal sealed partial class InlineChat
{
    private readonly StackPanel agendaEditor = new() { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 9, 0, 0) };
    private readonly StackPanel chatConversation = new();
    internal bool HasAgendaDraft => agendaEditor.Visibility == Visibility.Visible;
    private void ClearAgendaDraft() { agendaEditor.Children.Clear(); agendaEditor.Visibility = Visibility.Collapsed; chatConversation.Visibility = Visibility.Visible; }
    internal void ShowAgendaDraft(AgendaDraft draft, string? id = null)
    {
        ClearAgendaDraft(); agendaEditor.Visibility = Visibility.Visible; chatConversation.Visibility = Visibility.Collapsed;
        agendaEditor.Children.Add(new TextBlock { Text = id is null ? "确认日程" : "修改日程", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
        TextBox Box(string value, int max) => new() { Text = value, MaxLength = max, MinHeight = 0, Height = 31, Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0) };
        var title = Box(draft.Title, 80); var date = Box(draft.StartLocal.Replace('T', ' '), 16);
        var lead = Box(draft.LeadMinutes.ToString(CultureInfo.InvariantCulture), 5); lead.Width = 51;
        var repeat = new ComboBox { MinWidth = 108, MinHeight = 0, Height = 31, Margin = new Thickness(0), Padding = new Thickness(8, 4, 8, 4) };
        foreach (string value in PetAgenda.Repeats) repeat.Items.Add(new ComboBoxItem { Tag = value, Content = PetAgenda.RepeatName(value), IsSelected = value == draft.Repeat });
        void Field(string label, Control box)
        {
            agendaEditor.Children.Add(new TextBlock { Text = label, FontSize = 10, Foreground = CloudTheme.Muted, Margin = new Thickness(0, 5, 0, 3) });
            AutomationProperties.SetName(box, label); agendaEditor.Children.Add(box);
        }
        Field("事情", title); Field("日期时间 · yyyy-MM-dd HH:mm", date);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 5) };
        row.Children.Add(new TextBlock { Text = "提前", VerticalAlignment = VerticalAlignment.Center }); row.Children.Add(lead); row.Children.Add(new TextBlock { Text = " 分钟　", VerticalAlignment = VerticalAlignment.Center }); row.Children.Add(repeat); agendaEditor.Children.Add(row);
        AutomationProperties.SetName(lead, "日程提前分钟"); AutomationProperties.SetName(repeat, "日程重复方式");
        string owner = family;
        agendaEditor.Children.Add(new TextBlock { Text = CompanionPersonas.Text(owner, "name") + " · " + PetAgenda.Zone, FontSize = 10, Foreground = CloudTheme.Muted, TextWrapping = TextWrapping.Wrap });
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = CloudTheme.Blue, FontSize = 11, Margin = new Thickness(0, 5, 0, 0) }; agendaEditor.Children.Add(error);
        var buttons = new WrapPanel { Margin = new Thickness(0, 7, 0, 0) };
        var save = new Button { Content = "收进行事历", Padding = new Thickness(9, 5, 9, 5), Margin = new Thickness(0, 0, 6, 0) };
        var cancel = new Button { Content = "暂不保存", Padding = new Thickness(9, 5, 9, 5) };
        AutomationProperties.SetName(save, "确认保存日程"); AutomationProperties.SetName(cancel, "暂不保存日程"); buttons.Children.Add(save); buttons.Children.Add(cancel); agendaEditor.Children.Add(buttons);
        cancel.Click += (_, _) => { ClearAgendaDraft(); reply.Text = "这次没有保存，想好了再告诉我。"; pet.LayoutChat(); };
        save.Click += (_, _) =>
        {
            try
            {
                if (!HasAgendaDraft || owner != family || !pet.AccessTo("chat").Allowed) return;
                if (!int.TryParse(lead.Text, out int minutes)) throw new InvalidDataException("提前分钟需要填写整数。");
                var changed = draft with { Title = title.Text, StartLocal = date.Text.Trim().Replace(' ', 'T'), LeadMinutes = minutes, Repeat = (string)((ComboBoxItem)repeat.SelectedItem).Tag, Zone = PetAgenda.Zone };
                var saved = pet.Agenda.Put(changed, owner, DateTimeOffset.UtcNow, id);
                ClearAgendaDraft(); reply.Text = $"记下啦：{saved.Title}，{saved.StartAt.ToLocalTime():M月d日 HH:mm}。";
                history.Add(new("assistant", reply.Text)); pet.ConversationReply(reply.Text); pet.AgendaChanged();
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException) { error.Text = ex.Message; }
            pet.LayoutChat(true);
        };
        pet.LayoutChat(true);
        Dispatcher.BeginInvoke(() => { pet.LayoutChat(true); if (Child is ScrollViewer scroll) scroll.ScrollToTop(); });
    }
}

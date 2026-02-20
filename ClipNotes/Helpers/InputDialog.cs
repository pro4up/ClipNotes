using System.Windows;
using System.Windows.Controls;

namespace ClipNotes.Helpers;

/// <summary>Простой диалог ввода текста.</summary>
public static class InputDialog
{
    /// <summary>Показывает диалог ввода. Возвращает null если пользователь отказался.</summary>
    public static string? Show(string title, string prompt, string defaultValue = "")
    {
        var win = new Window
        {
            Title = title,
            Width = 400,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Application.Current?.MainWindow,
            ShowInTaskbar = false,
        };

        // Apply current app theme resources
        if (Application.Current?.Resources != null)
            foreach (var key in Application.Current.Resources.Keys)
                win.Resources[key] = Application.Current.Resources[key];

        var panel = new StackPanel { Margin = new Thickness(20) };
        var label = new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap };
        var textBox = new TextBox { Text = defaultValue, Margin = new Thickness(0, 0, 0, 16) };
        textBox.SelectAll();

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var ok = new Button { Content = "OK", Width = 80, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "Отмена", Width = 80, IsCancel = true };

        bool confirmed = false;
        ok.Click += (_, _) => { confirmed = true; win.Close(); };
        cancel.Click += (_, _) => win.Close();

        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(label);
        panel.Children.Add(textBox);
        panel.Children.Add(buttons);
        win.Content = panel;

        win.Loaded += (_, _) => textBox.Focus();
        win.ShowDialog();

        return confirmed ? textBox.Text : null;
    }
}

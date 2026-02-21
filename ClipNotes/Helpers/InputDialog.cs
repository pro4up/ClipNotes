using System.Windows;
using System.Windows.Controls;
using Brush      = System.Windows.Media.Brush;
using FontFamily = System.Windows.Media.FontFamily;
using Style      = System.Windows.Style;

namespace ClipNotes.Helpers;

/// <summary>Простой диалог ввода текста, следующий теме приложения.</summary>
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

        // Копируем все ресурсы приложения (темы, стили, кисти)
        if (Application.Current?.Resources != null)
            foreach (var key in Application.Current.Resources.Keys)
                win.Resources[key] = Application.Current.Resources[key];

        // Применяем фон из темы
        if (win.Resources["BgBrush"] is Brush bgBrush)
            win.Background = bgBrush;

        // Тёмный заголовок окна (DWM)
        win.SourceInitialized += (_, _) => App.ApplyTitleBarTheme(win, App.IsDark);

        var panel = new StackPanel { Margin = new Thickness(20) };

        var label = new TextBlock
        {
            Text = prompt,
            Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 14,
        };
        if (win.Resources["TextPrimaryBrush"] is Brush fgBrush)
            label.Foreground = fgBrush;

        var textBox = new TextBox { Text = defaultValue, Margin = new Thickness(0, 0, 0, 16) };
        textBox.SelectAll();

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

        var ok = new Button
        {
            Content = "OK",
            Width = 80,
            IsDefault = true,
            Margin = new Thickness(0, 0, 8, 0),
        };
        if (win.Resources["PrimaryButton"] is Style primaryStyle)
            ok.Style = primaryStyle;

        var cancel = new Button
        {
            Content = LocalizationService.T("loc_BtnCancel"),
            Width = 80,
            IsCancel = true,
        };
        if (win.Resources["SecondaryButton"] is Style secondaryStyle)
            cancel.Style = secondaryStyle;

        bool confirmed = false;
        ok.Click     += (_, _) => { confirmed = true; win.Close(); };
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

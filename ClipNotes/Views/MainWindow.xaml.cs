using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ClipNotes.Models;
using ClipNotes.ViewModels;

namespace ClipNotes.Views;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.InitializeHotkeys(this);
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        ViewModel.Cleanup();
    }

    private void HotkeyTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
            tb.Background = App.IsDark
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 76, 20))
                : System.Windows.Media.Brushes.LightYellow;
    }

    private void HotkeyTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
            tb.ClearValue(TextBox.BackgroundProperty);
    }

    private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LWin || key == Key.RWin)
            return;

        if (sender is TextBox tb && tb.Tag is HotkeyBinding binding)
        {
            var modifiers = Keyboard.Modifiers;

            if (key == Key.Escape)
            {
                binding.Key = Key.None;
                binding.Modifiers = ModifierKeys.None;
            }
            else
            {
                binding.Key = key;
                binding.Modifiers = modifiers;
            }

            // Force UI refresh by re-setting the key
            var tempKey = binding.Key;
            binding.Key = Key.None;
            binding.Key = tempKey;
            tb.ClearValue(TextBox.BackgroundProperty);

            ViewModel.SaveSettings();
            ViewModel.RefreshHotkeys();

            Keyboard.ClearFocus();
        }
    }
}

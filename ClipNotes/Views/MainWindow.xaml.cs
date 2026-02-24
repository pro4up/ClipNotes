using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ClipNotes.Models;
using ClipNotes.ViewModels;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace ClipNotes.Views;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;
    private Storyboard? _holdPulseStoryboard;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.InitializeHotkeys(this);
        App.ApplyTitleBarTheme(this, App.IsDark);
        SetupHoldAnimation();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        SyncDatePresetCombo();
        ViewModel.InitializeLogs();
    }

    /// <summary>Syncs the preset ComboBox selection to match the currently loaded DateSuffixFormat.</summary>
    private void SyncDatePresetCombo()
    {
        var format = ViewModel.DateSuffixFormat;
        foreach (ComboBoxItem item in DatePresetCombo.Items)
        {
            var tag = item.Tag?.ToString();
            if (!string.IsNullOrEmpty(tag) && tag != "Custom" && tag == format)
            {
                DatePresetCombo.SelectionChanged -= DateSuffixPreset_SelectionChanged;
                DatePresetCombo.SelectedItem = item;
                DatePresetCombo.SelectionChanged += DateSuffixPreset_SelectionChanged;
                return;
            }
        }
        // No preset matched → select "Custom"
        DatePresetCombo.SelectionChanged -= DateSuffixPreset_SelectionChanged;
        DatePresetCombo.SelectedIndex = DatePresetCombo.Items.Count - 1;
        DatePresetCombo.SelectionChanged += DateSuffixPreset_SelectionChanged;
    }

    private void SetupHoldAnimation()
    {
        var anim = new DoubleAnimation
        {
            From = 1.0, To = 0.45,
            Duration = new Duration(TimeSpan.FromSeconds(0.6)),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        Storyboard.SetTarget(anim, HoldIndicator);
        Storyboard.SetTargetProperty(anim, new PropertyPath(UIElement.OpacityProperty));
        _holdPulseStoryboard = new Storyboard();
        _holdPulseStoryboard.Children.Add(anim);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.IsHolding)) return;
        if (ViewModel.IsHolding)
            _holdPulseStoryboard?.Begin();
        else
        {
            _holdPulseStoryboard?.Stop();
            HoldIndicator.Opacity = 1.0;
        }
    }

    private async void MarkerButtons_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!ViewModel.HoldModeEnabled || !ViewModel.IsRecording) return;

        // Find the Button in the visual tree (OriginalSource may be inner TextBlock)
        var element = e.OriginalSource as DependencyObject;
        while (element != null && element is not Button)
            element = VisualTreeHelper.GetParent(element);
        if (element is not Button btn || btn.Tag is not MarkerType type) return;

        e.Handled = true; // prevent normal Click / Command
        await ViewModel.StartUiHoldAsync(type);
    }

    private async void Window_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel.HoldModeEnabled && ViewModel.IsHolding)
            await ViewModel.EndUiHoldAsync();
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && ViewModel.MinimizeToTray)
            ((App)Application.Current).HideToTray();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        ViewModel.Cleanup();
    }

    // Settings nav
    private void ScrollToCard(FrameworkElement card)
    {
        // Get card position within the current viewport, then compute absolute scroll offset
        var pt = card.TransformToVisual(SettingsScroll).Transform(new System.Windows.Point(0, 0));
        SettingsScroll.ScrollToVerticalOffset(SettingsScroll.VerticalOffset + pt.Y);
    }

    private void NavTo_Theme(object sender, RoutedEventArgs e)          => ScrollToCard(CardTheme);
    private void NavTo_Obs(object sender, RoutedEventArgs e)             => ScrollToCard(CardObs);
    private void NavTo_OutputDir(object sender, RoutedEventArgs e)       => ScrollToCard(CardOutputDir);
    private void NavTo_CustomPaths(object sender, RoutedEventArgs e)     => ScrollToCard(CardCustomPaths);
    private void NavTo_Naming(object sender, RoutedEventArgs e)          => ScrollToCard(CardNaming);
    private void NavTo_Timecodes(object sender, RoutedEventArgs e)       => ScrollToCard(CardTimecodes);
    private void NavTo_Audio(object sender, RoutedEventArgs e)           => ScrollToCard(CardAudio);
    private void NavTo_Transcription(object sender, RoutedEventArgs e)   => ScrollToCard(CardTranscription);
    private void NavTo_HistorySet(object sender, RoutedEventArgs e)      => ScrollToCard(CardHistorySet);
    private void NavTo_Hotkeys(object sender, RoutedEventArgs e)         => ScrollToCard(CardHotkeys);

    private void DateSuffixPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return; // skip initial XAML initialization firing
        if (sender is ComboBox cb && cb.SelectedItem is ComboBoxItem item)
        {
            var tag = item.Tag?.ToString();
            if (tag == "Custom" || string.IsNullOrEmpty(tag)) return;
            ViewModel.DateSuffixFormat = tag;
        }
    }

    private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb) tb.ScrollToEnd();
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

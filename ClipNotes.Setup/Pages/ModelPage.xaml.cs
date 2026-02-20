using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClipNotes.Setup.Models;

namespace ClipNotes.Setup.Pages;

public partial class ModelPage : UserControl
{
    private readonly InstallOptions _options;

    public ModelPage(InstallOptions options)
    {
        _options = options;
        InitializeComponent();

        PageTitle.Text    = Loc.T("inst_ModelTitle");
        PageSubtitle.Text = Loc.T("inst_ModelSubtitle");

        BuildModelList();
        UpdateNote();
    }

    private void BuildModelList()
    {
        ModelList.Children.Clear();
        foreach (var model in ModelInfo.All)
        {
            var radio = new RadioButton
            {
                Style     = (Style)FindResource("ModelRadioStyle"),
                IsChecked = model.Id == _options.Model,
                Tag       = model.Id,
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel();

            var nameRow = new StackPanel { Orientation = Orientation.Horizontal };
            nameRow.Children.Add(new TextBlock
            {
                Text       = model.DisplayName,
                FontSize   = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
            });
            if (model.Recommended)
            {
                nameRow.Children.Add(new Border
                {
                    Margin       = new Thickness(8, 0, 0, 0),
                    Background   = (Brush)FindResource("AccentBrush"),
                    CornerRadius = new CornerRadius(4),
                    Padding      = new Thickness(6, 1, 6, 1),
                    Child        = new TextBlock
                    {
                        Text       = Loc.T("inst_Recommended"),
                        FontSize   = 10,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.White,
                    }
                });
            }
            left.Children.Add(nameRow);
            left.Children.Add(new TextBlock
            {
                Text       = $"{Loc.T("inst_Accuracy")} {Loc.T(model.AccuracyKey)}",
                FontSize   = 11,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                Margin     = new Thickness(0, 2, 0, 0),
            });

            var right = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };
            right.Children.Add(new TextBlock
            {
                Text                = model.SizeMB,
                FontSize            = 13,
                FontWeight          = FontWeights.SemiBold,
                Foreground          = (Brush)FindResource("TextPrimaryBrush"),
                HorizontalAlignment = HorizontalAlignment.Right,
            });
            right.Children.Add(new TextBlock
            {
                Text                = $"VRAM {model.VramGB}",
                FontSize            = 11,
                Foreground          = (Brush)FindResource("TextSecondaryBrush"),
                HorizontalAlignment = HorizontalAlignment.Right,
            });

            Grid.SetColumn(left,  0);
            Grid.SetColumn(right, 1);
            grid.Children.Add(left);
            grid.Children.Add(right);

            radio.Content = grid;
            radio.Checked += (_, _) =>
            {
                _options.Model = (string)radio.Tag;
                UpdateNote();
            };

            ModelList.Children.Add(radio);
        }
    }

    private void UpdateNote()
    {
        var model = ModelInfo.All.FirstOrDefault(m => m.Id == _options.Model);
        if (model != null)
            NoteText.Text = Loc.T("inst_ModelNote", model.DisplayName, model.SizeMB);
    }
}

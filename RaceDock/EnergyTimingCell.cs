// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RaceDock.Core;
using ProgressBar = System.Windows.Controls.ProgressBar;

namespace RaceDock.App;

/// <summary>Provider-reported virtual energy allocation, distinct from fuel remaining.</summary>
internal sealed class EnergyTimingCell
{
    private static readonly SolidColorBrush GoodBrush = ClassificationRow.Frozen(Color.FromRgb(54, 190, 107));
    private static readonly SolidColorBrush WarningBrush = ClassificationRow.Frozen(Color.FromRgb(226, 182, 36));
    private static readonly SolidColorBrush LowBrush = ClassificationRow.Frozen(Color.FromRgb(205, 45, 62));
    private readonly TextBlock _value = new() { Text = "—", FontSize = 10.5, TextAlignment = TextAlignment.Center };
    private readonly ProgressBar _bar = new() { Minimum = 0, Maximum = 100, Height = 3, Margin = new Thickness(3, 3, 3, 0) };
    public StackPanel Element { get; } = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 6, 0) };

    public EnergyTimingCell()
    {
        _value.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        _bar.SetResourceReference(ProgressBar.ForegroundProperty, "AccentFillColorDefaultBrush");
        Element.Children.Add(_value);
        Element.Children.Add(_bar);
    }

    public void Update(double? percent)
    {
        var level = EnergyDisplay.Level(percent);
        var valid = level != EnergyLevel.Unavailable;
        ApplyColor(level);
        _value.Text = valid ? percent!.Value.ToString("0.#", CultureInfo.InvariantCulture) + "%" : "—";
        _bar.Visibility = valid ? Visibility.Visible : Visibility.Hidden;
        _bar.Value = valid ? percent!.Value : 0;
        Element.ToolTip = valid ? $"Virtual energy tank: {_value.Text} remaining (not physical fuel level)" : "Virtual energy tank unavailable";
    }

    private void ApplyColor(EnergyLevel level)
    {
        var brush = level switch
        {
            EnergyLevel.Good => GoodBrush,
            EnergyLevel.Warning => WarningBrush,
            EnergyLevel.Low => LowBrush,
            _ => null
        };
        if (brush is null)
        {
            _value.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorTertiaryBrush");
            _bar.SetResourceReference(ProgressBar.ForegroundProperty, "AccentFillColorDefaultBrush");
            return;
        }
        _value.Foreground = brush;
        _bar.Foreground = brush;
    }
}

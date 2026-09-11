// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RaceDock.Core;
using Control = System.Windows.Controls.Control;

namespace RaceDock.App;

/// <summary>Renders the three sector values as one movable classification column group.</summary>
internal sealed class SectorTimingCell
{
    private static readonly SolidColorBrush OverallBestBrush = ClassificationRow.Frozen(Color.FromRgb(196, 87, 255));
    private static readonly SolidColorBrush PersonalBestBrush = ClassificationRow.Frozen(Color.FromRgb(54, 190, 107));
    private static readonly SolidColorBrush SlowerBrush = ClassificationRow.Frozen(Color.FromRgb(226, 182, 36));
    private readonly TextBlock[] _values = new TextBlock[3];

    public SectorTimingCell()
    {
        Element = new Grid { VerticalAlignment = VerticalAlignment.Center };
        for (var index = 0; index < _values.Length; index++)
        {
            Element.ColumnDefinitions.Add(new ColumnDefinition());
            var value = new TextBlock
            {
                Text = "—",
                FontSize = 10.5,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(3, 0, 3, 0)
            };
            value.SetResourceReference(Control.ForegroundProperty, "TextFillColorTertiaryBrush");
            Grid.SetColumn(value, index);
            Element.Children.Add(value);
            _values[index] = value;
        }
    }

    public Grid Element { get; }

    public void Update(SectorTimingSet? sectors)
    {
        for (var index = 0; index < _values.Length; index++) UpdateValue(_values[index], sectors?.At(index));
    }

    private static void UpdateValue(TextBlock target, SectorTiming? sector)
    {
        target.Text = sector?.Value ?? "—";
        target.ToolTip = sector?.Value;
        if (sector is null)
        {
            target.SetResourceReference(Control.ForegroundProperty, "TextFillColorTertiaryBrush");
            return;
        }

        target.Foreground = sector.State switch
        {
            SectorTimingState.OverallBest => OverallBestBrush,
            SectorTimingState.PersonalBest => PersonalBestBrush,
            _ => SlowerBrush
        };
    }
}

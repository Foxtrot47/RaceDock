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
    private static readonly SolidColorBrush UnreachedBrush = ClassificationRow.Frozen(Color.FromArgb(55, 142, 148, 160));
    private static readonly SolidColorBrush NeutralBrush = ClassificationRow.Frozen(Color.FromRgb(112, 119, 132));
    private readonly TextBlock[] _values = new TextBlock[3];
    private readonly Grid[] _miniSectorBars = new Grid[3];

    public SectorTimingCell()
    {
        Element = new Grid { VerticalAlignment = VerticalAlignment.Center };
        for (var index = 0; index < _values.Length; index++)
        {
            Element.ColumnDefinitions.Add(new ColumnDefinition());
            var cell = new Grid { Margin = new Thickness(3, 0, 3, 0) };
            cell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });
            var value = new TextBlock
            {
                Text = "—",
                FontSize = 10.5,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            value.SetResourceReference(Control.ForegroundProperty, "TextFillColorTertiaryBrush");
            cell.Children.Add(value);
            var miniSectors = new Grid
            {
                Height = 3,
                Margin = new Thickness(2, 1, 2, 0),
                VerticalAlignment = VerticalAlignment.Top,
                Visibility = Visibility.Collapsed
            };
            Grid.SetRow(miniSectors, 1);
            cell.Children.Add(miniSectors);
            Grid.SetColumn(cell, index);
            Element.Children.Add(cell);
            _values[index] = value;
            _miniSectorBars[index] = miniSectors;
        }
    }

    public Grid Element { get; }

    public void Update(SectorTimingSet? sectors, IReadOnlyList<IReadOnlyList<MiniSectorState>>? miniSectors)
    {
        for (var index = 0; index < _values.Length; index++)
        {
            UpdateValue(_values[index], sectors?.At(index));
            UpdateMiniSectors(_miniSectorBars[index], miniSectors is not null && index < miniSectors.Count
                ? miniSectors[index]
                : null);
        }
    }

    private static void UpdateMiniSectors(Grid bar, IReadOnlyList<MiniSectorState>? states)
    {
        if (states is null || states.Count == 0)
        {
            bar.Visibility = Visibility.Collapsed;
            return;
        }

        if (bar.Children.Count != states.Count)
        {
            bar.Children.Clear();
            bar.ColumnDefinitions.Clear();
            for (var index = 0; index < states.Count; index++)
            {
                bar.ColumnDefinitions.Add(new ColumnDefinition());
                var block = new Border { Margin = new Thickness(0.75, 0, 0.75, 0), CornerRadius = new CornerRadius(1) };
                Grid.SetColumn(block, index);
                bar.Children.Add(block);
            }
        }

        for (var index = 0; index < states.Count; index++)
        {
            var block = (Border)bar.Children[index];
            (block.Background, block.ToolTip) = states[index] switch
            {
                MiniSectorState.OverallBest => (OverallBestBrush, "Overall best mini-sector"),
                MiniSectorState.PersonalBest => (PersonalBestBrush, "Personal best mini-sector"),
                MiniSectorState.Slower => (SlowerBrush, "Slower mini-sector"),
                MiniSectorState.PitLane => (NeutralBrush, "Pit lane"),
                MiniSectorState.Other => (NeutralBrush, "Unreported mini-sector state"),
                _ => (UnreachedBrush, "Not reached")
            };
        }
        bar.Visibility = Visibility.Visible;
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

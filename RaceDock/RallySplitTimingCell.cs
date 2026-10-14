// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using RaceDock.Core;
using Control = System.Windows.Controls.Control;

namespace RaceDock.App;

/// <summary>Renders the variable set of cumulative split times published for a rally stage.</summary>
internal sealed class RallySplitTimingCell
{
    private readonly List<TextBlock> _values = [];
    private IReadOnlyList<RallySplitPoint> _points = [];

    public Grid Element { get; } = new() { VerticalAlignment = VerticalAlignment.Center };

    public void Configure(IReadOnlyList<RallySplitPoint> points)
    {
        if (_points.Select(point => point.SplitPointId).SequenceEqual(points.Select(point => point.SplitPointId))) return;
        _points = points;
        Element.Children.Clear();
        Element.ColumnDefinitions.Clear();
        _values.Clear();

        for (var index = 0; index < points.Count; index++)
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
            value.SetResourceReference(Control.ForegroundProperty, "TextFillColorPrimaryBrush");
            Grid.SetColumn(value, index);
            Element.Children.Add(value);
            _values.Add(value);
        }
    }

    public void Update(IReadOnlyList<RallySplitTiming>? splits)
    {
        var byPoint = (splits ?? []).ToDictionary(split => split.SplitPointId);
        for (var index = 0; index < _points.Count; index++)
        {
            var text = byPoint.TryGetValue(_points[index].SplitPointId, out var split) ? split.Value : "—";
            _values[index].Text = text;
            _values[index].ToolTip = text;
        }
    }
}

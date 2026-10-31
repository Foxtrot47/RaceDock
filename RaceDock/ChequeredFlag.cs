// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Media;

namespace RaceDock.App;

/// <summary>The chequered flag as a brush, so every surface that shows the finish draws the same pattern.</summary>
internal static class ChequeredFlag
{
    /// <summary>A brush whose squares are <paramref name="cell"/> device-independent pixels on a side.</summary>
    public static DrawingBrush Brush(double cell)
    {
        var group = new DrawingGroup
        {
            Children = new DrawingCollection
            {
                new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new Rect(0, 0, cell, cell))),
                new GeometryDrawing(Brushes.Black, null, new RectangleGeometry(new Rect(cell, 0, cell, cell))),
                new GeometryDrawing(Brushes.Black, null, new RectangleGeometry(new Rect(0, cell, cell, cell))),
                new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new Rect(cell, cell, cell, cell)))
            }
        };
        var brush = new DrawingBrush(group)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, cell * 2, cell * 2),
            ViewportUnits = BrushMappingMode.Absolute
        };
        brush.Freeze();
        return brush;
    }
}

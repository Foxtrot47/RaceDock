// SPDX-License-Identifier: GPL-3.0-or-later
namespace MotorsportTaskbar.Core;

public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left; public int Height => Bottom - Top;
}

public static class TaskbarGeometry
{
    public static PixelRect MapWindowRegion(PixelRect window, PixelRect localRegion) =>
        new(window.Left + localRegion.Left, window.Top + localRegion.Top,
            window.Left + localRegion.Right, window.Top + localRegion.Bottom);

    public static (int X, int Y, int Width, int Height)? CalculateLeftChild(
        PixelRect taskbar, uint dpi, IEnumerable<PixelRect>? occupied = null,
        double logicalWidth = 520, double logicalHeight = 40)
    {
        var scale = Math.Max(1, dpi) / 96d;
        var width = (int)Math.Round(logicalWidth * scale); var height = Math.Min(taskbar.Height, (int)Math.Round(logicalHeight * scale));
        var margin = Math.Max(6, (int)Math.Round(8 * scale));
        var gap = Math.Max(3, (int)Math.Round(4 * scale));
        var leftAreaEnd = taskbar.Width / 2;
        var candidate = margin;

        foreach (var rect in (occupied ?? []).Where(x => x.Width > 0 && x.Height > 0).OrderBy(x => x.Left))
        {
            var left = Math.Clamp(rect.Left - taskbar.Left, 0, taskbar.Width);
            var right = Math.Clamp(rect.Right - taskbar.Left, 0, taskbar.Width);
            if (right <= candidate || left >= leftAreaEnd) continue;
            if (left - candidate >= width) break;
            candidate = Math.Max(candidate, right + gap);
        }

        if (candidate + width > leftAreaEnd - margin) return null;
        return (candidate, Math.Max(0, (taskbar.Height - height) / 2), width, height);
    }
}

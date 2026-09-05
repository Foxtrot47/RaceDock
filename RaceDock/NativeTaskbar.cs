// Taskbar child-window technique adapted from FluentFlyout
// Copyright (c) 2024-2026 The FluentFlyout Authors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Interop;
using RaceDock.Core;

namespace RaceDock.App;

internal sealed class NativeTaskbar(System.Windows.Window window)
{
    private const int GwlStyle = -16, WsPopup = unchecked((int)0x80000000), WsChild = 0x40000000;
    private const uint SwpNoZOrder = 0x0004, SwpNoActivate = 0x0010, SwpShowWindow = 0x0040;
    private const int SwHide = 0;
    private const int WmMouseActivate = 0x0021, MaNoActivate = 3;
    private readonly int _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
    private IntPtr _hwnd;
    private TaskbarWidgetPosition _position;
    private DateTime _lastAutomationScanUtc = DateTime.MinValue;
    private IReadOnlyList<PixelRect> _occupiedRectangles = [];
    public event Action? ExplorerRestarted;
    public event Action<bool>? PositionChanged;

    /// <summary>How much taskbar the widget asks for. The countdown shown between sessions needs far less
    /// room than a classification strip, so it gives the space back rather than reserving it empty.</summary>
    public double LogicalWidth { get; private set; } = 520;
    public double MinimumLogicalWidth { get; private set; } = 420;

    public void ApplyWidth(double logicalWidth, double minimumLogicalWidth)
    {
        if (LogicalWidth == logicalWidth && MinimumLogicalWidth == minimumLogicalWidth) return;
        LogicalWidth = logicalWidth;
        MinimumLogicalWidth = minimumLogicalWidth;
        Reattach();
    }

    public void ApplyPosition(TaskbarWidgetPosition position)
    {
        if (_position == position) return;
        _position = position;
        Reattach();
    }

    public void Attach()
    {
        _hwnd = new WindowInteropHelper(window).EnsureHandle();
        HwndSource.FromHwnd(_hwnd)?.AddHook(WndProc);
        SetWindowLongPtr(_hwnd, -20, new IntPtr(GetWindowLongPtr(_hwnd, -20).ToInt64() | 0x08000000L)); // WS_EX_NOACTIVATE
        Reattach();
    }

    public void Reattach()
    {
        var taskbar = FindWindow("Shell_TrayWnd", null); if (taskbar == IntPtr.Zero || _hwnd == IntPtr.Zero) return;
        var style = GetWindowLongPtr(_hwnd, GwlStyle).ToInt64(); SetWindowLongPtr(_hwnd, GwlStyle, new IntPtr((style & ~WsPopup) | WsChild));
        var parentChanged = Reparent(taskbar);
        if (!GetWindowRect(taskbar, out var rect)) return;
        var taskbarRect = new PixelRect(rect.Left, rect.Top, rect.Right, rect.Bottom);
        var geometry = TaskbarGeometry.CalculateChild(taskbarRect, GetDpiForWindow(taskbar), _position,
            FindOccupiedRectangles(taskbar, taskbarRect), LogicalWidth, minimumLogicalWidth: MinimumLogicalWidth);
        if (geometry is null)
        {
            Hide();
            return;
        }
        var placement = geometry.Value;
        if (!parentChanged && HasPlacement(taskbarRect, placement)) return;
        ApplyPlacement(taskbar, taskbarRect, placement);
    }

    private bool Reparent(IntPtr taskbar)
    {
        if (GetParent(_hwnd) == taskbar) return false;
        SetParent(_hwnd, taskbar);
        _lastAutomationScanUtc = DateTime.MinValue;
        _occupiedRectangles = [];
        return true;
    }

    private void Hide()
    {
        if (!IsWindowVisible(_hwnd)) return;
        ShowWindow(_hwnd, SwHide);
        PositionChanged?.Invoke(false);
    }

    private bool HasPlacement(PixelRect taskbar, (int X, int Y, int Width, int Height) placement)
    {
        if (!IsWindowVisible(_hwnd) || !GetWindowRect(_hwnd, out var current)) return false;
        return current.Left == taskbar.Left + placement.X && current.Top == taskbar.Top + placement.Y &&
            current.Right - current.Left == placement.Width && current.Bottom - current.Top == placement.Height;
    }

    private void ApplyPlacement(IntPtr taskbar, PixelRect taskbarRect, (int X, int Y, int Width, int Height) placement)
    {
        var parentPoint = new Point { X = taskbarRect.Left + placement.X, Y = taskbarRect.Top + placement.Y };
        MapWindowPoints(IntPtr.Zero, taskbar, ref parentPoint, 1);
        SetWindowPos(_hwnd, IntPtr.Zero, parentPoint.X, parentPoint.Y, placement.Width, placement.Height,
            SwpNoZOrder | SwpNoActivate | SwpShowWindow);
        var region = CreateRectRgn(0, 0, placement.Width, placement.Height);
        SetWindowRgn(_hwnd, region, true);
        PositionChanged?.Invoke(true);
    }

    /// <summary>
    /// Places the flyout against the taskbar. <paramref name="slideOffset"/> displaces it towards
    /// the taskbar in device-independent pixels, which is how the opening slide is drawn.
    /// </summary>
    public void PositionFlyout(System.Windows.Window flyout, double slideOffset = 0)
    {
        if (_hwnd == IntPtr.Zero || !GetWindowRect(_hwnd, out var widget) || widget.Right <= widget.Left) return;
        var flyoutHwnd = new WindowInteropHelper(flyout).EnsureHandle();
        if (!GetWindowRect(flyoutHwnd, out var flyoutRect) || !GetWindowRect(FindWindow("Shell_TrayWnd", null), out var taskbar)) return;
        var width = flyoutRect.Right - flyoutRect.Left; var height = flyoutRect.Bottom - flyoutRect.Top;
        var x = widget.Left + ((widget.Right - widget.Left) - width) / 2;
        x = Math.Clamp(x, taskbar.Left + 8, Math.Max(taskbar.Left + 8, taskbar.Right - width - 8));
        // The window is positioned in device pixels, so the offset is scaled to match rather than
        // applied as a layout transform.
        var scale = flyout.ActualHeight > 0 ? height / flyout.ActualHeight : 1.0;
        var offset = (int)Math.Round(slideOffset * scale);
        var taskbarIsAtTop = taskbar.Top <= 0;
        var rest = taskbarIsAtTop ? taskbar.Bottom + 8 : taskbar.Top - height - 8;
        var y = taskbarIsAtTop ? rest - offset : rest + offset;
        SetWindowPos(flyoutHwnd, IntPtr.Zero, x, y, width, height, SwpNoZOrder | SwpNoActivate | SwpShowWindow);
    }

    private IReadOnlyList<PixelRect> FindOccupiedRectangles(IntPtr taskbar, PixelRect bounds)
    {
        List<PixelRect> result = [];
        EnumChildWindows(taskbar, (child, _) =>
        {
            if (child != _hwnd && IsWindowVisible(child) && GetWindowRect(child, out var window))
                AddOccupiedRectangle(result,
                    GetVisibleWindowRect(child, new(window.Left, window.Top, window.Right, window.Bottom)), bounds);
            return true;
        }, IntPtr.Zero);

        if (RefreshAutomationRectangles(taskbar, bounds) is { } refreshed)
        {
            foreach (var rectangle in refreshed) AddOccupiedRectangle(result, rectangle, bounds);
            _occupiedRectangles = result.ToArray();
        }
        else
        {
            foreach (var rectangle in _occupiedRectangles) AddOccupiedRectangle(result, rectangle, bounds);
        }
        return result;
    }

    private static void AddOccupiedRectangle(List<PixelRect> result, PixelRect? candidate, PixelRect bounds)
    {
        if (candidate is not { } value || Clip(value, bounds) is not { } rectangle) return;
        if (result.Any(item => Math.Abs(item.Left - rectangle.Left) <= 2 &&
            Math.Abs(item.Right - rectangle.Right) <= 2)) return;
        result.Add(rectangle);
    }

    private IReadOnlyList<PixelRect>? RefreshAutomationRectangles(IntPtr taskbar, PixelRect bounds)
    {
        if (DateTime.UtcNow - _lastAutomationScanUtc < TimeSpan.FromSeconds(30)) return null;
        _lastAutomationScanUtc = DateTime.UtcNow;
        try
        {
            List<PixelRect> result = [];
            var root = AutomationElement.FromHandle(taskbar);
            var controls = root.FindAll(TreeScope.Descendants, new OrCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Thumb),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink)));
            foreach (AutomationElement control in controls)
            {
                if (control.Current.IsOffscreen) continue;
                var rectangle = control.Current.BoundingRectangle;
                if (Clip(new((int)Math.Floor(rectangle.Left), (int)Math.Floor(rectangle.Top),
                    (int)Math.Ceiling(rectangle.Right), (int)Math.Ceiling(rectangle.Bottom)), bounds) is { } clipped)
                    result.Add(clipped);
            }
            // Start and Search temporarily remove every taskbar automation peer. Keep the last complete scan.
            return result.Count == 0 ? null : result;
        }
        catch (Exception failure) when (failure is ElementNotAvailableException or InvalidOperationException or COMException)
        {
            return null;
        }
    }

    private static PixelRect? Clip(PixelRect rectangle, PixelRect bounds)
    {
        var clipped = new PixelRect(Math.Max(bounds.Left, rectangle.Left), Math.Max(bounds.Top, rectangle.Top),
            Math.Min(bounds.Right, rectangle.Right), Math.Min(bounds.Bottom, rectangle.Bottom));
        return clipped.Width <= 0 || clipped.Height < bounds.Height / 3 || clipped.Width > bounds.Width / 2
            ? null
            : clipped;
    }

    private static PixelRect? GetVisibleWindowRect(IntPtr hwnd, PixelRect windowRect)
    {
        var region = CreateRectRgn(0, 0, 0, 0);
        if (region == IntPtr.Zero) return windowRect;
        try
        {
            var kind = GetWindowRgn(hwnd, region);
            if (kind == 1) return null; // NULLREGION: the child currently exposes no pixels.
            if (kind is 2 or 3 && GetRgnBox(region, out var box) is 2 or 3)
                return TaskbarGeometry.MapWindowRegion(windowRect, new(box.Left, box.Top, box.Right, box.Bottom));
            return windowRect; // No explicit region: the complete child rectangle is visible.
        }
        finally { DeleteObject(region); }
    }

    public PixelRect TaskbarScreenRect
    {
        get { var taskbar = FindWindow("Shell_TrayWnd", null); return GetWindowRect(taskbar, out var r) ? new(r.Left, r.Top, r.Right, r.Bottom) : default; }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == _taskbarCreated) { ExplorerRestarted?.Invoke(); Reattach(); }
        if (msg == WmMouseActivate) { handled = true; return new IntPtr(MaNoActivate); }
        return IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string className, string? title);
    [DllImport("user32.dll")] private static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int MapWindowPoints(IntPtr from, IntPtr to, ref Point point, uint count);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int RegisterWindowMessage(string value);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("user32.dll")] private static extern int GetWindowRgn(IntPtr hwnd, IntPtr region);
    [DllImport("gdi32.dll")] private static extern int GetRgnBox(IntPtr region, out Rect rect);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
    [DllImport("user32.dll")] private static extern int SetWindowRgn(IntPtr hwnd, IntPtr region, bool redraw);
}

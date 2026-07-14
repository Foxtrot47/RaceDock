// Taskbar child-window technique adapted from FluentFlyout
// Copyright (c) 2024-2026 The FluentFlyout Authors
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Interop;
using MotorsportTaskbar.Core;

namespace MotorsportTaskbar.App;

internal sealed class NativeTaskbar(System.Windows.Window window)
{
    private const int GwlStyle = -16, WsPopup = unchecked((int)0x80000000), WsChild = 0x40000000;
    private const uint SwpNoZOrder = 0x0004, SwpNoActivate = 0x0010, SwpShowWindow = 0x0040;
    private const int SwHide = 0;
    private const int WmMouseActivate = 0x0021, MaNoActivate = 3;
    private readonly int _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
    private IntPtr _hwnd;
    public event Action? ExplorerRestarted;
    public event Action<bool>? PositionChanged;

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
        SetParent(_hwnd, taskbar);
        if (!GetWindowRect(taskbar, out var rect)) return;
        var taskbarRect = new PixelRect(rect.Left, rect.Top, rect.Right, rect.Bottom);
        var geometry = TaskbarGeometry.CalculateLeftChild(taskbarRect, GetDpiForWindow(taskbar), FindOccupiedRectangles(taskbar, taskbarRect));
        if (geometry is null) { ShowWindow(_hwnd, SwHide); PositionChanged?.Invoke(false); return; }
        var placement = geometry.Value;
        SetWindowPos(_hwnd, IntPtr.Zero, placement.X, placement.Y, placement.Width, placement.Height, SwpNoZOrder | SwpNoActivate | SwpShowWindow);
        var region = CreateRectRgn(0, 0, placement.Width, placement.Height); SetWindowRgn(_hwnd, region, true);
        PositionChanged?.Invoke(true);
    }

    public void PositionFlyout(System.Windows.Window flyout)
    {
        if (_hwnd == IntPtr.Zero || !GetWindowRect(_hwnd, out var widget) || widget.Right <= widget.Left) return;
        var flyoutHwnd = new WindowInteropHelper(flyout).EnsureHandle();
        if (!GetWindowRect(flyoutHwnd, out var flyoutRect) || !GetWindowRect(FindWindow("Shell_TrayWnd", null), out var taskbar)) return;
        var width = flyoutRect.Right - flyoutRect.Left; var height = flyoutRect.Bottom - flyoutRect.Top;
        var x = widget.Left + ((widget.Right - widget.Left) - width) / 2;
        x = Math.Clamp(x, taskbar.Left + 8, Math.Max(taskbar.Left + 8, taskbar.Right - width - 8));
        var taskbarIsAtTop = taskbar.Top <= 0;
        var y = taskbarIsAtTop ? taskbar.Bottom + 8 : taskbar.Top - height - 8;
        SetWindowPos(flyoutHwnd, IntPtr.Zero, x, y, width, height, SwpNoZOrder | SwpNoActivate | SwpShowWindow);
    }

    private IReadOnlyList<PixelRect> FindOccupiedRectangles(IntPtr taskbar, PixelRect bounds)
    {
        List<PixelRect> result = [];
        EnumChildWindows(taskbar, (child, _) =>
        {
            if (child != _hwnd && IsWindowVisible(child) && GetWindowRect(child, out var r))
            {
                var windowRect = new PixelRect(r.Left, r.Top, r.Right, r.Bottom);
                var visibleRect = GetVisibleWindowRect(child, windowRect);
                if (visibleRect is { } visible) Add(visible.Left, visible.Top, visible.Right, visible.Bottom);
            }
            return true;
        }, IntPtr.Zero);

        try
        {
            var root = AutomationElement.FromHandle(taskbar);
            var controls = root.FindAll(TreeScope.Descendants, new OrCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Thumb),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink)));
            foreach (AutomationElement control in controls)
            {
                if (control.Current.IsOffscreen) continue;
                var r = control.Current.BoundingRectangle;
                Add((int)Math.Floor(r.Left), (int)Math.Floor(r.Top), (int)Math.Ceiling(r.Right), (int)Math.Ceiling(r.Bottom));
            }
        }
        catch (ElementNotAvailableException) { }
        catch (InvalidOperationException) { }

        return result;

        void Add(int left, int top, int right, int bottom)
        {
            var clipped = new PixelRect(Math.Max(bounds.Left, left), Math.Max(bounds.Top, top), Math.Min(bounds.Right, right), Math.Min(bounds.Bottom, bottom));
            if (clipped.Width <= 0 || clipped.Height < bounds.Height / 3 || clipped.Width > bounds.Width / 2) return;
            if (result.Any(x => Math.Abs(x.Left - clipped.Left) <= 2 && Math.Abs(x.Right - clipped.Right) <= 2)) return;
            result.Add(clipped);
        }
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
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string className, string? title);
    [DllImport("user32.dll")] private static extern IntPtr SetParent(IntPtr child, IntPtr parent);
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

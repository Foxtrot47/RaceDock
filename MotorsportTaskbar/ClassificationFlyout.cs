// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MotorsportTaskbar.Core;

namespace MotorsportTaskbar.App;

public sealed class ClassificationFlyout : Window
{
    private readonly StackPanel _rows = new();
    private readonly TextBlock _header = new();
    private readonly DispatcherTimer _closeTimer;
    public event EventHandler? PointerEntered;
    public ClassificationFlyout()
    {
        Width = 610; Height = 470; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; ShowActivated = false; Topmost = true; AllowsTransparency = true; Background = Brushes.Transparent;
        var root = new Border { Background = new SolidColorBrush(Color.FromArgb(250, 25, 25, 29)), CornerRadius = new(10), Padding = new(14), BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 76)), BorderThickness = new(1) };
        var stack = new StackPanel(); _header.Foreground = Brushes.White; _header.FontWeight = FontWeights.SemiBold; _header.Margin = new(0, 0, 0, 8); stack.Children.Add(_header);
        stack.Children.Add(new TextBlock { Text = "POS  DRIVER       GAP       INTERVAL    TYRE     LAP   STATUS", Foreground = Brushes.Gray, FontFamily = new FontFamily("Consolas"), FontSize = 12 });
        var scroll = new ScrollViewer { Content = _rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 400 }; stack.Children.Add(scroll); root.Child = stack; Content = root;
        _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) }; _closeTimer.Tick += (_, _) => { _closeTimer.Stop(); Hide(); };
        MouseEnter += (_, _) => { PointerEntered?.Invoke(this, EventArgs.Empty); CancelClose(); }; MouseLeave += (_, _) => ScheduleClose();
    }
    protected override void OnSourceInitialized(EventArgs e) { base.OnSourceInitialized(e); var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle; NativeNoActivate.Set(hwnd); }
    public void UpdateSnapshot(TimingSnapshot snapshot)
    {
        _header.Text = $"{snapshot.Meeting} — {snapshot.Session}   Lap {snapshot.CurrentLap}/{snapshot.TotalLaps?.ToString() ?? "—"}   {snapshot.ConnectionState}";
        _rows.Children.Clear(); foreach (var s in snapshot.Competitors)
        {
            var status = s.Retired ? "RET" : s.Stopped ? "STOP" : s.InPit ? "PIT" : ""; var fastest = s.IsOverallFastest ? " ◉" : "";
            _rows.Children.Add(new TextBlock { Text = $"{s.Position,2}   {s.Code,-3}{fastest,-3}   {(s.GapToLeader ?? "—"),-9} {(s.IntervalToPositionAhead ?? "—"),-10} {(s.Tyre ?? "—"),-8} {s.Lap,3}   {status}", Foreground = s.IsOverallFastest ? Brushes.Violet : s.Retired ? Brushes.Gray : Brushes.White, FontFamily = new FontFamily("Consolas"), FontSize = 13, Margin = new(0, 3, 0, 0) });
        }
    }
    public void ScheduleClose() { _closeTimer.Stop(); _closeTimer.Start(); }
    public void CancelClose() => _closeTimer.Stop();
    private static class NativeNoActivate
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
        public static void Set(IntPtr h) => SetWindowLongPtr(h, -20, new IntPtr(GetWindowLongPtr(h, -20).ToInt64() | 0x08000000L));
    }
}

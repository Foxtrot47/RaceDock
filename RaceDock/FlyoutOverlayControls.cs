// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Cursors = System.Windows.Input.Cursors;
using Orientation = System.Windows.Controls.Orientation;
using SymbolIcon = Wpf.Ui.Controls.SymbolIcon;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace RaceDock.App;

/// <summary>Title-row settings and pin buttons, with an optional drag handle for the pinned flyout.</summary>
internal sealed class FlyoutOverlayControls
{
    private readonly SymbolIcon _icon = new() { Symbol = SymbolRegular.Pin24, FontSize = 15 };
    private readonly ToggleButton _pin;
    private FrameworkElement? _dragHandle;
    public StackPanel Element { get; }
    public event Action<bool>? PinChanged;
    public event Action? DragRequested;
    public event Action? SettingsRequested;

    public FlyoutOverlayControls()
    {
        var settings = new Button
        {
            Content = new SymbolIcon { Symbol = SymbolRegular.Settings24, FontSize = 15 }, Focusable = false,
            Width = 24, Height = 24, Padding = new Thickness(3), ToolTip = "Settings"
        };
        settings.Click += (_, _) => SettingsRequested?.Invoke();
        System.Windows.Automation.AutomationProperties.SetName(settings, "Open settings");
        _pin = new ToggleButton
        {
            Content = _icon, Focusable = false, Width = 24, Height = 24,
            Padding = new Thickness(3), Margin = new Thickness(4, 0, 0, 0),
            ToolTip = "Pin as a movable overlay above other windows"
        };
        _pin.Checked += (_, _) => PinChanged?.Invoke(true);
        _pin.Unchecked += (_, _) => PinChanged?.Invoke(false);
        System.Windows.Automation.AutomationProperties.SetName(_pin, "Pin timing overlay");
        Element = new StackPanel
        {
            Orientation = Orientation.Horizontal, Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Top, Children = { settings, _pin }
        };
    }

    public void AttachDragHandle(FrameworkElement handle)
    {
        _dragHandle = handle;
        handle.MouseLeftButtonDown += (_, e) =>
        {
            if (_pin.IsChecked != true) return;
            e.Handled = true;
            DragRequested?.Invoke();
        };
    }

    public void SetPinned(bool pinned)
    {
        _pin.IsChecked = pinned;
        _icon.Symbol = pinned ? SymbolRegular.PinOff24 : SymbolRegular.Pin24;
        _pin.ToolTip = pinned ? "Unpin and return to the taskbar flyout" : "Pin as a movable overlay above other windows";
        System.Windows.Automation.AutomationProperties.SetName(_pin, pinned ? "Unpin timing overlay" : "Pin timing overlay");
        if (_dragHandle is null) return;
        _dragHandle.Cursor = pinned ? Cursors.SizeAll : Cursors.Arrow;
        _dragHandle.ToolTip = pinned ? "Drag to move overlay" : null;
    }
}

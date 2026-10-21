// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls.Primitives;
using Cursors = System.Windows.Input.Cursors;
using SymbolIcon = Wpf.Ui.Controls.SymbolIcon;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace RaceDock.App;

/// <summary>A title-row pin button with an optional drag handle for the pinned flyout.</summary>
internal sealed class FlyoutOverlayControls
{
    private readonly SymbolIcon _icon = new() { Symbol = SymbolRegular.Pin24, FontSize = 15 };
    private FrameworkElement? _dragHandle;
    public ToggleButton Element { get; }
    public event Action<bool>? PinChanged;
    public event Action? DragRequested;

    public FlyoutOverlayControls()
    {
        Element = new ToggleButton
        {
            Content = _icon, Focusable = false, Width = 24, Height = 24,
            Padding = new Thickness(3), Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Top,
            ToolTip = "Pin as a movable overlay above other windows"
        };
        Element.Checked += (_, _) => PinChanged?.Invoke(true);
        Element.Unchecked += (_, _) => PinChanged?.Invoke(false);
        System.Windows.Automation.AutomationProperties.SetName(Element, "Pin timing overlay");
    }

    public void AttachDragHandle(FrameworkElement handle)
    {
        _dragHandle = handle;
        handle.MouseLeftButtonDown += (_, e) =>
        {
            if (Element.IsChecked != true) return;
            e.Handled = true;
            DragRequested?.Invoke();
        };
    }

    public void SetPinned(bool pinned)
    {
        Element.IsChecked = pinned;
        _icon.Symbol = pinned ? SymbolRegular.PinOff24 : SymbolRegular.Pin24;
        Element.ToolTip = pinned ? "Unpin and return to the taskbar flyout" : "Pin as a movable overlay above other windows";
        System.Windows.Automation.AutomationProperties.SetName(Element, pinned ? "Unpin timing overlay" : "Pin timing overlay");
        if (_dragHandle is null) return;
        _dragHandle.Cursor = pinned ? Cursors.SizeAll : Cursors.Arrow;
        _dragHandle.ToolTip = pinned ? "Drag to move overlay" : null;
    }
}

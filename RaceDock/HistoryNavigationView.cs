// SPDX-License-Identifier: GPL-3.0-or-later
using NavigationView = Wpf.Ui.Controls.NavigationView;

namespace RaceDock.App;

/// <summary>
/// A navigation view whose back button works for pages that are placed directly rather than navigated to.
/// </summary>
/// <remarks>
/// The settings window keeps one live instance of each page and shows it with ReplaceContent, which by
/// design leaves the built-in journal untouched. The journal therefore stays empty, the back button stays
/// disabled, and GoBack has nothing to return to. This view records the pages the window shows instead, and
/// hands the previous one back when the button, or the mouse back button, asks to go back.
/// </remarks>
internal sealed class HistoryNavigationView : NavigationView
{
    private readonly Stack<object> _history = new();

    /// <summary>Raised with the token of the page to return to.</summary>
    public event Action<object>? BackNavigated;

    /// <summary>Records the page being left, so it can be returned to.</summary>
    public void Remember(object page)
    {
        _history.Push(page);
        IsBackEnabled = true;
    }

    public override bool GoBack()
    {
        if (_history.Count == 0) return false;
        var page = _history.Pop();
        IsBackEnabled = _history.Count > 0;
        BackNavigated?.Invoke(page);
        return true;
    }
}

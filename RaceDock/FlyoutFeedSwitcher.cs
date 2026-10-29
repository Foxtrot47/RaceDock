// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using RaceDock.Core;
using Brush = System.Windows.Media.Brush;
using Control = System.Windows.Controls.Control;
using SymbolIcon = Wpf.Ui.Controls.SymbolIcon;
using SymbolRegular = Wpf.Ui.Controls.SymbolRegular;

namespace RaceDock.App;

/// <summary>
/// The flyout's live-feed tabs: one tab per championship the source can show, plus Auto while the flyout
/// follows rotation. Purely presentational; the owner decides what a selection means.
/// </summary>
internal sealed class FlyoutFeedSwitcher
{
    private const string AutoLabel = "Auto";

    /// <summary>Tall enough to read a square series mark without crowding the tab's label.</summary>
    private const double MarkSize = 16;

    /// <summary>An ended mark is desaturated and then dimmed, so it cannot outshout its own disabled label.</summary>
    private const double MutedMarkOpacity = 0.6;

    private readonly WrapPanel _chips = new();
    private readonly List<Championship> _feeds = [];
    private readonly HashSet<Championship> _ended = [];
    private readonly Dictionary<Championship, ToggleButton> _tabs = [];
    private ToggleButton? _auto;
    private Championship? _selected;
    private bool _allowAuto = true;

    public FrameworkElement Element => _chips;

    /// <summary>Null means Auto, the source's rotation.</summary>
    public event Action<Championship?>? SelectionChanged;

    /// <summary>A pinned overlay is always on a chosen feed, so it offers no way back to rotation.</summary>
    public bool AllowAuto
    {
        get => _allowAuto;
        set
        {
            if (_allowAuto == value) return;
            _allowAuto = value;
            Rebuild();
        }
    }

    /// <summary>
    /// Shows <paramref name="feeds"/> in rotation order. A championship in <paramref name="ended"/> is kept in
    /// place as a disabled tab, so a finished session is explained without a sentence under the strip.
    /// </summary>
    public void SetFeeds(IReadOnlyList<Championship> feeds, IReadOnlySet<Championship> ended)
    {
        if (feeds.SequenceEqual(_feeds) && ended.SetEquals(_ended)) return;
        _feeds.Clear();
        _feeds.AddRange(feeds);
        _ended.Clear();
        _ended.UnionWith(ended);
        Rebuild();
    }

    public void SetSelected(Championship? championship)
    {
        // Always repaint: a ToggleButton unchecks itself on click before the owner confirms the selection.
        _selected = championship;
        ApplySelection();
    }

    private void Rebuild()
    {
        _chips.Children.Clear();
        _tabs.Clear();
        _auto = null;
        if (_allowAuto)
        {
            _auto = CreateAutoTab();
            _auto.Click += (_, _) => SelectionChanged?.Invoke(null);
            _chips.Children.Add(_auto);
            // Auto is a mode rather than a series, so a hairline keeps it out of the feed list.
            _chips.Children.Add(CreateDivider());
        }
        foreach (var championship in _feeds)
        {
            var feed = championship;
            var tab = CreateFeedTab(feed, _ended.Contains(feed));
            tab.Click += (_, _) => SelectionChanged?.Invoke(feed);
            _tabs[feed] = tab;
            _chips.Children.Add(tab);
        }
        ApplySelection();
    }

    private static ToggleButton CreateAutoTab()
    {
        var content = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        content.Children.Add(new SymbolIcon
        {
            Symbol = SymbolRegular.ArrowSync24,
            FontSize = 10,
            Margin = new Thickness(0, 0, 5, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        content.Children.Add(new TextBlock { Text = AutoLabel, VerticalAlignment = VerticalAlignment.Center });
        return CreateTab(content, "Follow the taskbar rotation");
    }

    private static ToggleButton CreateFeedTab(Championship feed, bool ended)
    {
        var label = new TextBlock
        {
            Text = ChampionshipDisplay.ShortName(feed),
            VerticalAlignment = VerticalAlignment.Center
        };
        if (ended) label.SetResourceReference(Control.ForegroundProperty, "TextFillColorDisabledBrush");

        var mark = ChampionshipLogo.CreateMark(feed, MarkSize, ended);
        mark.Margin = new Thickness(0, 0, 5, 0);
        mark.VerticalAlignment = VerticalAlignment.Center;
        if (ended) mark.Opacity = MutedMarkOpacity;

        var content = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };
        content.Children.Add(mark);
        content.Children.Add(label);

        var tab = CreateTab(content, $"Show {ChampionshipDisplay.FullName(feed)} timing", ChampionshipAccent.For(feed));
        if (ended)
        {
            // A feed that has ended keeps its place but cannot be selected, and still explains itself in a tooltip.
            tab.IsEnabled = false;
            tab.SetResourceReference(Control.BorderBrushProperty, "TextFillColorDisabledBrush");
            ToolTipService.SetShowOnDisabled(tab, true);
        }
        return tab;
    }

    private static ToggleButton CreateTab(FrameworkElement content, string automationName, Brush? accent = null)
    {
        var tab = new ToggleButton
        {
            Content = content,
            Style = System.Windows.Application.Current?.TryFindResource("FlyoutFeedChipStyle") as Style,
            ToolTip = automationName,
            Margin = new Thickness(0, 0, 2, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        // A series uses its own accent for the selection underline; Auto falls back to the system accent.
        if (accent is not null) tab.BorderBrush = accent;
        else tab.SetResourceReference(Control.BorderBrushProperty, "AccentFillColorDefaultBrush");
        System.Windows.Automation.AutomationProperties.SetName(tab, automationName);
        return tab;
    }

    private static FrameworkElement CreateDivider()
    {
        var divider = new Border
        {
            Width = 1,
            Height = 14,
            Margin = new Thickness(3, 0, 7, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.4
        };
        divider.SetResourceReference(Border.BackgroundProperty, "TextFillColorTertiaryBrush");
        return divider;
    }

    private void ApplySelection()
    {
        if (_auto is not null) _auto.IsChecked = _selected is null && _allowAuto;
        foreach (var (championship, tab) in _tabs) tab.IsChecked = _selected == championship;
    }
}

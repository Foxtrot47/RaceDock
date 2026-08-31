// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using RaceDock.Core;
using Control = System.Windows.Controls.Control;

namespace RaceDock.App;

/// <summary>The compact schedule state shown on the taskbar while no championship is live.</summary>
internal sealed class IdleTaskbarView
{
    private readonly Border _surface;
    private readonly Border _logo = new()
    {
        Width = 42,
        Height = 22,
        Margin = new Thickness(0, 0, 10, 0),
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly ScrollingTextBlock _event = new()
    {
        FontSize = 11,
        FontWeight = FontWeights.SemiBold,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly TextBlock _detail = new()
    {
        FontSize = 9.5,
        TextTrimming = TextTrimming.CharacterEllipsis
    };
    private Championship _logoChampionship = Championship.Unknown;

    public IdleTaskbarView()
    {
        _surface = new Border
        {
            Padding = new Thickness(7, 2, 6, 2),
            SnapsToDevicePixels = true
        };

        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _surface.Child = layout;

        Grid.SetColumn(_logo, 0);
        layout.Children.Add(_logo);

        var copy = new Grid { VerticalAlignment = VerticalAlignment.Center };
        copy.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        copy.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _event.SetResourceReference(Control.ForegroundProperty, "TextFillColorPrimaryBrush");
        _detail.SetResourceReference(Control.ForegroundProperty, "TextFillColorSecondaryBrush");
        Grid.SetRow(_detail, 1);
        copy.Children.Add(_event);
        copy.Children.Add(_detail);
        Grid.SetColumn(copy, 1);
        layout.Children.Add(copy);
    }

    public FrameworkElement Element => _surface;

    public void Update(UpcomingSession session, DateTimeOffset now, TimeZoneInfo timeZone)
    {
        if (_logoChampionship != session.Championship)
        {
            _logoChampionship = session.Championship;
            _logo.Child = ChampionshipLogo.Create(session.Championship, _logo.Width, _logo.Height);
        }
        _event.Text = ChampionshipDisplay.ShortEventName(session.Championship, session.EventName).ToUpperInvariant();
        _detail.Text = NextSessionDisplay.Detail(session, now, timeZone);
        var tooltip = NextSessionDisplay.Tooltip(session, timeZone);
        _surface.ToolTip = tooltip;
        System.Windows.Automation.AutomationProperties.SetName(_surface, tooltip);
    }
}

// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RaceDock.Core;
using Control = System.Windows.Controls.Control;

namespace RaceDock.App;

/// <summary>The compact schedule state shown on the taskbar while no championship is live.</summary>
internal sealed class IdleTaskbarView
{
    private static readonly FontFamily TaskbarFont = new("Segoe UI Variable, Microsoft YaHei UI, Yu Gothic UI");
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
        FontSize = 13,
        FontWeight = FontWeights.Normal,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly Grid _copy = new();
    private readonly TextBlock _detail = new()
    {
        FontSize = 13,
        FontWeight = FontWeights.Normal,
        Opacity = 0.5,
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis
    };
    private Championship _logoChampionship = Championship.Unknown;

    public double LogicalWidth { get; private set; } = 210;

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

        _copy.Width = 145;
        _copy.HorizontalAlignment = HorizontalAlignment.Left;
        _copy.VerticalAlignment = VerticalAlignment.Center;
        TextOptions.SetTextRenderingMode(_copy, TextRenderingMode.ClearType);
        TextOptions.SetTextFormattingMode(_copy, TextFormattingMode.Ideal);
        _event.FontFamily = TaskbarFont;
        _detail.FontFamily = TaskbarFont;
        _copy.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _copy.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _copy.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _copy.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _event.SetResourceReference(Control.ForegroundProperty, "TextFillColorPrimaryBrush");
        _detail.SetResourceReference(Control.ForegroundProperty, "TextFillColorPrimaryBrush");
        _copy.Children.Add(_event);
        _copy.Children.Add(_detail);
        Grid.SetColumn(_copy, 1);
        layout.Children.Add(_copy);
    }

    public FrameworkElement Element => _surface;

    public void Update(UpcomingSession session, DateTimeOffset now, TimeZoneInfo timeZone, bool singleRow)
    {
        if (_logoChampionship != session.Championship)
        {
            _logoChampionship = session.Championship;
            _logo.Child = ChampionshipLogo.Create(session.Championship, _logo.Width, _logo.Height);
        }
        var eventName = ChampionshipDisplay.ShortEventName(session.Championship, session.EventName);
        var detail = NextSessionDisplay.Detail(session, now, timeZone);
        _event.Text = eventName;
        _detail.Text = detail.Replace(" · ", " ", StringComparison.Ordinal);
        if (singleRow) ApplySingleRowLayout();
        else ApplyTwoRowLayout();
        ApplyWidth(singleRow);
        var tooltip = NextSessionDisplay.Tooltip(session, timeZone);
        _surface.ToolTip = tooltip;
        System.Windows.Automation.AutomationProperties.SetName(_surface, tooltip);
    }

    private void ApplySingleRowLayout()
    {
        _logo.Margin = new Thickness(0, 0, 8, 0);
        _detail.Margin = new Thickness(8, 0, 0, 0);
        _copy.HorizontalAlignment = HorizontalAlignment.Left;
        _copy.RowDefinitions[0].Height = GridLength.Auto;
        _copy.RowDefinitions[1].Height = new GridLength(0);
        Grid.SetRow(_detail, 0);
        Grid.SetColumn(_detail, 1);
        Grid.SetColumnSpan(_detail, 1);
        Grid.SetColumnSpan(_event, 1);
    }

    private void ApplyTwoRowLayout()
    {
        _logo.Margin = new Thickness(0, 0, 10, 0);
        _detail.Margin = new Thickness(0, -1.35, 0, 0);
        _copy.HorizontalAlignment = HorizontalAlignment.Left;
        _copy.RowDefinitions[0].Height = new GridLength(17);
        _copy.RowDefinitions[1].Height = new GridLength(17);
        Grid.SetRow(_detail, 1);
        Grid.SetColumn(_detail, 0);
        Grid.SetColumnSpan(_detail, 2);
        Grid.SetColumnSpan(_event, 2);
    }

    private void ApplyWidth(bool singleRow)
    {
        if (!singleRow)
        {
            _copy.Width = 145;
            LogicalWidth = 210;
            return;
        }

        _event.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        _detail.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        const double chromeWidth = 63;
        var contentWidth = _event.DesiredSize.Width + _detail.DesiredSize.Width;
        LogicalWidth = Math.Clamp(chromeWidth + contentWidth, 210, 520);
        _copy.Width = LogicalWidth - chromeWidth;
    }
}

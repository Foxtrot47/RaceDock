// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using RaceDock.Core;
using Brush = System.Windows.Media.Brush;
using Control = System.Windows.Controls.Control;

namespace RaceDock.App;

/// <summary>
/// One reusable taskbar strip cell. Like <see cref="ClassificationRow"/> the element structure is fixed at
/// construction, so the 250 ms redraw assigns values instead of rebuilding and re-measuring the strip.
/// </summary>
internal sealed class TaskbarCell
{
    private static readonly (Color Start, Color End)[] CardColors =
    [
        (Color.FromRgb(145, 62, 7), Color.FromRgb(211, 119, 26)),
        (Color.FromRgb(128, 16, 54), Color.FromRgb(202, 42, 98)),
        (Color.FromRgb(14, 73, 107), Color.FromRgb(32, 131, 185)),
        (Color.FromRgb(74, 20, 115), Color.FromRgb(130, 48, 198)),
        (Color.FromRgb(16, 84, 55), Color.FromRgb(36, 148, 101))
    ];

    private static readonly Brush[] ColoredCardBrushes = [.. CardColors.Select(colors => FrozenGradient(colors.Start, colors.End))];

    private readonly Border _container;
    private readonly Grid _layout;
    private readonly ColumnDefinition _markerColumn;
    private readonly Border _markerHost;
    private readonly Border _positionCard;
    private readonly TextBlock _positionText;
    private readonly Grid _details;
    private readonly ColumnDefinition _inlinePositionColumn;
    private readonly ColumnDefinition _driverColumn;
    private readonly ColumnDefinition _intervalColumn;
    private readonly RowDefinition _detailFirstRow;
    private readonly RowDefinition _detailSecondRow;
    private readonly TextBlock _inlinePosition;
    private readonly TextBlock _driver;
    private readonly TextBlock _interval;

    private string _logoKey = "";
    private bool? _singleRow;

    public TaskbarCell()
    {
        _layout = new Grid();
        _markerColumn = new ColumnDefinition { Width = new GridLength(30) };
        _layout.ColumnDefinitions.Add(_markerColumn);
        _layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _positionText = new TextBlock
        {
            FontSize = 12,
            FontWeight = FontWeights.Normal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.White
        };
        _positionCard = new Border
        {
            Width = 26,
            Height = 28,
            Margin = new Thickness(1),
            Padding = new Thickness(1),
            CornerRadius = new CornerRadius(7),
            Child = _positionText,
            SnapsToDevicePixels = true,
            Effect = new DropShadowEffect
            {
                BlurRadius = 8,
                ShadowDepth = 1,
                Direction = 270,
                Opacity = .28,
                Color = Colors.Black
            }
        };
        _markerHost = new Border { Margin = new Thickness(1), Child = _positionCard };
        _layout.Children.Add(_markerHost);

        _inlinePosition = new TextBlock { FontSize = 12, FontWeight = FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
        _inlinePosition.SetResourceReference(Control.ForegroundProperty, "TextFillColorPrimaryBrush");
        _driver = new TextBlock
        {
            FontSize = 12,
            FontWeight = FontWeights.Normal,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        _driver.SetResourceReference(Control.ForegroundProperty, "TextFillColorPrimaryBrush");
        _interval = new TextBlock { FontSize = 12, FontWeight = FontWeights.Normal, MinHeight = 12, Opacity = 0.5, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        _interval.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        _details = new Grid { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 0, 0) };
        _inlinePositionColumn = new ColumnDefinition { Width = GridLength.Auto };
        _driverColumn = new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) };
        _intervalColumn = new ColumnDefinition { Width = GridLength.Auto };
        _details.ColumnDefinitions.Add(_inlinePositionColumn);
        _details.ColumnDefinitions.Add(_driverColumn);
        _details.ColumnDefinitions.Add(_intervalColumn);
        _detailFirstRow = new RowDefinition { Height = new GridLength(17) };
        _details.RowDefinitions.Add(_detailFirstRow);
        _detailSecondRow = new RowDefinition { Height = new GridLength(17) };
        _details.RowDefinitions.Add(_detailSecondRow);
        Grid.SetColumnSpan(_driver, 3);
        Grid.SetRow(_interval, 1);
        Grid.SetColumnSpan(_interval, 3);
        _details.Children.Add(_inlinePosition);
        _details.Children.Add(_driver);
        _details.Children.Add(_interval);
        Grid.SetColumn(_details, 1);
        _layout.Children.Add(_details);

        _container = new Border
        {
            Child = _layout,
            Background = Brushes.Transparent,
            Margin = new Thickness(2),
            SnapsToDevicePixels = true
        };
    }

    public UIElement Element => _container;

    public void Update(CompetitorStanding standing, Championship championship, ChampionshipDisplaySettings display,
        string driverText, bool singleRow)
    {
        ApplyLayout(singleRow);
        _details.Visibility = Visibility.Visible;
        _inlinePosition.Text = standing.Position.ToString();
        _driver.Text = driverText;
        _driver.ToolTip = standing.Name;
        _interval.Text = standing.Position == 1 ? "LEAD" : standing.IntervalToPositionAhead ?? "—";

        UpdateMarker(standing, championship, display, singleRow);
        if (!ReferenceEquals(_markerHost.Child, _positionCard)) return;
        _positionText.Text = standing.Position.ToString();
        _positionCard.Background = CardBrush(standing.Position);
        _positionCard.Opacity = 1;
    }

    /// <summary>Renders the placeholder used for positions the feed has not filled yet.</summary>
    public void UpdateEmpty(int position, bool singleRow)
    {
        ApplyLayout(singleRow);
        _details.Visibility = singleRow ? Visibility.Visible : Visibility.Collapsed;
        _inlinePosition.Text = position.ToString();
        _driver.Text = "—";
        _driver.ToolTip = null;
        _interval.Text = "";
        _logoKey = "";
        _markerHost.Child = _positionCard;
        _positionText.Text = "—";
        _positionCard.Background = CardBrush(position);
        _positionCard.Opacity = .45;
    }

    private void UpdateMarker(
        CompetitorStanding standing, Championship championship, ChampionshipDisplaySettings display, bool singleRow)
    {
        var logoKey = !singleRow && display.TaskbarUseManufacturerLogos
            ? $"{championship}|{standing.Manufacturer}|{standing.Team}|{ManufacturerLogo.IsDarkTheme}"
            : "";
        if (logoKey == _logoKey) return;
        _logoKey = logoKey;
        _markerHost.Child = logoKey.Length > 0 &&
            ManufacturerLogo.TryCreateForStanding(standing, championship, 26, out var logo)
                ? logo
                : _positionCard;
    }

    private void ApplyLayout(bool singleRow)
    {
        if (_singleRow == singleRow) return;
        _singleRow = singleRow;
        if (singleRow) ApplySingleRowLayout();
        else ApplyTwoRowLayout();
    }

    private void ApplySingleRowLayout()
    {
        _markerColumn.Width = new GridLength(0);
        _markerHost.Visibility = Visibility.Collapsed;
        _container.Margin = new Thickness(1);
        _details.Margin = new Thickness(0);
        _details.HorizontalAlignment = HorizontalAlignment.Center;
        _inlinePositionColumn.Width = GridLength.Auto;
        _driverColumn.Width = GridLength.Auto;
        _intervalColumn.Width = GridLength.Auto;
        _detailFirstRow.Height = GridLength.Auto;
        _detailSecondRow.Height = new GridLength(0);
        _inlinePosition.Visibility = Visibility.Visible;
        Grid.SetColumn(_driver, 1);
        Grid.SetColumnSpan(_driver, 1);
        Grid.SetRow(_interval, 0);
        Grid.SetColumn(_interval, 2);
        Grid.SetColumnSpan(_interval, 1);
        _driver.Margin = new Thickness(4, 0, 0, 0);
        _interval.Margin = new Thickness(5, 0, 0, 0);
    }

    private void ApplyTwoRowLayout()
    {
        _markerColumn.Width = new GridLength(30);
        _markerHost.Visibility = Visibility.Visible;
        // One pixel a side: the strip hands the difference to the event text, which needs it more than the gaps do.
        _container.Margin = new Thickness(1);
        _details.Margin = new Thickness(5, 0, 0, 0);
        _details.HorizontalAlignment = HorizontalAlignment.Stretch;
        _inlinePositionColumn.Width = GridLength.Auto;
        _driverColumn.Width = new GridLength(1, GridUnitType.Star);
        _intervalColumn.Width = GridLength.Auto;
        _detailFirstRow.Height = new GridLength(17);
        _detailSecondRow.Height = new GridLength(17);
        _inlinePosition.Visibility = Visibility.Collapsed;
        Grid.SetColumn(_driver, 0);
        Grid.SetColumnSpan(_driver, 3);
        Grid.SetRow(_interval, 1);
        Grid.SetColumn(_interval, 0);
        Grid.SetColumnSpan(_interval, 3);
        _driver.Margin = new Thickness(0);
        _interval.Margin = new Thickness(0);
    }

    private static Brush CardBrush(int position) =>
        ColoredCardBrushes[Math.Clamp(position, 1, ColoredCardBrushes.Length) - 1];

    private static LinearGradientBrush FrozenGradient(Color start, Color end)
    {
        var brush = new LinearGradientBrush(start, end, 35);
        brush.Freeze();
        return brush;
    }
}

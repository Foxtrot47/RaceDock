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
    private readonly Border _markerHost;
    private readonly Border _positionCard;
    private readonly TextBlock _positionText;
    private readonly StackPanel _details;
    private readonly TextBlock _driver;
    private readonly TextBlock _gap;

    private string _logoKey = "";

    public TaskbarCell()
    {
        _layout = new Grid();
        _layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        _layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _positionText = new TextBlock
        {
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
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

        _driver = new TextBlock
        {
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        _driver.SetResourceReference(Control.ForegroundProperty, "TextFillColorPrimaryBrush");
        _gap = new TextBlock { FontSize = 11, MinHeight = 11, TextTrimming = TextTrimming.CharacterEllipsis };
        _gap.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        _details = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 0, 0) };
        _details.Children.Add(_driver);
        _details.Children.Add(_gap);
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
        string driverText)
    {
        _details.Visibility = Visibility.Visible;
        _driver.Text = driverText;
        _driver.ToolTip = standing.Name;
        _gap.Text = standing.Position == 1 ? "LEAD" : standing.GapToLeader ?? "—";

        var logoKey = display.TaskbarUseManufacturerLogos ? $"{championship}|{standing.Manufacturer}|{standing.Team}" : "";
        if (logoKey != _logoKey)
        {
            _logoKey = logoKey;
            _markerHost.Child = logoKey.Length > 0 &&
                ManufacturerLogo.TryCreateForStanding(standing, championship, 26, out var logo)
                    ? logo
                    : _positionCard;
        }
        if (!ReferenceEquals(_markerHost.Child, _positionCard)) return;
        _positionText.Text = standing.Position.ToString();
        _positionCard.Background = CardBrush(standing.Position);
        _positionCard.Opacity = 1;
    }

    /// <summary>Renders the placeholder used for positions the feed has not filled yet.</summary>
    public void UpdateEmpty(int position)
    {
        _details.Visibility = Visibility.Collapsed;
        _driver.ToolTip = null;
        _logoKey = "";
        _markerHost.Child = _positionCard;
        _positionText.Text = "—";
        _positionCard.Background = CardBrush(position);
        _positionCard.Opacity = .45;
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

// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace RaceDock.App;

/// <summary>Renders championship artwork in one fixed navigation viewport without ImageIcon template padding.</summary>
/// <remarks>
/// The element drives the navigation row height: WPF-UI sizes the item's hover border to its content plus a
/// 1px border, so 46 here yields 48px rows with no dead band between adjacent hover surfaces.
/// </remarks>
public sealed class ChampionshipNavigationIcon : IconElement
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(ImageSource), typeof(ChampionshipNavigationIcon),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public ImageSource? Source
    {
        get => (ImageSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public ChampionshipNavigationIcon()
    {
        Width = 30;
        Height = 46;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        SnapsToDevicePixels = true;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
    }

    protected override UIElement InitializeChildren()
    {
        var image = new System.Windows.Controls.Image
        {
            Width = 30,
            Height = 24,
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SnapsToDevicePixels = true
        };
        image.SetBinding(System.Windows.Controls.Image.SourceProperty,
            new System.Windows.Data.Binding(nameof(Source)) { Source = this });
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        return image;
    }
}

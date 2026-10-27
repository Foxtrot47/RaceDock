// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RaceDock.Core;
using Wpf.Ui.Appearance;
using Control = System.Windows.Controls.Control;
using Image = System.Windows.Controls.Image;

namespace RaceDock.App;

/// <summary>Championship artwork used as a compact visual anchor in the schedule.</summary>
internal static class ChampionshipLogo
{
    private static readonly Dictionary<Championship, string> Assets = new()
    {
        [Championship.Formula1] = "f1",
        [Championship.Formula2] = "f2",
        [Championship.Formula3] = "f3",
        [Championship.FormulaE] = "formulae",
        [Championship.MotoGp] = "motogp",
        [Championship.Moto2] = "moto2",
        [Championship.Moto3] = "moto3",
        [Championship.WorldRallyChampionship] = "wrc",
        [Championship.GtWorldChallengeEurope] = "gtwc",
        [Championship.WorldEnduranceChampionship] = "wec",
        [Championship.EuropeanLeMansSeries] = "elms",
        [Championship.MichelinLeMansCup] = "mlmc",
        [Championship.Imsa] = "imsa",
        [Championship.Dtm] = "dtm",
        [Championship.IndyCar] = "indycar",
        [Championship.NascarCup] = "nascar",
        [Championship.NascarXfinity] = "nascar",
        [Championship.NascarTrucks] = "nascar",
    };

    // These championships currently have no wide asset; all others use wide artwork by default.
    private static readonly HashSet<Championship> CompactArtwork =
    [
        Championship.FormulaE,
        Championship.Moto2,
        Championship.Moto3
    ];

    private static readonly HashSet<Championship> MonochromeArtwork =
    [
        Championship.MotoGp,
        Championship.Moto2,
        Championship.Moto3,
        Championship.EuropeanLeMansSeries,
        Championship.MichelinLeMansCup
    ];

    private static readonly HashSet<Championship> FlatBackgroundArtwork =
    [
        Championship.FormulaE,
        Championship.Dtm
    ];

    // Taskbar artwork defaults to the wide mark; only championships with an explicit override use compact artwork.
    private static readonly HashSet<Championship> CompactTaskbarArtwork =
    [
        Championship.NascarCup,
        Championship.NascarXfinity,
        Championship.NascarTrucks
    ];

    private static readonly Dictionary<(Championship Championship, bool Light, bool Wide), ImageSource?> Cache = [];

    public static FrameworkElement Create(Championship championship, double width, double height) =>
        Create(championship, width, height, useWideArtwork: true);

    public static FrameworkElement CreateTaskbar(Championship championship, double width, double height) =>
        Create(championship, width, height, useWideArtwork: !CompactTaskbarArtwork.Contains(championship));

    private static FrameworkElement Create(Championship championship, double width, double height, bool useWideArtwork)
    {
        var light = ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Dark &&
            (MonochromeArtwork.Contains(championship) ||
             useWideArtwork && championship is (Championship.NascarCup or Championship.NascarXfinity or Championship.NascarTrucks));
        if (Load(championship, light, useWideArtwork) is { } artwork)
        {
            var image = new Image { Source = artwork, Width = width, Height = height, Stretch = Stretch.Uniform };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            return image;
        }
        var fallback = new TextBlock
        {
            Text = ChampionshipDisplay.ShortName(championship),
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Width = width,
            Height = height,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        fallback.SetResourceReference(Control.ForegroundProperty, "TextFillColorSecondaryBrush");
        return fallback;
    }

    private static ImageSource? Load(Championship championship, bool light, bool useWideArtwork)
    {
        var key = (championship, light, useWideArtwork);
        if (Cache.TryGetValue(key, out var cached)) return cached;
        ImageSource? artwork = null;
        if (Assets.TryGetValue(championship, out var asset))
        {
            try
            {
                var suffix = useWideArtwork && !CompactArtwork.Contains(championship) ? "-wide" : "";
                var file = light && asset == "nascar" && suffix == "-wide"
                    ? "nascar-wide-dark"
                    : $"{asset}{suffix}";
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri($"pack://application:,,,/Assets/ChampionshipLogos/{file}.png");
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                BitmapSource source = FlatBackgroundArtwork.Contains(championship)
                    ? RemoveFlatBackground(bitmap)
                    : bitmap;
                artwork = light && !(asset == "nascar" && suffix == "-wide") ? LightSilhouette(source) : source;
            }
            catch (Exception failure) when (failure is IOException or NotSupportedException or UriFormatException)
            {
                AppLog.Warning("Schedule", $"Championship logo '{asset}' could not be loaded.", failure);
            }
        }
        Cache[key] = artwork;
        return artwork;
    }

    /// <summary>Removes the solid rectangle baked into a few provider-supplied logo files.</summary>
    private static BitmapSource RemoveFlatBackground(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        var blue = pixels[0];
        var green = pixels[1];
        var red = pixels[2];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            if (Math.Abs(pixels[index] - blue) > 10
                || Math.Abs(pixels[index + 1] - green) > 10
                || Math.Abs(pixels[index + 2] - red) > 10) continue;
            pixels[index + 3] = 0;
        }
        var result = BitmapSource.Create(converted.PixelWidth, converted.PixelHeight, converted.DpiX,
            converted.DpiY, PixelFormats.Bgra32, null, pixels, stride);
        result.Freeze();
        return result;
    }

    /// <summary>Turns a logo white while preserving its antialiased alpha edge.</summary>
    private static BitmapSource LightSilhouette(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        for (var index = 0; index < pixels.Length; index += 4)
        {
            if (pixels[index + 3] == 0) continue;
            pixels[index] = 0xFF;
            pixels[index + 1] = 0xFF;
            pixels[index + 2] = 0xFF;
        }
        var result = BitmapSource.Create(converted.PixelWidth, converted.PixelHeight, converted.DpiX,
            converted.DpiY, PixelFormats.Bgra32, null, pixels, stride);
        result.Freeze();
        return result;
    }
}

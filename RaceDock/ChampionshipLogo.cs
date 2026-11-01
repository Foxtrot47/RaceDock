// SPDX-License-Identifier: GPL-3.0-or-later
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
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
        [Championship.F1Academy] = "f1academy",
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

    // A couple of series ship a wordmark under the compact name; the sidebar file is the square mark.
    private static readonly Dictionary<Championship, string> SquareArtwork = new()
    {
        [Championship.Moto2] = "moto2-sidebar",
        [Championship.Moto3] = "moto3-sidebar",
    };

    // Keyed by what actually ends up on screen rather than the championship, because a mark may use a
    // different file for the same series.
    private readonly record struct ArtworkKey(string File, bool Light, bool Stripped, bool Grayscale);

    private static readonly Dictionary<ArtworkKey, ImageSource?> Cache = [];

    public static FrameworkElement Create(Championship championship, double width, double height) =>
        Create(championship, width, height, useWideArtwork: true);

    /// <summary>
    /// The square series mark, for places only tall enough for an icon such as the flyout's feed tabs. It keeps
    /// an opaque tile intact: at this size the artwork is the whole icon, not a backdrop to remove. A
    /// <paramref name="muted"/> mark is desaturated so an ended series reads as inactive, not merely dim.
    /// </summary>
    public static FrameworkElement CreateMark(Championship championship, double size, bool muted = false)
    {
        var light = ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Dark &&
            MonochromeArtwork.Contains(championship);
        var artwork = SquareFile(championship) is { } file
            ? LoadArtwork(championship, file, light, stripBackground: false, grayscale: muted)
            : null;
        if (artwork is null)
            return new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = muted ? Brushes.Gray : ChampionshipAccent.For(championship),
                VerticalAlignment = VerticalAlignment.Center
            };
        var image = new Image { Source = artwork, Width = size, Height = size, Stretch = Stretch.Uniform };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        return image;
    }

    public static FrameworkElement CreateTaskbar(Championship championship, double width, double height) =>
        Create(championship, width, height, useWideArtwork: !CompactTaskbarArtwork.Contains(championship));

    private static FrameworkElement Create(Championship championship, double width, double height, bool useWideArtwork)
    {
        var light = ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Dark &&
            (MonochromeArtwork.Contains(championship) ||
             useWideArtwork && championship is (Championship.NascarCup or Championship.NascarXfinity or Championship.NascarTrucks));
        if (ArtworkFile(championship, light, useWideArtwork) is { } file
            && LoadArtwork(championship, file, light, stripBackground: true) is { } artwork)
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

    /// <summary>The file for the wide or compact logo a schedule row or taskbar slot shows.</summary>
    private static string? ArtworkFile(Championship championship, bool light, bool useWideArtwork)
    {
        if (!Assets.TryGetValue(championship, out var asset)) return null;
        var wide = useWideArtwork && !CompactArtwork.Contains(championship);
        // The dark taskbar needs the light-on-dark NASCAR mark instead of an inverted wide logo.
        if (light && wide && asset == "nascar") return "nascar-wide-dark";
        return wide ? $"{asset}-wide" : asset;
    }

    /// <summary>The square file for <see cref="CreateMark"/>, which is always compact artwork.</summary>
    private static string? SquareFile(Championship championship)
    {
        if (SquareArtwork.TryGetValue(championship, out var mark)) return mark;
        return Assets.TryGetValue(championship, out var asset) ? asset : null;
    }

    private static ImageSource? LoadArtwork(Championship championship, string file, bool light,
        bool stripBackground, bool grayscale = false)
    {
        var strip = stripBackground && FlatBackgroundArtwork.Contains(championship);
        var key = new ArtworkKey(file, light, strip, grayscale);
        if (Cache.TryGetValue(key, out var cached)) return cached;
        ImageSource? artwork = null;
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri($"pack://application:,,,/Assets/ChampionshipLogos/{file}.png");
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            BitmapSource source = strip ? RemoveFlatBackground(bitmap) : bitmap;
            // The light NASCAR mark is already drawn for a dark surface, so it keeps its colours.
            if (light && file != "nascar-wide-dark") source = LightSilhouette(source);
            if (grayscale) source = Desaturate(source);
            artwork = source;
        }
        catch (Exception failure) when (failure is IOException or NotSupportedException or UriFormatException)
        {
            AppLog.Warning("Schedule", $"Championship logo '{file}' could not be loaded.", failure);
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

    /// <summary>Removes colour while keeping the mark's shape, for a series that has stopped.</summary>
    private static BitmapSource Desaturate(BitmapSource source)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        for (var index = 0; index < pixels.Length; index += 4)
        {
            var grey = (byte)Math.Round(
                0.2126 * pixels[index + 2] + 0.7152 * pixels[index + 1] + 0.0722 * pixels[index]);
            pixels[index] = grey;
            pixels[index + 1] = grey;
            pixels[index + 2] = grey;
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

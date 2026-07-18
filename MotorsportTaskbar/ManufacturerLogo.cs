// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Image = System.Windows.Controls.Image;

namespace MotorsportTaskbar.App;

internal static class ManufacturerLogo
{
    private const string ResourceRoot = "Assets/ManufacturerLogos/";
    private static readonly Lazy<IReadOnlyDictionary<string, LogoDefinition>> Catalog = new(LoadCatalog);

    public static bool TryCreate(string? manufacturer, double size, out Border logo)
    {
        logo = null!;
        var definition = Resolve(manufacturer);
        if (definition is null) return false;

        try
        {
            var artwork = definition.Asset.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
                ? LoadVector(definition)
                : LoadBitmap(definition);
            if (artwork is null) return false;

            logo = new Border
            {
                Width = size,
                Height = size,
                Background = Brushes.Transparent,
                ToolTip = manufacturer,
                Child = artwork,
                SnapsToDevicePixels = true
            };
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or JsonException or System.Xml.XmlException or NotSupportedException)
        {
            return false;
        }
    }

    private static LogoDefinition? Resolve(string? manufacturer)
    {
        var key = Normalize(manufacturer);
        if (key.Length == 0) return null;
        if (Catalog.Value.TryGetValue(key, out var exact)) return exact;
        return Catalog.Value
            .Where(pair => pair.Key.Length >= 4 && key.Contains(pair.Key, StringComparison.Ordinal))
            .OrderByDescending(pair => pair.Key.Length)
            .Select(pair => pair.Value)
            .FirstOrDefault();
    }

    private static IReadOnlyDictionary<string, LogoDefinition> LoadCatalog()
    {
        using var stream = OpenResource("manifest.json");
        if (stream is null) return new Dictionary<string, LogoDefinition>();
        var definitions = JsonSerializer.Deserialize<List<LogoDefinition>>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? [];
        return definitions
            .SelectMany(definition => definition.Aliases.Select(alias => (Alias: Normalize(alias), Definition: definition)))
            .Where(pair => pair.Alias.Length > 0)
            .GroupBy(pair => pair.Alias, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Definition, StringComparer.Ordinal);
    }

    private static FrameworkElement? LoadVector(LogoDefinition definition)
    {
        using var stream = OpenResource(definition.Asset);
        if (stream is null) return null;
        var document = XDocument.Load(stream);
        var geometries = document.Descendants()
            .Where(element => element.Name.LocalName == "path")
            .Select(element => element.Attribute("d")?.Value)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Geometry.Parse(path!))
            .ToList();
        if (geometries.Count == 0) return null;

        var group = new GeometryGroup { FillRule = FillRule.Nonzero };
        foreach (var geometry in geometries) group.Children.Add(geometry);
        group.Freeze();
        var brush = BrandBrush(definition);
        var image = new DrawingImage(new GeometryDrawing(brush, null, group));
        image.Freeze();
        return Artwork(image);
    }

    private static FrameworkElement? LoadBitmap(LogoDefinition definition)
    {
        using var stream = OpenResource(definition.Asset);
        if (stream is null) return null;
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        var mask = new ImageBrush(bitmap)
        {
            Stretch = Stretch.Uniform,
            AlignmentX = AlignmentX.Center,
            AlignmentY = AlignmentY.Center
        };
        mask.Freeze();
        return new Border
        {
            Background = BrandBrush(definition),
            OpacityMask = mask,
            Margin = new Thickness(2),
            UseLayoutRounding = true,
            SnapsToDevicePixels = true
        };
    }

    private static SolidColorBrush BrandBrush(LogoDefinition definition)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(definition.Color)!;
        brush.Freeze();
        return brush;
    }

    private static Image Artwork(ImageSource source) => new()
    {
        Source = source,
        Stretch = Stretch.Uniform,
        Margin = new Thickness(2),
        UseLayoutRounding = true,
        SnapsToDevicePixels = true
    };

    private static Stream? OpenResource(string asset) => System.Windows.Application.GetResourceStream(
        new Uri($"pack://application:,,,/MotorsportTaskbar;component/{ResourceRoot}{asset}", UriKind.Absolute))?.Stream;

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(character)) result.Append(char.ToLowerInvariant(character));
        }
        return result.ToString();
    }

    private sealed class LogoDefinition
    {
        public string Asset { get; init; } = "";
        public string Color { get; init; } = "#FFFFFF";
        public string[] Aliases { get; init; } = [];
    }
}

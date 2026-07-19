// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    private static readonly Regex SvgTransformPattern = new(@"([A-Za-z]+)\s*\(([^)]*)\)", RegexOptions.Compiled);
    private static readonly Regex SvgNumberPattern = new(@"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?", RegexOptions.Compiled);
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
        if (definition.PreserveColors) return LoadColorVector(document);

        var geometries = SvgDrawableElements(document)
            .Select(SvgGeometry)
            .Where(geometry => geometry is not null)
            .ToList();
        if (geometries.Count == 0) return null;

        var group = new GeometryGroup { FillRule = FillRule.Nonzero };
        foreach (var geometry in geometries) group.Children.Add(geometry!);
        group.Freeze();
        var brush = BrandBrush(definition);
        var image = new DrawingImage(new GeometryDrawing(brush, null, group));
        image.Freeze();
        return Artwork(image);
    }

    private static FrameworkElement? LoadColorVector(XDocument document)
    {
        var drawing = new DrawingGroup();
        foreach (var element in SvgDrawableElements(document))
        {
            var geometry = SvgGeometry(element);
            if (geometry is null) continue;
            var brush = SvgFill(element);
            if (brush is null) continue;
            drawing.Children.Add(new GeometryDrawing(brush, null, geometry));
        }
        if (drawing.Children.Count == 0) return null;

        drawing.Freeze();
        var image = new DrawingImage(drawing);
        image.Freeze();
        return Artwork(image);
    }

    private static IEnumerable<XElement> SvgDrawableElements(XDocument document) => document.Descendants()
        .Where(element => element.Name.LocalName is "path" or "polygon" or "polyline")
        .Where(element => !element.Ancestors().Any(ancestor =>
            ancestor.Name.LocalName is "defs" or "clipPath" or "mask" or "symbol"));

    private static Geometry? SvgGeometry(XElement element)
    {
        Geometry? geometry = element.Name.LocalName switch
        {
            "path" when !string.IsNullOrWhiteSpace(element.Attribute("d")?.Value) =>
                Geometry.Parse(element.Attribute("d")!.Value),
            "polygon" or "polyline" => SvgPointsGeometry(element.Attribute("points")?.Value),
            _ => null
        };
        if (geometry is null) return null;

        var transform = SvgTransform(element);
        if (transform.IsIdentity) return geometry;

        geometry = geometry.Clone();
        geometry.Transform = new MatrixTransform(transform);
        return geometry;
    }

    private static Geometry? SvgPointsGeometry(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var values = SvgNumberPattern.Matches(value)
            .Select(number => double.Parse(number.Value, NumberStyles.Float, CultureInfo.InvariantCulture))
            .ToArray();
        if (values.Length < 4 || values.Length % 2 != 0) return null;

        var geometry = new StreamGeometry { FillRule = FillRule.Nonzero };
        using (var context = geometry.Open())
        {
            context.BeginFigure(new System.Windows.Point(values[0], values[1]), true, true);
            for (var index = 2; index < values.Length; index += 2)
                context.LineTo(new System.Windows.Point(values[index], values[index + 1]), true, false);
        }
        return geometry;
    }

    private static Matrix SvgTransform(XElement element)
    {
        var result = Matrix.Identity;
        foreach (var current in element.AncestorsAndSelf())
        {
            var value = current.Attribute("transform")?.Value;
            if (string.IsNullOrWhiteSpace(value)) continue;
            result = Matrix.Multiply(result, ParseSvgTransform(value));
        }
        return result;
    }

    private static Matrix ParseSvgTransform(string value)
    {
        var result = Matrix.Identity;
        foreach (Match match in SvgTransformPattern.Matches(value))
        {
            var values = SvgNumberPattern.Matches(match.Groups[2].Value)
                .Select(number => double.Parse(number.Value, NumberStyles.Float, CultureInfo.InvariantCulture))
                .ToArray();
            var operation = SvgTransformOperation(match.Groups[1].Value, values);
            result = Matrix.Multiply(operation, result);
        }
        return result;
    }

    private static Matrix SvgTransformOperation(string name, double[] values)
    {
        if (name.Equals("matrix", StringComparison.OrdinalIgnoreCase) && values.Length >= 6)
            return new Matrix(values[0], values[1], values[2], values[3], values[4], values[5]);
        if (name.Equals("translate", StringComparison.OrdinalIgnoreCase) && values.Length >= 1)
            return new Matrix(1, 0, 0, 1, values[0], values.Length >= 2 ? values[1] : 0);
        if (name.Equals("scale", StringComparison.OrdinalIgnoreCase) && values.Length >= 1)
            return new Matrix(values[0], 0, 0, values.Length >= 2 ? values[1] : values[0], 0, 0);
        if (name.Equals("rotate", StringComparison.OrdinalIgnoreCase) && values.Length >= 1)
        {
            var radians = values[0] * Math.PI / 180;
            var cosine = Math.Cos(radians);
            var sine = Math.Sin(radians);
            var centerX = values.Length >= 3 ? values[1] : 0;
            var centerY = values.Length >= 3 ? values[2] : 0;
            return new Matrix(cosine, sine, -sine, cosine,
                centerX - cosine * centerX + sine * centerY,
                centerY - sine * centerX - cosine * centerY);
        }
        if (name.Equals("skewX", StringComparison.OrdinalIgnoreCase) && values.Length >= 1)
            return new Matrix(1, 0, Math.Tan(values[0] * Math.PI / 180), 1, 0, 0);
        if (name.Equals("skewY", StringComparison.OrdinalIgnoreCase) && values.Length >= 1)
            return new Matrix(1, Math.Tan(values[0] * Math.PI / 180), 0, 1, 0, 0);
        return Matrix.Identity;
    }

    private static System.Windows.Media.Brush? SvgFill(XElement element)
    {
        var value = SvgProperty(element, "fill") ?? "#000000";
        if (value.Equals("none", StringComparison.OrdinalIgnoreCase)) return null;
        var brush = (System.Windows.Media.Brush?)new BrushConverter().ConvertFromString(value);
        if (brush is null) return null;

        var opacity = SvgOpacity(element);
        if (opacity < 1)
        {
            brush = brush.Clone();
            brush.Opacity = opacity;
        }
        brush.Freeze();
        return brush;
    }

    private static string? SvgProperty(XElement element, string name)
    {
        foreach (var current in element.AncestorsAndSelf())
        {
            var attribute = current.Attributes().FirstOrDefault(attribute =>
                attribute.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(attribute?.Value)) return attribute.Value.Trim();

            var style = current.Attribute("style")?.Value;
            if (string.IsNullOrWhiteSpace(style)) continue;
            foreach (var declaration in style.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = declaration.IndexOf(':');
                if (separator < 0 || !declaration[..separator].Trim().Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
                return declaration[(separator + 1)..].Trim();
            }
        }
        return null;
    }

    private static double SvgOpacity(XElement element)
    {
        var opacity = 1d;
        foreach (var current in element.AncestorsAndSelf())
        {
            if (TrySvgNumber(SvgPropertyOnElement(current, "opacity"), out var layerOpacity)) opacity *= layerOpacity;
        }
        if (TrySvgNumber(SvgProperty(element, "fill-opacity"), out var fillOpacity)) opacity *= fillOpacity;
        return Math.Clamp(opacity, 0, 1);
    }

    private static string? SvgPropertyOnElement(XElement element, string name)
    {
        var attribute = element.Attributes().FirstOrDefault(attribute =>
            attribute.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(attribute?.Value)) return attribute.Value.Trim();

        var style = element.Attribute("style")?.Value;
        if (string.IsNullOrWhiteSpace(style)) return null;
        foreach (var declaration in style.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = declaration.IndexOf(':');
            if (separator >= 0 && declaration[..separator].Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
                return declaration[(separator + 1)..].Trim();
        }
        return null;
    }

    private static bool TrySvgNumber(string? value, out double number) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number);

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
        if (definition.PreserveColors) return Artwork(bitmap);

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
        public bool PreserveColors { get; init; }
        public string[] Aliases { get; init; } = [];
    }
}

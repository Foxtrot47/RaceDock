// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace RaceDock.Core.Presentation;

/// <summary>
/// Resolves a manufacturer or team name to a logo asset file name. The alias catalog is the
/// manifest embedded from Assets/ManufacturerLogos/manifest.json, the same data the Windows app
/// ships, so both front ends pick the same artwork. Core never loads the artwork: each front end
/// maps the returned file name to its own asset folder.
/// </summary>
public static class ManufacturerLogos
{
    public sealed record LogoDefinition(string Asset, string Color, bool PreserveColors, string[] Aliases);

    private static readonly Lazy<Dictionary<string, LogoDefinition>> Catalog = new(LoadCatalog);
    private static readonly ConcurrentDictionary<string, LogoDefinition?> ResolvedNames = new(StringComparer.Ordinal);

    /// <summary>The logo asset file name for a manufacturer, or null when nothing matches.</summary>
    public static string? ResolveAsset(string? manufacturer) => Resolve(manufacturer)?.Asset;

    public static LogoDefinition? Resolve(string? manufacturer)
    {
        var name = manufacturer ?? "";
        return ResolvedNames.GetOrAdd(name, ResolveUncached);
    }

    private static LogoDefinition? ResolveUncached(string manufacturer)
    {
        var key = Normalize(manufacturer);
        if (key.Length == 0)
            return null;
        if (Catalog.Value.TryGetValue(key, out var exact))
            return exact;
        return Catalog.Value
            .Where(pair => pair.Key.Length >= 4 && key.Contains(pair.Key, StringComparison.Ordinal))
            .OrderByDescending(pair => pair.Key.Length)
            .Select(pair => pair.Value)
            .FirstOrDefault();
    }

    private static Dictionary<string, LogoDefinition> LoadCatalog()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith("ManufacturerLogos.manifest.json", StringComparison.Ordinal));
            if (resourceName is null)
                return [];
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
                return [];
            var definitions = JsonSerializer.Deserialize<List<LogoDefinition>>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            return definitions
                .SelectMany(definition => definition.Aliases.Select(alias => (Alias: Normalize(alias), Definition: definition)))
                .Where(pair => pair.Alias.Length > 0)
                .GroupBy(pair => pair.Alias, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().Definition, StringComparer.Ordinal);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or JsonException)
        {
            return [];
        }
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            // FormD decomposition leaves combining marks behind as non-letter characters, so keeping
            // letters and digits alone also strips diacritics.
            if (char.IsLetterOrDigit(character))
                result.Append(char.ToLowerInvariant(character));
        }
        return result.ToString();
    }
}

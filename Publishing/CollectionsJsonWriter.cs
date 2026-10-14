using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CodedByKay.ArtShow.CLI.Data;
using CodedByKay.ArtShow.CLI.Options;

namespace CodedByKay.ArtShow.CLI.Publishing;

/// <summary>
/// Writes data/collections.json: presentation details for the site's collections (the Group
/// classifiers). Only groups at least one artwork uses are listed. The site works without this
/// file or any field in it (slugified name, newest piece as cover, no blurb, size order).
/// </summary>
public static partial class CollectionsJsonWriter
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static async Task WriteAsync(IEnumerable<Artwork> artworks, IEnumerable<Classifier> classifiers, PublishOptions publish, CancellationToken ct = default)
    {
        var usedNames = artworks.SelectMany(a => a.Groups).ToHashSet(StringComparer.Ordinal);
        var knownIds = artworks.Select(a => a.Id).ToHashSet();

        var items = classifiers
            .Where(c => c.Kind == ClassifierKind.Group && usedNames.Contains(c.Name))
            .OrderBy(c => c.SortOrder ?? int.MaxValue)
            .ThenBy(c => c.Name, StringComparer.Ordinal)
            .Select(c => new CollectionsJsonItem
            {
                Slug = Slugify(c.Name),
                Name = c.Name,
                Blurb = string.IsNullOrWhiteSpace(c.Blurb) ? null : c.Blurb,
                CoverId = c.CoverArtworkId is int id && knownIds.Contains(id) ? id.ToString() : null,
                Order = c.SortOrder,
            })
            .ToList();

        var json = JsonSerializer.Serialize(items, WriteOptions);
        Directory.CreateDirectory(Path.GetDirectoryName(publish.OutputCollectionsJsonPath)!);
        await File.WriteAllTextAsync(publish.OutputCollectionsJsonPath, json, ct);
    }

    /// <summary>Same as the site's slugify (js/modules/data.js) and the MCP's.</summary>
    public static string Slugify(string text)
    {
        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormKD);
        var sb = new StringBuilder();
        foreach (var ch in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        return NonSlug().Replace(sb.ToString(), "-").Trim('-');
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlug();
}

public class CollectionsJsonItem
{
    [System.Text.Json.Serialization.JsonPropertyName("slug")]
    public string Slug { get; set; } = "";

    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [System.Text.Json.Serialization.JsonPropertyName("blurb")]
    public string? Blurb { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("coverId")]
    public string? CoverId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("order")]
    public int? Order { get; set; }
}

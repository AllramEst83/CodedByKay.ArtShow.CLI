using System.Text.Json.Serialization;

namespace CodedByKay.ArtShow.CLI.Publishing;

/// <summary>Matches the legacy/published artwork.json shape 1:1 — see AGENTS.md "Known local paths".</summary>
public class ArtworkJsonItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("addedDate")]
    public string AddedDate { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("createdDate")]
    public string? CreatedDate { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonPropertyName("groups")]
    public List<string> Groups { get; set; } = [];

    [JsonPropertyName("medium")]
    public string? Medium { get; set; }

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("videoUrl")]
    public string? VideoUrl { get; set; }

    [JsonPropertyName("thumbnailUrl")]
    public string? ThumbnailUrl { get; set; }

    // Added 2026-10 for the gallery overhaul. Appended AFTER the legacy fields so existing
    // readers and the byte-for-byte diff against older publishes stay readable.
    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }

    [JsonPropertyName("displayUrl")]
    public string? DisplayUrl { get; set; }

    [JsonPropertyName("datePrecision")]
    public string? DatePrecision { get; set; }

    [JsonPropertyName("media")]
    public List<string> Media { get; set; } = [];
}

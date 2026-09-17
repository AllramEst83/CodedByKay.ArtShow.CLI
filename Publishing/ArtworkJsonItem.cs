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
}

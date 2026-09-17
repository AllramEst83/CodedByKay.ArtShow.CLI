using System.Text.Json;
using CodedByKay.ArtShow.CLI.Data;
using CodedByKay.ArtShow.CLI.Options;

namespace CodedByKay.ArtShow.CLI.Publishing;

public static class ArtworkJsonWriter
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static async Task WriteAsync(IEnumerable<Artwork> artworks, R2Options r2, PublishOptions publish, CancellationToken ct = default)
    {
        var items = artworks
            .OrderBy(a => a.AddedDate)
            .Select(a => ToJsonItem(a, r2))
            .ToList();

        var json = JsonSerializer.Serialize(items, WriteOptions);
        Directory.CreateDirectory(Path.GetDirectoryName(publish.OutputArtworkJsonPath)!);
        await File.WriteAllTextAsync(publish.OutputArtworkJsonPath, json, ct);
    }

    private static ArtworkJsonItem ToJsonItem(Artwork a, R2Options r2) => new()
    {
        Id = a.Id.ToString(),
        Type = a.Type,
        AddedDate = a.AddedDate.ToString("yyyy-MM-dd"),
        Title = a.Title,
        Description = a.Description,
        CreatedDate = a.CreatedDate?.ToString("yyyy-MM-dd"),
        Category = a.Category,
        Tags = a.Tags,
        Groups = a.Groups,
        Medium = a.Medium,
        ImageUrl = a.IsVideo || a.R2Key is null ? null : $"{r2.PublicBaseUrl}/{a.R2Key}",
        // Bare embed URL — the frontend appends playback query params (autoplay/mute/rel) at open time.
        VideoUrl = a.IsVideo && a.VideoId is not null ? $"https://www.youtube-nocookie.com/embed/{a.VideoId}" : null,
        // YouTube's own auto-generated thumbnail — no upload needed for video rows.
        ThumbnailUrl = a.IsVideo
            ? (a.VideoId is null ? null : $"https://img.youtube.com/vi/{a.VideoId}/hqdefault.jpg")
            : (a.ThumbR2Key is null ? null : $"{r2.PublicBaseUrl}/{a.ThumbR2Key}"),
    };
}

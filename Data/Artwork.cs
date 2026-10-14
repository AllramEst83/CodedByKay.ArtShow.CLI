namespace CodedByKay.ArtShow.CLI.Data;

public class Artwork
{
    // SQLite-generated (rowid autoincrement) — not derived from the legacy "art-NNN" ids.
    public int Id { get; set; }
    public DateOnly AddedDate { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateOnly? CreatedDate { get; set; }
    public string? Category { get; set; }
    public string? Medium { get; set; }
    public string? Type { get; set; } // null = image, "video" = video
    public string OriginalPath { get; set; } = "";
    public string? R2Key { get; set; }
    public string? ThumbR2Key { get; set; }
    public string? VideoId { get; set; } // YouTube video id, e.g. "dQw4w9WgXcQ"

    // Pixel size of the (orientation-corrected) original. Null for videos and for rows added
    // before 2026-10 until the MCP's backfill_image_metadata has run. The site uses the ratio to
    // reserve space for each card, so there is no layout shift while thumbnails load.
    public int? Width { get; set; }
    public int? Height { get; set; }

    // ~2000px long-edge WebP the viewer shows by default. Null when the original is already
    // that small (the site then falls back to the original) and for videos.
    public string? DisplayR2Key { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<string> Groups { get; set; } = [];

    public bool IsVideo => Type == "video";
}

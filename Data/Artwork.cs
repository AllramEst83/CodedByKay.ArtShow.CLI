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
    public List<string> Tags { get; set; } = [];
    public List<string> Groups { get; set; } = [];

    public bool IsVideo => Type == "video";
}

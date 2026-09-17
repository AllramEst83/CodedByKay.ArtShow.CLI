using CodedByKay.ArtShow.CLI.Data;
using CodedByKay.ArtShow.CLI.Options;
using Spectre.Console;

namespace CodedByKay.ArtShow.CLI.Menus;

/// <summary>Read-only detail view for a single artwork — every field in one bordered table, so
/// a quick look doesn't require reaching for SQL. Separate from Edit (which walks every field
/// for changes) and from Status (which is about R2/catalog sync, not browsing one record) —
/// this is pure local-DB browsing, no R2 round trip.</summary>
public static class ViewFlow
{
    public static void Run(ArtworkDbContext db, R2Options r2Options)
    {
        var artworks = db.Artworks.OrderBy(a => a.Id).ToList();
        if (artworks.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Catalog is empty — nothing to view.[/]");
            return;
        }

        do
        {
            var target = ArtworkPicker.Pick(artworks, "Which artwork?");
            if (target is null)
                return;

            ShowDetails(target, r2Options);
        }
        while (AnsiConsole.Confirm("View another?", false));
    }

    private static void ShowDetails(Artwork a, R2Options r2Options)
    {
        var table = new Table().Border(TableBorder.Rounded).Title($"#{a.Id} — {a.Title.EscapeMarkup()}");
        table.AddColumn("Field");
        table.AddColumn("Value");

        void Row(string field, string? value) =>
            table.AddRow(field, string.IsNullOrWhiteSpace(value) ? "[grey](none)[/]" : value.EscapeMarkup());

        Row("Id", a.Id.ToString());
        Row("Type", a.Type ?? "image");
        Row("Title", a.Title);
        Row("Description", a.Description);
        Row("AddedDate", a.AddedDate.ToString("yyyy-MM-dd"));
        Row("CreatedDate", a.CreatedDate?.ToString("yyyy-MM-dd"));
        Row("Category", a.Category);
        Row("Medium", a.Medium);
        Row("Tags", a.Tags.Count == 0 ? null : string.Join(", ", a.Tags));
        Row("Groups", a.Groups.Count == 0 ? null : string.Join(", ", a.Groups));
        Row("OriginalPath", a.OriginalPath);
        Row("R2Key", a.R2Key);
        Row("ThumbR2Key", a.ThumbR2Key);
        Row("VideoId", a.VideoId);

        // Derived URLs (same formulas as Publishing/ArtworkJsonWriter.cs) — rows in the same
        // table as everything else, not printed separately outside its border.
        if (a.IsVideo)
        {
            Row("videoUrl", a.VideoId is null ? null : $"https://www.youtube-nocookie.com/embed/{a.VideoId}");
            Row("thumbnailUrl", a.VideoId is null ? null : $"https://img.youtube.com/vi/{a.VideoId}/hqdefault.jpg");
        }
        else
        {
            Row("imageUrl", a.R2Key is null ? null : $"{r2Options.PublicBaseUrl}/{a.R2Key}");
            Row("thumbnailUrl", a.ThumbR2Key is null ? null : $"{r2Options.PublicBaseUrl}/{a.ThumbR2Key}");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }
}

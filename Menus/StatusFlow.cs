using CodedByKay.ArtShow.CLI.Data;
using CodedByKay.ArtShow.CLI.Storage;
using Spectre.Console;

namespace CodedByKay.ArtShow.CLI.Menus;

/// <summary>Cross-references the SQLite catalog against what's actually in the R2 bucket. For
/// browsing/viewing one record's full details, see ViewFlow instead — kept separate so this
/// flow's R2-listing round trip isn't in the way when all you want is to look at one item.</summary>
public static class StatusFlow
{
    private record Row(int Id, string Title, string Type, string Original, string Thumb, string Display, string Video);

    public static async Task RunAsync(ArtworkDbContext db, R2Client r2)
    {
        var artworks = db.Artworks.OrderBy(a => a.Id).ToList();
        var r2Objects = await AnsiConsole.Status().StartAsync("Listing R2 bucket contents...", _ => r2.ListAllObjectsAsync());

        var referencedKeys = new HashSet<string>();
        var missingCount = 0;
        var rows = new List<Row>();

        foreach (var a in artworks)
        {
            string originalCell, thumbCell, displayCell, videoCell;

            if (a.IsVideo)
            {
                originalCell = "—";
                videoCell = a.VideoId is null
                    ? "[yellow]not set[/]"
                    : $"[green]{a.VideoId.EscapeMarkup()}[/]";
            }
            else
            {
                originalCell = CheckKey(a.R2Key, r2Objects, referencedKeys, ref missingCount);
                videoCell = "—";
            }

            thumbCell = a.IsVideo && a.ThumbR2Key is null
                ? "—"
                : CheckKey(a.ThumbR2Key, r2Objects, referencedKeys, ref missingCount);

            // Display image is optional: null = original was already small enough.
            displayCell = a.DisplayR2Key is null
                ? "—"
                : CheckKey(a.DisplayR2Key, r2Objects, referencedKeys, ref missingCount);

            rows.Add(new Row(a.Id, a.Title, a.Type ?? "image", originalCell, thumbCell, displayCell, videoCell));
        }

        Paginate(rows);

        var orphaned = r2Objects.Keys.Where(k => !referencedKeys.Contains(k)).OrderBy(k => k).ToList();
        var totalBytes = r2Objects.Values.Sum();

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]{artworks.Count}[/] artwork(s) in the catalog, [bold]{r2Objects.Count}[/] object(s) in R2 ({FormatBytes(totalBytes)} total).");
        if (missingCount > 0)
            AnsiConsole.MarkupLine($"[red]{missingCount} R2 key(s) referenced by the catalog but missing from the bucket.[/]");

        if (orphaned.Count > 0)
        {
            AnsiConsole.MarkupLine($"[yellow]{orphaned.Count} object(s) in R2 not referenced by any catalog row[/] (e.g. old test uploads — see working-on/r2-api-docs.md):");
            foreach (var key in orphaned)
                AnsiConsole.MarkupLine($"  {key.EscapeMarkup()} ({FormatBytes(r2Objects[key])})");
        }
    }

    private static void Paginate(List<Row> rows) =>
        TablePager.Show(rows,
            newTable: () => new Table().Border(TableBorder.Rounded)
                .AddColumn("Id").AddColumn("Title").AddColumn("Type")
                .AddColumn("Original in R2").AddColumn("Thumb in R2").AddColumn("Display in R2").AddColumn("Video"),
            addRow: (table, row) => table.AddRow($"#{row.Id}", row.Title.EscapeMarkup(), row.Type, row.Original, row.Thumb, row.Display, row.Video),
            emptyMessage: "[yellow]Catalog is empty.[/]");

    private static string CheckKey(string? key, Dictionary<string, long> r2Objects, HashSet<string> referencedKeys, ref int missingCount)
    {
        if (key is null)
            return "[red]not uploaded[/]";

        referencedKeys.Add(key);
        if (r2Objects.ContainsKey(key))
            return "[green]✓[/]";

        missingCount++;
        return "[red]missing![/]";
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes / (1024.0 * 1024.0):F1} MB",
    };
}

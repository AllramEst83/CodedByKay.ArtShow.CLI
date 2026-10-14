using CodedByKay.ArtShow.CLI.Data;
using CodedByKay.ArtShow.CLI.Options;
using CodedByKay.ArtShow.CLI.Publishing;
using Spectre.Console;

namespace CodedByKay.ArtShow.CLI.Menus;

public static class PublishFlow
{
    public static async Task RunAsync(ArtworkDbContext db, R2Options r2, PublishOptions publish)
    {
        var artworks = db.Artworks.ToList();
        await ArtworkJsonWriter.WriteAsync(artworks, r2, publish);
        await CollectionsJsonWriter.WriteAsync(artworks, db.Classifiers.ToList(), publish);
        await PublishStateStore.MarkPublishedAsync(db);
        AnsiConsole.MarkupLine($"[green]Wrote {artworks.Count} item(s) to[/] {publish.OutputArtworkJsonPath.EscapeMarkup()}");
        AnsiConsole.MarkupLine($"[green]Wrote collections to[/] {publish.OutputCollectionsJsonPath.EscapeMarkup()}");
        AnsiConsole.MarkupLine("[grey]Metadata-only — review the diff, then commit + push the site repo yourself.[/]");
    }
}

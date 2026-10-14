using CodedByKay.ArtShow.CLI.Data;
using CodedByKay.ArtShow.CLI.Storage;
using Spectre.Console;

namespace CodedByKay.ArtShow.CLI.Menus;

public static class DeleteFlow
{
    public static async Task RunAsync(ArtworkDbContext db, R2Client r2)
    {
        var artworks = db.Artworks.OrderBy(a => a.Id).ToList();
        if (artworks.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Catalog is empty — nothing to delete.[/]");
            return;
        }

        var selected = ArtworkPicker.Pick(artworks, "Delete which artwork?");
        if (selected is null)
            return;

        var confirmed = AnsiConsole.Confirm(
            $"Delete #{selected.Id} — '{selected.Title.EscapeMarkup()}'? This also removes its image(s) from R2 and cannot be undone.",
            false);
        if (!confirmed)
            return;

        if (selected.R2Key is not null)
            await r2.DeleteAsync(selected.R2Key);
        if (selected.ThumbR2Key is not null)
            await r2.DeleteAsync(selected.ThumbR2Key);
        if (selected.DisplayR2Key is not null)
            await r2.DeleteAsync(selected.DisplayR2Key);

        db.Artworks.Remove(selected);
        await db.SaveChangesAsync();
        await PublishStateStore.MarkDirtyAsync(db);
        AnsiConsole.MarkupLine($"[green]Deleted #{selected.Id}.[/]");
    }
}

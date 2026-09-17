using CodedByKay.ArtShow.CLI.Data;
using Spectre.Console;

namespace CodedByKay.ArtShow.CLI.Menus;

public static class EditFlow
{
    public static async Task RunAsync(ArtworkDbContext db)
    {
        var artworks = db.Artworks.OrderBy(a => a.Id).ToList();
        if (artworks.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Catalog is empty — nothing to edit.[/]");
            return;
        }

        var selected = ArtworkPicker.Pick(artworks, "Which artwork?");
        if (selected is null)
            return;

        selected.Title = Prompts.Ask("Title:", defaultValue: selected.Title);
        selected.Description = PromptOptional("Description:", selected.Description);
        selected.Category = await CatalogPicker.PickOptionalAsync(db, ClassifierKind.Category, "Category:", selected.Category);
        selected.Medium = await CatalogPicker.PickOptionalAsync(db, ClassifierKind.Medium, "Medium:", selected.Medium);
        selected.Tags = await CatalogPicker.PickManyAsync(db, ClassifierKind.Tag, "Tags:", selected.Tags);
        selected.Groups = await CatalogPicker.PickManyAsync(db, ClassifierKind.Group, "Groups:", selected.Groups);

        if (selected.IsVideo)
        {
            var youtubeInput = Prompts.Ask("YouTube video URL or id:", defaultValue: selected.VideoId ?? "");
            var youtubeId = YouTubeIdParser.Parse(youtubeInput);
            if (youtubeId is null)
                AnsiConsole.MarkupLine($"[yellow]Couldn't find a YouTube video id in '{youtubeInput.EscapeMarkup()}' — leaving it unchanged.[/]");
            else
                selected.VideoId = youtubeId;
        }

        await db.SaveChangesAsync();
        await PublishStateStore.MarkDirtyAsync(db);
        AnsiConsole.MarkupLine($"[green]Updated {selected.Id}.[/]");
    }

    private static string? PromptOptional(string label, string? current)
    {
        var defaultValue = string.IsNullOrWhiteSpace(current) ? null : current;
        var value = Prompts.Ask($"{label} [grey](optional)[/]", defaultValue: defaultValue, allowEmpty: true);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

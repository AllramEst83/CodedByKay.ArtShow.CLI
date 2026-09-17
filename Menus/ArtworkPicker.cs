using CodedByKay.ArtShow.CLI.Data;
using Spectre.Console;

namespace CodedByKay.ArtShow.CLI.Menus;

/// <summary>
/// Shared "which artwork?" picker for View/Edit/Delete — shows the same paginated table as
/// Status/Sync (browsable with arrow keys / PageUp / PageDown once it's past a screen), then
/// asks for the id to act on. Replaces the old single-column SelectionPrompt&lt;Artwork&gt;
/// menu, which didn't show enough at once to tell two similarly-titled pieces apart.
/// </summary>
public static class ArtworkPicker
{
    public static Artwork? Pick(List<Artwork> artworks, string title)
    {
        while (true)
        {
            TablePager.Show(artworks,
                newTable: () => new Table().Border(TableBorder.Rounded).Title(title)
                    .AddColumn("Id").AddColumn("Title").AddColumn("Type").AddColumn("Category").AddColumn("Added"),
                addRow: (table, a) => table.AddRow(
                    $"#{a.Id}",
                    a.Title.EscapeMarkup(),
                    a.Type ?? "image",
                    a.Category ?? "—",
                    a.AddedDate.ToString("yyyy-MM-dd")),
                emptyMessage: "[yellow]Catalog is empty.[/]");

            var idStr = Prompts.Ask("Id [grey](blank to cancel)[/]:", allowEmpty: true);
            if (string.IsNullOrWhiteSpace(idStr))
                return null;

            if (!int.TryParse(idStr.Trim(), out var id))
            {
                AnsiConsole.MarkupLine("[red]Not a number.[/]");
                continue;
            }

            var match = artworks.FirstOrDefault(a => a.Id == id);
            if (match is null)
            {
                AnsiConsole.MarkupLine($"[red]No artwork with id {id}.[/]");
                continue;
            }

            return match;
        }
    }
}

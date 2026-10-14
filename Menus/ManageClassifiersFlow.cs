using CodedByKay.ArtShow.CLI.Data;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;

namespace CodedByKay.ArtShow.CLI.Menus;

/// <summary>TUI for cleaning up the Classifier table — add/rename/delete known Category,
/// Medium, Tag, and Group values. Not required for day-to-day use (Add/Edit already remembers
/// new values automatically via CatalogPicker) — this is for fixing typos, merging near-dupes,
/// or retiring values you no longer want offered.</summary>
public static class ManageClassifiersFlow
{
    public static async Task RunAsync(ArtworkDbContext db)
    {
        while (true)
        {
            var kind = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Manage which classifier?")
                    .AddChoices(ClassifierKind.Category, ClassifierKind.Medium, ClassifierKind.Tag, ClassifierKind.Group, "Back"));

            if (kind == "Back")
                return;

            await ManageKindAsync(db, kind);
        }
    }

    private static async Task ManageKindAsync(ArtworkDbContext db, string kind)
    {
        while (true)
        {
            var classifiers = await db.Classifiers.Where(c => c.Kind == kind).OrderBy(c => c.Name).ToListAsync();
            var artworks = await db.Artworks.ToListAsync();

            AnsiConsole.WriteLine();
            var title = kind switch
            {
                ClassifierKind.Category => "Categories",
                ClassifierKind.Medium => "Mediums",
                ClassifierKind.Tag => "Tags",
                ClassifierKind.Group => "Groups",
                _ => kind,
            };
            TablePager.Show(classifiers,
                newTable: () => new Table().Border(TableBorder.Rounded).Title(title).AddColumn("Name").AddColumn("Used by"),
                addRow: (table, c) => table.AddRow(c.Name.EscapeMarkup(), $"{UsageCount(artworks, kind, c.Name)} artwork(s)"),
                emptyMessage: "[yellow]None yet.[/]");

            // A group is a "collection" on the site; only groups carry presentation details.
            var actions = kind == ClassifierKind.Group
                ? new[] { "Add", "Rename", "Delete", "Collection details", "Back" }
                : new[] { "Add", "Rename", "Delete", "Back" };
            var action = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Action?")
                    .AddChoices(actions));

            switch (action)
            {
                case "Add": await AddAsync(db, kind); break;
                case "Rename": await RenameAsync(db, kind, classifiers); break;
                case "Delete": await DeleteAsync(db, kind, classifiers, artworks); break;
                case "Collection details": await EditCollectionDetailsAsync(db, classifiers, artworks); break;
                case "Back": return;
            }
        }
    }

    /// <summary>Blurb, cover piece and display order for a group — published to
    /// data/collections.json. Everything is optional; empty = site default.</summary>
    private static async Task EditCollectionDetailsAsync(ArtworkDbContext db, List<Classifier> groups, List<Artwork> artworks)
    {
        if (groups.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No groups yet.[/]");
            return;
        }

        var target = AnsiConsole.Prompt(
            new SelectionPrompt<Classifier>().Title("Details for which collection?").UseConverter(c => c.Name).AddChoices(groups));

        var blurb = Prompts.Ask("Blurb [grey](one line, optional)[/]:", target.Blurb, allowEmpty: true).Trim();
        target.Blurb = blurb.Length == 0 ? null : blurb;

        var orderText = Prompts.Ask("Order [grey](number, lower first; empty = by size)[/]:", target.SortOrder?.ToString(), allowEmpty: true).Trim();
        if (orderText.Length == 0)
            target.SortOrder = null;
        else if (int.TryParse(orderText, out var order))
            target.SortOrder = order;
        else
            AnsiConsole.MarkupLine("[yellow]Not a number — order left unchanged.[/]");

        var members = artworks
            .Where(a => !a.IsVideo && a.Groups.Any(g => string.Equals(g, target.Name, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(a => a.Id)
            .ToList();
        if (members.Count > 0 && AnsiConsole.Confirm("Pick a cover piece?", target.CoverArtworkId is not null))
        {
            var cover = ArtworkPicker.Pick(members, $"Cover for '{target.Name.EscapeMarkup()}':");
            if (cover is not null)
                target.CoverArtworkId = cover.Id;
        }
        else if (target.CoverArtworkId is not null && AnsiConsole.Confirm("Clear the cover (use newest piece)?", false))
        {
            target.CoverArtworkId = null;
        }

        await db.SaveChangesAsync();
        await PublishStateStore.MarkDirtyAsync(db);
        AnsiConsole.MarkupLine("[green]Saved.[/]");
    }

    private static int UsageCount(List<Artwork> artworks, string kind, string name) => kind switch
    {
        ClassifierKind.Category => artworks.Count(a => string.Equals(a.Category, name, StringComparison.OrdinalIgnoreCase)),
        ClassifierKind.Medium => artworks.Count(a => string.Equals(a.Medium, name, StringComparison.OrdinalIgnoreCase)),
        ClassifierKind.Tag => artworks.Count(a => a.Tags.Any(t => string.Equals(t, name, StringComparison.OrdinalIgnoreCase))),
        ClassifierKind.Group => artworks.Count(a => a.Groups.Any(g => string.Equals(g, name, StringComparison.OrdinalIgnoreCase))),
        _ => 0,
    };

    private static async Task AddAsync(ArtworkDbContext db, string kind)
    {
        var name = Prompts.Ask("Name:").Trim();
        if (string.IsNullOrWhiteSpace(name))
            return;

        var exists = await db.Classifiers.AnyAsync(c => c.Kind == kind && c.Name.ToLower() == name.ToLower());
        if (exists)
        {
            AnsiConsole.MarkupLine("[yellow]Already exists.[/]");
            return;
        }

        db.Classifiers.Add(new Classifier { Kind = kind, Name = name });
        await db.SaveChangesAsync();
        AnsiConsole.MarkupLine("[green]Added.[/]");
    }

    private static async Task RenameAsync(ArtworkDbContext db, string kind, List<Classifier> classifiers)
    {
        if (classifiers.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Nothing to rename.[/]");
            return;
        }

        var target = AnsiConsole.Prompt(
            new SelectionPrompt<Classifier>().Title("Rename which?").UseConverter(c => c.Name).AddChoices(classifiers));
        var newName = Prompts.Ask($"New name for '{target.Name.EscapeMarkup()}':").Trim();
        if (string.IsNullOrWhiteSpace(newName) || newName == target.Name)
            return;

        var dupe = await db.Classifiers.AnyAsync(c => c.Kind == kind && c.Id != target.Id && c.Name.ToLower() == newName.ToLower());
        if (dupe)
        {
            AnsiConsole.MarkupLine("[red]That name already exists.[/]");
            return;
        }

        var oldName = target.Name;
        target.Name = newName;
        var touchedArtwork = await CascadeRenameAsync(db, kind, oldName, newName);
        await db.SaveChangesAsync();
        if (touchedArtwork)
            await PublishStateStore.MarkDirtyAsync(db);
        AnsiConsole.MarkupLine($"[green]Renamed '{oldName.EscapeMarkup()}' → '{newName.EscapeMarkup()}'.[/]");
    }

    private static async Task<bool> CascadeRenameAsync(ArtworkDbContext db, string kind, string oldName, string newName)
    {
        var artworks = await db.Artworks.ToListAsync();
        var touched = false;
        foreach (var a in artworks)
        {
            switch (kind)
            {
                case ClassifierKind.Category when string.Equals(a.Category, oldName, StringComparison.OrdinalIgnoreCase):
                    a.Category = newName;
                    touched = true;
                    break;
                case ClassifierKind.Medium when string.Equals(a.Medium, oldName, StringComparison.OrdinalIgnoreCase):
                    a.Medium = newName;
                    touched = true;
                    break;
                case ClassifierKind.Tag when a.Tags.Any(t => string.Equals(t, oldName, StringComparison.OrdinalIgnoreCase)):
                    a.Tags = Renamed(a.Tags, oldName, newName);
                    touched = true;
                    break;
                case ClassifierKind.Group when a.Groups.Any(g => string.Equals(g, oldName, StringComparison.OrdinalIgnoreCase)):
                    a.Groups = Renamed(a.Groups, oldName, newName);
                    touched = true;
                    break;
            }
        }
        return touched;
    }

    // Renaming can collide with a value already present elsewhere in the same list (e.g.
    // renaming "sketch" to "pencil" on an item that already has both) — dedup after the swap.
    private static List<string> Renamed(List<string> values, string oldName, string newName) =>
        values
            .Select(v => string.Equals(v, oldName, StringComparison.OrdinalIgnoreCase) ? newName : v)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static async Task DeleteAsync(ArtworkDbContext db, string kind, List<Classifier> classifiers, List<Artwork> artworks)
    {
        if (classifiers.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]Nothing to delete.[/]");
            return;
        }

        var target = AnsiConsole.Prompt(
            new SelectionPrompt<Classifier>().Title("Delete which?").UseConverter(c => c.Name).AddChoices(classifiers));
        var count = UsageCount(artworks, kind, target.Name);

        if (count > 0)
        {
            var confirmed = AnsiConsole.Confirm(
                $"'{target.Name.EscapeMarkup()}' is used by {count} artwork(s) — delete anyway and remove it from them?", false);
            if (!confirmed)
                return;

            await RemoveFromArtworkAsync(db, kind, target.Name);
            await PublishStateStore.MarkDirtyAsync(db);
        }

        db.Classifiers.Remove(target);
        await db.SaveChangesAsync();
        AnsiConsole.MarkupLine("[green]Deleted.[/]");
    }

    private static async Task RemoveFromArtworkAsync(ArtworkDbContext db, string kind, string name)
    {
        var artworks = await db.Artworks.ToListAsync();
        foreach (var a in artworks)
        {
            switch (kind)
            {
                case ClassifierKind.Category when string.Equals(a.Category, name, StringComparison.OrdinalIgnoreCase):
                    a.Category = null;
                    break;
                case ClassifierKind.Medium when string.Equals(a.Medium, name, StringComparison.OrdinalIgnoreCase):
                    a.Medium = null;
                    break;
                case ClassifierKind.Tag:
                    a.Tags = a.Tags.Where(t => !string.Equals(t, name, StringComparison.OrdinalIgnoreCase)).ToList();
                    break;
                case ClassifierKind.Group:
                    a.Groups = a.Groups.Where(g => !string.Equals(g, name, StringComparison.OrdinalIgnoreCase)).ToList();
                    break;
            }
        }
        await db.SaveChangesAsync();
    }
}

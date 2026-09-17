using CodedByKay.ArtShow.CLI.Data;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;

namespace CodedByKay.ArtShow.CLI.Menus;

/// <summary>
/// "Dropdown"-style pickers for Category/Medium/Tags/Groups, backed by the <see cref="Classifier"/>
/// table. Falls back to plain free-text entry when there are no known values yet for a kind.
/// A newly typed value is remembered automatically (added to Classifiers) so it shows up as a
/// pickable choice from then on — "Manage classifiers" (see ManageClassifiersFlow) is only
/// needed for cleanup, not for day-to-day use.
/// </summary>
public static class CatalogPicker
{
    private const string NewValueSentinel = "＋ Enter a new value...";
    private const string NoneSentinel = "— none —";

    /// <summary>Single optional value — pick from what's already known, or type a new one.</summary>
    public static async Task<string?> PickOptionalAsync(ArtworkDbContext db, string kind, string label, string? current)
    {
        var existing = await KnownValuesAsync(db, kind);

        if (existing.Count == 0)
            return await FreeTextAsync(db, kind, label, current);

        var choices = new List<string>();
        if (!string.IsNullOrWhiteSpace(current))
            choices.Add(current); // first = highlighted by default, so Enter alone keeps it
        choices.AddRange(existing.Where(e => !string.Equals(e, current, StringComparison.OrdinalIgnoreCase)));
        choices.Add(NewValueSentinel);
        choices.Add(NoneSentinel);

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title(label)
                .PageSize(15)
                .EnableSearch()
                .AddChoices(choices));

        return selection switch
        {
            NewValueSentinel => await FreeTextAsync(db, kind, label, null),
            NoneSentinel => null,
            _ => selection,
        };
    }

    /// <summary>Multi-select from what's already known, plus a free-text field for anything new.</summary>
    public static async Task<List<string>> PickManyAsync(ArtworkDbContext db, string kind, string label, IEnumerable<string> currentSelected)
    {
        var existing = await KnownValuesAsync(db, kind);
        var selected = new List<string>();

        if (existing.Count > 0)
        {
            var prompt = new MultiSelectionPrompt<string>()
                .Title($"{label} [grey](<space> to toggle, <enter> to confirm)[/]")
                .PageSize(15)
                .NotRequired()
                .AddChoices(existing);

            foreach (var current in currentSelected)
            {
                var match = existing.FirstOrDefault(e => string.Equals(e, current, StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                    prompt.Select(match);
            }

            selected = AnsiConsole.Prompt(prompt);
        }

        var newRaw = Prompts.Ask($"{label} — add new [grey](comma-separated, optional)[/]:", allowEmpty: true);
        var newOnes = newRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var name in newOnes)
            await EnsureKnownAsync(db, kind, name);

        var result = selected.Concat(newOnes).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // Unlike SelectionPrompt/TextPrompt, MultiSelectionPrompt doesn't leave a visible trace
        // of what was picked once it closes — echo it explicitly so it's not silently unclear.
        var summary = result.Count == 0 ? "[grey](none)[/]" : string.Join(", ", result.Select(r => r.EscapeMarkup()));
        AnsiConsole.MarkupLine($"{label} {summary}");

        return result;
    }

    private static async Task<string?> FreeTextAsync(ArtworkDbContext db, string kind, string label, string? current)
    {
        var defaultValue = string.IsNullOrWhiteSpace(current) ? null : current;
        var value = Prompts.Ask($"{label} [grey](optional)[/]:", defaultValue: defaultValue, allowEmpty: true);
        if (string.IsNullOrWhiteSpace(value))
            return null;

        await EnsureKnownAsync(db, kind, value);
        return value;
    }

    private static Task<List<string>> KnownValuesAsync(ArtworkDbContext db, string kind) =>
        db.Classifiers.Where(c => c.Kind == kind).OrderBy(c => c.Name).Select(c => c.Name).ToListAsync();

    private static async Task EnsureKnownAsync(ArtworkDbContext db, string kind, string name)
    {
        var lower = name.ToLowerInvariant();
        var exists = await db.Classifiers.AnyAsync(c => c.Kind == kind && c.Name.ToLower() == lower);
        if (exists)
            return;

        db.Classifiers.Add(new Classifier { Kind = kind, Name = name });
        await db.SaveChangesAsync();
    }
}

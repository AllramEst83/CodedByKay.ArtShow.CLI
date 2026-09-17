using Microsoft.EntityFrameworkCore;

namespace CodedByKay.ArtShow.CLI.Data;

/// <summary>One-time backfill so existing catalog values aren't lost when the Classifiers
/// table is introduced — runs at startup, no-ops once the table has any rows.</summary>
public static class ClassifierSeeder
{
    public static async Task SeedFromExistingArtworkAsync(ArtworkDbContext db)
    {
        if (await db.Classifiers.AnyAsync())
            return;

        var artworks = await db.Artworks.ToListAsync();

        var seeds = new List<Classifier>();
        seeds.AddRange(Distinct(artworks.Select(a => a.Category)).Select(name => new Classifier { Kind = ClassifierKind.Category, Name = name }));
        seeds.AddRange(Distinct(artworks.Select(a => a.Medium)).Select(name => new Classifier { Kind = ClassifierKind.Medium, Name = name }));
        seeds.AddRange(Distinct(artworks.SelectMany(a => a.Tags)).Select(name => new Classifier { Kind = ClassifierKind.Tag, Name = name }));
        seeds.AddRange(Distinct(artworks.SelectMany(a => a.Groups)).Select(name => new Classifier { Kind = ClassifierKind.Group, Name = name }));

        if (seeds.Count == 0)
            return;

        db.Classifiers.AddRange(seeds);
        await db.SaveChangesAsync();
    }

    private static IEnumerable<string> Distinct(IEnumerable<string?> values) =>
        values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .Distinct(StringComparer.OrdinalIgnoreCase);
}

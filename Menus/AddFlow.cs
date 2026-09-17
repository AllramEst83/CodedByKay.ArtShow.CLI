using System.Text.RegularExpressions;
using CodedByKay.ArtShow.CLI.Data;
using CodedByKay.ArtShow.CLI.Imaging;
using CodedByKay.ArtShow.CLI.Storage;
using Spectre.Console;

namespace CodedByKay.ArtShow.CLI.Menus;

public static partial class AddFlow
{
    public static async Task RunAsync(ArtworkDbContext db, R2Client r2)
    {
        var kind = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Type?")
                .AddChoices("Image", "Video"));

        var isVideo = kind == "Video";

        string? filePath = null;
        string? youtubeId = null;

        if (isVideo)
        {
            var youtubeInput = Prompts.Ask("YouTube video URL or id [grey](upload it via YouTube Studio first)[/]:");
            youtubeId = YouTubeIdParser.Parse(youtubeInput);
            if (youtubeId is null)
            {
                AnsiConsole.MarkupLine($"[red]Couldn't find a YouTube video id in:[/] {youtubeInput.EscapeMarkup()}");
                return;
            }
        }
        else
        {
            filePath = Prompts.Ask("File path of the artwork:");
            filePath = filePath.Trim('"');
            if (!File.Exists(filePath))
            {
                AnsiConsole.MarkupLine($"[red]File not found:[/] {filePath.EscapeMarkup()}");
                return;
            }
        }

        var title = Prompts.Ask("Title:");
        var description = Prompts.Ask("Description [grey](optional)[/]:", allowEmpty: true);
        var category = await CatalogPicker.PickOptionalAsync(db, ClassifierKind.Category, "Category:", null);
        var medium = await CatalogPicker.PickOptionalAsync(db, ClassifierKind.Medium, "Medium:", null);
        var createdDateStr = Prompts.Ask("Created date [grey](yyyy-MM-dd, optional)[/]:", allowEmpty: true);
        var tags = await CatalogPicker.PickManyAsync(db, ClassifierKind.Tag, "Tags:", []);
        var groups = await CatalogPicker.PickManyAsync(db, ClassifierKind.Group, "Groups:", []);

        var addedDate = DateOnly.FromDateTime(DateTime.Now);
        var createdDate = string.IsNullOrWhiteSpace(createdDateStr) ? (DateOnly?)null : DateOnly.Parse(createdDateStr);

        var artwork = new Artwork
        {
            Type = isVideo ? "video" : null,
            AddedDate = addedDate,
            Title = title,
            Description = NullIfEmpty(description),
            CreatedDate = createdDate,
            Category = category,
            Medium = medium,
            Tags = tags,
            Groups = groups,
            // Video rows have no local file — the id doubles as a dedup key.
            OriginalPath = isVideo ? $"youtube:{youtubeId}" : Path.GetFullPath(filePath!),
        };

        if (isVideo)
        {
            artwork.VideoId = youtubeId;
        }
        else
        {
            var slug = Slugify(title);
            var year = (createdDate ?? addedDate).Year.ToString();

            await AnsiConsole.Status().StartAsync("Uploading to R2...", async ctx =>
            {
                var originalKey = $"originals/{year}/{slug}.webp";
                var thumbKey = $"thumbs/{year}/{slug}-600w.webp";

                ctx.Status("Encoding + uploading original...");
                await using (var sourceForOriginal = File.OpenRead(filePath!))
                {
                    var originalBytes = ThumbnailGenerator.EncodeOriginalAsWebp(sourceForOriginal);
                    using var originalStream = new MemoryStream(originalBytes);
                    await r2.UploadAsync(originalKey, originalStream, "image/webp");
                }

                ctx.Status("Generating + uploading thumbnail...");
                await using (var sourceForThumb = File.OpenRead(filePath!))
                {
                    var thumbBytes = ThumbnailGenerator.CreateThumbnail(sourceForThumb);
                    using var thumbStream = new MemoryStream(thumbBytes);
                    await r2.UploadAsync(thumbKey, thumbStream, "image/webp");
                }

                artwork.R2Key = originalKey;
                artwork.ThumbR2Key = thumbKey;
            });
        }

        db.Artworks.Add(artwork);
        await db.SaveChangesAsync();
        await PublishStateStore.MarkDirtyAsync(db);
        AnsiConsole.MarkupLine($"[green]Added #{artwork.Id}: {title.EscapeMarkup()}[/]");
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string Slugify(string title) =>
        NonSlugCharsPattern().Replace(title.ToLowerInvariant(), "-").Trim('-');

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonSlugCharsPattern();
}

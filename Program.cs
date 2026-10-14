using CodedByKay.ArtShow.CLI.Data;
using CodedByKay.ArtShow.CLI.Menus;
using CodedByKay.ArtShow.CLI.Options;
using CodedByKay.ArtShow.CLI.Storage;
using CodedByKay.ArtShow.CLI.Terminal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .Build();

var r2Options = configuration.GetSection(R2Options.SectionName).Get<R2Options>() ?? new R2Options();
var catalogOptions = configuration.GetSection(CatalogOptions.SectionName).Get<CatalogOptions>() ?? new CatalogOptions();
var publishOptions = configuration.GetSection(PublishOptions.SectionName).Get<PublishOptions>() ?? new PublishOptions();

var dbOptions = new DbContextOptionsBuilder<ArtworkDbContext>()
    .UseSqlite($"Data Source={catalogOptions.DbPath}")
    .Options;
await using var db = new ArtworkDbContext(dbOptions);
await db.Database.MigrateAsync();
await ClassifierSeeder.SeedFromExistingArtworkAsync(db);

// `ArtShow.CLI --publish`: write data/artwork.json + data/collections.json and exit, no TUI.
// Same code path as the menu's Publish action; handy for scripting and for checking that the
// MCP's publish produces byte-identical output.
if (args.Contains("--publish"))
{
    await PublishFlow.RunAsync(db, r2Options, publishOptions);
    return;
}

using var alternateScreen = new AlternateScreen();
Console.CancelKeyPress += (_, _) => alternateScreen.Dispose(); // Ctrl+C bypasses normal unwind — restore explicitly

AnsiConsole.Write(new FigletText("ArtShow CLI").Color(Color.SteelBlue1));

while (true)
{
    var publishLabel = await PublishStateStore.IsDirtyAsync(db)
        ? "Publish [yellow](pending changes)[/]"
        : "Publish";

    var choice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("What do you want to do?")
            .AddChoices(
                "Add artwork",
                "Edit artwork",
                "Delete artwork",
                "View artwork",
                "Manage classifiers",
                publishLabel,
                "Status / sync check",
                "Exit"));

    try
    {
        switch (choice)
        {
            case "Add artwork":
                RequireR2Credentials(r2Options);
                using (var r2 = new R2Client(r2Options))
                    await AddFlow.RunAsync(db, r2);
                break;

            case "Edit artwork":
                await EditFlow.RunAsync(db);
                break;

            case "Delete artwork":
                RequireR2Credentials(r2Options);
                using (var r2 = new R2Client(r2Options))
                    await DeleteFlow.RunAsync(db, r2);
                break;

            case "View artwork":
                ViewFlow.Run(db, r2Options);
                break;

            case "Manage classifiers":
                await ManageClassifiersFlow.RunAsync(db);
                break;

            case var c when c.StartsWith("Publish"):
                await PublishFlow.RunAsync(db, r2Options, publishOptions);
                break;

            case "Status / sync check":
                RequireR2Credentials(r2Options);
                using (var r2 = new R2Client(r2Options))
                    await StatusFlow.RunAsync(db, r2);
                break;

            case "Exit":
                return;
        }
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message.EscapeMarkup()}");
    }

    AnsiConsole.WriteLine();
}

static void RequireR2Credentials(R2Options options)
{
    if (string.IsNullOrWhiteSpace(options.AccessKeyId) || string.IsNullOrWhiteSpace(options.SecretAccessKey))
    {
        throw new InvalidOperationException(
            "R2 credentials not configured. Set R2:AccessKeyId / R2:SecretAccessKey via " +
            "'dotnet user-secrets set' (see AGENTS.md → Config).");
    }
}

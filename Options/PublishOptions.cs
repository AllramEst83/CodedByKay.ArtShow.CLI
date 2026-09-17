namespace CodedByKay.ArtShow.CLI.Options;

public class PublishOptions
{
    public const string SectionName = "Publish";

    // Defaults to Kay's confirmed local path (see AGENTS.md "Known local paths"). Overridable via config.
    public string SiteRepoPath { get; set; } = @"C:\Users\kaywi\dev\CodedByKay.ArtShow";

    public string OutputArtworkJsonPath => Path.Combine(SiteRepoPath, "data", "artwork.json");
}

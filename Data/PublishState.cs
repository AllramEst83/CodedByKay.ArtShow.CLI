namespace CodedByKay.ArtShow.CLI.Data;

/// <summary>Singleton row (Id always 1) tracking whether the catalog has changes since the last
/// successful Publish — drives the "pending changes" indicator on the main menu.</summary>
public class PublishState
{
    public int Id { get; set; } = 1;
    public bool Dirty { get; set; }
}

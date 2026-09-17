namespace CodedByKay.ArtShow.CLI.Options;

public class R2Options
{
    public const string SectionName = "R2";

    public string AccountId { get; set; } = "";
    public string AccessKeyId { get; set; } = "";
    public string SecretAccessKey { get; set; } = "";
    public string Bucket { get; set; } = "";
    public string PublicBaseUrl { get; set; } = "";

    public string ServiceUrl => $"https://{AccountId}.r2.cloudflarestorage.com";
}

using System.Text.RegularExpressions;

namespace CodedByKay.ArtShow.CLI.Menus;

/// <summary>Extracts an 11-char YouTube video id from a pasted URL or bare id.</summary>
public static partial class YouTubeIdParser
{
    public static string? Parse(string input)
    {
        input = input.Trim();
        if (BareIdPattern().IsMatch(input))
            return input;

        var match = UrlIdPattern().Match(input);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"^[A-Za-z0-9_-]{11}$")]
    private static partial Regex BareIdPattern();

    [GeneratedRegex(@"(?:youtu\.be/|youtube\.com/(?:watch\?v=|embed/|shorts/))([A-Za-z0-9_-]{11})")]
    private static partial Regex UrlIdPattern();
}

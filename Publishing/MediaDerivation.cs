using System.Text.RegularExpressions;

namespace CodedByKay.ArtShow.CLI.Publishing;

/// <summary>
/// Derives the normalised medium vocabulary the site's Medium facet uses from the free-text
/// Medium field plus tags. MUST stay identical to the MCP's src/publishing/derive.ts and to the
/// site's js/modules/data.js fallback (same patterns, same order). RegexOptions.ECMAScript makes
/// \b and \s ASCII-only like JavaScript's, so the three agree on non-ASCII text.
/// </summary>
public static class MediaDerivation
{
    private static readonly (string Key, Regex Pattern)[] Patterns =
    [
        ("pencil", Make("pencil")),
        ("watercolor", Make(@"water\s?colou?r")),
        ("ink", Make(@"\bink\b|fine\s?liner")),
        ("marker", Make("marker")),
        ("digital", Make("digital")),
        ("photography", Make("photo")),
    ];

    private static Regex Make(string pattern) =>
        new(pattern, RegexOptions.IgnoreCase | RegexOptions.ECMAScript | RegexOptions.CultureInvariant);

    public static List<string> Derive(string? medium, IEnumerable<string> tags)
    {
        var text = $"{medium ?? ""} {string.Join(' ', tags)}";
        return Patterns.Where(p => p.Pattern.IsMatch(text)).Select(p => p.Key).ToList();
    }

    /// <summary>"year" when the stored date is a January 1st (the catalog's year-only
    /// convention: 127 of 143 rows), "day" otherwise, null when there is no date. Same rule as the
    /// site's fallback; swap for a real stored precision if the CLI ever captures one.</summary>
    public static string? DatePrecision(DateOnly? createdDate) =>
        createdDate is null ? null : createdDate is { Month: 1, Day: 1 } ? "year" : "day";
}

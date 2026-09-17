namespace CodedByKay.ArtShow.CLI.Data;

/// <summary>The four free-text fields on <see cref="Artwork"/> that benefit from a known,
/// manageable set of values instead of retyping/misremembering them each time.</summary>
public static class ClassifierKind
{
    public const string Category = "Category";
    public const string Medium = "Medium";
    public const string Tag = "Tag";
    public const string Group = "Group";

    public static readonly string[] All = [Category, Medium, Tag, Group];
}

/// <summary>
/// A known value for one of the <see cref="ClassifierKind"/>s — e.g. Kind="Tag", Name="pencil".
/// Independent of whether any <see cref="Artwork"/> currently uses it: you can pre-declare one
/// before it's used, and Add/Edit auto-adds one here the first time you type a brand-new value,
/// so this table never needs manual upkeep to stay complete — "Manage classifiers" is only for
/// cleanup (rename/delete), not required bookkeeping.
/// </summary>
public class Classifier
{
    public int Id { get; set; }
    public string Kind { get; set; } = "";
    public string Name { get; set; } = "";
}

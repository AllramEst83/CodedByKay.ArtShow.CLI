using Spectre.Console;

namespace CodedByKay.ArtShow.CLI.Menus;

/// <summary>
/// Real in-app paging for a Table — Spectre.Console's Table has no scrollable viewport of its
/// own, it just prints and relies on the terminal's scrollback. This gives every list in the app
/// (not just Status/sync) one screenful of rows at a time, navigated with the arrow keys /
/// PageUp / PageDown, so views stay usable as the catalog grows well past a screen's height.
/// </summary>
public static class TablePager
{
    public static void Show<T>(
        IReadOnlyList<T> rows,
        Func<Table> newTable,
        Action<Table, T> addRow,
        string emptyMessage = "[yellow](none)[/]")
    {
        if (rows.Count == 0)
        {
            AnsiConsole.MarkupLine(emptyMessage);
            return;
        }

        var pageSize = Math.Max(5, AnsiConsole.Profile.Height - 10);
        var pageCount = (int)Math.Ceiling(rows.Count / (double)pageSize);
        var page = 0;

        while (true)
        {
            AnsiConsole.Clear();

            var table = newTable();
            foreach (var row in rows.Skip(page * pageSize).Take(pageSize))
                addRow(table, row);

            AnsiConsole.Write(table);

            if (pageCount <= 1)
                break;

            AnsiConsole.MarkupLine($"[grey]Page {page + 1}/{pageCount} — ↑/↓ or PageUp/PageDown to scroll, any other key to continue[/]");

            var key = Console.ReadKey(intercept: true).Key;
            switch (key)
            {
                case ConsoleKey.DownArrow or ConsoleKey.PageDown when page < pageCount - 1:
                    page++;
                    break;
                case ConsoleKey.UpArrow or ConsoleKey.PageUp when page > 0:
                    page--;
                    break;
                case ConsoleKey.DownArrow or ConsoleKey.PageDown or ConsoleKey.UpArrow or ConsoleKey.PageUp:
                    break; // at the first/last page already — ignore and keep browsing
                default:
                    return;
            }
        }
    }
}

using Spectre.Console;

namespace CodedByKay.ArtShow.CLI.Menus;

/// <summary>
/// Free-text input with real line editing — Left/Right/Home/End/Delete move and edit in place,
/// not just Backspace-from-the-end like Spectre.Console's own TextPrompt. A default value (for
/// editing an existing field) is pre-filled into the buffer as ordinary editable text rather than
/// a separate "press Enter to keep it" hint, so arrow keys work on it too. Replaces every
/// AnsiConsole.Ask/TextPrompt call site for plain strings across the app.
/// </summary>
public static class Prompts
{
    public static string Ask(string label, string? defaultValue = null, bool allowEmpty = false)
    {
        while (true)
        {
            AnsiConsole.Markup(label.TrimEnd() + " ");
            var startLeft = Console.CursorLeft;
            var startTop = Console.CursorTop;

            var buffer = new List<char>(defaultValue ?? "");
            var cursor = buffer.Count;
            var renderedLength = 0;
            Redraw(startLeft, startTop, buffer, cursor, ref renderedLength);

            var submitted = false;

            while (!submitted)
            {
                var key = Console.ReadKey(intercept: true);
                switch (key.Key)
                {
                    case ConsoleKey.Enter:
                        var text = new string(buffer.ToArray());
                        if (text.Length == 0 && !allowEmpty)
                        {
                            submitted = true; // fall through to the "invalid" retry below
                        }
                        else
                        {
                            SetLogicalCursor(startLeft, startTop, buffer.Count);
                            AnsiConsole.WriteLine();
                            return text;
                        }
                        break;

                    case ConsoleKey.LeftArrow:
                        if (cursor > 0)
                        {
                            cursor--;
                            SetLogicalCursor(startLeft, startTop, cursor);
                        }
                        break;

                    case ConsoleKey.RightArrow:
                        if (cursor < buffer.Count)
                        {
                            cursor++;
                            SetLogicalCursor(startLeft, startTop, cursor);
                        }
                        break;

                    case ConsoleKey.Home:
                        cursor = 0;
                        SetLogicalCursor(startLeft, startTop, cursor);
                        break;

                    case ConsoleKey.End:
                        cursor = buffer.Count;
                        SetLogicalCursor(startLeft, startTop, cursor);
                        break;

                    case ConsoleKey.Backspace:
                        if (cursor > 0)
                        {
                            buffer.RemoveAt(cursor - 1);
                            cursor--;
                            Redraw(startLeft, startTop, buffer, cursor, ref renderedLength);
                        }
                        break;

                    case ConsoleKey.Delete:
                        if (cursor < buffer.Count)
                        {
                            buffer.RemoveAt(cursor);
                            Redraw(startLeft, startTop, buffer, cursor, ref renderedLength);
                        }
                        break;

                    default:
                        if (!char.IsControl(key.KeyChar))
                        {
                            buffer.Insert(cursor, key.KeyChar);
                            cursor++;
                            Redraw(startLeft, startTop, buffer, cursor, ref renderedLength);
                        }
                        break;
                }
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[red]Value required.[/]");
        }
    }

    private static void Redraw(int startLeft, int startTop, List<char> buffer, int cursor, ref int renderedLength)
    {
        Console.SetCursorPosition(startLeft, startTop);
        Console.Write(buffer.ToArray());
        if (renderedLength > buffer.Count)
            Console.Write(new string(' ', renderedLength - buffer.Count));
        renderedLength = buffer.Count;
        SetLogicalCursor(startLeft, startTop, cursor);
    }

    private static void SetLogicalCursor(int startLeft, int startTop, int index)
    {
        var width = Math.Max(1, Console.BufferWidth);
        var absolute = startLeft + index;
        var row = startTop + absolute / width;
        var col = absolute % width;
        if (row < Console.BufferHeight)
            Console.SetCursorPosition(col, row);
    }
}

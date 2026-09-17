namespace CodedByKay.ArtShow.CLI.Terminal;

/// <summary>
/// Enters the terminal's "alternate screen buffer" — the same mechanism vim/htop/less use — so
/// this app's interactive session doesn't scroll through the shell's prior command history, and
/// whatever was on screen before is restored exactly as it was on exit. Standard VT100 sequence
/// (ESC[?1049h / ESC[?1049l); supported by Windows Terminal and any modern VT-processing
/// console — on a very old conhost.exe window it's likely just a no-op, not an error.
/// </summary>
public sealed class AlternateScreen : IDisposable
{
    // Console.Clear() (and the escape sequence itself) only makes sense against a real
    // console — redirected output (piped to a file, a non-interactive/CI context) has no
    // screen to switch, and Console.Clear() throws IOException there. Skip entirely rather
    // than risk throwing from inside the constructor: if that happened, `using` would never
    // get to run Dispose(), leaving the real terminal stuck in the alternate buffer forever.
    private readonly bool _active = !Console.IsOutputRedirected;
    private bool _left;

    public AlternateScreen()
    {
        if (!_active)
            return;

        Console.Out.Write("\x1b[?1049h");
        Console.Out.Flush();
        Console.Clear();
    }

    public void Dispose()
    {
        if (!_active || _left)
            return;
        _left = true;
        Console.Out.Write("\x1b[?1049l");
        Console.Out.Flush();
    }
}

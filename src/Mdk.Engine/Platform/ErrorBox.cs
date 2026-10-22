using SDL;

namespace Mdk.Engine.Platform;

/// <summary>Personal tweak: the program is a windowed app on Windows (no console window opens with
/// it), so an error that stops it before the game starts shows in a message box, not only on the
/// console nobody sees.</summary>
public static class ErrorBox
{
    /// <summary>Writes <paramref name="message"/> to the error output and, unless the run is hidden
    /// (tests, <c>MDK_HIDDEN</c>), shows it in a box; SDL needs no initialising for it.</summary>
    public static unsafe void Show(string message)
    {
        Console.Error.WriteLine(message);
        if (Environment.GetEnvironmentVariable("MDK_HIDDEN") == null)
        {
            SDL3.SDL_ShowSimpleMessageBox(SDL_MessageBoxFlags.SDL_MESSAGEBOX_ERROR, "MDK", message, null);
        }
    }
}

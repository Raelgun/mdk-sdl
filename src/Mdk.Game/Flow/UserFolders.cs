namespace Mdk.Game.Flow;

/// <summary>Where settings, saves and mods live: next to the executable (a portable install), or,
/// when that folder can't be written (Program Files, a protected Steam or GOG folder), the user's
/// own data folder, e.g. <c>%APPDATA%\mdk-sdl</c> or <c>~/.local/share/mdk-sdl</c>.</summary>
public static class UserFolders
{
    private const string Name = "mdk-sdl";
    private const string Probe = ".write-test";

    /// <summary>The user's data folder of this program (not created).</summary>
    public static string Personal()
    {
        var root = OperatingSystem.IsWindows() ? Environment.SpecialFolder.ApplicationData : Environment.SpecialFolder.LocalApplicationData;
        return Path.Combine(Environment.GetFolderPath(root), Name);
    }

    /// <summary><paramref name="beside"/> when a file can be written there, else <paramref name="personal"/> (created).</summary>
    public static string Choose(string beside, string personal)
    {
        if (IsWritable(beside))
        {
            return beside;
        }

        return Directory.CreateDirectory(personal).FullName;
    }

    /// <summary>Writes and deletes a probe file: folder rights, read-only media and the like all count.</summary>
    private static bool IsWritable(string folder)
    {
        var probe = Path.Combine(folder, Probe);
        try
        {
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

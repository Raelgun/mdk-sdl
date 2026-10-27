using System.Text;
using Mdk.Formats;
using Mdk.Game.Menu;

namespace Mdk.Game.Tests;

/// <summary>The splash before the main menu (#22).</summary>
public class IntroSplashTests
{
    [Fact]
    public void ShortSplashIsSkipped()
    {
        var bni = Bni.Load(Archive("INTRO1A", new byte[100]));

        Assert.True(new IntroSplash(null!, bni).Done);
    }

    /// <summary>A BNI of one entry: u32 size, u32 count, char[12] name, u32 offset (from 4), data.</summary>
    private static string Archive(string name, byte[] data)
    {
        const int Header = 4 + 4 + 12 + 4;
        var bytes = new byte[Header + data.Length];
        BitConverter.TryWriteBytes(bytes.AsSpan(0), bytes.Length - 4);
        BitConverter.TryWriteBytes(bytes.AsSpan(4), 1);
        Encoding.ASCII.GetBytes(name).CopyTo(bytes, 8);
        BitConverter.TryWriteBytes(bytes.AsSpan(20), Header - 4);
        data.CopyTo(bytes, Header);

        var path = Path.GetTempFileName();
        File.WriteAllBytes(path, bytes);
        return path;
    }
}

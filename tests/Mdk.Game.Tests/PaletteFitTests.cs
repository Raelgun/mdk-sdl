using Mdk.Formats;
using Mdk.Game.HdTextures;
using Mdk.Game.Mods;

namespace Mdk.Game.Tests;

/// <summary>A mod's full-colour image fitted to a palette for the original look (the scope's wide frame).</summary>
public class PaletteFitTests
{
    private const int Channels = 4;
    private const byte Opaque = 255;

    /// <summary>A palette of magenta but for the colours given by index (red, green, black, white...).</summary>
    private static Palette Colours(params (int Index, byte R, byte G, byte B)[] entries)
    {
        var palette = new Palette();
        for (var i = 1; i < Palette.Size; i++)
        {
            (palette.Rgba[i * Channels], palette.Rgba[i * Channels + 2], palette.Rgba[i * Channels + 3]) = (Opaque, Opaque, Opaque);
        }

        foreach (var (index, r, g, b) in entries)
        {
            (palette.Rgba[index * Channels], palette.Rgba[index * Channels + 1], palette.Rgba[index * Channels + 2]) = (r, g, b);
        }

        return palette;
    }

    private static HdImage Image(int width, int height, params (byte R, byte G, byte B, byte A)[] pixels)
    {
        var rgba = new byte[width * height * Channels];
        for (var i = 0; i < width * height; i++)
        {
            var (r, g, b, a) = pixels[i % pixels.Length];
            (rgba[i * Channels], rgba[i * Channels + 1], rgba[i * Channels + 2], rgba[i * Channels + 3]) = (r, g, b, a);
        }

        return new HdImage(width, height, 1, rgba);
    }

    [Fact]
    public void PixelsOfAPaletteColourTakeItsIndex()
    {
        var palette = Colours((7, Opaque, 0, 0), (8, 0, Opaque, 0));
        var image = Image(2, 1, (Opaque, 0, 0, Opaque), (0, Opaque, 0, Opaque));
        var (width, height, indices) = PaletteFit.Fit(image, palette, 1);
        Assert.Equal((2, 1), (width, height));
        Assert.Equal(new byte[] { 7, 8 }, indices);
    }

    [Fact]
    public void TheWidthFollowsTheImagesProportions()
    {
        var palette = Colours((5, 0, 0, 0));
        var (width, height, indices) = PaletteFit.Fit(Image(4, 2, (0, 0, 0, Opaque)), palette, 4);
        Assert.Equal((8, 4), (width, height));
        Assert.Equal(32, indices.Length);
    }

    [Fact]
    public void OnlyTransparentPixelsAreIndexZero()
    {
        var palette = Colours((5, 0, 0, 0));
        var image = Image(2, 1, (0, 0, 0, Opaque), (0, 0, 0, 0));
        var (_, _, indices) = PaletteFit.Fit(image, palette, 1);
        Assert.Equal(5, indices[0]);
        Assert.Equal(0, indices[1]);
    }

    [Fact]
    public void AGreyBetweenTwoColoursIsDitheredIntoBoth()
    {
        var palette = Colours((1, 0, 0, 0), (2, Opaque, Opaque, Opaque));
        var (_, _, indices) = PaletteFit.Fit(Image(64, 64, (128, 128, 128, Opaque)), palette, 64);
        var white = indices.Count(i => i == 2) / (float)indices.Length;
        Assert.InRange(white, 0.4f, 0.6f);
        Assert.All(indices, i => Assert.Contains(i, new byte[] { 1, 2 }));
    }
}

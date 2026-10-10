using Mdk.Formats;
using Mdk.Game.HdTextures;

namespace Mdk.Game.Mods;

/// <summary>A mod's RGBA image fitted to a 256-colour palette, for the original look: that look draws
/// index textures through the palette, so a full-colour image has to become indices of its colours.
/// The image is resampled to the screen's own pixel grid (a given number of rows, as wide as its
/// proportions make it) and each pixel takes its nearest palette colour, the error spread to the next
/// pixels (Floyd-Steinberg) so that dark gradients don't band. Index 0 is transparent in the game, so
/// it is used for the transparent pixels only.</summary>
public static class PaletteFit
{
    private const int Channels = 4;
    private const int Rgb = 3;
    private const byte Opaque = 255;
    /// <summary>The colour search runs on 5 bits per channel (a 32768 entries table), not on each pixel.</summary>
    private const int LevelBits = 5;
    private const int Levels = 1 << LevelBits;
    private const int Shift = 8 - LevelBits;

    /// <summary>The image as palette indices, <paramref name="height"/> rows high.</summary>
    public static (int Width, int Height, byte[] Indices) Fit(HdImage image, Palette palette, int height)
    {
        var width = Math.Max(1, (int)MathF.Round(image.Width * (float)height / image.Height));
        var (colours, alpha) = Resample(image, width, height);
        var table = NearestTable(palette);
        var indices = new byte[width * height];

        // The error of the pixel before (this row) and of the row below, three channels each, with
        // a pixel of margin on both sides.
        var rows = new[] { new float[(width + 2) * Rgb], new float[(width + 2) * Rgb] };
        Span<float> value = stackalloc float[Rgb];
        for (var y = 0; y < height; y++)
        {
            var (current, next) = (rows[y & 1], rows[(y + 1) & 1]);
            Array.Clear(next);
            for (var x = 0; x < width; x++)
            {
                var p = y * width + x;
                if (alpha[p] < Opaque / 2)
                {
                    continue;
                }

                for (var c = 0; c < Rgb; c++)
                {
                    value[c] = Math.Clamp(colours[p * Rgb + c] + current[(x + 1) * Rgb + c], 0f, Opaque);
                }

                var index = table[((int)value[0] >> Shift << (2 * LevelBits)) | ((int)value[1] >> Shift << LevelBits) | ((int)value[2] >> Shift)];
                indices[p] = index;
                for (var c = 0; c < Rgb; c++)
                {
                    var error = value[c] - palette.Rgba[index * Channels + c];
                    current[(x + 2) * Rgb + c] += error * 7f / 16f;
                    next[x * Rgb + c] += error * 3f / 16f;
                    next[(x + 1) * Rgb + c] += error * 5f / 16f;
                    next[(x + 2) * Rgb + c] += error / 16f;
                }
            }
        }

        return (width, height, indices);
    }

    /// <summary>The image averaged down (or up) to <paramref name="width"/> x <paramref name="height"/>:
    /// straight colours and alpha. The image's colours are premultiplied (<see cref="ModImages"/>).</summary>
    private static (byte[] Colours, byte[] Alpha) Resample(HdImage image, int width, int height)
    {
        var colours = new byte[width * height * Rgb];
        var alpha = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            var y0 = (int)((long)y * image.Height / height);
            var y1 = Math.Max(y0 + 1, (int)((long)(y + 1) * image.Height / height));
            for (var x = 0; x < width; x++)
            {
                var x0 = (int)((long)x * image.Width / width);
                var x1 = Math.Max(x0 + 1, (int)((long)(x + 1) * image.Width / width));
                long r = 0, g = 0, b = 0, a = 0;
                for (var sy = y0; sy < y1; sy++)
                {
                    for (var sx = x0; sx < x1; sx++)
                    {
                        var i = (sy * image.Width + sx) * Channels;
                        r += image.Rgba[i];
                        g += image.Rgba[i + 1];
                        b += image.Rgba[i + 2];
                        a += image.Rgba[i + 3];
                    }
                }

                var count = (y1 - y0) * (x1 - x0);
                var p = y * width + x;
                alpha[p] = (byte)(a / count);
                if (a > 0)
                {
                    colours[p * Rgb] = (byte)Math.Min(r * Opaque / a, Opaque);
                    colours[p * Rgb + 1] = (byte)Math.Min(g * Opaque / a, Opaque);
                    colours[p * Rgb + 2] = (byte)Math.Min(b * Opaque / a, Opaque);
                }
            }
        }

        return (colours, alpha);
    }

    /// <summary>For each colour (5 bits a channel) its nearest palette index from 1 up: green weighs
    /// most, as the eye sees it best.</summary>
    private static byte[] NearestTable(Palette palette)
    {
        var table = new byte[Levels * Levels * Levels];
        var half = (1 << Shift) / 2;
        for (var r = 0; r < Levels; r++)
        {
            for (var g = 0; g < Levels; g++)
            {
                for (var b = 0; b < Levels; b++)
                {
                    int cr = (r << Shift) + half, cg = (g << Shift) + half, cb = (b << Shift) + half;
                    var best = 1;
                    var bestDistance = int.MaxValue;
                    for (var i = 1; i < Palette.Size; i++)
                    {
                        int dr = cr - palette.Rgba[i * Channels], dg = cg - palette.Rgba[i * Channels + 1], db = cb - palette.Rgba[i * Channels + 2];
                        var distance = 2 * dr * dr + 4 * dg * dg + 3 * db * db;
                        if (distance < bestDistance)
                        {
                            (best, bestDistance) = (i, distance);
                        }
                    }

                    table[(r << (2 * LevelBits)) | (g << LevelBits) | b] = (byte)best;
                }
            }
        }

        return table;
    }
}

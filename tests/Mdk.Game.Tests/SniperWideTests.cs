using Mdk.Game.Hud;

namespace Mdk.Game.Tests;

/// <summary>The scope's wide frame (a mod's <c>SNIPERS1_WIDE</c>): which part of it fills each side
/// of a window wider than the 4:3 screen.</summary>
public class SniperWideTests
{
    private const float Height = 360f;
    private const float ScreenWidth = 480f;

    private static float Left(float canvasWidth) => (canvasWidth - ScreenWidth) / 2f;

    [Fact]
    public void NoStripsOnAFourByThreeWindow()
    {
        Assert.Empty(SniperOverlay.WideStrips(ScreenWidth, 0f, 32f / 9f));
    }

    [Fact]
    public void AnImageAsWideAsTheWindowFillsBothSidesWhole()
    {
        var width = Height * 32f / 9f;
        var strips = SniperOverlay.WideStrips(width, Left(width), 32f / 9f);
        Assert.Equal(2, strips.Length);
        Assert.Equal(0f, strips[0].From, 3);
        Assert.Equal(Left(width), strips[0].To, 3);
        Assert.Equal(0f, strips[0].U0, 3);
        Assert.Equal(Left(width) / width, strips[0].U1, 3);
        Assert.Equal(width - Left(width), strips[1].From, 3);
        Assert.Equal(width, strips[1].To, 3);
        Assert.Equal(1f, strips[1].U1, 3);
    }

    [Fact]
    public void ANarrowerWindowCropsTheImageSymmetrically()
    {
        // 21:9 window on a 32:9 image: the image's middle fills it, the edges are cut.
        var width = Height * 21f / 9f;
        var strips = SniperOverlay.WideStrips(width, Left(width), 32f / 9f);
        var imageWidth = Height * 32f / 9f;
        var cut = (imageWidth - width) / 2f / imageWidth;
        Assert.Equal(cut, strips[0].U0, 3);
        Assert.Equal(1f - cut, strips[1].U1, 3);
        // The strips and the screen's own pixels are the same image: u runs on at the screen's edge.
        Assert.Equal(0.5f - ScreenWidth / 2f / imageWidth, strips[0].U1, 3);
        Assert.Equal(0.5f + ScreenWidth / 2f / imageWidth, strips[1].U0, 3);
    }

    [Fact]
    public void AWiderWindowThanTheImageLeavesBlackBeyondIt()
    {
        var width = Height * 32f / 9f;
        var strips = SniperOverlay.WideStrips(width, Left(width), 21f / 9f);
        var imageWidth = Height * 21f / 9f;
        Assert.Equal((width - imageWidth) / 2f, strips[0].From, 3);
        Assert.Equal(0f, strips[0].U0, 3);
        Assert.Equal(width - (width - imageWidth) / 2f, strips[1].To, 3);
        Assert.Equal(1f, strips[1].U1, 3);
    }
}

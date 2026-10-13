using System.Numerics;
using Mdk.Game.Kurt;

namespace Mdk.Game.Tests;

/// <summary>Kurt's sprite: the rows at and below his feet (the shadow) drawn in front of the floor.</summary>
public class KurtSpriteTests
{
    private static readonly Vector3 Feet = new(100f, 200f, 50f);

    [Theory]
    [InlineData(0f)]
    [InlineData(0.5f)]
    public void ShadowStaysAboveTheFloor(float bump)
    {
        // The follow camera: 8 behind, 6 above; the shadow's bottom row 0.6 below the feet, on
        // their plane; the floor there flat or a little higher than the feet.
        var eye = Feet + new Vector3(0f, -8f, 6f);
        var bottom = Feet + new Vector3(0f, 0f, -0.6f);

        var scale = KurtSprite.Nearer(Vector3.Distance(eye, Feet with { Z = eye.Z }));
        var moved = eye + (bottom - eye) * scale;

        Assert.True(moved.Z > Feet.Z + bump, $"{moved.Z - Feet.Z} under the floor at {bump}");
    }

    [Fact]
    public void CloseUpStaysInFront() => Assert.InRange(KurtSprite.Nearer(1f), 0.1f, 1f);
}

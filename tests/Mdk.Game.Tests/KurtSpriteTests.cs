using System.Numerics;
using Mdk.Game.Kurt;

namespace Mdk.Game.Tests;

/// <summary>Kurt's sprite: the rows at and below his feet (the shadow) drawn in front of the floor.</summary>
public class KurtSpriteTests
{
    private static readonly Vector3 Feet = new(100f, 200f, 50f);

    /// <summary>The follow camera: 8 behind, 6 above.</summary>
    private static readonly Vector3 Eye = Feet + new Vector3(0f, -8f, 6f);

    /// <summary>The shadow's bottom row, 0.6 below the feet on the sprite's plane (facing the eye).</summary>
    private static readonly Vector3 Bottom = Feet - SpriteUp * 0.6f;

    [Theory]
    [InlineData(0f)]
    [InlineData(0.5f)]
    public void ShadowStaysAboveTheFloor(float bump)
    {
        var moved = Moved(KurtSprite.Nearer(Depth, null));

        Assert.True(moved.Z > Feet.Z + bump, $"{moved.Z - Feet.Z} under the floor at {bump}");
    }

    [Fact]
    public void ShadowStaysAboveADownhill()
    {
        // The floor falls away from the camera at 30°: higher than the feet between them.
        var slope = MathF.Tan(float.DegreesToRadians(30f));
        float FloorAt(float y) => Feet.Z + (Feet.Y - y) * slope;
        var direction = Bottom - Eye;
        var t = (FloorAt(Eye.Y) - Eye.Z) / (direction.Z + direction.Y * slope);
        var floorDepth = Vector3.Dot(direction * t, Forward);

        var moved = Moved(KurtSprite.Nearer(Depth, floorDepth));

        Assert.True(moved.Z > FloorAt(moved.Y), $"{FloorAt(moved.Y) - moved.Z} under the slope");
    }

    [Fact]
    public void CloseUpStaysInFront() => Assert.InRange(KurtSprite.Nearer(1f, null), 0.1f, 1f);

    private static Vector3 Forward => Vector3.Normalize(Feet - Eye);

    private static Vector3 SpriteUp => Vector3.Normalize(Vector3.UnitZ - Forward * Forward.Z);

    private static float Depth => Vector3.Dot(Feet - Eye, Forward);

    private static Vector3 Moved(float scale) => Eye + (Bottom - Eye) * scale;
}

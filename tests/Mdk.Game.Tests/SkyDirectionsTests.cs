using System.Numerics;
using Mdk.Engine.Render;
using Mdk.Game.Kurt;

namespace Mdk.Game.Tests;

/// <summary>The sky's clip-to-direction matrix (<see cref="ViewCamera.SkyDirections"/>) is built from the
/// camera's axes: the same directions as inverting rotation × projection, without that inverse's
/// float32 error, which changed with the camera's angle and shook the sky when turning.</summary>
public class SkyDirectionsTests
{
    private const float Near = 0.5f;
    private const float Far = 20000f;

    public static TheoryData<float, float, float> Cameras => new()
    {
        { 0f, -10f, 16f / 9f },
        { 37.5f, 4f, 16f / 9f },
        { 123.4f, -25f, 32f / 9f },
        { 271f, 12f, 4f / 3f },
    };

    private static View ViewAt(float yaw, float pitch, float aspect, bool scope)
    {
        var (yawSin, yawCos) = MathF.SinCos(float.DegreesToRadians(yaw));
        var (pitchSin, pitchCos) = MathF.SinCos(float.DegreesToRadians(pitch));
        var forward = new Vector3(yawCos * pitchCos, yawSin * pitchCos, pitchSin);
        return scope
            ? CameraMath.ScopeView(Vector3.Zero, forward, 0.5f, aspect, Near, Far)
            : CameraMath.View(Vector3.Zero, forward, FollowCamera.UpOf(forward), 60f, aspect, Near, Far);
    }

    private static Vector3 Direction(Matrix4x4 clipToDirection, float x, float y)
    {
        var p = Vector4.Transform(new Vector4(x, y, 1f, 1f), clipToDirection);
        return Vector3.Normalize(new Vector3(p.X, p.Y, p.Z) / p.W);
    }

    /// <summary>A pixel's direction as sky.hlsl gets it: the matrix gives the screen triangle's corners
    /// ((-1, -1), (3, -1), (-1, 3), at clip depth 1, divided by w), the GPU blends them linearly to the
    /// pixel, and the pixel normalizes the blend. It's right only if the corners lie in one plane.</summary>
    private static Vector3 ShaderDirection(Matrix4x4 clipToDirection, float x, float y)
    {
        Vector3 Corner(float cx, float cy)
        {
            var p = Vector4.Transform(new Vector4(cx, cy, 1f, 1f), clipToDirection);
            return new Vector3(p.X, p.Y, p.Z) / p.W;
        }

        float b1 = (x + 1f) / 4f, b2 = (y + 1f) / 4f;
        return Vector3.Normalize(Corner(-1f, -1f) * (1f - b1 - b2) + Corner(3f, -1f) * b1 + Corner(-1f, 3f) * b2);
    }

    /// <summary>The exact direction through clip point (x, y), in doubles.</summary>
    private static (double X, double Y, double Z) Exact(ViewCamera camera, double x, double y)
    {
        var f = Vector3.Normalize(camera.Forward);
        var r = Vector3.Normalize(Vector3.Cross(f, camera.Up));
        var u = Vector3.Cross(r, f);
        var p = camera.Projection;
        double a = (x + p.M31) / p.M11, b = (y + p.M32) / p.M22;
        var (dx, dy, dz) = (f.X + r.X * a + u.X * b, f.Y + r.Y * a + u.Y * b, f.Z + r.Z * a + u.Z * b);
        var length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        return (dx / length, dy / length, dz / length);
    }

    /// <summary>How far a unit direction is from the exact one (the chord: the angle, for small ones;
    /// acos of the dot product loses its precision there).</summary>
    private static double Angle(Vector3 d, (double X, double Y, double Z) exact) =>
        Math.Sqrt((d.X - exact.X) * (d.X - exact.X) + (d.Y - exact.Y) * (d.Y - exact.Y) + (d.Z - exact.Z) * (d.Z - exact.Z));

    [Theory]
    [MemberData(nameof(Cameras))]
    public void SkyDirectionsAreTheInversesDirections(float yaw, float pitch, float aspect)
    {
        foreach (var scope in (bool[])[false, true])
        {
            var view = ViewAt(yaw, pitch, aspect, scope);
            var sky = view.Camera!.Value.SkyDirections();
            for (var x = -1f; x <= 1f; x += 0.5f)
            {
                for (var y = -1f; y <= 1f; y += 0.5f)
                {
                    var ours = Direction(sky, x, y);
                    var inverse = Direction(view.ClipToDirection, x, y);
                    Assert.True(Vector3.Distance(ours, inverse) < 1e-3f, $"({x}, {y}): {ours} vs {inverse}");
                }
            }
        }
    }

    [Fact]
    public void SkyDirectionsStaySteadyAllTheWayRound()
    {
        // Turning a full circle (32:9, looking 4 degrees up), as sky.hlsl draws the directions: ours
        // stay within float rounding everywhere. The inverse's corners leave their plane at some
        // angles (by 0.002 rad at yaw 37.5: about 3 pixels of a 1440-high view), so the sky shook.
        const float Pitch = 4f;
        const float Aspect = 32f / 9f;
        double ours = 0.0, inverse = 0.0;
        for (var yaw = 0f; yaw < 360f; yaw += 0.5f)
        {
            var view = ViewAt(yaw, Pitch, Aspect, scope: false);
            var camera = view.Camera!.Value;
            var sky = camera.SkyDirections();
            for (var x = -1f; x <= 1f; x += 0.5f)
            {
                for (var y = -1f; y <= 1f; y += 0.5f)
                {
                    var exact = Exact(camera, x, y);
                    ours = Math.Max(ours, Angle(ShaderDirection(sky, x, y), exact));
                    inverse = Math.Max(inverse, Angle(ShaderDirection(view.ClipToDirection, x, y), exact));
                }
            }
        }

        Assert.True(ours < 1e-5, $"ours {ours}");
        Assert.True(inverse > 1e-4, $"inverse {inverse}");
    }
}
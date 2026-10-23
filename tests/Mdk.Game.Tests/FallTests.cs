using System.Numerics;
using Mdk.Formats;
using Mdk.Game.Fall;
using Mdk.Game.Kurt;

namespace Mdk.Game.Tests;

/// <summary>The fall before a level (fall.gd, fall_missile.gd): steering, the camera, the ground's
/// mapping and track, the missiles, the pickups and the end.</summary>
public class FallTests
{
    private const float Step = 1f / 60f;
    private static readonly FallSim.Animation Loop = new(1f, 30);
    private static readonly string[] FirstPickups = ["SW_HOME", "SW_GATT", "SW_HOME"];

    private static FallSim NewFall(int index = 0, Difficulty difficulty = Difficulty.Normal, int seed = 1) =>
        new(new FallSim.Setup(index, difficulty, FirstPickups, Loop, Loop, Loop), new Random(seed));

    /// <summary>Runs the sim for <paramref name="seconds"/>, steering by <paramref name="steer"/>.</summary>
    private static void Run(FallSim sim, float seconds, Func<FallSim, Vector2>? steer = null)
    {
        for (var t = 0f; t < seconds && sim.State == FallSim.Outcome.Running; t += Step)
        {
            sim.Update(Step, steer?.Invoke(sim) ?? Vector2.Zero);
        }
    }

    [Fact]
    public void SteeringAcceleratesBrakesAndTurnsBack()
    {
        // A key: a step a tick up to the maximum.
        Assert.Equal(FallSim.SteerStep, FallSim.Steer(0f, 1, 1f / FallSim.TicksPerSecond), 3);
        Assert.Equal(FallSim.SteerMax, FallSim.Steer(FallSim.SteerMax, 1, 1f), 3);

        // The opposite key resets the speed to one step; no key brakes to 0.
        Assert.Equal(-FallSim.SteerStep, FallSim.Steer(50f, -1, Step), 3);
        Assert.Equal(0f, FallSim.Steer(3f, 0, 1f / FallSim.TicksPerSecond));
        Assert.Equal(50f - FallSim.SteerStep, FallSim.Steer(50f, 0, 1f / FallSim.TicksPerSecond), 3);
    }

    [Theory]
    // Personal tweak: spreads 2 tighter than the original's (FallSim.SpreadBase), not below 0.
    [InlineData(0, Difficulty.Normal, 117.65f, 2, 4.5f, 32, 63)]
    [InlineData(4, Difficulty.Easy, 164.71f, 2, 1.5f, 28, 51)]
    [InlineData(4, Difficulty.Hard, 274.51f, 4, 0f, 12, 27)]
    public void DifficultyAsGodot(int index, Difficulty difficulty, float beam, int missiles, float spread, int missileGap, int radarGap)
    {
        var sim = NewFall(index, difficulty);
        Assert.Equal(beam, sim.BeamSpeed, 2);
        Assert.Equal(missiles, sim.MissilesPerDetection);
        Assert.Equal(spread, sim.Spread, 3);
        Assert.Equal(missileGap, sim.MissileGap);
        Assert.Equal(radarGap, sim.RadarGap);
    }

    [Fact]
    public void IntroLastsFiveSecondsThenKurtFalls()
    {
        var sim = NewFall();
        Run(sim, 4.9f);
        Assert.True(sim.InIntro);
        Assert.True(sim.KurtVisible);

        Run(sim, 0.2f);
        Assert.False(sim.InIntro);
        Run(sim, 1f);
        Assert.Equal(FallSim.KurtStartZ - FallSim.KurtSpeed * sim.Time, sim.KurtPosition.Z, 0);
        Assert.Equal(sim.KurtPosition.Z + 10f, sim.CameraPosition.Z, 1);
    }

    [Fact]
    public void KurtStaysWithinLimits()
    {
        var sim = NewFall();
        Run(sim, 5f + 3f, _ => new Vector2(-1f, 1f));
        Assert.Equal(-FallSim.Limit.X, sim.KurtPosition.X, 3);
        Assert.Equal(FallSim.Limit.Y, sim.KurtPosition.Y, 3);
    }

    [Fact]
    public void EndsAt33SecondsPulledBackWithTheCameraStopped()
    {
        var sim = NewFall(seed: 3);
        Run(sim, 5f + 29f, _ => Vector2.One);
        Run(sim, 10f);
        if (sim.State == FallSim.Outcome.GameOver)
        {
            return;
        }

        Assert.Equal(FallSim.Outcome.Landed, sim.State);
        Assert.InRange(sim.Time, FallSim.EndTime, FallSim.EndTime + 0.1f);
        Assert.InRange(sim.KurtPosition.X, -0.2f, 0.2f);
        Assert.Equal(3213.7f, sim.CameraPosition.Z, 0);
        Assert.Equal(0, sim.Wind);
    }

    [Fact]
    public void EscSkips()
    {
        var sim = NewFall();
        Run(sim, 1f);
        sim.Skip();
        Assert.Equal(FallSim.Outcome.Landed, sim.State);
    }

    [Fact]
    public void SteeringToThePickupsTakesThem()
    {
        var sim = NewFall();

        // Kurt steers over the next pickup rising to him (under its chute, below him).
        static Vector2 Towards(FallSim sim)
        {
            var below = sim.Pickups.Where(p => p.Chute && p.Position.Z < sim.KurtPosition.Z).ToList();
            if (below.Count == 0)
            {
                return Vector2.Zero;
            }

            var target = below.MaxBy(p => p.Position.Z)!.Position;
            var d = new Vector2(target.X - sim.KurtPosition.X, target.Y - sim.KurtPosition.Y);
            return new Vector2(MathF.Abs(d.X) < 1f ? 0f : MathF.Sign(d.X), MathF.Abs(d.Y) < 1f ? 0f : MathF.Sign(d.Y));
        }

        Run(sim, 5f + 29f, Towards);
        Assert.NotEmpty(sim.Collected);
        Assert.All(sim.Collected, name => Assert.Contains(name, FirstPickups));
        Assert.NotEmpty(sim.Inventory.Slots);
    }

    [Fact]
    public void BoxCrossings()
    {
        var kurt = new Vector3(0f, 0f, 100f);
        Assert.True(FallSim.CrossesBox(kurt, new Vector3(0f, 0f, 90f), new Vector3(1f, 1f, 102f)));
        Assert.True(FallSim.CrossesBox(kurt, new Vector3(0f, 0f, 90f), new Vector3(0f, 0f, 110f)));
        Assert.False(FallSim.CrossesBox(kurt, new Vector3(5f, 0f, 90f), new Vector3(5f, 0f, 110f)));
        Assert.False(FallSim.CrossesBox(kurt, new Vector3(0f, 0f, 80f), new Vector3(0f, 0f, 94f)));
    }

    [Fact]
    public void MissileFliesFreeThenHomesThenPasses()
    {
        var missile = new FallMissile(0f, new Random(1));
        var kurt = new Vector3(0f, 0f, 300f);
        var events = new List<FallMissile.Event>();
        for (var tick = 0; tick < 600 && !events.Contains(FallMissile.Event.Gone); tick++)
        {
            var happened = missile.Update(1f / FallSim.TicksPerSecond, 1, kurt);
            if (happened != FallMissile.Event.None)
            {
                events.Add(happened);
            }
        }

        Assert.Equal([FallMissile.Event.Passed, FallMissile.Event.Gone], events);
        Assert.Equal(FallMissile.TrailPoints, missile.Trail.Count);
    }

    [Fact]
    public void MissilesHomeAsFastAt120FpsAsAt30()
    {
        // The homing blends once per 1/30 s tick (0.8 v + 0.2 aim): frames between ticks don't stiffen it.
        // Free until tick 59, then 3 ticks of homing: mid-turn, where per-frame blending turned the
        // 120 fps missile about 76 degrees off the 30 fps one (about 12 with the blend per tick).
        const int Ticks = 62;
        const int Frames = 4;
        const float MaxDegrees = 20f;
        var kurt = new Vector3(0f, 0f, 2000f);
        var atTicks = new FallMissile(0f, new Random(1));
        var atFrames = new FallMissile(0f, new Random(1));
        for (var tick = 0; tick < Ticks; tick++)
        {
            atTicks.Update(1f / FallSim.TicksPerSecond, 1, kurt);
            for (var frame = 0; frame < Frames; frame++)
            {
                atFrames.Update(1f / (FallSim.TicksPerSecond * Frames), frame == 0 ? 1 : 0, kurt);
            }
        }

        var cos = Vector3.Dot(Vector3.Normalize(atTicks.Velocity), Vector3.Normalize(atFrames.Velocity));
        Assert.True(float.RadiansToDegrees(MathF.Acos(Math.Clamp(cos, -1f, 1f))) < MaxDegrees);
    }

    [Fact]
    public void AHitExplodesOnKurt()
    {
        // The explosion goes off at Kurt's position, not where the missile met his box (up to 4
        // units to his side), and follows him down.
        var sim = NewFall();
        Run(sim, 5f);
        while (sim.Explosions.Count == 0 && sim.Time < FallSim.SteerTime)
        {
            sim.Update(Step, Vector2.Zero);
        }

        var explosion = Assert.Single(sim.Explosions);
        Assert.Equal(sim.KurtPosition, explosion.Position);
    }

    [Fact]
    public void RadarBeamReachesItsSpot()
    {
        // Rings 1-3 halve the way to the spot (0x412af8), ring 4 is the spot itself.
        var (basePoint, spot) = (new Vector3(-40f, 0f, 0f), new Vector3(0f, 0f, 400f));
        Assert.Equal((new Vector3(-20f, 0f, 200f), 5f), FallView.RadarRing(basePoint, spot, 10f, 0));
        Assert.Equal((new Vector3(-5f, 0f, 350f), 8.75f), FallView.RadarRing(basePoint, spot, 10f, 2));
        Assert.Equal((spot, 10f), FallView.RadarRing(basePoint, spot, 10f, 3));
    }

    [Fact]
    public void ModelsAtTheCameraAreNotDrawn()
    {
        // A missile past Kurt (5 units above him, 5 below the camera) would fill the view.
        var camera = new Vector3(0f, 0f, 1000f);
        Assert.False(FallView.NearCamera(new Vector3(0f, 0f, 990f), camera));
        Assert.True(FallView.NearCamera(new Vector3(0f, 0f, 996f), camera));
        Assert.True(FallView.NearCamera(new Vector3(0f, 0f, 1003f), camera));
    }

    [Theory]
    [InlineData(0, 16)]
    [InlineData(3, 18)]
    [InlineData(23, 31)]
    [InlineData(24, 32)]
    public void TrackWidens(int row, int width) => Assert.Equal(width, FallGround.TrackWidth(row));

    [Fact]
    public void GroundMapping()
    {
        // At the start a texel is a pixel: the 480-wide view shows columns 272-752 around row 824.
        var (screen, texels) = FallGround.Visible(new Vector3(0f, 0f, FallGround.UnitHeight), FallGround.CentreRow(0f), 480f);
        Assert.Equal(new System.Drawing.RectangleF(0f, 0f, 480f, 360f), screen);
        Assert.Equal(new System.Drawing.RectangleF(272f, 644f, 480f, 360f), texels);
        Assert.Equal(200f, FallGround.CentreRow(FallSim.EndTime), 3);
    }

    [Theory]
    [InlineData(0, 1f)]
    [InlineData(1, 1.25f)]
    [InlineData(2, 1.2f)]
    [InlineData(5, 1f)]
    [InlineData(19, 0.5f)]
    [InlineData(31, 1f / 26f)]
    public void TrailsSwellBehindTheMissileThenTaper(int age, float width) =>
        // The original's trail (0x439454): its widths by a point's age in ticks (0x491d64, then linear).
        Assert.Equal(width, FallView.TrailWidth(age), 4);

    [Fact]
    public void TrailsTurnYellowAndOpaqueOverTheirLastEightSegments()
    {
        // 0x410874's translucent table: white at 215, then blue 255 down to 31, alpha up to 250.
        const int Count = 32;
        Assert.Equal(Engine.Render.Vertex.Rgba(255, 255, 255, 215), FallView.TrailColour(1, Count));
        Assert.Equal(Engine.Render.Vertex.Rgba(255, 255, 255, 215), FallView.TrailColour(23, Count));
        Assert.Equal(Engine.Render.Vertex.Rgba(255, 255, 255, 215), FallView.TrailColour(24, Count));
        Assert.Equal(Engine.Render.Vertex.Rgba(255, 255, 223, 220), FallView.TrailColour(25, Count));
        Assert.Equal(Engine.Render.Vertex.Rgba(255, 255, 31, 250), FallView.TrailColour(31, Count));
    }

    [Fact]
    public void WideGroundCoversA32By9ViewFromTheStart()
    {
        // Personal tweak: 32:9 is 1280 wide; the original leaves 128 pixels each side, a 1536-wide
        // ground (the original centred) covers it all, the same texels in the middle.
        var camera = new Vector3(0f, 0f, FallGround.UnitHeight);
        var (original, _) = FallGround.Visible(camera, FallGround.CentreRow(0f), 1280f);
        Assert.Equal(new System.Drawing.RectangleF(128f, 0f, 1024f, 360f), original);
        var (screen, texels) = FallGround.Visible(camera, FallGround.CentreRow(0f), 1280f, 1536);
        Assert.Equal(new System.Drawing.RectangleF(0f, 0f, 1280f, 360f), screen);
        Assert.Equal(new System.Drawing.RectangleF(128f, 644f, 1280f, 360f), texels);
    }

    [Fact]
    public void WideGroundGetsTheTrackInItsMiddle()
    {
        const int Wide = 1536;
        const byte Track = 9;
        var ground = new Texture { Width = Wide, Height = FallGround.Size, Indices = new byte[Wide * FallGround.Size] };
        var pod = new Texture { Width = 128, Height = FallGround.Size, Indices = Enumerable.Repeat(Track, 128 * FallGround.Size).ToArray() };
        var fall = new FallGround(ground, pod);
        Assert.True(fall.UpdateTrack(FallGround.CentreRow(0f)));

        // The track's first row: 16 texels each side of the middle column (768).
        var row = (int)MathF.Round(FallGround.CentreRow(0f) - 108f * 80f / 256f);
        Assert.Equal(Track, fall.Indices[row * Wide + 768 - 16]);
        Assert.Equal(Track, fall.Indices[row * Wide + 768 + 15]);
        Assert.Equal(0, fall.Indices[row * Wide + 768 - 17]);
        Assert.Equal(0, fall.Indices[row * Wide + 768 + 16]);
    }
}

using System.Numerics;
using Mdk.Engine.Audio;
using Mdk.Engine.Platform;
using Mdk.Game.Audio;
using Mdk.Game.Collision;
using Mdk.Game.Kurt;
using Mdk.Game.Objects;
using Mdk.Game.Scripts;

namespace Mdk.Game.Tests;

/// <summary>Frames between the fixed steps (issue #8: screens above 60 Hz looked 60 Hz): Kurt,
/// the objects and the camera drawn between their last two states, the mouse turning at once.</summary>
public class FrameBlendTests
{
    private const float Step = Viewer.Step;
    private const float Half = 0.5f;
    private const float Precision = 4;
    /// <summary>LEVEL6's lift moves this much a tick at full speed.</summary>
    private const float LiftMove = 25f;

    private static readonly AudioDevice Device = new(Output.Muted);

    private static Kurt.Kurt NewKurt() => new(new ArenaSpace(), new SoundMixer(Device, _ => null), _ => 1) { OnFloor = true };

    [Fact]
    public void LerpEndsExactlyAtTheLatest()
    {
        var from = new Vector3(0.1f, 0.2f, 0.3f);
        var to = new Vector3(1e6f, -3.3f, 7.7f);
        Assert.Equal(to, FrameBlend.Lerp(from, to, 1f));
        Assert.Equal(new Vector3(5f, 0f, 0f), FrameBlend.Lerp(Vector3.Zero, new Vector3(10f, 0f, 0f), Half));
    }

    [Fact]
    public void AnglesTurnTheShortWay()
    {
        Assert.Equal(360f, FrameBlend.LerpAngle(350f, 10f, Half), Precision);
        Assert.Equal(180f, FrameBlend.LerpAngle(170f, 190f, Half), Precision);
        Assert.Equal(-45f, FrameBlend.LerpAngle(0f, 270f, Half), Precision);
    }

    [Fact]
    public void TeleportsAreJumps()
    {
        Assert.False(FrameBlend.Jumped(Vector3.Zero, new Vector3(4f, 0f, 0f), Step));
        Assert.True(FrameBlend.Jumped(Vector3.Zero, new Vector3(500f, 0f, 0f), Step));
    }

    [Fact]
    public void KurtIsDrawnBetweenSteps()
    {
        var kurt = NewKurt();
        var blend = new FrameBlend(Blending.On);
        blend.Record(kurt, null, null, 0);
        kurt.Feet = new Vector3(1f, 0f, 0f);
        kurt.Yaw = 10f;
        blend.Record(kurt, null, null, 0);

        blend.Update(kurt, Half, 1f);
        Assert.Equal(new Vector3(Half, 0f, 0f), blend.Feet);
        Assert.Equal(5f, blend.Yaw, Precision);

        blend.Update(kurt, 1f, 1f);
        Assert.Equal(kurt.Feet, blend.Feet);
    }

    [Fact]
    public void TeleportsAreDrawnWhereTheyEnd()
    {
        var kurt = NewKurt();
        var blend = new FrameBlend(Blending.On);
        blend.Record(kurt, null, null, 0);
        kurt.Teleport(new Vector3(2f, 0f, 0f), 0f);
        blend.Record(kurt, null, null, 0);

        blend.Update(kurt, Half, 1f);
        Assert.Equal(kurt.Feet, blend.Feet);
    }

    [Fact]
    public void TestsDrawTheStepsAsTheyAre()
    {
        var kurt = NewKurt();
        var blend = new FrameBlend(Blending.Off);
        blend.Record(kurt, null, null, 0);
        kurt.Feet = new Vector3(1f, 0f, 0f);
        blend.Record(kurt, null, null, 0);

        blend.Update(kurt, Half, Half);
        var obj = new MdkObject { Position = new Vector3(3f, 0f, 0f) };
        obj.Remember();
        obj.Position = new Vector3(5f, 0f, 0f);
        Assert.Equal(kurt.Feet, blend.Feet);
        Assert.Equal(obj.Transform, blend.TransformOf(obj));
    }

    [Fact]
    public void ObjectsAreDrawnBetweenTicks()
    {
        var obj = new MdkObject { Position = Vector3.Zero, Yaw = 0f, Arena = "A" };
        Assert.Equal(obj.Transform, obj.TransformAt(Half));

        obj.Remember();
        obj.Position = new Vector3(10f, 0f, 0f);
        obj.Yaw = 90f;
        Assert.Equal(new Vector3(5f, 0f, 0f), obj.PositionAt(Half));
        Assert.Equal(45f, obj.YawAt(Half), Precision);
        Assert.Equal(obj.Transform, obj.TransformAt(1f));

        // Into another arena: where it is.
        obj.Arena = "B";
        Assert.Equal(obj.Position, obj.PositionAt(Half));
    }

    [Fact]
    public void KurtOnAPlatformMovesWithIt()
    {
        // The platform moves 10 at the tick; Kurt, carried at the tick, is drawn where the platform is.
        var kurt = NewKurt();
        var lift = new MdkObject { Arena = "A" };
        var blend = new FrameBlend(Blending.On);
        blend.Record(kurt, lift, null, 0);
        lift.Remember();
        lift.Position = new Vector3(0f, 0f, 10f);
        kurt.Feet = lift.Position;
        blend.Record(kurt, lift, null, 1);

        blend.Update(kurt, Half, Half / 2f);
        Assert.Equal(lift.PositionAt(Half / 2f), blend.Feet);
    }

    [Fact]
    public void KurtLandingOnALiftIsDrawnOnIt()
    {
        // LEVEL6's lift (25 a tick): Kurt falls onto it as it moves; the step he lands on it
        // starts where the last frame drew him, not 25 further (--fps=240 --trace: a jitter).
        var kurt = NewKurt();
        kurt.Feet = new Vector3(0f, 0f, 2f);
        var lift = new MdkObject { Arena = "A" };
        var blend = new FrameBlend(Blending.On);
        blend.Record(kurt, null, null, 0);
        blend.Record(kurt, null, null, 0);

        lift.Remember();
        lift.Position = new Vector3(0f, -LiftMove, 0f);
        kurt.Feet = lift.Position;
        blend.Record(kurt, lift, null, 1);
        blend.Update(kurt, 0f, 0f);
        Assert.Equal(new Vector3(0f, 0f, 2f), blend.Feet);

        // A step without a tick where he isn't on it (his fall finds it a step later): still
        // drawn with it.
        blend.Record(kurt, null, null, 1);
        blend.Update(kurt, Half, Half);
        Assert.Equal(lift.PositionAt(Half), blend.Feet);
    }

    [Fact]
    public void WhatFollowsKurtDoesNotCarryHim()
    {
        // LEVEL4's board: Kurt stands on it, it follows him at the tick. He stood still: drawn so,
        // not moved with it (--fps=240 --trace: 0.3 back and forth as he boards).
        var kurt = NewKurt();
        kurt.Feet = Vector3.UnitX;
        var board = new MdkObject { Arena = "A" };
        var blend = new FrameBlend(Blending.On);
        blend.Record(kurt, board, board, 0);
        board.Remember();
        board.Position = kurt.Feet;
        blend.Record(kurt, board, board, 1);

        blend.Update(kurt, Half, Half / 2f);
        Assert.Equal(kurt.Feet, blend.Feet);
    }

    [Fact]
    public void TheMouseTurnsKurtEveryFrame()
    {
        var kurt = NewKurt();
        var input = new Input();
        kurt.Update(input, Step);
        var yaw = kurt.Yaw;

        input.AddMouseMotion(10f, 0f);
        var turn = kurt.MouseLook(input);
        Assert.NotEqual(0f, turn);
        Assert.Equal(yaw + turn, kurt.Yaw);
        Assert.Equal(0f, input.MouseX);

        // The step after doesn't turn him again.
        kurt.Update(input, Step);
        Assert.Equal(yaw + turn, kurt.Yaw, Precision);
    }

    [Fact]
    public void TheCameraIsPlacedForKurtAsDrawn()
    {
        // At the last step's place and pitch the frame's camera is the step's.
        var kurt = NewKurt();
        var camera = new FollowCamera();
        var input = new Input();
        camera.Update(kurt, 4f, input, Step);
        camera.Update(kurt, 4f, input, Step);
        var (position, forward) = (camera.Position, camera.Forward);

        camera.Blend(kurt, kurt.Feet, kurt.Yaw, 1f);
        Assert.Equal(position.X, camera.Position.X, Precision);
        Assert.Equal(position.Y, camera.Position.Y, Precision);
        Assert.Equal(position.Z, camera.Position.Z, Precision);
        Assert.Equal(forward.Z, camera.Forward.Z, Precision);

        // Kurt drawn 1 further: so is the camera.
        camera.Blend(kurt, kurt.Feet + Vector3.UnitX, kurt.Yaw, 1f);
        Assert.Equal(position.X + 1f, camera.Position.X, Precision);
    }

    [Fact]
    public void TheScopeIsDrawnBetweenSteps()
    {
        // Issue #16: the zoom and the clip moved at 60 Hz. The mouse's tilt since the step shows.
        var kurt = NewKurt();
        kurt.EnterSniper();
        var scope = kurt.Scope;
        scope.ClipTime = 2f;
        var blend = new FrameBlend(Blending.On);
        blend.Record(kurt, null, null, 0);
        (scope.Zoom, scope.Pitch, scope.ClipTime) = (Half, 10f, 1f);
        blend.Record(kurt, null, null, 0);
        scope.Pitch += 2f;

        blend.Update(kurt, Half, 1f);
        Assert.Equal(0.75f, blend.Zoom, Precision);
        Assert.Equal(7f, blend.ScopePitch, Precision);
        Assert.Equal(1.5f, blend.ClipTime, Precision);

        // A shot or a reload refills the clip at once.
        scope.ClipTime = 3f;
        blend.Record(kurt, null, null, 0);
        blend.Update(kurt, Half, 1f);
        Assert.Equal(3f, blend.ClipTime);
    }

    [Fact]
    public void TheScopeOpensAtOnce()
    {
        // The last time's zoom isn't drawn on the way to the new one.
        var kurt = NewKurt();
        kurt.Scope.Zoom = Scope.ZoomMin;
        var blend = new FrameBlend(Blending.On);
        blend.Record(kurt, null, null, 0);
        kurt.EnterSniper();
        blend.Record(kurt, null, null, 0);

        blend.Update(kurt, Half, 1f);
        Assert.Equal(1f, blend.Zoom);
    }

    [Fact]
    public void PiecesAreDrawnBetweenTicks()
    {
        // Issue #16: sparks and pieces flew at 30 Hz. A new one is drawn where it is.
        var debris = new Debris(new Random(1));
        debris.Spark("A", Vector3.Zero, 1, 1f, 3, 3);
        var piece = debris.Pieces[0];
        Assert.Equal(piece.Center, piece.CenterAt(Half));
        Assert.Equal(piece.Orientation, piece.OrientationAt(Half));

        var (center, orientation) = (piece.Center, piece.Orientation);
        debris.Update(1f);
        Assert.Equal(Vector3.Lerp(center, piece.Center, Half), piece.CenterAt(Half));
        Assert.Equal(Quaternion.Slerp(orientation, piece.Orientation, Half), piece.OrientationAt(Half));
        Assert.Equal(piece.Center, piece.CenterAt(1f));
    }

    [Fact]
    public void EffectsAreDrawnBetweenTicks()
    {
        // Issue #16: slime drops flew at 30 Hz. A new one is drawn where it is.
        var effects = new Effects(new Random(1)) { FrameCount = (_, _) => 4 };
        effects.SpawnDrop("A", Vector3.Zero, Vector3.UnitX, 1f);
        var drop = effects.All[0];
        Assert.Equal(drop.Position, drop.PositionAt(Half));

        effects.Update(1f);
        Assert.Equal(new Vector3(Half, 0f, 0f), drop.PositionAt(Half));
        Assert.Equal(drop.Position, drop.PositionAt(1f));
    }
}

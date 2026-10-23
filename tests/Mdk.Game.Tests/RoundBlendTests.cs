using System.Numerics;
using Mdk.Engine.Audio;
using Mdk.Formats;
using Mdk.Game.Audio;
using Mdk.Game.Collision;
using Mdk.Game.Level;
using Mdk.Game.Scripts;

namespace Mdk.Game.Tests;

/// <summary>Issue #16: sniper rounds and their cameras (the views at the top of the scope) moved
/// at 30 Hz. A frame draws them between the last tick and this one; a new round is drawn where it is.</summary>
public class RoundBlendTests
{
    private const int Level = 3;
    /// <summary>High above the arenas: the round hits nothing.</summary>
    private static readonly Vector3 Eye = new(0f, 0f, 20000f);
    private const float Half = 0.5f;
    private const float Precision = 1e-3f;

    private static readonly MdkData Data = MdkData.Find() ?? throw new InvalidOperationException("MDK data not found");
    private static readonly AudioDevice Device = new(Output.Muted);

    private static ScriptRuntime Runtime()
    {
        var level = new LevelData(Data, Level);
        var cmi = Cmi.Load(Data.PathOf($"TRAVERSE/LEVEL{Level}/LEVEL{Level}.CMI"));
        var sprites = Bni.Load(Data.PathOf("TRAVERSE/TRAVSPRT.BNI"));
        var space = new ArenaSpace();
        var mixer = new SoundMixer(Device, _ => null);
        return new ScriptRuntime(level, cmi, sprites, space, new TriangleGroups(), mixer, new Kurt.Kurt(space, mixer, _ => 1));
    }

    private static void Near(Vector3 expected, Vector3 actual) =>
        Assert.True(Vector3.Distance(expected, actual) < Precision, $"{expected} != {actual}");

    [DataFact]
    public void RoundCamerasAreDrawnBetweenTicks()
    {
        var rounds = Runtime().SniperRounds;
        rounds.Fire(Kurt.Scope.Bullets, Eye, 0f, 0f, null);
        Near(rounds.Camera(0, 1f).Position, rounds.Camera(0, Half).Position);

        rounds.Update(1f);
        var was = rounds.Camera(0, 1f).Position;
        rounds.Update(1f);
        var now = rounds.Camera(0, 1f);
        Assert.Equal(SniperRounds.Shot.Watching, now.Shot);
        Assert.NotEqual(was, now.Position);
        Near(Vector3.Lerp(was, now.Position, Half), rounds.Camera(0, Half).Position);
        Near(now.Forward, rounds.Camera(0, Half).Forward);
    }
}

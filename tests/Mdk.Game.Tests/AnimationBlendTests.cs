using System.Numerics;
using Mdk.Engine.Audio;
using Mdk.Formats;
using Mdk.Game.Audio;
using Mdk.Game.Collision;
using Mdk.Game.Level;
using Mdk.Game.Objects;
using Mdk.Game.Scripts;

namespace Mdk.Game.Tests;

/// <summary>Issue #16: frames between ticks drew the objects' animations (monsters, items, doors)
/// at 30 Hz. A frame draws the pose between the last tick's and this tick's animation time; a
/// restart or the end of a non-looping animation snaps.</summary>
public class AnimationBlendTests
{
    private const int Level = 4;
    private const string Arena = "MEAT_5";
    private const string Alien = "XC";
    private const string Charge = "XC_STOP";
    private static readonly Vector3 Somewhere = new(-4.3f, 13181.5f, -1989.05f);
    private const float Half = 0.5f;
    private const float Precision = 1e-4f;

    private static readonly MdkData Data = MdkData.Find() ?? throw new InvalidOperationException("MDK data not found");
    private static readonly AudioDevice Device = new(Output.Muted);

    private static (ScriptRuntime Runtime, MdkObject Obj, ModelAnimation Animation) Spawned()
    {
        var level = new LevelData(Data, Level);
        var cmi = Cmi.Load(Data.PathOf($"TRAVERSE/LEVEL{Level}/LEVEL{Level}.CMI"));
        var sprites = Bni.Load(Data.PathOf("TRAVERSE/TRAVSPRT.BNI"));
        var space = new ArenaSpace();
        var mixer = new SoundMixer(Device, _ => null);
        var runtime = new ScriptRuntime(level, cmi, sprites, space, new TriangleGroups(), mixer, new Kurt.Kurt(space, mixer, _ => 1));
        var obj = runtime.Spawn(runtime.GetArenaState(Arena).Controller, Alien, Somewhere, 0f, -1, 0, ScriptRuntime.Spawning.Plain)!;
        var animation = runtime.FindArenaAnimation(Arena, Charge)!;
        return (runtime, obj, animation);
    }

    private static FrameBlend BlendAt(float tick)
    {
        var space = new ArenaSpace();
        var kurt = new Kurt.Kurt(space, new SoundMixer(Device, _ => null), _ => 1);
        var blend = new FrameBlend(Blending.On);
        blend.Update(kurt, 1f, tick);
        return blend;
    }

    /// <summary>A tick of the animation, from a remembered start.</summary>
    private static void Tick(MdkObject obj)
    {
        obj.Remember();
        obj.AdvanceAnimation(ScriptRuntime.Tick);
    }

    /// <summary>Some vertex the tick moved: (part, vertex).</summary>
    private static (int Part, int Vertex) Moved(Vector3[][] from, Vector3[][] to)
    {
        for (var p = 0; p < to.Length; p++)
        {
            for (var v = 0; v < to[p].Length; v++)
            {
                if (from[p][v] != to[p][v])
                {
                    return (p, v);
                }
            }
        }

        throw new InvalidOperationException("nothing moved");
    }

    private static void Near(Vector3 expected, Vector3 actual) =>
        Assert.True(Vector3.Distance(expected, actual) < Precision, $"{expected} != {actual}");

    [DataFact]
    public void PosesAreDrawnBetweenFrames()
    {
        var (_, obj, animation) = Spawned();
        obj.RestartAnimation(animation, MdkObject.Looping.Once);
        var from = obj.PoseParts();
        Tick(obj);
        var to = obj.PoseParts();

        var (p, v) = Moved(from, to);
        Near(Vector3.Lerp(from[p][v], to[p][v], Half), BlendAt(Half).PoseOf(obj)[p][v]);
        Near(to[p][v], BlendAt(1f).PoseOf(obj)[p][v]);
    }

    [DataFact]
    public void ARestartIsDrawnAtOnce()
    {
        var (_, obj, animation) = Spawned();
        obj.RestartAnimation(animation, MdkObject.Looping.Once);
        Tick(obj);
        Tick(obj);
        obj.Remember();
        obj.RestartAnimation(animation, MdkObject.Looping.Once);

        var pose = BlendAt(Half).PoseOf(obj);
        var now = obj.PoseParts();
        for (var part = 0; part < now.Length; part++)
        {
            Assert.Equal(now[part], pose[part]);
        }
    }

    [DataFact]
    public void ALoopGoesOnToItsFirstFrame()
    {
        var (_, obj, animation) = Spawned();
        obj.RestartAnimation(animation, MdkObject.Looping.Loop);
        while (obj.AnimationFrame != animation.FrameCount - 1)
        {
            Tick(obj);
        }

        var last = obj.PoseParts();
        Tick(obj);
        var first = obj.PoseParts();

        var (p, v) = Moved(last, first);
        Near(Vector3.Lerp(last[p][v], first[p][v], Half), BlendAt(Half).PoseOf(obj)[p][v]);
    }

    [DataFact]
    public void TestsDrawTheFramesAsTheyAre()
    {
        var (_, obj, animation) = Spawned();
        obj.RestartAnimation(animation, MdkObject.Looping.Once);
        Tick(obj);
        var kurt = new Kurt.Kurt(new ArenaSpace(), new SoundMixer(Device, _ => null), _ => 1);
        var blend = new FrameBlend(Blending.Off);
        blend.Update(kurt, Half, Half);
        Assert.Same(obj.PoseParts(), blend.PoseOf(obj));
    }
}

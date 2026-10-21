using System.Numerics;
using Mdk.Engine.Audio;
using Mdk.Game.Objects;

namespace Mdk.Game.Tests;

/// <summary>Objects' rope lines (update_ropes): a swing's rope, or opcode 242's lines.</summary>
public class RopeTests
{
    private static readonly Vector3 Pivot = new(10f, 20f, 50f);
    private const float Half = 0.5f;
    private static readonly AudioDevice Device = new(Output.Muted);

    [Fact]
    public void SwingRopeGoesFromAboveTheObjectToThePivot()
    {
        var obj = new MdkObject { Position = new Vector3(10f, 20f, 0f), RopeMask = 1 };
        obj.RopePoints[0] = obj.Position;
        obj.RopePoints[1] = Pivot;
        Assert.Equal([new Vector3(10f, 20f, 5f), Pivot], RopeView.LinesOf(obj));
    }

    [Fact]
    public void AllLinesSkipZeroPointsAndStartAtTheObjectWithoutModel()
    {
        var obj = new MdkObject { Position = Vector3.UnitX, RopeMask = RopeView.AllLines };
        obj.RopePoints[1] = Pivot;
        // Without a model a reference point is the object's position.
        Assert.Equal([Vector3.UnitX, Pivot], RopeView.LinesOf(obj));
    }

    [Fact]
    public void RopesHangFromTheObjectAsDrawn()
    {
        // A swing between ticks: its rope starts at the object as the frame draws it, not where
        // the tick left it.
        var obj = new MdkObject { Arena = "A", RopeMask = 1 };
        obj.Remember();
        obj.Position = new Vector3(10f, 0f, 0f);
        obj.RopePoints[0] = obj.Position;
        obj.RopePoints[1] = Pivot;
        var blend = new FrameBlend(Blending.On);
        blend.Update(new Kurt.Kurt(new Collision.ArenaSpace(), new Audio.SoundMixer(Device, _ => null), _ => 1), 1f, Half);

        Assert.Equal([new Vector3(5f, 0f, 5f), Pivot], RopeView.LinesOf(obj, blend));
    }

    [Fact]
    public void NoMaskNoLines() => Assert.Empty(RopeView.LinesOf(new MdkObject()));
}

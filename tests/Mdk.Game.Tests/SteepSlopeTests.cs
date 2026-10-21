using System.Numerics;
using Mdk.Engine.Audio;
using Mdk.Engine.Platform;
using Mdk.Formats;
using Mdk.Game.Audio;
using Mdk.Game.Collision;

namespace Mdk.Game.Tests;

/// <summary>Running down a steep slope (LEVEL6's OLYM_8, in-game level 2: 42°, the floor's normal z
/// 0.74): Kurt's feet must follow it, not leave it every other step and flick his animation between
/// the run and the fall.</summary>
public class SteepSlopeTests
{
    private const int Level = 6;
    private const float Step = 1f / 60f;
    private const int Frames = 5;
    private const float Seconds = 1.5f;

    private static readonly MdkData Data = MdkData.Find() ?? throw new InvalidOperationException("MDK data not found");
    private static readonly AudioDevice Device = new(Output.Muted);

    [DataTheory]
    [InlineData(178f)]
    [InlineData(160f)]
    [InlineData(-160f)]
    public void RunningDownhillNeverFalls(float yaw)
    {
        var mto = Mto.Load(Data.PathOf($"TRAVERSE/LEVEL{Level}/LEVEL{Level}O.MTO"));
        var space = new ArenaSpace();
        space.Add(mto.GetArena("OLYM_8"));
        var kurt = new Kurt.Kurt(space, new SoundMixer(Device, _ => null), _ => Frames) { Feet = new Vector3(2307f, 2469f, -1164f), Yaw = yaw };
        var input = new Input();
        for (var t = 0f; t < 0.5f; t += Step)
        {
            kurt.Update(input, Step);
        }

        input.Hold(Key.Forward, Input.State.Down);
        var falls = 0;
        var airSteps = 0;
        var previous = kurt.Current;
        for (var t = 0f; t < Seconds; t += Step)
        {
            kurt.Update(input, Step);
            airSteps += kurt.OnFloor ? 0 : 1;
            if (kurt.Current == Kurt.Kurt.State.Fall && previous != Kurt.Kurt.State.Fall)
            {
                falls++;
            }

            previous = kurt.Current;
        }

        Assert.True(falls == 0, $"fell {falls} times, {airSteps} steps off the floor, ended at {kurt.Feet}");
    }
}

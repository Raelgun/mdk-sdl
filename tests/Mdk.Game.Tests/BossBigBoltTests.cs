using System.Numerics;
using Mdk.Engine.Audio;
using Mdk.Formats;
using Mdk.Game.Audio;
using Mdk.Game.Collision;
using Mdk.Game.Level;
using Mdk.Game.Objects;
using Mdk.Game.Scripts;

namespace Mdk.Game.Tests;

/// <summary>LEVEL7's boss (DANT_10, XB1) fires BIGBOLTs from the GUN part in his hands (fire, opcode 61):
/// they start where the animation holds the gun, not at the rest pose's gun by his feet (which put
/// them in the floor, dead at once), and cross the arena to Kurt.</summary>
public class BossBigBoltTests
{
    private const float Tick = 1f / 30f;
    /// <summary>The far side of DANT_10, where Kurt first sees the tower (about 650 away).</summary>
    private static readonly Vector3 FarSide = new(545.89f, 5023.39f, 184.54f);
    /// <summary>XB1's script: the firing animation, then fire [1, "GUN"], "BIGBOLT".</summary>
    private const int Firing = 0x20baa;

    [DataFact]
    public void BigBoltsLeaveTheGunAndReachKurt()
    {
        var data = MdkData.Find()!;
        var level = new LevelData(data, 7);
        var cmi = Cmi.Load(data.PathOf("TRAVERSE/LEVEL7/LEVEL7.CMI"));
        var sprites = Bni.Load(data.PathOf("TRAVERSE/TRAVSPRT.BNI"));
        var space = new ArenaSpace();
        foreach (var arena in level.Arenas.Where(a => level.IsReachable(a.Name)))
        {
            space.Add(arena);
        }

        var mixer = new SoundMixer(new AudioDevice(Output.Muted), _ => null);
        var runtime = new ScriptRuntime(level, cmi, sprites, space, new TriangleGroups(), mixer, new Kurt.Kurt(space, mixer, _ => 1));
        runtime.TeleportKurt("DANT_10", FarSide, 0f);
        for (var i = 0; i < 30; i++)
        {
            runtime.Update(Tick);
        }

        var boss = runtime.Objects.First(o => o.TypeName == "XB1" && !o.Dead);
        boss.Restart = Firing;
        MdkObject? bolt = null;
        for (var i = 0; i < 30 && bolt == null; i++)
        {
            runtime.Update(Tick);
            bolt = runtime.Objects.FirstOrDefault(o => o.TypeName == "BIGBOLT");
        }

        Assert.NotNull(bolt);

        // Above the floor he stands on, in his hands.
        Assert.True(bolt.Position.Z > boss.Position.Z + 3f, $"bolt starts at {bolt.Position}, boss at {boss.Position}");

        // It flies across (125 a second) and ends next to Kurt, not by the tower.
        var health = runtime.Kurt.Health;
        for (var i = 0; i < 8 * 30 && !bolt.Dead && bolt.Health > 0; i++)
        {
            runtime.Update(Tick);
        }

        Assert.True(Vector3.Distance(bolt.Position, runtime.KurtPosition) < 20f, $"bolt ended at {bolt.Position}, Kurt at {runtime.KurtPosition}");
        Assert.True(runtime.Kurt.Health < health || bolt.Health == 0);
    }
}

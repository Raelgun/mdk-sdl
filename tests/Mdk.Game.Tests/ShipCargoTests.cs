using System.Numerics;
using Mdk.Engine.Audio;
using Mdk.Formats;
using Mdk.Game.Audio;
using Mdk.Game.Collision;
using Mdk.Game.Level;
using Mdk.Game.Scripts;

namespace Mdk.Game.Tests;

/// <summary>LEVEL7's flying ships (XE) carry a bomb (XBANG) or soldiers (XG) on a chain (spawn_chain).
/// The ship has a single reference point, the hook under it: the cargo hangs there, below the ship,
/// not from the ship's centre (inside it).</summary>
public class ShipCargoTests
{
    private const float Tick = 1f / 30f;
    /// <summary>DANT_10's bomb ship script: follow_path, then spawn_chain 1, "XBANG".</summary>
    private const int BombShip = 0x20fbc;

    [DataFact]
    public void TheBombHangsFromTheShipsHook()
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
        runtime.TeleportKurt("DANT_10", new Vector3(545.89f, 5023.39f, 184.54f), 0f);
        runtime.Update(Tick);

        var anyone = runtime.Objects.First(o => o.Arena == "DANT_10" && !o.Dead);
        var ship = runtime.Spawn(anyone, "XE", new Vector3(1301f, 5018f, 315f), 180f, -1, BombShip, ScriptRuntime.Spawning.Plain)!;
        for (var i = 0; i < 10; i++)
        {
            runtime.Update(Tick);
        }

        var bomb = runtime.Objects.First(o => o.TypeName == "XBANG" && o.Leader == ship);
        Assert.Single(ship.Model!.ReferencePoints);
        Assert.True(Vector3.Distance(bomb.ReferencePoint(0), ship.ReferencePoint(0)) < 0.5f,
            $"bomb hook {bomb.ReferencePoint(0)}, ship hook {ship.ReferencePoint(0)}");
        Assert.True(bomb.Position.Z < ship.Position.Z - 5f, $"bomb at {bomb.Position}, ship at {ship.Position}");
    }
}

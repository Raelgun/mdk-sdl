using Mdk.Engine.Audio;
using Mdk.Formats;
using Mdk.Game.Audio;
using Mdk.Game.Level;

namespace Mdk.Game.Tests;

/// <summary>Some sounds are kept by the arenas of <c>LEVELnO.MTO</c>, not by an SNI archive: LEVEL7's
/// barricade soldiers (DANT_6) sing SING and taunt with TAUNT1 from there. The level's bank finds them.</summary>
public class ArenaSoundsTests
{
    [DataFact]
    public void TheBarricadeSoldiersSongIsInTheLevelsBank()
    {
        var data = MdkData.Find()!;
        var level = new LevelData(data, 7);
        Assert.True(level.Arenas.First(a => a.Name == "DANT_6").Sounds.ContainsKey("SING"));

        var bank = SoundBank.ForLevel(data, 7, level.Arenas);
        Assert.NotNull(bank.Get("SING"));
        Assert.NotNull(bank.Get("TAUNT1"));
        Assert.Equal(SoundMixer.FullVolume, bank.Get("SING")!.Volume);

        // The archives' sounds are as they were; without the arenas the song is missing.
        Assert.NotNull(bank.Get("XG_SPOT1"));
        Assert.Null(SoundBank.ForLevel(data, 7).Get("SING"));
    }
}

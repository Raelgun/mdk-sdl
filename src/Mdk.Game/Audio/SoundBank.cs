using Mdk.Engine.Audio;
using Mdk.Formats;

namespace Mdk.Game.Audio;

/// <summary>A level's sounds by name, from its SNI archives (<c>TRAVERSE.SNI</c>, <c>LEVELnS.SNI</c>,
/// <c>LEVELnO.SNI</c> with the music), converted at the level's load (<see cref="Preload"/>). Later archives win.
/// Then the sounds the arenas of <c>LEVELnO.MTO</c> carry themselves (<see cref="Arena.Sounds"/>), for the
/// names no archive has: LEVEL7's barricade soldiers (DANT_2, DANT_6) sing SING and TAUNT1 from there.</summary>
public sealed class SoundBank(IReadOnlyList<Sni> archives, IReadOnlyDictionary<string, byte[]>? arenaSounds = null)
{
    private readonly Dictionary<string, SoundMixer.Entry?> _cache = [];
    private readonly IReadOnlyDictionary<string, byte[]> _arenaSounds = arenaSounds ?? new Dictionary<string, byte[]>();

    public static SoundBank ForLevel(MdkData data, int level, IEnumerable<Arena>? arenas = null)
    {
        var dir = $"TRAVERSE/LEVEL{level}/LEVEL{level}";
        return new SoundBank([
            Sni.Load(data.PathOf("TRAVERSE/TRAVERSE.SNI")),
            Sni.Load(data.PathOf(dir + "S.SNI")),
            Sni.Load(data.PathOf(dir + "O.SNI")),
        ], ArenaSounds(arenas ?? []));
    }

    /// <summary>The arenas' own sounds by name (the first arena's when two share a name).</summary>
    public static IReadOnlyDictionary<string, byte[]> ArenaSounds(IEnumerable<Arena> arenas)
    {
        var sounds = new Dictionary<string, byte[]>();
        foreach (var arena in arenas)
        {
            foreach (var (name, wav) in arena.Sounds)
            {
                sounds.TryAdd(name, wav);
            }
        }

        return sounds;
    }

    /// <summary>A level of the 1996 demo: its own archives only (sounds it lacks stay silent).</summary>
    public static SoundBank ForBeta(BetaDemo beta, int number) => new(beta.LoadSounds(BetaDemo.LevelOf(number)));

    public SoundMixer.Entry? Get(string name)
    {
        if (_cache.TryGetValue(name, out var entry))
        {
            return entry;
        }

        return _cache[name] = Load(name);
    }

    /// <summary>Converts every sound of the archives now (a level's load: none while playing).</summary>
    public void Preload()
    {
        foreach (var archive in archives)
        {
            foreach (var (name, entry) in archive.Entries)
            {
                if (archive.IsSound(entry))
                {
                    Get(name);
                }
            }
        }

        foreach (var name in _arenaSounds.Keys)
        {
            Get(name);
        }
    }

    /// <summary>Kurt's sprite animations stored with the sounds (the board's <c>K_SURF</c>), or null.</summary>
    public SpriteAnimation? Animation(string name)
    {
        for (var i = archives.Count - 1; i >= 0; i--)
        {
            if (archives[i].GetAnimation(name) is { } animation)
            {
                return animation;
            }
        }

        return null;
    }

    private SoundMixer.Entry? Load(string name)
    {
        for (var i = archives.Count - 1; i >= 0; i--)
        {
            var archive = archives[i];
            var found = archive.Entries.FirstOrDefault(e => e.Key == name);
            if (found.Value == null || !archive.IsSound(found.Value))
            {
                continue;
            }

            var wav = Wav.Parse(archive.GetBytes(found.Value));
            if (wav == null)
            {
                return null;
            }

            var looping = (found.Value.Flags & Sni.FlagLoop) != 0 ? Looping.Forever : Looping.Once;
            var sound = Sound.FromPcm(wav.Data, wav.Channels, wav.SampleRate, wav.BitsPerSample, looping);
            return new SoundMixer.Entry(sound, found.Value.Volume);
        }

        // An arena's own sound: a plain WAV, played once at full volume (as the archives' effects are).
        if (_arenaSounds.TryGetValue(name, out var bytes) && Wav.Parse(bytes) is { } arenaWav)
        {
            var sound = Sound.FromPcm(arenaWav.Data, arenaWav.Channels, arenaWav.SampleRate, arenaWav.BitsPerSample, Looping.Once);
            return new SoundMixer.Entry(sound, SoundMixer.FullVolume);
        }

        return null;
    }
}

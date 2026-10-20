using System.Numerics;

namespace Mdk.Game.Kurt;

/// <summary>A platform that keeps Kurt (flag 0x800000, set_targetable 2): landed on it, he stays on
/// it (damp_gravity 0x469efc sets 0x573b8c). Each step his XY is its XY and his feet stay at least
/// as high above it as when he landed (0x46634e), whatever the arena's walls; he doesn't walk
/// (vel_accel skipped). It lets him go when it loses the flag, or on a teleport or a ride.
/// E.g. LEVEL3's hover ship (XGS) flies him through HMO_9's low opening.</summary>
public sealed partial class Kurt
{
    /// <summary>The position of a platform that keeps Kurt, or null when it doesn't (set by the
    /// scripts runtime).</summary>
    public Func<object, Vector3?>? Keeper;

    private object? _keeper;
    /// <summary>His feet above the keeping platform (0x573a20).</summary>
    private float _keptHeight;

    private bool Kept => _keeper != null;

    /// <summary>Lets go of a platform that no longer keeps him.</summary>
    private void CheckKeeper()
    {
        if (_keeper != null && Keeper?.Invoke(_keeper) == null)
        {
            _keeper = null;
        }
    }

    /// <summary>The keeping platform as the fall's floor (damp_platform_floor: no scan), or null.</summary>
    private (float Top, object Owner)? KeptPlatform(float dz)
    {
        if (_keeper == null || VerticalSpeed > 0f || Keeper?.Invoke(_keeper) is not { } at)
        {
            return null;
        }

        var top = at.Z + _keptHeight;
        return Feet.Z + dz <= top ? (top, _keeper) : null;
    }

    /// <summary>Landed on <paramref name="owner"/>: kept by it if it keeps.</summary>
    private void KeepOn(object owner)
    {
        if (_keeper != null || Keeper?.Invoke(owner) is not { } at)
        {
            return;
        }

        _keeper = owner;
        _keptHeight = Feet.Z - at.Z;
    }

    /// <summary>After his move: on the keeping platform's XY, not below his height on it.</summary>
    private void StayOnKeeper()
    {
        if (_keeper == null || Keeper?.Invoke(_keeper) is not { } at)
        {
            return;
        }

        Platform = _keeper;
        var top = at.Z + _keptHeight;
        Feet = new Vector3(at.X, at.Y, MathF.Max(Feet.Z, top));
        if (Feet.Z > top)
        {
            return;
        }

        OnFloor = true;
        VerticalSpeed = MathF.Max(VerticalSpeed, 0f);
    }
}

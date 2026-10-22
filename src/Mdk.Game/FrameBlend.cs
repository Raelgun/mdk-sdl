using System.Numerics;
using Mdk.Formats;
using Mdk.Game.Objects;

namespace Mdk.Game;

/// <summary>Whether frames are drawn between the fixed steps; tests' frames are steps, drawn as they are.</summary>
public enum Blending { Off, On }

/// <summary>Frames between the fixed steps (screens above 60 Hz): Kurt is drawn between his last two
/// steps, the objects between their last two ticks, by the time left over. Kurt on a platform or in
/// the XE is drawn with it; what he rides otherwise (the XD2, the board) is drawn with him. A jump
/// (a teleport, faster than <see cref="MaxSpeed"/>) is drawn where it ends.
/// <code>
///   steps   │─────1/60─────│─────1/60─────│   Kurt:    previous ──α──► current, α = left over / step
///   ticks   │────────────1/30─────────────│   objects: last tick ──α──► now, α = since the tick / tick
///                                  ▲ frame
/// </code></summary>
public sealed class FrameBlend(Blending blending)
{
    /// <summary>Moving faster than this (u/s; the lift: 750) is a jump.</summary>
    public const float MaxSpeed = 1500f;

    /// <summary>Kurt after a step, and what carries him there (its place and yaw, now and a step
    /// before).</summary>
    private readonly record struct Pose(Vector3 Feet, float Yaw, int Teleports, MdkObject? Carrier, Vector3 CarrierAt, float CarrierYaw,
        Vector3 CarrierWas, float CarrierYawWas);

    private Pose? _previous;
    private Pose _current;
    private float _tick = 1f;
    /// <summary>The scripts' tick count at the last step.</summary>
    private int _ticks;
    /// <summary>What Kurt rides that follows him (drawn moved with him).</summary>
    private MdkObject? _driven;
    private Vector3 _lag;
    /// <summary>Per model, the pose drawn and its blended parts.</summary>
    private readonly Dictionary<Model, (Vector3[][] Pose, Vector3[][] Parts)> _poses = [];

    /// <summary>Kurt's feet and yaw as drawn.</summary>
    public Vector3 Feet { get; private set; }
    public float Yaw { get; private set; }

    /// <summary>Kurt after a step: on or in <paramref name="platform"/>, riding <paramref name="driven"/>,
    /// the scripts at <paramref name="tick"/>.</summary>
    public void Record(Kurt.Kurt kurt, MdkObject? platform, MdkObject? driven, int tick)
    {
        // What follows him (the board he stands on) doesn't carry him.
        var carrier = platform != driven ? platform : null;

        // The carrier a step ago: before this step's tick if one ran (objects move at ticks).
        var was = tick != _ticks ? 0f : 1f;
        _ticks = tick;
        var now = new Pose(kurt.Feet, kurt.Yaw, kurt.Teleports, carrier, carrier?.Position ?? Vector3.Zero, carrier?.Yaw ?? 0f,
            carrier?.PositionAt(was) ?? Vector3.Zero, carrier?.YawAt(was) ?? 0f);
        _previous = _previous == null || now.Teleports != _current.Teleports ? now : _current;
        _current = now;
        _driven = driven;
    }

    /// <summary>The mouse turned Kurt by <paramref name="degrees"/> between steps: drawn at once.</summary>
    public void Turn(float degrees)
    {
        if (_previous is not { } previous)
        {
            return;
        }

        _previous = previous with { Yaw = previous.Yaw + degrees };
        _current = _current with { Yaw = _current.Yaw + degrees };
    }

    /// <summary>A frame: <paramref name="step"/> and <paramref name="tick"/> of the way from the last
    /// step and tick to the next (0-1).</summary>
    public void Update(Kurt.Kurt kurt, float step, float tick)
    {
        _tick = tick;
        (Feet, Yaw) = blending == Blending.Off || _previous is not { } from ? (kurt.Feet, kurt.Yaw) : KurtAt(from, _current, step, tick);
        _lag = Feet - kurt.Feet;
    }

    /// <summary>An object's transform as drawn.</summary>
    public Matrix4x4 TransformOf(MdkObject obj)
    {
        if (blending == Blending.Off)
        {
            return obj.Transform;
        }

        return obj == _driven ? obj.Transform * Matrix4x4.CreateTranslation(_lag) : obj.TransformAt(_tick);
    }

    /// <summary>An object's place as drawn (tests' traces).</summary>
    public Vector3 PositionOf(MdkObject obj)
    {
        if (blending == Blending.Off)
        {
            return obj.Position;
        }

        return obj == _driven ? obj.Position + _lag : obj.PositionAt(_tick);
    }

    /// <summary>An object's pose as drawn: between its animation's frames (a scratch pose per
    /// model, kept: no allocation per frame).</summary>
    public Vector3[][] PoseOf(MdkObject obj)
    {
        if (blending == Blending.Off || obj.FramesAt(_tick) is not { } frames)
        {
            return obj.PoseParts();
        }

        var (from, to, share) = frames;
        if (!_poses.TryGetValue(obj.Model!, out var scratch))
        {
            scratch = _poses[obj.Model!] = (new Vector3[to.Length][], new Vector3[to.Length][]);
        }

        var (pose, parts) = scratch;
        for (var p = 0; p < to.Length; p++)
        {
            // A part the animation leaves still is the model's own.
            if (from[p] == to[p])
            {
                pose[p] = to[p];
                continue;
            }

            if (parts[p]?.Length != to[p].Length)
            {
                parts[p] = new Vector3[to[p].Length];
            }

            for (var v = 0; v < to[p].Length; v++)
            {
                parts[p][v] = Vector3.Lerp(from[p][v], to[p][v], share);
            }

            pose[p] = parts[p];
        }

        return pose;
    }

    /// <summary>Where a frame draws a point moving with an object (a rope's end on it).</summary>
    public Vector3 Carried(MdkObject obj, Vector3 point)
    {
        if (blending == Blending.Off)
        {
            return point;
        }

        var turn = obj == _driven ? 0f : obj.YawAt(_tick) - obj.Yaw;
        return PositionOf(obj) + Vector3.Transform(point - obj.Position, Matrix4x4.CreateRotationZ(float.DegreesToRadians(turn)));
    }

    /// <summary>Kurt between two steps; on a carrier at either (landing, leaving, or his fall
    /// missing it a step), his place and yaw on it: it moves at ticks, he at steps.</summary>
    private static (Vector3 Feet, float Yaw) KurtAt(Pose from, Pose to, float step, float tick)
    {
        if ((to.Carrier ?? from.Carrier) is not { } carrier)
        {
            return Jumped(from.Feet, to.Feet, Viewer.Step) ? (to.Feet, to.Yaw) : (Lerp(from.Feet, to.Feet, step), LerpAngle(from.Yaw, to.Yaw, step));
        }

        var (near, nearYaw) = from.Carrier == carrier ? (from.Feet - from.CarrierAt, from.Yaw - from.CarrierYaw)
            : (from.Feet - to.CarrierWas, from.Yaw - to.CarrierYawWas);
        var (far, farYaw) = to.Carrier == carrier ? (to.Feet - to.CarrierAt, to.Yaw - to.CarrierYaw)
            : (to.Feet - carrier.Position, to.Yaw - carrier.Yaw);
        var on = Jumped(near, far, Viewer.Step) ? far : Lerp(near, far, step);
        var turned = LerpAngle(nearYaw, farYaw, step);
        return (carrier.PositionAt(tick) + on, carrier.YawAt(tick) + turned);
    }

    /// <summary>From <paramref name="from"/> to <paramref name="to"/> by <paramref name="t"/>; 1 gives <paramref name="to"/> exactly.</summary>
    public static Vector3 Lerp(Vector3 from, Vector3 to, float t) => t >= 1f ? to : Vector3.Lerp(from, to, t);

    public static float Lerp(float from, float to, float t) => t >= 1f ? to : float.Lerp(from, to, t);

    /// <summary>Degrees, the short way round: 350 to 10 goes through 0.</summary>
    public static float LerpAngle(float from, float to, float t)
    {
        if (t >= 1f)
        {
            return to;
        }

        var turn = MathF.IEEERemainder(to - from, 360f);
        return from + turn * t;
    }

    /// <summary>Whether a move within <paramref name="seconds"/> is too fast to be drawn on the way.</summary>
    public static bool Jumped(Vector3 from, Vector3 to, float seconds) =>
        Vector3.DistanceSquared(from, to) > MaxSpeed * seconds * MaxSpeed * seconds;
}

namespace Cargo.Controls.Harbour;

/// <summary>
/// Orbit camera around a target on the water line, with a real perspective divide. The
/// foreshortening is what makes the scene read as a place rather than an isometric diorama.
/// </summary>
public sealed class HarbourCamera
{
    /// <summary>Camera distance in world units: a moderate lens.</summary>
    public const double Distance = 1000;

    public const double MinPitch = 10 * Math.PI / 180;
    public const double MaxPitch = 88 * Math.PI / 180;
    public const double MinZoom = 0.8;
    public const double MaxZoom = 9;

    public double Yaw { get; set; }
    public double Pitch { get; set; }
    public double Zoom { get; set; } = 1;
    public double TargetX { get; set; }
    public double TargetY { get; set; }

    // Derived per frame by Update().
    private double _cy, _sy, _cp, _sp, _scale, _ox, _oy;

    public V3 Eye { get; private set; }

    /// <summary>Pixels per world unit at the target.</summary>
    public double Scale => _scale;

    public double ViewWidth { get; private set; }

    public double ViewHeight { get; private set; }

    public void Update(double width, double height)
    {
        ViewWidth = width;
        ViewHeight = height;
        _cy = Math.Cos(Yaw);
        _sy = Math.Sin(Yaw);
        _cp = Math.Cos(Pitch);
        _sp = Math.Sin(Pitch);
        _scale = Math.Min(width / 1150, height / 600) * Zoom;
        _ox = width / 2;
        _oy = height / 2 + height * 0.06;
        Eye = new V3(TargetX + Distance * _cp * _sy, TargetY + Distance * _cp * _cy, Distance * _sp);
    }

    public (float X, float Y) Project(double x, double y, double z)
    {
        var dx = x - TargetX;
        var dy = y - TargetY;
        var x1 = dx * _cy - dy * _sy;
        var y1 = dx * _sy + dy * _cy;
        var k = Distance / Math.Max(80, Distance - (y1 * _cp + z * _sp));
        return ((float)(_ox + x1 * _scale * k), (float)(_oy + (y1 * _sp - z * _cp) * _scale * k));
    }

    public (float X, float Y) Project(V3 p) => Project(p.X, p.Y, p.Z);

    /// <summary>Painter's key: larger is closer to the eye.</summary>
    public double Depth(double x, double y, double z)
    {
        var a = x - Eye.X;
        var b = y - Eye.Y;
        var c = z - Eye.Z;
        return -Math.Sqrt(a * a + b * b + c * c);
    }

    public double Depth(V3 p) => Depth(p.X, p.Y, p.Z);

    /// <summary>Back-face test against the eye position.</summary>
    public bool Faces(V3 normal, V3 point) =>
        normal.X * (Eye.X - point.X) + normal.Y * (Eye.Y - point.Y) + normal.Z * (Eye.Z - point.Z) > 0;

    /// <summary>How squarely the camera looks toward a horizontal direction (for the sun glint).</summary>
    public double Facing(double dirX, double dirY) => -(_sy * dirX + _cy * dirY);

    public HarbourCameraPose Pose => new(Yaw, Pitch, Zoom, TargetX, TargetY);

    public void Apply(HarbourCameraPose pose)
    {
        Yaw = pose.Yaw;
        Pitch = pose.Pitch;
        Zoom = pose.Zoom;
        TargetX = pose.TargetX;
        TargetY = pose.TargetY;
    }
}

public readonly record struct HarbourCameraPose(double Yaw, double Pitch, double Zoom, double TargetX, double TargetY)
{
    private static double Rad(double d) => d * Math.PI / 180;

    public static HarbourCameraPose Overview => new(Rad(-20), Rad(40), 2.15, 170, 40);

    public static HarbourCameraPose FromSea(HarbourCameraPose pose) => pose with { Yaw = 0, Pitch = Rad(18) };

    public static HarbourCameraPose FromLand(HarbourCameraPose pose) => pose with { Yaw = Math.PI, Pitch = Rad(26) };

    public static HarbourCameraPose Plan(HarbourCameraPose pose) => pose with { Yaw = 0, Pitch = Rad(86) };

    /// <summary>Eases from this pose to <paramref name="to"/>; yaw takes the short way round.</summary>
    public HarbourCameraPose Lerp(HarbourCameraPose to, double t)
    {
        var dYaw = to.Yaw - Yaw;
        dYaw = ((dYaw + Math.PI) % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI) - Math.PI;
        return new(Yaw + dYaw * t, Pitch + (to.Pitch - Pitch) * t, Zoom + (to.Zoom - Zoom) * t,
            TargetX + (to.TargetX - TargetX) * t, TargetY + (to.TargetY - TargetY) * t);
    }
}

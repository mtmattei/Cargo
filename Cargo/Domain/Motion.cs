using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Storage;
using Windows.UI.ViewManagement;

namespace Cargo.Domain;

/// <summary>
/// The reduced-motion preference. Skia desktop reports <see cref="UISettings.AnimationsEnabled"/>
/// as always true, so the platform value alone cannot honour it; the app keeps its own setting,
/// saved between launches, and holds decorative motion still when either one asks.
/// </summary>
public static class Motion
{
    private const string SettingKey = "ReduceMotion";

    private static readonly bool PlatformEnabled = new UISettings().AnimationsEnabled;
    private static bool? _requested;

    /// <summary>Raised when <see cref="Requested"/> changes.</summary>
    public static event EventHandler? Changed;

    /// <summary>True when decorative motion should hold still.</summary>
    public static bool Reduced => Requested || !PlatformEnabled;

    /// <summary>The in-app setting.</summary>
    public static bool Requested
    {
        get => _requested ??= Read();
        set
        {
            if (Requested == value)
            {
                return;
            }

            _requested = value;
            Write(value);
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>A duration from Themes/MotionTokens.xaml, e.g. <c>DurationRollMs</c>.</summary>
    public static TimeSpan Duration(string key) =>
        TimeSpan.FromMilliseconds(Application.Current.Resources.TryGetValue(key, out var v) && v is double ms ? ms : 200);

    /// <summary>A number from Themes/MotionTokens.xaml, e.g. <c>RollTravelEm</c>.</summary>
    public static double Number(string key, double fallback) =>
        Application.Current.Resources.TryGetValue(key, out var v) && v is double d ? d : fallback;

    /// <summary>A house curve (a <see cref="KeySpline"/> token) as a Composition easing function.</summary>
    public static CompositionEasingFunction Ease(Compositor compositor, string key = "EaseSmooth")
    {
        var spline = Application.Current.Resources.TryGetValue(key, out var v) && v is KeySpline k
            ? k
            : new KeySpline { ControlPoint1 = new(0.22, 1), ControlPoint2 = new(0.36, 1) };
        return compositor.CreateCubicBezierEasingFunction(
            new((float)spline.ControlPoint1.X, (float)spline.ControlPoint1.Y),
            new((float)spline.ControlPoint2.X, (float)spline.ControlPoint2.Y));
    }

    // Settings storage can be unavailable (a sandboxed or read-only profile); the preference then
    // lasts for the session only.
    private static bool Read()
    {
        try
        {
            return ApplicationData.Current.LocalSettings.Values[SettingKey] is true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void Write(bool value)
    {
        try
        {
            ApplicationData.Current.LocalSettings.Values[SettingKey] = value;
        }
        catch (Exception)
        {
        }
    }
}

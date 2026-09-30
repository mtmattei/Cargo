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

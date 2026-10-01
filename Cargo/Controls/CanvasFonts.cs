using SkiaSharp;
using Windows.Storage;

namespace Cargo.Controls;

/// <summary>
/// The app's fonts as SkiaSharp typefaces, for text drawn on a canvas (the harbour's berth numbers,
/// the timeline's labels, the tide lane). Each file loads once, off the packaged assets, and is
/// shared; a canvas draws with the default face until its typeface arrives.
/// </summary>
public static class CanvasFonts
{
    private static readonly Dictionary<string, Task<SKTypeface?>> Loads = new();

    public const string MonoMedium = "IBMPlexMono_Medium.ttf";
    public const string Mono = "IBMPlexMono_Regular.ttf";
    public const string BodyMedium = "Archivo_Medium.ttf";
    public const string BodyStrong = "Archivo_SemiBold.ttf";

    public static Task<SKTypeface?> Load(string file)
    {
        lock (Loads)
        {
            if (!Loads.TryGetValue(file, out var load))
            {
                load = LoadAsync(file);
                Loads[file] = load;
            }

            return load;
        }
    }

    private static async Task<SKTypeface?> LoadAsync(string file)
    {
        try
        {
            var storage = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///Assets/Fonts/{file}"));
            using var stream = await storage.OpenStreamForReadAsync();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            return SKTypeface.FromData(SKData.CreateCopy(memory.ToArray()));
        }
        catch (Exception)
        {
            // The default typeface is an acceptable fallback for canvas text
            return null;
        }
    }
}

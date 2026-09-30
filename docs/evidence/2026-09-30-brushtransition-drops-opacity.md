# Evidence: BrushTransition interpolates Color only, dropping brush Opacity

- Session: e9a97965-eab8-4207-aa5f-07e514c1ac63 · 2026-09-30
- Versions: Uno.Sdk 6.7.30, `net10.0-desktop`, Windows 11 Win32 host
- Gotcha: `BrushTransition` on Uno Skia interpolates `SolidColorBrush.Color` only; an Opacity-based brush renders as solid colour

## Symptom
The Berths status rows had a `Border` whose `Background` swapped between cached brushes built as
`new SolidColorBrush(InkColor) { Opacity = 0 }` (rest) and `{ Opacity = .06 }` (linked hover), with:

```xml
<Border.BackgroundTransition>
  <BrushTransition Duration="0:0:0.15" />
</Border.BackgroundTransition>
```

After adding the transition, every row rendered solid ink (black ground, text unreadable); the
linked row rendered dark grey. The build reported 0 warnings (no Uno0001).

## Probe
The same rows rendered correctly in the previous build (commit 58206e1), which used the same
brushes without the `BackgroundTransition`. The only change between the two captures was the
transition, so the brush Opacity was being lost once the transition drove the colour.

## Fix that held
Alpha moved into the colour itself, brush Opacity 1 (`Tokens.Tint`, commit 40a1a97):

```csharp
public static SolidColorBrush Tint(string key, double alpha)
{
    var c = Color(key);
    return Of(Windows.UI.Color.FromArgb((byte)Math.Round(c.A * alpha), c.R, c.G, c.B));
}
```

Rebuilt and captured: rows back to their paper ground, the linked row at 6% ink, the needs row at
16% amber, and the fade still running.

## Not established
No reduced single-control repro; whether the transition drops Opacity at both endpoints or only
the target is not isolated.

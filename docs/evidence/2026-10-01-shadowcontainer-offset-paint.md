# Evidence: ShadowContainer keeps its paint when only its offset changes

Session 443328ad-ef50-4a19-bf30-bc7cd0288c09 · 2026-10-01 · Uno.Sdk 6.7.30 · Uno.Toolkit 9.1.3 · net10.0-desktop · Windows 11

Gotcha: `~/.claude/rules/uno-runtime-gotchas.md`, "ShadowContainer keeps its painted background and shadow where they were when only its offset changes".

## Setup

The harbour's layer switcher in `Cargo/Presentation/MainPage.xaml` was a `utu:ShadowContainer` (surface
`SolidColorBrush`, three inline shadows) whose position came from `{utu:Responsive}` values on the container
itself, with a `ResponsiveLayout` whose Normal threshold was 1200:

```xml
<utu:ShadowContainer Grid.Row="1" Background="{StaticResource SurfaceInvariantBrush}" VerticalAlignment="Bottom"
                     HorizontalAlignment="{utu:Responsive Layout={StaticResource HudBreakpoints}, Narrow=Left, Normal=Center}"
                     Margin="{utu:Responsive Layout={StaticResource HudBreakpoints}, Narrow='40,0,0,64', Normal='0,0,0,16'}">
```

## Symptom (transcript, assistant turns, trimmed)

> Launched at 1680 x 1020, then the window was sized to 1100 x 900 (twice, target+8 then target, per the
> stale-pixel resize gotcha). "The stacked switcher looks wrong: its float background is narrower than its
> segments and shifted left ('Traffic' sits outside it)."

> Second capture a few seconds later: "It persists, so it's not a stale frame. The segments sit where the
> layout puts them, but the ShadowContainer's painted background stayed at its old geometry when its alignment
> flipped at the breakpoint."

The segments (Map / Yard / Security / Traffic) started at x ≈ 44 as laid out; the surface and shadow spanned
roughly x −10 to 317, about 50 px left of and narrower than the content.

## Probe and fix

> "One cheap test separates the hypotheses: wrap the container in a plain Grid that takes the responsive
> alignment and margin. The ShadowContainer itself then never moves relative to its parent."

```xml
<!-- WORKAROUND(ShadowContainer keeps its paint when only its offset changes) -->
<Grid Grid.Row="1" VerticalAlignment="Bottom"
      HorizontalAlignment="{utu:Responsive Layout={StaticResource HudBreakpoints}, Narrow=Left, Normal=Center}"
      Margin="{utu:Responsive Layout={StaticResource HudBreakpoints}, Narrow='40,0,0,64', Normal='0,0,0,16'}">
  <utu:ShadowContainer Background="{StaticResource SurfaceInvariantBrush}"> ... </utu:ShadowContainer>
</Grid>
```

> Rebuilt, relaunched at 1680, resized to 1100 x 900: "The wrapper fixes it: the stacked switcher renders
> whole at x = 40, directly above the camera menu, clear of the Key/zoom group."

## Evidence grade

Measured once on Windows desktop, before and after; not reduced to a minimal page and not traced in Toolkit
source. The cause (repaint on size change, not on an arrange-offset change) is inferred.

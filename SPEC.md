# SPEC: Harbour scene (orbitable true-scale miniature)

Port `docs/prototypes/north-quay-miniature.html` (concept B4) into Cargo as the Overview harbour.
Written 2026-09-29. Built in the same session at the user's request ("move it to our app"; see the
build-end-to-end feedback), so this spec is also the build record.

## Capability inventory

| Capability (implied by the prototype) | Status | Note |
|---|---|---|
| 3D harbour, orbit / tilt / zoom camera | implemented | `SKCanvasElement`, own perspective projection + painter's sort (prototype engine ported 1:1) |
| Camera presets (Overview, From sea, From land, Plan) | implemented | XAML segmented buttons in the stage HUD |
| Fly-to on selection | implemented | selecting a vessel tag or hull eases the camera to it |
| Data tags on leader lines | implemented | XAML `Button`s (real focus, automation names); leaders drawn in Skia |
| Chart layer (isobaths, soundings, fairway, berth pocket) | implemented | ground layer in the static picture |
| Live cranes (trolley/spreader cycle), terminal trucks | implemented | 30 fps `DispatcherTimer` → `Invalidate()`, stopped on Unloaded |
| Tilt-shift, AO, edge highlights, haze | implemented | `SaveLayer` + cached blur filter + `DstIn` gradient mask |
| Harbour layers (Map, Yard, Security, Traffic) | implemented | re-expressed as ground overlays in 3D (were 2D overlays) |
| Berth hover (drives `PortState.HoveredBerth`) | implemented | ground-tile hit test |
| Compact strip on Vessels / Security | **deferred** | keeps the existing `PortStage` sprite strip until phase 2 |
| Keyboard orbit | implemented | arrows / +/- / 0 on the focusable `HarbourView` |
| Reduced motion | **substituted** | `UISettings.AnimationsEnabled` is hardcoded true on Skia desktop (gotcha); honoured where real (Android), no in-app setting yet |
| Dark theme | **omitted** | the app has no dark theme (audit H11); scene is light-only by design |
| Screen-reader access to the 3D view | **substituted** | canvas is decorative to AT; vessel tags + Overview "Next up" list carry the information |

## Architecture Brief

- **MVVM, unchanged.** `PortState` (`Domain/PortState.cs`) stays the single observable store; MVUX is not introduced mid-project (project rule). The scene reads `HarbourSelection`, `HarbourLayer`, `HoveredBerth`, `Section`, and invokes `PickHullVesselCommand` / `OpenVesselCommand`.
- **Module layout** (`Controls/Harbour/`):
  - `HarbourMath.cs`: `V3`, polygon helpers (convex hull, clip, extrude, beam), seeded RNG.
  - `HarbourCamera.cs`: yaw/pitch/zoom/target, perspective projection (F = 1000 world units), depth, presets.
  - `HarbourWorld.cs`: builds faces, stacks, shadows, decals and the chart layer from `PortData` (vessel length, draft, load/unload %, operator livery; berth state and depth). Pure C#, built once.
  - `HarbourPalette.cs`: resolves every scene colour from `Themes/Tokens.xaml` (`Harbour*` group) into `SKColor`; face shading and edge light.
  - `HarbourRenderer.cs`: all SkiaSharp drawing. Owns reused `SKPaint`/`SKPath`; records the static scene into an `SKPicture` when the camera, layer or selection changes; draws the live layer every frame.
  - `HarbourScene.cs`: the `SKCanvasElement`. Pointer input, camera tweens, timer, invalidation.
  - `HarbourView.xaml(.cs)`: the stage `UserControl`: canvas, tag overlay, HUD (presets, layers, scale bar, compass, zoom).
- **Data flow:** `PortData` → `HarbourWorld` (once) → renderer. `PortState` events → `HarbourView` snapshots selection/layer → scene marks static dirty → `Invalidate()`. Camera changes → `HarbourView` re-projects tag anchors (UI thread) → hands clamped anchors to the renderer for leaders.
- **Platform constraints:** Skia renderer on every head (`SkiaRenderer` + desktop). `SKCanvasElement.IsSupportedOnCurrentPlatform()` guards construction; unsupported heads keep `PortStage`.
- **Validation:** `dotnet build -f net10.0-desktop` 0 errors; launch with `APP_NO_HOTDESIGN=1 CARGO_START_SECTION=overview`, capture with `tools/Capture-Window.ps1`; drive presets through a DEBUG env hook `CARGO_HARBOUR_VIEW`.

Decision: SKCanvasElement over XAML shapes.
Reason: ~8k faces with per-frame projection and depth sort; XAML elements cost a visual each and cannot sort per frame.
Tradeoff: the scene is opaque to automation; tags and lists carry the accessible surface.

Decision: static `SKPicture` + live layer.
Reason: the prototype showed a full redraw costs tens of ms; cranes and trucks animate at 30 fps while the camera is still.
Tradeoff: two picture replays per frame when tilt-shift is on (sharp + blurred copy).

## Design Brief

- **Direction:** high-key "real-life miniature" (prototype B4): true scale (1 unit = 3.9 m), perspective, studio light with blue-violet shadows, tilt-shift focus band, edge highlights, white haze at the top.
- **Colour:** every value is a token in `Themes/Tokens.xaml` under a new `Harbour scene` group. Saturated colour is status only, using the app's existing semantics: reserved/arriving = `Amber`, restricted = `Alert`, occupied = neutral, selected = `Ink` outline. Container liveries and hulls are desaturated operator colours (MSC, ONE, Maersk, Hapag-Lloyd, CMA CGM).
- **Layout:** stage height = clamp(width × 0.42, 380, 640) in full mode. HUD: presets bottom-left, layers bottom-centre, scale + compass + zoom bottom-right, 12 px insets.
- **Typography:** tags use the app's `MonoMediumFont` (IBM Plex Mono) at the existing caption size; painted berth numbers use Plex Mono in the canvas.
- **Components:** HUD surfaces use `SurfaceBrush` + the app's card shadow recipe; segmented buttons follow the existing `BareButton` pattern already used by the layer switcher.

## Interaction Brief

- **Flows:** drag orbits (yaw unlimited, pitch 10–88°), wheel/pinch zooms (0.8–9×), presets ease over 800 ms (ease-out-quart), tag/hull click toggles `HarbourSelection` and flies to the vessel, a selected tag expands with arrival, departure, length/draft, security and "Open vessel profile".
- **Keyboard:** `HarbourView` is a tab stop: ←/→ yaw 15°, ↑/↓ pitch 6°, +/- zoom, 0 resets.
- **States:** data is in-memory constants (no loading/error path exists, recorded as a gap by the audit). Font load failure falls back to the default typeface.
- **Motion jobs:** camera ease (continuity), crane cycle and trucks (live operations), intro swing (shows it is a 3D space). Reduced motion: no intro, no ease, static cranes where the platform reports it.
- **Verification:** build → launch on Overview → capture default view → capture each preset via `CARGO_HARBOUR_VIEW` → drag once via Win32 input if available.

## Implementation Plan

1. Tokens: add the `Harbour scene` colour group.
2. Port the engine: math, camera, world, palette, renderer.
3. `HarbourScene` element + `HarbourView` stage (tags, HUD).
4. Wire into `ShellPage`: full mode shows `HarbourView`; compact keeps `PortStage`; retire the XAML callouts.
5. Build, launch, capture, fix, commit per step.

## Unresolved Questions

- Compact strip on Vessels / Security still uses the old sprite `PortStage` (deferred to phase 2).
- Reduced motion on Skia desktop needs an in-app setting (platform reports animations always enabled).
- Dark theme omitted: the app has none yet (audit H11).
- The 3D view is not screen-reader navigable; tags and the Overview list carry the information.
- Performance of orbiting on low-end GPUs is unmeasured; the static picture helps idle frames, not drag frames.

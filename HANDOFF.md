# HANDOFF — Cargo harbour scene
Updated: 2026-09-29

## Where we are
Audited the app (`docs/audit-2026-09-29.md`) and ran a ui-craft critique. Took the harbour through web prototype rounds: isometric pastel (rejected as "toy") → perspective operations model → muted colour → true scale → real-life miniature (`docs/prototypes/north-quay-miniature.html`). Wrote `SPEC.md` and shipped the harbour as an `SKCanvasElement` scene (`Cargo/Controls/Harbour/`) on the full stage (Overview, or an expanded stage elsewhere). The compact strip on Vessels/Security still uses the old sprite `PortStage` (phase 2).

## Last verified state
- Build: pass, `net10.0-desktop` Debug, 0 errors; no new warnings (the pre-existing Uno0001 warnings remain).
- Runtime (Win32 input injection + PrintWindow, no App MCP): default view renders; crane/truck frames animate; the Plan preset works; tag click → selection, fly-to, facts card, Open vessel profile; drag orbit with inertia; Yard layer; the Vessels compact strip is unchanged. Shots: `shots/w-overview.png`, `x-harbour-selected.png`, `x-harbour-orbit.png`.
- Git: `main`, last commit `469ebf0` + this handoff; not pushed.
- Lint: Harbour files 0 gating hits. App-wide pre-existing: CARD 37 · TOKENTHEME 57 · CODEBEHIND 27 · HEX 4 · OVERLAY 2 · BUILTIN 1.

## Guidance applied
| Skill / rule / decision | Applied at (file:line) |
|---|---|
| skiasharp-uno: `IsSupportedOnCurrentPlatform` guard | `Controls/Harbour/HarbourView.xaml.cs:55` |
| skiasharp-uno: cache filters, never build per frame | `HarbourRenderer.cs:695`, `:837` |
| gotchas: timer + `Invalidate`, no held Rendering loop; stop on Unloaded | `HarbourScene.cs:23`, `:52` |
| Still scene recorded once per camera change | `HarbourRenderer.cs:97` |
| Project rule: no colour literals, tokens only | `Themes/Tokens.xaml` "Harbour scene" group |
| MVVM + `PortState` kept (no MVUX mid-project) | `HarbourScene.cs` `PickHullVesselCommand`, `HoveredBerth` |
Read, not applied: `uno-toolkit` CardContentControl (the project uses its own `controls:Card`).

## Next actions (in order)
1. Phase 2: build the compact strip for Vessels/Security from the 3D scene (a low strip camera), then retire `PortStage`.
2. Audit quick wins: C1 (`Tokens.Color` drops brush opacity), H11 (pin Light theme), C3/C4 (overlaps on Overview and Vessels).
3. Measure orbit fps on a Release build; if it's under 30, cache the static layer to an image during drag, or lower the detail.
4. Push to origin once reviewed.

## Open questions
- Reduced motion on Skia desktop needs an in-app setting (the platform always reports animations enabled).
- The 3D view isn't screen-reader navigable; tags and the Overview lists carry the information.
- Tilt-shift strength, and whether to add a small signature colour touch (pink trees at the gate).

## Relaunch
```
cd C:\Users\Platform006\Cargo
dotnet build Cargo/Cargo.csproj -f net10.0-desktop
$env:APP_NO_HOTDESIGN='1'; $env:CARGO_START_SECTION='overview'   # optional: $env:CARGO_HARBOUR_VIEW='plan|sea|land'
dotnet run --project Cargo/Cargo.csproj -f net10.0-desktop
```

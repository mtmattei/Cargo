# HANDOFF — Cargo: needs-you + Overview context panel spec written; build and GL trial next
Updated: 2026-09-30

## Where we are
2026-09-30, second half: the six-item fix list is done.
- Brushes renamed `*InvariantBrush` (the app stays light-only); lint is 0 on every gating rule.
- Reduced-motion toggle in the header, saved in LocalSettings (`Domain/Motion.cs`, `Live.RunWhileShown`).
- The harbour's screen-reader summary is a live region and its FullDescription.
- Layer tabs stack above the camera presets below 1040 px.
- `CARGO_ORBIT` measurement hook.
- Stable Uno.Sdk 6.7.30, merged to main.
The compact strip was already done (HarbourView `Compact`).
Queued: the MainPage design pass against the user's seven principles, and the user's question about `GLCanvasElement` versus the Skia harbour.

## Last verified state
- Build: pass on Uno.Sdk 6.7.30, `net10.0-desktop`, full `--no-incremental` rebuild, 0 warnings, 0 errors.
- Runtime (uno-app MCP):
  - All five sections, the Security Inspection tab, and no startup.log.
  - Motion off persists across a relaunch, and the harbour is pixel-identical over 10 s while off.
  - The summary text follows the preset, layer and selection.
  - HUD checked at 960 and 1100 px.
- Perf (Debug, 16 cores, 4 x 5 s after a 45 s warm-up): Overview idle 60-86% of one core, orbit 112-144%. The same on 6.8.0-dev.23 and on 6.7.30.
- Git: main pushed to origin; working tree clean.
- Lint: CARD 0 · HEX 0 · TOKENTHEME 0 · BACKBAR 0 · CODEBEHIND 0 · OVERLAY 0 · RESPONSIVE 0 · BUILTIN 0 · ICON 0.

## Guidance applied
| Skill / rule / decision | Applied at (file:line) |
|---|---|
| uno-navigation: routes + Visibility region | Cargo/App.xaml.cs:89 RegisterRoutes; Presentation/MainPage.xaml:189 `uen:Region.Navigator="Visibility"` |
| navigation-xaml: declarative requests | Presentation/MainPage.xaml:56; Presentation/SecurityView.xaml:33 |
| toolkit-responsive: header breakpoints | Presentation/MainPage.xaml:14 `ResponsiveLayout`, used at :74, :81, :96 |
| uno-toolkit: CardContentControl + lightweight keys | Themes/Styles.xaml:81 PanelCard |
| uno-toolkit: CommandExtensions | Presentation/FleetView.xaml:303 |
| skiasharp-uno: SKCanvasElement harbour | Controls/Harbour/HarbourScene.cs:15 |
| gotcha: router builds view models off the UI thread | Presentation/BerthsViewModel.cs:17, CargoViewModel.cs:15 `dispatcher.TryEnqueue` |
| idle-turn section build | Presentation/MainPage.xaml.cs:64 `DispatcherQueuePriority.Low` |
Read, not applied: none.

## Next actions (in order)
1. Answer the 6 Unresolved Questions in `SPEC-BERTHS-NEEDS-YOU.md`, then build it (plan steps 1-9) in a fresh session. The spec now includes the Overview context panel agreed with the user: below the harbour, one fixed-height panel follows the harbour (priority: needs-you decision, then selection, then berth hover after a 300 ms pause, then the layer tab); the remaining Overview charts move to their sections.
2. Harbour renderer trial: a `GLCanvasElement` option beside the Skia one (uno-build-options), orbit CPU for both, and a side-by-side screen recording.
3. MainPage design pass (gold-standard-pass) against the user's seven principles.

Done 2026-09-30, second pass (all verified with the uno-app MCP):
- Idle CPU is not a regression. Measured the same way, c9e95e9 idles at 126-218% and HEAD at 48-53%.
- The harbour bake is reused (probe: about 12.6 frames/s, one re-bake a minute).
- Tags lead with their berth (B04 ·) and step up instead of overlapping.
- Overview fits narrow windows (masthead stacks below 1240 px; the pane takes the viewport width).
- The Overview stage is capped at 58% of the window height.

## Open questions
- SPEC.md still lists the compact strip as deferred; the code has moved on.
- SPEC.md compared SKCanvasElement only against XAML shapes. `GLCanvasElement` (UnoFeature `GLCanvas`, Silk.NET) was never evaluated. Per the docs it runs on WinAppSDK, Skia desktop with hardware acceleration, and Skia WebAssembly, not on Android or iOS, both of which this app targets.
- Screen readers get a one-line harbour summary and the tag buttons, but no berth-by-berth keyboard walk (declined for now).
- These gotchas from 2026-09-29 still have no exported transcript excerpt: RouteChanged path segments, off-UI-thread view models, RowSpan overlay shrinking an Auto row.

## Relaunch
```
cd C:\Users\Platform006\Cargo\Cargo
dotnet build -f net10.0-desktop
$env:APP_NO_HOTDESIGN='1'; $env:CARGO_START_SECTION='overview'; .\bin\Debug\net10.0-desktop\Cargo.exe
```
The first launch after a full rebuild takes more than 10 s to show a window. `CARGO_HARBOUR_VIEW=sea|land|plan` jumps the camera to a preset.

# HANDOFF — Cargo: needs-you build, steps 1-7 done; Overview context panel next
Updated: 2026-09-30

## Where we are
2026-09-30, third session: the spec's six questions are answered (amber; Confirm writes a real
assignment; full bar on Berths plus a panel mode on Overview; minutes-only clock roll; Nordic Star
only; Activity as a Map panel mode), recorded under *Decisions* in `SPEC-BERTHS-NEEDS-YOU.md`.
Plan steps 1-7 are built, runtime-verified and committed: motion tokens + `RollingText` clock;
`PortState` decision state; the Berths needs-you bar, status list and vessel detail; linked hover
(rows, tags, bar, hull); the sliding camera pill; the creeping route with confirm/undo fade; rolling
countdowns and the sliding cargo bar. Steps 8 (Overview context panel) and 9 (reduced-motion pass,
critique) remain.

## Last verified state
- Build: pass, `net10.0-desktop`, Uno.Sdk 6.7.30, 0 warnings, 0 errors (full `--no-incremental` at step 1; incremental since).
- Runtime (uno-app MCP): clock roll mid-flight (60 s probe); Confirm → busy → done → Undo; Review; free-berth pick; keyboard-focus link sets `HoveredVessel` and lights tag + row; pill slide, re-target mid-slide (20 s probe) and zoom fade; route creep (frame diff); confirm fade mid-flight (20 s probe); countdown roll at a minute boundary; stacked layout at 1020 px.
- Git: `main` at 32b4e3b, 8 commits ahead of origin (not pushed); tree clean.
- Lint: CARD 0 · HEX 0 · TOKENTHEME 0 · BACKBAR 0 · CODEBEHIND 0 · OVERLAY 0 · RESPONSIVE 0 · BUILTIN 0 · ICON 0.

## Guidance applied
| Skill / rule / decision | Applied at (file:line) |
|---|---|
| xaml-design-polish: house curves as tokens | Themes/MotionTokens.xaml:10 `EaseSmooth`; Domain/Motion.cs:57 `CreateCubicBezierEasingFunction` |
| xaml-design-polish: SetIsTranslationEnabled before Translation | Controls/RollingText.cs:150 |
| xaml-design-polish: reduced motion jumps to end | Controls/RollingText.cs:66; Controls/Slide.cs:35; Controls/Harbour/HarbourView.xaml.cs:598 |
| uno-toolkit: `{utu:Responsive}` breakpoints | Presentation/BerthsView.xaml:24, :58 (`BerthsBreakpoints`) |
| project convention: work only while shown (Live) | Presentation/BerthsView.xaml.cs:25, :27 |
| gotcha: router builds view models off the UI thread | Presentation/BerthsViewModel.cs:17 `dispatcher.TryEnqueue` |
| gotcha: whitespace TextBlock measures zero | Controls/RollingText.cs:56 `SpaceAdvance()` |
| new gotcha: BrushTransition drops Opacity | Presentation/BerthsView.xaml:98; Domain/Tokens.cs:64 `Tint` |
| spec: hover outlines in the live layer | Controls/Harbour/HarbourScene.cs:410; Controls/Harbour/HarbourRenderer.cs:412 `DrawNeeds` |
| spec a11y: polite live regions | Presentation/NeedsYouBar.xaml:14; Presentation/BerthsView.xaml:144 |
Read, not applied: none.

## Next actions (in order)
1. Plan step 8, Overview context panel (spec section *Overview context panel*): `OverviewContextViewModel` with the mode priority (needs-you, selection, berth hover after a 300 ms dwell, layer); one view per mode in a Visibility region or VSM (no code-behind toggling); reuse `NeedsYouBar`; move Container volume and Cargo flow to Cargo, Sea & weather and tide to Waterways; Activity becomes a Map panel mode. Verify each mode at 1680 x 1020 and 1100 x 900, no vertical scroll at 1020 high.
2. Plan step 9: reduced-motion pass over every new motion, lint, `ui-craft` self-critique, HANDOFF.
3. Push `main` once step 8 lands (8 commits local).
4. Then the queued items: `GLCanvasElement` harbour trial (uno-build-options), MainPage design pass (gold-standard-pass).

## Open questions
- Row 07 recolours through the row's 150 ms hover transition; the spec's 400 ms for the confirm recolour is not separate.
- The row ground `BrushTransition` (150 ms) is not gated by reduced motion (colour only, no movement).
- Berths is clipped below ~1200 px wide on `main` too: `VesselsView` has fixed columns (240 + min 560 + 290). Pre-existing, not in this spec.
- The harbour cannot frame a free berth: "Show on map" on a berth only opens the stage.
- Confirm's error state (berth taken) is wired but not driven at runtime; the demo data never makes berth 07 unfit.
- The pill slide uses a Storyboard with a dependent Width animation, not Composition (Composition would scale the pill and stretch its corners at rest).
- SPEC.md still lists the compact strip as deferred; `GLCanvasElement` never evaluated (Android and iOS unsupported per docs).
- Evidence for the BrushTransition gotcha is exported (`docs/evidence/2026-09-30-brushtransition-drops-opacity.md`); the 2026-09-29 gotchas (RouteChanged segments, off-UI-thread view models, RowSpan overlay) still have none.

## Relaunch
```
cd C:\Users\Platform006\Cargo\Cargo
dotnet build -f net10.0-desktop
$env:APP_NO_HOTDESIGN='1'; $env:CARGO_START_SECTION='berths'; .\bin\Debug\net10.0-desktop\Cargo.exe
```
With the App MCP: `uno_app_start`, then click Berths in the header (the start section is env-only). `uno_app_start` does not always kill the old instance: `taskkill /IM Cargo.exe /F` first, and never in parallel with the start. Screenshot frames are often stale right after a click; take a second capture.

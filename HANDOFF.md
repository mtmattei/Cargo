# HANDOFF — Cargo docs refresh after the harbour and header passes
Updated: 2026-09-30

## Where we are
This session was docs only: README.md now describes the five-section app (it still listed the design's eight), Uno Navigation, the 3D harbour and the idle-turn section build. The code state comes from the commits after the last handoff (a337e32). The header breakpoints moved to `utu:Responsive` (2dac1b0). All five sections are built one per idle turn after launch, about 1.5 s total (b3e46e2). The harbour bakes its settled frame into one GPU image and idles at 15 fps, measured at 22-30% of a core against 121-124% before (c9e95e9). Overview's greeting and figures are laid over the harbour stage (93dc043, ff9093f). The scanner is a floating panel over a 55% dim. The water-level charts use the river's colour (bd9790e), and the survey grid is gone from the page background (a93c2d1). The old sprite `PortStage` is retired: every non-Overview section shows `HarbourView` in `Compact` mode (210 px).

## Last verified state
- Build: not run this session (docs only). ff9093f's commit notes record a runtime check (0 dark frames in 40 captures); no full-build result was recorded after a337e32.
- Runtime: not verified this session.
- Git: main, ff9093f plus uncommitted README.md and HANDOFF.md (docs commit follows this file).
- Lint: CARD 0 · HEX 0 · CODEBEHIND 0 · OVERLAY 0 · RESPONSIVE 1 · BUILTIN 0 · BACKBAR 0 · ICON 0 · TOKENTHEME 40.
  - RESPONSIVE 1 is `Presentation/MainPage.xaml.cs:19`: the masthead's measured height sets `Harbour.TopInset`. It is marked `xaml-lint: allow`, but the RESPONSIVE rule ignores the marker. It is a measurement, not a breakpoint.
  - TOKENTHEME went up from 37 to 40 with the new water and fade brushes.

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
1. Build and relaunch to confirm the current state: all five sections, the compact harbour strip, the floating scanner.
2. Decide dark mode (TOKENTHEME 40): add ThemeDictionaries, or rename the single-value brushes `*Invariant`.
3. Decide the Uno.Sdk pin: 6.8.0-dev.23 against stable 6.7.30 (a build config change).
4. Measure the harbour while orbiting. Idle is fixed, but drag frames still draw the full scene every frame.

## Open questions
- SPEC.md still lists the compact strip as deferred to `PortStage`; the code has moved on (HarbourView `Compact`). Should SPEC.md get a note?
- Reduced motion on Skia desktop needs an in-app setting (the platform always reports animations enabled).
- The 3D view can't be navigated with a screen reader; the tags and the Next up list carry that information.
- These gotchas from 2026-09-29 still have no exported transcript excerpt: RouteChanged path segments, off-UI-thread view models, RowSpan overlay shrinking an Auto row.

## Relaunch
```
cd C:\Users\Platform006\Cargo\Cargo
dotnet build -f net10.0-desktop
$env:APP_NO_HOTDESIGN='1'; $env:CARGO_START_SECTION='overview'; .\bin\Debug\net10.0-desktop\Cargo.exe
```
The first launch after a full rebuild takes more than 10 s to show a window. `CARGO_HARBOUR_VIEW=sea|land|plan` jumps the camera to a preset.

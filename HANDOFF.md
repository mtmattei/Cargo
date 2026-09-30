# HANDOFF — Cargo MVVM + Uno Navigation refactor
Updated: 2026-09-29

## Where we are
The app runs on Uno Navigation with a ViewModel for every page and embedded panel. The route tree is Shell > Main > overview | berths | cargo | fleet | security; Security's tabs are nested, view-less routes in the page's Visibility region. The header switches sections with `uen:Navigation.Request`. Jumps that start in the store (open a vessel, "orders late", "Open inspection") set `PortState.Section` (plus `SecurityTab`), and `MainViewModel` navigates to match. Code-built scenes are XAML controls with dependency properties, cards are Toolkit `CardContentControl`, and the scanner overlay is declared in MainPage's own layer. Earlier the same day: the harbour scene, the 8 to 5 section merge, the performance pass, and the dead-code and token cleanup.

## Last verified state
- Build: pass, `net10.0-desktop`, 0 warnings, 0 errors (full `--no-incremental` rebuild).
- Runtime (Win32 harness + PrintWindow captures):
  - All 5 sections through the header, and starting on Security and on Cargo.
  - Security: tabs, person selection, and the "Open inspection" deep link from Cargo on a first visit.
  - Berths: drag to assign, click-select, the scrubber, Now, and vessel list selection.
  - Waterways: chat by Enter, Send and quick messages.
  - Overview: charts' now markers and Next up leading to Berths.
  - Cargo: filters, the stack panel open and close, and the yard ticks.
  - Scanner: presets, X-ray, zoom and close.
  - No new `startup.log` entries.
- Git: main at a337e32, pushed; working tree clean.
- Lint: CARD 0 · HEX 0 · CODEBEHIND 0 · OVERLAY 0 · RESPONSIVE 0 · BUILTIN 0 · BACKBAR 0 · ICON 0 · TOKENTHEME 37 (open, see below). Remaining code-behind is marked `xaml-lint: allow` with reasons (code-built scenes, visibility-gated refresh).

## Guidance applied
| Skill / rule / decision | Applied at (file:line) |
|---|---|
| uno-navigation: routes + Visibility region | Cargo/App.xaml.cs RegisterRoutes; Presentation/MainPage.xaml `uen:Region.Navigator="Visibility"` |
| navigation-xaml: declarative requests | Presentation/MainPage.xaml header `uen:Navigation.Request="{x:Bind Route}"`; SecurityView.xaml tabs |
| navigation-code: `./` nested navigation | Presentation/MainViewModel.cs ShowSectionAsync; SecurityViewModel.cs ShowTabAsync |
| uno-toolkit: CardContentControl + lightweight keys | Themes/Styles.xaml PanelCard / OutlinedPanel |
| uno-toolkit: CommandExtensions | Presentation/FleetView.xaml chat TextBox |
| gotcha: router builds view models off the UI thread | every *ViewModel ctor `dispatcher.TryEnqueue`; App.xaml.cs `new PortState()` |
Read, not applied: utu:Responsive (header breakpoints still in MainViewModel.ApplyWidth, marked allow).

## Next actions (in order)
1. Decide dark mode. TOKENTHEME 37: the app is `RequestedTheme="Light"`. Either add ThemeDictionaries, or rename the single-value brushes `*Invariant`.
2. Move the header breakpoints to `utu:Responsive` / VSM.
3. Harbour performance: Overview runs ~87% of a core for its 30 fps render.
4. Decide the Uno.Sdk pin: 6.8.0-dev.23 against stable 6.7.30 (a build config change).

## Open questions
- The first visit to a section is built on demand (the old background pre-build is gone); the delay is not measured.
- Runtime gotchas added this session (in `~/.claude/rules/uno-runtime-gotchas.md`), each still without an exported transcript excerpt:
  - RouteChanged path segments.
  - Off-UI-thread view models.
  - RowSpan overlay shrinking an Auto row.

## Relaunch
```
cd C:\Users\Platform006\Cargo\Cargo
dotnet build -f net10.0-desktop
$env:APP_NO_HOTDESIGN='1'; $env:CARGO_START_SECTION='overview'; .\bin\Debug\net10.0-desktop\Cargo.exe
```
The first launch after a full rebuild takes more than 10 s to show a window.

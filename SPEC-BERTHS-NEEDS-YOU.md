# SPEC: Berths "needs you" flow and linked interactions

Written 2026-09-30. Source design: the **North Quay Twin** artifact
(https://claude.ai/artifact/NYuaftYiPe9a6sfbZyLjws), plus the user's interaction list (Before / After / Why,
pasted 2026-09-30, reproduced under *Interaction Brief*). Skills that shape it: `xaml-design-polish` (motion
numbers), `ui-craft` (judgment, critique), `uno-toolkit`, `winui-xaml`; rules `uno-scaffolding.md`,
`uno-runtime-gotchas.md`, `capability-coverage.md`.

**Task in one sentence:** show the dispatcher the one berth that needs a decision (Nordic Star to berth 07),
let them make it in place, and link every list row to the thing it means on the harbour.

## Capability inventory

| Capability (implied by the design + list) | Status | Note |
|---|---|---|
| "Needs you" bar with Confirm / Review / Undo | to build | New; the Berths page has no decision surface today |
| Berth status list (8 berths + anchorage) with berth chips | to build | New panel; `DockingView` plan rows stay as the 36 h planner |
| Selected-vessel detail (state, big number, note, cargo bar, facts, Show on map) | to build | Reuses `VesselsViewModel` data; the existing vessel profile stays below |
| Row ↔ harbour hover link, both directions, keyboard focus too | to build | Harbour already has `PortState.HoveredBerth`; add `HoveredVessel` |
| Sliding camera pill (200 ms, re-targets mid-slide; fades 150 ms on drag) | to build | Replaces `MarkView` style swap in `HarbourView` |
| Flowing approach route (one dash cycle / 1.2 s, only while unconfirmed) | to build | The route is already drawn in `HarbourRenderer` live layer |
| Confirm fade (400 ms) of teal box, painted number, route, label line; Undo reverses | to build | Renderer takes a 0..1 "needs" opacity per berth |
| Row 07 updates in place (background, chip, status, label, 400 ms) | to build | `ObservableCollection` items mutate; the list is never rebuilt |
| Live clock from 20:58, real time | **implemented** | `PortState.NowHours` already advances in real time from `PortData.NowHours` |
| Rolling digits each minute (clock, row 07 time, detail big number) | to build | New `RollingText` control; seconds in the header clock do not roll |
| Cargo bar slides 300 ms between ships | to build | `TickBar` / new bar animates `Percent` |
| Reduced motion | covered | Every loop and fade above checks `Motion.Reduced` (`Domain/Motion.cs`) and jumps to the end state |

## Architecture Brief

- **MVVM, unchanged** (project rule: no MVUX mid-project). `PortState` remains the one store.
- **State added to `PortState`:**
  - `NeedsDecision` (computed): Nordic Star arriving at its reserved berth 07 and not yet confirmed.
  - `ConfirmedBerths` (set of vessel ids), `Confirm(vesselId)`, `UndoConfirm(vesselId)`. Confirm writes the
    assignment through the existing `_assigned` map (`Assign`), so the planner, the conflict check and the
    harbour all read one source.
  - `HoveredVessel` (string?) next to the existing `HoveredBerth`, raised on `HoverChanged` (repaint, no rebuild).
- **New view models** (in `Presentation/`, built on the UI thread per the router gotcha: `dispatcher.TryEnqueue`):
  - `NeedsYouViewModel`: title, meta, `ConfirmCommand` (busy ~600 ms, then confirmed), `ReviewCommand`
    (selects the vessel and flies the harbour to it), `UndoCommand`, `IsDone`.
  - `BerthStatusViewModel`: `ObservableCollection<BerthStatusRow>`; rows are **mutated in place** on confirm.
  - `VesselDetailViewModel`: the selected vessel's state, big value, sub line, note, cargo percent, facts,
    `ShowOnMapCommand`.
- **Controls:** `Controls/RollingText.cs` (per-character cells; old glyph slides up and out, new slides in), and a
  `CargoBar` (or `TickBar` with an animated `Percent`). No Toolkit or WCT equivalent exists for rolling digits
  (checked: Toolkit controls list, WCT 8 Uno support table in `capability-coverage.md`).
- **Harbour:** `HarbourFrameState` gains `NeedsOpacity` (per berth, animated 1→0 over 400 ms) and
  `RoutePhase` (dash offset, advanced by the live timer only while `NeedsDecision`). Hover outlines draw in the
  live layer, so they never re-bake the still frame.
- **Placement:** Berths page, top to bottom: masthead (the needs-you bar joins it on the right, the metrics move
  under the title), a new two-column section (Berth status 1.55 fr · Selected vessel 1 fr), then the existing
  `DockingView` and `VesselsView`.
- **Platform:** Skia renderer everywhere. Composition keyframe + `CreateCubicBezierEasingFunction` work on Uno
  Skia; springs and implicit animations throw `NotImplementedException` (`xaml-design-polish` cross-platform
  table), so every motion here is an explicit keyframe or a Storyboard.
- **Validation:** `dotnet build -f net10.0-desktop` 0 warnings; uno-app MCP drives Confirm, Undo, row hover
  (pointer), focus (Tab), and captures; frame-level motion checked with a 60 s hot-reloaded duration
  (App MCP screenshot latency ~2 s, gotcha).

## Design Brief

- **Direction:** keep the app's (flat, ruled, ink-first). Colour is status only.
- **Colour mapping:** the artifact uses teal for "needs you". In this app amber already means
  *reserved · arriving* (legend, tags, harbour), so **needs-you is amber here** (`AmberInvariantBrush`, ink
  `AmberInkInvariantBrush`); confirmed resolves to the occupied neutral. (Unresolved question 1.)
- **Tokens:** add `Themes/MotionTokens.xaml` (house curves `EaseSmooth 0.22,1 0.36,1`, `EaseOut 0.17,1 0.32,1`;
  durations press 120, state 180, slide 200, bar 300, resolve 400 ms). Spacing 4-based; control height 40;
  radii panel 12 / control 8 / row 4 (from the artifact's `:root`).
- **Type:** berth chip and times in `MonoMediumFont` (tabular); names `BodyStrong` 15; state label caps 11
  tracked via per-character layout only if needed (CharacterSpacing is a no-op on Skia, gotcha).
- **Layout:** Berth status rows = chip 34x30 · state/name/meta · time · chevron; 10 px padding; hairline
  dividers. Detail: state label, big number 32 px display, sub line, note, 6 px cargo bar, 2-column facts,
  ghost "Show on map".
- **Responsive:** below 1040 px the two columns stack (`utu:Responsive`, `HeaderBreakpoints` values); the
  needs-you bar goes full width below 960.

## Interaction Brief

The user's list, as the acceptance table:

| Area | Behaviour | Numbers |
|---|---|---|
| Model ↔ list | Hover a ship's row: outline that ship, light its tag. Hover a free berth row: dashed outline on the berth. | fade in 150 ms |
| List ↔ model | Hover a ship or its tag: its row gets the hover background (row 07: thin amber outline instead) | 150 ms |
| Needs-you bar | Hover outlines Nordic Star, lights its tag and row 07 | 150 ms |
| Keyboard | Tabbing onto a row highlights the same things as hover | same |
| Camera pill | One ink pill slides and resizes to the chosen preset; a second click mid-slide re-targets from the current position | 200 ms `EaseSmooth` |
| Camera pill | Dragging the model fades the pill out (no preset is current) | 150 ms |
| Route | Approach route to berth 07 creeps toward the berth, only while it needs confirming | 1 dash cycle / 1.2 s |
| Confirm | Berth 07's box, painted number, route and tag leader fade out; Undo fades them back | 400 ms |
| Row 07 | Updates in place: background, chip, status text, time colour | 400 ms, no rebuild, scroll kept |
| Countdown | Each minute the clock's minutes, row 07's time and the detail big number roll: old digit slides up and out, new slides in | 280 ms `EaseSmooth`, 0.6 em travel |
| Cargo bar | Slides from the old value to the new when another ship is picked | 300 ms `EaseSmooth` |
| Press | Every button and row scales to 0.98 | 120 ms |

- **States:** needs-you bar: needs → busy (spinner, 600 ms) → done (check mark, surface ground, Undo visible)
  → needs again on Undo. Rows: normal / hover / focus / selected / needs / restricted / free.
- **Empty:** no decision pending → the bar shows "Nothing needs you" in the done style; never hidden, so the
  layout does not jump.
- **Error:** Confirm can fail when the berth is no longer free (another assignment in the planner): the bar
  shows the `Assign` message in alert ink and stays in *needs*.
- **Loading:** data is in-memory; none (recorded gap, same as the rest of the app).
- **Reduced motion:** no creep, no roll, fades and slides resolve instantly (`Motion.Reduced`).
- **Accessibility:** the bar is a polite live region; rows are buttons with names ("Berth 07, Nordic Star,
  arriving 21:40, needs confirming"); the harbour summary (`HarbourView.UpdateSummary`) reports the decision.
- **Runtime verification:** Confirm → capture at 0 / 200 / 400 ms with a 60 s hot-reloaded duration; Undo;
  hover each row type via pointer; Tab through rows; reduced motion on; minute roll (wait for a minute boundary).

## Implementation Plan

1. `MotionTokens.xaml` + `RollingText` control; roll the header clock minutes. Build, verify, commit.
2. `PortState`: `HoveredVessel`, `NeedsDecision`, `Confirm` / `UndoConfirm`. Unit-free; verify via MCP datacontext.
3. Berths layout: needs-you bar, Berth status list, Selected vessel detail (static first, real data), responsive stack.
4. Linked hover both ways + keyboard focus; harbour outline in the live layer.
5. Camera pill slide + drag fade in `HarbourView`.
6. Harbour: route creep, `NeedsOpacity` fade on confirm/undo; row 07 in-place update.
7. Cargo bar slide; big-number roll in the detail.
8. Reduced motion pass, lint, gold-standard self-critique (`ui-craft` checklist), HANDOFF.

## Unresolved Questions

- Needs-you colour: amber (the app's existing *reserved · arriving*) or teal as in the artifact? Spec assumes amber.
- Placement: Berths page only, or also a compact needs-you bar on Overview?
- Confirm writes a real assignment (`_assigned`), which also changes the planner and conflict count. Intended?
- Header clock rolls minutes only; seconds keep ticking plainly. OK?
- Only Nordic Star can need a decision in the demo data; a general "decisions" queue is out of scope.

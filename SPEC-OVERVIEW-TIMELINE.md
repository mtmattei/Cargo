# SPEC: Overview "One timeline" redesign, app-wide restyle, Liveline tide lane

Written 2026-09-30. Source design: **A · One timeline (v6, interactive)**, copied to
`docs/design/overview-timeline/` (`overview-timeline.png`, `Main.dc.html`, `harbour-reference.jpg`).
Every token, size and string is in `docs/design/overview-timeline/EXTRACTION.md`; this spec cites
it as **[X §n]** rather than repeating values. Tide lane: the user's **Liveline** chart
(`github.com/mtmattei/Uno-Builds-net10/tree/main/Liveline`), extended for this lane.

**Task in one sentence:** replace the Overview body with one shared-axis timeline (vessels, tide,
moves, cargo), a general Needs-you queue, a Next 6 h list and a two-column Activity feed, restyle
the whole app to the design's blue palette and header, and keep the live 3D harbour.

**Supersedes** `SPEC-BERTHS-NEEDS-YOU.md` step 8 (the harbour-driven context panel) and its
decision "only Nordic Star can need a decision" and "needs-you is amber". Steps 1–7 of that spec
stay built; this spec restyles them.

## Decisions (user, 2026-09-30)

- **Liveline:** extend it in the user's repo for this lane (fixed time window, forecast segment,
  transparent background, no own grid/labels, app font, render loop that stops when settled);
  Cargo references it.
- **Scope:** app-wide: new palette, header and nav for every section; Overview rebuilt to the design.
- **Needs you:** a general queue (inspection hold, gate ID check, late orders, berth confirm).
- **Step 8:** replaced by this design.

## Capability inventory

| Capability (design implies) | Status | Note |
|---|---|---|
| Live clock, moving NOW, countdowns | **implemented**, extend | `PortState.NowHours` runs from 20:58; the prototype is frozen at 21:35. NOW, shading, filled-vs-hollow, countdowns and "late" durations all derive from the clock. |
| Harbour with camera presets, layers, zoom, expand | **implemented** (3D, live), restyle | The design's static image + CSS-only menus are a substitution *in the design*; the app keeps `HarbourView` and restyles its HUD and tags [X §2 Harbour]. |
| Linked highlight across map, chart, lists | **implemented**, extend | `PortState.Hover` + `HoverLink` (Berths step 4); add chart, Next 6 h and Activity sources. |
| Shared-axis timeline: vessels, moves, cargo lanes | **to build**, bespoke Skia | One `SKCanvasElement` for the three lanes + axis + NOW + shading. Substitution vs a chart library: see Unresolved Questions. |
| Tide lane | **to build** with **extended Liveline** | Aligned to the shared axis; past solid, forecast at 50%, HW and now markers. |
| Chart / Table toggle with an hourly table | **to build** | 25 rows, no virtualization needed (< 30). |
| Needs-you queue with actions | **to build** | `PortState.Decisions`; actions navigate to where the task is done, except Confirm berth, which acts. |
| Next 6 h with to-scale ship glyphs | **to build** | Glyph drawn from the recipe [X §2 Ship glyph]; lengths from `Vessel.Length`. |
| Activity feed, two columns, NEEDS ACTION chips | **restyle** `ActivityView` | Chips derive from open decisions. |
| Duty-operator card | **to build**, new data | `PortData.DutyOperator` (name, role, badge, clocked-in, shift end). |
| Account menu (avatar) | **to build** | Holds the reduced-motion toggle that the header loses. |
| Remaining Overview charts | **move** | Container volume and Cargo flow → Cargo; Sea & weather + tide → Waterways (from the replaced step 8). Vessel movements chart retired (the VESSELS lane replaces it). |
| Loading / empty / error | **recorded gap** | Data is in-memory and synchronous, as in the rest of the app. Empty states are specified below. |

## Architecture Brief

- **MVVM (CommunityToolkit.Mvvm), unchanged.** Project rule: no MVUX mid-project; `x:Bind` on every
  page. `PortState` remains the one store; the router builds view models off the UI thread, so new
  view models create brushes/collections inside `dispatcher.TryEnqueue` (gotcha, measured Cargo
  2026-09-29).
- **New in `PortState`:**
  - `Decisions`: `ObservableCollection<Decision>` (Id, Kind {InspectionHold, GateId, LateOrders,
    BerthConfirm}, Title, Sub, LateText, ActionLabel, IsPrimary, Command, VesselId?). Rebuilt on
    structure change, *mutated in place* on tick (late minutes). `NeedsYouCount`.
    Sources: inspection hold from `PortData.Containers`/security hold data ("CMAU 918204 4", due
    21:15); gate ID check from `PortData.Activity`/Vehicles (TRK 8834, since 20:41); late orders from
    `RiverFleet.SlipMinutes > 0` (2); berth confirm from `PendingDecision` (step 2).
  - `SectionNeedsYou(sectionId)` for the nav dots (Cargo: late orders; Security: hold + gate ID;
    Berths: berth confirm).
  - `DutyOperator` read-only from `PortData`.
- **New view models (Presentation/):** `TimelineViewModel` (lane summaries, NOW label, table rows,
  `IsTable`), `NeedsYouListViewModel` (shares `PortState.Decisions`), `NextSixHoursViewModel`
  (rows mutated in place, NOW-row position), restyled `ActivityViewModel` (two columns, chips).
  `OverviewViewModel` composes them.
- **Controls (Controls/):**
  - `TimelineLanes : SKCanvasElement`: vessels, moves, cargo, gridlines, axis, NOW line + pill,
    future shade; exposes hit-testing for vessel dots (link + focus). Draws text with the app's
    typefaces (as `HarbourScene` already loads them).
  - Liveline `LivelineChart` placed exactly over the tide lane's plot rect.
  - `ShipGlyph : SKCanvasElement` (one per Next 6 h row; static, invalidates only on hover lift).
  - `Segmented` behaviour for Chart/Table and the layer switcher: reuse the camera-pill technique
    (Berths step 5: Storyboard, `EaseSmooth`, re-target mid-slide). Generalise `HarbourView`'s
    `MovePill` into a small reusable helper only because three controls now need it.
- **Liveline (external, `C:\Users\Platform006\Uno-Builds-net10\Liveline`, branch `liveline-window`):**
  - Bump Uno.Sdk 6.5.36 → **6.7.30** (Cargo's pin, latest stable verified 2026-09-30); add
    `net10.0-android;net10.0-ios` (Cargo targets them; today Liveline is desktop + wasm only).
  - New properties: `WindowStart`/`WindowEnd` (x by time, not by index); `Forecast`
    (`IList<LivelinePoint>`, drawn at a forecast opacity); `NowTime` (solid/forecast split and live
    dot); `Background` (transparent allowed; today it `Clear`s an opaque palette background);
    `ShowAxisLabels`; `Typeface` (today hard-coded "Segoe UI"); `Markers` (point, filled/hollow,
    label); explicit `LineColor`/fill gradient stops (today derived from one hex).
  - **Render loop:** subscribe to `CompositionTarget.Rendering` only while `TickAnimation()` reports
    motion; unsubscribe when settled; resubscribe on any `PushState`. Gotcha (TransitionsGallery
    2026-08-17): a held `Rendering` subscription forces frames at display refresh, ~50-70% of a core,
    which on Overview (the start page) would erase the measured idle budget.
  - Reduced motion: `LerpSpeed = 1` (snap) when Cargo's `Motion.Reduced`.
  - Keep existing behaviour as the default so the demo and other consumers are unchanged.
  - Cargo references it by `ProjectReference` to the clone (outside OneDrive, per project rule).
- **Theme (app-wide):** `Themes/Tokens.xaml` swaps teal for the design palette [X §1 Colours]:
  new keys `AccentBrush` family (`#1C6E9E`, dark `#155A82`, hover `#185F88`), `NavyBrush`
  (`#0F4A6E`), `WaterBrush` (`#5598C4`), `AlertBrush`/`AlertInkBrush` (`#C9553B`/`#B04A32`),
  ground/surface/hairlines per [X §1]. All stay `*Invariant` (light-only app; TOKENTHEME lint).
  Status mapping changes: **reserved/arriving → accent blue** (harbour polygon, route, legend, key);
  **needs-you → alert** (Berths bar ground becomes the alert tint; amber retires from "needs you").
  Teal keys are removed once no reference remains (grep gate).
- **Data flow:** clock tick → `PortState.Ticked` → timeline NOW/shade/labels, Next 6 h countdowns,
  decision late-minutes, duty progress; structure change → rebuild lanes/decisions. All panels
  follow the store only while Overview is shown (`Live.RebuildWhenVisible`/`TickWhenVisible`).
- **Platform:** Skia renderer everywhere. `SKCanvasElement` ignores `UIElement.Opacity` of itself and
  ancestors (gotcha G38): the Chart→Table cross-fade cannot fade the canvases; fade the table in over
  a canvas that is collapsed, or bake alpha into the renderers.
- **Validation:** `dotnet build -f net10.0-desktop` 0 warnings; lint 0 gating; uno-app MCP: every
  section at 1680×1020 and 1100×900, Overview at 1440 compared against `overview-timeline.png`;
  Liveline demo runs after the extension; CPU idle on Overview measured before/after (budget: no
  regression from the 48-53% baseline, handoff 2026-09-30).

## Design Brief

- **Direction:** the design's: calm surface panels on a warm ground, ink type, one accent blue,
  colour only for status (navy happened/vessel, water blue tide/out, alert red needs-you).
- **Layout (1440 artboard):** header 56 → masthead (padding 20 40) → full-bleed harbour (340,
  expanded 491) → main (max 1440, padding 32 40 56, 12-col grid, gap 24): Timeline span 8, aside
  span 4 (Needs you over Next 6 h), Activity span 12 [X §2].
- **Typography:** Archivo / Bricolage 600 / IBM Plex Mono (already shipped); add **IBM Plex Mono
  SemiBold** (the design uses mono 600 in four places; static TTF per the variable-font gotcha).
  Every size becomes a named style in `Styles.xaml` (no inline `FontSize`, audit 2026-09-30); the
  ~130 legacy inline sizes in other sections migrate in the restyle step.
- **Spacing/radii/shadows:** [X §1]. Panels: `utu:CardContentControl` with lightweight keys
  (radius 12, padding 24, surface, 1px ring); floats: `utu:ShadowContainer` with the three-layer
  float shadow set inline per instance (Toolkit gotcha G36); the duty card uses the ID-card stack.
- **Component hierarchy:** Header (wordmark, tabs with alert dots, clock, account button) →
  Masthead (greeting, date, figures, duty card) → HarbourView (restyled HUD: camera dropdown, layer
  segmented, Key flyout, scale+zoom group, Expand; tags: light + dark-for-pending) → Timeline panel
  (head, toggle, legend, lanes or table) → Aside (Needs you section, divider, Next 6 h) → Activity.
- **Responsive:** `{utu:Responsive}` with an Overview `ResponsiveLayout` (Normal 1040, Wide 1280):
  ≥1280 as designed; 1040–1279 aside drops below the timeline (both span 12), Activity stays two
  columns; <1040 Activity one column, masthead figures wrap under the greeting, duty card hidden
  into the account menu. Timeline plot width scales; px/h derives from the plot width, never fixed.
- **Other sections:** inherit palette, header and type; no layout redesign beyond what the palette
  and type migration forces.

## Interaction Brief

| Area | Behaviour | Numbers |
|---|---|---|
| Linked highlight | Hover or keyboard focus on a vessel anywhere (map tag/anchor, chart dot/label, Next 6 h row, Activity row) highlights it everywhere: row tint, glyph lift, tag lift + shadow, chart dot 5→7, anchor 5.5→8, label → ink | 200 ms `EaseSmooth`; glyph lift 1.5px 300 ms; tag lift 2px |
| Needs-you figure | Hover/focus rings the Needs-you section (inset 2px alert .35); click scrolls it into view and focuses its heading | ring 200 ms |
| Needs-you actions | Open inspection → Security · Inspection; Verify driver → Security · gate check (person); View orders → Waterways (late filter); Confirm berth → acts in place (Berths step 3 flow) | press .96 / 150 ms |
| Chart / Table | Segmented pill slides; incoming view fades in (table only; canvases switch, gotcha G38) | pill 260 ms, fade 200 ms |
| NOW | Moves with the clock; future shade, solid→forecast split, filled→hollow flips and Next 6 h NOW row follow; Next 6 h window is [now − 15 min, now + 6 h] so a just-departed ship stays above the NOW row | per tick, no animation; countdowns roll each minute (RollingText) |
| Expand | Harbour 340→491 | 320 ms `EaseSmooth`; icon cross-fade 300 ms |
| Camera dropdown | `MenuFlyout` placed above, `RadioMenuFlyoutItem`s: Overview / From sea / From land / Plan; drives the existing camera presets; drag clears the check (no preset) | flyout rise 180 ms |
| Layer segmented | Map / Yard / Security / Traffic drive the existing harbour layers | pill 260 ms |
| Key | Flyout with the five swatches [X §2 Key] | 180 ms |
| Duty card | Entrance once per launch; hover straightens and lifts | 700 ms after 250 ms; hover 300 ms |
| Wake | Departing ship glyph wake breathes | 3.6 s loop, only while shown and motion on |
| Tabs | Hover ink + 2px .16 underline; active 2px accent | 160 ms |

- **States:** decision rows: open / late (alert text) / resolved (row leaves the list, Activity
  logs it). Timeline: vessel happened/planned; moves/cargo have no forecast (footer copy says so).
- **Empty:** Needs you with 0 → section keeps its height, ground drops the tint, text "Nothing needs
  you", badge hidden; Next 6 h with 0 movements → "No movements in the next 6 h"; Activity 0 → "No
  activity yet today".
- **Loading/Error:** none (in-memory data) — recorded gap, same as the rest of the app.
- **Reduced motion:** every transition resolves instantly (`Motion.Reduced`); wake and duty-card
  entrance off; Liveline snaps; RollingText swaps.
- **Accessibility:** chart canvas carries the generated `<desc>`-style summary as its automation
  name and a polite live region updating on structural change; the Table view is the full
  non-visual alternative; vessel dots are keyboard-focusable (Tab order left→right); Needs-you
  count announced ("3 items"); nav dots carry ", needs you"; focus ring 2px accent offset 2.
- **Runtime verification:** each section after the palette swap; Overview at 1440×1708-equivalent
  capture vs `overview-timeline.png`; Chart↔Table; hover links via keyboard focus (MCP has no
  pointer-move); Needs-you actions navigate; Confirm berth from Overview; Expand; camera menu and
  layers; NOW at a minute boundary; reduced motion on; 1100 and 1680 widths; idle CPU measured.

## Implementation Plan

0. Commit the pending uno-audit working tree (7 files, awaiting the user's go).
1. **Liveline**: clone to `C:\Users\Platform006\Uno-Builds-net10`, branch `liveline-window`; Uno.Sdk
   6.7.30 + android/ios TFMs; add window/forecast/now/background/typeface/markers/colours; render
   loop that stops when settled; demo page shows a windowed forecast example. Build + run demo,
   measure idle CPU with the chart settled. Commit on the branch (push only when asked).
2. **Tokens + type**: palette swap, new styles, Plex Mono SemiBold; status mapping (reserved→blue,
   needs-you→alert); migrate inline font sizes section by section; lint + every section captured.
3. **Header + account menu**: wordmark, tabs with alert dots, clock, avatar `MenuFlyout` (reduced
   motion, operator). Remove the ISPS/weather/tide header badges (see Unresolved).
4. **Masthead**: figures + duty card (+ `PortData.DutyOperator`).
5. **Harbour restyle**: tags (light / dark for the pending arrival), anchor rings, reserved polygon +
   route in accent blue, HUD (camera MenuFlyout, layer segmented, Key flyout, scale+zoom, Expand
   340/491).
6. **Decisions queue**: `PortState.Decisions`, nav dots, Needs-you panel; Berths bar restyled to the
   alert tint.
7. **Timeline**: `TimelineLanes` canvas + Liveline tide lane aligned to the plot rect; summary
   column; legend; Chart/Table toggle; Table view.
8. **Next 6 h** with `ShipGlyph` and NOW row; **Activity** two columns + chips; move Container
   volume / Cargo flow / Sea & weather to their sections; retire the Overview movements chart.
9. **Linked highlight** everywhere, motion pass, reduced motion, accessibility, responsive bands,
   lint, CPU measurement, HANDOFF.

## Risks (time-boxed spikes)

- **Liveline on Android/iOS with SkiaRenderer** (15 min): add TFMs, build both heads.
- **Liveline under a ProjectReference across repos** (10 min): SDK resolution picks the referenced
  project's `global.json`; both must be 6.7.30.
- **`MenuFlyout` placement Top + RadioMenuFlyoutItem styling on Uno Skia** (10 min); fallback:
  Flyout with a radio list.
- **Canvas text typefaces** (10 min): reuse `HarbourScene`'s font loading for `TimelineLanes`,
  `ShipGlyph` and Liveline.
- After a spike's time box, trip the two-failure Debug Checkpoint.

## Unresolved Questions

- **Lanes substitution:** vessels/moves/cargo are drawn in a bespoke `SKCanvasElement`, not a chart
  library (LiveCharts2 is the first choice in `capability-coverage.md`). Reason: four lanes share one
  time axis, one NOW line, a future shade and cross-lane linked hover, and the app's other charts are
  already bespoke. Accept, or evaluate LiveCharts2 first?
- **Header badges:** the design drops ISPS level, weather/sea and tide from the header. Move them to
  Waterways (weather, tide) and Security (ISPS), or keep a compact version in the header?
- **Reduced-motion toggle** moves into the account menu (the design has no header slot). OK?
- **Berth confirm in the queue** makes the count 4 at 20:58 (hold, gate ID, late orders, berth),
  while the design shows 3 at 21:35. Keep it in the queue?
- **Needs-you actions navigate** (they open the screen where the task is done) except Confirm
  berth. Should Verify driver / View orders resolve in place instead?
- **Duty operator** "Elin Lindqvist, WH 0417, in 21:31 until 06:00" is new demo data, and 21:31 is
  after the app's 20:58 start. Shift the clock-in to 18:00 (progress meaningful from the start)?
- **Tide "now" marker:** the design places it at Nordic Star's arrival (21:40, "1.8 m as Nordic Star
  arrives"). Pin it to the next arrival, or to NOW?
- **Liveline upstream:** push the `liveline-window` branch and open a PR to Uno-Builds-net10, or keep
  it local until Cargo ships?
- **Pending uno-audit commit** (7 files): commit before step 1?

# Westhaven Dispatch (Cargo)

An Uno Platform implementation of the **Quay Port v4** design
([Claude Design project](https://claude.ai/design/p/254ce48f-20f1-482a-bfa8-c813390c0840)) — a port
operations dashboard for the fictional port of Westhaven.

All eight sections of the design are implemented:

| # | Section | What it does |
|---|---------|--------------|
| 01 | Overview | Greeting, live counters, vessel-movement ribbon, next-up list, throughput blocks, cargo flow, sea & tide |
| 02 | Fleet & waterways | 120 km river map with chainage, buoys, gauges, locks and notices; lockings, water levels, crew chat |
| 03 | Vessels | Interactive side profile with a clickable bay grid, live crane scene, cargo composition, voyage and clearances, bay detail |
| 04 | Docking | Berth map with drag-to-assign, 36-hour timeline scrubber, conflict detection and a berth optimiser |
| 05 | Containers | Filterable explorer, journey tracker, and a full-window 3D inspection view with X-ray |
| 06 | Yard | Live block map — stack heights shift as the yard works, click a slot for its tiers |
| 07 | Security | Zone map and event timeline, access control per badge, and the cargo-inspection sheet |
| 08 | Activity | Throughput curve and the day's event feed |

## Running it

```
dotnet build Cargo/Cargo.csproj -f net10.0-desktop
dotnet run   --project Cargo/Cargo.csproj -f net10.0-desktop
```

Verified building for `net10.0-desktop`, `net10.0-browserwasm` and `net10.0-android`.
iOS is in the target list but needs a paired Mac to build.

## Architecture

- **Uno.Sdk 6.8.0-dev.23**, single project, Skia renderer, `SimpleTheme` + Uno Toolkit.
- **MVVM (`CommunityToolkit.Mvvm`)**, not MVUX. Every figure in this app is an in-memory
  constant; there is no async or reactive data flow for feeds to model. What the app
  actually has is a lot of imperative selection state — which berth is hovered, which bay
  is open, where a dragged hull currently is — which is what MVVM is good at.
  `Domain/PortState.cs` is the single observable store, and it raises four separate signals
  so that work is proportional to what actually changed: `StructureChanged` for selections
  and assignments, `Ticked` for the clock, `HoverChanged` for the pointer, and `YardChanged`
  for the yard's own live moves. Hover and yard moves fire orders of magnitude more often
  than anything else and are handled as repaints of the affected element, not rebuilds — a
  berth hover used to tear down and rebuild the entire harbour.
- **Design tokens** live in `Themes/Tokens.xaml`; `Domain/Tokens.cs` resolves them for code
  so no colour literal appears outside that one file.
- **Scenes** (harbour, river, yard, berth map, charts, vessel profile) are drawn into a
  fixed design-space `Canvas` hosted by `Controls/SceneHost.cs`, which scales it to the
  available width — the WinUI equivalent of an SVG `viewBox`. The source design is SVG
  throughout, so this keeps every coordinate in the file identical to the design's.

### Notable pieces

- `Controls/ContainerScanner.cs` — WinUI has no CSS-style 3D transforms, so the inspection
  box is projected by hand: eight corners rotated about Y then X, divided through by a
  1500-unit perspective, faces painted back to front. Drag rotates it.
- `Controls/BerthMap.cs` — pointer-driven berth assignment. A drop is validated against
  draft versus berth depth and against the other vessels' occupancy windows, so a berth
  that cannot take the ship lights red.
- `Controls/VesselProfile.cs` — the bay grid is laid over the hull render, positioned from
  the deck rect measured inside the sprite, and each bay column is its own hit target.
- `Presentation/ShellPage.xaml.cs` — sections are built once and cached. Unvisited ones are
  built on idle turns after the shell loads, because paying a section's construction cost on
  the click is what made moving to a new section feel like a stall.

## Artwork

The scenes are the design's own rendered artwork, in `Cargo/Assets/Sprites/`:
the terminal strip, water tile, crane bases, plan and side views of the hulls, the container
renders, the quay crane and the river buoys. Every one is placed with the rect the design
uses, in the same coordinate space, so a berth and the ship alongside it keep their exact
relationship at any window size (`Controls/Sprites.cs` holds the placement helpers, the
intrinsic sizes and the vessel-to-artwork mapping).

Where a hull sprite is overlaid with interactive geometry — the bay grid on the vessel
profile — the deck rect is measured from inside the sprite (`Sprites.Hull`) and everything
scales off that, so the columns land on the containers the artwork actually shows.

A handful of things stay vector because the design draws them that way too: the yard block
map, the yard tractor, the security zone plan and the chart furniture.

## Deviations from the design, and why

**Fonts.** The design specifies Instrument Sans / IBM Plex Mono / Bricolage Grotesque.
Plex Mono and Bricolage were available locally; Instrument Sans was not, and the sandbox
blocks the font download. **Archivo** stands in for it — same neo-grotesque lineage, and the
three weights the design uses (400/500/600) all exist as static instances. Static instances
are deliberate: variable-font axes do not resolve on the Uno Skia text stack.

**Letterspacing.** The design tracks its display type at −0.035em. `CharacterSpacing` is a
silent no-op on the Skia text stack, so the display sizes are set without it rather than
faked with per-character layout.

**Motion.** The design's CSS keyframe loops (crane hoist, yard RTGs, tractors, dash marching)
are driven by `DispatcherTimer` rather than `RepeatBehavior="Forever"` storyboards, which pin
the Skia compositor at display refresh and cost roughly a third of a core each, even off
screen. The timers stop on `Unloaded`, and they run at 10 fps: an RTG covers 330 px in nine
seconds and a tractor 1180 px in twenty-two, so the step is a few pixels and the motion reads
the same, while the scene behind it is re-composited half as often.

## Verification

`tools/Capture-Window.ps1` captures the window with `PrintWindow` (occlusion-proof, with a
screen-copy fallback when the GL surface hands back a blank bitmap). Screenshots of every
section are in `shots/`.

Because Windows will not let a background process drive the foreground window, panels that
only exist after an interaction are reachable through DEBUG-only environment hooks so a
headless run can photograph them:

| Variable | Effect |
|---|---|
| `APP_NO_HOTDESIGN=1` | Skips `UseStudio()`, which otherwise blocks window creation with no DevServer |
| `CARGO_START_SECTION` | Opens straight onto a section (`overview`, `fleet`, …) |
| `CARGO_SECURITY_TAB` | `zones`, `access` or `inspection` |
| `CARGO_DOCK_SELECT` | Vessel id to preselect in the docking planner |
| `CARGO_STACK` | Yard slot key, e.g. `A-1-2` |
| `CARGO_SCANNER=1`, `CARGO_XRAY=1` | Opens the container inspection, optionally in X-ray |
| `CARGO_LAYER` | Harbour overlay: `port`, `yard`, `security`, `traffic` |
| `CARGO_OPTIMIZE=1` | Runs the berth optimiser at startup |
| `CARGO_SCROLL` | Scrolls the section body by N pixels, to photograph a panel below the fold |
| `CARGO_HOVER_BERTH` | Berth index 0–7 to show hovered, for the harbour highlight and its label |

Startup failures are written to `startup.log` beside the executable — a desktop Uno app is a
windowed process, so redirected stdout captures nothing.

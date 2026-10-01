# Overview recording plan

Rehearsed 2026-10-01 via the uno-app MCP at 1680x1020, `net10.0-desktop` Debug, `main` at 8e45a3d.
Story: the evening duty operator takes the harbour over at shift change, reads it, and clears what needs her.

## Before each take

- Relaunch the app for every take. Confirm berth is one-way, so state only resets on launch: the clock starts at
  20:58, Needs you at 4.
- Record without the dev toolbar (it covers the "Westhaven" wordmark in Debug). Run
  `$env:APP_NO_HOTDESIGN='1'; .\bin\Debug\net10.0-desktop\Cargo.exe`, or a Release build (not yet verified).
- Window: 1680x1020 (the rehearsed layout) or 1920x1080. Anything at or above 1280 wide keeps the side-by-side layout.
- Capture: OBS window capture, Windows 10 (1903+) method. Do one 10-second test take first, because GDI region and
  title capture can return black for the Skia GL window.
- Leave a beat (about 1 s) after every click. The page has occasional 100-200 ms hitches, and animations that land on one stutter.
- Do not touch Reduce motion in the account menu. Do not select a vessel tag before beat 9.

## Beat script

Say the line, then do the action. Never talk while clicking.

### 1. Arrive (spine: "This is the harbour as it is right now.")

| # | Say | Do | Point at |
|---|---|---|---|
| 1 | "Evening shift. One screen tells me what the harbour is doing." | Hold on the page for 3 s after launch | The greeting and the duty card dropping in |
| 2 | "Three alongside, two inbound, and four things waiting on me." | Hover the duty card (it lifts 2 px) | Alongside 3, Inbound 2, Needs you 4 |

Segue: "Start with the water."

### 2. Read the harbour (spine: "The harbour is live, to scale, and I can look at it any way I need.")

| # | Say | Do | Point at |
|---|---|---|---|
| 3 | "Nordic Star has a pilot aboard, forty-two minutes out." | Nothing | The dark Nordic Star tag |
| 4 | "I can look from the sea side..." | Camera menu, then From sea | The camera flying round |
| 5 | "...and back to the overview." | Camera menu, then Overview | Label back to Overview |
| 6 | "The yard, security and traffic are layers on the same map." | Yard, wait, Security, wait, Traffic, wait, Map | The pill sliding; blocks, fence, lanes |
| 7 | "The key explains every mark." | Key, hold 2 s, Escape | The legend |
| 8 | "More room when I need it." | Expand, hold, Collapse | The band growing 340 to 491 |
| 9 | "And any ship is one click from its facts." | Click the Nordic Star tag, hold 3 s, click it again | Facts card; the timeline dot enlarges |

Live-only (the MCP cannot hover or drag; do these with the mouse): drag to orbit the harbour, mouse-wheel zoom.
After beat 9 the camera stays close. Choose Overview in the camera menu or move on.

Segue: "Now the rest of today."

### 3. Read the day (spine: "Everything that happened and is about to, on one time axis.")

| # | Say | Do | Point at |
|---|---|---|---|
| 10 | "Vessels, tide, crane moves and cargo, on one axis. Solid has happened, open is still to come." | Hover a vessel dot on the timeline (lights its harbour tag) | The NOW line at 20:58 |
| 11 | "The same hours as a table, when I need numbers." | Table, hold 2 s, Chart | The rows |

Segue: "Four things need me."

### 4. Act (spine: "What needs me is ranked, and each one is a single action.")

| # | Say | Do | Point at |
|---|---|---|---|
| 12 | "Nordic Star needs her berth confirmed." | Confirm berth | Row leaves; 4 becomes 3 in both counts; the Berths dot clears; the tag turns light, "arriving 21:40" |
| 13 | "The hold on this container goes straight to its evidence." | Open inspection, hold 3 s on the inspection page, then the Overview tab | Weight variance +1.8 t |

Segue: "And the log of the shift so far."

### 5. Look back (spine: "The shift log, with what still needs action marked.")

| # | Say | Do | Point at |
|---|---|---|---|
| 14 | "The next six hours, ships drawn to scale." | Scroll to Next 6 h; hover a row (lights its tag) | The ship glyphs |
| 15 | "Every entry opens in place." | Scroll to Activity, click Driver ID pending at Gate 3 | The row lifting, the column softening, "Logged 20:41 · 17 min ago" |
| 16 | "Closing it puts the list back." | Click the same row | The default list |

Close on beat 16, or end with "Verify driver" (it drills into Security > Access control) if the take runs long.

## Risks found in rehearsal

| Risk | Status | In the take |
|---|---|---|
| Dev toolbar covers the wordmark in Debug | Open | Record with `APP_NO_HOTDESIGN=1` or Release |
| A Next 6 h row opens Berths scrolled mid-page, with the selected row off-screen | Open (bug) | Do not click Next 6 h rows; hover only |
| Selecting a vessel leaves the camera close, and the menu still reads "Overview" | Open (bug) | Beat 9 is last in its section; re-pick Overview |
| Plan preset hides the Nordic Star tag (in the bottom HUD margin) | By design | Skip Plan |
| The facts card covers most of Nordic Star's hull | Cosmetic | Hold only 3 s |
| Key and zoom group shifts about 40 px when the scale bar changes width | Cosmetic | Avoid the zoom buttons; use the camera menu |
| The masthead Needs you button only scrolls, so it shows nothing when the queue is already in view | By design | Skip |
| Page hitches (idle p99 206 ms) | Open (perf) | Pause after each click; retake if a transition stutters |
| Activity detail opened empty | Fixed in 8e45a3d | Shown working in rehearsal |

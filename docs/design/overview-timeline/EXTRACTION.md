# Design extraction: Westhaven Overview, "A · One timeline (v6, interactive)"

Extracted 2026-09-30 from `Main.dc.html` (this folder) and the 2× capture `overview-timeline.png`
(2880×3416 = the 1440 artboard). Artboard 1440 × 1708. `harbour-reference.jpg` (1917×654) is the
design's static harbour image; the app keeps its live 3D harbour instead (see the spec).

**The prototype has no script logic.** Every interaction is CSS (`:has()`, hidden radios and
checkboxes, `<details>`). The clock and NOW are fixed at 21:35. Camera menu, layers and zoom are
visual only.

## 1. Tokens

### Colours
| Value | Role | Used for |
|---|---|---|
| `#F3F3F1` | ground | page background; NOW pill text; selected segment text; harbour top fade |
| `#FBFBFA` | surface | header, panels, light tags, HUD floats, popovers, secondary button; halo stroke behind chart text; white ring on dots; hollow marker fill |
| `#121617` | ink | text, figures; dark tag; NOW line and pill; segmented indicator; map leaders; scale bar |
| `#5E6569` | muted | secondary text, axis labels, lane labels, planned labels, inactive tabs, inactive icons |
| `#155A82` | accent dark | "Operations." in the greeting; links |
| `#1C6E9E` | accent blue | active-tab underline, avatar, primary button, focus ring (2px, offset 2), menu check, duty progress, MOVES line and dots, reserved-berth polygon and approach route |
| `#185F88` | accent hover | primary button hover |
| `#0F4A6E` | navy (vessel / happened) | alongside dot, filled happened markers, hollow-marker ring, cargo **in** line, hulls, map anchor dots, VESSELS key |
| `#5598C4` | water blue | TIDE line and area, cargo **out** line, cargo band, waterline and wake |
| `#C9553B` | alert (icon/marker) | nav needs-you dots, needs-you square marker, alert icons |
| `#B04A32` | alert (text) | needs-you figure, late text, count badge background |
| `rgba(201,85,59,.05)` | alert ground | Needs-you section tint |
| `rgba(201,85,59,.12)` | alert chip | NEEDS ACTION chip |
| `rgba(176,74,50,.35)` | alert ring | inset 2px ring on Needs-you when the masthead figure is hovered/focused |
| `#C9CED1` | on-dark muted | second line of the dark tag |
| `#FFFFFF` | on-accent | avatar initials, primary button text, badge text |
| `#F2F3F2` | secondary hover | secondary button hover |
| `rgba(18,22,23,.08)` | hairline | card rings, header inset, dividers, gridlines, progress track |
| `rgba(18,22,23,.18)` | hairline strong | masthead divider, toggle ring, secondary ring, Thu 00:00 gridline, y=300 divider, vessel baseline/leaders, ticks, timeline rail |
| `rgba(18,22,23,.03)` | ahead-of-now shade | chart future region, table future rows |
| `rgba(18,22,23,.05)` | ghost hover | |
| `rgba(18,22,23,.16)` | tab hover underline | |
| `rgba(18,22,23,.34)` | secondary hover ring | |
| `rgba(28,110,158,.07)` | linked-row tint | |
| `rgba(28,110,158,.12)` | reserved berth fill | map polygon and Key swatch |
| `#1C6E9E #5598C4 #D3D8DA #C4CBCE #8A9399 #B9C0C4 #A3AFB6` | container fills (ship glyphs) | bridge `#E7EAEB`, stroke `rgba(18,22,23,.35)` .75 |

Gradients: moves area `#1C6E9E` .2 → 0 (vertical); tide area `#5598C4` .3 → .04; harbour top
fade `#F3F3F1` → transparent, 40px.

### Fonts
Archivo 400/500/600; Bricolage Grotesque 600; IBM Plex Mono 400/500 (mono 600 used in four places
but not loaded: "111 / h", NOW-row values, count badge).

| Role | Size / line height | Weight | Other |
|---|---|---|---|
| Body | 15 / 1.45 | 400 | |
| Wordmark (Bricolage) | 19 / 1 | 600 | -.01em |
| H1 (Bricolage) | 40 / 1.1 | 600 | -.02em |
| Panel H2 | 18 / 1.2 | 600 | |
| Nav active / inactive | 14 / 1 | 600 / 500 | |
| Figures | 30 / 1 | 600 | |
| Figure labels | 13 / 16 | 400 | |
| Subtitles | 13 / 1.4 | | muted |
| List titles: Needs you / Next 6 h / Activity | 15 | 600 / 600 / 500 | |
| Caps labels (Key header, lanes, table heads) | 11 | 600 | .08em uppercase |
| Chip | 11 | 600 | .06em uppercase |
| Chart big values | 20 | 600 | |
| Chart sub lines | 12 | 400 | |
| Chart vessel labels | 12 | 500 | |
| Mono: header clock | 14 / 1 | 500 | |
| Mono: list times | 13 / 20 | 500 (Activity 400 muted) | |
| Mono: tag sub, axis | 12 | 400 | "Thu 00:00" 500 ink |
| Mono: badge "WH 0417" | 11 | 500 | .04em |
| Mono: NOW pills | 11–12 | 500 | .04em |
| Mono: table values | 13 / 1 | 400–600 | tabular |

### Radii
2 (square marker, swatches, clip slot) · 1.5 (progress, lane keys) · 4 (chip) · 7 (NOW pill) ·
8 (buttons, segments, tags, menu items, row highlight) · 10 (HUD floats, segmented containers,
Expand) · 12 (panels, popovers, badge) · 50% (dots, avatars).

### Shadows
- Card ring: `0 0 0 1px rgba(18,22,23,.08)`
- Float: ring + `0 1px 2px .06` + `0 6px 20px .07`; hover: ring .12 + `0 2px 4px .08` + `0 10px 24px .10`
- ID card: ring + `0 1px 2px .06` + `0 12px 28px .08`; hover ring .12 + `0 2px 4px .08` + `0 16px 32px .12`
- Dark tag: `0 6px 20px .18`; tag linked-hover: ring .14 + `0 12px 28px .16`
- Header: `inset 0 -1px 0 .08`; active tab: `inset 0 -2px 0 #1C6E9E`

### Heights
Header 56 (tabs 56, padding 0 12) · avatar hit 44 / circle 32 · HUD summary buttons 44 · zoom
44×44 · Expand 40 · segments 40 · menu items 40 · Needs-you buttons 44 (padding 0 16) · map tags
30 · table header 32 / rows 28 / NOW row 32.

### Motion
`cubic-bezier(.2,0,0,1)`; buttons scale .15s ease-out, background .16s, shadow .16s, colour .2s;
`:active` scale .96.

## 2. Layout, top to bottom

**Header** 56, `#FBFBFA`, bottom inset hairline, gap 28, padding `0 28 0 40`. Wordmark
"Westhaven". Tabs (padding 0 12, no gap): Overview (active, ink 600, 2px `#1C6E9E` underline),
Berths, Cargo●, Waterways, Security● (inactive muted 500). ● = 6×6 `#C9553B`, SR text
", needs you". Right (gap 12): `<time>` mono 14/500, avatar button 44 (32px `#1C6E9E`, "EL" 600 12
white, aria "Account and settings: E. Lindqvist").

**Masthead** padding `20 40 20`, gap 24, space-between. Left (gap 8): H1 "Good evening, " +
"Operations." `#155A82`; "Wednesday 2 September · Westhaven" 15/1.4 muted. Right (gap 40):
figures (gap 28, padding-top 4; label row gap 6 13/16 muted over 30/600 value):
Alongside (8px filled `#0F4A6E`) "3"; Inbound (8px ring 1.6 `#0F4A6E`) "2"; 1px `.18` divider;
Needs you (8×8 r2 `#C9553B`) "3" in `#B04A32`, a link to the Needs-you section. Duty card: width
252, padding `18 16 14`, r12, `#FBFBFA`, ID-card shadow, `rotate:-1.2deg` origin top; clip slot
28×4 at top 7 (fill ground, inset ring .18); top row grid `36 | 1fr | auto` gap 12: 36px avatar
"EL" 600 13, "Elin Lindqvist" 600 14/1.2 over "Duty operator · in 21:31" 400 12 muted (gap 3),
"WH 0417" mono 500 11 .04em muted; bottom row (margin-top 12, gap 10): track 3px r1.5 hairline,
fill `#1C6E9E` (placeholder 2.0%), "until 06:00" mono 400 11 muted.

**Harbour stage** full bleed. View 340 tall (expanded 491); image layer aspect 1917/654, collapsed
`translateY(-110)`. Top fade 40. Overlay: reserved berth polygon fill `rgba(28,110,158,.12)`,
stroke `#1C6E9E` 1.5 dash `5 4`; approach route `#1C6E9E` 2 dash `6 5` round. Vertical leaders ink
1.25 ending in anchor rings r5.5: alongside/happened filled `#0F4A6E` with `#FBFBFA` stroke 2;
inbound hollow (`#FBFBFA` fill, `#0F4A6E` 1.6). Light tags: r8, surface, float shadow, padding
`0 10`, height 30, gap 8; name 600 13 ink + sub mono 400 12 muted ("MSC Aurora" / "B04 ·
discharging", "Baltic Crown" / "B06 · departing"). Dark tag (the pending arrival): `#121617`,
text `#FBFBFA`, padding `8 12`, r8, dark-tag shadow, gap 3; row 1 "Nordic Star" 600 13/18 +
"in 5 min" mono 500 12/18 (space-between, gap 16); row 2 "B07 · pilot aboard" mono 400 12/1.3
`#C9CED1`.

HUD (floats `#FBFBFA` + float shadow): **Expand** top-right (right 40, top 16) h40, padding
`0 14 0 12`, r10, gap 8, 500 13, 18px icon (expand `M14 4h6v6M10 20H4v-6M20 4l-6.5 6.5M4 20l6.5-6.5`
/ collapse `M20 10h-6V4M4 14h6v6M14 10l6.5-6.5M10 14l-6.5 6.5`, stroke 1.6), "Expand"/"Collapse".
**Camera dropdown** bottom-left (left 40, bottom 16): summary h44, padding `0 14 0 16`, r10, gap 8,
500 14, "Overview" + 16px chevron `M7 10l5 5 5-5`; panel opens upward (bottom 52), min-width 220,
padding 6, r12; items h40 padding `0 10` r8 14px: Overview (600 + check `M5 12.5l4.2 4.2L19 7`
`#1C6E9E` 2), From sea, From land, Plan. **Layer segmented** bottom-centre: padding 2, r10,
indicator ink r8 h40; segments h40 r8 gap 8 500 14 with 18px icons: Map 88
(`M9 4 3.5 6v14L9 18l6 2 5.5-2V4L15 6z` + `M9 4v14M15 6v14`), Yard 88 (rect 3.5,7 17×11 rx1 +
`M3.5 12.5h17M9.2 7v11M14.8 7v11`), Security 116 (shield
`M12 3.2 19 6v5.2c0 4.4-2.9 7.5-7 8.9-4.1-1.4-7-4.5-7-8.9V6z`), Traffic 104 (`M4 12h15` +
`M14 7l5 5-5 5`); selected text `#F3F3F1`. **Bottom-right** (right 40, bottom 16, gap 8): **Key**
summary h44; panel right-anchored, bottom 52, min-width 220, padding 6, r12; header "KEY" 600 11
.08em muted padding `8 10 4`; rows gap 10 padding `8 10` 13/1.3: 9px filled navy "Vessel
alongside"; 9px ring "Vessel inbound"; 14×12 r2 reserved fill + 1.5 dashed `#1C6E9E` "Berth
reserved for an arrival"; 14×12 hairline fill + 1.5 dashed muted "Restricted"; 14px top-border 1.5
dashed muted "Depth contour, metres". **Scale + zoom group** r10 clipped: scale cell padding
`0 12 0 14` gap 8 mono 500 12, bar 64×6 1.5 ink no top, "250 m"; 1px dividers; zoom out / in 44×44
(`M6 12h12`, `M6 12h12M12 6v12`).

**Main** max-width 1440, padding `32 40 56`, gap 24; 12-col grid, gaps 24. At 1440: span 8 =
898.67 (panel inner 850.67), span 4 = 437.33.

**Today at North Quay** (span 8): surface, r12, ring, padding 24, gap 16. Head: H2 + "Wed 06:00 →
Thu 06:00 · live" 13 muted; **Chart/Table toggle** radiogroup: padding 2, r10, no fill, inset ring
.18, indicator 72×40 r8 ink, labels 72×40 500 13. Legend (gap `8 20`, 13 muted, item gap 8): 10px
filled navy "Happened", 10px ring "Planned", 16×12 hairline rect "Ahead of now".

Chart (viewBox 851×580): summary column x 0–168; plot x 168–839, **27.958 px/h**,
`x = 168 + hoursSince06:00 × 27.958`; plot y 28–540. Future shade from NOW x to 839, fill .03.
Gridlines every 3h (x 168, 251.9, 335.8, 419.6, 503.5, 587.4, **671.2 Thu 00:00 at .18**, 755.1,
839) at .08. Lane dividers y 164 (.08), **300 (.18)**, 428 (.08). Lane header: key bar 12×3 r1.5
at (0, T+8); label at x16, y T+14 (11 600 muted .08em); big value y T+40 (20 600); sub lines y T+60
and T+77 (12 muted). Lane tops 28 / 164 / 300 / 428. CARGO has two key bars (navy at x0, water at
x16), label at x32. All chart text has a 4px `#FBFBFA` halo.

- VESSELS: baseline y92 (.18); arrivals above (dot cy 84), departures below (cy 100); dots r5
  (happened filled navy + white 2; planned surface fill + navy 1.6); leaders .18; labels 12/500
  name + mono 12 muted time; happened labels ink, planned muted; hit circles r12.
- TIDE: `y = 284 − 35·m`; area over the full window; past line `#5598C4` 2 up to NOW, future line
  at stroke-opacity .5; HW marker r4 hollow (surface / water 1.6) with "HW 23:10 · 2.1 m" mono 12
  muted; now point r4 filled navy + white 2 with a 12/500 ink label.
- MOVES: `y = 412 − 0.46·moves`; reference line y366 (.08) "100 / h" mono 12 muted; area + line
  `#1C6E9E` 2 **up to NOW only**; peak marker r3.5 hollow with value label; now dot r5 accent +
  white 2, label mono 13 600 ink ("111 / h").
- CARGO: `y ≈ 524 − 0.66·v`; in line navy 2, out line water 2, band between at water .16; both
  stop at NOW; labels "out N" / "in N" mono 12 muted.
- Axis: hourly ticks at y540 (5px; 8px every 3h, .18); labels y568 mono 12 muted: 06:00 (start),
  09:00…18:00 (centred), no 21:00, "Thu 00:00" (500 ink), 03:00, 06:00 (end).
- NOW: line ink 1.25 from y20 to 548; pill 80×22 r11 ink, "NOW 21:35" mono 500 12 ground .04em.

**Table view**: columns `72 | 1fr | 96 | 92 | 76 | 76 | 72`, gap 12, row padding `0 8`. Headers
(h32): Time · ▬Vessels · ▬Tide m · ▬Moves / h · ▬In / h · ▬Out / h · Net / h (bars 12×3 in lane
colours; Vessels left, others right). Rows hairline top. Vessel cell: 9px dot, name (past 500 13
ink / future 400 13 muted), status mono 12 muted. Prefixes "HW"/"LW"/"peak" 500 11 .04em muted.
NOW row h32 with 1.25 ink top and bottom borders, NOW pill row header, values mono 600 ink.
Future rows shade .03, "—" in .18. Footer `padding 12 8 0` 12 muted: "Hourly values; the NOW row
is live. Ahead of now: tide is the tide-table forecast, moves and cargo have no forecast." and
"Totals 2,146 moves" (mono).

**Needs you + Next 6 h** aside (span 4): surface, r12, ring, padding 24, gap 24. Needs-you section
bleeds to the card edges (margin -24, padding `24 24 16`), tint `rgba(201,85,59,.05)`, radius
`12 12 0 0`. Head H2 "Needs you" + badge (mono 600 13, padding `6 10`, r12, `#B04A32`, white).
Rows: grid `20 | 1fr | auto`, gap 12, padding `16 0`, hairline between; 18px icon stroke `#C9553B`
1.6; title 600 15/1.3; sub 13/1.4 muted with late part 600 `#B04A32`; button h44 padding `0 16`
r8 500 14 (primary `#1C6E9E` white; secondary surface + ring .18). Items: lock "Hold on CMAU 918204
4" / "Inspection due 21:15 · 20 min late" / primary "Open inspection"; ID card "Driver ID pending
at Gate 3" / "TRK 8834 at the barrier · waiting 54 min" / "Verify driver"; clock "2 orders late" /
"Both in the yard queue" / "View orders". Full-bleed 1px .18 divider below.

Next 6 h: H2 + "21:35 → 03:35 · 4 movements · ships to scale". Rows grid `44 | 10 | 1fr | auto`,
gap 12, padding `10 0`; time mono 500 13/20; dot 10px (filled navy + white 2 / hollow ring 1.6),
margin-top 5; title 600 15/20; sub 13/1.4 muted; right column countdown mono 500 13/20 (ink when
under an hour, muted after) over the ship glyph. Rail 1px .18 at x60. NOW row: height 14, pill
"NOW" mono 500 11 ground on ink r7 44 wide, line 1.25 ink. Rows: Baltic Crown 21:30 "Departs
Berth 06" "departing" (wake); NOW; Nordic Star 21:40 "Arrives Berth 07 · pilot aboard" "in 5
min"; Kaida Maru 23:30 "Departs Berth 01" "in 1 h 55"; Levant Express 02:30 "Arrives Thu · no
berth yet" "in 4 h 55".

Ship glyph, to scale at 0.2825 px/m, height 26: waterline `x -3 → w+2` y22.5 `#5598C4` .7 1.25;
hull `M0 14.4 L(w−1) 15 L(w−2.2) 22 L6 22 C3 22 1.2 18.5 0 14.4 Z` navy (bow left); containers
4.5×3 at x = 7 + 5.5n, rows y 11.75 / 8.25 / 4.75, stacks 2–3; bridge at x≈0.8w, y1, 7×14 rx.6
`#E7EAEB` stroke .35 .75, window line y3.4 ink .6; funnel bridge+8, y6, 3×9 navy; wake (departing)
`M86 21 L100 21.8 M87 18.6 L96 19.4` water 1 round.

**Activity** (full width): surface, r12, ring, padding `24 24 12`, gap 8. H2 "Activity" + "7
today · 2 need action · newest first". Two columns (gap 24). Row grid `48 | 20 | 1fr`, gap 12,
padding `12 0`, hairline top on every row; time mono 400 13/20 muted; 18px icon stroke 1.6 muted
(alert items `#C9553B`); title 500 15/1.3; sub 13/1.4 muted, one line, ellipsis; chip "NEEDS
ACTION" 600 11 .06em, ink on `rgba(201,85,59,.12)`, padding `3 6`, r4, margin-left 8. Icons:
truck `rect 3,6.5 11×9.5 rx1` + `M14 9.5h3.8l3.2 3.4V16h-7` + wheels; ID card; pilot boat
`M3.5 14.5h17l-2.6 4.5H6.1z M7 14.5V10h10v4.5 M12 10V5`; shield-check; lock; crane/bay
`rect 3,7 18×11 rx1 + M7.5 7v11M12 7v11M16.5 7v11`; document `M7 3h7l4 4v14H7z M14 3v4h4
M10 13h5M10 16.5h5`.

## 3. Summary column copy (design snapshot at 21:35)
VESSELS "3 of 7 done" / "1 departing · 3 to come" / "arrivals ↑ departures ↓". TIDE "1.8 m" /
"rising · HW 23:10" / "wind 14 kn SW". MOVES "2,146 today" / "per hour, 06:00 → now" / "peak 158 / h
at 17:00". CARGO "Net −4 / h" (U+2212) / "46 in · 50 out per hour" / "out ahead since 09:00".

## 4. Interactions (CSS in the prototype)
- Chart/Table: indicator `translateX(72)` .26s; incoming view fades in .2s.
- Linked highlight by vessel key on every element carrying it (map tag, anchor, chart dot/label/hit
  circle, Next 6 h row, Activity row): row tint `rgba(28,110,158,.07)` bleeding 12px, .2s; ship glyph
  `translateY(-1.5)` .3s; map tag `translateY(-2)` + linked shadow .2s; chart dot r 5→7 .2s; map
  anchor r 5.5→8; chart label fill → ink. Focusable: map tags, chart hit circles, Next 6 h rows.
- Masthead Needs-you figure hover/focus: inset 2px alert ring on the Needs-you section, .2s; click
  scrolls to it.
- Expand/Collapse: view 340→491 and layer `translateY(-110)`→0, .32s; icon crossfade with scale .25
  and blur 4, .3s; label swaps instantly.
- Camera dropdown / Key: panel rises from `translateY(4)` + opacity 0, .18s; chevron rotates 180°,
  .2s. No outside-click or Escape handling in the prototype.
- Layer segmented: indicator moves and resizes .26s (Map 0/88, Yard 88/88, Security 176/116,
  Traffic 292/104).
- Duty card: entrance .7s after .25s from opacity 0, `translate(0,-10)`, `rotate(-5deg)` to
  `-1.2deg`; hover rotates to 0 and lifts 2px, .3s. Shift progress grows `scaleX 0→1` .9s after .6s.
- Wake: 3.6s ease-in-out loop, opacity .35↔1, scaleX .7↔1.
- Tab hover: ink + `inset 0 -2px 0 .16`, .16s. Buttons: primary hover `#185F88`, secondary
  `#F2F3F2` + ring .34, ghost `.05`, floats to the hover shadow, `:active` .96.
- Focus ring 2px `#1C6E9E` offset 2 (rows -2). Reduced motion: transitions .01ms, animations off.

## 5. Ambiguities found in the prototype
1. No runtime logic: camera, layers, zoom, tooltips, live NOW and countdowns undefined.
2. Tide past/future lines leave a ~7px gap at NOW.
3. Tide "now" point sits at 21:40 (Nordic Star's arrival), not at NOW 21:35.
4. HW 23:10 on the chart vs HW 23:00 in the hourly table (rounding).
5. "3 of 7 done · 1 departing": Baltic Crown (21:30) is drawn filled but counted as departing.
6. Next 6 h window "21:35 → 03:35" lists Baltic Crown at 21:30, above the NOW row.
7. Activity "2 need action" vs Needs you 3 (late orders have no Activity entry); Alongside 3 but
   only two tags on the map (Kaida Maru at B01 untagged).
8. Duty progress 2.0% is a placeholder.
9. IBM Plex Mono 600 used but not loaded.
10. Harbour labels baked into the image; tags only align at the image's aspect.
11. Activity row highlight bleeds 12px into the gutter.
12. No 21:00 axis label (collision with NOW).
13. No loading, empty, error, narrow-width or dark states.

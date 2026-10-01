# SPEC: Overview scrolling with a condensing harbour

Status: spec, not built. Written 2026-10-01. Build in a fresh session: read this cold, then HANDOFF.md.

## Problem

On Overview, 517 px of a 981 px client (1664x981) is fixed chrome: header 56, masthead 121, harbour band 340.
The sections below get 464 px (47%), so the timeline, Needs you and Next 6 h never show in full.

## Decision (taken with the user, 2026-10-01)

A **condensing harbour**: when the operator scrolls, the masthead folds away and the harbour band snaps from
340 to a **mini band of 180 px**. Scrolling back to the top restores both. Content then gets 981 - 56 - 180 =
**745 px (76%)**. The harbour stays visible because linked hover (timeline dots and Next 6 h rows light their
ship's tag) only works while the ships are on screen.

Rejected: content scrolling over the harbour (it hides the harbour while you read, so linked hover is lost), and
a floating picture-in-picture viewer (a new floating surface over the cards, with a tiny HUD and tag layout).

Mini height 180, not the 150 first sketched: in the scene, the vessel tags start at about y 165 and the hulls end
at about y 360. That is 205 px of interest; 180 keeps every tag and pin with the hulls slightly cropped, while
150 cuts the tags. Verify this in step 1.

## Architecture Brief

- **Pattern:** MVVM (CommunityToolkit.Mvvm, `x:Bind`), unchanged. `PortState` is the single store. This is
  page-chrome state, so it stays on the existing state object; no MVUX.
- **State:** add `PortState.OverviewCondensed` (bool, `[ObservableProperty]`). `StageHeight` becomes
  `Section == "overview" ? (OverviewCondensed ? 180 : OverviewExpanded ? 491 : 340) : NaN`. Condensed wins over
  Expanded: condensing from 491 goes to 180, and restoring at the top returns to whichever of 340 or 491 was set.
  Add `MastheadShown => IsOverview && !OverviewCondensed` for the masthead fold.
- **Scroll source:** `SectionScroll.ViewChanged` (MainPage) sets `OverviewCondensed`, with hysteresis:
  condense when `VerticalOffset > 48`; restore when `VerticalOffset <= 4`. Only on Overview; leaving Overview
  clears it. This is code-behind (`ViewChanged` has no Command surface): mark it `xaml-lint: allow codebehind -
  scroll offset to page-chrome state`, or an attached behavior (`controls:Condense.Offset`) if the lint budget
  wants it out of the page. Breakpoints are not involved (no width logic), so RESPONSIVE stays at 0.
- **Harbour band:** the existing band mode does the work (scene rendered once at 491, clipped and slid;
  `SetBand` steps height, slide, clip and fades in one frame; `HarbourScene.SetWindow` keeps the fades on the
  visible slice). Extend `SlideBand`'s offset map to three points: 491 to 0, 340 to 110 (today), 180 to the
  mini offset (start at 170, so the visible slice is scene y 170 to 350; tune in step 1). Piecewise linear between.
  No scene re-render while condensing.
- **Masthead fold:** the masthead's row animates its height to 0 with an opacity fade, stepped in the same
  frames as the band (one tween drives both, so their edges never drift apart). Implementation choice: put both
  on the band's existing per-frame step (`OnBandFrame`), or step the masthead with `controls:Reveal` (`Grow`)
  and accept two tweens with the same duration and curve. Prefer one driver.
- **No Storyboards** for any of this: gotcha "A Storyboard replaced mid-run can leave its animated values in
  force" (uno-runtime-gotchas.md, Cargo 2026-10-01). Step local values and write the end state on the last
  step, with the guard timer from gotcha "CompositionTarget.Rendering is not a steady frame clock".
- **Platform:** desktop Skia first (`net10.0-desktop`); Android builds must stay green. Touch scroll on Android
  triggers the same ViewChanged path.
- **Validation:** uno-app MCP at 1664x981 and 1100x900. Probe: `CompositionTarget.Rendering` frame-interval
  trace during the fold (see Interaction Brief: verification).

## Design Brief

- **Visual direction:** the harbour stays the page's anchor. Condensed, it reads as a live strip, not a
  thumbnail: same palette, the same fades top and bottom (14% of the visible slice), tags at full size.
- **Layout, top of page:** header 56 / masthead 121 / band 340 (or 491) / sections. Unchanged.
- **Layout, condensed:** header 56 / mini band 180 / sections. The masthead is gone; its figures (Alongside,
  Inbound, Needs you) are not repeated elsewhere (open question).
- **HUD in the mini band:** hidden: camera menu, layer switcher, Key, scale and zoom (they need about 60 px of
  height and would cover the ships). Kept: one float, top-right, labelled **Show harbour** with the Expand icon;
  it scrolls the page to the top, which restores everything. Tag culling insets drop from 40/56 (top/bottom HUD
  room) to 8/8 in the mini band.
- **Typography, spacing, components:** no new styles. The Show harbour float reuses `HudSummaryButton` and the
  inline float shadows (ShadowContainer, Toolkit gotcha G36). Any float that moves keeps the wrapper-Grid
  workaround (gotcha "ShadowContainer keeps its paint when only its offset changes").
- **Theme:** unchanged (invariant light tokens).
- **Responsive:** same behaviour at every width; the HUD breakpoint (1200) only affects the full band.

## Interaction Brief

- **Flows:**
  1. Scroll down from the top: past 48 px the masthead folds and the band snaps to 180 over 320 ms on EaseSmooth.
     Content moves up with the chrome (the viewport grows at its top), so what you were reading stays in view.
  2. Read the timeline or Next 6 h while hovering: tags light in the mini band as they do in the full band.
  3. Scroll back to the top (offset <= 4): the band returns to 340 (or 491 if it was expanded) and the masthead
     unfolds, 320 ms.
  4. Click Show harbour in the mini band: the page scrolls to the top (animated `ChangeView`), which restores.
  5. Click a tag in the mini band: selects the vessel as today (fly-to and facts card). Open question: the
     facts card needs about 130 px above the tag, which the mini band does not have.
- **Input:** wheel, scrollbar, PageDown/End, touch. Keyboard focus moving into a section must not be hidden
  behind the band (it is outside the ScrollViewer, so it never overlaps the content).
- **Empty, loading and error states:** none new; the band and sections already have theirs. If the content is
  too short to scroll past 48 px, the page never condenses (correct).
- **Animation:** one 320 ms tween (`DurationExpandMs`, EaseSmooth) for band height, slide, clip, fades and the
  masthead fold. Reduced motion: jump to the end state. Hysteresis (48 down, 4 up) prevents flapping at the edge.
- **Feedback:** the Show harbour float appears only while condensed (fade 160 ms).
- **Accessibility:** Show harbour has an AutomationProperties.Name; the masthead's figures stay in the
  automation tree only while shown. The harbour summary text is unchanged.
- **Runtime verification (uno-app MCP):**
  1. Top of page: identical to today (screenshot diff against the current build).
  2. PageDown: condensed; mini band 180 with all three tags and pins visible; HUD hidden; Show harbour shown.
  3. Hover a timeline dot while condensed: its tag lights.
  4. Home: restores to 340; masthead back; HUD back.
  5. Expand at the top, PageDown, Home: returns to 491.
  6. Show harbour: scrolls to the top and restores.
  7. Leave Overview while condensed, return: not condensed.
  8. Frame-interval trace during a fold: report median and p90; compare with the idle baseline (median 17.8,
     p90 45.6 ms).
  9. Android build passes.

## Implementation Plan

1. **Spike (15 min):** in band mode, temporarily set StageHeight 180 and try offsets 150/170/190; pick the slice
   that keeps all tags and pins. Confirm the scene does not re-render at 180 (still keyed on 491).
2. `PortState`: `OverviewCondensed`, `StageHeight` with three heights, `MastheadShown`; clear on section change.
3. `HarbourView`: three-point offset map in `SlideBand`; mini-band tag insets; expose a `Condensed` DP or read
   StageHeight to hide the HUD floats inside the view (camera menu, Key, scale and zoom).
4. `MainPage`: `ViewChanged` with hysteresis; masthead fold stepped with the band; layer switcher hidden while
   condensed; Show harbour float (scrolls to top).
5. Verify flows 1 to 9 above; lint (all gating 0); desktop and Android builds 0 warnings.
6. Update `docs/demo/overview-recording.md` (the take can now show Next 6 h and Activity with the harbour visible),
   HANDOFF.md, commit per step.

## Unresolved Questions

- Should the condensed state repeat the three figures (Alongside / Inbound / Needs you) somewhere, for example
  as a compact line in the header, or is the Needs you section enough once you have scrolled?
- Clicking a tag in the mini band: allow the facts card to overflow the band (drawn over the content), skip
  the card and only fly-to plus highlight, or restore the full band first?
- Snap vs scroll-linked: this spec snaps (320 ms). Scroll-linked shrinking tracks the finger better but
  steps layout every scroll frame on a page with measured hitches (idle p99 206 ms). Accepting snap as the risk.
- Masthead fold: one driver (the band's frame step) or a second tween (`Reveal`)? Spec prefers one driver;
  decide in step 4 once the code is open.
- Ecosystem check before step 4 (5 min): is there an Uno Toolkit or WCT 8 sticky or condensing-header
  mechanism? WinUI has no sticky headers; if none exists, the custom path above stands and goes in the handoff.
- Risk: the frame hitches measured on Overview can land inside the 320 ms fold. Profiling the hitches is
  separate work, already queued.

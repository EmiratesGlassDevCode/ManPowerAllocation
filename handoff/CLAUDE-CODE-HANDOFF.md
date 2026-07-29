# Fluent 2 redesign — Claude Code handoff

Paste this file (and `handoff/app-tokens.css`) into Claude Code in the
`EmiratesGlassDevCode/ManPowerAllocation` repo, branch
`claude/techsource-internal-app-setup-2zm35u`.

## Reference material in this folder

- `reference/ManpowerAllocation-Reference.html` — the full redesign as a single
  self-contained file. **Open it in a browser** and navigate all 13 screens from the
  sidebar (the "System pages" group reaches Emergency Access and Access Denied).
  You can also read the file directly to lift exact style values per element; every
  style is inline, so what you read is what renders.
- `reference/screens/*.png` — one screenshot per route, in nav order.
- `app-tokens.css` — the token layer plus a selector-by-selector target list.

When this document and the reference disagree, **the reference wins**. Where a value is
not stated here, read it off the reference rather than inventing one.

## Fidelity note

Do not hand-build controls to match the reference pixel for pixel. The reference
hand-rolls switches, buttons, selects and search fields because it is plain HTML; the
real app must keep `FluentSwitch`, `FluentButton`, `FluentSelect`, `FluentSearch`,
`FluentTextField`, `FluentNumberField` and `FluentCheckbox`. Match the **layout,
colour, spacing, type and states**; let the Fluent components render themselves.

---

## Ground rules

1. **No framework change.** The app already uses Fluent UI Blazor with
   `<FluentDesignTheme Mode="Light" CustomColor="#0F6CBD" />`. Keep it. Do not add
   Tailwind, Bootstrap, or a component library.
2. **Keep every existing CSS class name** in `wwwroot/app.css` and in the `.razor`
   markup. This is a re-tokenisation plus targeted layout changes, not a rewrite.
   Only replace values and add the few new rules called out below.
3. **Drop the "glass" treatment.** `.glass` becomes a flat white Fluent surface:
   `#FFFFFF`, 4px radius, depth-2 shadow, no blur, no border.
4. **Fluent geometry everywhere:** 4px corner radius on cards/controls, 8px on modals,
   32px control height, 8/12/16px spacing steps.
5. **Type ramp:** Segoe UI Variable Text. caption1 12/16 · body1 14/20 ·
   subtitle2 16/22 · subtitle1 20/26 · title2 28/36 (`h1`) · KPI numeral 32/40 ·
   hero numeral 36/40. Never below 12px.
6. **Colour is semantic, never decorative.** Green = optimal/present, red = short/absent,
   amber = vacation/watch, purple = outsource, teal = excess/available, brand blue =
   selection and primary action. Division accents (blue / teal / magenta) are the only
   identity-only colours.

---

## Step 1 — token layer

Replace the top of `wwwroot/app.css` with `handoff/app-tokens.css` and rewrite the
existing rules to consume the custom properties. That file also lists, selector by
selector, the target values for every class already in the stylesheet — work through it
top to bottom. Delete any leftover gradient, blur, or `backdrop-filter` declarations.

## Step 2 — shell (`Components/Layout/MainLayout.razor`, `NavMenu.razor`)

- Top bar: 48px, solid `--topbar-bg` (`#0F548C`). Keep the existing
  `<img src="img/emirates-glass-logo.png" class="topbar-logo-img" />` — the real navy/gold
  wordmark does not read on the blue bar, so seat it on a white plaque: 32px tall,
  4px radius, `padding: 4px 10px`, image `height: 20px; width: auto`. Then a 1px
  `#3A7AB8` divider and the white 16/22 semibold "Manpower Allocation" title. Right side, in order: attendance-health pill, user email,
  28px initials avatar (`--supply` background), outlined Sign out button.
- `AttendanceHealth` becomes a 24px pill: dark `--brand-pressed` fill, 6px dot,
  `#9FD89F` text when healthy; `#8A3707` fill + `#FFD5B0` text while syncing.
- Sidebar: 260px, white, 1px right divider, sticky under the top bar. Each `NavLink`
  gets a 3px × 16px brand indicator (transparent unless active) and a 12px rounded
  colour square: Summary blue, EGL blue, Functional Support teal, BRG magenta,
  Employee Search gold; admin items blue/teal/purple/gold/red/green. Active item:
  `--brand-tint` background, `--brand-hover` text, 600 weight.
- Add a department count pill to each division link (`--brand-tint` / `--brand`).
- Pin an attendance-sync status card to the bottom of the sidebar
  (`--brand-tint`, 4px radius, 12px pad, two lines).
- `BreakGlassBanner`: `--danger-bg`, 1px `--danger-bd`, 4px left `--danger` bar.

## Step 3 — page-by-page

Every page gets the same header block: caption breadcrumb (12/16, `--n-fg-3`),
`h1` 28/36, then a one-line 14/20 `--n-fg-3` hint. The shift selector sits on the
right of that row on Summary and DivisionView only.

### `/` Home.razor — Factory Summary
- Export row: label + three neutral 32px buttons, each with a 10px colour square
  (blue / teal / gold); hover tints to `--brand-tint`.
- Three division `.stat-card`s in `repeat(auto-fit, minmax(300px,1fr))`: 4px accent
  strip, 32px tile + 20/26 title + "Open →" affordance, 36/40 present numeral coloured
  by variance sign, then a present/outsource split progress bar, then badges
  (on roll, absent, vacation, outsource, variance, short depts).
- Factory total: one card, 4px strip green/red by net variance, 28/36 numeral,
  variance badge, right-aligned shift-scope caption.
- Division breakdown: keep the `<table class="grid">`; header on `--n-surface-tint`,
  numeric columns right-aligned, variance cell 600 and semantic-coloured, division
  cell prefixed with a 10px accent square.
- Four `AlertPanel`s in one `.alert-row` grid (`minmax(340px,1fr)`): Absent (red),
  On vacation (amber), Shortage departments (teal), Outsource present (purple).
  Empty state: "Nothing to report." at 14/20 `--n-fg-3` — never an empty box.

### `/division/{DivisionName}` DivisionView.razor
- KPI row: 8 cards, `repeat(auto-fit, minmax(150px,1fr))`, 4px coloured top strip,
  12/16 semibold label, 32/40 numeral, 12/16 caption ("on floor now", "planned",
  "assigned", "unplanned", "approved leave", "contract cover", "vs. required",
  "need cover").
- Toolbar: 260px search with brand underline, then the five filters as **pills**
  (All / Shortage / Excess / Optimal / OFF) instead of `FluentSelect`, then All ON /
  All OFF neutral buttons, then the accent "+ Add department".
- `.dept-grid`: `repeat(auto-fill, minmax(340px,1fr))` — **never a fixed column count**;
  below ~340px the roster row collapses and the name flexes to zero width.
- Department card: status-tinted header (32px accent tile, name 16/22, "N on roll ·
  all shifts" caption, Fluent switch + On/Off label), then present/required ratio,
  split bar, badges, a divider, then `Show people` link + Edit / Delete buttons.
  `status-off` → `opacity:.62`.
- Roster row: 26px status-tinted initials avatar, name (`flex:1; min-width:80px;`
  ellipsis), 44px shift chip (gold for Day, `#3B4A8C` for Night), 76px right-aligned
  semantic status. Row actions on a second line, indented 36px: Mark/Unmark vac,
  ↔ Shift, Move to… — hide entirely for outsource rows and for non-editors.
- Department add/edit modal: 8px radius, `--brand-tint` header, name field full width,
  three number fields in a row, right-aligned Cancel / Save.
- Bottom `.alert-row`: Absent, On vacation, Outsource present, Available / excess (teal).

### `/search` EmployeeSearch.razor
320px search field; idle state is an explicit empty card
("Type at least two characters to search all divisions."), not a blank page. Results
become a CSS-grid table (`1.6fr .8fr 1fr 1.2fr .6fr .8fr 1.2fr`): avatar + name,
badge ID, division, department, shift chip, status badge, notes (`—` when blank).
No-match state uses `.message.error`.

### `/admin/roles` Roles.razor
Form card first: object-id (340px), display name, role select, accent Save. Table adds
an avatar to the display-name cell, a role badge (Admin red / User blue / Viewer
neutral), monospace object id, and a text-only red Remove.

### `/admin/import` Import.razor
Two side-by-side cards — Attendance (brand tint header) and Requirements (teal tint
header) — each wrapping the existing `InputFile` in a dashed drop zone with a filled
"Choose file" button and a max-size caption. Below, full width, the result panel on
`--ok-bg` with four big numerals (created / updated / imported / removed) and warnings
as captions.

### `/admin/attendance` Attendance.razor
Left card: status header tinted green on success / amber while running, run timestamp
on the right, "Sync now" accent button (greys and reads "Syncing…" when busy), then
four tinted stat tiles (present green, absent red, vacation amber, changed blue).
Right card: "Sync rules" list, one coloured dot per rule, carrying the existing
four-rule copy verbatim.

### `/admin/shift-settings` ShiftSettings.razor
Card with amber tint header, the two `input type=time` fields plus accent Save, then a
34px day/night band bar whose day segment width = (night start − day start) / 24, then
the operational-day caption. A second `--brand-tint` panel shows the current setting at
20/26 plus the last-changed line.

### `/admin/break-glass` BreakGlass.razor
Danger callout first (`--danger-bg`, 4px left bar) carrying the existing warning copy,
then a key/value card: 260px label column on `--n-surface-alt`, value column 14/20, and
a status pill in the card header (green "Disabled" / red "ENABLED").

### `/admin/audit` AuditTrail.razor
Checkbox becomes a pill toggle that turns `--danger-bg` when on, plus an
"Showing N of M most recent entries" caption. Rows are a CSS grid
(`32px 1.3fr 1.2fr .8fr 1fr .7fr .9fr`) with an action badge (Create green,
Update blue, Delete red, sign-in neutral); break-glass rows get `--danger-bg` and an
inset 3px red left bar. Expanded detail: two `<pre>` columns on white with 1px borders,
"Before" labelled red, "After" green.

### `/break-glass` BreakGlassLogin.razor
420px centred card on a `linear-gradient(160deg,#0F548C,#0C3B5E)` backdrop, 4px red top
strip, the real `panel-logo` `<img>` at `height: 26px` (natural colours, on white) above a
"Manpower Allocation" caption, `h1`, danger-tinted advisory box, two labelled fields, a
full-width red submit, and the Entra sign-in link. Keep the plain form POST.

### `/access-denied` AccessDenied.razor
560px centred card on `--n-bg`, 4px amber top strip, the real `panel-logo` `<img>` at
`height: 26px` above a "Manpower Allocation" caption, the identity key/value table with
a monospace object id, the IT instruction caption, and Back / Sign out buttons.

---

## Step 4 — assets

`wwwroot/img/emirates-glass-logo.png` stays as-is; all three usages keep a real `<img>`
element (no CSS glyph or letter substitute). Do not recolour or mask the file — the white
plaque in the top bar is what makes it legible.

## Step 4b — verify

- `dotnet build` clean; no new NuGet packages.
- Every page at 1280px and 900px wide: no fixed-column grid may squeeze a text cell to
  zero width (this was the one real defect found in the design pass).
- Contrast: all body text ≥ 4.5:1 — the semantic foregrounds above are chosen for
  their own tint backgrounds and are safe; do not lighten them.
- Keyboard: the division stat-cards keep `role="button"`, `tabindex="0"`, and the
  Enter/Space handler; the pill filters and pill toggles need the same treatment.
- Focus visible on every interactive element: 2px `--brand` outline, 2px offset.

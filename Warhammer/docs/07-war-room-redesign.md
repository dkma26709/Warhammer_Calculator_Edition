# War Room Redesign

The visual redesign of the simulator, decided on 2026-09-29. It replaces the "Styling Direction"
section of `04-ui-ux.md` and its green/grey/red colour coding of results.

## The decision

Four directions were drawn on a design canvas against the same real data (Greatswords vs
Hammerers; a 1,974/2,000 pt Empire list failing the Core minimum): War Room, Dice Lab, Battle
Gazette and an Old World Builder look. **War Room** was chosen, then refined in two rounds:

- **Colours: "A4 · Colorblind-safe"** — War Room's dark, warm ground and brass, with the defender
  moved from crimson to blue. A colour-blind reviewer is among the app's users, and the old
  pairings (green attacker vs red defender in the app; brass vs crimson in the first mockup) are the
  ones red-green colour blindness merges.
- **Headings: Cinzel** (carved Roman capitals). **Body: Alegreya Sans.** Cinzel runs wide, so it is
  set smaller than the blackletter it replaced and never below ~16px.

The figures on the mockups were placeholders, not simulator output.

## Accessibility rules the design is built to

ISO/IEC 40500 is the ISO edition of WCAG 2.x; ISO 9241-171 (software accessibility) asks the same
of colour. Four success criteria govern this design, and every screen keeps them:

| Criterion | Rule | How the design meets it |
|---|---|---|
| 1.4.1 Use of Color | Colour is never the only carrier of meaning | Defender marks are **hatched** as well as blue; failed limits and validation errors carry an **icon and a label**; toggles differ by fill, not hue |
| 1.4.3 Contrast (Minimum) | Text ≥ 4.5:1 | Lowest text pair is muted text on a card, 6.8:1 |
| 1.4.11 Non-text Contrast | Controls and chart marks ≥ 3:1 | Form-control borders use `--wr-field` (4.4:1); every bar fill ≥ 3:1 against its card |
| 2.4.7 Focus Visible | Keyboard focus is visible | A brass double ring on every focusable control |

Side colours were checked with a Machado (2009) simulation of protanopia, deuteranopia and
tritanopia: brass and the defender blue stay far apart (CIE76 ΔE ≥ 66) under all three.

## Tokens

All colours live as custom properties at the top of `wwwroot/css/app.css`; Razor markup names a
role, never a hex value.

| Token | Value | Role |
|---|---|---|
| `--wr-bg-deep` | `#0c0a08` | Header bar |
| `--wr-bg` | `#110e0b` | Page |
| `--wr-bg-raised` | `#16120e` | Raised bands, card headers |
| `--wr-surface` | `#1a1612` | Cards, inputs, tables |
| `--wr-rule-soft` / `--wr-rule` | `#2c251e` / `#3b3228` | Dividers, card borders, bar tracks |
| `--wr-field` | `#8a7c67` | Borders of form controls (the 3:1 one) |
| `--wr-text` / `--wr-text-2` / `--wr-muted` | `#eee5d2` / `#d9cfbb` / `#aa9d87` | Text, secondary text, labels |
| `--wr-brass` | `#d6a849` | Attacker (Gauntlet: subject), primary action, OK state |
| `--wr-defender` | `#74b2ea` | Defender (Gauntlet: opponent), text and fill |
| `--wr-defender-stripe` | `#4a88ca` | The hatch stripe of defender fills |
| `--wr-draw` | `#8b8170` | Draws and neutral fills |
| `--wr-warning` | `#f0c96e` | Warnings, General badge |
| `--wr-error` / `--wr-error-fill` | `#ef7f98` / `#c93c5f` | Error text / error fill |
| `--wr-on-fill` | `#0c0a08` | Text on any light fill (brass, blue, draw, bone) |

Bootstrap stays (5.3, from the CDN). Its own variables and the handful of variant classes the app
uses are re-pointed at these tokens, so `bg-dark`, `text-body-secondary`, `table-dark` and friends
pick up the theme without markup changes. `success` means OK (brass), `danger` means error
(rose-red), `info` is a neutral bone and `warning` a pale gold.

Sides are their own classes, because a side is not a status: `text-attacker`, `bg-attacker`,
`border-attacker` and the `-defender` equivalents (`bg-defender` is hatched), plus `bg-draw`.
Combat outcomes in the multi-round panel use `outcome-*` classes in the same two families.

## The plan

Each step is its own pull request into `main`; the build and the full test suite stay green
throughout (there are no UI tests, so each step is also checked by running the app against the
mockups).

1. **Theme foundation.** Tokens, fonts, Bootstrap re-pointed, side classes replacing
   `success`/`danger` where they meant attacker/defender, icons on errors and failed limits, focus
   rings, the header restyled. Layouts unchanged; `/roster` stays light-on-white for printing.
2. **Matchup layout.** The two units face each other across a raised band with portraits and stat
   lines; charge, rounds, iterations, Simulate and the win split in the middle; loadouts on the
   sides; results in the centre column (wound distributions, combat-resolution margin, CR and
   points tables).
3. **Builder layout.** Validation band across the top; three columns (add units, army list,
   composition); composition bars with a warning icon and dashed outline on a failed limit.
4. **Gauntlet.** No mockup; it reuses the pieces of steps 1–3.

Anything a mockup shows that the simulator does not yet compute becomes its own tested change in
the C# layer, not part of a layout step.

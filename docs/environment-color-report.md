# Environment Color — Design Report

**Date:** 2026-04-28  
**Status:** Design proposal — no code changes in this document  
**Related idea:** [UX ideas § 7.1 Environment color indicator](ux-ideas.md)

---

## 1. Problem Statement

When an HTTP client tool manages multiple environments (Development, Staging, Production, …) it is easy to accidentally fire a request against the wrong one.
A user who has "Production" selected while debugging against a local echo server will not notice their mistake until the request either succeeds or fails in a surprising way.

A color coding system assigns each environment an optional accent color so that the active environment is immediately obvious from any screen of the application — much like the colored title bar used by SQL clients (e.g., DataGrip, Azure Data Studio) to warn users that they are connected to a production database.

---

## 2. Design Goals

| Goal | Notes |
|------|-------|
| **Warning at a glance** | The color must be visible without reading text, even in peripheral vision |
| **Optional** | Environments with no color assigned look exactly like today — zero disruption |
| **Consistent with app theme** | Works in both Dark and Light theme; color must meet WCAG 2.1 AA contrast vs. its background |
| **Configurable** | Any color can be chosen, but a small set of recommended presets (red, amber, green, blue, purple) covers most workflows |
| **Not intrusive** | No modal dialogs, flashing, or sounds — a persistent, low-noise visual cue |

---

## 3. Visualization Patterns

Four placement patterns are evaluated below; they can be combined.

---

### Pattern A — Colored Env Dropdown (toolbar)

The `Env:` ComboBox in the top toolbar changes its background to the environment's accent color when an environment with a color is selected.
White text is used on the colored background so that the label remains readable.

**Production (red):**

![Colored env dropdown — Production red](screenshots/env-color-dropdown-production.png)

**Development (green):**

![Colored env dropdown — Development green](screenshots/env-color-dropdown-development.png)

**Pros:**
- The dropdown is always visible regardless of which dock panels are open.
- No additional screen real-estate required.
- Familiar pattern: VS Code uses a colored status bar segment for remote connection state; browser extensions use badge colors.

**Cons:**
- A small control; the color patch is ~190 × 30 px — small for peripheral detection.
- Must ensure text contrast: white text on the accent color must meet 4.5:1 WCAG AA. A brightness check at assignment time can enforce this automatically.

---

### Pattern B — Full-Width Warning Banner

A narrow (≈ 24 px) colored strip is inserted directly below the main toolbar and spans the full application width. It contains a short label such as `● PRODUCTION — requests sent to this environment will affect real data`.

![Full-width banner — Production red](screenshots/env-color-banner.png)

**Pros:**
- Maximum visual surface — impossible to miss.
- The text can provide an explicit human-readable warning message.
- Directly inspired by the "connected to production" banners common in database clients.

**Cons:**
- Consumes vertical space; may feel noisy for non-critical environments.
- Recommended only for environments explicitly flagged as "warn me" (e.g., Production), not for every colored environment.

**Recommended use:** Make the banner opt-in per environment via an additional `ShowWarningBanner: bool` flag, separate from the color. A red environment would show the colored dropdown (Pattern A) by default; enabling the banner adds the strip as a stronger signal.

---

### Pattern C — Color Swatch in the Environments Panel

Each environment row in the "Manage environments" panel shows a small filled circle to the left of the environment name.
A color picker row (`Color (optional):`) is added to the inline edit form, offering five preset swatches plus a "no color" option (∅).

![Color swatch in environment panel](screenshots/env-color-panel-swatches.png)

**Pros:**
- The color is visible when browsing and selecting environments before switching.
- Low implementation cost: a single optional color property on the model + a small swatch rendered in the `DataTemplate`.
- The color picker can be as simple as a row of `RadioButton`-style colored rectangles rather than a full `ColorPicker` dialog, keeping the UI lightweight.

**Cons:**
- The panel must be open to see this indicator; it adds no ambient warning once an environment is active.

**Recommended use:** Always implement — this is the configuration surface where colors are assigned, regardless of which display patterns are chosen.

---

### Pattern D — Activity Bar Badge Dot + Colored Dropdown (combined)

A small filled circle (badge dot) overlays the Environments icon in the activity bar using the active environment's color. Combined with Pattern A's colored dropdown, this gives two simultaneous cues.

![Activity bar badge dot + colored dropdown](screenshots/env-color-activity-badge.png)

**Pros:**
- The activity bar is always visible; the badge dot provides an ambient cue even when the user's eyes are elsewhere on the UI.
- Consistent with VS Code's source-control badge pattern.

**Cons:**
- The badge dot is small (≈ 12 px); relies on users recognizing the convention.
- Requires propagating the active-environment color to the `EnvironmentsViewModel` that owns the activity bar icon binding.

---

## 4. Recommended Combination

For a first implementation the following combination balances impact with implementation scope:

| Layer | Pattern | When |
|---|---|---|
| Environment panel swatch | Pattern C | Always (configuration surface) |
| Toolbar dropdown color | Pattern A | Whenever a color is assigned |
| Activity bar badge dot | Pattern D | Whenever a color is assigned |
| Full-width banner | Pattern B | Only when `ShowWarningBanner` is enabled (opt-in per environment) |

---

## 5. Suggested Color Semantics

No color is mandated by the application — teams choose what works for them — but the following palette makes a good default set of presets:

| Preset name | Hex | Suggested use |
|---|---|---|
| **Red** | `#B41E1E` | Production / live |
| **Amber** | `#C47A00` | Staging / pre-prod |
| **Green** | `#1E7A3C` | Development / local |
| **Blue** | `#1E50B4` | QA / test |
| **Purple** | `#6A1EB4` | Demo / sandbox |
| *(none)* | — | Default; no color cue |

White text (`#FFFFFF`) is legible at 4.5:1 contrast against all five swatches, satisfying WCAG 2.1 AA for normal text.

---

## 6. Model Change Required (not implemented)

When this feature is implemented, `RequestEnvironment` in `Arbor.HttpClient.Core` would need one or two new optional properties:

```csharp
// Conceptual — not implemented
public sealed record RequestEnvironment(
    int Id,
    string Name,
    IReadOnlyList<EnvironmentVariable> Variables,
    string? AccentColor = null,          // e.g. "#B41E1E" -- null = no color
    bool ShowWarningBanner = false);     // opt-in full-width banner
```

The SQLite schema would need a corresponding column migration.
The `IEnvironmentRepository` interface and its SQLite implementation would need `AccentColor` and `ShowWarningBanner` wired through their CRUD operations.

Estimated scope: **M** (1–3 days) for Pattern A + C combined; **S** (a few hours extra) to add Pattern D badge and Pattern B banner.

---

## 7. Accessibility Considerations

- The color alone is never the **only** indicator: the environment name is always visible in text inside the dropdown and in the panel. Color is an additive signal, not a replacement.
- The full-width banner (Pattern B) includes an explicit text label — screen readers can announce it when focus enters the window.
- Color-blind users: red and green are distinguished by many dichromats only when the text label is also present. Ensure the name is always readable. The `6.4 High-contrast and colour-blind-safe themes` UX idea (see `ux-ideas.md`) is complementary — a deuteranopia palette would map Production to amber/dark rather than red.

---

## 8. Prior Art

| Tool | Pattern used |
|---|---|
| **Azure Data Studio** | Colored connection badge in the status bar |
| **DataGrip** | Colored title bar strip per data-source |
| **VS Code** | Colored remote-connection section in status bar |
| **Postman** | Environments displayed as dropdown with a colored dot (the dot color is user-assigned) |
| **Hoppscotch** | No environment color |
| **Insomnia** | Named environments with an optional color dot in the selector |
| **Bruno** | Environment name in the toolbar; no color (relies on name only) |

The **Insomnia** and **Postman** patterns are the closest analogs: a small colored circle next to the environment name in the selector. This report recommends going further with a full dropdown tint (Pattern A) to maximize ambient visibility.

---

*This document is a design-only exploration. No source code has been modified.*

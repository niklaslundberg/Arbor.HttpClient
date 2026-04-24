---
applyTo: '**/*.axaml'
description: 'Avalonia XAML rules for Arbor.HttpClient — Fluent theme metrics, TextEditor styling, and WCAG accessibility requirements.'
---

# Avalonia XAML Rules

> These rules apply to all `.axaml` files. They are a targeted subset of the full guidelines in `.github/copilot-instructions.md`. When in doubt, defer to the canonical source.

## Fluent Theme Metrics

**[REQUIRED]** `AvaloniaEdit.TextEditor` controls used in place of a standard `TextBox` (e.g. the URL bar) must match these Fluent `TextBox` visual metrics so they align correctly in forms:

| Property | Value | Reason |
|---|---|---|
| `Padding` on inner `TextEditor` | `"5,6,5,6"` | Aligns text at the same vertical position as a `TextBox` for the default 13 px font in a 32 px row |
| `CornerRadius` on surrounding `Border` | `"3"` | Fluent standard |
| `Background` on surrounding `Border` | `{DynamicResource SurfaceBackgroundBrush}` | Adapts with the active theme |
| `Background` on inner `TextEditor` | `Transparent` | Lets the Border background show through |
| `HorizontalScrollBarVisibility` | `Hidden` | For single-line inputs |
| `VerticalScrollBarVisibility` | `Hidden` | For single-line inputs |

**[REQUIRED]** Font family and size must be propagated from the app-level `UiFontFamily`/`UiFontSize` bindings via `ApplyEditorFont`.

**[REQUIRED]** Never mix a raw `TextEditor` and a styled `TextBox` in the same row or form group without ensuring they have the same effective height and padding.

## Theme Colors

**[REQUIRED]** All colors used for text or interactive elements must be defined per-theme (Dark/Light) inside `ResourceDictionary.ThemeDictionaries` in `App.axaml` so each variant can be independently validated.

**[REQUIRED]** Never hardcode a color value in XAML that is used for text or interactive element backgrounds — always reference a `DynamicResource` so the theme switch applies correctly.

## Accessibility — Color Contrast

**[REQUIRED][ACCESSIBILITY]** Every foreground/background color pair for text or interactive elements must meet WCAG 2.1 Level AA:
- ≥ 4.5:1 for normal text
- ≥ 3:1 for large text (bold ≥ 14 pt, or regular ≥ 18 pt) and UI components

**[REQUIRED][ACCESSIBILITY]** Any new color pair introduced in `App.axaml` must be covered by a corresponding test case in `AccessibilityContrastTests.cs` that asserts the WCAG contrast ratio.

## Accessibility — Keyboard and Screen Readers

**[REQUIRED][ACCESSIBILITY]** All interactive controls (buttons, list items, text boxes) must be reachable and operable by keyboard alone (Tab, Enter/Space, arrow keys where applicable).

**[REQUIRED][ACCESSIBILITY]** All non-decorative icons and images must carry an accessible name using `AutomationProperties.Name`.

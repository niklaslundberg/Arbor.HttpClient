# UI Consistency Audit Report

**Date:** 2026-04-25  
**Scope:** All Avalonia XAML views and theme resources in `src/Arbor.HttpClient.Desktop/`  
**Themes reviewed:** Dark (default) and Light  
**Reference tools compared:** Postman, Hoppscotch, Insomnia, Bruno, HTTPie Desktop

---

## 1. Audit Findings

### 1.1 Font Family — Hardcoded in Log Panel

**File:** `Views/LogPanelView.axaml`  
**Severity:** Medium  

All four log list `DataTemplate` rows hardcode:
```xml
FontFamily="Cascadia Code,Consolas,Menlo,monospace"
```
This bypasses the user-configurable font family set in Options → Look & Feel. If the user selects a different font, the log panel ignores it.

**Fix:** Replace with `{DynamicResource AppFontFamily}`.

---

### 1.2 Remove-Button Symbol Inconsistency

**File:** `Views/LeftPanelView.axaml`  
**Severity:** Low  

The "Cancel new collection" button uses `✗` (U+2717 BALLOT X), while every other remove/cancel button in the application uses `✕` (U+2715 MULTIPLICATION X).

| Location | Symbol used |
|---|---|
| Request query parameter rows | `✕` |
| Request header rows | `✕` |
| Environment variable rows | `✕` |
| LeftPanelView — cancel new collection | `✗` ← inconsistent |

**Fix:** Standardize to `✕`.

---

### 1.3 Toolbar Button Spacing — Inconsistent Margins

**File:** `Views/MainWindow.axaml`  
**Severity:** Low–Medium  

The right-side toolbar buttons each carry ad-hoc margins rather than being arranged in a `StackPanel` with a uniform `Spacing`:

| Button | Margin |
|---|---|
| Options | `"8,0,0,0"` |
| Environments | `"6,0"` |
| Import OpenAPI | `"0,0,6,0"` |
| Cookies | `"0,0,6,0"` |
| Logs | *(none)* |

This creates uneven visual gaps between buttons. The left gap before "Options" is 8 px; gaps between the rest vary.

**Fix:** Group right-side buttons in a `StackPanel Orientation="Horizontal" Spacing="6"`.

---

### 1.4 Section Header Font Size — Options vs. Other Panel Views

**File:** `Views/OptionsView.axaml`  
**Severity:** Low  

The Options window page title uses `FontSize="18" FontWeight="SemiBold"`. Every other panel header in the application uses `FontSize="16" FontWeight="SemiBold"` (LogPanelView "Live Log", EnvironmentsView "Manage environments").

**Fix:** Change Options page title to `FontSize="16"` to match the pattern across all panel headers.

---

### 1.5 Secondary Label Opacity — Inconsistent Values

**File:** Multiple views  
**Severity:** Low  

"Descriptive labels" adjacent to inputs use varying opacity:

| Location | Opacity |
|---|---|
| MainWindow toolbar "Env:" | 0.7 |
| LeftPanelView "Sort:", "Show:" | 0.7 |
| RequestView "Type" label | 0.8 |
| GraphQL "Operation name" label | 0.8 |

Both `0.7` and `0.8` are used for labels that serve the same purpose (describing a nearby input/control). The standard should be `0.7` (less prominent, clearly secondary).

**Fix:** Standardize to `Opacity="0.7"` for all secondary input labels.

---

### 1.6 Response Headers Label — Hardcoded FontSize="12"

**File:** `Views/ResponseView.axaml`  
**Severity:** Low  

The "Response headers" section header uses:
```xml
<TextBlock Text="Response headers" FontWeight="SemiBold" FontSize="12" Opacity="0.7" />
```
All other section labels within views use `FontWeight="SemiBold"` without an explicit `FontSize` override (they inherit the window font size). This label is unnecessarily smaller.

**Fix:** Remove the hardcoded `FontSize="12"` attribute.

---

### 1.7 Avalonia Official Documentation Reference Missing from Instructions

**File:** `.github/instructions/avalonia.instructions.md`  
**Severity:** Low (process)  

The issue comment specifically requests: *"For reference to avalonia use official documentation, add this to instructions for avalonia."*

**Fix:** Add a link to the official Avalonia documentation at the top of the instruction file.

---

## 2. Items Confirmed Consistent ✅

The following were reviewed and found consistent between Light and Dark themes:

- **Method label colors** (`MethodGetBrush`, `MethodPostBrush`, etc.): Each has a unique, accessible color per theme. All pairs pass WCAG AA 4.5:1 as verified by `AccessibilityContrastTests.cs`.
- **HTTP status colors** (`StatusSuccessBrush`, etc.): Correctly themed; test coverage complete.
- **Variable token colors** (`VariableBracketBrush`, `VariableNameBrush`): Accessible in both themes.
- **Border and surface brushes**: All use `DynamicResource` and resolve correctly in both Dark and Light themes.
- **`VariableTextBox` custom control**: Correctly reads `PanelBorderBrush` and `SurfaceBackgroundBrush` via `TryGetResource` for per-theme colors; correctly propagates font via `FontFamilyProperty`/`FontSizeProperty`.
- **Response body editors**: `ResponseView.axaml.cs` code-behind correctly calls `ApplyEditorFont` when `UiFontFamily`/`UiFontSize` change, and switches TextMate theme on `ActualThemeVariantChanged`.
- **Request URL editor**: Uses `Background="Transparent"`, `HorizontalScrollBarVisibility="Hidden"`, `VerticalScrollBarVisibility="Hidden"`, consistent with Fluent TextBox metrics.
- **App-level `TextBox` font style**: `App.axaml` applies `{DynamicResource AppFontFamily}` globally to all `TextBox` controls.
- **Options navigation**: Uses `DynamicResource OptionsNavBackgroundBrush` for left-panel background — correctly themed.

---

## 3. Fixes Applied

| # | File | Change |
|---|---|---|
| 1 | `Views/LogPanelView.axaml` | Replace hardcoded font families with `{DynamicResource AppFontFamily}` |
| 2 | `Views/LeftPanelView.axaml` | Change cancel button `✗` → `✕` |
| 3 | `Views/MainWindow.axaml` | Wrap right-side toolbar buttons in `StackPanel Spacing="6"` |
| 4 | `Views/OptionsView.axaml` | Page title `FontSize="18"` → `FontSize="16"` |
| 5 | `Views/RequestView.axaml` | Labels `Opacity="0.8"` → `Opacity="0.7"` |
| 6 | `Views/ResponseView.axaml` | Remove `FontSize="12"` override on "Response headers" label |
| 7 | `.github/instructions/avalonia.instructions.md` | Add official Avalonia docs reference |

---

## 4. UI/UX Rating vs. Similar Clients

### Compared Tools

| Tool | Approach | Target audience |
|---|---|---|
| Postman | Electron, cross-platform | Teams, enterprise, testing |
| Insomnia | Electron, cross-platform | Developers, REST/GraphQL |
| Hoppscotch | Web-first, PWA | Browser-centric developers |
| Bruno | Electron, offline-first | Privacy-conscious devs (git-native) |
| HTTPie Desktop | Electron | CLI-familiar developers |
| **Arbor.HttpClient** | Avalonia, .NET-native | .NET/C# developers |

### Rating (after fixes)

| Dimension | Score | Notes |
|---|---|---|
| **Visual theme consistency** | 8/10 | Both Dark and Light themes apply correctly to all surfaces; WCAG AA contrast met throughout; minor label opacity variance fixed in this PR |
| **Font coherence** | 8/10 | App-wide font preference respected by TextBox, TextEditor URL bar, VariableTextBox, and response editors; log panel fix applied; monospace font matches developer expectations |
| **Layout and spacing** | 7/10 | Dock-based layout is powerful and flexible; toolbar button spacing fixed; section header sizes now consistent; compound field alignment (method+URL+Send as one Border) is polished and Insomnia-inspired |
| **Accessibility** | 8/10 | All color pairs verified at WCAG AA 4.5:1; keyboard navigation supported throughout; remove buttons consistently labelled; `AutomationProperties.Name` on icon buttons |
| **Request authoring UX** | 7/10 | Method + URL + Send as single seamless bar is excellent; variable `{{token}}` highlighting is a standout feature; auth tab covers Bearer/Basic/API Key/OAuth2; Ctrl+Enter to send is natural |
| **Response display** | 7/10 | TextMate syntax highlighting switches with theme; status code color coding is clear; Copy/Save/cURL buttons discoverable; response headers panel is compact and practical |
| **Collection management** | 7/10 | Tree view, sorting, and display modes are strong; search works live; could benefit from drag-to-reorder |
| **Overall** | **7.4/10** | Competitive with mid-tier HTTP clients; standout: native .NET performance, no Electron overhead, excellent dark/light theme support, variable highlighting. Gap to close: tabbed requests, collection sharing |

### Strengths vs. Competitors

1. **Native rendering** — Avalonia renders at native speed with no Chromium process overhead. Response display stays smooth even with large payloads that freeze Electron-based tools.
2. **Variable highlighting** — The `{{variable}}` token colorizer in the URL bar and header fields is implemented on par with Postman and Hoppscotch.
3. **GraphQL, WebSocket, SSE** — First-class protocol support beyond REST is rare in open-source clients (Hoppscotch does it; Insomnia does it; Bruno is REST-only).
4. **Theme quality** — Dark and Light themes are both polished. The WCAG-tested color palette is more rigorous than most open-source clients.
5. **Scheduled jobs** — Periodic request execution with web-view preview is a unique feature not available in Postman Free or Insomnia Community.

### Gaps vs. Competitors

1. **No tabbed requests** (UX idea 3.1) — Postman, Insomnia, and Hoppscotch all support multiple open requests as tabs. This is the most significant UX gap.
2. **No cURL paste-import** (UX idea 1.1) — All major clients detect a cURL command on the clipboard and pre-fill the composer.
3. **No collection sharing** — Bruno and Hoppscotch offer Git-native or URL-based sharing. This client's collections are stored in local SQLite only.
4. **gRPC is placeholder** — The gRPC tab shows an informational message but no working proto import or call support.
5. **No environment switching from the main toolbar** (minor) — Environment selection is at the top right but is a `ComboBox`; Hoppscotch's environment switcher is more discoverable.

---

*Report generated: 2026-04-25. Re-run after implementing tabbed requests (3.1) and cURL paste-import (1.1) for an updated rating.*

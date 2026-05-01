# UX Improvement Ideas

This catalog collects UX enhancement ideas inspired by reviewing Hoppscotch, Insomnia, HTTPie Desktop, Postman, and browser DevTools.  
Each idea includes a description of what it means in practice, notes on how it could be implemented in this Avalonia/.NET codebase, and a rough scope estimate.

**Scope key**

| Label | Meaning |
|-------|---------|
| S | Small — a few hours; self-contained UI or model tweak |
| M | Medium — 1–3 days; new control, converter, or view-model feature |
| L | Large — 1–2 weeks; new subsystem, cross-cutting model change, or significant UI restructuring |
| XL | Extra-large — multi-week; architectural change or major new feature area |

> **Maintenance note:** On every PR, move implemented ideas to the [Implemented](#implemented) section at the bottom and add a reference to the PR number, commit SHA, and relevant file(s). See `.github/copilot-instructions.md` section 13 for the full workflow.

---

## Not Yet Implemented

### 1. Composer / Request Authoring

### 1.1 Paste cURL import
**What it means:** A button or keyboard shortcut that detects a cURL command on the clipboard and pre-fills the method, URL, headers, and body automatically. Used daily in Postman and Insomnia to capture requests from browser DevTools.

**How to implement:**
- Add an `ImportFromClipboardCommand` on `MainViewModel`.
- Parse the clipboard string with a `CurlParser` class that extracts `-X`, `-H`, `--data-raw`, `--data-binary`, and `--user` flags using a simple state-machine string parser (no external library needed).
- Populate the relevant VM properties and navigate to the request tab.

**Scope:** M

---

### 1.2 `{{variable}}` autocomplete in the URL and header fields
**What it means:** While the user is typing `{{`, a drop-down lists the defined environment variables so they can be inserted without switching to the variables panel. Common in Postman and Hoppscotch.

**How to implement:**
- The URL field already uses `AvaloniaEdit.TextEditor`; register a custom `ICompletionData` provider that triggers on `{` input.
- Completion items are sourced from `EnvironmentViewModel.Variables` via the parent VM.
- Headers tab text boxes are plain `TextBox`; for those, a simpler `AutoCompleteBox` overlay positioned under the caret can be used.

**Scope:** L

---

### 1.4 Auth helper tab
**What it means:** A dedicated "Auth" tab next to Body/Headers that generates the correct `Authorization` header for common schemes — Bearer token, Basic (username/password), API Key, OAuth 2 client-credentials flow — so the user does not need to craft headers manually.

**How to implement:**
- Add an `AuthMode` enum and an `AuthViewModel` with sub-view-models per mode.
- On Send, the `HttpRequestBuilder` merges the auth header before creating the `HttpRequestMessage`.
- The tab is a `ContentControl` bound to `AuthViewModel.CurrentMode` with `DataTemplate` per mode.

**Scope:** M

---

### 1.5 Pre/post scripting ✅ Implemented
> Implemented in PR #141 — `src/Arbor.HttpClient.Core/Scripting/`, `src/Arbor.HttpClient.Desktop/Features/Scripting/`, `src/Arbor.HttpClient.Desktop/Features/HttpRequest/RequestView.axaml`, `src/Arbor.HttpClient.Desktop/Features/Main/MainWindowViewModel.cs`

**What it means:** C# script snippets that run before a request is sent (to set variables, compute signatures) and after a response is received (to extract tokens, assert conditions). Postman uses JavaScript; Bruno uses JavaScript; some tools support Tengo or Lua.

> **See also:** [`docs/scripting-ideas.md`](scripting-ideas.md) for a full evaluation of scripting approaches — comparing Roslyn `CSharpScript`, Westwind.Scripting, .NET 10 file scripts, Jint (JavaScript), and Lua — including pros/cons, tradeoffs, and a recommended approach. The preferred solution is **Roslyn `CSharpScript` (in-process)** rather than Jint, because the target audience is C# developers and C# scripting avoids a language context-switch.

**What shipped:**
- `ScriptContext`, `ScriptResponse`, `ScriptResult`, `IScriptRunner` in `Arbor.HttpClient.Core/Scripting/` — no Roslyn dependency in Core
- `RoslynScriptRunner` in `Arbor.HttpClient.Desktop/Features/Scripting/` — compiles and executes C# scripts using `Microsoft.CodeAnalysis.CSharp.Scripting` 5.3.0; SHA-256 content-hash cache avoids re-compilation
- `ScriptViewModel` — observable VM with `PreRequestScript`, `PostResponseScript`, `Errors`, `Log` collections and `ClearLog`/`ClearErrors` commands
- Script tab in `RequestView.axaml` — two sub-tabs (Pre-request / Post-response), each with a `AvaloniaEdit.TextEditor`; collapsible error panel and script log panel shown after execution
- Scripts receive `ctx` (a named `ScriptContext`) with `ctx.Method`, `ctx.Url`, `ctx.Headers`, `ctx.Body`, `ctx.Env` (read/write), `ctx.Response` (post-response only, with `BodyJson` parsed via STJ), `ctx.Log(msg)`, `ctx.Assert(condition, msg)`
- Pre-request script mutations to `Method`, `Url`, `Body` propagate into the actual HTTP request; `Env` mutations write back to the active environment
- 12 unit tests for Core scripting types (`ScriptContextTests`); 14 unit tests for `RoslynScriptRunner` (compilation, execution, cancellation, STJ JSON navigation, assertion failures)

**Polish items remaining:**
- C# syntax highlighting in the script editors (requires TextMate grammar for C#)
- Per-request script persistence (save script text alongside request in collection)
- Script execution timeout UI control

**Scope:** XL

---

### 1.6 HAR / Postman / Insomnia / Bruno collection import
**What it means:** Import an existing collection file from another tool so users can migrate without re-entering every request manually.

**How to implement:**
- Add an `ICollectionImporter` interface with implementations per format.
- HAR: parse `entries[].request` from the HAR JSON (well-documented spec).
- Postman v2.1: parse `item[]` tree from JSON.
- Insomnia: parse `resources[]` from JSON export.
- Bruno: parse `.bru` plain-text format (simple key-value grammar).
- Map each format's request model to the internal `RequestDefinition` and persist via the existing storage layer.

**Scope:** L (each importer is S–M; the import UI/UX is M)

---

## 2. Response UX

### 2.1 Inline response diff
**What it means:** A side-by-side or unified diff view comparing the current response body to any previous response for the same request. Useful for detecting API changes during development.

**How to implement:**
- Store the last N response bodies per request in memory (configurable, e.g., 10).
- Add a "Diff" toggle button in the response toolbar.
- Use `AvaloniaEdit` in diff mode with a custom `IHighlightingDefinition` to colour added/removed lines, or use a standalone diff library such as [DiffPlex](https://github.com/mmanela/diffplex) (Apache-2.0) to compute the diff and render it in a custom control.

**Scope:** M

---

### 2.2 Copy / save response shortcuts
**What it means:** Quick-access buttons or keyboard shortcuts for "Copy body to clipboard", "Save body as file", and "Copy as cURL" in the response area. Eliminates the need to select-all + copy manually.

**How to implement:**
- Bind `IClipboard` (already wired for cURL copy on history items) to commands on `ResponseViewModel`.
- "Save as file" uses `IStorageProvider.SaveFilePickerAsync` (already used elsewhere).
- Add a small toolbar row above the response body `AvaloniaEdit` control with icon buttons.

**Scope:** S

---

### 2.3 Diagnostics waterfall ribbon
**What it means:** A horizontal bar chart showing DNS lookup, TCP connect, TLS handshake, time-to-first-byte, and download durations — similar to browser DevTools Network tab — so the user can understand where latency comes from.

**How to implement:**
- `HttpClientHandler` exposes `ConnectCallback` and `PlaintextStreamFilter`; combine with `SocketsHttpHandler` metrics events (`HttpMetricsEnrichmentContext`) or the `DiagnosticListener` named `"HttpHandlerDiagnosticListener"` which emits `RequestStart`, `ConnectionEstablished`, `ResponseHeadersRead`, etc.
- Store the timestamps on `HttpResponseDetails`.
- Render a `Canvas` with proportionally-scaled coloured rectangles in the response panel.

**Scope:** L

---

### 2.4 Image / PDF / HTML response previewers
**What it means:** When the `Content-Type` is `image/*`, `application/pdf`, or `text/html`, show a rendered preview instead of (or alongside) the raw bytes/text.

**How to implement:**
- Image: Avalonia's built-in `Image` control with `new Bitmap(stream)`.
- HTML: embed a `WebView` via the `Avalonia.WebView` community package or spawn a local browser window.
- PDF: use a native PDF library (`PdfiumViewer` port or `Docnet.Core`) or simply offer "Open in default app".

**Scope:** M (image S, HTML M, PDF L)

---

### 2.5 Cookie jar editor
**What it means:** A panel that shows all cookies set by responses, lets the user inspect their values, manually add/edit/delete cookies, and toggle whether they are sent with future requests.

**How to implement:**
- Use `CookieContainer` on `HttpClientHandler`; expose its contents via reflection (`CookieContainer.GetAllCookies()` — available in .NET 6+).
- Bind to a `CookieJarViewModel` with an `ObservableCollection<CookieEntryViewModel>`.
- Show a panel in the left sidebar or as a floating tool window.

**Scope:** M

---

### 2.6 Response body search (Ctrl+F)
**What it means:** An in-panel search bar that highlights all occurrences of a search string in the response body and jumps between matches — like browser Ctrl+F.

**How to implement:**
- `AvaloniaEdit` has a built-in `SearchPanel` that can be attached via `SearchPanel.Install(textEditor)`. No custom rendering needed.
- Wire `Ctrl+F` in the response panel's `KeyBindings`.

**Scope:** S

---

## 3. Navigation / Workflow

### 3.1 Tabbed requests
**What it means:** Multiple open requests as tabs across the top of the main content area, so the user can switch between in-flight or draft requests without losing state — the primary UX paradigm of Postman, Insomnia, and Hoppscotch.

**How to implement:**
- Replace the single `RequestViewModel` on `MainViewModel` with an `ObservableCollection<RequestTabViewModel>` and an `ActiveTab` property.
- The main content area becomes a `TabControl` (or a custom tab bar with underline style) bound to the collection.
- Each tab has its own request state, response, and history reference.
- "New tab" creates a blank `RequestTabViewModel`; "Close tab" removes it (with an unsaved-changes prompt if dirty).

**Scope:** L (touches most of the VM and view layer)

---

### 3.2 Ctrl+K command palette
**What it means:** A fuzzy-search overlay (like VS Code's Ctrl+K / Ctrl+P) listing all saved requests, environment variables, and commands (Send, New Request, Open Options…). Lets power users navigate entirely by keyboard.

**How to implement:**
- Show a borderless `Window` or `Popup` centered on the main window on `Ctrl+K`.
- Items are an `ObservableCollection` combining history entries, collection items, and static commands.
- Filter with a simple `Contains` or integrate `FuzzySharp` (MIT) for fuzzy matching.
- On Enter, execute the selected item's action delegate.

**Scope:** M

---

### 3.3 Chain / runner view
**What it means:** A sequential list of requests that are executed in order, where each request can reference a variable captured from the previous response (e.g., extract a `token` from a login response and inject it into the next request's `Authorization` header). Called "Collection Runner" in Postman.

**How to implement:**
- Model: `RequestChain` contains an ordered list of `ChainStep` (request reference + variable-extraction rules + variable-injection bindings).
- Execution: a `ChainRunner` iterates the steps, running each `HttpClient` call and applying JSONPath / regex extraction rules to update a shared `VariableScope`.
- UI: a simple list view with drag-to-reorder (Avalonia's `ItemsReorderBehavior`) and per-step pass/fail indicators.

**Scope:** XL

---

### 3.4 Global search across all saved requests
**What it means:** A search box that searches by URL, method, header name/value, and body text across all collections and history entries — essential once a user accumulates hundreds of saved requests.

**How to implement:**
- Index the serialized request JSON on save (in-memory trie or simple linear scan for small collections).
- For large collections, SQLite FTS5 (via `Microsoft.Data.Sqlite`) would give sub-millisecond search.
- Display results in a side-panel list with highlighted matching fragments.

**Scope:** M

---

### 3.5 Per-request Markdown notes
**What it means:** A free-text notes field attached to each saved request where the user can document purpose, expected responses, known quirks, etc. Bruno stores these as Markdown in `.bru` files alongside the request.

**How to implement:**
- Add a `Notes` string property to the request persistence model.
- Show a "Notes" tab in the request editor using `AvaloniaEdit` with Markdown syntax highlighting (built-in XML-based highlighting definition).
- Optionally render a preview pane using a Markdown renderer such as `Markdig` (MIT).

**Scope:** S

---

### 3.6 Git-style collection sync
**What it means:** Persist each collection as a folder of plain-text files (e.g., one JSON or `.bru`-style file per request) so the user can version-control their requests with Git. Bruno's entire value proposition is based on this.

**How to implement:**
- Replace (or supplement) the current single-file JSON persistence with a folder-per-collection strategy using `System.IO` directory/file APIs.
- Each request becomes one file named `{method}-{slug}.json`.
- The existing `IRequestStorage` / `ICollectionPersistence` abstraction (or equivalent) would get a new `FileSystemCollectionPersistence` implementation.

**Scope:** M

---

## 4. History & Sharing

### 4.1 Pin / favourite history entries
**What it means:** A star icon on each history entry that moves it to a "Pinned" section at the top of the history list, preventing it from being evicted when the history limit is reached.

**How to implement:**
- Add an `IsPinned` bool to the history entry model and persist it.
- Sort the `ObservableCollection<HistoryEntryViewModel>` with pinned entries first.
- Expose a `TogglePinCommand` bound to a star `Button` in the history item `DataTemplate`.

**Scope:** S

---

### 4.2 Group history by day
**What it means:** Visual day-separator headers ("Today", "Yesterday", "Mon 14 Apr") in the history list, grouping requests by when they were sent.

**How to implement:**
- Use a `CollectionViewSource` with a `GroupDescription` keyed on `DateOnly` derived from `HistoryEntry.SentAt`.
- In the `ItemsControl` template, use `GroupStyle` with a separator header `DataTemplate`.

**Scope:** S

---

### 4.3 Share request as URL / gist / Markdown
**What it means:** A "Share" menu that serialises the current request to a shareable format: a URL with base64-encoded state (like Hoppscotch's share links), a GitHub Gist, or a Markdown code block that can be pasted into documentation.

**How to implement:**
- URL share: base64-encode a minimal JSON representation and append to a well-known prefix (works offline, no server needed).
- Gist: POST to `https://api.github.com/gists` using a personal access token stored in the credential store.
- Markdown: format as a fenced code block with `http` syntax highlighting.

**Scope:** M

---

### 4.4 "Resend" in history context menu
**What it means:** Right-click a history entry → "Resend" loads the original request back into the composer and immediately sends it, or just loads it for editing. Already partially available via "Load" but not as a one-click resend.

**How to implement:**
- Add a `ResendCommand` to `HistoryEntryViewModel` that calls `LoadIntoComposer` then `SendRequestCommand` in sequence.
- Wire to a context menu item in the history `DataTemplate`.

**Scope:** S

---

## 5. Diagnostics & Performance

### 5.1 DNS / TLS / TTFB waterfall visualisation
*(See 2.3 above — listed here for completeness as it is also a diagnostics feature.)*

**Scope:** L

---

### 5.2 Two-response diff tool
**What it means:** Select any two history entries and compare their response bodies side-by-side. Useful for regression testing (e.g., "did this API change between yesterday and today?").

**How to implement:**
- Add multi-select to the history list (`SelectionMode="Multiple"`).
- When exactly two items are selected, enable a "Diff" toolbar button.
- Open a `DiffWindow` with two `AvaloniaEdit` panels (or a unified diff view) using `DiffPlex` to compute the diff.

**Scope:** M

---

### 5.3 History size bar chart
**What it means:** A mini bar chart in the history panel showing response size over time for a given URL pattern — lets the user spot responses that are growing unexpectedly.

**How to implement:**
- Filter history entries by base URL.
- Compute `ContentLength` per entry.
- Render a `Canvas`-based bar chart (no charting library needed for a simple bar chart of this scale) or use `LiveChartsCore.SkiaSharpView.Avalonia` (MIT).

**Scope:** M

---

## 6. Usability & Accessibility

### 6.1 Resizable environment sidebar
**What it means:** The left panel (currently fixed or with a minimal splitter) should be freely resizable so users with many environment variables or long collection names can expand it.

**How to implement:**
- Wrap the left panel and main content in a `Grid` with a `GridSplitter` between the two columns.
- Persist the last column width in user settings.

**Scope:** S

---

### 6.2 Customisable keyboard shortcuts
**What it means:** A settings page where the user can remap the application's keyboard shortcuts (Send, New Request, Focus URL, etc.) to their own preferred keys.

**How to implement:**
- Define shortcuts as a `Dictionary<CommandName, KeyGesture>` stored in user settings.
- On startup, replace the static `KeyBindings` on the affected controls with dynamically-constructed `KeyBinding` instances.
- Settings UI: a two-column list (Command | Shortcut) with an editable shortcut capture control.

**Scope:** M

---

### 6.3 Focus / zen mode
**What it means:** A toggle that hides the sidebar, toolbar, and status bar, leaving only the URL bar, body editor, and response panel — maximises screen real-estate for writing and reading.

**How to implement:**
- Bind the `Visibility` of the sidebar and toolbar rows to a `IsFocusModeActive` bool on `MainViewModel`.
- Toggle with a menu item or keyboard shortcut (`F11` / `Ctrl+Shift+F`).

**Scope:** S

---

### 6.4 High-contrast and colour-blind-safe themes
**What it means:** Additional theme variants (high-contrast for users with low vision, and a colour-blind-safe palette that avoids red/green confusion) selectable from Options.

**How to implement:**
- Add new `ResourceDictionary.ThemeDictionaries` entries (`HighContrast`, `Deuteranopia`) in `App.axaml` with alternative colour values.
- Extend the `ThemeSelector` in Options to list these new variants.
- Verify all foreground/background pairs meet WCAG 2.1 AA contrast ratios (≥ 4.5:1) and add test cases to `AccessibilityContrastTests.cs`.

**Scope:** M

---

### 6.5 UI density toggle
**What it means:** A "Compact / Normal / Spacious" density switch that adjusts padding, font size, and row heights globally — useful on small laptop screens vs. large monitors.

**How to implement:**
- Define a `UiDensity` enum stored in user settings.
- In `App.axaml`, expose `DensityPadding` and `DensityFontSize` as dynamic resources.
- On density change, update those resources and propagate via `Application.Current.Resources`.

**Scope:** M

### 6.6 Language / Locale selector
**What it means:** A dropdown in the Options › Look & Feel page that lets the user override the application display language without changing the OS locale. The localization infrastructure (`Strings.resx` + `System.Resources.ResourceManager`) is already in place; this idea covers the UI entry point and translation files.

**How to implement:**
- Add a `Language` option to `ApplicationOptions` (e.g. `"default"`, `"en"`, `"de"`, `"fr"`, etc.).
- On startup and on change, call `Thread.CurrentThread.CurrentUICulture = new CultureInfo(language)`.
- Since `{x:Static}` values are effectively read when the XAML is loaded and do not automatically refresh when the culture changes at runtime, dynamic re-translation requires either (a) restarting the app after a language change, or (b) replacing `{x:Static}` with a custom `LocalizationExtension` that responds to culture-change notifications.
- Add at least one `Strings.<culture>.resx` file (e.g. `Strings.de.resx`) as a proof-of-concept.

**Scope:** M

---

## 7. Environments

### 7.1 Environment color indicator ✅ Implemented
> Implemented in PR #114 (commit `71a8e69`) — `src/Arbor.HttpClient.Core/Environments/RequestEnvironment.cs`, `src/Arbor.HttpClient.Core/Environments/IEnvironmentRepository.cs`, `src/Arbor.HttpClient.Storage.Sqlite/SqliteEnvironmentRepository.cs`, `src/Arbor.HttpClient.Desktop/Features/Environments/EnvironmentsViewModel.cs`, `src/Arbor.HttpClient.Desktop/Features/Environments/EnvironmentsView.axaml`, `src/Arbor.HttpClient.Desktop/Features/Main/MainWindow.axaml`

**What it means:** Each environment can be assigned an optional accent color (e.g. red for Production, green for Development). When a colored environment is active the color is reflected in the `Env:` dropdown background in the top toolbar, as a small badge dot on the Environments activity-bar icon, and optionally as a full-width warning banner below the toolbar. The color is configured in the Environments panel via a small row of preset color swatches.

**Full design proposal:** [`docs/environment-color-report.md`](environment-color-report.md)

**What shipped:**
- `AccentColor` (nullable hex string) and `ShowWarningBanner` (bool) added to `RequestEnvironment` and wired through `IEnvironmentRepository`, `SqliteEnvironmentRepository` (with schema migration), and `InMemoryEnvironmentRepository`
- Five preset color swatches in the Environments edit form (Red `#B41E1E`, Amber `#8B5500`, Green `#1E7A3C`, Blue `#1E50B4`, Purple `#6A1EB4`) plus a "∅ none" clear option (Pattern C)
- Toolbar `Env:` ComboBox background/foreground tinted by the active environment's accent color — white text enforced via `HexColorToForegroundConverter` (Pattern A)
- Full-width warning banner with environment name shown below the toolbar when `ShowWarningBanner = true` (Pattern B)
- Activity-bar Environments icon shows a colored badge dot when a color is set (Pattern D)
- WCAG 2.1 AA verified: all five presets achieve ≥ 4.5:1 contrast against white text; verified in `AccessibilityContrastTests.cs`
- **Color dot visible in dropdown items before selection:** Each environment in the `Env:` ComboBox dropdown now shows a colored dot next to its name so users can see environment colors before making a selection (fixes UX issue where color only appeared after selection was done). Implemented in `MainWindow.axaml` `ItemTemplate`.
- **Hover text fixed in light theme:** Added `:pointerover`, `:selected`, and `:selected:pointerover` foreground styles for `ComboBox.EnvSelector ComboBoxItem` to prevent white-on-white text in the dropdown hover state in light theme.
- **Accent color preserved on ComboBox hover:** Added `ComboBox.EnvSelector:pointerover /template/ Border#Background` style to keep the environment's accent color visible on hover (using slight opacity) rather than switching to the Fluent default hover color.

**Remaining polish items:**
- No live contrast-ratio warning when a custom hex color is entered (only preset swatches are offered for now)
- No deuteranopia/high-contrast theme variant yet (complementary UX idea 6.4 still open)

---

## Implemented

> Ideas move here once their primary UX behaviour is usable in the application. Each entry retains its original description and adds an implementation reference. Do not delete entries — this section is a historical record.

### Persistent Layout and Request/Response Split View ✅ Implemented
> Implemented in PR (this PR) — `src/Arbor.HttpClient.Desktop/Features/Layout/DockFactory.cs`, `src/Arbor.HttpClient.Desktop/Features/Layout/DockLayoutSnapshot.cs`, `src/Arbor.HttpClient.Desktop/Features/Main/MainWindowViewModel.cs`, `src/Arbor.HttpClient.Desktop/Features/Main/MainWindow.axaml.cs`, `src/Arbor.HttpClient.Desktop/Options/ApplicationOptionsStore.cs`, `src/Arbor.HttpClient.Desktop/App.axaml.cs`

**What it means:** The dock layout (panel sizes, tool panel order, floating window positions) is persisted to `options.json` when the application closes and fully restored on next launch. The response panel now appears below the request panel by default so both are visible simultaneously without switching tabs.

**What shipped:**
- `DockFactory` now creates a vertical `ProportionalDock` (`document-layout`) containing a `request-dock` (top, 60% height) and `response-dock` (bottom, 40% height) instead of a single tabbed `DocumentDock`
- `DockLayoutSnapshot` stores `RequestDockProportion` and `ResponseDockProportion` so the user's resized split is remembered across restarts
- `DockLayoutSnapshot` now also stores `WindowWidth`, `WindowHeight`, `WindowX`, `WindowY` so the main window size and position are fully restored across restarts
- `MainWindow.OnClosing` now calls `SyncDockProportionsFromVisuals()` (walks the visual tree, reads actual `ProportionalStackPanel.ProportionProperty` values, and writes them back to the model) before persisting — this is the reliable source of truth regardless of binding-propagation timing
- `App.axaml.cs` calls `viewModel.ReapplyStartupLayout()` from `window.Opened` so that saved proportions are re-applied to the dock model after the visual tree and TwoWay PSP bindings are established; this fixes the root cause where `ProportionalStackPanel.AssignProportions` fires before bindings exist and propagates equal-distribution values back to the model via TwoWay binding
- `MainWindow.OnClosing` records the window geometry via `viewModel.SetWindowGeometry()` before `PersistCurrentLayout()` so the full window state is captured
- `App.axaml.cs` restores window width, height, and position from the saved snapshot when creating the main window
- Two new E2E tests: `Layout_DefaultSplitView_ShouldShowRequestAboveResponse` and `Layout_SplitViewProportions_ShouldPersistAcrossRestarts`
- KVM/Alpine system test (`scripts/start-ui-automation-kvm-alpine.sh`) updated with layout persistence steps: drags the main splitter, closes the app, relaunches, and screenshots before/after for human comparison; also retrieves `options.json` so reviewers can verify the saved proportions

**Polish items remaining:**
- Keyboard shortcut to resize request/response split
- Per-tab layout persistence (when multi-tab is added)

---

### 1.2b System environment variable support (`{{env:VAR}}`) ✅ Implemented
> Implemented in PR #116 (commit `b1437c8`) — `src/Arbor.HttpClient.Core/Variables/ISystemEnvironmentVariableProvider.cs`, `src/Arbor.HttpClient.Core/Variables/SystemEnvironmentVariableProvider.cs`, `src/Arbor.HttpClient.Core/Variables/VariableResolver.cs`, `src/Arbor.HttpClient.Desktop/Features/Variables/VariableTokenColorizer.cs`, `src/Arbor.HttpClient.Desktop/Features/Variables/VariableCompletionEngine.cs`, `src/Arbor.HttpClient.Desktop/Features/Variables/VariableAutoCompleteController.cs`, `src/Arbor.HttpClient.Desktop/App.axaml`

**What it means:** Users can reference the host machine's system (process) environment variables directly in requests using `{{env:SomeVariable}}`. This is distinct from the in-app environment variables (`{{variableName}}`), which are managed in the Environments panel.

**What shipped:**
- `{{env:VarName}}` tokens are resolved at send time via `VariableResolver` using the real process environment
- `ISystemEnvironmentVariableProvider` abstraction allows tests to inject a fake set of env vars without depending on the real process environment (`FakeSystemEnvironmentVariableProvider` in `Arbor.HttpClient.Testing`)
- Autocomplete: typing `{{env:` in the URL bar or request body triggers a drop-down listing all available system environment variables, filtered as you type
- Syntax coloring: `env:` prefix is rendered in a distinct teal color (`EnvVariablePrefixBrush`) separate from the bracket color (amber) and variable-name color (violet) — both Dark and Light theme variants meet WCAG AA ≥ 4.5:1
- Env prefix lookup is case-insensitive (`{{ENV:VAR}}` and `{{env:var}}` both work)
- If the referenced variable is not set, the token collapses to an empty string (consistent with app variable behavior)

**Polish items remaining:**
- Header fields use plain `TextBox` controls — system env variable autocomplete in header keys/values requires additional work (see idea 1.2)

---

### Localization Infrastructure ✅ Implemented
> Implemented in PR #106 (commit `00b551e`) — `src/Arbor.HttpClient.Desktop/Localization/Strings.resx`, `src/Arbor.HttpClient.Desktop/Localization/Strings.Designer.cs`, and all 15 Desktop AXAML view files

**What it means:** All user-visible texts in the Desktop UI are stored in a `.resx` resource file (`Strings.resx`) and referenced from AXAML via `{x:Static loc:Strings.KeyName}`. Adding a new translation requires only creating a `Strings.<culture>.resx` file — the `System.Resources.ResourceManager` picks up the correct file automatically based on `Thread.CurrentThread.CurrentUICulture`.

**What shipped:**
- `Localization/Strings.resx` — 130+ English string keys covering all AXAML files
- `Localization/Strings.Designer.cs` — Strongly-typed static accessor class; throws in DEBUG builds when a key is missing
- All 14 AXAML files updated to use `{x:Static loc:Strings.KeyName}` instead of hardcoded strings
- Translator comments added to `Strings.resx` for mnemonic underscores and technical terms that should not be translated

**Polish items remaining:**
- No language selector UI yet (see "Language / Locale selector" idea below)
- The `Strings.Designer.cs` is hand-authored (the `PublicResXFileCodeGenerator` requires Windows tooling); future maintainers should regenerate it when adding new keys

### About Window ✅ Implemented
> Implemented in PR #96 (commit `27e3f57`) — `src/Arbor.HttpClient.Desktop/ViewModels/AboutWindowViewModel.cs`, `src/Arbor.HttpClient.Desktop/Views/AboutWindow.axaml`, `src/Arbor.HttpClient.Desktop/Views/AboutWindow.axaml.cs`, `src/Arbor.HttpClient.Desktop/Views/MainWindow.axaml`, `src/Arbor.HttpClient.Desktop/Views/MainWindow.axaml.cs`, `src/Arbor.HttpClient.Desktop/ViewModels/MainWindowViewModel.cs`

**What it means:** An About window accessible from the Help menu that shows the application version, the git commit hash the binary was built from, MIT license attribution, copyright, and a link to the GitHub repository.

**What shipped:**
- `AboutWindowViewModel` reads `AssemblyInformationalVersion` (format `1.0.0+<hash>`) to expose `AppVersion`, `GitHash`, `BuildLabel`, `Copyright`, `License`, `LicenseText`, and `GitHubUrl`
- `Directory.Build.targets` embeds the short git hash into the assembly via the SDK's `SourceRevisionId` mechanism (gracefully omitted when git is unavailable)
- `AboutWindow` — a fixed-size dialog with version/hash label, MIT license text, copyright, and a clickable GitHub link
- Help > About menu item in `MainWindow.axaml`
- `OpenAboutWindowAction` delegate on `MainWindowViewModel` (same pattern as `ExitApplicationAction`) wired in `MainWindow.axaml.cs`

---

### 1.3 GraphQL / WebSocket / SSE / gRPC request types ✅ Implemented
> Implemented in PR (commit TBD) — `src/Arbor.HttpClient.Core/Models/RequestType.cs`, `src/Arbor.HttpClient.Core/Services/GraphQlService.cs`, `src/Arbor.HttpClient.Core/Services/WebSocketService.cs`, `src/Arbor.HttpClient.Core/Services/SseService.cs`, `src/Arbor.HttpClient.Desktop/ViewModels/GraphQlViewModel.cs`, `src/Arbor.HttpClient.Desktop/ViewModels/WebSocketViewModel.cs`, `src/Arbor.HttpClient.Desktop/ViewModels/SseViewModel.cs`, `src/Arbor.HttpClient.Desktop/Views/RequestView.axaml`

**What it means:** First-class support for protocols beyond HTTP/1.1 REST. GraphQL sends a POST with a `query`/`variables` JSON body and introspects the schema. WebSocket and SSE connections stay open and stream events. gRPC uses Protobuf over HTTP/2.

**What shipped:** `RequestType` enum, GraphQL service + VM + UI (query/variables/introspection), WebSocket service + VM + UI (connect/send/receive), SSE service + VM + UI (event streaming), gRPC UI placeholder. 23 new unit tests.

**Polish items remaining:** gRPC proto import, WebSocket binary frames, SSE auto-reconnect, GraphQL variable completion from schema.

---

### 6.6 Auto-save drafts per tab ✅ Implemented
> Implemented in PR #62 (commit `0feb9e9`) — `src/Arbor.HttpClient.Desktop/Services/DraftPersistenceService.cs`, `src/Arbor.HttpClient.Desktop/Models/DraftState.cs`, `src/Arbor.HttpClient.Desktop/ViewModels/MainWindowViewModel.cs`, `src/Arbor.HttpClient.Desktop/Views/MainWindow.axaml`

**What it means:** The current in-progress request (URL, headers, body, selected environment) is saved automatically every few seconds so that an unexpected crash does not lose unsaved work.

**What shipped:** `DraftPersistenceService` serialises `RequestEditorViewModel` state to `drafts/draft.json` in the app data directory on a 30-second `PeriodicTimer`. On startup, if a draft file exists, a banner is shown offering to restore or discard it. The draft file is cleared on clean application exit. 10 unit tests added.

**Polish items remaining:** Per-tab drafts once tabbed request UI (3.1) is implemented; offer to restore environment selection alongside the request state.

---

### 2.4 Scheduled jobs web view ✅ Implemented
> Implemented in PR (see copilot/send-scheduled-request-web-view) — `src/Arbor.HttpClient.Desktop/Views/WebViewWindow.axaml`, `src/Arbor.HttpClient.Desktop/Views/WebViewWindow.axaml.cs`, `src/Arbor.HttpClient.Desktop/ViewModels/ScheduledJobViewModel.cs`, `src/Arbor.HttpClient.Desktop/Services/ScheduledJobService.cs`, `src/Arbor.HttpClient.Storage.Sqlite/SqliteScheduledJobRepository.cs`

**What it means:** Scheduled GET jobs can optionally open a `WebViewWindow` that embeds the platform-native browser engine (WebView2 on Windows, WebKit on macOS, WebKitGTK on Linux) and navigates to the job's URL. The window auto-refreshes on each completed tick.

**What shipped:**
- `Avalonia.Controls.WebView` 12.0.0 (MIT, official Avalonia package) added
- `WebViewWindow` — floating window with `NativeWebView`, navigation bar (Back / Forward / Refresh / URL / Go), auto-refresh on each scheduled tick, subscribes to `ScheduledJobViewModel.LastResponseStatus` changes
- "Show in web view" checkbox on each scheduled job card (GET-only)
- "🌐 Web view" button opens the window on demand; hidden when not applicable
- `UseWebView` flag persisted in SQLite, `ScheduledJobService` passes `HandleResponse` callback when enabled
- Last-response status badge (status code + local timestamp) shown inline after first tick

**Polish items remaining:** Image response preview (`image/*`), PDF viewer, HTML preview in the main request/response panel (not just scheduled jobs).

---

---

### Collections Management ✅ Implemented
> Implemented in PR #77 (commit `237d9de`) — `src/Arbor.HttpClient.Core/Abstractions/ICollectionRepository.cs`, `src/Arbor.HttpClient.Storage.Sqlite/SqliteCollectionRepository.cs`, `src/Arbor.HttpClient.Testing/Repositories/InMemoryCollectionRepository.cs`, `src/Arbor.HttpClient.Desktop/ViewModels/CollectionItemViewModel.cs`, `src/Arbor.HttpClient.Desktop/ViewModels/CollectionGroupViewModel.cs`, `src/Arbor.HttpClient.Desktop/ViewModels/MainWindowViewModel.cs`, `src/Arbor.HttpClient.Desktop/Views/LeftPanelView.axaml`

**What it means:** Users can create and manage named collections of HTTP requests directly inside the app, search/filter them, sort by different fields, choose which parts of the request URL to display, and switch to a tree view that groups requests by top-level path segment.

**What shipped:**
- **Create collections**: inline "+ New" form in the Collections tab creates an empty collection
- **Add current request**: "+ Add request" button appends the current URL/method as a new collection entry
- **Search**: live filter box narrows the visible requests by name, path, or method
- **Sort**: toolbar switches between Default, Name, Method, Path ordering
- **Display modes**: toolbar cycles between Name+Path, Name only, Path only, Full URL
- **Tree view**: "🌿 Tree" toggle groups requests by their first path segment (e.g. `/pets/…` → "pets"), each group is individually collapsible
- **Import OpenAPI**: existing OpenAPI import button moved into the per-collection toolbar
- **Rename collections**: inline "✏ Rename" form in the per-collection toolbar; uniqueness enforced (case-insensitive); rename form hidden on collection change
- **Unique name enforcement**: creating or renaming a collection with a name already in use shows an error and is blocked
- `ICollectionRepository.UpdateAsync` added; SQLite and in-memory implementations updated
- `CollectionItemViewModel`: `FullUrl` and `GroupKey` computed properties
- `CollectionGroupViewModel`: collapsible group VM with `ToggleExpandedCommand`
- `BoolToExpandIconConverter`: `▼`/`▶` expand/collapse icons

**Polish items remaining:** reorder requests via drag-and-drop; export a collection as JSON/OpenAPI; per-request notes editor in the collection entry.

---

### Layout Management Dockable Panel ✅ Implemented
> Implemented in PR #86 (commit `e412b95`) — `src/Arbor.HttpClient.Desktop/ViewModels/LayoutManagementViewModel.cs`, `src/Arbor.HttpClient.Desktop/Views/LayoutManagementView.axaml`, `src/Arbor.HttpClient.Desktop/ViewModels/DockFactory.cs`, `src/Arbor.HttpClient.Desktop/Views/MainWindow.axaml`

**What it means:** The window layout management controls (save/restore/remove named layouts) are now a proper dockable tool panel in the left dock, instead of a fixed horizontal bar toggled at the top of the window. The panel can be moved, floated, and repositioned like all other tool panels.

**What shipped:**
- New `LayoutManagementViewModel` (extends `Tool`) with proxy commands to `MainWindowViewModel`
- New `LayoutManagementView.axaml` with the "Window Layout" title, "Saved layouts:" ComboBox, and "Save As New", "Save to Selected", "Remove Selected", "Restore Default" buttons
- `DockFactory` adds the Layout panel as the sixth dockable in the left `ToolDock`
- `Window > Layout` menu item now focuses/activates the Layout dockable instead of toggling a fixed bar
- Old inline `Border` layout bar removed from `MainWindow.axaml`

**Polish items remaining:** Keyboard shortcut to focus the Layout panel directly.

---

### Full Menu Bar Coverage ✅ Implemented
> Implemented in PR #93 (commit `4d99849`) — `src/Arbor.HttpClient.Desktop/Features/Main/MainWindow.axaml`

**What it means:** Every feature panel and action in the application is reachable from the top menu bar. There is no case where a panel is closed and cannot be re-opened.

**What shipped:**
- `File` menu: Import OpenAPI… (`ImportCollectionCommand`) and Exit
- `View` menu: History, Collections, Scheduled Jobs (left-panel tabs), Options, Environments, Cookies, Logs, Layout (left tool-dock panels)
- `Help` menu: About
- The old `Window > Layout` item replaced by `View > Layout` together with all other panel items

---

### Sensitive Variables & TTL ✅ Implemented
> Implemented in PR #118 (commit `03fcf56`) — `src/Arbor.HttpClient.Core/Environments/EnvironmentVariable.cs`, `src/Arbor.HttpClient.Core/Environments/SensitiveVariableDetector.cs`, `src/Arbor.HttpClient.Core/Variables/VariableResolver.cs`, `src/Arbor.HttpClient.Desktop/Features/Environments/EnvironmentVariableViewModel.cs`, `src/Arbor.HttpClient.Desktop/Features/Environments/EnvironmentsView.axaml`, `src/Arbor.HttpClient.Storage.Sqlite/SqliteEnvironmentRepository.cs`

**What it means:** Variables whose names match common sensitive patterns (password, token, secret, apikey, etc.) are automatically flagged as sensitive. Sensitive values are masked in the UI with bullet characters; a per-variable reveal button (👁/🔒) lets the user temporarily show the real value for editing. Any variable can optionally carry a UTC expiry date — when reached, the variable is automatically ignored during request resolution.

**What shipped:**
- `EnvironmentVariable` record gains `IsSensitive` (bool) and `ExpiresAtUtc` (DateTimeOffset?) properties with `IsExpired` computed helper
- `SensitiveVariableDetector` — static class with case-insensitive keyword matching for 18 common sensitive-data patterns; auto-applied when a variable name is typed
- `VariableResolver.Resolve` skips expired variables (token collapses to empty string, consistent with disabled-variable behavior)
- `EnvironmentVariableViewModel` gains `IsSensitive`, `ExpiresAtUtc`, `IsValueRevealed`, `IsValueMasked`, `IsExpired`, `ExpiresAtUtcText`, and `ToggleRevealCommand`; the `OnNameChanged` partial auto-detects sensitivity
- `EnvironmentsView.axaml` — new "Sensitive" checkbox column, value TextBox uses `PasswordChar` converter (`BoolToPasswordCharConverter`) to mask when sensitive, reveal button appears when `IsSensitive=true`, "Expires (UTC)" TextBox column for TTL, expired rows dimmed via `BoolToExpiredOpacityConverter`
- SQLite migration: `is_sensitive INTEGER NOT NULL DEFAULT 0` and `expires_at_utc TEXT` columns added to `environment_variables` via `EnsureEnvironmentVariable*ColumnAsync` helpers (non-destructive upgrade of existing databases)
- Export JSON includes `IsSensitive` and `ExpiresAtUtc` fields

**Polish items remaining:**
- Encryption at rest for sensitive values: see `docs/security-review.md` § "Future — Sensitive Variable Encryption" for guidance
- External secret sources (HashiCorp Vault, OS Keychain, Azure Key Vault): not yet implemented
- Global "show all sensitive values" toggle on the Environments panel (currently per-variable only)

---

*Last updated: April 2026. Suggestions sourced from comparative review of Hoppscotch, Insomnia, Postman, Bruno, and browser DevTools.*

---

### Unhandled Exception Reporting ✅ Implemented
> Implemented in PR (this PR) — `src/Arbor.HttpClient.Desktop/Features/Diagnostics/`, `src/Arbor.HttpClient.Desktop/Features/Options/ApplicationOptions.cs`, `src/Arbor.HttpClient.Desktop/Features/Main/MainWindowViewModel.cs`, `src/Arbor.HttpClient.Desktop/App.axaml.cs`

**What it means:** A global option (disabled by default) that collects unhandled exceptions locally. The user can review them in a dedicated Diagnostics window and optionally report each one as a pre-filled GitHub issue (opened in the browser; nothing is sent automatically).

**What shipped:**
- `DiagnosticsOptions` — new options class with `CollectUnhandledExceptions: bool = false`
- `ApplicationOptions.Diagnostics` — new field; validated and persisted alongside other options
- `UnhandledExceptionCollector` — thread-safe service that captures and persists exceptions to `exceptions.json` in the app data directory (capped at 50 entries)
- Global exception handlers registered in `App.axaml.cs`: `AppDomain.CurrentDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException`
- Non-fatal caught exceptions also collected: `ScheduledJobService` job failures and startup initialization failures (via `App.InitializeAsync`) are forwarded to the collector so users can report them
- `DiagnosticsWindow` — modal window listing collected exceptions with timestamp, type, message, and expandable stack trace; per-entry "Report on GitHub" (opens pre-filled browser issue) and "Dismiss" buttons; "Clear All" footer action
- `DiagnosticsViewModel` / `UnhandledExceptionEntryViewModel` — VM pair driving the diagnostics UI
- Options › Diagnostics page — checkbox to enable collection, explanatory note, and "View Collected Exceptions…" button
- Help › Diagnostics menu item in `MainWindow.axaml`
- 8 unit tests in `UnhandledExceptionCollectorTests.cs`, 3 integration tests in `ScheduledJobServiceTests.cs`

---

### VSCode-Style Activity Bar ✅ Implemented
> Implemented in PR (commit `05f9a69`) — `src/Arbor.HttpClient.Desktop/Features/Main/MainWindow.axaml`, `src/Arbor.HttpClient.Desktop/Localization/Strings.resx`, `src/Arbor.HttpClient.Desktop/Localization/Strings.Designer.cs`

**What it means:** A narrow (48 px) activity bar docked to the left edge of the main window — inspired by VS Code — provides icon buttons for the main application features. Action buttons previously scattered in the top toolbar are consolidated here, freeing the toolbar to show only the title and environment selector.

**What shipped:**
- Activity bar `Border` (48 px wide, styled with `OptionsNavBackgroundBrush`) containing a two-zone `Grid`: main navigation icons (top) and auxiliary icons (bottom)
- Icons for: Collections (📁), Environments (🌐), Options (⚙), Cookies (🍪), Logs (📋), Import OpenAPI (📥), About (ℹ)
- Each icon `Button` has `ToolTip.Tip` and `AutomationProperties.Name` bound to localized strings for accessibility
- Old top-toolbar text buttons (Options, Environments, Import OpenAPI, Cookies, Logs) removed
- Top toolbar now contains only the app title and the environment `ComboBox`
- 7 new localized string keys added: `ActivityBarCollections`, `ActivityBarEnvironments`, `ActivityBarOptions`, `ActivityBarCookies`, `ActivityBarLogs`, `ActivityBarImportOpenApi`, `ActivityBarAbout`

**Polish items remaining:** Highlighted/active state on the activity bar icon corresponding to the currently visible left-dock panel; keyboard shortcut badge overlays; tooltip delay tuning.

---

### OpenAPI Import Structure ✅ Implemented
> Implemented in PR #124 (commit `e085960`) — `src/Arbor.HttpClient.Core/OpenApiImport/OpenApiImportService.cs`, `src/Arbor.HttpClient.Core/Collections/CollectionRequest.cs`, `src/Arbor.HttpClient.Desktop/Features/Collections/CollectionItemViewModel.cs`, `src/Arbor.HttpClient.Desktop/Features/Main/MainWindowViewModel.cs`, `src/Arbor.HttpClient.Storage.Sqlite/SqliteCollectionRepository.cs`

**What it means:** OpenAPI import now enriches each imported request with structural metadata drawn directly from the spec: query parameters are appended to the URL as `{{param}}` placeholders, header parameters and security auth headers are stored on the request, the first available example body is stored and populated on load, the matching `Content-Type` is detected and applied, and OpenAPI tags drive the tree-view grouping instead of path segment splitting.

**What shipped:**
- `CollectionRequest` gains four new optional fields: `Tag`, `Body`, `ContentType`, `Headers`
- `OpenApiImportService.Import` extracts: first operation tag → `Tag`; query parameters → appended to path as `?param={{param}}`; header parameters → `Headers` as `{{paramName}}`; HTTP Bearer / Basic / API Key security schemes → `Headers` with placeholder values; first JSON example (inline or named) → `Body`; content media type → `ContentType`
- `CollectionItemViewModel.GroupKey` now prefers `Tag` over the first path segment for tree grouping
- `LoadCollectionRequestCore` in `MainWindowViewModel` populates body, content type, and request headers from the stored `CollectionRequest` fields
- SQLite schema: `tag`, `body`, `content_type`, `headers` columns added via non-destructive `ALTER TABLE` migrations; headers serialised as JSON
- 13 new unit tests added to `OpenApiImportServiceTests`

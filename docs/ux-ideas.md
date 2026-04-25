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

### 1.5 Pre/post scripting
**What it means:** JavaScript or C# script snippets that run before a request is sent (to set variables, compute signatures) and after a response is received (to extract tokens, assert conditions). Postman uses JavaScript; Bruno uses JavaScript; some tools support Tengo or Lua.

**How to implement:**
- Embed [Jint](https://github.com/sebastienros/jint) (MIT, JavaScript interpreter) for a scripting sandbox with no native-code dependencies.
- Expose a `pm`-like API object: `pm.environment.set(key, value)`, `pm.response.json()`, etc.
- UI: a `ScriptEditor` tab using `AvaloniaEdit` with syntax highlighting.
- Sandboxing: run scripts in a separate `AppDomain` or with a `CancellationToken` timeout.

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

---

## Implemented

> Ideas move here once their primary UX behaviour is usable in the application. Each entry retains its original description and adds an implementation reference. Do not delete entries — this section is a historical record.

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

*Last updated: April 2026. Suggestions sourced from comparative review of Hoppscotch, Insomnia, Postman, Bruno, and browser DevTools.*

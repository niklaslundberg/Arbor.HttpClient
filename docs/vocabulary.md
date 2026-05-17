# Arbor.HttpClient Vocabulary

This document explains the main terms used in Arbor.HttpClient. It is meant as a shared language guide for contributors: what each concept means, how the application uses it, and where to look in the code.

Arbor.HttpClient is organized as vertical feature slices. The Core project defines UI-agnostic domain concepts, the Storage.Sqlite project persists those concepts, and the Desktop project presents and coordinates them in Avalonia.

## Project And Architecture Terms

| Term | What it is | How it is used |
|---|---|---|
| Core | The UI-independent business logic project. | Lives in `src/Arbor.HttpClient.Core` and defines request, collection, environment, variable, scripting, and protocol models/services. Code here should not depend on Avalonia. |
| Desktop | The Avalonia UI application. | Lives in `src/Arbor.HttpClient.Desktop` and hosts windows, feature view models, commands, options, dock layout, demo server startup, and UI-specific services. |
| Storage.Sqlite | The SQLite persistence implementation. | Lives in `src/Arbor.HttpClient.Storage.Sqlite` and implements repository interfaces from Core, including request history, collections, environments, and scheduled jobs. |
| Testing | The shared test doubles project. | Lives in `src/Arbor.HttpClient.Testing` and provides in-memory repositories and fakes so tests can avoid SQLite, network, or UI runtime dependencies. |
| Feature slice | A folder that groups related model, view model, view, converter, and service code by product feature. | Core uses top-level folders such as `HttpRequest`, `Collections`, and `Environments`. Desktop mirrors this under `Features/`. See `docs/architecture/clean-feature-separation.md`. |
| MainWindowViewModel | The current top-level Desktop composition/orchestration view model. | It wires features together and still owns some workflows. New feature behavior should move into feature-specific view models/services rather than growing this type. |
| Dockable | A UI panel or document hosted by the Dock framework. | Request, response, environments, collections, logs, options, cookies, and layout panels are arranged and persisted through `Features/Layout/DockFactory.cs` and dock snapshots. |
| Document | A central dock item used for primary work surfaces. | Request and response panels are modeled as document-style dockables. |
| Tool | A side or utility dock item. | Collections, environments, logs, cookies, options, diagnostics, and layout management behave as tool panels. |

## Request Authoring Terms

| Term | What it is | How it is used |
|---|---|---|
| Request composer | The UI where a user builds a request: method, URL, headers, query parameters, body, auth, options, and scripts. | Implemented mostly in `Features/HttpRequest/RequestEditorViewModel.cs` and `Features/HttpRequest/RequestView.axaml`. |
| Request tab | One open request editor instance. | `RequestTabViewModel` owns one `RequestEditorViewModel`; switching tabs swaps the active editor state. |
| Request editor | The view model that holds editable request state. | Builds `ResolvedHttpRequestDraft` values for sending and persists `RequestEditorSnapshot` values through `DraftPersistenceService`. |
| Request type | The protocol/request style selected in the composer. | `RequestType` distinguishes HTTP, GraphQL, WebSocket, SSE, and gRPC unary placeholder flows. |
| Resolved HTTP request draft | A Core record describing the concrete HTTP request that will be sent after editor values, variables, headers, and options have been resolved. | `ResolvedHttpRequestDraft` carries name, method, URL, body, headers, HTTP version, redirect behavior, TLS override, timeout, and certificate-validation override. It is send-time data, not the editable UI draft. |
| Header | A name/value pair sent with a request. | Core represents headers with `RequestHeader`; Desktop uses `RequestHeaderViewModel` for editable rows. Disabled or blank rows are ignored when sending. |
| Query parameter | A name/value pair appended to the request URL query string. | Desktop stores these in `RequestQueryParameterViewModel` rows and resolves variables before preview/send. |
| Placeholder row | The always-visible blank row at the bottom of header/query lists. | Lets users type directly without clicking Add. Once a key/name is typed, the row becomes active and a new placeholder appears. |
| Auth helper | Request-editor UI that creates authorization headers from auth mode fields. | Supports modes such as none, Bearer, Basic, API key, and OAuth2 access token in request editor snapshot persistence. |
| Request options | Per-request send controls. | Includes follow redirects, validate URL before send, ignore certificate validation, pretty-print request body, TLS version override, timeout, and HTTP version. |
| Request preview | The resolved outgoing request shown before send. | Uses active environment variables and request options so users can inspect the exact URL/body/headers. |
| Pretty-print request body | Formatting applied to known JSON/XML request bodies. | Can affect preview and send payload without changing source text, or format the source body once through the body-tab action. |

## Response Terms

| Term | What it is | How it is used |
|---|---|---|
| Response details | The Core representation of an HTTP response. | `HttpResponseDetails` carries status code, reason phrase, body text, headers, optional bytes, and elapsed time. |
| Response body | The payload returned by the server. | Shown in body/raw/preview tabs, copied to clipboard, saved to disk, or opened in an external editor. |
| Response headers | Metadata returned by the server. | Stored as name/value pairs on `HttpResponseDetails` and shown in a dedicated response tab. |
| Response raw | A view that combines response headers and body. | Useful for saving or inspecting the full wire-style response representation. |
| Web view response tab | A rendered HTML response preview. | Uses Avalonia `NativeWebView` for `text/html` responses and related preview flows. |
| Response actions | Commands that operate on the current response or history item. | `ResponseActionsViewModel` owns copy/save/open/copy-as-curl behavior and receives state via `IResponseActionsContext`. |
| HTTP diagnostics | Timing and version metadata captured for a request. | `HttpRequestDiagnostics` tracks requested/response HTTP versions, DNS/TLS notes, timing breakdowns, and total duration. |

## Variables And Environments

| Term | What it is | How it is used |
|---|---|---|
| Environment | A named set of variables used to resolve request templates. | `RequestEnvironment` includes name, variables, optional accent color, and warning-banner setting. The active environment is selected from the toolbar. |
| Environment variable | A user-defined key/value pair inside an environment. | `EnvironmentVariable` has name, value, enabled state, sensitivity flag, and optional UTC expiry. Disabled or expired variables resolve to empty string. |
| Variable token | A placeholder in request text using double braces, such as `{{baseUrl}}`. | `VariableResolver` replaces tokens in URL, body, headers, and query parameters before preview/send. Unknown tokens resolve to empty string. |
| System environment token | A token that reads from process environment variables, such as `{{env:HOME}}`. | The `env:` prefix is reserved and resolved through `ISystemEnvironmentVariableProvider`. It is separate from user-defined variables. |
| Computed token | A token computed by the app rather than stored, such as `{{c:TimeStampUtc}}`. | The `c:` prefix is reserved for timestamp values and optional date/time formatting. |
| Sensitive variable | A variable intended to hold secrets. | Detected by `SensitiveVariableDetector` or set manually. The UI masks values, and expired/sensitive metadata is persisted. Values are not yet encrypted at rest. |
| TTL / expiry | A UTC timestamp after which a variable should no longer resolve. | `EnvironmentVariable.ExpiresAtUtc` marks values as expired; expired variables are ignored during resolution. |
| Environment accent color | An optional color assigned to an environment. | Used as a color dot/badge and optional warning banner so users notice production-like environments. |
| Variable autocomplete | Completion UI for inserting variable tokens. | Desktop uses `VariableCompletionEngine`, `VariableAutoCompleteController`, and `VariableTextBox` to suggest app variables, system variables, and some plain-text values like header names. |
| Variable token colorizer | Syntax highlighting for variable tokens. | `VariableTokenColorizer` colors braces, prefixes, and names differently in AvaloniaEdit-based text inputs. |

## Collections And History

| Term | What it is | How it is used |
|---|---|---|
| Collection | A named group of reusable saved requests. | Core `Collection` stores ID, name, source path, base URL, requests, and optional default headers. SQLite persists it through `SqliteCollectionRepository`. |
| Collection request | A reusable saved request inside a collection. | `CollectionRequest` stores name, method, path, description, notes, tag, body, content type, and headers. Loading one fills the request editor. Use this term when users mean a saved request; avoid using saved request for history rows. |
| Collection base URL | A base URL applied to requests in a collection. | Allows imported or saved request paths to resolve against a shared host. It can include variables. |
| Collection default headers | Headers stored at collection level. | Applied to collection requests unless overridden or disabled at the request level. Edits in the Collections panel are persisted with a short debounce (no explicit save button) and then used when loading collection requests into the editor/preview. |
| OpenAPI import | Conversion from an OpenAPI document into collections and requests. | `OpenApiImportService` extracts paths, methods, tags, query/header parameters, security headers, content type, and sample bodies. |
| Tag grouping | Grouping collection requests by OpenAPI tag. | `CollectionItemViewModel.GroupKey` prefers imported tags before falling back to URL path segments. |
| Request history | Recently sent requests. | Saved via `IRequestHistoryRepository`/`SqliteRequestHistoryRepository`, then shown in the history panel and used for load/copy-as-curl flows. History entries are lossy compared with collection requests: they currently store name, method, URL, body, and timestamp, but not headers, auth settings, scripts, or per-request options. |
| Request history entry | The Core model for one row in request history. | `RequestHistoryEntry` stores name, method, URL, body, and creation timestamp for a sent request. Use this term for history rows; avoid saved request because that conflicts with `CollectionRequest`. |
| Copy as cURL | Formatting a request as a cURL command. | `CurlFormatter` turns current or historical request data into a command that can be pasted into a terminal. |

## Protocol Terms

| Term | What it is | How it is used |
|---|---|---|
| HTTP / REST request | A standard request/response interaction over HTTP. | Sent by `HttpRequestService` from a `ResolvedHttpRequestDraft`, with history storage and response rendering. |
| GraphQL draft | Inputs for a GraphQL query/mutation. | `GraphQlDraft` carries URL, query, variables JSON, operation name, and headers; `GraphQlService` sends it as a JSON POST. |
| WebSocket connection | A long-lived bidirectional connection. | `WebSocketService` connects, sends text frames, receives frames, and reports `WebSocketMessage` entries with sent/received direction. |
| SSE connection | A Server-Sent Events stream. | `SseService` consumes `text/event-stream` responses and emits parsed `SseEvent` values. |
| gRPC unary | Planned request style for single request/single response gRPC calls over HTTP/2. | Present as `RequestType.GrpcUnary` and UI placeholder; proto import/call execution remains future work. |
| Demo server | Embedded localhost server for testing the app without external dependencies. | `DemoServer` exposes `/status`, `/echo`, `/sse`, `/ws`, `/docs`, and `/docs.html` over configurable HTTP/HTTPS ports. |

## Scheduled Jobs And Web View

| Term | What it is | How it is used |
|---|---|---|
| Scheduled job | A request that runs repeatedly in the background. | `ScheduledJobConfig` stores method, URL, body, serialized headers, interval, auto-start, redirect override, and web-view setting. |
| ScheduledJobService | Desktop service that starts/stops scheduled jobs and records log output. | Owns timers/background execution and invokes the HTTP service on each tick. |
| Auto-start | A flag that allows a scheduled job to start when the app launches. | Controlled per job and by the global scheduled-jobs option. |
| Scheduled web view | A browser window attached to a scheduled GET job. | Opens `WebViewWindow` and refreshes as the scheduled request completes. |

## Scripting Terms

| Term | What it is | How it is used |
|---|---|---|
| Pre-request script | C# script executed before a request is sent. | Can mutate method, URL, headers, body, and environment values through `ScriptContext`. |
| Post-response script | C# script executed after a response is received. | Can read `ScriptResponse`, parse JSON through `BodyJson`, log messages, and record assertions. |
| Script context | The globals object injected into scripts as `ctx`. | `ScriptContext` exposes request fields, `Env`, optional `Response`, `Log`, and `Assert`. |
| Script response | Response data made available to post-response scripts. | `ScriptResponse` exposes status code, reason phrase, body, headers, and parsed JSON body when available. |
| Script result | The outcome of a script run. | `ScriptResult` carries success/failure, errors, and log messages for the Script panel. |
| Roslyn script runner | The Desktop implementation of Core `IScriptRunner`. | `RoslynScriptRunner` compiles and runs C# scripts with Microsoft.CodeAnalysis.CSharp.Scripting and caches by content hash. |
| Script log | Messages written by user scripts. | Populated through `ctx.Log(...)` and shown in the scripting UI. |
| Script assertion | A non-throwing validation inside a script. | `ctx.Assert(condition, message)` records failed assertions as script errors. |

## Persistence And Options

| Term | What it is | How it is used |
|---|---|---|
| Application options | The persisted settings root for the desktop app. | `ApplicationOptions` groups HTTP, appearance, scheduled job, layout, and diagnostics options. |
| HTTP options | Global defaults for request execution and demo server settings. | Includes HTTP version, TLS version, diagnostics, default content type, follow redirects, save path/pattern, timeout, and demo server ports. |
| Appearance options | Look-and-feel settings. | Stores theme/font-related preferences used by the UI. |
| Layout options | Saved dock layouts and window layout settings. | Persisted by `ApplicationOptionsStore` and `DockLayoutSnapshot`/`NamedDockLayout`. |
| Request editor snapshot | Auto-saved in-progress request editor state. | `RequestEditorSnapshot` serializes request fields, auth fields, options, notes, request type, headers, and save timestamp to the app data drafts folder. It is editable UI state, not the resolved request sent by `HttpRequestService`. |
| Named dock layout | A user-saved layout preset. | Lets users save, restore, and remove window/dock arrangements. |
| Dock layout snapshot | Serialized dock state. | Stores proportions, floating windows, and main window geometry so layout can survive restarts. |
| Response save filename pattern | A template for naming saved response files. | Formatted by `ResponseSaveFileNamePatternFormatter` using request/response metadata. |

## Diagnostics, Logging, And Errors

| Term | What it is | How it is used |
|---|---|---|
| Live log | In-memory stream of application events. | Serilog writes through `InMemorySink`; the log panel/window displays recent events by tab. |
| Log entry | A single application log item. | `LogEntry` stores timestamp, level, message, and tab. |
| Diagnostics options | User settings for diagnostics collection. | Controls whether unhandled exceptions are collected locally. |
| Unhandled exception entry | A persisted exception report candidate. | `UnhandledExceptionEntry` stores ID, timestamp, exception type, message, and stack trace. |
| Unhandled exception collector | Local service that records exceptions. | `UnhandledExceptionCollector` persists capped exception entries to `exceptions.json` for review/reporting. |

## UI Terms

| Term | What it is | How it is used |
|---|---|---|
| AvaloniaEdit TextEditor | Rich text editor control used instead of a plain TextBox where syntax coloring/completion is needed. | Used for URL/body/script/response surfaces. The project applies Fluent TextBox-like metrics for single-line inputs. |
| NativeWebView | Avalonia web view control. | Used for rendered HTML response preview and scheduled job browser windows. |
| Activity bar | The narrow left navigation strip. | Provides icon buttons for collections, environments, options, cookies, logs, import, and about. |

## Automation Terms

| Term | What it is | How it is used |
|---|---|---|
| Headless UI test | An Avalonia test that exercises the desktop UI without a visible OS window. | Lives in `Arbor.HttpClient.Desktop.E2E.Tests` and uses Avalonia headless infrastructure for UI behavior coverage. |
| Screenshot test | A headless UI test category used to generate documentation screenshots. | Run through `scripts/take-screenshots.sh` and outputs to `docs/screenshots/`. |
| VM system test | Real GUI automation in a disposable VM. | Documented in `docs/vm-ui-automation.md`; used for stronger UI/runtime validation than headless tests. |
| KVM/Alpine automation | Linux VM automation path using QEMU/KVM and Alpine. | `scripts/start-ui-automation-kvm-alpine.sh` publishes the app, drives it with X11 tools, and captures screenshots/report artifacts. |

## Planning Terms

| Term | What it is | How it is used |
|---|---|---|
| Profile | A proposed isolated app instance, similar to a local-first workspace. | Evaluated in `docs/profile-concept-evaluation.md`; not currently an implemented runtime feature. |
| Workspace | A comparative term from other HTTP clients for isolated collections/environments/history. | Used in planning docs to explain the profile concept, not as current UI terminology. |
| UX idea | A backlog item in `docs/ux-ideas.md`. | Every PR should review whether it implemented or added a UX idea, then update the backlog accordingly. |

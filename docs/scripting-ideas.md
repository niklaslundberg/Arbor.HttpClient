# Scripting Ideas for Arbor.HttpClient

## Overview

This document explores adding pre/post-request scripting to Arbor.HttpClient — a capability that lets users inject custom logic before a request is sent (to set variables, compute signatures, or modify the request) and after a response is received (to extract tokens, assert conditions, or chain to the next request).

The ideas below are captured from reviewing:
- How Postman, Bruno, HTTPie Desktop, and Insomnia handle scripting today
- Rick Strahl's April 2026 blog series *Revisiting C# Scripting with the Westwind.Scripting / Templating Library* (Parts 1 & 2), which motivates a C#-native scripting approach over embedded JavaScript
- The C# scripting landscape: Roslyn `CSharpScript`, `Westwind.Scripting`, `CS-Script`, and the .NET 10 native file-based scripting feature

This is an **ideas and planning document**. No implementation is described in code. The goal is to evaluate options clearly so that a future implementation PR can make an informed choice.

---

## 1. How Other HTTP Clients Handle Scripting

### 1.1 Postman

| Aspect | Detail |
|--------|--------|
| Language | JavaScript (ES6 subset, via the Sandbox engine — formerly ChakraCore, now a hardened JS context) |
| Pre-request hook | Full JS file, access to `pm.request`, `pm.environment`, `pm.globals`, `pm.collectionVariables` |
| Post-response hook | Access to `pm.response`, `pm.test()` for assertions, `pm.environment.set()` to extract values |
| Sandboxing | Postman Sandbox — isolated JS runtime. No `require()` unless explicitly allowed; no file system access |
| API familiarity | Very widely known; the `pm.*` object is a de-facto standard in API testing |
| Weakness | Tightly coupled to the Postman cloud. JS is a foreign language for .NET developers; debugging is limited |

### 1.2 Bruno

| Aspect | Detail |
|--------|--------|
| Language | JavaScript (Node.js-compatible subset, run via a bundled Node.js runtime or Bun) |
| Pre-request hook | `pre-request` block in each `.bru` file |
| Post-response hook | `tests` block in each `.bru` file |
| Sandboxing | Moderate — access to `bru.setVar`, `bru.getVar`, `res`, `req` objects; no direct file system by default |
| Strength | Open source, file-per-request collections that live in Git. Growing community |
| Weakness | Bundling Node.js makes the installer large; JS is still a foreign runtime for .NET apps |

### 1.3 Insomnia

| Aspect | Detail |
|--------|--------|
| Language | JavaScript (nunjucks for templating; full JS for plugins) |
| Pre-request hook | Template tags for dynamic values; plugin hooks for advanced flows |
| Post-response hook | Plugin system written in JavaScript |
| Sandboxing | Electron / Node.js plugin context — moderate sandboxing |
| Weakness | Less focused on scripting than Postman; heavy Electron app |

### 1.4 HTTPie Desktop

| Aspect | Detail |
|--------|--------|
| Language | Python (CLI plugin system) |
| Pre-request hook | `on_request_prepare(request, **kwargs)` method on a plugin class |
| Post-response hook | `on_response(response, **kwargs)` method |
| Strength | Python is well-suited to HTTP scripting; pip ecosystem for crypto, JWT, OAuth helpers |
| Weakness | Python runtime dependency; not natural for .NET audiences |

---

## 2. C# Scripting Options

Since Arbor.HttpClient is a C#/.NET application, using C# as the scripting language is the most natural choice — it removes the cognitive overhead of switching languages, gives full access to the .NET BCL, and keeps the toolchain consistent. Below are the main approaches.

### 2.1 Roslyn `CSharpScript` (Microsoft.CodeAnalysis.CSharp.Scripting)

**What it is:** The official Roslyn scripting API from Microsoft. Evaluates and executes C# code strings at runtime inside the host process.

**How it would work:**
```csharp
// Host-side (simplified)
var globals = new ScriptContext { Request = request, Environment = envVars };
await CSharpScript.RunAsync(userScriptCode, ScriptOptions.Default, globals);
```

| Property | Detail |
|----------|--------|
| Language | Full C# (including async/await, LINQ, all BCL types) |
| Globals injection | The `globals` parameter makes host objects available to the script by name |
| Error handling | Compilation diagnostics returned as structured `Diagnostic` objects |
| NuGet size | ~8–20 MB for the scripting package + transitive Roslyn deps |
| License | MIT (part of dotnet/roslyn — Apache-2.0 with MIT dual) |
| Maturity | Very mature — ships with the .NET SDK; used in Roslyn Analyzers, scripting REPLs, `csi.exe`, LINQPad |

**Pros:**
- Full C# language — no learning curve for C# developers
- Compile-time error reporting, strong types, IntelliSense possible
- Native async/await support
- Access to any loaded assembly or BCL type
- Actively maintained by Microsoft, long-term stable

**Cons:**
- Roslyn assemblies add ~15–30 MB to the deployment (already present if the app ships with .NET SDK, but not in a trimmed/published app)
- Cold-start compilation of the first script is noticeable (200–800 ms); caching compiled scripts mitigates this
- No built-in sandboxing — a malicious or buggy script can access the file system, network, or process environment unless extra restrictions are applied
- Running scripts in a `CancellationToken`-bounded task can interrupt infinite loops, but does not prevent memory exhaustion without additional guards

**Tradeoffs:**
- Choosing Roslyn means trusting the user (or restricting the API surface they are given through the globals). Suitable for a desktop tool where the user is the same person running the app.
- Compilation overhead is a one-time (or cached) cost per script version.

**Established?** Yes — the definitive C# scripting API. Used in production by LINQPad, scriptcs, OmniSharp, and many others.

---

### 2.2 Westwind.Scripting (wrapper around Roslyn)

**What it is:** A thin, ergonomic wrapper around `CSharpScript` by Rick Strahl (West Wind Technologies). Simplifies globals, references, and templating, at the cost of one extra NuGet dependency.

**How it would work:**
```csharp
var exec = new CSharpScriptExecution();
exec.AddDefaultReferencesAndNamespaces();

var globals = new { Request = request, Env = envVars };
exec.ScriptGlobalsType = globals.GetType();
var result = exec.Execute(userScriptCode, globals);
```

| Property | Detail |
|----------|--------|
| Language | Full C# (same as Roslyn, since it wraps it) |
| Templating | Supports Razor-like `@{ ... }` and `@(expression)` in template strings |
| License | MIT |
| NuGet package | Small shim (~50 KB); still pulls in full Roslyn transitively |
| Maturity | Actively maintained by a well-known .NET blogger; used in production tools |

**Pros:**
- Simpler API than raw Roslyn: fewer lines of boilerplate to get scripts executing
- Built-in template engine useful for generating dynamic request bodies
- Friendly error messages and diagnostics
- Same full C# power as raw Roslyn
- MIT license — compatible with this project

**Cons:**
- Adds an extra dependency (though small); Roslyn is still pulled in as a transitive dep
- Less widely known than raw `CSharpScript` — onboarding contributors requires familiarity with the wrapper
- Wrapper abstractions may lag behind Roslyn features occasionally (e.g., new C# language versions)

**Tradeoffs:**
- The convenience API is most valuable if request-body templating (not just script execution) is also desired. If only hook execution is needed, raw `CSharpScript` is nearly as ergonomic.

**Established?** Moderately — widely read blog series, actively used, but not a major open-source project with hundreds of contributors.

---

### 2.3 CS-Script

**What it is:** An alternative C# scripting engine (not based on Roslyn; uses direct `csc.exe`/`mcs` or Roslyn internally depending on version). Supports `.cs` file execution, NuGet references in scripts, and has a hosted API.

| Property | Detail |
|----------|--------|
| Language | Full C# |
| Runtime | Can use Roslyn or native compiler; supports script caching to disk |
| License | MIT |
| NuGet size | ~5–10 MB (modern .NET Core variant) |
| Maturity | Long-lived project (15+ years); less active community than Roslyn scripting |

**Pros:**
- Supports `//css_nuget` directive in scripts for referencing packages inline
- Can cache compiled scripts to avoid repeated compilation overhead
- Supports loading `.cs` file scripts directly, not just strings

**Cons:**
- Less popular in the modern .NET ecosystem than Roslyn scripting
- Documentation is sparser; fewer examples for embedded usage
- An extra abstraction layer over what Roslyn already provides natively
- Some features depend on external compiler tooling being available

**Established?** Yes for its niche, but Roslyn has largely superseded it in the .NET 5+ era.

---

### 2.4 .NET 10 Native File-Based C# Scripting (`dotnet run file.cs`)

**What it is:** .NET 10 introduces the ability to run a single `.cs` file directly via `dotnet run myscript.cs` without a project file. This is a first-class SDK feature, not a third-party library.

| Property | Detail |
|----------|--------|
| Language | Full C# (top-level statements) |
| Shebang | Unix `#!/usr/bin/env dotnet-script` supported |
| NuGet refs | Planned support for `#r "nuget:PackageName/Version"` directives |
| Runtime | Same as a normal .NET app — no extra runtime, no interpretation overhead |
| Maturity | New in .NET 10 (released November 2025); the file-based scripting feature shipped as stable in the .NET 10 SDK |

**Pros:**
- No extra NuGet dependencies — pure .NET 10 SDK feature
- AOT-compilation friendly: scripts can be compiled ahead of time
- No sandboxing complexity — the script IS a .NET app
- Best startup performance (pre-compiled model possible)
- Idiomatic .NET — familiar to any C# developer

**Cons:**
- Requires .NET 10 SDK on the machine (Arbor.HttpClient already targets .NET 10, so this is not a new requirement)
- Designed for **external scripts** run as child processes, not for **in-process hooks** attached to a request pipeline — integrating into the host app requires an out-of-process model with IPC (pipes, sockets, stdin/stdout JSON protocol)
- Out-of-process model adds latency per script invocation (acceptable for pre/post hooks but non-trivial)
- Security boundary: scripts run as separate processes, which provides true OS-level isolation but also makes accessing the host app's in-memory state harder

**Tradeoffs:**
- Best fit for a "script runner" model where the user writes a `.cs` file that receives a serialized `ScriptRequest` via stdin and writes back a `ScriptResponse` via stdout. The host app spawns the process per invocation (or keeps it alive with a handshake). This is similar to how some IDEs run language servers.

**Established?** The file-based scripting feature is new (.NET 10). The out-of-process approach is established in IDE/LSP tooling.

---

### 2.5 Jint (Embedded JavaScript Interpreter)

**What it is:** A pure-.NET JavaScript interpreter. Allows running JavaScript code strings inside the host process, with a controlled API surface.

| Property | Detail |
|----------|--------|
| Language | JavaScript (ECMAScript 2016+ subset) |
| License | BSD-2-Clause |
| NuGet size | ~3–5 MB |
| Maturity | Active, widely used in .NET scripting scenarios |

**Pros:**
- Low deployment footprint
- Sandboxed by default — only exposes what you explicitly pass in
- Familiar to developers who know Postman (`pm.*` pattern is easy to replicate)
- No compiler toolchain required — scripts are interpreted at runtime
- Works on any .NET 6+ target (no .NET 10 requirement)

**Cons:**
- **JavaScript is a different language** from the host application — a burden for C#-only developers
- Not a true security boundary; Jint runs in-process and can be exploited if the exposed API is careless
- Performance: interpreter overhead is significant for complex scripts; much slower than compiled C#
- Limited access to .NET BCL types unless explicitly exposed
- ECMAScript compliance gaps — some modern JS features may be unsupported

**Tradeoffs:**
- Choosing Jint replicates the Postman/Bruno user experience for users who know JavaScript. It is the wrong choice if the target audience is exclusively .NET/C# developers.

**Established?** Yes — widely used in CMS platforms, automation tools, and embedded scripting scenarios.

---

### 2.6 Lua (via NLua or MoonSharp)

**What it is:** Lua is a lightweight embeddable scripting language. Two .NET bindings exist: NLua (binds to the native Lua C runtime) and MoonSharp (pure .NET Lua interpreter).

| Property | Detail |
|----------|--------|
| Language | Lua |
| License | NLua: MIT; MoonSharp: BSD-2-Clause |
| NuGet size | MoonSharp ~1 MB; NLua adds native Lua DLL |
| Maturity | Lua is very mature (30+ years); .NET bindings are well-maintained |

**Pros:**
- Very small runtime footprint (MoonSharp is pure .NET)
- Excellent sandboxing — Lua's runtime is designed for embedding safely
- Simple syntax; easy to sandbox dangerous operations

**Cons:**
- Lua is largely unknown to C# developers — high learning curve for users
- Niche in the HTTP client space (Bruno formerly explored Lua but adopted JavaScript)
- Limited BCL access without explicit bridging

**Established?** Lua is very established in gaming (Roblox, World of Warcraft addons) and embedded systems. Its use in desktop HTTP clients is minimal.

---

## 3. Summary Comparison Table

| Option | Language | Deployment Size | Sandboxing | C# Dev Familiarity | Maturity | In-Process? |
|--------|----------|-----------------|------------|-------------------|----------|-------------|
| Roslyn CSharpScript | C# | +15–30 MB | None built-in | ★★★★★ | ★★★★★ | Yes |
| Westwind.Scripting | C# | +15–30 MB (Roslyn) | None built-in | ★★★★☆ | ★★★☆☆ | Yes |
| CS-Script | C# | +5–10 MB | None built-in | ★★★★☆ | ★★★☆☆ | Yes |
| .NET 10 file scripts | C# | None extra | OS process boundary | ★★★★★ | ★★☆☆☆ (new) | No (out-of-process) |
| Jint (JavaScript) | JavaScript | +3–5 MB | Configurable | ★★☆☆☆ | ★★★★☆ | Yes |
| Lua (MoonSharp) | Lua | +1–2 MB | Very good | ★☆☆☆☆ | ★★★☆☆ | Yes |

---

## 4. Integration Model

Regardless of the scripting engine chosen, the integration into Arbor.HttpClient would follow the same lifecycle:

```
┌───────────────────────────────────────────────────────────┐
│ User presses Send                                          │
│                                                            │
│   1. Load pre-request script (from ScriptEditor tab)       │
│   2. Execute pre-request script                            │
│      • Input:  ScriptContext { Method, Url, Headers,       │
│                Body, EnvVars }                              │
│      • Output: optionally mutated ScriptContext             │
│   3. Send HTTP request (using mutated context)             │
│   4. Receive response                                      │
│   5. Execute post-response script                          │
│      • Input:  ScriptContext + ScriptResponse              │
│               { StatusCode, Headers, Body, BodyJson }      │
│      • Output: optionally mutated EnvVars, assertions      │
│   6. Render response                                       │
└───────────────────────────────────────────────────────────┘
```

### 4.1 Proposed Host API (C#-native approach)

A minimal host API surface that scripts interact with:

```csharp
// Available in script as global object named "ctx"
public class ScriptContext
{
    public string Method { get; set; }
    public string Url { get; set; }
    public Dictionary<string, string> Headers { get; }
    public string? Body { get; set; }
    public IDictionary<string, string> Env { get; }        // read/write
    public ScriptResponse? Response { get; }               // null in pre-request
    public void Log(string message);
    public void Assert(bool condition, string message);
}
```

### 4.2 UI

- Add a **Script** tab next to Body / Headers in the request editor.
- Use `AvaloniaEdit` with syntax highlighting (C# or JavaScript depending on engine choice).
- Show compilation/runtime errors in a dismissible panel below the editor.
- Add a **Script Log** sub-panel within the response area for messages written with `ctx.Log(...)`.

---

## 5. Pros and Cons Summary per Approach for Arbor.HttpClient

### Option A — Roslyn `CSharpScript` (in-process)

**Pros:**
- Language the developer already knows
- Full .NET BCL access (System.Text.Json, Regex, DateTime, etc.)
- Good IDE support possible (AvaloniaEdit + Roslyn completion)
- Compile-time error reporting before running
- No runtime language mismatch

**Cons:**
- ~20 MB increase in published app size
- Cold-start compilation latency (~300–700 ms for first run; cached runs are fast)
- No sandboxing — script can call `File.Delete` or `Environment.Exit`
- For a desktop tool used by its own developer, the sandboxing concern is largely theoretical

**Tradeoff:** Accepts larger app size and no sandboxing in exchange for first-class C# scripting. Correct for an audience of C#/.NET developers.

---

### Option B — Westwind.Scripting (in-process, wraps Roslyn)

**Pros:**
- Simpler boilerplate than raw Roslyn
- Includes a template engine — useful if dynamic request bodies are also desired
- Same C# power as Roslyn

**Cons:**
- Extra dependency with a smaller community than the Roslyn core
- Templating adds scope beyond pure scripting; could be added incrementally
- Marginal benefit over raw Roslyn for the hook-execution use case

**Tradeoff:** Best when template generation (e.g., `Hello @(ctx.Env["name"])!`) is needed alongside imperative scripting. Adds friction for contributors unfamiliar with the Westwind library.

---

### Option C — .NET 10 out-of-process file scripts

**Pros:**
- True OS process isolation — script bugs cannot crash the host app
- No Roslyn assemblies shipped with the host
- Scripts are plain `.cs` files — version-controlled naturally

**Cons:**
- Latency: spawning a process per invocation (or maintaining a long-lived process) adds complexity and 50–200 ms overhead
- Requires an IPC protocol (stdin/stdout JSON, named pipes) to exchange request/response data
- .NET 10 file-based scripting is very new; may have rough edges
- Significantly more implementation complexity

**Tradeoff:** Correct choice if strong security isolation is a goal. Overkill for a developer desktop tool.

---

### Option D — Jint (JavaScript, in-process)

**Pros:**
- Familiar pattern for users coming from Postman
- Sandboxed by default
- Smaller footprint than Roslyn

**Cons:**
- JavaScript is a different language from the rest of the app
- Users must context-switch; error messages reference JS stack frames
- The `pm.*` API surface must be re-implemented from scratch
- Performance is poor for complex scripts

**Tradeoff:** Correct choice if migration from Postman/Bruno is a key use case and the user base knows JavaScript.

---

## 6. Preferred Solution and Rationale

**Recommended: Roslyn `CSharpScript` (Option A) — in-process, with a well-defined `ScriptContext` globals object.**

### Reasons

1. **Language consistency**: Arbor.HttpClient is a C# application. Its users are .NET developers. Asking them to write C# scripts to automate their HTTP workflows requires zero language learning.

2. **Full ecosystem access**: Scripts can use `System.Text.Json`, LINQ, `Regex`, `DateTimeOffset`, `System.Security.Cryptography` for HMAC signatures — all without installing anything extra.

3. **Compile-time feedback**: Roslyn returns structured diagnostics before the script runs. The ScriptEditor tab can show squiggles and error descriptions inline.

4. **Established and stable**: `Microsoft.CodeAnalysis.CSharp.Scripting` is maintained by Microsoft, ships with the .NET SDK, and is used in LINQPad, OmniSharp, and Roslyn Analyzers. It will not be abandoned.

5. **Alignment with the .NET 10 direction**: .NET 10's native file-based scripting also uses Roslyn under the hood. Adopting Roslyn scripting now aligns with the platform's direction.

6. **Sandboxing is acceptable for this tool**: Arbor.HttpClient is a developer desktop tool. The person writing the script is the same person running the app. Postman itself does not provide OS-level isolation. The practical sandboxing risk is low.

7. **Deployment size is acceptable**: Publishing a .NET app with Roslyn adds ~15–20 MB. Arbor.HttpClient already targets .NET 10 (which includes Roslyn in the SDK). For a self-contained publish, this is a one-time, acceptable cost.

### Why not Westwind.Scripting?

Westwind.Scripting is a good library, but for Arbor.HttpClient the primary need is **hook execution** (pre/post request), not string templating. The convenience API is not valuable enough to justify an extra dependency with a smaller community. If templating of request bodies becomes a future requirement, Westwind.Scripting can be evaluated at that point.

### Why not Jint?

Arbor.HttpClient's audience is .NET developers, not JavaScript developers. Switching scripting languages at the tool boundary introduces unnecessary friction. Jint would be the right choice only if the primary user story were "migrate my Postman collections to Arbor.HttpClient."

### Why not .NET 10 out-of-process scripts?

The implementation complexity (IPC protocol, process lifecycle, serialization contract) is disproportionate to the benefit for a desktop tool. The sandboxing benefit is not needed here. This approach should be reconsidered only if scripting is later exposed to untrusted or third-party script sources.

---

## 7. Implementation Sketch (not a specification)

A future implementation PR would roughly follow this order:

1. **Define `IScriptContext` and `ScriptContext`** in `Arbor.HttpClient.Core` — the globals object scripts receive. No Roslyn dependency here.
2. **Add `IScriptRunner` interface** in `Arbor.HttpClient.Core` with `RunPreRequestAsync(string script, ScriptContext ctx, CancellationToken ct)` and `RunPostResponseAsync(...)`.
3. **Implement `RoslynScriptRunner`** in a new project `Arbor.HttpClient.Scripting` (or `Arbor.HttpClient.Desktop` if kept small). Reference `Microsoft.CodeAnalysis.CSharp.Scripting`. Cache compiled scripts keyed by script hash to avoid re-compilation.
4. **Add `ScriptViewModel`** in `Arbor.HttpClient.Desktop`, containing the script text, an error list, and a log output collection.
5. **Add a Script tab** to the request editor using `AvaloniaEdit` with the C# syntax highlighting definition.
6. **Wire `ScriptViewModel` into the request pipeline** — call `IScriptRunner` in `MainWindowViewModel.SendRequestAsync` (or the future `RequestEditorViewModel`) before and after the HTTP call.
7. **Write unit tests** for `RoslynScriptRunner` covering: compile errors, runtime exceptions, successful variable mutation, timeout/cancellation.

**Estimated scope:** XL (multi-week) as already noted in `docs/ux-ideas.md` under idea 1.5.

---

## 8. Relationship to Existing UX Ideas

Idea [1.5 Pre/post scripting](ux-ideas.md#15-prepost-scripting) in `docs/ux-ideas.md` already lists this area and proposes Jint. This document supersedes the Jint suggestion with a C#-native (Roslyn) recommendation. If implemented, idea 1.5 should be updated in `docs/ux-ideas.md` to reference this document and the chosen approach.

---

## References

- [Rick Strahl — Westwind.Scripting GitHub](https://github.com/RickStrahl/Westwind.Scripting)
- [Microsoft.CodeAnalysis.CSharp.Scripting on NuGet](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp.Scripting)
- [Jint — JavaScript Interpreter for .NET](https://github.com/sebastienros/jint)
- [Bruno Scripting Docs](https://docs.usebruno.com/scripting)
- [Postman — Writing Pre-request Scripts](https://learning.postman.com/docs/writing-scripts/pre-request-scripts/)
- [.NET 10 Single File C# Scripting — GitHub Proposal](https://github.com/dotnet/runtime/issues/91728)
- [UX ideas — idea 1.5](ux-ideas.md#15-prepost-scripting)

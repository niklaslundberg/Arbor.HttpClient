# C# Best Practices Merge Report

**Source:** [`PlagueHO/github-copilot-assets-library` — `csharp-best-practices.instructions.md`](https://github.com/PlagueHO/github-copilot-assets-library/blob/main/instructions/csharp-best-practices.instructions.md)  
**Compared against:**
- `.github/instructions/csharp.instructions.md` (scoped instruction file)
- `.github/copilot-instructions.md` §§ 7, 9, 10, 11 (canonical guidelines)

**Date:** 2026-04-25

---

## Summary

The external file is a broad general-purpose C# baseline. This repository's instructions are already a superset on most areas that matter for this codebase. The table below categorises every section from the external file as:

| Status | Meaning |
|--------|---------|
| ✅ Already covered | Repo rule exists and is equivalent or stricter |
| ➕ Gap — additive | Useful rule not yet in the repo; no conflict |
| ⚠️ Contradiction | External rule conflicts with an existing repo rule |
| ⏩ Not applicable | Not relevant to this codebase |

---

## Section-by-Section Comparison

### Code Style and Formatting

#### Namespace Declarations
- Use file-scoped namespace declarations for C# 10+ (`namespace MyNamespace;`)
- Keep namespace declarations consistent

**Status: ➕ Gap — additive**  
The repo instructions do not prescribe a namespace style. File-scoped namespaces are idiomatic for modern C# and the codebase targets a recent SDK, so this is safe to adopt. Recommended merge: add a `[RECOMMENDED]` item to `csharp.instructions.md`.

#### Access Modifiers
- Explicitly declare access modifiers
- `readonly` for constructor-only fields
- `const` for compile-time constants
- Least-privilege access

**Status: ✅ Already covered**  
`csharp.instructions.md` already has `[REQUIRED]` for `readonly`. Explicit access modifiers and `const` are covered implicitly by the Roslyn analyser policy (§ 7 of `copilot-instructions.md`). No merge needed.

#### Type Declarations (`var`)
- Use `var` when the type is obvious from the right side

**Status: ➕ Gap — additive**  
Not mentioned in repo instructions. This is a common, low-risk convention aligned with the Microsoft C# style guide. Recommended merge: `[RECOMMENDED]` bullet in `csharp.instructions.md`.

#### Method and Property Formatting
- Expression-bodied members for simple one-liners
- Opening braces on new lines
- Auto-implemented properties

**Status: ➕ Gap — additive**  
Not mentioned explicitly, though EditorConfig likely enforces brace style. These are additive, low-risk formatting guidelines. Recommended merge as `[RECOMMENDED]` notes, or rely on EditorConfig rather than prose.

---

### Modern C# Patterns

#### Null Safety
- Use null-conditional operators (`?.`, `??`, `??=`)
- Validate parameters with null checks
- `string.IsNullOrEmpty()` / `string.IsNullOrWhiteSpace()`
- Consider nullable reference types (C# 8+)

**Status: ✅ Already covered (partially) + ➕ Gap (nullable reference types, is { })**  
Repo already mandates `is null` / `is not null` for null checks (`csharp.instructions.md`). Per owner feedback, the preferred non-null check pattern is `is { }` instead of `is not null` — this is stricter and more idiomatic in modern C#. Null-conditional operators are complementary (for chaining/coalescing, not for guard checks) — no conflict. Nullable reference types should be enabled globally (e.g., via `<Nullable>enable</Nullable>` in `Directory.Build.props`), not via per-file `#nullable enable` directives. **Merged:** `is { }` preference added to `csharp.instructions.md`; global nullable guidance added.

#### String Handling
- Prefer string interpolation `$""`
- `StringBuilder` for heavy manipulation
- `string.Equals()` with `StringComparison`
- Verbatim strings `@""`

**Status: ➕ Gap — additive**  
Not addressed by repo instructions. These are all safe, standard guidelines. Recommended merge: add as `[RECOMMENDED]` group to `csharp.instructions.md`.

#### Exception Handling
- Use specific exception types
- `when` clause filtering
- Meaningful messages
- Fail-fast

**Status: ✅ Already covered**  
`copilot-instructions.md` § 11 and `csharp.instructions.md` already mandate specific types, `throw;` over `throw ex;`, and domain-specific types at subsystem boundaries. No merge needed.

---

### Performance Considerations

#### Memory Efficiency / `IDisposable`
- Dispose `IDisposable` with `using` statements
- Avoid boxing/unboxing
- Consider object pooling

**Status: ✅ Already covered (using) + ➕ Gap (boxing/pooling)**  
`csharp.instructions.md` already requires `using` for `IDisposable`. Boxing and pooling guidance is not mentioned but is additive. Profiling guidance in § 7 of `copilot-instructions.md` partially covers object-pooling concerns.

#### Collections
- Use appropriate types (`List<T>`, `HashSet<T>`, `Dictionary<TKey, TValue>`)
- `IEnumerable<T>` for parameters when only enumeration is needed
- `IReadOnlyList<T>` / `IReadOnlyCollection<T>` for immutable exposure
- Initialize with known capacity

**Status: ➕ Gap — additive**  
Not mentioned in repo instructions. Good general guidance; all four points are safe to add as `[RECOMMENDED]`.

#### LINQ Usage
- Use method syntax for simple operations
- Beware deferred execution
- `ToList()` / `ToArray()` when enumerating multiple times

**Status: ➕ Gap — additive (partial overlap)**  
`csharp.instructions.md` already covers the related `.Where()` preference over `if` inside `foreach`. The deferred-execution and materialisation guidance is additive. Recommended merge as `[RECOMMENDED]`.

---

### Async/Await Best Practices

#### Async Methods
- `async Task` for void-returning async methods
- Avoid `async void` except for event handlers
- **`ConfigureAwait(false)` in library code**

**Status: ✅ Already covered (async Task) + ⏩ Skip (ConfigureAwait)**  
The repo mandates `CancellationToken` passing but does not mention `ConfigureAwait(false)`. Per owner decision, this item is **skipped** — the codebase includes Avalonia Desktop (UI context) where `ConfigureAwait(false)` can cause issues, so blanket adoption is not appropriate here.

#### Cancellation
- Accept `CancellationToken` in long-running async methods
- Pass through the call chain
- Handle `OperationCanceledException`

**Status: ✅ Already covered**  
`copilot-instructions.md` § 9 and `csharp.instructions.md` already mandate this with the added constraint of using the full name `cancellationToken`. No merge needed. The repo rule is stricter.

---

### Object-Oriented Principles

#### Encapsulation, SOLID
- Private fields, exposed via properties
- Composition over inheritance
- SOLID principles

**Status: ➕ Gap — additive**  
Not explicitly listed in repo instructions, though good design is expected. These are widely known principles; merging a summary SOLID reminder as `[RECOMMENDED]` would be worthwhile.

---

### XML Documentation
- XML doc comments for all public APIs (`<summary>`, `<param>`, `<returns>`, `<exception>`)
- `<example>` for complex methods
- Document thread safety

**Status: ➕ Gap — additive**  
Repo instructions do not mention XML documentation at all. Public API policy exists (§ 8 of `copilot-instructions.md`) but only covers breaking-change declaration, not documentation. Recommended merge: `[REQUIRED]` for public APIs in `Arbor.HttpClient.Core`, `[RECOMMENDED]` elsewhere.

---

### Testing Patterns

#### AAA Pattern — ⚠️ CONTRADICTION

External file recommends:
> Follow AAA pattern: Arrange, Act, Assert

Repo instructions (`csharp.instructions.md`, `[REQUIRED]`) explicitly state:
> Do not emit `// Arrange`, `// Act`, or `// Assert` comments in test methods.

**This is a direct contradiction.** The external file implies following (and presumably labelling) AAA structure; the repo rules forbid the comments. The repo's rule should take precedence: structure tests with blank lines and well-named variables rather than comment delimiters. **Do not adopt the AAA comment guidance from the external file.**

#### Test Naming
- Meaningful test names that describe the scenario

**Status: ✅ Already covered (stricter)**  
Repo mandates the `Method_Scenario_ExpectedResult` pattern. No merge needed.

#### Other Testing Guidance
- Modern frameworks (xUnit/NUnit/MSTest)
- Happy path and edge cases
- Mock dependencies

**Status: ➕ Gap — additive**  
Not mentioned explicitly in repo instructions. These are reasonable additions; recommend as `[RECOMMENDED]` guidelines.

---

### File Organization
- One public type per file
- File name = primary type name
- `using` statement order: System → third-party → project
- Remove unused `using` statements

**Status: ➕ Gap — additive**  
Not mentioned in repo instructions. All four points are standard Microsoft convention. EditorConfig and Roslyn analysers likely enforce many of these already, but documenting them is worthwhile as `[RECOMMENDED]`.

---

### Code Quality Rules
- Follow Microsoft's .NET coding conventions
- EditorConfig for consistent formatting
- Enable and address code analysis warnings
- Static analysis (SonarQube / Roslyn analysers)
- Code reviews

**Status: ✅ Already covered**  
§ 7 of `copilot-instructions.md` covers analyser warnings, Roslyn/CA rules, and EditorConfig. No merge needed.

---

### Error Handling
- Exceptions only for exceptional circumstances, not control flow
- Result pattern for expected failures

**Status: ✅ Already covered (stricter)**  
`copilot-instructions.md` § 11 already recommends "result/discriminated-union types at subsystem boundaries" — this is functionally equivalent to the Result pattern. No merge needed.

---

### Resource Management
- `IDisposable` for unmanaged resources
- `using` for cleanup
- Dispose pattern
- Finalizers only when necessary

**Status: ✅ Already covered**  
`csharp.instructions.md` already requires `using` for `IDisposable`. The full Dispose pattern and finalizer guidance is additive but uncommon for application code. Add as `[RECOMMENDED]`.

---

### Post-Generation Actions

External file mandates:
> - **ALWAYS trim trailing whitespace from all lines after any code changes**
> - Ensure consistent line endings (LF on Unix, CRLF on Windows)
> - Remove extra blank lines at the end of files
> - **Ensure proper indentation (4 spaces for C#, no tabs)**
> - Format code according to project standards

**Status: ➕ Gap — additive**  
Repo instructions do not mention trailing whitespace, line endings, or indentation size explicitly. These are all handled by `.editorconfig` in practice, but documenting the expectation is useful. The 4-space indentation is standard C# and should already match the EditorConfig. Recommended merge: a brief `[RECOMMENDED]` post-edit hygiene note in `csharp.instructions.md`.

---

### Security Considerations
- Validate all input parameters
- Secure string handling
- Proper auth/authz
- OWASP guidelines
- No hardcoded secrets
- HTTPS/TLS
- Error handling without exposing sensitive information

**Status: ✅ Already covered**  
`docs/security-review.md`, `copilot-instructions.md` § 15 (blocking items on TLS, secrets), and `csharp.instructions.md` (no sensitive data in logs) collectively cover these. No merge needed.

---

## Contradictions Summary

| External rule | Repo rule | Resolution |
|---------------|-----------|------------|
| Follow AAA pattern (implies `// Arrange`, `// Act`, `// Assert` labels) | `[REQUIRED]` No AAA comments — use blank lines and named variables instead | **Repo rule wins.** Do not adopt the comment-label convention from the external file. The structural AAA _approach_ (arrange inputs, perform action, assert result) is still encouraged; only the comment labels are banned. |
| Null-conditional operators (`?.`, `??`) for null checks | `[REQUIRED]` `is null` / `is not null` | **Repo rule extended.** Non-null checks updated to prefer `is { }` over `is not null`. |

No other direct contradictions were found. Several external rules are silent where the repo has stricter requirements (e.g. `cancellationToken` full-name mandate).

---

## Recommended Merge Actions

The following changes to `.github/instructions/csharp.instructions.md` (and optionally `.github/copilot-instructions.md`) would incorporate the non-contradicting, additive rules from the external file:

### High-value additions (recommend adopting)

1. ~~**`ConfigureAwait(false)` in library code**~~ — **SKIPPED** (not appropriate given Avalonia UI context in the same solution).
2. **File-scoped namespaces** — `[REQUIRED]` for new files using C# 10+ features. ✅ Merged.
3. **Nullable reference types** — must be enabled **globally** via `Directory.Build.props` (`<Nullable>enable</Nullable>`), not via per-file `#nullable enable` directives. ✅ Merged (global-only note added).
4. **XML documentation for public APIs** — `[REQUIRED]` in `Arbor.HttpClient.Core`, `[RECOMMENDED]` elsewhere. ✅ Merged.
5. **`var` for obvious local variables** — `[RECOMMENDED]`. ✅ Merged.

**Additional rule adopted from owner feedback:**
- Prefer `is { }` over `is not null` for non-null checks. ✅ Merged into `csharp.instructions.md`.

### Lower-value additions (consider adopting)

6. String handling: interpolation, `StringBuilder`, `string.Equals` with `StringComparison` — `[RECOMMENDED]`. ✅ Merged.
7. Collection guidelines: `IEnumerable<T>` for parameters, `IReadOnlyList<T>` for immutable exposure — `[RECOMMENDED]`. ✅ Merged.
8. LINQ: deferred execution awareness, materialise with `ToList()` when needed — `[RECOMMENDED]`. ✅ Merged.
9. SOLID principles summary — `[RECOMMENDED]`. ✅ Merged.
10. File organisation: one public type per file, `using` order, no unused `using` — `[RECOMMENDED]`. ✅ Merged.
11. Post-edit hygiene: trailing whitespace, blank lines at EOF — `[RECOMMENDED]`. ✅ Merged.
12. Full Dispose pattern guidance — `[RECOMMENDED]`. ✅ Merged.

### Do NOT adopt

- AAA comment labels (`// Arrange`, `// Act`, `// Assert`) — **directly contradicts** the `[REQUIRED]` repo rule.
- `ConfigureAwait(false)` blanket guidance — **skipped** by owner decision; not appropriate for a solution that mixes library and Avalonia Desktop projects.
- Per-file `#nullable enable` directives — **skipped**; nullable reference types must be enabled globally via `Directory.Build.props`.

---

## Not Applicable to This Repo

- Performance monitoring / health checks — partly covered by § 7 profiling requirement; full health-check infra is not part of this codebase.
- SonarQube — not in the current toolchain (Roslyn analysers are used instead).
- Code reviews / pair programming — process outside the scope of agent instructions.

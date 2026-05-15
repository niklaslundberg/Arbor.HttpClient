# Architecture Decision Format

Arbor.HttpClient does not currently keep a root `docs/adr/` directory. Prefer updating the existing architecture documentation under [docs/architecture/](../../../docs/architecture/) when a decision belongs to an ongoing architecture topic.

Create a formal ADR only when a decision is hard to reverse, surprising without context, and the result of a real tradeoff. If a formal ADR is warranted, create `docs/architecture/adr/` lazily.

## File Naming

Use sequential numbering:

```text
docs/architecture/adr/0001-short-slug.md
docs/architecture/adr/0002-short-slug.md
```

Scan the existing `docs/architecture/adr/` directory, if it exists, and increment the highest number.

## Minimal Template

```md
# {Short title of the decision}

{One to three sentences describing the context, what was decided, and why.}
```

## Optional Sections

Only include these when they add real value:

```md
## Status

Accepted | Proposed | Deprecated | Superseded by ADR-NNNN

## Considered Options

- Option A: why it was rejected or accepted.
- Option B: why it was rejected or accepted.

## Consequences

- Non-obvious downstream effect.
- Constraint future changes must respect.
```

## What Belongs In An ADR

Good candidates:

- Feature-boundary decisions that affect project structure.
- Persistence or storage choices that are costly to unwind.
- Public API shape decisions in `Arbor.HttpClient.Core`.
- Security posture or TLS/networking decisions that future maintainers might otherwise weaken.
- Workflow or packaging decisions that must stay mirrored across CI/release automation.

Poor candidates:

- Simple renames that are already captured by `docs/vocabulary.md`.
- Small implementation details covered by tests.
- Reversible UI copy or layout changes.
- Decisions already obvious from the code and existing docs.

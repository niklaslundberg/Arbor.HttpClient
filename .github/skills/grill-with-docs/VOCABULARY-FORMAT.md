# Vocabulary Format

The Arbor.HttpClient glossary lives at [docs/vocabulary.md](../../../docs/vocabulary.md). Keep it as the single canonical vocabulary document for this repo.

## Structure

Use the existing sectioned table format:

```md
## {Concept Group} Terms

| Term | What it is | How it is used |
|---|---|---|
| Canonical term | One concise definition of the concept. | Where it appears in the product/code, important aliases to avoid, and key boundaries with related concepts. |
```

## Rules

- Use the canonical term as the `Term` value.
- Keep `What it is` to one sentence where practical.
- Put implementation references and important distinctions in `How it is used`.
- Prefer existing product language over introducing new abstractions.
- Flag ambiguous or overloaded words directly in the relevant row.
- If a term replaces an older ambiguous name, mention the boundary rather than preserving the old name as a peer concept.
- Include only Arbor.HttpClient concepts, not generic programming terms.
- Keep related concepts near each other so boundaries are visible.

## Current High-Value Boundaries

The following distinctions are especially important in this repository:

- `CollectionRequest` is the reusable saved request inside a collection.
- `RequestHistoryEntry` is a lossy history row captured after send.
- `ResolvedHttpRequestDraft` is the send-ready HTTP request after editor values and variables are resolved.
- `RequestEditorSnapshot` is persisted editable UI state, not the request sent over the network.

When the user says "saved request", "request draft", "history", or "draft", ask which of these concepts they mean unless context makes it unambiguous.

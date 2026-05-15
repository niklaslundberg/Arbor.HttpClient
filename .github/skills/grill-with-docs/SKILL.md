---
name: grill-with-docs
description: "Use when: stress-testing an Arbor.HttpClient plan against the repo vocabulary, architecture docs, PR process, and actual code. Challenges fuzzy terminology, asks one precise question at a time, recommends answers, and updates docs/vocabulary.md or architecture decision notes when decisions crystallize."
---

# Grill With Docs

Use this skill when the user wants to pressure-test a design, feature plan, refactor, naming choice, architecture boundary, or product concept against Arbor.HttpClient's documented language and current implementation.

This is adapted for Arbor.HttpClient from the upstream `grill-with-docs` workflow. In this repo, the canonical glossary is [docs/vocabulary.md](../../../docs/vocabulary.md), not `CONTEXT.md`. Do not create `CONTEXT.md` for this workflow.

## What To Do

Interview the user rigorously until the plan is precise enough to implement or document. Walk the design tree one decision at a time. Ask one question at a time and wait for feedback before continuing, unless the question can be answered by reading the repository.

For each question:

1. State the ambiguity or decision plainly.
2. Check the relevant docs and code before asking if the answer might already exist.
3. Give your recommended answer, including the tradeoff behind it.
4. Call out any contradiction between the user's wording, [docs/vocabulary.md](../../../docs/vocabulary.md), and the implementation.
5. Update documentation inline when terminology or a meaningful decision is resolved.

## Required Repo Context

Before grilling a plan, read the canonical project instructions and the docs that shape the question. At minimum, use the repository's session-start requirements from [.github/copilot-instructions.md](../../copilot-instructions.md). For most grill sessions, also read:

- [docs/vocabulary.md](../../../docs/vocabulary.md) for canonical terms and known ambiguities.
- [docs/architecture/clean-feature-separation.md](../../../docs/architecture/clean-feature-separation.md) for feature-boundary guidance.
- [docs/review-checklist.md](../../../docs/review-checklist.md) and [docs/security-review.md](../../../docs/security-review.md) when the plan touches PR readiness, security, networking, persistence, CI, or dependencies.
- [docs/ux-ideas.md](../../../docs/ux-ideas.md) when the plan changes user workflows or introduces a new UX idea.

Then inspect the relevant source files. If the user's claim and the code disagree, treat that disagreement as the next question to resolve.

## Repository Documentation Model

Arbor.HttpClient already has a vocabulary document and architecture notes. Use them instead of introducing generic upstream files.

- Glossary and concept boundaries: update [docs/vocabulary.md](../../../docs/vocabulary.md). Follow [VOCABULARY-FORMAT.md](./VOCABULARY-FORMAT.md).
- Architecture and hard-to-reverse decisions: prefer updating an existing file under [docs/architecture/](../../../docs/architecture/) when it is the natural home. If a formal ADR is genuinely warranted and no existing architecture document fits, create `docs/architecture/adr/` lazily and follow [ADR-FORMAT.md](./ADR-FORMAT.md).
- UX backlog items: update [docs/ux-ideas.md](../../../docs/ux-ideas.md) using the repo's existing UX idea format.
- Security or review guidance: update [docs/security-review.md](../../../docs/security-review.md), [docs/review-checklist.md](../../../docs/review-checklist.md), or [.github/copilot-instructions.md](../../copilot-instructions.md) only when the plan resolves a reusable process rule.

## During The Session

### Challenge Against The Vocabulary

When the user uses a term that conflicts with [docs/vocabulary.md](../../../docs/vocabulary.md), surface it immediately.

Example: "The vocabulary defines a `CollectionRequest` as the reusable saved request, while `RequestHistoryEntry` is a lossy history row. You said saved request here. Which concept do you mean?"

### Sharpen Fuzzy Language

When the user uses overloaded words such as "request", "draft", "saved", "environment", "profile", "workspace", "history", or "collection", propose the canonical term from the vocabulary.

If no term exists yet, propose one and explain why it fits the codebase.

### Discuss Concrete Scenarios

Stress-test relationships with realistic Arbor.HttpClient scenarios. Prefer scenarios that expose boundaries:

- Loading a collection request into the editor versus loading a request history entry.
- Sending an HTTP request after variables and scripts mutate it.
- Persisting a request editor snapshot across restart.
- Importing OpenAPI requests into collections.
- Applying environment variables, sensitive values, and expiry.
- Running a scheduled job or GraphQL request through the same response/history surfaces.

### Cross-Reference With Code

When the plan states how something works, verify the relevant code. If the code disagrees, say so and make that the next decision.

Useful starting points include:

- Core request concepts: `src/Arbor.HttpClient.Core/HttpRequest/`
- Collections: `src/Arbor.HttpClient.Core/Collections/`
- Environments and variables: `src/Arbor.HttpClient.Core/Environments/`, `src/Arbor.HttpClient.Core/Variables/`
- Desktop request editor and response actions: `src/Arbor.HttpClient.Desktop/Features/HttpRequest/`
- Main orchestration: `src/Arbor.HttpClient.Desktop/Features/Main/MainWindowViewModel.cs`
- Layout/editor snapshots: `src/Arbor.HttpClient.Desktop/Features/Layout/`
- SQLite persistence: `src/Arbor.HttpClient.Storage.Sqlite/`

### Update Docs Inline

When terminology is resolved, update [docs/vocabulary.md](../../../docs/vocabulary.md) during the session. Do not batch glossary updates until the end. Use the existing table style and keep implementation details in the "How it is used" column.

When a decision is resolved, update the smallest appropriate documentation surface. Prefer existing docs over new files.

### Offer ADRs Sparingly

Only suggest a formal ADR when all three are true:

1. The decision is hard to reverse.
2. A future maintainer would be surprised without context.
3. The decision was a real tradeoff between plausible alternatives.

If any of these is missing, update the vocabulary or an existing architecture note instead.

## Output Style

During a grill session, keep the conversation interactive and focused. Ask one question at a time. Each question should include:

- **Question:** the decision or ambiguity.
- **Recommended answer:** what you think the repo should do.
- **Why:** the concrete doc/code evidence or tradeoff.
- **Doc impact:** what file would change if the user agrees.

Once the user agrees, make the doc change immediately and continue to the next unresolved question.

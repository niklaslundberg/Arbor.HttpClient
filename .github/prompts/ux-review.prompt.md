---
mode: ask
description: Review docs/ux-ideas.md and update the Implemented / Not Yet Implemented lists for this PR.
---

# UX Ideas Maintenance

> Full rules are in `.github/copilot-instructions.md` section 13. This prompt walks through the required update to `docs/ux-ideas.md` on every PR.

## Steps

1. Read `docs/ux-ideas.md` in full.
2. Read the diff for this PR and identify any ideas that were **fully or partially implemented**.
3. For each implemented idea, move its entry from the "Not Yet Implemented" section to the "Implemented" section using the format below.
4. If this PR introduces a new UX idea (discovered during implementation or review), add it to "Not Yet Implemented" with the standard description and scope estimate.
5. Confirm that no idea has been deleted — the "Implemented" section is a permanent historical record.

## Format for implemented entries

```markdown
### 1.1 Feature Name ✅ Implemented
> Implemented in PR #<number> (commit `<short-sha>`) — `src/path/to/Feature.cs`

**What it means:** <original description retained verbatim>

**How it was implemented:** <brief note on actual approach taken, referencing specific classes or files>

**Scope:** <original estimate> (actual: <S/M/L/XL>)
```

## What counts as "implemented"

An idea is implemented when its primary UX behaviour is usable in the application, even if polish items remain. Record any remaining polish gaps as sub-items rather than keeping the whole idea in "Not Yet Implemented".

## Output

After updating `docs/ux-ideas.md`, confirm with:

```
✅ UX ideas reviewed.
- Moved to Implemented: <list of idea numbers, or "none">
- Added to Not Yet Implemented: <list of new ideas, or "none">
```

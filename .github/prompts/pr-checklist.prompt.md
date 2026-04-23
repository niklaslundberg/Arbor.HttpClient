---
mode: ask
description: End-of-PR compliance checklist — verify every blocking principle before marking a PR ready for review.
---

# PR Compliance Checklist

> Full rules are in `.github/copilot-instructions.md` (section 15). This prompt produces the checklist that must be completed and pasted into every PR description.

Go through each item below and replace `[ ]` with `[x]` for satisfied, or `[~]` with a short justification for any item intentionally skipped. Items marked **[BLOCKING]** must never be skipped without an explicit documented reason.

```markdown
## PR Compliance Checklist

### Tests & Build
- [ ] **[BLOCKING]** All tests pass: `dotnet test Arbor.HttpClient.slnx`
- [ ] **[BLOCKING]** No compiler warnings introduced
- [ ] **[REQUIRED]** CodeQL / static analysis findings addressed (no new open findings)

### Security
- [ ] **[BLOCKING]** No secrets, tokens, or credentials committed
- [ ] **[BLOCKING]** No HTTP/TLS configuration downgrade
- [ ] **[REQUIRED]** No sensitive data logged (credentials, PII, raw request bodies)
- [ ] **[REQUIRED]** `persist-credentials: false` retained on all `actions/checkout` steps
- [ ] **[REQUIRED]** Vulnerability audit passes: `dotnet list Arbor.HttpClient.slnx package --vulnerable --include-transitive`

### Dependencies
- [ ] **[REQUIRED]** New NuGet packages verified for MIT-compatible license
- [ ] **[REQUIRED]** New packages declared in `Directory.Packages.props` (not inline in `.csproj`)
- [ ] **[REQUIRED]** New packages documented in `THIRD_PARTY_NOTICES.md`

### UI Changes (skip if no UI changes)
- [ ] **[REQUIRED]** E2E screenshot tests run; output committed to `docs/screenshots/`
- [ ] **[REQUIRED]** Screenshots embedded inline in the PR description (not just `/tmp/`)
- [ ] **[REQUIRED]** Accessibility contrast tests updated for any new color pairs (WCAG AA ≥ 4.5:1)
- [ ] **[REQUIRED]** Interactive elements keyboard-accessible

### UX Ideas (section 13)
- [ ] **[REQUIRED]** `docs/ux-ideas.md` reviewed; implemented ideas moved to the "Implemented" section with PR/commit references
- [ ] **[RECOMMENDED]** New UX ideas from this PR added to the "Not Yet Implemented" section

### Architecture (section in `docs/architecture/clean-feature-separation.md`)
- [ ] **[RECOMMENDED]** No new logic added to `MainWindowViewModel` that belongs in a feature VM
- [ ] **[RECOMMENDED]** New features have at least one focused unit test that does not require the full UI runtime

### Instruction Improvement Loop (section 14)
- [ ] **[RECOMMENDED]** Instruction Retrospective block written below
- [ ] **[RECOMMENDED]** Proposed instruction improvement applied to `.github/copilot-instructions.md` (or tracked as a GitHub issue)

### Final Self-Check
- [ ] **[BLOCKING]** Every changed line traces directly to the user's request (no unrelated edits)
- [ ] **[REQUIRED]** PR description explains *what* changed and *why*
```

---

## Instruction Retrospective

*(Fill in after the checklist above is complete.)*

- **What was unclear:** 
- **What caused rework:** 
- **Proposed instruction addition/change:** 

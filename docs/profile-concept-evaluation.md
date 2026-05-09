# Profile Concept Evaluation

## Goal

A **profile** should behave as an isolated app instance where these items are separated per profile:

- collections
- environments and variables
- request history / requests
- options and related persisted state

This is similar to what many tools call a **workspace**.

---

## Option A — Separate SQLite database per profile

### Description

Store each profile in its own SQLite file (for example `profile-{id}.db`) and switch connection string when the active profile changes.

### Pros

- Strongest isolation boundary (data is physically separated).
- Simpler mental model for backup/export/import (one file per profile).
- Lower risk of cross-profile data leaks from missing SQL filters.
- Easy to delete/archive a profile by removing one DB file.

### Cons

- Requires profile-aware connection management and lifecycle handling in app startup.
- Schema migrations must run for all profile DB files, not only one.
- Cross-profile views/operations become harder (must query multiple DB files).
- More file-management edge cases (missing/corrupt profile DB, concurrent file access).

### Estimated impact on current codebase

**Impact: Medium–Large (M/L)**

Expected touchpoints:

- App composition/startup (`src/Arbor.HttpClient.Desktop/App.axaml.cs`) to resolve active profile and DB path.
- All SQLite repositories in `src/Arbor.HttpClient.Storage.Sqlite` (constructed with profile-specific connection string).
- Options persistence flow (`ApplicationOptionsStore`) for active profile metadata.
- New profile management UI and profile switch flow in Desktop layer.

---

## Option B — Multi-tenant model in a single SQLite database

### Description

Keep one DB file but add a `profile_id` column to profile-scoped tables. Every query and write must include profile filtering.

### Pros

- Single DB file and migration path.
- Easier global maintenance (backup, migration, diagnostics).
- Enables cross-profile aggregate views when needed.
- Lower file-management complexity than per-profile DB files.

### Cons

- Higher risk of isolation bugs if any query misses `profile_id` filtering.
- Requires broad schema and repository updates.
- Existing unique constraints may need profile-aware redesign (for example unique name per profile).
- Harder to guarantee strict isolation than separate files.

### Estimated impact on current codebase

**Impact: Large (L)**

Expected touchpoints:

- DB schema/migrations for multiple tables in `Sqlite*Repository` implementations.
- Core repository contracts in `src/Arbor.HttpClient.Core` to accept/propagate profile context.
- All repository CRUD queries and tests (`*.Storage.Sqlite.Tests`) to enforce profile filter behavior.
- Potentially request draft/layout/options persistence if those are profile-scoped.

---

## Option C — Hybrid: separate DB for request data + shared global settings store

### Description

Use separate DB per profile for request-domain data (collections, environments, history), while keeping a small shared settings store for global UI/system preferences.

### Pros

- Strong isolation where it matters most (request data and variables).
- Keeps global app preferences truly global (theme, window behavior if desired).
- Reduces profile switch complexity compared to fully separate-everything design.

### Cons

- Two persistence scopes to reason about (global vs profile).
- Requires clear definition of what is profile-scoped vs global.
- Slightly more architectural complexity than either pure approach.

### Estimated impact on current codebase

**Impact: Medium–Large (M/L)**

Expected touchpoints:

- Same key startup/repository changes as Option A for profile DB routing.
- Additional decisions and refactoring in options/layout persistence to split global vs profile state.

---

## Recommendation for Arbor.HttpClient

For this codebase, **Option A (separate DB per profile)** is the safest first implementation.

Why:

- Current storage is repository-based with one connection string per repository instance, which maps naturally to per-profile DB files.
- It minimizes silent isolation regressions compared to multi-tenant filtering in every query.
- It allows incremental rollout: introduce profile selection and DB switching first, then add profile UX improvements.

If future requirements need global analytics/search across profiles, a hybrid or consolidated model can be evaluated later.

---

## Workspace comparison with other HTTP clients

> Short, practical comparison of how common clients isolate data.

### Postman

- Uses **workspaces** as top-level collaboration/isolation units (requests, collections, environments are scoped to workspace context).
- Supports personal/team/public workspace modes.
- Isolation is strong at the workspace object model level; collaboration features are first-class.

### Insomnia

- Uses **projects/workspaces** in local/cloud models.
- Request collections and environments are grouped per workspace/project context.
- Isolation is mostly organizational with optional sync/collaboration depending on chosen storage model.

### Bruno

- Uses **collections as local files/folders** (Git-native workflow).
- Isolation is primarily filesystem/repository based rather than cloud workspace tenancy.
- Strong local isolation by choosing separate folders/repos for different contexts.

### Hoppscotch

- Uses **workspaces** (personal/team) to group collections/environments.
- Isolation is workspace-centric and collaboration-oriented.

### HTTPie (Desktop)

- Focuses on local request collections/history with account/sync options in newer versions.
- Isolation is lighter and less workspace-heavy than Postman/Hoppscotch.

### Compared to this profile concept

The described **profile** maps most closely to a **local-first workspace** with strict persistence separation. The main difference is emphasis on local isolated app state (including options), not only request objects.

---

## Suggested phased implementation plan

1. Add profile metadata + active-profile selection.
2. Route repository connection string by active profile (Option A baseline).
3. Add profile CRUD UI (create/rename/delete/switch).
4. Decide and implement global-vs-profile scope for options/layout.
5. Add profile export/import and smoke tests for isolation boundaries.

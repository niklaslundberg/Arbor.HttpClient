# Task: Determine the adoption scope for ReactiveUI

**Description**
- Decide whether the project will adopt only `System.Reactive` (lightweight observable streams) while keeping `CommunityToolkit.Mvvm` for property generation, or perform a full migration to `ReactiveUI` as the ViewModel base.
- Document the decision, the expected migration steps, and any constraints.

**Acceptance Criteria**
1. The decision ("System.Reactive only" or "Full ReactiveUI migration") is clearly stated at the top of this file.
2. If "System.Reactive only" is chosen, a brief outline of how the existing MVVM toolkit will continue to be used alongside Rx streams is provided.
3. If "Full ReactiveUI migration" is chosen, a checklist of migration tasks (replace `ViewModelBase` with `ReactiveViewModelBase`, convert all commands to `ReactiveCommand`, etc.) is listed.
4. The document is linked from `docs/architecture/rx-reactive-evaluation.md` and `docs/architecture/reactiveui-migration-plan.md`.
5. No code changes are made yet.

**Tests to Create**
- No tests are required for the decision document itself, but a CI sanity check can verify that this file exists and contains one of the two allowed decision strings (e.g., a small script that greps for `System.Reactive only` or `Full ReactiveUI migration`).
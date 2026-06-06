# Task: Decide and define the event‑bus design for cross‑feature communication

**Description**
- Evaluate whether the project should use a single unified `IFeatureEventBus` that aggregates multiple `Subject<T>` streams, or expose separate per‑feature observable endpoints.
- Create the chosen design (interface(s) and implementation) and update feature ViewModels to publish/subscribe through it.

**Acceptance Criteria**
1. A design decision is documented in this file (single bus vs per‑feature observables).
2. If a single bus is chosen, an interface `IFeatureEventBus` exists with properties like `IObservable<RequestEnvironmentChanged>` and methods `Publish<T>(T @event)`.
3. If per‑feature observables are chosen, each feature defines its own interface (e.g., `IRequestSelection`) exposing the observable streams.
4. Implementation class `FeatureEventBus` (or per‑feature implementations) is registered in the composition root.
5. Feature ViewModels are updated to use the new event contracts instead of direct method calls or shared mutable state.
6. No runtime behavior changes – existing UI flows continue to work.

**Tests to Create**
- Unit test for the bus confirming that a published event is received by a subscribed subscriber.
- Integration test that two feature ViewModels (e.g., `EnvironmentsViewModel` and `RequestEditorViewModel`) can communicate via the bus without direct references.
- Verify that disposing the bus disposes all underlying `Subject`s.
- If per‑feature observables are used, a test that the exposed `IObservable<T>` streams emit the expected values when the feature state
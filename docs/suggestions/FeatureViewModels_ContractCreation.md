# Task: Create Feature‑specific ViewModels with clear contracts

**Description**
- Introduce dedicated ViewModel classes for each UI feature (e.g., `RequestEditorViewModel`, `EnvironmentPanelViewModel`, `OptionsPanelViewModel`, `SchedulerPanelViewModel`).
- Each ViewModel owns its own state and commands and exposes only the minimal public interface needed by other features.
- `MainWindowViewModel` will compose these feature VMs without containing their business logic.

**Acceptance Criteria**
1. New ViewModel classes exist under `src/Arbor.HttpClient.Desktop/Features/<Feature>/`.
2. Each class inherits from the shared base (`ViewModelBase` or `ReactiveViewModelBase` later) and has no direct references to unrelated feature types.
3. Public contracts are defined via interfaces (e.g., `IRequestEditor`) and are used by `MainWindowViewModel` for composition.
4. All existing UI bindings compile without changes to XAML (property names remain the same).
5. No new lines are added to `MainWindowViewModel` beyond wiring the feature VMs.

**Tests to Create**
- Unit test per feature VM verifying that its public methods update its internal state correctly (e.g., `RequestEditorViewModel_SetUrl_UpdatesUrl`).
- Test that `MainWindowViewModel` can instantiate and compose the VMs via constructor injection.
- Verify that feature VMs expose only the intended interfaces using reflection assertions.
using System.Runtime.CompilerServices;
using ReactiveUI.Avalonia;
using ReactiveUI.Builder;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// ReactiveUI 23+ requires explicit initialization before any <c>WhenAnyValue</c> /
/// <c>ReactiveCommand</c> usage. The desktop app initializes via <c>UseReactiveUI</c> in
/// <c>Program.BuildAvaloniaApp</c>; tests that construct view models directly need this
/// module initializer instead. <c>WithAvalonia()</c> registers the Avalonia main-thread
/// scheduler so command notifications are marshalled to the (headless) UI thread —
/// a core-only init would deliver CanExecuteChanged on worker threads and crash bound
/// controls. Initialization is idempotent, so headless tests that also build the
/// Avalonia app are unaffected.
/// </summary>
internal static class ReactiveUiTestInitializer
{
    [ModuleInitializer]
    internal static void Initialize() =>
        RxAppBuilder.CreateReactiveUIBuilder()
            .WithAvalonia()
            .BuildApp();
}

using Arbor.HttpClient.Desktop;
using Arbor.HttpClient.Desktop.E2E.Tests;
using Avalonia;
using Avalonia.Headless;
using ReactiveUI.Avalonia;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Arbor.HttpClient.Desktop.E2E.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
        .WithInterFont()
        .UseReactiveUI(reactiveUiBuilder => reactiveUiBuilder.WithAvalonia())
        .LogToTrace();
}

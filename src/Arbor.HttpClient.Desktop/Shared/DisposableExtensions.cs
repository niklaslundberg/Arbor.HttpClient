using System.Reactive.Disposables;

namespace Arbor.HttpClient.Desktop.Shared;

/// <summary>
/// Fluent registration of subscriptions with a <see cref="CompositeDisposable"/>.
/// Mirrors the <c>DisposeWith</c> extension that ReactiveUI provided before v23.
/// </summary>
public static class DisposableExtensions
{
    public static TDisposable DisposeWith<TDisposable>(this TDisposable disposable, CompositeDisposable disposables)
        where TDisposable : IDisposable
    {
        disposables.Add(disposable);
        return disposable;
    }
}

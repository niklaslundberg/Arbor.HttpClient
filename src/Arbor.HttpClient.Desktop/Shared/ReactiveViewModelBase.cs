using System.Reactive.Disposables;
using ReactiveUI;

namespace Arbor.HttpClient.Desktop.Shared;

/// <summary>
/// Base class for ReactiveUI view models. Subscriptions registered with
/// <see cref="Disposables"/> are released deterministically when the view model is disposed.
/// </summary>
public abstract class ReactiveViewModelBase : ReactiveObject, IDisposable
{
    protected CompositeDisposable Disposables { get; } = new();

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing && !Disposables.IsDisposed)
        {
            Disposables.Dispose();
        }
    }
}

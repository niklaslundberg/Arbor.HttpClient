using System.Reactive.Disposables;
using Dock.Model.ReactiveUI.Controls;

namespace Arbor.HttpClient.Desktop.Shared;

/// <summary>
/// Base class for Dock tool view models that own ReactiveUI subscriptions.
/// Subscriptions registered with <see cref="Disposables"/> are released
/// deterministically when the tool is disposed.
/// </summary>
public abstract class ReactiveToolBase : Tool, IDisposable
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

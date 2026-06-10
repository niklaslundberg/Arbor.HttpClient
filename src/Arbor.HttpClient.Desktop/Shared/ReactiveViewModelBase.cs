using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;

namespace Arbor.HttpClient.Desktop.Shared;

/// <summary>
/// Base class for ReactiveUI view models. Subscriptions registered with
/// <see cref="Disposables"/> are released deterministically when the view model is disposed.
/// </summary>
public abstract class ReactiveViewModelBase : ReactiveObject, IDisposable
{
    private IObservable<PropertyChangedEventArgs>? _propertyChangedObservable;

    protected CompositeDisposable Disposables { get; } = new();

    public IObservable<PropertyChangedEventArgs> PropertyChangedObservable =>
        _propertyChangedObservable ??= Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => PropertyChanged += handler,
                handler => PropertyChanged -= handler)
            .Select(eventPattern => eventPattern.EventArgs);

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

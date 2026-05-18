using System.ComponentModel;
using System.Reactive.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arbor.HttpClient.Desktop.Shared;

public abstract class ViewModelBase : ObservableObject
{
    private IObservable<PropertyChangedEventArgs>? _propertyChangedObservable;

    public IObservable<PropertyChangedEventArgs> PropertyChangedObservable =>
        _propertyChangedObservable ??= Observable
            .FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => PropertyChanged += handler,
                handler => PropertyChanged -= handler)
            .Select(eventPattern => eventPattern.EventArgs);
}

using Arbor.HttpClient.Desktop.Shared;
using System.ComponentModel;
using System.Reactive.Linq;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

public sealed class ViewModelBaseRxTests
{
    [Fact]
    public async Task PropertyChangedObservable_WhenPropertyChanges_PublishesEventArgs()
    {
        var viewModel = new TestViewModel();
        var completionSource = new TaskCompletionSource<PropertyChangedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = viewModel.PropertyChangedObservable
            .Where(eventArgs => string.Equals(eventArgs.PropertyName, nameof(TestViewModel.Name), StringComparison.Ordinal))
            .Subscribe(eventArgs => completionSource.TrySetResult(eventArgs));

        viewModel.Name = "rx-enabled";

        var eventArgs = await completionSource.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        eventArgs.PropertyName.Should().Be(nameof(TestViewModel.Name));
    }

    [Fact]
    public void PropertyChangedObservable_WhenSubscriptionDisposed_DoesNotEmitAfterward()
    {
        var viewModel = new TestViewModel();
        var observedEventCount = 0;
        var subscription = viewModel.PropertyChangedObservable
            .Where(eventArgs => string.Equals(eventArgs.PropertyName, nameof(TestViewModel.Name), StringComparison.Ordinal))
            .Subscribe(_ => observedEventCount++);

        subscription.Dispose();
        viewModel.Name = "ignored-after-dispose";

        observedEventCount.Should().Be(0);
    }

    private sealed class TestViewModel : ViewModelBase
    {
        private string _name = string.Empty;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
    }
}

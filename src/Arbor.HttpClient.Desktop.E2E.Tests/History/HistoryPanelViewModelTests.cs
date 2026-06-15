using System;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Arbor.HttpClient.Core.HttpRequest;
using Arbor.HttpClient.Desktop.Features.History;
using Arbor.HttpClient.Testing.Repositories;

namespace Arbor.HttpClient.Desktop.E2E.Tests.History;

[Collection("HeadlessAvalonia")]
[Trait("Category", "Integration")]
public class HistoryPanelViewModelTests
{
    private static RequestHistoryEntry Entry(string name, string method, string url) =>
        new(Name: name, Method: method, Url: url, Body: null, CreatedAtUtc: DateTimeOffset.UtcNow);

    [AvaloniaFact(Timeout = 10_000)]
    public async Task ReloadAsync_PopulatesHistoryFromRepository()
    {
        var repository = new InMemoryRequestHistoryRepository();
        await repository.SaveAsync(Entry("first", "GET", "http://localhost/a"));
        await repository.SaveAsync(Entry("second", "POST", "http://localhost/b"));
        using var viewModel = new HistoryPanelViewModel(repository, new MessageBus());

        await viewModel.ReloadAsync();

        viewModel.History.Should().HaveCount(2);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public async Task ReloadAsync_WithSearchQuery_FiltersHistory()
    {
        var repository = new InMemoryRequestHistoryRepository();
        await repository.SaveAsync(Entry("create item", "POST", "http://localhost/items"));
        await repository.SaveAsync(Entry("list users", "GET", "http://localhost/users"));
        using var viewModel = new HistoryPanelViewModel(repository, new MessageBus())
        {
            HistorySearchQuery = "POST"
        };

        await viewModel.ReloadAsync();

        viewModel.History.Should().ContainSingle().Which.Method.Should().Be("POST");
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void LoadHistoryRequestCommand_WithEntry_PublishesHistoryRequestLoadRequested()
    {
        var bus = new MessageBus();
        using var viewModel = new HistoryPanelViewModel(new InMemoryRequestHistoryRepository(), bus);
        HistoryRequestLoadRequested? received = null;
        using var subscription = bus.Listen<HistoryRequestLoadRequested>().Subscribe(message => received = message);
        var entry = Entry("entry", "GET", "http://localhost/x");

        viewModel.LoadHistoryRequestCommand.Execute(entry).Subscribe();

        received.Should().NotBeNull();
        received!.Entry.Should().BeSameAs(entry);
    }

    [AvaloniaFact(Timeout = 10_000)]
    public void LoadHistoryRequestCommand_WithNull_DoesNotPublish()
    {
        var bus = new MessageBus();
        using var viewModel = new HistoryPanelViewModel(new InMemoryRequestHistoryRepository(), bus);
        var published = false;
        using var subscription = bus.Listen<HistoryRequestLoadRequested>().Subscribe(_ => published = true);

        viewModel.LoadHistoryRequestCommand.Execute(null).Subscribe();

        published.Should().BeFalse();
    }
}

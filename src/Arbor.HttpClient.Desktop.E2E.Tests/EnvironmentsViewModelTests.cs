using Arbor.HttpClient.Desktop.Features.Environments;
using Arbor.HttpClient.Desktop.Features.HttpRequest;
using Arbor.HttpClient.Testing.Repositories;
using Arbor.HttpClient.Core.Environments;
using Arbor.HttpClient.Core.Variables;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

public class EnvironmentsViewModelTests
{
    private static EnvironmentsViewModel CreateViewModel(InMemoryEnvironmentRepository repository)
    {
        var requestEditor = new RequestEditorViewModel(new VariableResolver(), () => []);
        return new EnvironmentsViewModel(repository, requestEditor, () => null);
    }

    [Fact]
    public void NewEnvironmentCommand_ShouldResetDraftAndShowPanel()
    {
        var repository = new InMemoryEnvironmentRepository();
        var viewModel = CreateViewModel(repository);

        viewModel.NewEnvironmentName = "old";
        viewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("host", "localhost"));

        viewModel.NewEnvironmentCommand.Execute(null);

        viewModel.NewEnvironmentName.Should().BeEmpty();
        viewModel.ActiveEnvironment.Should().BeNull();
        viewModel.ActiveEnvironmentVariables.Should().BeEmpty();
        viewModel.IsEnvironmentPanelVisible.Should().BeTrue();
    }

    [Fact]
    public async Task SaveEnvironmentCommand_ShouldCreateAndActivateEnvironment()
    {
        var repository = new InMemoryEnvironmentRepository();
        var viewModel = CreateViewModel(repository);
        viewModel.NewEnvironmentCommand.Execute(null);
        viewModel.NewEnvironmentName = "dev";
        viewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("host", "localhost"));

        await viewModel.SaveEnvironmentCommand.ExecuteAsync(null);

        viewModel.Environments.Should().ContainSingle(environment => environment.Name == "dev");
        viewModel.ActiveEnvironment.Should().NotBeNull();
        viewModel.ActiveEnvironment!.Name.Should().Be("dev");
    }

    [Fact]
    public async Task EditEnvironmentCommand_ShouldLoadVariablesIntoDraft()
    {
        var repository = new InMemoryEnvironmentRepository();
        var environmentId = await repository.SaveAsync("prod",
        [
            new EnvironmentVariable("baseUrl", "http://localhost:5000", true),
            new EnvironmentVariable("token", "secret", false)
        ]);

        var viewModel = CreateViewModel(repository);
        await viewModel.LoadEnvironmentsAsync();
        var environment = viewModel.Environments.Single(e => e.Id == environmentId);

        viewModel.EditEnvironmentCommand.Execute(environment);

        viewModel.NewEnvironmentName.Should().Be("prod");
        viewModel.ActiveEnvironmentVariables.Should().HaveCount(2);
        viewModel.ActiveEnvironmentVariables[0].Name.Should().Be("baseUrl");
        viewModel.ActiveEnvironmentVariables[1].IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteEnvironmentCommand_ShouldRemoveEnvironmentAndClearSelection()
    {
        var repository = new InMemoryEnvironmentRepository();
        var environmentId = await repository.SaveAsync("qa", [new EnvironmentVariable("host", "localhost", true)]);
        var viewModel = CreateViewModel(repository);
        await viewModel.LoadEnvironmentsAsync();
        viewModel.ActiveEnvironment = viewModel.Environments.Single(environment => environment.Id == environmentId);

        await viewModel.DeleteEnvironmentCommand.ExecuteAsync(viewModel.ActiveEnvironment);

        viewModel.Environments.Should().BeEmpty();
        viewModel.ActiveEnvironment.Should().BeNull();
    }

    [Fact]
    public void GetActiveVariablesForEditor_ShouldReturnCurrentVariableSnapshot()
    {
        var repository = new InMemoryEnvironmentRepository();
        var viewModel = CreateViewModel(repository);
        viewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("host", "localhost", true));
        viewModel.ActiveEnvironmentVariables.Add(new EnvironmentVariableViewModel("token", "abc", false));

        var variables = viewModel.GetActiveVariablesForEditor();

        variables.Should().HaveCount(2);
        variables[0].Name.Should().Be("host");
        variables[1].IsEnabled.Should().BeFalse();
    }
}

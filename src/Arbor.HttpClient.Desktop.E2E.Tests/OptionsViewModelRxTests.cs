using Arbor.HttpClient.Desktop.Features.Options;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

public sealed class OptionsViewModelRxTests
{
    [Fact]
    public void SelectedOptionsPage_WhenChanged_UpdatesDerivedReactiveProperties()
    {
        using var viewModel = new OptionsViewModel(app: null!);

        viewModel.SelectedOptionsPage = "ScheduledJobs";

        viewModel.SelectedOptionsPageTitle.Should().Be("Scheduled Jobs");
        viewModel.SelectedOptionsPageBreadcrumb.Should().Be("Options ›  Scheduled Jobs");
    }

    [Fact]
    public void Dispose_WhenCalled_StopsDerivedReactivePropertyUpdates()
    {
        using var viewModel = new OptionsViewModel(app: null!);
        viewModel.SelectedOptionsPage = "ScheduledJobs";
        viewModel.SelectedOptionsPageTitle.Should().Be("Scheduled Jobs");

        viewModel.Dispose();
        viewModel.SelectedOptionsPage = "Diagnostics";

        viewModel.SelectedOptionsPageTitle.Should().Be("Scheduled Jobs");
        viewModel.SelectedOptionsPageBreadcrumb.Should().Be("Options ›  Scheduled Jobs");
    }
}

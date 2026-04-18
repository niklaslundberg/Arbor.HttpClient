using Arbor.HttpClient.Desktop.Models;
using Arbor.HttpClient.Desktop.Services;
using AwesomeAssertions;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

public class ApplicationOptionsStoreTests
{
    [Fact]
    public void Load_ShouldReturnDefaults_WhenOptionsFileDoesNotExist()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}", "options.json");
        var store = new ApplicationOptionsStore(path);

        var options = store.Load();

        options.Http.DefaultContentType.Should().Be("application/json");
        options.Appearance.Theme.Should().Be("System");
        options.ScheduledJobs.AutoStartOnLaunch.Should().BeTrue();
        options.ScheduledJobs.DefaultIntervalSeconds.Should().Be(60);
    }

    [Fact]
    public void Import_ShouldRejectInvalidHttpVersion()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var optionsPath = Path.Combine(tempDir, "options.json");
        var importPath = Path.Combine(tempDir, "invalid-options.json");
        File.WriteAllText(importPath, """
        {
          "http": {
            "httpVersion": "5.0",
            "tlsVersion": "Tls12",
            "defaultContentType": "application/json",
            "followRedirects": true,
            "defaultRequestUrl": "https://example.com"
          },
          "appearance": {
            "theme": "Dark",
            "fontSize": 13,
            "fontFamily": "Cascadia Code,Consolas,Menlo,monospace"
          }
        }
        """);

        var store = new ApplicationOptionsStore(optionsPath);
        var action = () => store.Import(importPath);

        action.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void SaveAndImport_ShouldRoundtripValidOptions()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var optionsPath = Path.Combine(tempDir, "options.json");
        var exportPath = Path.Combine(tempDir, "exported-options.json");
        var store = new ApplicationOptionsStore(optionsPath);
        var options = new ApplicationOptions
        {
            Http = new HttpOptions
            {
                HttpVersion = "2.0",
                TlsVersion = "Tls13",
                DefaultContentType = "application/json",
                FollowRedirects = false,
                DefaultRequestUrl = "https://example.com/api"
            },
            Appearance = new AppearanceOptions
            {
                Theme = "Dark",
                FontFamily = "Consolas,Menlo,monospace",
                FontSize = 14
            },
            ScheduledJobs = new ScheduledJobsOptions
            {
                AutoStartOnLaunch = false,
                DefaultIntervalSeconds = 120
            }
        };

        store.Save(options);
        store.Export(exportPath, options);
        var imported = store.Import(exportPath);

        imported.Http.HttpVersion.Should().Be("2.0");
        imported.Http.FollowRedirects.Should().BeFalse();
        imported.Appearance.Theme.Should().Be("Dark");
        imported.Appearance.FontSize.Should().Be(14);
        imported.ScheduledJobs.AutoStartOnLaunch.Should().BeFalse();
        imported.ScheduledJobs.DefaultIntervalSeconds.Should().Be(120);
    }

    [Fact]
    public void Validate_ShouldRejectZeroDefaultInterval()
    {
        var options = new ApplicationOptions
        {
            Http = new HttpOptions
            {
                HttpVersion = "1.1",
                TlsVersion = "SystemDefault",
                DefaultContentType = "application/json",
                FollowRedirects = true,
                DefaultRequestUrl = "https://example.com"
            },
            Appearance = new AppearanceOptions
            {
                Theme = "System",
                FontFamily = "Consolas",
                FontSize = 13
            },
            ScheduledJobs = new ScheduledJobsOptions
            {
                AutoStartOnLaunch = true,
                DefaultIntervalSeconds = 0
            }
        };

        var action = () => ApplicationOptionsStore.Validate(options);

        action.Should().Throw<InvalidDataException>().WithMessage("*interval*");
    }
}

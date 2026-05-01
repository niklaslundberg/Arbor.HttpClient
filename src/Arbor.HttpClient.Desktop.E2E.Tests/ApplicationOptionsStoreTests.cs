using Arbor.HttpClient.Desktop.Features.Layout;
using Arbor.HttpClient.Desktop.Features.Options;
using Arbor.HttpClient.Desktop.Features.ScheduledJobs;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

[Trait("Category", "Integration")]
public class ApplicationOptionsStoreTests
{
    [Fact]
    public void Load_ShouldReturnDefaults_WhenOptionsFileDoesNotExist()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}", "options.json");
        var store = new ApplicationOptionsStore(path);

        var options = store.Load();

        options.Http.DefaultContentType.Should().Be("application/json");
        options.Http.EnableHttpDiagnostics.Should().BeFalse();
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
            "defaultRequestUrl": "http://localhost:5000/echo"
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
                EnableHttpDiagnostics = true,
                DefaultContentType = "application/json",
                FollowRedirects = false,
                DefaultRequestUrl = "http://localhost:5000/echo"
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
            },
            Layouts = new LayoutOptions
            {
                CurrentLayout = new DockLayoutSnapshot
                {
                    LeftToolProportion = 0.3,
                    DocumentProportion = 0.7,
                    ActiveToolDockableId = "left-panel",
                    LeftToolDockableOrder = ["left-panel", "options"]
                },
                SavedLayouts =
                [
                    new NamedDockLayout
                    {
                        Name = "Layout 1",
                        Layout = new DockLayoutSnapshot
                        {
                            LeftToolProportion = 0.4,
                            DocumentProportion = 0.6,
                            ActiveToolDockableId = "options",
                            LeftToolDockableOrder = ["options", "left-panel"]
                        }
                    }
                ]
            }
        };

        store.Save(options);
        store.Export(exportPath, options);
        var imported = store.Import(exportPath);

        imported.Http.HttpVersion.Should().Be("2.0");
        imported.Http.EnableHttpDiagnostics.Should().BeTrue();
        imported.Http.FollowRedirects.Should().BeFalse();
        imported.Appearance.Theme.Should().Be("Dark");
        imported.Appearance.FontSize.Should().Be(14);
        imported.ScheduledJobs.AutoStartOnLaunch.Should().BeFalse();
        imported.ScheduledJobs.DefaultIntervalSeconds.Should().Be(120);
        imported.Layouts.CurrentLayout.Should().NotBeNull();
        imported.Layouts.CurrentLayout!.LeftToolProportion.Should().Be(0.3);
        imported.Layouts.SavedLayouts.Should().ContainSingle();
        imported.Layouts.SavedLayouts[0].Name.Should().Be("Layout 1");
    }

    [Theory]
    [InlineData("Tls10")]
    [InlineData("Tls11")]
    public void Validate_ShouldAcceptInsecureTlsVersions(string tlsVersion)
    {
        var options = new ApplicationOptions
        {
            Http = new HttpOptions
            {
                HttpVersion = "1.1",
                TlsVersion = tlsVersion,
                DefaultContentType = "application/json",
                FollowRedirects = true,
                DefaultRequestUrl = "https://example.com"
            },
            Appearance = new AppearanceOptions
            {
                Theme = "System",
                FontFamily = "Consolas",
                FontSize = 13
            }
        };

        var action = () => ApplicationOptionsStore.Validate(options);

        action.Should().NotThrow();
    }

    [Fact]
    public void Validate_ShouldRejectUnknownTlsVersion()
    {
        var options = new ApplicationOptions
        {
            Http = new HttpOptions
            {
                HttpVersion = "1.1",
                TlsVersion = "Tls99",
                DefaultContentType = "application/json",
                FollowRedirects = true,
                DefaultRequestUrl = "https://example.com"
            },
            Appearance = new AppearanceOptions
            {
                Theme = "System",
                FontFamily = "Consolas",
                FontSize = 13
            }
        };

        var action = () => ApplicationOptionsStore.Validate(options);

        action.Should().Throw<InvalidDataException>().WithMessage("*TLS version*");
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
                DefaultRequestUrl = "http://localhost:5000/echo"
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

    [Fact]
    public void Validate_ShouldRejectDuplicateSavedLayoutNames()
    {
        var options = new ApplicationOptions
        {
            Layouts = new LayoutOptions
            {
                SavedLayouts =
                [
                    new NamedDockLayout { Name = "Layout 1", Layout = new DockLayoutSnapshot() },
                    new NamedDockLayout { Name = "layout 1", Layout = new DockLayoutSnapshot() }
                ]
            }
        };

        var action = () => ApplicationOptionsStore.Validate(options);

        action.Should().Throw<InvalidDataException>().WithMessage("*Duplicate saved layout name*");
    }
}

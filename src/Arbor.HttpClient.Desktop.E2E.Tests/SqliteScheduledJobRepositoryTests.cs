using Arbor.HttpClient.Core.Models;
using Arbor.HttpClient.Storage.Sqlite;
using AwesomeAssertions;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

public class SqliteScheduledJobRepositoryTests
{
    [Fact]
    public async Task SaveAndLoad_ShouldPersistFollowRedirectOverride()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        var repository = new SqliteScheduledJobRepository(connectionString);
        await repository.InitializeAsync();

        var id = await repository.SaveAsync(new ScheduledJobConfig(
            0,
            "job",
            "GET",
            "https://example.com",
            null,
            null,
            30,
            AutoStart: true,
            FollowRedirects: false));

        var items = await repository.GetAllAsync();

        items.Should().ContainSingle();
        items[0].Id.Should().Be(id);
        items[0].FollowRedirects.Should().BeFalse();
    }

    [Fact]
    public async Task SaveAndLoad_ShouldPersistUseWebViewEnabled()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        var repository = new SqliteScheduledJobRepository(connectionString);
        await repository.InitializeAsync();

        var id = await repository.SaveAsync(new ScheduledJobConfig(
            0,
            "web-view-job",
            "GET",
            "https://example.com",
            null,
            null,
            60,
            AutoStart: false,
            FollowRedirects: true,
            UseWebView: true));

        var items = await repository.GetAllAsync();

        items.Should().ContainSingle();
        items[0].Id.Should().Be(id);
        items[0].UseWebView.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAndLoad_ShouldDefaultUseWebViewToFalse()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        var repository = new SqliteScheduledJobRepository(connectionString);
        await repository.InitializeAsync();

        var id = await repository.SaveAsync(new ScheduledJobConfig(
            0,
            "plain-job",
            "POST",
            "https://example.com/api",
            null,
            null,
            120,
            AutoStart: false));

        var items = await repository.GetAllAsync();

        items.Should().ContainSingle();
        items[0].Id.Should().Be(id);
        items[0].UseWebView.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAndLoad_ShouldPersistUseWebViewChange()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        var repository = new SqliteScheduledJobRepository(connectionString);
        await repository.InitializeAsync();

        var id = await repository.SaveAsync(new ScheduledJobConfig(
            0,
            "update-job",
            "GET",
            "https://example.com",
            null,
            null,
            30,
            AutoStart: false,
            UseWebView: false));

        await repository.UpdateAsync(new ScheduledJobConfig(
            id,
            "update-job",
            "GET",
            "https://example.com",
            null,
            null,
            30,
            AutoStart: false,
            UseWebView: true));

        var items = await repository.GetAllAsync();

        items.Should().ContainSingle();
        items[0].UseWebView.Should().BeTrue();
    }
}

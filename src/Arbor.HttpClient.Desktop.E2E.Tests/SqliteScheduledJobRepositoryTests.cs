using Arbor.HttpClient.Core.Models;
using Arbor.HttpClient.Storage.Sqlite;
using Microsoft.Data.Sqlite;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

public class SqliteScheduledJobRepositoryTests
{
    [Fact]
    public async Task SaveAndLoad_ShouldPersistFollowRedirectOverride()
    {
        var dbPath = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        var repository = new SqliteScheduledJobRepository(connectionString);
        await repository.InitializeAsync();

        try
        {
            var id = await repository.SaveAsync(new ScheduledJobConfig(
                0,
                "job",
                "GET",
                "http://localhost:5000",
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
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public async Task SaveAndLoad_ShouldPersistUseWebViewEnabled()
    {
        var dbPath = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        var repository = new SqliteScheduledJobRepository(connectionString);
        await repository.InitializeAsync();

        try
        {
            var id = await repository.SaveAsync(new ScheduledJobConfig(
                0,
                "web-view-job",
                "GET",
                "http://localhost:5000",
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
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public async Task SaveAndLoad_ShouldDefaultUseWebViewToFalse()
    {
        var dbPath = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        var repository = new SqliteScheduledJobRepository(connectionString);
        await repository.InitializeAsync();

        try
        {
            var id = await repository.SaveAsync(new ScheduledJobConfig(
                0,
                "plain-job",
                "POST",
                "http://localhost:5000/api",
                null,
                null,
                120,
                AutoStart: false));

            var items = await repository.GetAllAsync();

            items.Should().ContainSingle();
            items[0].Id.Should().Be(id);
            items[0].UseWebView.Should().BeFalse();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }

    [Fact]
    public async Task UpdateAndLoad_ShouldPersistUseWebViewChange()
    {
        var dbPath = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        var connectionString = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        var repository = new SqliteScheduledJobRepository(connectionString);
        await repository.InitializeAsync();

        try
        {
            var id = await repository.SaveAsync(new ScheduledJobConfig(
                0,
                "update-job",
                "GET",
                "http://localhost:5000",
                null,
                null,
                30,
                AutoStart: false,
                UseWebView: false));

            await repository.UpdateAsync(new ScheduledJobConfig(
                id,
                "update-job",
                "GET",
                "http://localhost:5000",
                null,
                null,
                30,
                AutoStart: false,
                UseWebView: true));

            var items = await repository.GetAllAsync();

            items.Should().ContainSingle();
            items[0].UseWebView.Should().BeTrue();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
            {
                File.Delete(dbPath);
            }
        }
    }
}

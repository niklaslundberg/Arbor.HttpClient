using Microsoft.Data.Sqlite;
using Arbor.HttpClient.Core.ScheduledJobs;

namespace Arbor.HttpClient.Storage.Sqlite;

public sealed class SqliteScheduledJobRepository(string connectionString) : IScheduledJobRepository
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS scheduled_jobs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                method TEXT NOT NULL,
                url TEXT NOT NULL,
                body TEXT NULL,
                headers_json TEXT NULL,
                interval_seconds INTEGER NOT NULL,
                auto_start INTEGER NOT NULL DEFAULT 0,
                follow_redirects INTEGER NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        await using var addFollowRedirectsColumn = connection.CreateCommand();
        addFollowRedirectsColumn.CommandText = "ALTER TABLE scheduled_jobs ADD COLUMN follow_redirects INTEGER NULL;";
        try
        {
            await addFollowRedirectsColumn.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception) when (exception.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase))
        {
            // Column already exists.
        }

        await using var addUseWebViewColumn = connection.CreateCommand();
        addUseWebViewColumn.CommandText = "ALTER TABLE scheduled_jobs ADD COLUMN use_web_view INTEGER NOT NULL DEFAULT 0;";
        try
        {
            await addUseWebViewColumn.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException exception) when (exception.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase))
        {
            // Column already exists.
        }
    }

    public async Task<int> SaveAsync(ScheduledJobConfig config, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            INSERT INTO scheduled_jobs (name, method, url, body, headers_json, interval_seconds, auto_start, follow_redirects, use_web_view)
            VALUES ($name, $method, $url, $body, $headers, $interval, $autoStart, $followRedirects, $useWebView);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$name", config.Name);
        cmd.Parameters.AddWithValue("$method", config.Method);
        cmd.Parameters.AddWithValue("$url", config.Url);
        cmd.Parameters.AddWithValue("$body", config.Body ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$headers", config.HeadersJson ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$interval", config.IntervalSeconds);
        cmd.Parameters.AddWithValue("$autoStart", config.AutoStart ? 1 : 0);
        cmd.Parameters.AddWithValue("$followRedirects", config.FollowRedirects is null ? DBNull.Value : config.FollowRedirects.Value ? 1 : 0);
        cmd.Parameters.AddWithValue("$useWebView", config.UseWebView ? 1 : 0);

        var id = (long)(await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
        return (int)id;
    }

    public async Task UpdateAsync(ScheduledJobConfig config, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            UPDATE scheduled_jobs
            SET name=$name, method=$method, url=$url, body=$body,
                headers_json=$headers, interval_seconds=$interval, auto_start=$autoStart,
                follow_redirects=$followRedirects, use_web_view=$useWebView
            WHERE id=$id;
            """;
        cmd.Parameters.AddWithValue("$id", config.Id);
        cmd.Parameters.AddWithValue("$name", config.Name);
        cmd.Parameters.AddWithValue("$method", config.Method);
        cmd.Parameters.AddWithValue("$url", config.Url);
        cmd.Parameters.AddWithValue("$body", config.Body ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$headers", config.HeadersJson ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$interval", config.IntervalSeconds);
        cmd.Parameters.AddWithValue("$autoStart", config.AutoStart ? 1 : 0);
        cmd.Parameters.AddWithValue("$followRedirects", config.FollowRedirects is null ? DBNull.Value : config.FollowRedirects.Value ? 1 : 0);
        cmd.Parameters.AddWithValue("$useWebView", config.UseWebView ? 1 : 0);

        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM scheduled_jobs WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ScheduledJobConfig>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT id, name, method, url, body, headers_json, interval_seconds, auto_start, follow_redirects, use_web_view
            FROM scheduled_jobs
            ORDER BY id;
            """;

        var results = new List<ScheduledJobConfig>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(new ScheduledJobConfig(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetInt32(6),
                reader.GetInt32(7) != 0,
                reader.IsDBNull(8) ? null : reader.GetInt32(8) != 0,
                reader.GetInt32(9) != 0));
        }

        return results;
    }
}

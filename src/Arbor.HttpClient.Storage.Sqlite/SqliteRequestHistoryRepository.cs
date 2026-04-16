using Arbor.HttpClient.Core.Abstractions;
using Arbor.HttpClient.Core.Models;
using Microsoft.Data.Sqlite;

namespace Arbor.HttpClient.Storage.Sqlite;

public sealed class SqliteRequestHistoryRepository(string databasePath) : IRequestHistoryRepository
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        EnsureDirectoryExists();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS request_history (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                method TEXT NOT NULL,
                url TEXT NOT NULL,
                body TEXT NULL,
                created_at_utc TEXT NOT NULL
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsync(SavedRequest request, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO request_history (name, method, url, body, created_at_utc)
            VALUES ($name, $method, $url, $body, $createdAtUtc);
            """;
        command.Parameters.AddWithValue("$name", request.Name);
        command.Parameters.AddWithValue("$method", request.Method);
        command.Parameters.AddWithValue("$url", request.Url);
        command.Parameters.AddWithValue("$body", request.Body ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$createdAtUtc", request.CreatedAtUtc.UtcDateTime);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SavedRequest>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT name, method, url, body, created_at_utc
            FROM request_history
            ORDER BY created_at_utc DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        var items = new List<SavedRequest>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            items.Add(new SavedRequest(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(4), DateTimeKind.Utc))));
        }

        return items;
    }

    private void EnsureDirectoryExists()
    {
        var databaseDirectory = Path.GetDirectoryName(new SqliteConnectionStringBuilder(_connectionString).DataSource);
        if (!string.IsNullOrWhiteSpace(databaseDirectory))
        {
            Directory.CreateDirectory(databaseDirectory);
        }
    }
}

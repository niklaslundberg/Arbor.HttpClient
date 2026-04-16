using Arbor.HttpClient.Core.Abstractions;
using Arbor.HttpClient.Core.Models;
using Microsoft.Data.Sqlite;

namespace Arbor.HttpClient.Storage.Sqlite;

public sealed class SqliteCollectionRepository(string connectionString) : ICollectionRepository
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS collections (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                source_path TEXT NULL,
                base_url TEXT NULL,
                created_at_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS collection_requests (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                collection_id INTEGER NOT NULL REFERENCES collections(id) ON DELETE CASCADE,
                name TEXT NOT NULL,
                method TEXT NOT NULL,
                path TEXT NOT NULL,
                description TEXT NULL
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> SaveAsync(string name, string? sourcePath, string? baseUrl, IReadOnlyList<CollectionRequest> requests, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using var insertCollection = connection.CreateCommand();
        insertCollection.Transaction = (SqliteTransaction)transaction;
        insertCollection.CommandText =
            """
            INSERT INTO collections (name, source_path, base_url, created_at_utc)
            VALUES ($name, $sourcePath, $baseUrl, $createdAtUtc);
            SELECT last_insert_rowid();
            """;
        insertCollection.Parameters.AddWithValue("$name", name);
        insertCollection.Parameters.AddWithValue("$sourcePath", sourcePath ?? (object)DBNull.Value);
        insertCollection.Parameters.AddWithValue("$baseUrl", baseUrl ?? (object)DBNull.Value);
        insertCollection.Parameters.AddWithValue("$createdAtUtc", DateTimeOffset.UtcNow.UtcDateTime);

        var collectionId = (long)(await insertCollection.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;

        foreach (var request in requests)
        {
            await using var insertRequest = connection.CreateCommand();
            insertRequest.Transaction = (SqliteTransaction)transaction;
            insertRequest.CommandText =
                """
                INSERT INTO collection_requests (collection_id, name, method, path, description)
                VALUES ($collectionId, $name, $method, $path, $description);
                """;
            insertRequest.Parameters.AddWithValue("$collectionId", collectionId);
            insertRequest.Parameters.AddWithValue("$name", request.Name);
            insertRequest.Parameters.AddWithValue("$method", request.Method);
            insertRequest.Parameters.AddWithValue("$path", request.Path);
            insertRequest.Parameters.AddWithValue("$description", request.Description ?? (object)DBNull.Value);
            await insertRequest.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return (int)collectionId;
    }

    public async Task<IReadOnlyList<Collection>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT c.id, c.name, c.source_path, c.base_url,
                   r.name, r.method, r.path, r.description
            FROM collections c
            LEFT JOIN collection_requests r ON r.collection_id = c.id
            ORDER BY c.id, r.id;
            """;

        var collections = new Dictionary<int, (string Name, string? SourcePath, string? BaseUrl, List<CollectionRequest> Requests)>();

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var id = reader.GetInt32(0);
            if (!collections.TryGetValue(id, out var entry))
            {
                entry = (reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), []);
                collections[id] = entry;
            }

            if (!reader.IsDBNull(4))
            {
                entry.Requests.Add(new CollectionRequest(
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7)));
            }
        }

        return collections
            .Select(kvp => new Collection(kvp.Key, kvp.Value.Name, kvp.Value.SourcePath, kvp.Value.BaseUrl, kvp.Value.Requests))
            .ToList();
    }

    public async Task DeleteAsync(int collectionId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM collections WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", collectionId);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

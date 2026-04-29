using System.Text.Json;
using Microsoft.Data.Sqlite;
using Arbor.HttpClient.Core.Collections;
using Arbor.HttpClient.Core.HttpRequest;

namespace Arbor.HttpClient.Storage.Sqlite;

public sealed class SqliteCollectionRepository(string connectionString) : ICollectionRepository
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnableForeignKeysAsync(connection, cancellationToken).ConfigureAwait(false);

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
                description TEXT NULL,
                notes TEXT NULL
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        await RunMigrationAsync(connection, "ALTER TABLE collection_requests ADD COLUMN notes TEXT NULL;", cancellationToken).ConfigureAwait(false);
        await RunMigrationAsync(connection, "ALTER TABLE collection_requests ADD COLUMN tag TEXT NULL;", cancellationToken).ConfigureAwait(false);
        await RunMigrationAsync(connection, "ALTER TABLE collection_requests ADD COLUMN body TEXT NULL;", cancellationToken).ConfigureAwait(false);
        await RunMigrationAsync(connection, "ALTER TABLE collection_requests ADD COLUMN content_type TEXT NULL;", cancellationToken).ConfigureAwait(false);
        await RunMigrationAsync(connection, "ALTER TABLE collection_requests ADD COLUMN headers TEXT NULL;", cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> SaveAsync(string name, string? sourcePath, string? baseUrl, IReadOnlyList<CollectionRequest> requests, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnableForeignKeysAsync(connection, cancellationToken).ConfigureAwait(false);

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

        await InsertRequestsAsync(connection, (SqliteTransaction)transaction, (int)collectionId, requests, cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return (int)collectionId;
    }

    public async Task<IReadOnlyList<Collection>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnableForeignKeysAsync(connection, cancellationToken).ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT c.id, c.name, c.source_path, c.base_url,
                   r.name, r.method, r.path, r.description, r.notes,
                   r.tag, r.body, r.content_type, r.headers
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
                var headersJson = reader.IsDBNull(12) ? null : reader.GetString(12);
                IReadOnlyList<RequestHeader>? headers = null;
                if (!string.IsNullOrEmpty(headersJson))
                {
                    headers = JsonSerializer.Deserialize<List<RequestHeader>>(headersJson);
                }

                entry.Requests.Add(new CollectionRequest(
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7),
                    reader.IsDBNull(8) ? null : reader.GetString(8),
                    Tag: reader.IsDBNull(9) ? null : reader.GetString(9),
                    Body: reader.IsDBNull(10) ? null : reader.GetString(10),
                    ContentType: reader.IsDBNull(11) ? null : reader.GetString(11),
                    Headers: headers));
            }
        }

        return collections
            .Select(kvp => new Collection(kvp.Key, kvp.Value.Name, kvp.Value.SourcePath, kvp.Value.BaseUrl, kvp.Value.Requests))
            .ToList();
    }

    public async Task UpdateAsync(int collectionId, string name, string? sourcePath, string? baseUrl, IReadOnlyList<CollectionRequest> requests, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnableForeignKeysAsync(connection, cancellationToken).ConfigureAwait(false);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using var updateCollection = connection.CreateCommand();
        updateCollection.Transaction = (SqliteTransaction)transaction;
        updateCollection.CommandText =
            """
            UPDATE collections
            SET name = $name, source_path = $sourcePath, base_url = $baseUrl
            WHERE id = $id;
            """;
        updateCollection.Parameters.AddWithValue("$id", collectionId);
        updateCollection.Parameters.AddWithValue("$name", name);
        updateCollection.Parameters.AddWithValue("$sourcePath", sourcePath ?? (object)DBNull.Value);
        updateCollection.Parameters.AddWithValue("$baseUrl", baseUrl ?? (object)DBNull.Value);
        await updateCollection.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        await using var deleteRequests = connection.CreateCommand();
        deleteRequests.Transaction = (SqliteTransaction)transaction;
        deleteRequests.CommandText = "DELETE FROM collection_requests WHERE collection_id = $id;";
        deleteRequests.Parameters.AddWithValue("$id", collectionId);
        await deleteRequests.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        await InsertRequestsAsync(connection, (SqliteTransaction)transaction, collectionId, requests, cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(int collectionId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnableForeignKeysAsync(connection, cancellationToken).ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM collections WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", collectionId);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnableForeignKeysAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys=ON;";
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task RunMigrationAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        try
        {
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqliteException)
        {
            // Column already exists — safe to ignore
        }
    }

    private static async Task InsertRequestsAsync(SqliteConnection connection, SqliteTransaction transaction, int collectionId, IReadOnlyList<CollectionRequest> requests, CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
        {
            return;
        }

        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText =
            """
            INSERT INTO collection_requests (collection_id, name, method, path, description, notes, tag, body, content_type, headers)
            VALUES ($collectionId, $name, $method, $path, $description, $notes, $tag, $body, $contentType, $headers);
            """;
        var pCollectionId = cmd.Parameters.Add("$collectionId", SqliteType.Integer);
        var pName = cmd.Parameters.Add("$name", SqliteType.Text);
        var pMethod = cmd.Parameters.Add("$method", SqliteType.Text);
        var pPath = cmd.Parameters.Add("$path", SqliteType.Text);
        var pDescription = cmd.Parameters.Add("$description", SqliteType.Text);
        var pNotes = cmd.Parameters.Add("$notes", SqliteType.Text);
        var pTag = cmd.Parameters.Add("$tag", SqliteType.Text);
        var pBody = cmd.Parameters.Add("$body", SqliteType.Text);
        var pContentType = cmd.Parameters.Add("$contentType", SqliteType.Text);
        var pHeaders = cmd.Parameters.Add("$headers", SqliteType.Text);
        pCollectionId.Value = collectionId;

        await cmd.PrepareAsync(cancellationToken).ConfigureAwait(false);

        foreach (var request in requests)
        {
            pName.Value = request.Name;
            pMethod.Value = request.Method;
            pPath.Value = request.Path;
            pDescription.Value = request.Description ?? (object)DBNull.Value;
            pNotes.Value = request.Notes ?? (object)DBNull.Value;
            pTag.Value = request.Tag ?? (object)DBNull.Value;
            pBody.Value = request.Body ?? (object)DBNull.Value;
            pContentType.Value = request.ContentType ?? (object)DBNull.Value;
            pHeaders.Value = request.Headers is { Count: > 0 }
                ? JsonSerializer.Serialize(request.Headers)
                : (object)DBNull.Value;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}


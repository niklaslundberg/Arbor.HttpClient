using Arbor.HttpClient.Core.Abstractions;
using Arbor.HttpClient.Core.Models;
using Microsoft.Data.Sqlite;

namespace Arbor.HttpClient.Storage.Sqlite;

public sealed class SqliteEnvironmentRepository(string connectionString) : IEnvironmentRepository
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS environments (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS environment_variables (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                environment_id INTEGER NOT NULL REFERENCES environments(id) ON DELETE CASCADE,
                name TEXT NOT NULL,
                value TEXT NOT NULL
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> SaveAsync(string name, IReadOnlyList<EnvironmentVariable> variables, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using var insertEnv = connection.CreateCommand();
        insertEnv.Transaction = (SqliteTransaction)transaction;
        insertEnv.CommandText =
            """
            INSERT INTO environments (name) VALUES ($name);
            SELECT last_insert_rowid();
            """;
        insertEnv.Parameters.AddWithValue("$name", name);
        var envId = (long)(await insertEnv.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;

        await InsertVariablesAsync(connection, (SqliteTransaction)transaction, (int)envId, variables, cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return (int)envId;
    }

    public async Task UpdateAsync(int environmentId, string name, IReadOnlyList<EnvironmentVariable> variables, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using var updateName = connection.CreateCommand();
        updateName.Transaction = (SqliteTransaction)transaction;
        updateName.CommandText = "UPDATE environments SET name = $name WHERE id = $id;";
        updateName.Parameters.AddWithValue("$name", name);
        updateName.Parameters.AddWithValue("$id", environmentId);
        await updateName.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        await using var deleteVars = connection.CreateCommand();
        deleteVars.Transaction = (SqliteTransaction)transaction;
        deleteVars.CommandText = "DELETE FROM environment_variables WHERE environment_id = $id;";
        deleteVars.Parameters.AddWithValue("$id", environmentId);
        await deleteVars.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        await InsertVariablesAsync(connection, (SqliteTransaction)transaction, environmentId, variables, cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RequestEnvironment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT e.id, e.name, v.name, v.value
            FROM environments e
            LEFT JOIN environment_variables v ON v.environment_id = e.id
            ORDER BY e.id, v.id;
            """;

        var environments = new Dictionary<int, (string Name, List<EnvironmentVariable> Variables)>();

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var id = reader.GetInt32(0);
            if (!environments.TryGetValue(id, out var entry))
            {
                entry = (reader.GetString(1), []);
                environments[id] = entry;
            }

            if (!reader.IsDBNull(2))
            {
                entry.Variables.Add(new EnvironmentVariable(reader.GetString(2), reader.GetString(3)));
            }
        }

        return environments
            .Select(kvp => new RequestEnvironment(kvp.Key, kvp.Value.Name, kvp.Value.Variables))
            .ToList();
    }

    public async Task DeleteAsync(int environmentId, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM environments WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", environmentId);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertVariablesAsync(SqliteConnection connection, SqliteTransaction transaction, int environmentId, IReadOnlyList<EnvironmentVariable> variables, CancellationToken cancellationToken)
    {
        if (variables.Count == 0)
        {
            return;
        }

        await using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText =
            """
            INSERT INTO environment_variables (environment_id, name, value)
            VALUES ($envId, $name, $value);
            """;
        var pEnvId = cmd.Parameters.Add("$envId", SqliteType.Integer);
        var pName = cmd.Parameters.Add("$name", SqliteType.Text);
        var pValue = cmd.Parameters.Add("$value", SqliteType.Text);
        pEnvId.Value = environmentId;

        await cmd.PrepareAsync(cancellationToken).ConfigureAwait(false);

        foreach (var variable in variables)
        {
            pName.Value = variable.Name;
            pValue.Value = variable.Value;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}

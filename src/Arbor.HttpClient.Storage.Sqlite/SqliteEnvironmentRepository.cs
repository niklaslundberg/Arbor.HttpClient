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
        await EnableForeignKeysAsync(connection, cancellationToken).ConfigureAwait(false);

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
                value TEXT NOT NULL,
                is_enabled INTEGER NOT NULL DEFAULT 1
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await EnsureEnvironmentVariableEnabledColumnAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> SaveAsync(string name, IReadOnlyList<EnvironmentVariable> variables, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnableForeignKeysAsync(connection, cancellationToken).ConfigureAwait(false);

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
        await EnableForeignKeysAsync(connection, cancellationToken).ConfigureAwait(false);

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
        await EnableForeignKeysAsync(connection, cancellationToken).ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT e.id, e.name, v.name, v.value, v.is_enabled
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
                entry.Variables.Add(new EnvironmentVariable(reader.GetString(2), reader.GetString(3), reader.GetInt32(4) == 1));
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
        await EnableForeignKeysAsync(connection, cancellationToken).ConfigureAwait(false);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM environments WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", environmentId);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnableForeignKeysAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys=ON;";
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
            INSERT INTO environment_variables (environment_id, name, value, is_enabled)
            VALUES ($envId, $name, $value, $isEnabled);
            """;
        var pEnvId = cmd.Parameters.Add("$envId", SqliteType.Integer);
        var pName = cmd.Parameters.Add("$name", SqliteType.Text);
        var pValue = cmd.Parameters.Add("$value", SqliteType.Text);
        var pIsEnabled = cmd.Parameters.Add("$isEnabled", SqliteType.Integer);
        pEnvId.Value = environmentId;

        await cmd.PrepareAsync(cancellationToken).ConfigureAwait(false);

        foreach (var variable in variables)
        {
            pName.Value = variable.Name;
            pValue.Value = variable.Value;
            pIsEnabled.Value = variable.IsEnabled ? 1 : 0;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task EnsureEnvironmentVariableEnabledColumnAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var columnInfo = connection.CreateCommand();
        columnInfo.CommandText = "PRAGMA table_info(environment_variables);";

        var hasEnabledColumn = false;
        await using (var reader = await columnInfo.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (string.Equals(reader.GetString(1), "is_enabled", StringComparison.OrdinalIgnoreCase))
                {
                    hasEnabledColumn = true;
                    break;
                }
            }
        }

        if (hasEnabledColumn)
        {
            return;
        }

        await using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE environment_variables ADD COLUMN is_enabled INTEGER NOT NULL DEFAULT 1;";
        await alter.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

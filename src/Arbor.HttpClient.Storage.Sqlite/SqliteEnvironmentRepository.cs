using System.Globalization;
using Microsoft.Data.Sqlite;
using Arbor.HttpClient.Core.Environments;

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
        await EnsureEnvironmentColorColumnsAsync(connection, cancellationToken).ConfigureAwait(false);
        await EnsureEnvironmentVariableSensitiveColumnAsync(connection, cancellationToken).ConfigureAwait(false);
        await EnsureEnvironmentVariableExpiresAtColumnAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> SaveAsync(string name, IReadOnlyList<EnvironmentVariable> variables, string? accentColor = null, bool showWarningBanner = false, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnableForeignKeysAsync(connection, cancellationToken).ConfigureAwait(false);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using var insertEnv = connection.CreateCommand();
        insertEnv.Transaction = (SqliteTransaction)transaction;
        insertEnv.CommandText =
            """
            INSERT INTO environments (name, accent_color, show_warning_banner) VALUES ($name, $accentColor, $showWarningBanner);
            SELECT last_insert_rowid();
            """;
        insertEnv.Parameters.AddWithValue("$name", name);
        insertEnv.Parameters.AddWithValue("$accentColor", (object?)accentColor ?? DBNull.Value);
        insertEnv.Parameters.AddWithValue("$showWarningBanner", showWarningBanner ? 1 : 0);
        var envId = (long)(await insertEnv.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;

        await InsertVariablesAsync(connection, (SqliteTransaction)transaction, (int)envId, variables, cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return (int)envId;
    }

    public async Task UpdateAsync(int environmentId, string name, IReadOnlyList<EnvironmentVariable> variables, string? accentColor = null, bool showWarningBanner = false, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnableForeignKeysAsync(connection, cancellationToken).ConfigureAwait(false);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using var updateName = connection.CreateCommand();
        updateName.Transaction = (SqliteTransaction)transaction;
        updateName.CommandText = "UPDATE environments SET name = $name, accent_color = $accentColor, show_warning_banner = $showWarningBanner WHERE id = $id;";
        updateName.Parameters.AddWithValue("$name", name);
        updateName.Parameters.AddWithValue("$accentColor", (object?)accentColor ?? DBNull.Value);
        updateName.Parameters.AddWithValue("$showWarningBanner", showWarningBanner ? 1 : 0);
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
            SELECT e.id, e.name, e.accent_color, e.show_warning_banner, v.name, v.value, v.is_enabled, v.is_sensitive, v.expires_at_utc
            FROM environments e
            LEFT JOIN environment_variables v ON v.environment_id = e.id
            ORDER BY e.id, v.id;
            """;

        var environments = new Dictionary<int, (string Name, string? AccentColor, bool ShowWarningBanner, List<EnvironmentVariable> Variables)>();

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var id = reader.GetInt32(0);
            if (!environments.TryGetValue(id, out var entry))
            {
                var accentColor = reader.IsDBNull(2) ? null : reader.GetString(2);
                var showWarningBanner = reader.GetInt32(3) == 1;
                entry = (reader.GetString(1), accentColor, showWarningBanner, []);
                environments[id] = entry;
            }

            if (!reader.IsDBNull(4))
            {
                var isSensitive = !reader.IsDBNull(7) && reader.GetInt32(7) == 1;
                DateTimeOffset? expiresAtUtc = null;
                if (!reader.IsDBNull(8))
                {
                    if (DateTimeOffset.TryParse(reader.GetString(8), null, DateTimeStyles.RoundtripKind, out var parsed))
                    {
                        expiresAtUtc = parsed;
                    }
                }
                entry.Variables.Add(new EnvironmentVariable(reader.GetString(4), reader.GetString(5), reader.GetInt32(6) == 1, isSensitive, expiresAtUtc));
            }
        }

        return environments
            .Select(kvp => new RequestEnvironment(kvp.Key, kvp.Value.Name, kvp.Value.Variables, kvp.Value.AccentColor, kvp.Value.ShowWarningBanner))
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
            INSERT INTO environment_variables (environment_id, name, value, is_enabled, is_sensitive, expires_at_utc)
            VALUES ($envId, $name, $value, $isEnabled, $isSensitive, $expiresAtUtc);
            """;
        var pEnvId = cmd.Parameters.Add("$envId", SqliteType.Integer);
        var pName = cmd.Parameters.Add("$name", SqliteType.Text);
        var pValue = cmd.Parameters.Add("$value", SqliteType.Text);
        var pIsEnabled = cmd.Parameters.Add("$isEnabled", SqliteType.Integer);
        var pIsSensitive = cmd.Parameters.Add("$isSensitive", SqliteType.Integer);
        var pExpiresAtUtc = cmd.Parameters.Add("$expiresAtUtc", SqliteType.Text);
        pEnvId.Value = environmentId;

        await cmd.PrepareAsync(cancellationToken).ConfigureAwait(false);

        foreach (var variable in variables)
        {
            pName.Value = variable.Name;
            pValue.Value = variable.Value;
            pIsEnabled.Value = variable.IsEnabled ? 1 : 0;
            pIsSensitive.Value = variable.IsSensitive ? 1 : 0;
            pExpiresAtUtc.Value = variable.ExpiresAtUtc.HasValue
                ? (object)variable.ExpiresAtUtc.Value.ToString("O")
                : DBNull.Value;
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

    private static async Task EnsureEnvironmentColorColumnsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var columnInfo = connection.CreateCommand();
        columnInfo.CommandText = "PRAGMA table_info(environments);";

        var hasAccentColorColumn = false;
        var hasShowWarningBannerColumn = false;
        await using (var reader = await columnInfo.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var colName = reader.GetString(1);
                if (string.Equals(colName, "accent_color", StringComparison.OrdinalIgnoreCase))
                {
                    hasAccentColorColumn = true;
                }
                else if (string.Equals(colName, "show_warning_banner", StringComparison.OrdinalIgnoreCase))
                {
                    hasShowWarningBannerColumn = true;
                }
            }
        }

        if (!hasAccentColorColumn)
        {
            await using var alterAccent = connection.CreateCommand();
            alterAccent.CommandText = "ALTER TABLE environments ADD COLUMN accent_color TEXT;";
            await alterAccent.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        if (!hasShowWarningBannerColumn)
        {
            await using var alterBanner = connection.CreateCommand();
            alterBanner.CommandText = "ALTER TABLE environments ADD COLUMN show_warning_banner INTEGER NOT NULL DEFAULT 0;";
            await alterBanner.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task EnsureEnvironmentVariableSensitiveColumnAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var columnInfo = connection.CreateCommand();
        columnInfo.CommandText = "PRAGMA table_info(environment_variables);";

        var hasSensitiveColumn = false;
        await using (var reader = await columnInfo.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (string.Equals(reader.GetString(1), "is_sensitive", StringComparison.OrdinalIgnoreCase))
                {
                    hasSensitiveColumn = true;
                    break;
                }
            }
        }

        if (hasSensitiveColumn)
        {
            return;
        }

        await using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE environment_variables ADD COLUMN is_sensitive INTEGER NOT NULL DEFAULT 0;";
        await alter.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureEnvironmentVariableExpiresAtColumnAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var columnInfo = connection.CreateCommand();
        columnInfo.CommandText = "PRAGMA table_info(environment_variables);";

        var hasExpiresAtColumn = false;
        await using (var reader = await columnInfo.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (string.Equals(reader.GetString(1), "expires_at_utc", StringComparison.OrdinalIgnoreCase))
                {
                    hasExpiresAtColumn = true;
                    break;
                }
            }
        }

        if (hasExpiresAtColumn)
        {
            return;
        }

        await using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE environment_variables ADD COLUMN expires_at_utc TEXT;";
        await alter.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

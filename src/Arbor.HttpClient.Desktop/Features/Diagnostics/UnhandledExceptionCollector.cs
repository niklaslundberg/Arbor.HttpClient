using System.Text.Json;

namespace Arbor.HttpClient.Desktop.Features.Diagnostics;

/// <summary>
/// Collects unhandled exceptions and persists them to a local JSON file so they
/// can be reviewed and optionally reported by the end user.
/// </summary>
public sealed class UnhandledExceptionCollector
{
    private const int MaxEntries = 50;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _storagePath;
    private readonly List<UnhandledExceptionEntry> _entries = [];
    private readonly object _lock = new();

    public UnhandledExceptionCollector(string storagePath)
    {
        _storagePath = storagePath;
        Load();
    }

    /// <summary>Gets or sets whether new exceptions should be collected.</summary>
    public bool IsCollecting { get; set; }

    /// <summary>Adds an exception to the collection and persists it to disk.</summary>
    public void Add(Exception exception)
    {
        if (!IsCollecting || exception is null)
        {
            return;
        }

        var entry = new UnhandledExceptionEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
            Message = exception.Message,
            StackTrace = exception.StackTrace ?? string.Empty
        };

        lock (_lock)
        {
            _entries.Insert(0, entry);
            while (_entries.Count > MaxEntries)
            {
                _entries.RemoveAt(_entries.Count - 1);
            }
        }

        Persist();
    }

    /// <summary>Returns all collected entries, newest first.</summary>
    public IReadOnlyList<UnhandledExceptionEntry> GetAll()
    {
        lock (_lock)
        {
            return [.. _entries];
        }
    }

    /// <summary>Removes a single entry by ID and persists the change.</summary>
    public void Remove(string id)
    {
        lock (_lock)
        {
            var index = _entries.FindIndex(e => string.Equals(e.Id, id, StringComparison.Ordinal));
            if (index >= 0)
            {
                _entries.RemoveAt(index);
            }
        }

        Persist();
    }

    /// <summary>Removes all entries and persists the change.</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
        }

        Persist();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_storagePath))
            {
                return;
            }

            var json = File.ReadAllText(_storagePath);
            var loaded = JsonSerializer.Deserialize<List<UnhandledExceptionEntry>>(json, SerializerOptions);
            if (loaded is { Count: > 0 })
            {
                lock (_lock)
                {
                    _entries.AddRange(loaded.Take(MaxEntries));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Silently ignore load failures — the file may be corrupt or missing.
        }
    }

    private void Persist()
    {
        try
        {
            List<UnhandledExceptionEntry> snapshot;
            lock (_lock)
            {
                snapshot = [.. _entries];
            }

            var directory = Path.GetDirectoryName(_storagePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(snapshot, SerializerOptions);
            File.WriteAllText(_storagePath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Silently ignore persistence failures.
        }
    }
}

using System.Net.Http.Headers;
using System.Text.Json;
using Arbor.HttpClient.Desktop.Models;

namespace Arbor.HttpClient.Desktop.Services;

public sealed class ApplicationOptionsStore(string optionsPath)
{
    private static readonly HashSet<string> ValidHttpVersions =
    [
        "1.0",
        "1.1",
        "2.0",
        "3.0"
    ];

    private static readonly HashSet<string> ValidTlsVersions =
    [
        "SystemDefault",
        "Tls10",
        "Tls11",
        "Tls12",
        "Tls13"
    ];

    private static readonly HashSet<string> ValidThemes =
    [
        "System",
        "Dark",
        "Light"
    ];

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _optionsPath = optionsPath;

    public ApplicationOptions Load()
    {
        if (!File.Exists(_optionsPath))
        {
            return new ApplicationOptions();
        }

        var json = File.ReadAllText(_optionsPath);
        return DeserializeAndValidate(json);
    }

    public void Save(ApplicationOptions options)
    {
        Validate(options);
        Directory.CreateDirectory(Path.GetDirectoryName(_optionsPath)!);
        var json = JsonSerializer.Serialize(options, SerializerOptions);
        File.WriteAllText(_optionsPath, json);
    }

    public ApplicationOptions Import(string path)
    {
        var json = File.ReadAllText(path);
        return DeserializeAndValidate(json);
    }

    public void Export(string path, ApplicationOptions options)
    {
        Validate(options);
        var json = JsonSerializer.Serialize(options, SerializerOptions);
        File.WriteAllText(path, json);
    }

    public static void Validate(ApplicationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Http is null)
        {
            throw new InvalidDataException("HTTP options are required.");
        }

        if (options.Appearance is null)
        {
            throw new InvalidDataException("Appearance options are required.");
        }

        if (options.ScheduledJobs is null)
        {
            throw new InvalidDataException("Scheduled job options are required.");
        }

        if (options.Layouts is null)
        {
            throw new InvalidDataException("Layout options are required.");
        }

        if (!ValidHttpVersions.Contains(options.Http.HttpVersion))
        {
            throw new InvalidDataException($"Unsupported HTTP version '{options.Http.HttpVersion}'.");
        }

        if (!ValidTlsVersions.Contains(options.Http.TlsVersion))
        {
            throw new InvalidDataException($"Unsupported TLS version '{options.Http.TlsVersion}'.");
        }

        if (!MediaTypeHeaderValue.TryParse(options.Http.DefaultContentType, out _))
        {
            throw new InvalidDataException($"Invalid default content type '{options.Http.DefaultContentType}'.");
        }

        if (!Uri.TryCreate(options.Http.DefaultRequestUrl, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new InvalidDataException("Default request URL must be an absolute HTTP or HTTPS URL.");
        }

        if (!ValidThemes.Contains(options.Appearance.Theme))
        {
            throw new InvalidDataException($"Unsupported theme '{options.Appearance.Theme}'.");
        }

        if (options.Appearance.FontSize is < 10 or > 32)
        {
            throw new InvalidDataException("Font size must be in the range 10-32.");
        }

        if (string.IsNullOrWhiteSpace(options.Appearance.FontFamily))
        {
            throw new InvalidDataException("Font family is required.");
        }

        if (options.ScheduledJobs.DefaultIntervalSeconds < 1)
        {
            throw new InvalidDataException("Default scheduled job interval must be at least 1 second.");
        }

        ValidateLayoutSnapshot(options.Layouts.CurrentLayout);

        if (options.Layouts.SavedLayouts is null)
        {
            throw new InvalidDataException("Saved layouts are required.");
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var namedLayout in options.Layouts.SavedLayouts)
        {
            if (namedLayout is null)
            {
                throw new InvalidDataException("Saved layout cannot be null.");
            }

            if (string.IsNullOrWhiteSpace(namedLayout.Name))
            {
                throw new InvalidDataException("Saved layout name cannot be empty.");
            }

            if (!names.Add(namedLayout.Name))
            {
                throw new InvalidDataException($"Duplicate saved layout name '{namedLayout.Name}'.");
            }

            if (namedLayout.Layout is null)
            {
                throw new InvalidDataException($"Saved layout '{namedLayout.Name}' is missing layout data.");
            }

            ValidateLayoutSnapshot(namedLayout.Layout);
        }
    }

    private static void ValidateLayoutSnapshot(DockLayoutSnapshot? layout)
    {
        if (layout is null)
        {
            return;
        }

        if (layout.LeftToolProportion <= 0 || layout.DocumentProportion <= 0)
        {
            throw new InvalidDataException("Layout proportions must be positive values.");
        }
    }

    private static ApplicationOptions DeserializeAndValidate(string json)
    {
        ApplicationOptions? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<ApplicationOptions>(json, SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Invalid options JSON format.", exception);
        }

        if (parsed is null)
        {
            throw new InvalidDataException("Options JSON did not contain a valid options object.");
        }

        Validate(parsed);
        return parsed;
    }
}

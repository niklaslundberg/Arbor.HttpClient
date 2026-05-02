using System.Net.Http.Headers;
using System.Text.Json;
using Arbor.HttpClient.Desktop.Features.Layout;
using Arbor.HttpClient.Desktop.Features.Options;

namespace Arbor.HttpClient.Desktop.Features.Options;

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

    private static readonly HashSet<string> ValidDockNodeTypes =
    [
        "Root",
        "Proportional",
        "Splitter",
        "Tool",
        "Document"
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

        if (options.Diagnostics is null)
        {
            throw new InvalidDataException("Diagnostics options are required.");
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

        if (layout.LeftToolDockableOrder is null)
        {
            throw new InvalidDataException("Layout dockable order collections are required.");
        }

        if (layout.LeftToolDockableOrder.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException("Layout dockable order entries cannot be empty.");
        }

        // Validate dock proportions: 0 means "use default"; non-zero must be positive.
        if (layout.RequestDockProportion < 0)
        {
            throw new InvalidDataException("RequestDockProportion must be zero (default) or a positive value.");
        }

        if (layout.ResponseDockProportion < 0)
        {
            throw new InvalidDataException("ResponseDockProportion must be zero (default) or a positive value.");
        }

        // Validate window geometry: 0 means "not saved / use default"; non-zero must be a sensible minimum.
        if (layout.WindowWidth < 0 || (layout.WindowWidth > 0 && layout.WindowWidth < 100))
        {
            throw new InvalidDataException("WindowWidth must be zero (default) or at least 100 pixels.");
        }

        if (layout.WindowHeight < 0 || (layout.WindowHeight > 0 && layout.WindowHeight < 100))
        {
            throw new InvalidDataException("WindowHeight must be zero (default) or at least 100 pixels.");
        }

        if (layout.FloatingWindows is null)
        {
            throw new InvalidDataException("Layout floating windows collection is required.");
        }

        foreach (var fw in layout.FloatingWindows)
        {
            if (fw is null)
            {
                throw new InvalidDataException("Floating window snapshot cannot be null.");
            }

            if (fw.Width <= 0 || fw.Height <= 0)
            {
                throw new InvalidDataException("Floating window dimensions must be positive values.");
            }

            if (fw.DockableIds is null)
            {
                throw new InvalidDataException("Floating window dockable IDs collection is required.");
            }
        }

        if (layout.DockTree is { } dockTree)
        {
            ValidateDockTreeNode(dockTree, "DockTree");
        }
    }

    private static void ValidateDockTreeNode(DockTreeNode node, string path)
    {
        if (!ValidDockNodeTypes.Contains(node.Type))
        {
            throw new InvalidDataException($"{path}: unknown dock node type '{node.Type}'.");
        }

        if (node.Children is null)
        {
            throw new InvalidDataException($"{path}: Children collection is required.");
        }

        if (node.ContentIds is null)
        {
            throw new InvalidDataException($"{path}: ContentIds collection is required.");
        }

        if (node.Proportion < 0)
        {
            throw new InvalidDataException($"{path}: Proportion must be zero or a positive value.");
        }

        foreach (var child in node.Children)
        {
            ValidateDockTreeNode(child, $"{path} > {child.Id ?? child.Type}");
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

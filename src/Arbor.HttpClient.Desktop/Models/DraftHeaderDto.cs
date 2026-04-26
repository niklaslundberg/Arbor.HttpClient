namespace Arbor.HttpClient.Desktop.Models;

/// <summary>Serialisation model for a single request header entry inside a <see cref="DraftState"/>.</summary>
public sealed class DraftHeaderDto
{
    public string Name { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public bool IsEnabled { get; init; } = true;
}

namespace Arbor.HttpClient.Desktop.Models;

public sealed class AppearanceOptions
{
    public string Theme { get; init; } = "System";

    public double FontSize { get; init; } = 13d;

    public string FontFamily { get; init; } = "Cascadia Code,Consolas,Menlo,monospace";
}

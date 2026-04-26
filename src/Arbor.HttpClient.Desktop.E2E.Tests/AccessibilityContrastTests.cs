using Arbor.HttpClient.Desktop.Features.Main;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// Verifies that the color pairs defined in App.axaml meet WCAG 2.1 AA contrast requirements
/// (≥ 4.5:1 for normal text; ≥ 3:1 for large/bold text).
/// Method labels (bold) qualify as large text, so a 4.5:1 target is used here for additional safety.
/// Reference: https://www.w3.org/WAI/standards-guidelines/wcag/
/// </summary>
public class AccessibilityContrastTests
{
    // ─── Dark-theme backgrounds ──────────────────────────────────────────────

    private const string DarkSurface = "#1E1E1E";
    private const string DarkPanel = "#12212F";

    // SecondaryPanelBackgroundBrush — used by the layout-selector panel and the
    // environment panel in MainWindow.axaml.  TextBlock controls on these panels
    // render with Avalonia Fluent's dark-mode text (near-white).
    private const string DarkSecondaryPanel = "#1A1A2E";

    // ─── Dark-theme foreground (Avalonia Fluent default text in dark mode) ───

    // Avalonia Fluent dark theme renders TextBlock with SystemControlForegroundBaseHighBrush ≈ #FFFFFF.
    private const string DarkDefaultText = "#FFFFFF";

    // ─── Dark-theme method foreground colors ─────────────────────────────────

    private const string DarkMethodGet = "#4FC3F7";
    private const string DarkMethodPost = "#6FCF97";
    private const string DarkMethodPut = "#FFB74D";
    private const string DarkMethodPatch = "#CE93D8";
    private const string DarkMethodDelete = "#EF5350";
    private const string DarkMethodFallback = "#F5F5F5";
    private const string DarkError = "#FF6347";

    // ─── Dark-theme variable token colors ────────────────────────────────────

    // VariableBracketBrush (amber): same value as MethodPutBrush – already verified above.
    private const string DarkVariableBracket = "#FFB74D";

    // VariableNameBrush (violet): same value as MethodPatchBrush – already verified above.
    private const string DarkVariableName = "#CE93D8";

    // ─── Light-theme backgrounds ─────────────────────────────────────────────

    private const string LightSurface = "#FFFFFF";
    private const string LightPanel = "#F3F6FA";

    // SecondaryPanelBackgroundBrush — light variant.  TextBlock controls render
    // with Avalonia Fluent's light-mode text (near-black).
    private const string LightSecondaryPanel = "#F5F8FC";

    // ─── Light-theme foreground (Avalonia Fluent default text in light mode) ─

    // Avalonia Fluent light theme renders TextBlock with SystemControlForegroundBaseHighBrush ≈ #000000.
    private const string LightDefaultText = "#000000";

    // ─── Light-theme method foreground colors ────────────────────────────────

    private const string LightMethodGet = "#0065BD";
    private const string LightMethodPost = "#107C10";
    private const string LightMethodPut = "#875F09";
    private const string LightMethodPatch = "#744DA9";
    private const string LightMethodDelete = "#C50F1F";
    private const string LightMethodFallback = "#1F2328";
    private const string LightError = "#B42318";

    // ─── Light-theme variable token colors ───────────────────────────────────

    // VariableBracketBrush (amber): same value as MethodPutBrush – already verified above.
    private const string LightVariableBracket = "#875F09";

    // VariableNameBrush (violet): same value as MethodPatchBrush – already verified above.
    private const string LightVariableName = "#744DA9";

    // WCAG AA minimum for normal text (≥ 4.5:1).
    // Method labels are bold, so 3:1 (large-text AA) would suffice, but 4.5:1 is used for safety.
    private const double MinContrastRatio = 4.5;

    // ─── Dark theme ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(DarkMethodGet, DarkSurface, "GET / dark surface")]
    [InlineData(DarkMethodGet, DarkPanel, "GET / dark panel")]
    [InlineData(DarkMethodPost, DarkSurface, "POST / dark surface")]
    [InlineData(DarkMethodPost, DarkPanel, "POST / dark panel")]
    [InlineData(DarkMethodPut, DarkSurface, "PUT / dark surface")]
    [InlineData(DarkMethodPut, DarkPanel, "PUT / dark panel")]
    [InlineData(DarkMethodPatch, DarkSurface, "PATCH / dark surface")]
    [InlineData(DarkMethodPatch, DarkPanel, "PATCH / dark panel")]
    [InlineData(DarkMethodDelete, DarkSurface, "DELETE / dark surface")]
    [InlineData(DarkMethodDelete, DarkPanel, "DELETE / dark panel")]
    [InlineData(DarkMethodFallback, DarkSurface, "Fallback / dark surface")]
    [InlineData(DarkMethodFallback, DarkPanel, "Fallback / dark panel")]
    [InlineData(DarkError, DarkSurface, "Error / dark surface")]
    [InlineData(DarkError, DarkPanel, "Error / dark panel")]
    [InlineData(DarkDefaultText, DarkSecondaryPanel, "Default text / dark secondary panel")]
    [InlineData(DarkVariableBracket, DarkSurface, "Variable bracket / dark surface")]
    [InlineData(DarkVariableBracket, DarkPanel, "Variable bracket / dark panel")]
    [InlineData(DarkVariableName, DarkSurface, "Variable name / dark surface")]
    [InlineData(DarkVariableName, DarkPanel, "Variable name / dark panel")]
    // Response-status brushes (same palette values as above, re-verified explicitly).
    [InlineData("#6FCF97", DarkSurface, "StatusSuccessBrush / dark surface")]
    [InlineData("#6FCF97", DarkPanel, "StatusSuccessBrush / dark panel")]
    [InlineData("#4FC3F7", DarkSurface, "StatusRedirectBrush / dark surface")]
    [InlineData("#4FC3F7", DarkPanel, "StatusRedirectBrush / dark panel")]
    [InlineData("#FFB74D", DarkSurface, "StatusClientErrorBrush / dark surface")]
    [InlineData("#FFB74D", DarkPanel, "StatusClientErrorBrush / dark panel")]
    [InlineData("#FFB74D", DarkSurface, "WarningBrush / dark surface")]
    [InlineData("#FFB74D", DarkPanel, "WarningBrush / dark panel")]
    [InlineData("#EF5350", DarkSurface, "StatusServerErrorBrush / dark surface")]
    [InlineData("#EF5350", DarkPanel, "StatusServerErrorBrush / dark panel")]
    public void DarkTheme_ColorPair_MeetsWcagAA(string foreground, string background, string label)
    {
        var ratio = ContrastRatio(foreground, background);
        ratio.Should().BeGreaterThanOrEqualTo(MinContrastRatio,
            $"WCAG AA requires ≥{MinContrastRatio}:1 contrast for '{label}' (actual: {ratio:F2}:1)");
    }

    // ─── Light theme ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData(LightMethodGet, LightSurface, "GET / light surface")]
    [InlineData(LightMethodGet, LightPanel, "GET / light panel")]
    [InlineData(LightMethodPost, LightSurface, "POST / light surface")]
    [InlineData(LightMethodPost, LightPanel, "POST / light panel")]
    [InlineData(LightMethodPut, LightSurface, "PUT / light surface")]
    [InlineData(LightMethodPut, LightPanel, "PUT / light panel")]
    [InlineData(LightMethodPatch, LightSurface, "PATCH / light surface")]
    [InlineData(LightMethodPatch, LightPanel, "PATCH / light panel")]
    [InlineData(LightMethodDelete, LightSurface, "DELETE / light surface")]
    [InlineData(LightMethodDelete, LightPanel, "DELETE / light panel")]
    [InlineData(LightMethodFallback, LightSurface, "Fallback / light surface")]
    [InlineData(LightMethodFallback, LightPanel, "Fallback / light panel")]
    [InlineData(LightError, LightSurface, "Error / light surface")]
    [InlineData(LightError, LightPanel, "Error / light panel")]
    [InlineData(LightDefaultText, LightSecondaryPanel, "Default text / light secondary panel")]
    [InlineData(LightVariableBracket, LightSurface, "Variable bracket / light surface")]
    [InlineData(LightVariableBracket, LightPanel, "Variable bracket / light panel")]
    [InlineData(LightVariableName, LightSurface, "Variable name / light surface")]
    [InlineData(LightVariableName, LightPanel, "Variable name / light panel")]
    // Response-status brushes (same palette values as above, re-verified explicitly).
    [InlineData("#107C10", LightSurface, "StatusSuccessBrush / light surface")]
    [InlineData("#107C10", LightPanel, "StatusSuccessBrush / light panel")]
    [InlineData("#0065BD", LightSurface, "StatusRedirectBrush / light surface")]
    [InlineData("#0065BD", LightPanel, "StatusRedirectBrush / light panel")]
    [InlineData("#875F09", LightSurface, "StatusClientErrorBrush / light surface")]
    [InlineData("#875F09", LightPanel, "StatusClientErrorBrush / light panel")]
    [InlineData("#875F09", LightSurface, "WarningBrush / light surface")]
    [InlineData("#875F09", LightPanel, "WarningBrush / light panel")]
    [InlineData("#C50F1F", LightSurface, "StatusServerErrorBrush / light surface")]
    [InlineData("#C50F1F", LightPanel, "StatusServerErrorBrush / light panel")]
    public void LightTheme_ColorPair_MeetsWcagAA(string foreground, string background, string label)
    {
        var ratio = ContrastRatio(foreground, background);
        ratio.Should().BeGreaterThanOrEqualTo(MinContrastRatio,
            $"WCAG AA requires ≥{MinContrastRatio}:1 contrast for '{label}' (actual: {ratio:F2}:1)");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Calculates the WCAG 2.1 contrast ratio between two hex colors.
    /// </summary>
    private static double ContrastRatio(string hexForeground, string hexBackground)
    {
        var l1 = RelativeLuminance(hexForeground);
        var l2 = RelativeLuminance(hexBackground);
        var lighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);
        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>
    /// Computes WCAG 2.1 relative luminance for a hex color string (e.g. "#RRGGBB").
    /// </summary>
    private static double RelativeLuminance(string hex)
    {
        var color = hex.TrimStart('#');
        var r = int.Parse(color[..2], System.Globalization.NumberStyles.HexNumber) / 255.0;
        var g = int.Parse(color[2..4], System.Globalization.NumberStyles.HexNumber) / 255.0;
        var b = int.Parse(color[4..6], System.Globalization.NumberStyles.HexNumber) / 255.0;
        return 0.2126 * Linearize(r) + 0.7152 * Linearize(g) + 0.0722 * Linearize(b);
    }

    private static double Linearize(double c) =>
        c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
}

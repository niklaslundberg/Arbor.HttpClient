using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using CommunityToolkit.Mvvm.Input;
using Arbor.HttpClient.Desktop.Shared;

namespace Arbor.HttpClient.Desktop.Features.About;

/// <summary>
/// Provides version, build, and attribution information displayed in the About window.
/// </summary>
public sealed partial class AboutWindowViewModel : ViewModelBase
{
    public AboutWindowViewModel()
    {
        var assembly = typeof(AboutWindowViewModel).Assembly;
        var informationalVersion =
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? string.Empty;

        (AppVersion, GitHash) = ParseInformationalVersion(informationalVersion);
    }

    private static (string version, string hash) ParseInformationalVersion(string informationalVersion)
    {
        var plusIndex = informationalVersion.IndexOf('+', StringComparison.Ordinal);
        return plusIndex >= 0
            ? (informationalVersion[..plusIndex], informationalVersion[(plusIndex + 1)..])
            : (informationalVersion, string.Empty);
    }

    /// <summary>Application version (e.g. "1.0.0").</summary>
    public string AppVersion { get; }

    /// <summary>Short git commit hash embedded at build time (e.g. "27e3f57"), or empty if unavailable.</summary>
    public string GitHash { get; }

    /// <summary>Human-readable build label combining version and hash.</summary>
    public string BuildLabel => string.IsNullOrEmpty(GitHash)
        ? $"Version {AppVersion}"
        : $"Version {AppVersion} ({GitHash})";

    /// <summary>Copyright statement.</summary>
    public string Copyright => "Copyright © 2026 Niklas Lundberg";

    /// <summary>SPDX license identifier.</summary>
    public string License => "MIT License";

    /// <summary>GitHub repository URL.</summary>
    public string GitHubUrl => "https://github.com/niklaslundberg/Arbor.HttpClient";

    [RelayCommand]
    private void OpenGitHub()
    {
        try
        {
            Process.Start(new ProcessStartInfo(GitHubUrl) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException or PlatformNotSupportedException)
        {
            // No default browser or unsupported platform — silently ignore.
        }
    }

    /// <summary>Full MIT license text for display in the About window.</summary>
    public string LicenseText =>
        """
        Permission is hereby granted, free of charge, to any person obtaining a copy
        of this software and associated documentation files (the "Software"), to deal
        in the Software without restriction, including without limitation the rights
        to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
        copies of the Software, and to permit persons to whom the Software is
        furnished to do so, subject to the following conditions:

        The above copyright notice and this permission notice shall be included in
        all copies or substantial portions of the Software.

        THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
        IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
        FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
        AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
        LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
        OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
        THE SOFTWARE.
        """;
}

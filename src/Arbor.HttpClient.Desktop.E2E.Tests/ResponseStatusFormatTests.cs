using Arbor.HttpClient.Desktop.Converters;
using Arbor.HttpClient.Desktop.ViewModels;
using AwesomeAssertions;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// Pure in-process tests for the UX helpers introduced to surface response
/// metadata (time / size / color-coded status) to the Response view. These do
/// not require the Avalonia headless session.
/// </summary>
public class ResponseStatusFormatTests
{
    [Theory]
    [InlineData(0, "0 ms")]
    [InlineData(12.4, "12 ms")]
    [InlineData(999, "999 ms")]
    [InlineData(1000, "1.00 s")]
    [InlineData(1234, "1.23 s")]
    [InlineData(59_000, "59.00 s")]
    [InlineData(60_000, "1 min 0 s")]
    [InlineData(75_500, "1 min 15 s")]
    public void FormatElapsedMilliseconds_ShouldProduceHumanReadableOutput(double milliseconds, string expected)
    {
        MainWindowViewModel.FormatElapsedMilliseconds(milliseconds).Should().Be(expected);
    }

    [Fact]
    public void FormatElapsedMilliseconds_ShouldClampNegativeInput()
    {
        MainWindowViewModel.FormatElapsedMilliseconds(-5).Should().Be("0 ms");
    }

    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(1L, "1 B")]
    [InlineData(1023L, "1023 B")]
    [InlineData(1024L, "1 KB")]
    [InlineData(1536L, "1.5 KB")]
    [InlineData(1024L * 1024, "1 MB")]
    [InlineData((long)(1024 * 1024 * 2.5), "2.5 MB")]
    [InlineData(1024L * 1024 * 1024, "1 GB")]
    public void FormatByteSize_ShouldProduceHumanReadableOutput(long bytes, string expected)
    {
        MainWindowViewModel.FormatByteSize(bytes).Should().Be(expected);
    }

    [Fact]
    public void FormatByteSize_ShouldClampNegativeInput()
    {
        MainWindowViewModel.FormatByteSize(-42).Should().Be("0 B");
    }

    [Theory]
    [InlineData(100)]   // 1xx informational
    [InlineData(200)]   // 2xx success
    [InlineData(302)]   // 3xx redirect
    [InlineData(404)]   // 4xx client error
    [InlineData(500)]   // 5xx server error
    [InlineData(0)]     // no response yet
    [InlineData(999)]   // out of normal range
    public void StatusCodeToColorConverter_ShouldReturnABrush_ForEveryStatusFamily(int statusCode)
    {
        var brush = StatusCodeToColorConverter.Instance.Convert(
            statusCode,
            typeof(Avalonia.Media.IBrush),
            parameter: null,
            culture: System.Globalization.CultureInfo.InvariantCulture);

        brush.Should().BeAssignableTo<Avalonia.Media.IBrush>();
    }
}

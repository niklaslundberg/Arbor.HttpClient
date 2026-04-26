using Arbor.HttpClient.Desktop.Features.Diagnostics;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

public class UnhandledExceptionCollectorTests
{
    [Fact]
    public void Add_WhenNotCollecting_DoesNotStoreEntry()
    {
        var path = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}", "exceptions.json");
        var collector = new UnhandledExceptionCollector(path) { IsCollecting = false };

        collector.Add(new InvalidOperationException("test"));

        collector.GetAll().Should().BeEmpty();
    }

    [Fact]
    public void Add_WhenCollecting_StoresEntry()
    {
        var path = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}", "exceptions.json");
        var collector = new UnhandledExceptionCollector(path) { IsCollecting = true };

        collector.Add(new InvalidOperationException("boom"));

        var all = collector.GetAll();
        all.Should().ContainSingle();
        all[0].ExceptionType.Should().Be("System.InvalidOperationException");
        all[0].Message.Should().Be("boom");
        all[0].Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Add_NullException_IsIgnored()
    {
        var path = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}", "exceptions.json");
        var collector = new UnhandledExceptionCollector(path) { IsCollecting = true };

        collector.Add(null!);

        collector.GetAll().Should().BeEmpty();
    }

    [Fact]
    public void Remove_ExistingId_RemovesEntry()
    {
        var path = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}", "exceptions.json");
        var collector = new UnhandledExceptionCollector(path) { IsCollecting = true };
        collector.Add(new Exception("one"));
        var id = collector.GetAll()[0].Id;

        collector.Remove(id);

        collector.GetAll().Should().BeEmpty();
    }

    [Fact]
    public void Remove_UnknownId_DoesNothing()
    {
        var path = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}", "exceptions.json");
        var collector = new UnhandledExceptionCollector(path) { IsCollecting = true };
        collector.Add(new Exception("keep"));

        collector.Remove("no-such-id");

        collector.GetAll().Should().ContainSingle();
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var path = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}", "exceptions.json");
        var collector = new UnhandledExceptionCollector(path) { IsCollecting = true };
        collector.Add(new Exception("a"));
        collector.Add(new Exception("b"));

        collector.Clear();

        collector.GetAll().Should().BeEmpty();
    }

    [Fact]
    public void Entries_PersistedAndReloadedAcrossInstances()
    {
        var dir = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}");
        var path = Path.Join(dir, "exceptions.json");
        var collector1 = new UnhandledExceptionCollector(path) { IsCollecting = true };
        collector1.Add(new ArgumentException("persisted"));

        var collector2 = new UnhandledExceptionCollector(path);
        var all = collector2.GetAll();
        all.Should().ContainSingle();
        all[0].Message.Should().Be("persisted");
    }

    [Fact]
    public void GetAll_ReturnsNewestEntryFirst()
    {
        var path = Path.Join(Path.GetTempPath(), $"{Guid.NewGuid():N}", "exceptions.json");
        var collector = new UnhandledExceptionCollector(path) { IsCollecting = true };
        collector.Add(new Exception("first"));
        collector.Add(new Exception("second"));

        var all = collector.GetAll();
        all[0].Message.Should().Be("second");
        all[1].Message.Should().Be("first");
    }
}

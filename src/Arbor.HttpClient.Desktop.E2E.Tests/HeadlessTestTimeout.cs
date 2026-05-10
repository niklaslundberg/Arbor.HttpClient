using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Headless;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

internal static class HeadlessTestTimeout
{
    private static readonly TimeSpan MaxDuration = TimeSpan.FromSeconds(10);

    internal static async Task DispatchAsync(HeadlessUnitTestSession session, Func<Task<bool>> action)
    {
        using var cancellationTokenSource = new CancellationTokenSource(MaxDuration);
        await session.Dispatch(action, cancellationTokenSource.Token);
    }

    internal static async Task DispatchAsync(HeadlessUnitTestSession session, Func<bool> action)
    {
        using var cancellationTokenSource = new CancellationTokenSource(MaxDuration);
        await session.Dispatch(action, cancellationTokenSource.Token);
    }
}

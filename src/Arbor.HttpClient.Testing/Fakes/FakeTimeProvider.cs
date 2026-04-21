using System;

namespace Arbor.HttpClient.Testing.Fakes;

/// <summary>
/// Fake time provider for testing time-dependent code.
/// Returns a fixed DateTimeOffset for GetUtcNow().
/// </summary>
public sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    /// <summary>
    /// Advances the current time by the specified amount.
    /// </summary>
    public void Advance(TimeSpan duration) => _now += duration;

    /// <summary>
    /// Sets the current time to a specific value.
    /// </summary>
    public void SetTime(DateTimeOffset time) => _now = time;
}

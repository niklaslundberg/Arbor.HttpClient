namespace Arbor.HttpClient.Core.Integration.Tests;

/// <summary>
/// Defines the xUnit test collection that shares a single <see cref="KestrelServerFixture"/>
/// instance across all integration test classes.  Both <see cref="WebSocketServiceIntegrationTests"/>
/// and <see cref="SseServiceIntegrationTests"/> are members of this collection.
/// </summary>
[CollectionDefinition("KestrelServer")]
public sealed class KestrelServerCollection : ICollectionFixture<KestrelServerFixture>
{
    // No members – this class acts solely as a marker for xUnit's collection mechanism.
}

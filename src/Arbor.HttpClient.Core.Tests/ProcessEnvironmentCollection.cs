
namespace Arbor.HttpClient.Core.Tests;

/// <summary>
/// All tests that mutate the process environment (via <see cref="Environment.SetEnvironmentVariable"/>)
/// must belong to this collection so xUnit runs them sequentially and avoids race conditions
/// with other tests that read the same environment keys.
/// </summary>
[CollectionDefinition("ProcessEnvironment", DisableParallelization = true)]
public sealed class ProcessEnvironmentCollection;

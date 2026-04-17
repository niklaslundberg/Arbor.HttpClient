using Xunit;

namespace Arbor.HttpClient.Desktop.E2E.Tests;

/// <summary>
/// All tests that use <c>HeadlessUnitTestSession</c> must belong to this collection
/// so xUnit runs them sequentially (Avalonia's headless platform uses shared static state).
/// </summary>
[CollectionDefinition("HeadlessAvalonia", DisableParallelization = true)]
public sealed class HeadlessAvaloniaCollection;

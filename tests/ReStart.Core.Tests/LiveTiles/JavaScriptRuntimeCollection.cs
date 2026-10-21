namespace ReStart.Core.Tests;

// The exhaustive in-process sample matrix must not starve cold worker startup
// on a two-core runner. Concurrent-worker deadline tests still run four workers.
[CollectionDefinition("JavaScript runtime", DisableParallelization = true)]
public sealed class JavaScriptRuntimeCollection;

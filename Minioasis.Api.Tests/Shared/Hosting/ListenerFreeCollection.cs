using Xunit;

namespace Minioasis.Api.Tests.Shared.Hosting;

/// <summary>
/// Tests that must run when no other test host is alive, so that no other host's request activity listener can make
/// them pass. xUnit runs a collection that disables parallelization alone, after the parallel ones.
/// </summary>
[CollectionDefinition(nameof(ListenerFreeCollection), DisableParallelization = true)]
public sealed class ListenerFreeCollection
{
}

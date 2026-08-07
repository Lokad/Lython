using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

public sealed class PyModulePrimitiveTests
{
    [Fact]
    public async Task CachedMembersPublishOneIdentityAcrossConcurrentEngines()
    {
        var module = new FreshMemberModule();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = Enumerable.Range(0, 64)
            .Select(async _ =>
            {
                await start.Task;
                Assert.True(module.TryGetCachedMember("callable", out var value));
                return value;
            })
            .ToArray();

        start.SetResult();
        var values = await Task.WhenAll(reads);

        Assert.All(values, value => Assert.Same(values[0], value));
    }

    private sealed class FreshMemberModule : PyModule
    {
        public FreshMemberModule() : base("concurrent_test") { }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name == "callable")
            {
                value = new object();
                return true;
            }

            value = null;
            return false;
        }
    }
}

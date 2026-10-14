using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class PartialConstructionRetryTests : LifetimeScenario
{
    [Fact]
    public async Task PartialListFailuresDoNotAccumulatePerAttempt()
    {
        const string code = """
            def boom():
                raise ValueError("x")
            ok = 0
            for i in range(1):
                try:
                    v = [0] * 20000 + [boom()]
                except ValueError:
                    ok += 1
            return ok
            """;
        await AssertBatchCompletes(code, new System.Numerics.BigInteger(1), 8388608);
    }
}

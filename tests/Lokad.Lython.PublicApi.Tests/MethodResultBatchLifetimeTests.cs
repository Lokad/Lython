using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class MethodResultBatchLifetimeTests : LifetimeScenario
{
    [Fact]
    public async Task UpperResultBatchesReturnSharedAccounting()
    {
        await AssertBatchCompletes("b = 'y' * 2048\nfor i in range(2):\n    y = b.upper()\nreturn len(y)\n", new System.Numerics.BigInteger(2048), 16384);
    }
}

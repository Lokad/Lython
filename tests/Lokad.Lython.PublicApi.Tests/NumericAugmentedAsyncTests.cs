using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NumericAugmentedAsyncTests
{
    [Theory]
    [InlineData("True")]
    [InlineData("1.5")]
    public async Task ReflectedSubtractionAwaitsHost(string left)
        => await CheckReflected(left, "-", "sub");

    public static IEnumerable<object[]> Operators()
    {
        foreach (var pair in AugmentedProtocolFallbackTests.Operators())
            foreach (var left in new[] { "True", "1.5", "1+2j", "decimal.Decimal('1.5')" })
                yield return [left, pair[0], pair[1]];
    }

    [Theory]
    [MemberData(nameof(Operators))]
    public async Task BuiltinNumericScalarsAwaitReflectedMethods(string left, string token, string method)
        => await CheckReflected(left, token, method);

    private static async Task CheckReflected(string left, string token, string method,
        object? global = null, bool decline = false)
    {
        var source = $$"""
import decimal
class Right:
    def __r{{method}}__(self,other):
        with open('events.txt','a') as target: target.write(str(other)+'\n')
        return {{(decline ? "NotImplemented" : "42")}}
value={{left}}
try:
    value{{token}}=Right()
except TypeError as error:
    print(str(error))
    return 'declined'
return value
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        var options = new LythonRunOptions { Globals = global is null ? null : new Dictionary<string, object?> { ["seed"] = global } };
        using (var result = await script.RunAsync(host, options))
        {
            Assert.True(result.Success, result.Failure?.Message);
            if (decline)
            {
                Assert.Equal("declined", result.ReturnValue);
                Assert.Contains($"for {token}=:", result.StandardOutput);
            }
            else Assert.Equal(new System.Numerics.BigInteger(42), result.ReturnValue);
            Assert.True(host.CompletedAsynchronously > 0);
            Assert.Single(host.ReadText("/events.txt").Split('\n', StringSplitOptions.RemoveEmptyEntries));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("True")]
    [InlineData("1.5")]
    [InlineData("1+2j")]
    [InlineData("decimal.Decimal('1.5')")]
    public async Task DeclinedReflectedMethodsKeepAugmentedErrors(string left)
        => await CheckReflected(left, "-", "sub", decline: true);

    [Theory]
    [MemberData(nameof(AugmentedProtocolFallbackTests.Operators), MemberType = typeof(AugmentedProtocolFallbackTests))]
    public async Task DecliningComplexOperatorsKeepThePythonErrorToken(string token, string method)
    {
        var source = $$"""
class Right:
    def __r{{method}}__(self,other): return NotImplemented
value=1+2j
try:
    value{{token}}=Right()
except TypeError as error:
    print(str(error))
""";
        using var script = new LythonEngine().Compile(source);
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal($"unsupported operand type(s) for {token}=: 'complex' and 'Right'\n", result.StandardOutput);
        }
    }

    [Theory]
    [InlineData("sbyte")]
    [InlineData("byte")]
    [InlineData("short")]
    [InlineData("ushort")]
    [InlineData("int")]
    [InlineData("uint")]
    [InlineData("long")]
    [InlineData("ulong")]
    public async Task HostIntegralRepresentationsAwaitReflectedMethods(string kind)
    {
        object value = kind switch
        {
            "sbyte" => (sbyte)3, "byte" => (byte)3, "short" => (short)3,
            "ushort" => (ushort)3, "int" => 3, "uint" => (uint)3,
            "long" => (long)3, "ulong" => (ulong)3,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        await CheckReflected("seed", "-", "sub", value);
    }
}

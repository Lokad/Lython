using System.Numerics;
using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NumericIngressContractTests
{
    private static (string Source, string Output) Reference(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "NumericIngress", "host-scalars.json");
        var row = JsonSerializer.Deserialize<JsonElement[]>(File.ReadAllText(path))!.Single(row => row.GetProperty("name").GetString() == name);
        return (row.GetProperty("source").GetString()!, row.GetProperty("output").GetString()!);
    }

    public static IEnumerable<object[]> IntegralValues()
    {
        foreach (var value in new object[] { (sbyte)2, (byte)2, (short)2, (ushort)2, 2, 2u, 2L, 2UL, new BigInteger(2) })
            yield return [value.GetType().Name, value];
    }

    [Theory, MemberData(nameof(IntegralValues))]
    public async Task IntegralWidthsUseOneGuestNumericContract(string name, object value)
    {
        var reference = Reference("integral");
        using var script = new LythonEngine().Compile(reference.Source + "\nreturn x");
        Assert.True(script.IsValid, name);
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            var options = new LythonRunOptions { MemoryBudget = budget, Globals = new Dictionary<string, object?> { ["x"] = value } };
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(reference.Output, result.StandardOutput);
                Assert.Empty(result.StandardError);
                Assert.IsType(value.GetType(), result.ReturnValue);
                Assert.Equal(value, result.ReturnValue);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    public static IEnumerable<object[]> CanonicalValues()
    {
        yield return ["single", 2f, "float-two"];
        yield return ["double", 2d, "float-two"];
        yield return ["decimal", 2m, "decimal-two"];
        yield return ["single-zero", 0f, "float-zero"];
        yield return ["single-negative-zero", BitConverter.Int32BitsToSingle(int.MinValue), "float-zero"];
        yield return ["double-zero", 0d, "float-zero"];
        yield return ["decimal-zero", 0m, "decimal-zero"];
        yield return ["decimal-negative-zero", new decimal(0, 0, 0, true, 2), "decimal-negative-zero"];
    }

    [Theory, MemberData(nameof(CanonicalValues))]
    public async Task FloatingAndDecimalInputsHaveSupportedGuestRepresentations(string name, object value, string referenceName)
    {
        var reference = Reference(referenceName);
        using var script = new LythonEngine().Compile(reference.Source + "\nreturn x");
        Assert.True(script.IsValid, name);
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            var options = new LythonRunOptions { MemoryBudget = budget, Globals = new Dictionary<string, object?> { ["x"] = value } };
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(reference.Output, result.StandardOutput);
                Assert.Empty(result.StandardError);
                if (value is decimal number)
                    Assert.Equal(decimal.GetBits(number), decimal.GetBits(Assert.IsType<decimal>(result.ReturnValue)));
                else
                {
                    var expected = value is float single ? (double)single : (double)value;
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(Assert.IsType<double>(result.ReturnValue)));
                }
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task NestedAliasesStaySharedAndEscapingResultsStayFunded()
    {
        var reference = Reference("nested");
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(reference.Source + "\nreturn a", budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var values = new List<object?> { 0.25f, 0.25m, 2UL };
            foreach (var asynchronous in new[] { false, true })
            {
                var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["a"] = values, ["b"] = values } };
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(reference.Output, result.StandardOutput);
                    Assert.Equal(new object?[] { 0.25d, 0.25m, 2UL, 3m }, Assert.IsType<List<object?>>(result.ReturnValue));
                    Assert.True(budget.ReservedBytes > baseline);
                }
                Assert.Equal(3, values.Count);
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}

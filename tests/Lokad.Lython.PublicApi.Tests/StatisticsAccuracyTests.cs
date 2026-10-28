using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StatisticsAccuracyTests
{
    public static IEnumerable<object[]> Vectors()
    {
        var file = Path.Combine(AppContext.BaseDirectory, "Fixtures", "StatisticsContracts", "accuracy.json");
        foreach (var value in JsonSerializer.Deserialize<JsonElement[]>(File.ReadAllText(file))!)
            yield return [value.GetProperty("function").GetString()!, value.GetProperty("value").GetDouble(), value.GetProperty("expected").GetDouble()];
    }

    [Theory, MemberData(nameof(Vectors))]
    public async Task ManagedFunctionsMatchIndependentBinary64Vectors(string function, double value, double expected)
    {
        var expression = function is "erf" or "erfc" ? "math." + function + "(x)" : "statistics.NormalDist()." + function + "(x)";
        using var script = new LythonEngine().Compile("import math,statistics\nreturn " + expression);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["x"] = value } };
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options);
            Assert.True(result.Success, result.Failure?.Message);
            var actual = Assert.IsType<double>(result.ReturnValue);
            Assert.True(double.IsFinite(actual));
            var ulp = Math.Abs(Math.BitIncrement(expected) - expected);
            var tolerance = 32 * ulp;
            // Tail exp(-x*x) inherits rounded-square error. CDF subtracts nearly
            // equal values, so its absolute probability error is the useful bound.
            if (function == "erfc" && value > 8) tolerance = Math.Max(tolerance, 2e-13 * Math.Abs(expected));
            if (function == "cdf") tolerance = Math.Max(tolerance, 2e-15);
            Assert.True(Math.Abs(actual - expected) <= tolerance,
                $"{function}({value:R}): expected {expected:R}, actual {actual:R}, tolerance {tolerance:R}");
        }
    }

    [Fact]
    public async Task SymmetryMonotonicitySpecialValuesAndCentralRoundTrips()
    {
        using var script = new LythonEngine().Compile("""
            import math,statistics
            n=statistics.NormalDist()
            assert math.copysign(1,math.erf(-0.0)) == -1
            assert math.erf(float('inf')) == 1 and math.erfc(float('-inf')) == 2
            assert math.isnan(math.erf(float('nan'))) and math.isnan(math.erfc(float('nan')))
            xs=[-10.,-3.,-1.5,-1.,-1e-12,0.,1e-12,1.,1.5,3.,10.]
            e=[math.erf(x) for x in xs]
            c=[math.erfc(x) for x in xs]
            assert all(e[i] <= e[i+1] and c[i] >= c[i+1] for i in range(len(xs)-1))
            assert all(math.erf(x) == -math.erf(-x) for x in xs)
            ps=[.001,.025,.075,.25,.5,.75,.925,.975,.999]
            q=[n.inv_cdf(p) for p in ps]
            assert all(q[i] < q[i+1] for i in range(len(q)-1))
            assert all(abs(n.cdf(n.inv_cdf(p))-p) <= 2e-15 for p in ps)
            return True
            """);
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(true, result.ReturnValue);
        }
    }
}

using System.Numerics;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class BindingPlanAllocationDiagnostic(ITestOutputHelper output)
{
    private static readonly Dictionary<string, object> Empty = new();
    [Theory]
    [InlineData("empty")]
    [InlineData("single")]
    [InlineData("mixed")]
    [InlineData("context")]
    public void Allocation(string shape)
    {
        var budget = new LythonMemoryBudget(64 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var host = new MockLythonHost();
        var options = new LythonRunOptions { MemoryBudget = owner };
        var context = new Context(host, options);
        var parameters = Parameters(shape);
        var defaults = shape == "mixed" ? new Dictionary<string, object> { ["b"] = new BigInteger(2), ["c"] = new BigInteger(3), ["e"] = new BigInteger(5) } : Empty;
        var samples = new List<long>();
        object? metadata = null;
        for (var sample = -2; sample < 8; sample++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            FunctionBindingPlan? plan = null;
            for (var call = 0; call < 64; call++)
            {
                if (shape == "context") _ = new Context(host, options);
                else plan = new FunctionBindingPlan("f", PythonCallableKind.Function, parameters, defaults, context);
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            if (plan is not null) metadata = Metadata(plan);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            if (sample >= 0) samples.Add(allocated);
        }
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
        output.WriteLine("BINDING_PLAN_ALLOCATION:" + JsonSerializer.Serialize(new { shape, callsPerSample = 64, samples, metadata }));
    }

    [Fact]
    public void AdmissionAndRollback()
    {
        var records = new List<object>();
        foreach (var shape in new[] { "empty", "single", "mixed" })
        foreach (var available in new[] { 0, 127, 128, 255, 256, 384, 512, 768, 4096 })
        {
            const long limit = 65536;
            var budget = new LythonMemoryBudget(1024 * 1024);
            using var owner = budget.CreateScope();
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, MaxExecutionMemoryBytes = limit });
            var governor = context.MemoryGovernor;
            var supplied = new PyList([BigInteger.One], governor, null);
            var defaults = shape == "mixed" ? new Dictionary<string, object> { ["b"] = supplied, ["c"] = supplied, ["e"] = supplied } : Empty;
            var padding = limit - governor.CurrentAccountedBytes - available;
            governor.Reserve(padding, null); governor.Commit(padding);
            var before = governor.CurrentCommittedBytes;
            FunctionBindingPlan? plan = null;
            string? error = null, message = null, reason = null;
            try { plan = new FunctionBindingPlan("f", PythonCallableKind.Function, Parameters(shape), defaults, context); }
            catch (LythonRuntimeException failure)
            {
                error = failure.ExceptionType; message = failure.Message; reason = failure.Attribution?.Reason.ToString();
                Assert.Same(governor.AdmissionFailure, failure);
                Assert.Equal(before, governor.CurrentCommittedBytes);
            }
            Assert.Equal(0, governor.CurrentReservedBytes);
            Assert.Single(supplied); Assert.Equal(BigInteger.One, supplied[0]);
            var after = governor.CurrentCommittedBytes;
            records.Add(new { shape, available, error, message, reason, denied = governor.LastDeniedReservationBytes, committedDelta = after - before, names = plan?.LayoutParameterNames.ToArray() });
            owner.Dispose(); Assert.Equal(0, budget.ReservedBytes);
        }
        output.WriteLine("BINDING_PLAN_WORK:" + JsonSerializer.Serialize(records));
    }
    private static object Metadata(FunctionBindingPlan plan)
        => new { positional = plan.PositionalParameters.Select(p => p.Name).ToArray(), keyword = plan.KeywordOnlyParameters.Select(p => p.Name).ToArray(), names = plan.LayoutParameterNames.ToArray(), map = plan.LayoutParameterIndex.Select(p => new { p.Key, p.Value }).ToArray(), plan.PositionalOnlyCount, plan.NamedLayoutCount, variadic = plan.VariadicList?.Name, keywords = plan.VariadicDictionary?.Name, keywordNames = plan.KeywordDefaultNames.Select(n => n.AsString()).ToArray() };
    private static LoweredFunctionParameter[] Parameters(string shape) => shape switch
    {
        "single" => [new("x", FunctionParameterKind.Positional, null, null)],
        "mixed" => [new("a", FunctionParameterKind.PositionalOnly, null, null), new("b", FunctionParameterKind.PositionalOnly, null, null), new("c", FunctionParameterKind.Positional, null, null), new("args", FunctionParameterKind.VariadicList, null, null), new("d", FunctionParameterKind.KeywordOnly, null, null), new("e", FunctionParameterKind.KeywordOnly, null, null), new("kw", FunctionParameterKind.VariadicDictionary, null, null)],
        _ => []
    };
}

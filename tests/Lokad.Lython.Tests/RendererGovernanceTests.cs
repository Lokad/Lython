using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG06: joined renderers build through the owning governor. A tiny join
/// pins the exact contract: the builder growth peaks, then releases, leaving
/// exactly the retained string charge behind.
/// </summary>
public sealed class RendererGovernanceTests
{
    [Theory]
    [InlineData(20)] // Denied builder growth.
    [InlineData(220)] // Denied final string while scratch remains live.
    public void DeniedStringReprReleasesScratchOnEveryAttempt(long cap)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MaxExecutionMemoryBytes = cap });
        var rendering = new PyRenderingContext(context);
        var input = PyString.FromString(new string('\u200b', 10));
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var error = Assert.Throws<LythonRuntimeException>(() => PyRendering.ToReprPyString(input, rendering));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(new string('\u200b', 10), input.AsString());
    }

    [Fact]
    public void JoinRenderedSequenceCommitsExactOutput()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var rendering = new PyRenderingContext(context);
        var committedBefore = context.MemoryGovernor.CurrentCommittedBytes;
        var peakBefore = context.MemoryGovernor.PeakAccountedBytes;

        var text = PyRendering.JoinRenderedSequence("x", [PyString.FromString("yy")], "z", rendering);

        Assert.Equal("xyyz", text.AsString());
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(128 + 4, context.MemoryGovernor.CurrentCommittedBytes - committedBefore);
        Assert.Equal(8 + 132, context.MemoryGovernor.PeakAccountedBytes - peakBefore);
    }
    [Fact]
    public void OnlyGovernedJoinOverloadRemains()
    {
        // The context-free join built ungoverned strings for any caller; with
        // every renderer routed through the governor it must stay deleted so
        // the compiler rejects future ungoverned callers.
        var overloads = typeof(PyRendering).GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(method => method.Name == nameof(PyRendering.JoinRenderedSequence))
            .ToList();

        var surviving = Assert.Single(overloads);
        Assert.Equal(4, surviving.GetParameters().Length);
        Assert.Equal(typeof(PyRenderingContext), surviving.GetParameters()[3].ParameterType);
    }
    [Fact]
    public void DecimalReprCommitsExactOutput()
    {
        // Decimal rendering discarded its context entirely; the governed
        // result commits exactly its estimate with no builder transient.
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var rendering = new PyRenderingContext(context);
        var value = new PyDecimal(1.5m);

        var committedBefore = context.MemoryGovernor.CurrentCommittedBytes;
        var text = value.RenderPython(rendering);

        Assert.Equal("Decimal('1.5')", text.AsString());
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        Assert.Equal(128 + 14, context.MemoryGovernor.CurrentCommittedBytes - committedBefore);
    }
}

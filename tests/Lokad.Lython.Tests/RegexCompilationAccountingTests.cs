using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
// M05: adopted compilation results additionally hold one 128 B pool entry plus
// one 32 B young-tier growth beside the allowance below.
/// MG09: each compilation commits the retained-state allowance and releases
/// its scratch when the pattern is invalid.
/// </summary>
public sealed class RegexCompilationAccountingTests
{
    private static object CreatePattern(object pattern, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var compiler = typeof(LythonRuntime).GetNestedType("RegexCompiler", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("RegexCompiler not found.");
        var method = compiler.GetMethod("CreatePattern", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new InvalidOperationException("CreatePattern not found.");
        return method.Invoke(compiler, new object?[] { new object[] { pattern }, "re.compile(pattern[, flags])", span, context })
            ?? throw new InvalidOperationException("CreatePattern returned null.");
    }

    [Fact]
    public void SuccessfulCompilationCommitsAllowanceExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        _ = CreatePattern(PyString.FromString("a(b|c)*d"), context, span);
        Assert.Equal(65696L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void GroupAndLengthTermsCommitExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        // Ten groups: 65536 + 9 x 2048, length inside the free envelope.
        _ = CreatePattern(PyString.FromString(string.Concat(Enumerable.Repeat("(a)", 10))), context, span);
        Assert.Equal(84128L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void LongPatternLengthTermCommitsExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        // 100 chars, no groups: 65536 + 36 x 256.
        _ = CreatePattern(PyString.FromString(new string('a', 100)), context, span);
        Assert.Equal(74912L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void FailedCompilationReleasesScratch()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        Assert.ThrowsAny<Exception>(() => CreatePattern(PyString.FromString("a([invalid"), context, span));
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
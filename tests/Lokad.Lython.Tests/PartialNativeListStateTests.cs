using System.Reflection;
using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class PartialNativeListStateTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Theory]
    [InlineData(false, false, 2)]
    [InlineData(true, false, 2)]
    [InlineData(false, true, 2)]
    [InlineData(true, true, 2)]
    [InlineData(false, false, 20)]
    [InlineData(true, false, 20)]
    [InlineData(false, true, 20)]
    [InlineData(true, true, 20)]
    public async Task FailedConstructionRefundsOnlyItsPrivatePrefix(bool asynchronous, bool cleanupFailure, int count)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536) },regexProvider:Utf8RegexProvider.Instance);
        var alias = PyString.FromString("held");
        var held = new PyList([alias], context.MemoryGovernor);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var source = new FailingSource(alias, count, cleanupFailure);
        var list = Assert.IsAssignableFrom<LythonRuntime.ICallable>(context.State.BuiltinVariables["list"]);
        CallArgumentValue[] arguments = [CallArgumentValue.Positional(source)];
        var failure = asynchronous
            ? await Assert.ThrowsAsync<InvalidOperationException>(async () => await list.InvokeAsync(arguments, Span, context))
            : Assert.Throws<InvalidOperationException>(() => list.Invoke(arguments, Span, context));
        Assert.Equal("controlled construction failure", failure.Message);
        Assert.True(source.Cleaned);
        Assert.Same(alias, held[0]);
        Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);

    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeniedPrefixGrowthRefundsOnlyUnpublishedListStorage(bool asynchronous)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536) },regexProvider:Utf8RegexProvider.Instance);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var source = new AdmissionDeniedSource(context);
        var list = Assert.IsAssignableFrom<LythonRuntime.ICallable>(context.State.BuiltinVariables["list"]);
        CallArgumentValue[] arguments = [CallArgumentValue.Positional(source)];
        try
        {
            var failure = asynchronous
                ? await Assert.ThrowsAsync<LythonRuntimeException>(async () => await list.InvokeAsync(arguments, Span, context))
                : Assert.Throws<LythonRuntimeException>(() => list.Invoke(arguments, Span, context));
            Assert.Equal("MemoryError", failure.ExceptionType);
            Assert.True(source.Completed);
            Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(source.Funding, context.MemoryGovernor.CurrentReservedBytes);

        }
        finally { context.MemoryGovernor.ReleaseReserved(source.Funding); }
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    [InlineData(false, 20)]
    [InlineData(true, 20)]
    public async Task ConstructionPreservesItsPrefixAcrossGeneratorReentry(bool asynchronous, int count)
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(65536) },regexProvider:Utf8RegexProvider.Instance);
        context.Variables.Clear();
        var probe = new PrefixProbe();
        context.Variables["probe"] = probe;
        context.Variables["list"] = context.State.BuiltinVariables["list"];
        context.Variables["range"] = context.State.BuiltinVariables["range"];
        var source = $"def values():\n    yield 'x' * 16000\n    for i in range({count - 1}):\n        yield i\n    probe()\n    yield None\nreturn list(values())\n";
        var parsed = LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance);
        Assert.Empty(parsed.Diagnostics);
        var code = ExecutableScript.Compile(LoweredScript.Lower(parsed.Script!));
        var result = asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context)
            : LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, null);
        var produced = Assert.IsType<PyList>(result);
        Assert.Equal(count + 1, produced.Count);
        Assert.Equal(16000, Assert.IsType<PyString>(produced[0]).Utf8Bytes.Length);
        Assert.True(probe.Checked);

        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class PrefixProbe : LythonRuntime.ICallable
    {
        internal bool Checked;

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Checked = true;
            return PyNone.Instance;
        }
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            await Task.Yield();
            return Invoke(arguments, span, context);
        }
    }

    private sealed class FailingSource(object alias, int count, bool cleanupFailure) : IPyAsyncIterableValue
    {
        internal bool Cleaned;
        public IEnumerable<object> Iterate()
        {
            try
            {
                yield return alias;
                for (var i = 1; i < count; i++) yield return new BigInteger(i);
                if (!cleanupFailure) throw new InvalidOperationException("controlled construction failure");
            }
            finally
            {
                Cleaned = true;
                if (cleanupFailure) throw new InvalidOperationException("controlled construction failure");
            }
        }
        public async IAsyncEnumerable<object> IterateAsync()
        {
            await Task.Yield();
            foreach (var item in Iterate()) yield return item;
        }
    }

    private sealed class AdmissionDeniedSource(LythonRuntime.ExecutionContext context) : IPyAsyncIterableValue
    {
        internal long Funding;
        internal bool Completed;
        public IEnumerable<object> Iterate()
        {
            // Exhaust the admitted small capacity before applying pressure.
            // In-capacity stores intentionally need no further admission.
            for (var i = 0; i < PyListStorage.SmallCapacity; i++) yield return BigInteger.One;
            Completed = true;
            Funding = ((LythonMemoryBudget)context.MemoryGovernor.Budget!).LimitBytes!.Value - context.MemoryGovernor.CurrentAccountedBytes;
            context.MemoryGovernor.Reserve(Funding, Span);
            yield return new BigInteger(2);
        }
        public async IAsyncEnumerable<object> IterateAsync()
        {
            await Task.Yield();
            foreach (var item in Iterate()) yield return item;
        }
    }
}

using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;
using Writer = Lokad.Lython.Runtime.LythonRuntime.CsvWriterObject;

namespace Lokad.Lython.Tests;

public sealed class CsvWriterowCallBoundaryTests
{
    // Frozen on75c03d02 before changing call lowering or dispatch.
    private const string FrozenGuards = """{"False:row":{"exposed":false,"shape":"row","outcome":"saved","writes":["a,\"\u00e9,\ud83d\ude00\"\n"]},"True:scalar":{"exposed":true,"shape":"scalar","outcome":"TypeError:'NoneType' object is not iterable","writes":[]},"False:scalar":{"exposed":false,"shape":"scalar","outcome":"TypeError:'NoneType' object is not iterable","writes":[]},"True:unsupported-cell":{"exposed":true,"shape":"unsupported-cell","outcome":"TypeError:CSV rows must contain scalar values.","writes":[]},"False:unsupported-cell":{"exposed":false,"shape":"unsupported-cell","outcome":"TypeError:CSV rows must contain scalar values.","writes":[]},"True:row":{"exposed":true,"shape":"row","outcome":"saved","writes":["a,\"\u00e9,\ud83d\ude00\"\n"]}}""";
    private static readonly LythonSourceSpan Span = new(0, 0, 1, 1);

    public static IEnumerable<object[]> Guards()
    {
        foreach (var exposed in new[] { false, true })
        foreach (var asynchronous in new[] { false, true })
        foreach (var shape in new[] { "row", "scalar", "unsupported-cell" }) yield return [exposed, asynchronous, shape];
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrdinaryCallUsesReceiverOperandsWithoutABoundMemberCache(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var code = Compile("return writer.writerow(argument())");
            Assert.Equal(1, Assert.Single(code.CallSites, c => c.HasReceiver).ArgumentCount);
            var (context, _, _, sink) = Prepare(owner, "row");
            Assert.Same(sink.ReturnValue, await Run(code, context, asynchronous));
            var caches = (Array)sink.Frame!.GetType().GetField("_memberCaches", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(sink.Frame)!;
            Assert.All(caches.Cast<object?>(), Assert.Null);
            AssertUnwound(context);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [MemberData(nameof(Guards))]
    public async Task EveryCheckpointPreservesArgumentAndPublication(bool exposed, bool asynchronous, string shape)
    {
        var source = (exposed ? "method=writer.writerow\nreturn method(argument())" : "return writer.writerow(argument())");
        var code = Compile(source);
        string outcome;
        string[] writes;
        using (var owner = new LythonMemoryBudget(4 * 1024 * 1024).CreateScope())
        {
            var (context, writer, argument, sink) = Prepare(owner, shape);
            try { outcome = Assert.IsType<PyString>(await Run(code, context, asynchronous)).AsString(); }
            catch (LythonRuntimeException error) { outcome = error.ExceptionType + ":" + error.Message; }
            writes = sink.Writes.ToArray();
            Assert.Equal(1, argument.Calls);
            Assert.Equal(shape == "row" ? 1 : 0, writes.Length);
            Assert.Empty(writer.Rows);
            AssertUnwound(context);
        }
        using var frozen = JsonDocument.Parse(FrozenGuards);
        var expected = frozen.RootElement.GetProperty($"{exposed}:{shape}");
        Assert.Equal(expected.GetProperty("outcome").GetString(), outcome);
        Assert.Equal(expected.GetProperty("writes").EnumerateArray().Select(x => x.GetString()), writes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RefusedConversionRetainsNeitherHistoryNorPublishedOutput(bool asynchronous, bool history)
    {
        const long limit = 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        {
            var (context, writer, argument, sink) = Prepare(owner, "row", history);
            argument.Value = new PyList([BigInteger.One << 65536], context.MemoryGovernor, Span);
            var committed = context.MemoryGovernor.CurrentCommittedBytes;
            Assert.True(budget.TryReserve(limit - budget.ReservedBytes - 512, out var padding));
            using (padding)
            {
                var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Run(Compile("return writer.writerow(argument())"), context, asynchronous));
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
                Assert.Empty(writer.Rows);
                Assert.Empty(sink.Writes);
                Assert.Equal(1, argument.Calls);
                Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
                AssertUnwound(context);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuspendedArgumentKeepsTheEvaluatedWriter(bool cancel)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var (context, writer, argument, sink) = Prepare(owner, "row");
            var otherSink = new Sink(context.MemoryGovernor);
            var other = new Writer(Options(), otherSink, context.MemoryGovernor, Span);
            argument.Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = Run(Compile("return writer.writerow(argument())"), context, true).AsTask();
            try
            {
                Assert.False(pending.IsCompleted);
                Assert.Equal(1, argument.Calls);
                Assert.True(budget.ReservedBytes > 0);
                context.Variables["writer"] = other;
                if (cancel)
                {
                    argument.Release.SetCanceled();
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
                    Assert.Empty(sink.Writes);
                }
                else
                {
                    argument.Release.SetResult();
                    Assert.Same(sink.ReturnValue, await pending.WaitAsync(TimeSpan.FromSeconds(5)));
                    Assert.Single(sink.Writes);
                }
                Assert.Empty(otherSink.Writes);
                Assert.Empty(writer.Rows);
                AssertUnwound(context);
            }
            finally
            {
                argument.Release.TrySetCanceled();
                try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (OperationCanceledException) { }
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuspendedPublicationKeepsRenderedRowFundedUntilCompletion(bool cancel)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var (context, _, _, sink) = Prepare(owner, "row");
            sink.Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var before = budget.ReservedBytes;
            var pending = Run(Compile("return writer.writerow(argument())"), context, true).AsTask();
            try
            {
                Assert.False(pending.IsCompleted);
                var rendered = Assert.IsType<PyString>(sink.Retained);
                Assert.Equal("a,\"é,😀\"\n", rendered.AsString());
                Assert.Same(context.MemoryGovernor, rendered.OwnerMemoryGovernor);
                Assert.True(budget.ReservedBytes >= before + PyString.EstimateApproximateBytes(rendered.Utf8Bytes.Length));
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                if (cancel)
                {
                    sink.Release.SetCanceled();
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
                }
                else
                {
                    sink.Release.SetResult();
                    Assert.Same(sink.ReturnValue, await pending.WaitAsync(TimeSpan.FromSeconds(5)));
                }
                Assert.Equal("a,\"é,😀\"\n", rendered.AsString());
                Assert.Same(rendered, sink.Retained);
                AssertUnwound(context);
            }
            finally
            {
                sink.Release.TrySetCanceled();
                try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (OperationCanceledException) { }
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    internal static LythonRuntime.CsvOptions Options() => new(PyStringOps.CommaLiteral, PyString.FromString("\""),
        LythonRuntime.CsvQuotingMode.Minimal, true, null, false, PyString.FromString("\n"), false);

    private static (Context, Writer, Argument, Sink) Prepare(ILythonMemoryBudget owner, string shape, bool history = false)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner},regexProvider:Utf8RegexProvider.Instance);
        var sink = new Sink(context.MemoryGovernor);
        var writer = new Writer(Options(), history ? null : sink, context.MemoryGovernor, Span);
        object row = shape switch
        {
            "scalar" => PyNone.Instance,
            "unsupported-cell" => new PyList([new object()], context.MemoryGovernor, Span),
            _ => new PyList([PyString.FromString("a", context.MemoryGovernor, Span), PyString.FromString("é,😀", context.MemoryGovernor, Span)], context.MemoryGovernor, Span),
        };
        var argument = new Argument { Value = row };
        context.Variables["writer"] = writer;
        context.Variables["argument"] = argument;
        return (context, writer, argument, sink);
    }

    private static ExecutableCodeObject Compile(string source)
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;

    private static ValueTask<object?> Run(ExecutableCodeObject code, Context context, bool asynchronous)
        => asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
            : ValueTask.FromResult(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));

    private static void AssertUnwound(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class Argument : LythonRuntime.ICallable
    {
        internal object Value = PyNone.Instance;
        internal int Calls;
        internal TaskCompletionSource? Release;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context) { Calls++; return Value; }
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Calls++;
            if (Release is not null) await Release.Task.ConfigureAwait(false);
            return Value;
        }
    }

    private sealed class Sink(MemoryGovernor governor) : LythonRuntime.ICallable
    {
        internal readonly PyString ReturnValue = PyString.FromString("saved", governor, Span);
        internal readonly List<string> Writes = [];
        internal object? Retained;
        internal object? Frame;
        internal TaskCompletionSource? Release;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Retained = arguments[0].Value;
            Frame = context.CurrentExecutableFrame;
            Writes.Add(((PyString)Retained).AsString());
            return ReturnValue;
        }
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            var result = Invoke(arguments, span, context);
            if (Release is not null) await Release.Task.ConfigureAwait(false);
            return result;
        }
    }
}

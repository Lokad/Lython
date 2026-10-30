using System.Numerics;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class JsonIntegerEmissionAdmissionTests
{
    // Frozen on 9eb065eb before modifying production emission.
    private const string EagerFrozen = """{"chunks":["{\"9223372036854775807\": 1208925819614629174706176}"]}""";
    private const string IteratorFrozen = """{"chunks":["{","\"9223372036854775807\": 1208925819614629174706176","}"]}""";

    private static readonly LythonSourceSpan Span = new(2, 3, 4, 5);

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task NumericKeysPreserveRenderedTextAndChunks(bool iterator, bool asynchronous)
    {
        var source = "import json\nreturn " + (iterator ? "list(json.JSONEncoder().iterencode(source))" : "json.dumps(source)");
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        string[] chunks;
        using (var owner = budget.CreateScope())
        {
            var context = PrepareGuard(owner);
            var value = await RunGuard(code, context, asynchronous);
            chunks = iterator ? Assert.IsType<PyList>(value).Select(x => Assert.IsType<PyString>(x).AsString()).ToArray()
                : [Assert.IsType<PyString>(value).AsString()];
            Assert.Equal("{\"9223372036854775807\": 1208925819614629174706176}", string.Concat(chunks));
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Null(context.CurrentExecutableFrame);
        }
        Assert.Equal(0, budget.ReservedBytes);
        using var frozen = JsonDocument.Parse(iterator ? IteratorFrozen : EagerFrozen);
        Assert.Equal(frozen.RootElement.GetProperty("chunks").EnumerateArray().Select(x => x.GetString()!), chunks);
    }

    private static Context PrepareGuard(ILythonMemoryBudget owner)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner},regexProvider:Utf8RegexProvider.Instance);
        var dict = new PyDict(context.MemoryGovernor, Span);
        dict.SetItem(new BigInteger(long.MaxValue), BigInteger.One << 80);
        context.Variables["source"] = dict;
        return context;
    }

    private static ValueTask<object?> RunGuard(ExecutableCodeObject code, Context context, bool asynchronous)
        => asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
            : ValueTask.FromResult(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));

    public static IEnumerable<object[]> Refusals()
    {
        foreach (var route in new[] { "dumps", "encode", "iterator" })
        foreach (var key in new[] { false, true })
        foreach (var asynchronous in new[] { false, true })
        foreach (var shared in new[] { false, true }) yield return [route, key, asynchronous, shared];
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RefusedAppendRefundsAdmittedConversion(bool iterator, bool asynchronous)
    {
        const long limit = 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
            var input = BigInteger.One << 80;
            var callable = Load(context, iterator ? "iterator" : "dumps");
            CallArgumentValue[] arguments = [CallArgumentValue.Positional(input)];
            var enumerator = iterator ? Assert.IsType<LythonRuntime.JsonEncodeIterator>(callable.Invoke(arguments, Span, context)) : null;
            var working = RuntimeMemoryEstimates.EstimateIntegerRenderingWorkingBytes(input);
            Assert.True(budget.TryReserve(limit - budget.ReservedBytes - working - 1, out var padding));
            using (padding)
            {
                var before = context.MemoryGovernor.CurrentCommittedBytes;
                var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                {
                    if (enumerator is null)
                    {
                        if (asynchronous) await callable.InvokeAsync(arguments, Span, context);
                        else _ = callable.Invoke(arguments, Span, context);
                    }
                    else if (asynchronous) await enumerator.TryMoveNextAsync();
                    else enumerator.TryMoveNext(out _);
                });
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, error.Attribution?.Reason);
                Assert.Same(context.MemoryGovernor.AdmissionFailure, error);
                Assert.True(context.MemoryGovernor.LastDeniedReservationBytes > 1);
                Assert.Equal(before, context.MemoryGovernor.CurrentCommittedBytes);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NumericChunksKeepFundingAfterIteratorWorkingStorageEnds(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner },regexProvider:Utf8RegexProvider.Instance);
            var input = new PyDict(context.MemoryGovernor, Span);
            input.SetItem(new BigInteger(long.MaxValue), BigInteger.One << 80);
            var callable = Load(context, "iterator");
            var before = budget.ReservedBytes;
            var iterator = Assert.IsType<LythonRuntime.JsonEncodeIterator>(callable.Invoke([CallArgumentValue.Positional(input)], Span, context));
            var chunks = new List<PyString>();
            if (asynchronous)
            {
                while (true)
                {
                    var next = await iterator.TryMoveNextAsync();
                    if (!next.HasValue) break;
                    chunks.Add(Assert.IsType<PyString>(next.Value));
                }
            }
            else while (iterator.TryMoveNext(out var chunk)) chunks.Add(Assert.IsType<PyString>(chunk));
            Assert.Equal("{\"9223372036854775807\": 1208925819614629174706176}", string.Concat(chunks.Select(x => x.AsString())));
            Assert.All(chunks, chunk => Assert.Same(context.MemoryGovernor, chunk.OwnerMemoryGovernor));
            Assert.True(budget.ReservedBytes >= before + chunks.Sum(chunk => PyString.EstimateApproximateBytes(chunk.Utf8Bytes.Length)));
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            var held = budget.ReservedBytes;
            using (var otherOwner = budget.CreateScope())
            {
                var other = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = otherOwner },regexProvider:Utf8RegexProvider.Instance);
                _ = PyString.FromString(new string('x', 1024), other.MemoryGovernor, Span);
                Assert.True(budget.ReservedBytes > held);
            }
            Assert.Equal(held, budget.ReservedBytes);
            Assert.Equal(3, chunks.Count);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task RefusalPrecedesProportionalIntegerText(string route, bool key, bool asynchronous, bool shared)
    {
        foreach (var measured in new[] { false, true })
        {
            const long limit = 1024 * 1024;
            var budget = new LythonMemoryBudget(limit);
            using (var owner = budget.CreateScope())
            {
                var context = new Context(new MockLythonHost(), new LythonRunOptions
                    { MemoryBudget = owner},regexProvider:Utf8RegexProvider.Instance);
                var governor = context.MemoryGovernor;
                governor.Reserve(RuntimeMemoryEstimates.EstimateBigIntegerBytesFromBitCount(65537) + 64, Span);
                governor.Commit(governor.CurrentReservedBytes);
                object input = BigInteger.One << 65536;
                object value = input;
                if (key)
                {
                    var dict = new PyDict(governor, Span);
                    dict.SetItem(input, PyNone.Instance);
                    value = dict;
                }
                var callable = Load(context, route);
                CallArgumentValue[] arguments = [CallArgumentValue.Positional(value)];
                var iterator = route == "iterator"
                    ? Assert.IsType<LythonRuntime.JsonEncodeIterator>(callable.Invoke(arguments, Span, context)) : null;
                var retained = new List<PyString>(4);
                var padding = limit - budget.ReservedBytes - 16384;
                ILythonMemoryReservation? blocker = null;
                if (shared) Assert.True(budget.TryReserve(padding, out blocker));
                else { governor.Reserve(padding, Span); governor.Commit(padding); }
                using var hostPadding = blocker;
                var beforeCommitted = governor.CurrentCommittedBytes;
                LythonRuntimeException? failure = null;
                var thread = Environment.CurrentManagedThreadId;
                var before = GC.GetAllocatedBytesForCurrentThread();
                try
                {
                    if (iterator is null)
                    {
                        var pending = asynchronous ? callable.InvokeAsync(arguments, Span, context)
                            : ValueTask.FromResult(callable.Invoke(arguments, Span, context));
                        Assert.True(pending.IsCompleted);
                        _ = await pending;
                    }
                    else if (asynchronous)
                    {
                        while (true)
                        {
                            var pending = iterator.TryMoveNextAsync();
                            Assert.True(pending.IsCompleted);
                            var next = await pending;
                            if (!next.HasValue) break;
                            retained.Add(Assert.IsType<PyString>(next.Value));
                        }
                    }
                    else
                    {
                        while (iterator.TryMoveNext(out var chunk)) retained.Add(Assert.IsType<PyString>(chunk));
                    }
                }
                catch (LythonRuntimeException error) { failure = error; }
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.Equal(thread, Environment.CurrentManagedThreadId);
                Assert.NotNull(failure);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance,
                    failure.Attribution?.Reason);
                Assert.Same(governor.AdmissionFailure, failure);
                Assert.Same(Span, failure.Span);
                Assert.Equal(65537, ((BigInteger)input).GetBitLength());
                if (key) Assert.Same(input, Assert.Single(((PyDict)value)).Key);
                Assert.True(governor.CurrentCommittedBytes <= beforeCommitted + retained.Sum(chunk => PyString.EstimateApproximateBytes(chunk.Utf8Bytes.Length)));
                Assert.All(retained, chunk => Assert.Same(governor, chunk.OwnerMemoryGovernor));
                Assert.Null(context.CurrentExecutableFrame);
                Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
                Assert.Equal(0, governor.CurrentReservedBytes);
                if (measured) Assert.True(allocated < 16384, $"Refused JSON numeric emission allocated {allocated} bytes.");
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    private static LythonRuntime.ICallable Load(Context context, string route)
    {
        var target = route switch { "dumps" => "json.dumps", "encode" => "json.JSONEncoder().encode", _ => "json.JSONEncoder().iterencode" };
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile("import json\nreturn " + target,regexProvider:Utf8RegexProvider.Instance).Script!)).EntryPoint;
        return Assert.IsAssignableFrom<LythonRuntime.ICallable>(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
    }
}

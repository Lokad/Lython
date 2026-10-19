using System.Text.Json;
using System.Text;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class JsonStringEmissionAdmissionTests(ITestOutputHelper output)
{
    private static readonly LythonSourceSpan Span = new(2, 3, 4, 5);

    [Theory]
    [InlineData(1)]
    [InlineData(129)]
    public void InvalidUtf8UsesTheStrictDecoderAndRefundsWorkingStorage(int length)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var bytes = Enumerable.Repeat((byte)'a', length).ToArray();
            bytes[^1] = 255;
            var input = PyString.FromOwnedUtf8(bytes, context.MemoryGovernor, Span);
            var callable = Load(context, "dumps");
            var committed = context.MemoryGovernor.CurrentCommittedBytes;
            Assert.Throws<DecoderFallbackException>(() => callable.Invoke([CallArgumentValue.Positional(input)], Span, context));
            Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.Null(context.MemoryGovernor.AdmissionFailure);
            Assert.Equal(255, input.Utf8Bytes.Span[^1]);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("dumps", false, false)]
    [InlineData("dumps", false, true)]
    [InlineData("dumps", true, false)]
    [InlineData("dumps", true, true)]
    [InlineData("encode", false, false)]
    [InlineData("encode", false, true)]
    [InlineData("encode", true, false)]
    [InlineData("encode", true, true)]
    [InlineData("iterator", false, false)]
    [InlineData("iterator", false, true)]
    [InlineData("iterator", true, false)]
    [InlineData("iterator", true, true)]
    public async Task SmallResultStillRequiresAdmissionForRetainedUtf8(string route, bool asynchronous, bool shared)
    {
        const long limit = 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions
                { MemoryBudget = owner});
            var governor = context.MemoryGovernor;
            var input = PyString.FromString("a", governor, Span);
            var callable = Load(context, route);
            CallArgumentValue[] arguments = [CallArgumentValue.Positional(input)];
            var iterator = route == "iterator"
                ? Assert.IsType<LythonRuntime.JsonEncodeIterator>(callable.Invoke(arguments, Span, context)) : null;
            var resultBytes = PyString.EstimateApproximateBytes(3); // "a"
            var padding = limit - budget.ReservedBytes - resultBytes + 1;
            ILythonMemoryReservation? blocker = null;
            if (shared) Assert.True(budget.TryReserve(padding, out blocker));
            else { governor.Reserve(padding, Span); governor.Commit(padding); }
            using (blocker)
            {
                var committed = governor.CurrentCommittedBytes;
                object? published = null;
                var error = await Assert.ThrowsAsync<LythonRuntimeException>(async () =>
                {
                    if (iterator is null)
                        published = asynchronous ? await callable.InvokeAsync(arguments, Span, context)
                            : callable.Invoke(arguments, Span, context);
                    else if (asynchronous) published = await iterator.TryMoveNextAsync();
                    else { iterator.TryMoveNext(out var chunk); published = chunk; }
                });
                Assert.Null(published);
                Assert.Equal(shared ? LythonFailureReason.SharedMemoryAllowance : LythonFailureReason.SharedMemoryAllowance,
                    error.Attribution?.Reason);
                Assert.Same(governor.AdmissionFailure, error);
                Assert.Same(Span, error.Span);
                Assert.Equal(resultBytes, governor.LastDeniedReservationBytes);
                Assert.Equal(committed, governor.CurrentCommittedBytes);
                Assert.Equal("a", input.AsString());
                Assert.Equal(0, governor.CurrentReservedBytes);
                Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    public static IEnumerable<object[]> Refusals()
    {
        foreach (var route in new[] { "dumps", "encode", "iterator" })
        foreach (var key in new[] { false, true })
        foreach (var asynchronous in new[] { false, true })
        foreach (var shared in new[] { false, true }) yield return [route, key, asynchronous, shared];
    }

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task RefusalPrecedesProportionalInputDecode(string route, bool key, bool asynchronous, bool shared)
    {
        foreach (var measured in new[] { false, true })
        {
            const long limit = 1024 * 1024;
            var budget = new LythonMemoryBudget(limit);
            using (var owner = budget.CreateScope())
            {
                var context = new Context(new MockLythonHost(), new LythonRunOptions
                    { MemoryBudget = owner});
                var governor = context.MemoryGovernor;
                var input = PyString.FromString(new string('a', 65536), governor, Span);
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
                var committed = governor.CurrentCommittedBytes;
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
                    else while (iterator.TryMoveNext(out var chunk)) retained.Add(Assert.IsType<PyString>(chunk));
                }
                catch (LythonRuntimeException error) { failure = error; }
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.Equal(thread, Environment.CurrentManagedThreadId);
                if (measured) output.WriteLine("JSON_STRING_REFUSAL:" + JsonSerializer.Serialize(new
                    { route, key, asynchronous, shared, allocated, denied = governor.LastDeniedReservationBytes, chunks = retained.Count }));
                Assert.NotNull(failure);
                Assert.Equal(shared ? LythonFailureReason.SharedMemoryAllowance : LythonFailureReason.SharedMemoryAllowance,
                    failure.Attribution?.Reason);
                Assert.Same(governor.AdmissionFailure, failure);
                Assert.Same(Span, failure.Span);
                Assert.Equal(65536, input.Utf8Bytes.Length);
                Assert.True(input.Utf8Bytes.Span.IndexOfAnyExcept((byte)'a') < 0);
                if (key) Assert.Same(input, Assert.Single(((PyDict)value)).Key);
                Assert.True(governor.CurrentCommittedBytes <= committed + retained.Sum(chunk => PyString.EstimateApproximateBytes(chunk.Utf8Bytes.Length)));
                Assert.All(retained, chunk => Assert.Same(governor, chunk.OwnerMemoryGovernor));
                Assert.Null(context.CurrentExecutableFrame);
                Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
                Assert.Equal(0, governor.CurrentReservedBytes);
                if (measured) Assert.True(allocated < 16384, $"Refused JSON input decoding allocated {allocated} bytes.");
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RefusedOutputRefundsAdmittedInputDecode(bool iterator, bool asynchronous)
    {
        const long limit = 1024 * 1024;
        var budget = new LythonMemoryBudget(limit);
        using (var owner = budget.CreateScope())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var input = PyString.FromString(new string('a', 4097), context.MemoryGovernor, Span);
            var callable = Load(context, iterator ? "iterator" : "dumps");
            CallArgumentValue[] arguments = [CallArgumentValue.Positional(input)];
            var enumerator = iterator ? Assert.IsType<LythonRuntime.JsonEncodeIterator>(callable.Invoke(arguments, Span, context)) : null;
            var working = 64 + 2L * input.Utf8Bytes.Length;
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
                Assert.Same(Span, error.Span);
                Assert.Equal(8194, context.MemoryGovernor.LastDeniedReservationBytes);
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
    public async Task YieldedStringKeepsFundingAfterDecodeAndIteratorEnd(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var input = PyString.FromString(new string('a', 4097), context.MemoryGovernor, Span);
            var callable = Load(context, "iterator");
            var iterator = Assert.IsType<LythonRuntime.JsonEncodeIterator>(callable.Invoke([CallArgumentValue.Positional(input)], Span, context));
            var before = budget.ReservedBytes;
            var next = asynchronous ? await iterator.TryMoveNextAsync()
                : iterator.TryMoveNext(out var value) ? PyIterationResult.Yield(value) : PyIterationResult.End;
            Assert.True(next.HasValue);
            var chunk = Assert.IsType<PyString>(next.Value);
            Assert.Equal("\"" + new string('a', 4097) + "\"", chunk.AsString());
            Assert.Same(context.MemoryGovernor, chunk.OwnerMemoryGovernor);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            var end = asynchronous ? await iterator.TryMoveNextAsync()
                : iterator.TryMoveNext(out _) ? throw new Xunit.Sdk.XunitException("Unexpected second chunk.") : PyIterationResult.End;
            Assert.False(end.HasValue);
            Assert.True(budget.ReservedBytes >= before + PyString.EstimateApproximateBytes(chunk.Utf8Bytes.Length));
            var held = budget.ReservedBytes;
            using (var otherOwner = budget.CreateScope())
            {
                var other = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = otherOwner });
                Assert.True(budget.ReservedBytes >= held);
                Assert.Equal(0, other.MemoryGovernor.CurrentReservedBytes);
                Assert.Equal('"', chunk.AsString()[0]);
            }
            Assert.Equal(held, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static LythonRuntime.ICallable Load(Context context, string route)
    {
        var target = route switch { "dumps" => "json.dumps", "encode" => "json.JSONEncoder().encode", _ => "json.JSONEncoder().iterencode" };
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile("import json\nreturn " + target).Script!)).EntryPoint;
        return Assert.IsAssignableFrom<LythonRuntime.ICallable>(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
    }
}

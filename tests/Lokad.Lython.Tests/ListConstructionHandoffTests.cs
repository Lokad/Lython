using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class ListConstructionHandoffTests(ITestOutputHelper output)
{
    // Complete records frozen on19cd6654 before runtime changes.
    private const string FrozenGuards = """{"True:wide":"360DBD4F7A2C24823C6691870E424220A1626BEC8749604902C3F7CDAD4845B6","False:limit":"AD965D204E7D40DB16F2493EC52DDB6B6FA5D0EBF035A062019947B2DE27D990","False:cleanup":"750F19F78A308C0AD5113414CE7964AC8D2C652383C289460E5A76CBA4645AF3","True:known-wide":"2C1870E3FDCEF0945E7333D939FE7105DC379F82AAEB88A68D154FBB0DDBE0A0","True:cleanup":"5163A654DC296A6B2E1645B88A3FDD635B98CCFE73AB6EE9718C5AEBF39E12A9","False:pair":"FBC6314DDAC0DF6554B7E7973049254B7175A5381FFA87D1A5B09224AACF9760","False:known-empty":"3F8A5AAECC80EAABCF7C4617A210834C50433EDA34D34E171C5072B66E755A4E","True:known-empty":"6CCBF69E447D2E037A21F230D9126E4EE98A3FABBB479123FE23A8A1D9A559C8","True:move":"291DCDD8A554945C7CF8B72CC07502C58426C0B69D67178D2AE68E73F056DBC0","False:known-wide":"EE3521E4F46BDE89C27F7271305672E9101D92EA542DAA647EC84DC4C098BB5C","True:empty":"462F328DDF4DB0C5D91A5224F85C227844407FBA87E09E1447AB5FA8D20BF6FF","True:both":"EFAA4B454093AFAD9216422F6F6317BC0A80192503CCF9240B9F6222208E96FC","False:wide":"18083A94824483B85FD44A1E92F5CAD3B61661578B48DA17585423DA927B3018","True:pair":"4CBD27FEADD79EEE24A6E8D34B419A04682D24ED27C13007EA2568A3836E95DA","False:both":"7EFD94D5B8D8370A7B79D8DEB66BB43C686448456F0C2290F5EF85031D0BDA41","False:empty":"18121621DF907B7191D858B090AC5E5A5D2E65E08EA42116BFD3C303B0C4077A","True:limit":"8239F466279F73A9C67A0746CCFA509E42880C084BE5E1F5C0DA8D47C6A868FD","False:move":"F4AFAE8B692DAC392DE89B2EA55B5D8A8A293570964ECC1BDF4A9CDA1D46C348"}""";
    private const int MaximumSteps = 10000;
    private static readonly LythonSourceSpan Span = new(0, 0, 1, 1);
    private static readonly ExecutableCodeObject Code = ExecutableScript.Compile(
        LoweredScript.Lower(LythonFrontend.Compile("return list(source)").Script!)).EntryPoint;

    public static IEnumerable<object[]> Cases()
    {
        foreach (var asynchronous in new[] { false, true })
        foreach (var shape in new[] { "known-empty", "known-wide", "empty", "pair", "wide", "move", "cleanup", "both", "limit" })
            yield return [asynchronous, shape];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ConstructionPreservesFuelEffectsAndCleanupOrder(bool asynchronous, string shape)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        long steps;
        string outcome;
        string[] events;
        using (var owner = budget.CreateScope())
        {
            var (context, trace) = Prepare(owner, shape);
            try
            {
                var list = Assert.IsType<PyList>(await Run(context, asynchronous));
                Assert.DoesNotContain(shape, new[] { "move", "cleanup", "both", "limit" });
                var count = shape.Contains("empty", StringComparison.Ordinal) ? 0 : shape.Contains("wide", StringComparison.Ordinal) ? 65 : 2;
                Assert.Equal(Enumerable.Range(0, count).Select(i => (object)new BigInteger(i)), list);
                outcome = "list:" + list.Count;
            }
            catch (Exception error)
            {
                Assert.Contains(shape, new[] { "move", "cleanup", "both", "limit" });
                outcome = Describe(error);
                if (shape is "cleanup" or "both") Assert.Equal("cleanup", error.Message);
                if (shape == "move") Assert.Equal("move", error.Message);
            }
            steps = context.Limits.ExecutionStepCount;
            events = trace.ToArray();
            AssertUnwound(context);
        }
        Assert.Equal(0, budget.ReservedBytes);
        var checkpoints = new List<string>();
        for (var remaining = 0; remaining < steps; remaining++)
        {
            using (var owner = budget.CreateScope())
            {
                var (context, trace) = Prepare(owner, shape);
                context.Limits.ExecutionStepCount = MaximumSteps - remaining;
                var error = await Record.ExceptionAsync(async () => await Run(context, asynchronous));
                Assert.NotNull(error);
                // A hostile cleanup can replace an ordinary fuel exception.
                // Freeze that existing selection and the complete callback prefix.
                checkpoints.Add(Describe(error) + ":" + context.Limits.ExecutionStepCount + ":" + string.Join(';', trace));
                AssertUnwound(context);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
        var record = JsonSerializer.Serialize(new { asynchronous, shape, steps, outcome, events, checkpoints });
        output.WriteLine("LIST_HANDOFF_GUARDS:" + record);
        using var frozen = JsonDocument.Parse(FrozenGuards);
        Assert.Equal(frozen.RootElement.GetProperty($"{asynchronous}:{shape}").GetString(),
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(record))));
    }

    [Theory]
    [InlineData("success")]
    [InlineData("error")]
    [InlineData("cancel")]
    public async Task PendingCleanupKeepsPrivateBackingFundedUntilTheHandoff(string completion)
    {
        var budget = new LythonMemoryBudget(512 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            var value = PyString.FromString(new string('x', 4096), context.MemoryGovernor, Span);
            var source = new PendingCleanup(value);
            var list = Assert.IsAssignableFrom<LythonRuntime.ICallable>(context.State.BuiltinVariables["list"]);
            CallArgumentValue[] arguments = [CallArgumentValue.Positional(source)];
            var before = budget.ReservedBytes;
            var committed = context.MemoryGovernor.CurrentCommittedBytes;
            var pending = list.InvokeAsync(arguments, Span, context).AsTask();
            try
            {
                await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > before);
                Assert.True(context.MemoryGovernor.CurrentCommittedBytes > committed);
                if (completion == "cancel") source.Release.SetCanceled();
                else source.Release.SetResult(completion == "error");
                if (completion == "success")
                {
                    var result = Assert.IsType<PyList>(await pending);
                    Assert.Same(value, Assert.Single(result));
                    Assert.True(budget.ReservedBytes > before);
                    Assert.True(context.MemoryGovernor.CurrentCommittedBytes > committed);
                }
                else
                {
                    if (completion == "error") await Assert.ThrowsAsync<InvalidOperationException>(() => pending);
                    else await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
                    Assert.Equal(before, budget.ReservedBytes);
                    Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
                }
                Assert.Equal(1, source.CleanupCalls);
                Assert.Equal(new string('x', 4096), value.AsString());
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            finally
            {
                source.Release.TrySetResult(false);
                try { await pending.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (InvalidOperationException) when (completion == "error") { }
                catch (OperationCanceledException) when (completion == "cancel") { }
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static (Context, List<string>) Prepare(ILythonMemoryBudget owner, string shape)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions
        {
            MemoryBudget = owner, MaxExecutionSteps = MaximumSteps,
            MaxCollectionSize = shape == "limit" ? 1 : 100,
        });
        var events = new List<string>();
        var count = shape.Contains("empty", StringComparison.Ordinal) ? 0 : shape.Contains("wide", StringComparison.Ordinal) ? 65 : 2;
        context.Variables["source"] = shape.StartsWith("known", StringComparison.Ordinal)
            ? new PyTuple(Enumerable.Range(0, count).Select(i => (object)new BigInteger(i)).ToArray(), context.MemoryGovernor, Span)
            : new Source(events, count, shape is "move" or "both", shape is "cleanup" or "both");
        context.Variables["list"] = context.State.BuiltinVariables["list"];
        context.Limits.ExecutionStepCount = 0;
        return (context, events);
    }

    private static ValueTask<object?> Run(Context context, bool asynchronous)
        => asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(Code, context)
            : ValueTask.FromResult(LythonRuntime.ExecuteExecutableCodeObject(Code, context, default, null));

    private static string Describe(Exception error)
        => error is LythonRuntimeException python
            ? $"{python.ExceptionType}:{python.Message}:{python.Span?.Start},{python.Span?.Length},{python.Span?.Line},{python.Span?.Column}:{python.Attribution?.Reason}"
            : error.GetType().Name + ":" + error.Message;

    private static void AssertUnwound(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class Source(List<string> events, int count, bool moveFailure, bool cleanupFailure) : IPyAsyncIterableValue
    {
        public IEnumerable<object> Iterate()
        {
            events.Add("open");
            try
            {
                for (var i = 0; i < count; i++) { events.Add("pull:" + i); yield return new BigInteger(i); }
                events.Add("end");
                if (moveFailure) throw new InvalidOperationException("move");
            }
            finally
            {
                events.Add("close");
                if (cleanupFailure) throw new InvalidOperationException("cleanup");
            }
        }
        public async IAsyncEnumerable<object> IterateAsync()
        {
            await Task.CompletedTask;
            foreach (var value in Iterate()) yield return value;
        }
    }

    private sealed class PendingCleanup(object value) : IPyAsyncIterableValue
    {
        internal int CleanupCalls;
        internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<bool> Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IEnumerable<object> Iterate() { yield return value; }
        public IAsyncEnumerable<object> IterateAsync() => new Sequence(this, value);

        private sealed class Sequence(PendingCleanup owner, object value) : IAsyncEnumerable<object>, IAsyncEnumerator<object>
        {
            private bool _yielded;
            public object Current => value;
            public IAsyncEnumerator<object> GetAsyncEnumerator(CancellationToken cancellationToken = default) => this;
            public ValueTask<bool> MoveNextAsync()
            {
                var next = !_yielded;
                _yielded = true;
                return ValueTask.FromResult(next);
            }
            public async ValueTask DisposeAsync()
            {
                owner.CleanupCalls++;
                owner.Entered.SetResult();
                if (await owner.Release.Task.ConfigureAwait(false)) throw new InvalidOperationException("cleanup");
            }
        }
    }
}

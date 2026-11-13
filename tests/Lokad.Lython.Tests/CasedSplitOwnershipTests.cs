using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class CasedSplitOwnershipTests
{
    [Theory]
    [InlineData(0), InlineData(1), InlineData(2)]
    public void LongWordPublicationAfterStopKeepsRetainedBackingFunded(int failureKind)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new()
            { MemoryBudget = hook, CancellationToken = cancellation.Token });
            var span = new LythonSourceSpan(0, 1, 1, 1);
            var text = string.Concat(Enumerable.Repeat("é😀ß", 256));
            var expected = string.Concat(Enumerable.Repeat("É😀SS", 256));
            var input = PyString.FromString(text + " " + text, context.MemoryGovernor, span);
            using var cursor = PyStringOps.PrepareCasedWhitespaceIteration(input, true, context, span);
            Assert.True(cursor.TryReadWord(out var first));
            var retainedFirst = Assert.IsType<PyString>(cursor.Materialize(ref first));
            Assert.True(cursor.TryReadWord(out var last));
            LythonRuntimeException? failure = null;
            if (failureKind == 1) cancellation.Cancel();
            if (failureKind == 2)
                failure = context.MemoryGovernor.RefuseAdmission("controlled word refusal", 777, span);
            // Publishing the final loop binding during unwind may neither admit
            // new storage nor replace the cancellation/first refusal.
            hook.RefuseGrowth = _ => throw new InvalidOperationException("Late word admission.");
            var retainedLast = Assert.IsType<PyString>(cursor.Materialize(ref last));
            cursor.Dispose();
            context.State.CloseResources();
            var ended = budget.ReservedBytes;
            cursor.Dispose();
            Assert.Equal(ended, budget.ReservedBytes);
            Assert.NotSame(retainedFirst, retainedLast);
            Assert.Equal(expected, retainedFirst.AsString());
            Assert.Equal(expected, retainedLast.AsString());
            Assert.Equal(text + " " + text, input.AsString());
            Assert.True(System.Runtime.InteropServices.MemoryMarshal.TryGetArray(retainedLast.Utf8Bytes, out var backing));
            Assert.True(budget.ReservedBytes >= backing.Array!.LongLength
                + 2 * PyString.EstimateApproximateBytes(0) + input.CommittedOwnedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            if (failure is not null) Assert.Same(failure, context.MemoryGovernor.AdmissionFailure);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false), InlineData(true, false)]
    [InlineData(false, true), InlineData(true, true)]
    public void InterruptedRangeGrowthKeepsInputAndReturnsOwnedCapacity(bool upper, bool cancel)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new()
            { MemoryBudget = hook, CancellationToken = cancellation.Token });
            var span = new LythonSourceSpan(0, 1, 1, 1);
            var text = string.Join(' ', Enumerable.Repeat("Αß", 65));
            var input = PyString.FromString(text, context.MemoryGovernor, span);
            var baseline = budget.ReservedBytes;
            var reached = false;
            hook.RefuseGrowth = bytes =>
            {
                if (bytes < MemoryGovernor.WorkspaceBytes(32)) return false;
                reached = true;
                return !cancel;
            };
            hook.AfterGrowth = bytes =>
            {
                if (bytes >= MemoryGovernor.WorkspaceBytes(32)) cancellation.Cancel();
            };
            var error = Assert.Throws<LythonRuntimeException>(() =>
                PyStringOps.PrepareCasedWhitespaceIteration(input, upper, context, span));
            Assert.True(reached);
            Assert.Equal(cancel ? LythonFailureReason.Cancelled : LythonFailureReason.SharedMemoryAllowance,
                error.Attribution?.Reason);
            if (!cancel) Assert.Same(error, context.MemoryGovernor.AdmissionFailure);
            Assert.Equal(text, input.AsString());
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.True(budget.ReservedBytes > baseline); // returned inventory remains owned
            context.State.CloseResources();
            Assert.Equal(baseline, budget.ReservedBytes);
            Assert.Equal(text, input.AsString());
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void CancelledHelperReturnRelinquishesItsPreparedRangeLoan()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        using (var cancellation = new CancellationTokenSource())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new()
            { MemoryBudget = owner, CancellationToken = cancellation.Token });
            var span = new LythonSourceSpan(0, 1, 1, 1);
            var definition = Assert.IsType<LoweredFunctionDefinitionStatement>(Assert.Single(
                LoweredScript.Lower(LythonFrontend.Compile("def words(value):return value.lower().split()").Script!).Statements));
            var code = ExecutableScript.CompileClassFunction(definition);
            var function = new PyExecutableFunction("words", definition.ParameterLayout, code, context, [],
                new Dictionary<string, object>(), definition.ScopeFacts);
            var input = PyString.FromString("A ß C D E F G H I J K", context.MemoryGovernor, span);
            using var cursor = PyStringOps.PrepareCasedWhitespaceIteration(input, true, context, span);
            var index = Array.FindIndex(code.Instructions, instruction => instruction.OpCode == ExecutableOpCode.Return);
            var source = code.Instructions[index].InputStart;
            var values = new object[code.ValueStorageCapacity];
            values[source] = cursor;
            // Model interruption after the real fused kernel has completed but
            // before Return publishes its private value, without a timer race.
            var execution = new LythonRuntime.NativeExecution(function, values, true)
            { Index = index, LiveExtent = source + 1 };
            context.State.CloseResources();
            var active = budget.ReservedBytes;
            cancellation.Cancel();
            try
            {
                try { execution.Run(span, out _); Assert.Fail("Cancelled return completed."); }
                catch (LythonRuntimeException error)
                { Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason); }
            }
            finally { execution.Dispose(); }
            Assert.True(budget.ReservedBytes < active);
            var ended = budget.ReservedBytes;
            cursor.Dispose();
            Assert.Equal(ended, budget.ReservedBytes);
            Assert.Equal(0, context.State.CurrentInterpreterDepth);
            Assert.Equal(0, context.State.CurrentRecursionDepth);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void PreparedRangesKeepOverlappingLoansFundedThroughReuseShutdown()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = owner });
            var span = new LythonSourceSpan(0, 1, 1, 1);
            var input = PyString.FromString(string.Join(' ', Enumerable.Repeat("A ß C D E F G H I J K", 7)),
                context.MemoryGovernor, span);
            using var first = PyStringOps.PrepareCasedWhitespaceIteration(input, true, context, span);
            var beforeSecond = budget.ReservedBytes;
            using var second = PyStringOps.PrepareCasedWhitespaceIteration(input, true, context, span);
            Assert.True(budget.ReservedBytes > beforeSecond);
            Assert.Equal("A", Assert.IsType<PyString>(first.MoveNext().Value).AsString());
            Assert.Equal("A", Assert.IsType<PyString>(second.MoveNext().Value).AsString());
            first.Dispose();
            var idleAndActive = budget.ReservedBytes;
            context.State.CloseResources();
            Assert.True(budget.ReservedBytes < idleAndActive);
            var active = budget.ReservedBytes;
            Assert.Equal("SS", Assert.IsType<PyString>(second.MoveNext().Value).AsString());
            second.Dispose();
            Assert.True(budget.ReservedBytes < active);
            var ended = budget.ReservedBytes;
            first.Dispose();
            second.Dispose();
            context.State.CloseResources();
            Assert.Equal(ended, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void EagerWordRefusalReturnsPreparedStorageWithoutPublishingCursor()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new() { MemoryBudget = hook });
            var span = new LythonSourceSpan(0, 1, 1, 1);
            var input = PyString.FromString("A " + new string('B', 20000), context.MemoryGovernor, span);
            var baseline = budget.ReservedBytes;
            hook.RefuseGrowth = bytes => bytes >= 20000;
            var first = Assert.Throws<LythonRuntimeException>(() =>
                PyStringOps.PrepareCasedWhitespaceIteration(input, false, context, span));
            Assert.Same(first, context.MemoryGovernor.AdmissionFailure);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, first.Attribution?.Reason);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            Assert.True(budget.ReservedBytes > baseline); // returned range cache is still owned
            context.State.CloseResources();
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, 0), InlineData(true, 0)]
    [InlineData(false, 1), InlineData(true, 1)]
    [InlineData(false, 2), InlineData(true, 2)]
    public async Task ImmediateLoopsAdmitAllWordsBeforeBodyEffects(bool asynchronous, int failureKind)
    {
        foreach (var (definition, iterable) in new[]
        {
            ("def words(value):return value.lower().split()\n", "input.lower().split()"),
            ("def words(value):return value.lower().split()\n", "words(input)"),
            ("def words(value):return value.lower().split()\n", "words(value=input)"),
            ("def outer():\n count=0\n def words(value):\n  nonlocal count\n  count+=1\n  return value.lower().split()\n return words\nwords=outer()\n", "words(input)"),
            ("def outer():\n count=0\n def words(value):\n  nonlocal count\n  count+=1\n  return value.lower().split()\n def forward(value):return words(value=value)\n return forward\nwords=outer()\n", "words(input)"),
        })
        {
            var source = definition + "for word in " + iterable + ":\n effects.append(word)";
            var frontend = LythonFrontend.Compile(source);
            Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
            var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script.RequireNotNull())).EntryPoint;
            var budget = new LythonMemoryBudget(1024 * 1024);
            using var cancellation = new CancellationTokenSource();
            using (var owner = budget.CreateScope())
            {
                var hook = new AdmissionHookMemoryBudget(owner);
                var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new()
                { MemoryBudget = hook, CancellationToken = cancellation.Token });
                var origin = new LythonSourceSpan(120, 5, 12, 3);
                var input = PyString.FromString("A " + new string('B', 20000), context.MemoryGovernor, origin);
                var effects = new PyList(0, context.MemoryGovernor, origin);
                context.Variables["input"] = input;
                context.Variables["effects"] = effects;
                var fundedInput = budget.ReservedBytes;
                var reached = false;
                LythonRuntimeException? first = null;
                hook.AfterGrowth = bytes =>
                {
                    if (bytes < input.Utf8Bytes.Length) return;
                    hook.AfterGrowth = null;
                    reached = true;
                    Assert.Empty(effects);
                    Assert.True(budget.ReservedBytes >= fundedInput + input.Utf8Bytes.Length);
                    Assert.True(context.State.CurrentInterpreterDepth > 0);
                    if (failureKind == 1) cancellation.Cancel();
                    if (failureKind == 2)
                        first = context.MemoryGovernor.RefuseAdmission("controlled split iteration refusal", 777, origin);
                };
                try
                {
                    async Task Execute()
                    {
                        if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
                        else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
                    }
                    if (failureKind == 0)
                    {
                        await Execute();
                        Assert.Equal("a", Assert.IsType<PyString>(effects[0]).AsString());
                        Assert.Equal(new string('b', 20000), Assert.IsType<PyString>(effects[1]).AsString());
                    }
                    else
                    {
                        var error = await Assert.ThrowsAsync<LythonRuntimeException>(Execute);
                        Assert.Empty(effects);
                        if (failureKind == 1) Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
                        else { Assert.Same(first, error); Assert.Same(first, context.MemoryGovernor.AdmissionFailure); }
                    }
                    Assert.True(reached);
                    Assert.True(budget.ReservedBytes >= fundedInput);
                    Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                    Assert.Null(context.CurrentExecutableFrame);
                    Assert.Equal(0, context.State.CurrentInterpreterDepth);
                    Assert.Equal(0, context.State.CurrentRecursionDepth);
                }
                finally { context.State.CloseResources(); }
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false, 0), InlineData(true, 0)]
    [InlineData(false, 1), InlineData(true, 1)]
    [InlineData(false, 2), InlineData(true, 2)]
    public async Task OrdinaryChainsPreserveInputFundingAndTerminalUnwind(bool asynchronous, int failureKind)
    {
        foreach (var source in new[]
        {
            "effects.append(input.lower().split())",
            "def words(value):\n if value:\n  result=value.upper().split()\n  return result\n return []\neffects.append(words(input))",
            "def words(value):\n try:\n  return value.lower().split()\n except MemoryError:\n  return ['suppressed']\n finally:\n  pass\neffects.append(words(input))",
        }.SelectMany(source => new[] { source, source.Replace(".split()", ".rsplit()") }))
        {
            var frontend = LythonFrontend.Compile(source);
            Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
            var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script.RequireNotNull())).EntryPoint;
            var budget = new LythonMemoryBudget(1024 * 1024);
            using var cancellation = new CancellationTokenSource();
            using (var owner = budget.CreateScope())
            {
                var hook = new AdmissionHookMemoryBudget(owner);
                var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new()
                { MemoryBudget = hook, CancellationToken = cancellation.Token });
                var origin = new LythonSourceSpan(120, 5, 12, 3);
                var input = PyString.FromString(new string('A', 20000) + " B", context.MemoryGovernor, origin);
                var effects = new PyList(0, context.MemoryGovernor, origin);
                context.Variables["input"] = input;
                context.Variables["effects"] = effects;
                var fundedInput = budget.ReservedBytes;
                var reached = false;
                LythonRuntimeException? first = null;
                hook.AfterGrowth = bytes =>
                {
                    if (bytes < input.Utf8Bytes.Length) return;
                    hook.AfterGrowth = null;
                    reached = true;
                    Assert.True(budget.ReservedBytes >= fundedInput + input.Utf8Bytes.Length);
                    Assert.True(context.State.CurrentInterpreterDepth > 0);
                    if (failureKind == 1) cancellation.Cancel();
                    if (failureKind == 2)
                        first = context.MemoryGovernor.RefuseAdmission("controlled cased-split refusal", 777, origin);
                };
                try
                {
                    async Task Execute()
                    {
                        if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context);
                        else LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
                    }
                    if (failureKind == 0)
                    {
                        await Execute();
                        var words = Assert.IsType<PyList>(Assert.Single(effects));
                        Assert.Equal(2, words.Count);
                        Assert.Equal(20000, Assert.IsType<PyString>(words[0]).Length);
                    }
                    else
                    {
                        var error = await Assert.ThrowsAsync<LythonRuntimeException>(Execute);
                        Assert.Empty(effects);
                        if (failureKind == 1) Assert.Equal(LythonFailureReason.Cancelled, error.Attribution?.Reason);
                        else
                        {
                            Assert.Same(first, error);
                            Assert.Same(first, context.MemoryGovernor.AdmissionFailure);
                            Assert.Equal(origin, error.Span);
                            Assert.Equal(777L, error.Attribution?.Requested);
                        }
                    }
                    Assert.True(reached);
                    Assert.Equal(new string('A', 20000) + " B", input.AsString());
                    Assert.True(budget.ReservedBytes >= fundedInput);
                    Assert.Null(context.CurrentExecutableFrame);
                    Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
                    Assert.Equal(0, context.State.CurrentInterpreterDepth);
                    Assert.Equal(0, context.State.CurrentRecursionDepth);
                }
                finally { context.State.CloseResources(); }
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}

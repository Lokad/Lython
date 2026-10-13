using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class FrameStorageEdgesTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Fact]
    public void LexicalCyclesDoNotInitializeLazyNamespacesOrAllocateDuringWalking()
    {
        var payload = PyString.FromString("payload");
        var parent = new ExecutionFrame(null, new() { ["value"] = payload });
        var lazy = new ExecutionFrame(parent);
        parent.Variables["cycle"] = lazy;
        var variables = typeof(ExecutionFrame).GetField("_variables", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Null(variables.GetValue(lazy));
        using var walk = new MemoryStorageWalk(new MemoryGovernor(16000), 64);
        Assert.True(walk.TryTraverse([lazy]));
        var before = GC.GetAllocatedBytesForCurrentThread();
        Assert.True(walk.TryTraverse([lazy]));
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Null(variables.GetValue(lazy));
        Assert.Equal(1, walk.Visited.ToArray().Count(value => ReferenceEquals(value, payload)));
        Assert.Equal(3, walk.Visited.Length);
    }

    [Fact]
    public void NestedComprehensionsKeepSavedClosuresAndWithdrawOnlyTheirOwnBindings()
    {
        var context = Context();
        var outerValue = PyString.FromString("outer");
        var innerValue = PyString.FromString("inner");
        using var walk = new MemoryStorageWalk(context.MemoryGovernor, 256);
        using (var outer = context.EnterComprehension(ScopeDirectiveFacts.Empty))
        {
            outer.Bindings.Variables["outer"] = outerValue;
            using (var inner = context.EnterComprehension(ScopeDirectiveFacts.Empty))
            {
                inner.Bindings.Variables["inner"] = innerValue;
                Assert.True(walk.TryTraverse([context]));
                Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, outerValue));
                Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, innerValue));
            }
            Assert.True(walk.TryTraverse([context]));
            Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, outerValue));
            Assert.DoesNotContain(walk.Visited.ToArray(), value => ReferenceEquals(value, innerValue));
        }
        Assert.True(walk.TryTraverse([context]));
        Assert.DoesNotContain(walk.Visited.ToArray(), value => ReferenceEquals(value, outerValue));
    }

    [Fact]
    public void CellsExposeRawAndMirroredReferencesWithoutReadingValue()
    {
        var context = Context();
        var stored = PyString.FromString("stored");
        var mirrored = PyString.FromString("mirrored");
        context.MirrorLocalStores = true;
        context.Variables["value"] = mirrored;
        var cell = new LythonRuntime.ExecutableCell(stored, context, "value");
        Assert.Same(mirrored, cell.Value);
        using var walk = new MemoryStorageWalk(context.MemoryGovernor, 64);
        Assert.True(walk.TryTraverse([cell]));
        Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, stored));
        Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, mirrored));
    }

    [Fact]
    public void FrameSlotsIncludeUnnamedAliasesAndBothCellArrays()
    {
        var code = Compile("return None\n").EntryPoint;
        var hidden = PyString.FromString("hidden");
        var local = PyString.FromString("local");
        var closed = PyString.FromString("closed");
        var frame = new LythonRuntime.ExecutableFrameState(code, [hidden, hidden],
            [new(local), null], new List<LythonRuntime.ExecutableCell> { new(closed) });
        var context = Context();
        context.EnterExecutableSlots(frame);
        using var walk = new MemoryStorageWalk(context.MemoryGovernor, 256);
        Assert.True(walk.TryTraverse([context]));
        foreach (var payload in new[] { hidden, local, closed })
            Assert.Equal(1, walk.Visited.ToArray().Count(value => ReferenceEquals(value, payload)));
        context.LeaveExecutableSlots(null);
        Assert.True(walk.TryTraverse([context]));
        Assert.DoesNotContain(walk.Visited.ToArray(), value => ReferenceEquals(value, hidden));
    }

    [Fact]
    public void EmptyUninitializedSlotsStillConsumeTheWorkBudget()
    {
        var sentinel = typeof(LythonRuntime).GetField("UninitializedLocal", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var frame = new LythonRuntime.ExecutableFrameState(Compile("return None\n").EntryPoint,
            Enumerable.Repeat(sentinel, 10000).ToArray(), null, null);
        using var walk = new MemoryStorageWalk(new MemoryGovernor(16000), 32);
        Assert.False(walk.TryTraverse([frame]));
        Assert.Equal(MemoryStorageWalk.IncompleteReason.WorkspaceLimit, walk.Incomplete);
        Assert.True(walk.TryTraverse([new LythonRuntime.ExecutableCell(sentinel)]));
    }

    [Fact]
    public void RuntimeExceptionTraversesPayloadArgsAndChainsButStopsAtHostExceptions()
    {
        var payload = PyString.FromString("payload");
        var python = new PyException("ValueError", "message", payload);
        var error = new LythonRuntimeException("ValueError", "message", Span, null, payload)
        {
            OriginalPythonException = python,
            PythonCause = python,
            PythonContext = python,
            PythonExplicitArgs = new PyTuple([payload, python])
        };
        using var walk = new MemoryStorageWalk(new MemoryGovernor(16000), 64);
        Assert.True(walk.TryTraverse([error]));
        Assert.Equal(1, walk.Visited.ToArray().Count(value => ReferenceEquals(value, payload)));
        var hostError = new LythonRuntimeException("RuntimeError", "host", Span, new HostileException(), payload);
        Assert.False(walk.TryTraverse([hostError]));
        Assert.Equal(typeof(HostileException), walk.UnsupportedType);
    }

    [Fact]
    public void FunctionDefaultsAndMetadataTraverseTheActualClosureCycle()
    {
        var context = Context();
        var payload = PyString.FromString("default");
        var metadata = PyString.FromString("metadata");
        var function = new PyFunction("f", Array.Empty<LoweredFunctionParameter>(), Array.Empty<LoweredStatement>(),
            context, new() { ["value"] = payload }, ScopeDirectiveFacts.Empty);
        function.TrySetMember("tag", metadata);
        context.Variables["f"] = function;
        var committed = context.MemoryGovernor.CurrentCommittedBytes;
        using var walk = new MemoryStorageWalk(context.MemoryGovernor, 256);
        Assert.True(walk.TryTraverse([function]));
        Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, payload));
        Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, metadata));
        Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public void DescribedCodeBindingsTraverseNestedDefinitions()
    {
        var frame = new LythonRuntime.ExecutableFrameState(Compile("def f():\n    return None\n").EntryPoint, [], null, null);
        using var walk = new MemoryStorageWalk(new MemoryGovernor(16000), 256);
        Assert.True(walk.TryTraverse([frame]));
        Assert.Contains(walk.Visited.ToArray(), value => value is ExecutableFunctionBinding);
    }

    [Fact]
    public void OpaqueClosureCollectionsAreNotInvokedToDiscoverTheirContents()
    {
        var frame = new LythonRuntime.ExecutableFrameState(Compile("return None\n").EntryPoint,
            [], null, new HostileCells());
        using var walk = new MemoryStorageWalk(new MemoryGovernor(16000), 256);
        Assert.False(walk.TryTraverse([frame]));
        Assert.Equal(typeof(HostileCells), walk.UnsupportedType);
    }

    [Fact]
    public void PersistentRootsIncludeCacheKeysTargetsValuesArgsAndOpenWriters()
    {
        var state = new ExecutionState(new MockLythonHost(), new LythonRunOptions { Args = ["argv"] }, new());
        var key = PyString.FromString("site");
        var target = PyString.FromString("target");
        var value = PyString.FromString("cached");
        var output = PyString.FromString("writer");
        var writer = new Writer(output);
        state.WriteRuntimeMemberCache(key, target, value);
        state.TrackOpenFileWriter(writer);
        var committed = state.MemoryGovernor.CurrentCommittedBytes;
        using var walk = new MemoryStorageWalk(state.MemoryGovernor, 256);
        Assert.True(walk.TryTraverse([state]));
        foreach (var payload in new[] { key, target, value, output, state.Args[0] })
            Assert.Contains(walk.Visited.ToArray(), item => ReferenceEquals(item, payload));
        var before = GC.GetAllocatedBytesForCurrentThread();
        Assert.True(walk.TryTraverse([state]));
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(committed, state.MemoryGovernor.CurrentCommittedBytes);
        state.UntrackOpenFileWriter(writer);
        Assert.True(walk.TryTraverse([state]));
        Assert.DoesNotContain(walk.Visited.ToArray(), item => ReferenceEquals(item, output));
    }

    [Fact]
    public void IdentityEphemeronsStopCompletionWithoutRootingTheirWeakKeys()
    {
        var state = new ExecutionState(new MockLythonHost(), new LythonRunOptions(), new());
        var key = PyString.FromString("identity key");
        _ = state.GetObjectId(key);
        var committed = state.MemoryGovernor.CurrentCommittedBytes;
        using var walk = new MemoryStorageWalk(state.MemoryGovernor, 256);
        Assert.False(walk.TryTraverse([state]));
        Assert.Equal(typeof(ConditionalWeakTable<object, StrongBox<long>>), walk.UnsupportedType);
        Assert.DoesNotContain(walk.Visited.ToArray(), value => ReferenceEquals(value, key));
        Assert.Equal(committed, state.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public void ActiveStructuralPairsRemainStoredRootsWithoutRunningEquality()
    {
        var context = Context();
        var first = PyString.FromString("first");
        var second = PyString.FromString("second");
        using var walk = new MemoryStorageWalk(context.MemoryGovernor, 64);
        using (PyStructuralGuard.EnterPair(first, second, Span, context))
        {
            Assert.True(walk.TryTraverse([context.State.StructuralTraversal]));
            Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, first));
            Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, second));
        }
        Assert.True(walk.TryTraverse([context.State.StructuralTraversal]));
        Assert.Equal(1, walk.Visited.Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FinallyKeepsThePendingReturnAsAStoredChild(bool asynchronous)
    {
        var context = Context();
        var probe = new Probe();
        context.Variables["probe"] = probe;
        var code = Compile("try:\n    return 'x' * 16000\nfinally:\n    probe()\n");
        var result = asynchronous
            ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context)
            : LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, [], null);
        Assert.Equal(16000, Assert.IsType<PyString>(result).Utf8Bytes.Length);
        Assert.Equal(1, probe.Checked);
        Assert.Null(context.CurrentExecutableFrame);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ZeroLocalFailureRestoresThePreviouslyPublishedFrame(bool asynchronous, bool cancellation)
    {
        var context = Context();
        var previous = new LythonRuntime.ExecutableFrameState(Compile("return None\n").EntryPoint, [], null, null);
        context.EnterExecutableSlots(previous);
        context.Variables["probe"] = new FailingProbe(previous, cancellation);
        var code = Compile("return probe()\n");
        async Task Run()
        {
            if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context);
            else LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, [], null);
        }
        if (cancellation) await Assert.ThrowsAnyAsync<OperationCanceledException>(Run);
        else await Assert.ThrowsAsync<LythonRuntimeException>(Run);
        Assert.Same(previous, context.CurrentExecutableFrame);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualOperandStackRemainsVisibleAcrossNativeSuspension(bool asynchronous)
    {
        var context = Context();
        var probe = new Probe();
        context.Variables["probe"] = probe;
        var code = Compile("return probe('x' * 16000)\n");
        if (asynchronous) await LythonRuntime.ExecuteExecutableCodeObjectAsync(code.EntryPoint, context);
        else LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, [], null);
        Assert.Equal(1, probe.Checked);
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private static LythonRuntime.ExecutionContext Context()
    {
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
        context.Variables.Clear();
        return context;
    }

    private static ExecutableScript Compile(string source)
    {
        var frontend = LythonFrontend.Compile(source);
        Assert.Empty(frontend.Diagnostics);
        return ExecutableScript.Compile(LoweredScript.Lower(frontend.Script!));
    }

    private sealed class Probe : LythonRuntime.ICallable, IMemoryStorageEdges
    {
        internal int Checked;
        void IMemoryStorageEdges.VisitStorageChildren(MemoryStorageWalk visitor) { }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            var frame = Assert.IsAssignableFrom<IMemoryStorageEdges>(context.CurrentExecutableFrame);
            var payload = arguments.Length == 0 ? null : Assert.IsType<PyString>(arguments[0].Value);
            var committed = context.MemoryGovernor.CurrentCommittedBytes;
            using var walk = new MemoryStorageWalk(context.MemoryGovernor, 512);
            Assert.True(walk.TryTraverse([frame]));
            if (payload is not null) Assert.Contains(walk.Visited.ToArray(), value => ReferenceEquals(value, payload));
            else Assert.Contains(walk.Visited.ToArray(), value => value is PyString { Utf8Bytes.Length: 16000 });
            Assert.Equal(committed, context.MemoryGovernor.CurrentCommittedBytes);
            Checked++;
            return PyNone.Instance;
        }
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            await Task.Yield();
            return Invoke(arguments, span, context);
        }
    }

    private sealed class HostileException : Exception
    {
        public override string Message => throw new InvalidOperationException("host message getter");
    }

    private sealed class FailingProbe(LythonRuntime.ExecutableFrameState previous, bool cancellation) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            Assert.NotNull(context.CurrentExecutableFrame);
            Assert.NotSame(previous, context.CurrentExecutableFrame);
            if (cancellation) throw new OperationCanceledException();
            throw new LythonRuntimeException("ValueError", "failure", span);
        }
        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, LythonRuntime.ExecutionContext context)
        {
            await Task.Yield();
            return Invoke(arguments, span, context);
        }
    }

    private sealed class HostileCells : IReadOnlyList<LythonRuntime.ExecutableCell>
    {
        public int Count => throw new InvalidOperationException("host count");
        public LythonRuntime.ExecutableCell this[int index] => throw new InvalidOperationException("host index");
        public IEnumerator<LythonRuntime.ExecutableCell> GetEnumerator() => throw new InvalidOperationException("host iterator");
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class Writer(object payload) : IExecutionFileWriter, IMemoryStorageEdges
    {
        void IMemoryStorageEdges.VisitStorageChildren(MemoryStorageWalk visitor) => visitor.Add(payload);
        public object Exit(LythonRuntime.ExecutionContext context) => throw new InvalidOperationException("publication");
        public ValueTask<object> ExitAsync(LythonRuntime.ExecutionContext context) => throw new InvalidOperationException("async publication");
    }
}

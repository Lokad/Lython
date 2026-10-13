using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Lokad.Lython.Runtime.Text;
using Xunit.Abstractions;

namespace Lokad.Lython.Tests;

public sealed class ProvedStringAdmissionTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("held = None")]
    [InlineData("held = []")]
    public void ReleasedBindingAllowsReplacementAfterCaughtDenial(string withdrawal)
    {
        var source = "held = 'x' * 16000\ntry:\n    candidate = 'y' * 16000\nexcept MemoryError:\n    pass\n" +
            withdrawal + "\ncandidate = 'z' * 16000\nreturn len(candidate)\n";
        var frontend = LythonFrontend.Compile(source);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script.RequireNotNull()));
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions { MaxExecutionMemoryBytes = 28000 });
        output.WriteLine("ops: " + string.Join(",", code.EntryPoint.Blocks.SelectMany(b => b.InstructionArray).Select(i => i.OpCode)));
        var before = GC.CollectionCount(2);
        object? result = null;
        var error = Record.Exception(() => result = LythonRuntime.ExecuteExecutableCodeObject(code.EntryPoint, context, default, [], null));
        output.WriteLine("locals: " + string.Join(",", code.EntryPoint.LocalNames));
        output.WriteLine($"error={error?.Message}; heldPresent={context.Variables.ContainsKey("held")}; Gen2={GC.CollectionCount(2)-before}; committed={context.MemoryGovernor.CurrentCommittedBytes}");
        GC.Collect();
        output.WriteLine($"post-return sweep={context.State.CallTemporaries.Sweep(full: true)}; committed={context.MemoryGovernor.CurrentCommittedBytes}");
        Assert.Null(error);
        Assert.Equal(new System.Numerics.BigInteger(16000), result);
        GC.KeepAlive(context);
    }

    [Fact]
    public void ProofCountsOneFundedPayloadAcrossAliasesAndOneLevelLists()
    {
        var governor = new MemoryGovernor(48000);
        var pool = new ChargeReclamationPool(governor);
        governor.ReclamationPool = pool;
        var held = PyString.FromString(new string('x', 16000), governor);
        pool.TrackFreshString(held);
        var frame = new ExecutionFrame(null, new() { ["first"] = held, ["alias"] = held,
            ["list"] = new PyList([held, held]) });
        var remaining = 64;
        Assert.Equal(16000, frame.KnownLiveStringBytes(governor, ref remaining));
        Assert.InRange(remaining, 1, 63);
        remaining = 0;
        Assert.Equal(0, frame.KnownLiveStringBytes(governor, ref remaining));
        remaining = 64;
        Assert.Equal(0, frame.KnownLiveStringBytes(new MemoryGovernor(48000), ref remaining));
        GC.KeepAlive(frame);
    }

    [Fact]
    public void ProofDoesNotInitializeANamespaceOrTraverseCycles()
    {
        var governor = new MemoryGovernor(48000);
        var frame = new ExecutionFrame(new ExecutionFrame(null, new()));
        var remaining = 64;
        Assert.Equal(0, frame.KnownLiveStringBytes(governor, ref remaining));
        var cycle = new PyList([]);
        cycle.Add(cycle);
        remaining = 64;
        Assert.Equal(0, ExecutionFrame.KnownLiveStringBytes(cycle, governor, ref remaining));
        Assert.Equal(62, remaining);
    }

    [Fact]
    public void RefundedUnpublishedStringSuppliesNoProof()
    {
        var governor = new MemoryGovernor(48000);
        var pool = new ChargeReclamationPool(governor);
        var held = PyString.FromString(new string('x', 16000), governor);
        pool.TrackFreshString(held);
        pool.RefundUnpublishedValue(held);
        var remaining = 64;
        Assert.Equal(0, ExecutionFrame.KnownLiveStringBytes(held, governor, ref remaining));
        GC.KeepAlive(held);
    }

    [Fact]
    public void ExecutableProofUsesEffectiveCellsInsteadOfStaleLocals()
    {
        var governor = new MemoryGovernor(48000);
        var pool = new ChargeReclamationPool(governor);
        var held = PyString.FromString(new string('x', 16000), governor);
        pool.TrackFreshString(held);
        var frontend = LythonFrontend.Compile("held = None\n");
        var code = ExecutableScript.Compile(LoweredScript.Lower(frontend.Script.RequireNotNull())).EntryPoint;
        Assert.Single(code.LocalNames);
        var cell = new LythonRuntime.ExecutableCell(held);
        var frame = new LythonRuntime.ExecutableFrameState(code, [held], [cell], null);
        var remaining = 64;
        Assert.Equal(16000, frame.KnownLiveStringBytes(governor, ref remaining));
        cell.Value = PyNone.Instance;
        remaining = 64;
        Assert.Equal(0, frame.KnownLiveStringBytes(governor, ref remaining));
        GC.KeepAlive(frame);
    }

    [Fact]
    public void ExistingNamespaceProofRequiresNoAllocation()
    {
        var governor = new MemoryGovernor(48000);
        var pool = new ChargeReclamationPool(governor);
        var held = PyString.FromString(new string('x', 16000), governor);
        pool.TrackFreshString(held);
        var frame = new ExecutionFrame(null, new() { ["held"] = held });
        for (var i = 0; i < 100; i++) { var count = 64; frame.KnownLiveStringBytes(governor, ref count); }
        var before = GC.GetAllocatedBytesForCurrentThread();
        var remaining = 64;
        var actual = frame.KnownLiveStringBytes(governor, ref remaining);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(16000, actual);
        Assert.Equal(0, allocated);
        GC.KeepAlive(frame);
    }

    [Fact]
    public void ImpossibleProofPreservesReliefForALaterRealDrop()
    {
        var governor = new MemoryGovernor(28000);
        var pool = new ChargeReclamationPool(governor);
        governor.ReclamationPool = pool;
        var weak = DropAfterProof(governor, pool);
        governor.Reserve(16128, null);
        Assert.False(weak.TryGetTarget(out _));
        governor.ReleaseReserved(16128);
        Assert.Equal(0, pool.Count);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference<PyString> DropAfterProof(MemoryGovernor governor, ChargeReclamationPool pool)
    {
        var held = PyString.FromString(new string('x', 16000), governor);
        pool.TrackFreshString(held);
        var error = Assert.Throws<LythonRuntimeException>(() => governor.EnsureCanReserve(16128, null, 16000));
        Assert.Equal("MemoryError", error.ExceptionType);
        Assert.Equal(16128, governor.LastDeniedReservationBytes);
        GC.KeepAlive(held);
        return new(held);
    }
}

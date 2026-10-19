using System.Runtime.CompilerServices;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

[Collection("AllocationSensitive")]
public sealed class MappingSliceKeyOwnershipTests
{
    private static readonly LythonSourceSpan Span = new(0, 1, 1, 1);
    private static readonly string[] Factories = ["dict()", "Counter()", "defaultdict()", "ChainMap()"];

    public static IEnumerable<object[]> Mappings()
    {
        foreach (var factory in Factories)
        foreach (var asynchronous in new[] { false, true })
            yield return [factory, asynchronous];
    }

    public static IEnumerable<object[]> TemporaryKeys()
    {
        foreach (var mapping in Mappings())
        foreach (var operation in new[] { "lookup", "replace", "delete" })
            yield return [mapping[0], mapping[1], operation];
    }

    [Theory]
    [MemberData(nameof(Mappings))]
    public async Task StoredColonKeyIsOwnedAndEqualReplacementKeepsOriginalIdentity(string factory, bool asynchronous)
    {
        var root = await Define(factory, "mapping[3:5:2]=payload");
        await Invoke(root, asynchronous);
        var first = await ReadKey(root, asynchronous);
        await Invoke(root, asynchronous);
        var second = await ReadKey(root, asynchronous);
        Assert.Same(first, second);
        AssertUnwound(root);
    }

    [Theory]
    [MemberData(nameof(TemporaryKeys))]
    public async Task TemporaryColonKeysStayBatchOwnedWhileMappingIdentityIsPreserved(string factory, bool asynchronous, string operation)
    {
        var body = operation switch
        {
            "lookup" => "mapping[3:5:2]",
            "replace" => "mapping[3:5:2]=payload",
            _ => "del mapping[3:5:2]\n mapping[stored_key]=payload"
        };
        var root = await Define(factory, body, initialize: true);
        await Invoke(root, asynchronous);
        Reclaim(root);
        // Each fresh slice shell remains owned by this finite execution.
        var before = root.MemoryGovernor.CurrentCommittedBytes;
        for (var i = 0; i < 8; i++) await Invoke(root, asynchronous);
        Reclaim(root);
        var after = root.MemoryGovernor.CurrentCommittedBytes;
        Assert.Equal(before + 8 * 64, after);
        Assert.Same(root.Variables["stored_key"], await ReadKey(root, asynchronous));
        AssertUnwound(root);
        GC.KeepAlive(root);
    }

    [Theory]
    [MemberData(nameof(Mappings))]
    public async Task StoredSliceKeepsIdentityAndDropsItsPhysicalReferenceAfterDeletion(string factory, bool asynchronous)
    {
        var root = await Define(factory, "mapping[3:5:2]=payload");
        var weak = StoreAndDropHostReference(root, asynchronous);
        Reclaim(root);
        Assert.True(IsAlive(weak));
        var before = root.MemoryGovernor.CurrentCommittedBytes;
        var erase = Assert.IsAssignableFrom<PyFunctionBase>(root.Variables["erase"]);
        if (asynchronous) await erase.InvokeAsync([], Span, root);
        else erase.Invoke([], Span, root);
        Reclaim(root);
        Assert.False(IsAlive(weak));
        var after = root.MemoryGovernor.CurrentCommittedBytes;
        Assert.True(after < before);
        AssertUnwound(root);
        GC.KeepAlive(root);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<object> StoreAndDropHostReference(Context root, bool asynchronous)
    {
        Invoke(root, asynchronous).GetAwaiter().GetResult();
        var key = ReadKey(root, asynchronous).GetAwaiter().GetResult();
        return new WeakReference<object>(key);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsAlive(WeakReference<object> weak) => weak.TryGetTarget(out _);

    [Theory]
    [MemberData(nameof(Mappings))]
    public async Task DeniedMappingStoreKeepsMappingUnchangedAndKeyBatchOwned(string factory, bool asynchronous)
    {
        var root = await Define(factory, "mapping[3:5:limit()]=payload");
        IDisposable? pin = null;
        long committedBefore = 0;
        root.Variables["limit"] = new Limit((frame, span) =>
        {
            Assert.Same(root.Services, frame.Services);
            Assert.Same(root.MemoryGovernor, frame.MemoryGovernor);
            committedBefore = root.MemoryGovernor.CurrentCommittedBytes;
            pin = root.MemoryGovernor.ReserveTemporary(((LythonMemoryBudget)root.MemoryGovernor.Budget!).LimitBytes!.Value - root.MemoryGovernor.CurrentAccountedBytes - 64, span);
            return root.Variables["step_value"];
        });
        try
        {
            var error = await Assert.ThrowsAsync<LythonRuntimeException>(() => Invoke(root, asynchronous));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.Equal(committedBefore + 64, root.MemoryGovernor.CurrentCommittedBytes);
            Assert.Equal(0, Assert.IsAssignableFrom<IPySizedValue>(root.Variables["mapping"]).Length);
        }
        finally { pin?.Dispose(); }
        AssertUnwound(root);
    }

    private static async Task<Context> Define(string factory, string body, bool initialize = false)
    {
        var root = new Context(new MockLythonHost(), new LythonRunOptions
        {
            SourcePath = "/mapping-slice-ownership.py", MemoryBudget = new LythonMemoryBudget(1024 * 1024)
        });
        var source = "from collections import Counter,defaultdict,ChainMap\nmapping=" + factory + "\npayload=[]\nstep_value=2\n"
            + (initialize ? "stored_key=slice(3,5,2)\nmapping[stored_key]=payload\n" : "")
            + "def read_key():return next(iter(mapping))\ndef erase():del mapping[3:5:2]\ndef run():\n " + body + "\n";
        var frontend = LythonFrontend.Compile(source);
        Assert.DoesNotContain(frontend.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
        await LythonRuntime.ExecuteStatementsAsync(LoweredScript.Lower(frontend.Script.RequireNotNull()).Statements, root);
        return root;
    }

    private static async Task Invoke(Context root, bool asynchronous)
    {
        var run = Assert.IsAssignableFrom<PyFunctionBase>(root.Variables["run"]);
        if (asynchronous) await run.InvokeAsync([], Span, root);
        else run.Invoke([], Span, root);
    }

    private static async Task<PySlice> ReadKey(Context root, bool asynchronous)
    {
        var read = Assert.IsAssignableFrom<PyFunctionBase>(root.Variables["read_key"]);
        return Assert.IsType<PySlice>(asynchronous ? await read.InvokeAsync([], Span, root) : read.Invoke([], Span, root));
    }

    private static void Reclaim(Context root)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    }

    private static void AssertUnwound(Context root)
    {
        Assert.Equal(0, root.Limits.CurrentRecursionDepth);
        Assert.Equal(0, root.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, root.MemoryGovernor.CurrentReservedBytes);
        Assert.Null(root.CurrentExecutableFrame);
    }

    private sealed class Limit(Func<Context, LythonSourceSpan, object> callback) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] args, LythonSourceSpan span, Context context)
        {
            Assert.Empty(args);
            return callback(context, span);
        }
        public ValueTask<object> InvokeAsync(CallArgumentValue[] args, LythonSourceSpan span, Context context)
            => ValueTask.FromResult(Invoke(args, span, context));
    }
}

using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: the sys.modules snapshot owns its key strings beside the governed
/// dict, and re-added imports commit their keys on every refresh, instead of
/// escaping as invisible CLR heap.
/// </summary>
public sealed class SysModulesAccountingTests
{
    private static PyDict CreateSnapshot(LythonRuntime.ExecutionContext context)
    {
        var type = typeof(LythonRuntime).GetNestedType("SysModule", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("SysModule not found.");
        var method = type.GetMethod("CreateModulesSnapshot", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("CreateModulesSnapshot not found.");
        return Assert.IsType<PyDict>(method.Invoke(null, [context]));
    }

    [Fact]
    public void SnapshotKeysAreOwned()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var snapshot = CreateSnapshot(context);
        Assert.True(snapshot.Count > 50);
        foreach (var key in snapshot.Keys)
        {
            var text = Assert.IsType<PyString>(key);
            Assert.Same(context.MemoryGovernor, text.OwnerMemoryGovernor);
        }
    }

    [Fact]
    public void ReaddedKeysCommit()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        context.State.ImportedModules["helper"] = new ScriptPyModule("helper", new Dictionary<string, object>(), context.MemoryGovernor, null);
        var snapshot = CreateSnapshot(context);
        var key = snapshot.Keys.OfType<PyString>().Single(k => k.AsString() == "helper");
        Assert.Same(context.MemoryGovernor, key.OwnerMemoryGovernor);
    }
}
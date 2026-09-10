using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: importlib specs own the shell plus one attribute slot per member of
/// the fixed table at construction; payloads arrive already owned.
/// </summary>
public sealed class ImportlibSpecAccountingTests
{
    [Fact]
    public void SpecConstructionCommitsExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var type = typeof(LythonRuntime).GetNestedType("ImportlibModuleSpecObject", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ImportlibModuleSpecObject not found.");
        var ctor = type.GetConstructors().Single();
        _ = ctor.Invoke([
            "m",
            PyNone.Instance,
            PyNone.Instance,
            false,
            PyNone.Instance,
            context.MemoryGovernor,
            span]);
        // Shell plus member slots (768) plus the governed name payload (128 + 1).
        Assert.Equal(897L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}

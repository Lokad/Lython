using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: every dictionary reachable through a ChainMap carries a governor, so
/// writes through parents or converted default dicts cannot bypass container
/// accounting. Small sizes ride the governed base charge; past it every entry
/// commits. Ungoverned maps stay free.
/// </summary>
public sealed class ChainMapAccountingTests
{
    private const int EntryCount = 100;

    private static PyChainMap ParentsOf(PyChainMap chainmap)
    {
        Assert.True(chainmap.TryGetMember("parents", out var value));
        return Assert.IsType<PyChainMap>(value);
    }

    private static void WritePairs(PyChainMap chainmap, LythonSourceSpan span)
    {
        for (var i = 0; i < EntryCount; i++)
        {
            chainmap.SetSubscript(PyString.FromString("k" + i), PyString.FromString("v" + i), span);
        }
    }

    private static long CommittedForEntries()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var dict = new PyDict(context.MemoryGovernor, span);
        for (var i = 0; i < EntryCount; i++)
        {
            dict.SetItem(PyString.FromString("k" + i), PyString.FromString("v" + i));
        }

        return context.MemoryGovernor.CurrentCommittedBytes;
    }

    [Fact]
    public void ParentsFallbackCommitsExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        // The parent map is governed but stays empty; only its base charge
        // applies, while the fallback behind parents carries the entries.
        var governed = new PyDict(context.MemoryGovernor, span);
        var governedBase = context.MemoryGovernor.CurrentCommittedBytes;
        var parents = ParentsOf(new PyChainMap([governed]));
        WritePairs(parents, span);
        Assert.Equal(governedBase + CommittedForEntries(), context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void UngovernedParentsStayFree()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var parents = ParentsOf(new PyChainMap([new PyDict()]));
        WritePairs(parents, span);
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
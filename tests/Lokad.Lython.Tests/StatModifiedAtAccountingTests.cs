using System.Numerics;
using Lokad.Lython;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: stat modified_at renders fresh text on every access, so the result
/// is adopted into the caller governor like converted scalar renders.
/// </summary>
public sealed class StatModifiedAtAccountingTests
{
    [Fact]
    public void ModifiedAtOwnsItsResult()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var stat = new LythonPathStat(LythonPathKind.File, new BigInteger(1), DateTimeOffset.UnixEpoch);
        Assert.True(PyMemberAccess.TryResolve(stat, "modified_at", context, span, out var value));
        var text = Assert.IsType<PyString>(value);
        Assert.NotNull(text.OwnerMemoryGovernor);
        Assert.Equal("1970-01-01T00:00:00.0000000+00:00", text.AsString());
    }

    [Fact]
    public void OtherStatMembersStayPut()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var stat = new LythonPathStat(LythonPathKind.File, new BigInteger(1), DateTimeOffset.UnixEpoch);
        Assert.True(PyMemberAccess.TryResolve(stat, "exists", context, span, out var exists));
        Assert.Equal(true, exists);
        Assert.True(PyMemberAccess.TryResolve(stat, "size", context, span, out var size));
        Assert.Equal(new BigInteger(1), size);
        Assert.True(PyMemberAccess.TryResolve(stat, "st_mtime", context, span, out var modified));
        Assert.Equal(0.0, modified);
    }
}
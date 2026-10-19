using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: date/time member and operator results are fresh values, so each one
/// commits a value slot (tuples commit backing storage at the slot rate) like
/// constructed values. Transient scalar projections stay free.
/// </summary>
public sealed class DateTimeValueResultTests
{
    private static LythonRuntime.ExecutionContext NewContext(out LythonSourceSpan span)
    {
        span = new LythonSourceSpan(0, 0, 0, 0);
        return new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
    }

    [Fact]
    public void TimetupleBackingIsOwned()
    {
        var context = NewContext(out var span);
        var date = new PyDate(new DateOnly(2020, 1, 2));
        Assert.True(PyMemberAccess.TryResolve(date, "timetuple", context, span, out var value));
        var bound = Assert.IsAssignableFrom<LythonRuntime.ICallable>(value);
        var result = Assert.IsType<LythonRuntime.TimeStructTimeValue>(bound.Invoke([], span, context));
        Assert.Same(PyNone.Instance, result.Zone);
        Assert.Same(PyNone.Instance, result.GmtOffset);
    }
}
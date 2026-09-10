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
        return new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
    }

    private static long InvokeDelta(LythonRuntime.ExecutionContext context, LythonSourceSpan span, object receiver, string name, params CallArgumentValue[] arguments)
    {
        Assert.True(PyMemberAccess.TryResolve(receiver, name, context, span, out var value));
        var bound = Assert.IsAssignableFrom<LythonRuntime.ICallable>(value);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        _ = bound.Invoke(arguments, span, context);
        return context.MemoryGovernor.CurrentCommittedBytes - before;
    }

    [Fact]
    public void MemberResultsCommitSlots()
    {
        var context = NewContext(out var span);
        var zone = new PyTimezone(TimeSpan.FromHours(2), "EET");
        var date = new PyDate(new DateOnly(2020, 1, 2));
        var time = new PyTime(new TimeOnly(1, 2, 3));
        var awareTime = new PyTime(new TimeOnly(1, 2, 3), zone);
        var dateTime = new PyDateTime(new DateTime(2020, 1, 2, 3, 4, 5));
        var awareDateTime = new PyDateTime(new DateTime(2020, 1, 2, 3, 4, 5), zone);

        Assert.Equal(64, InvokeDelta(context, span, date, "replace"));
        Assert.Equal(144, InvokeDelta(context, span, date, "isocalendar"));
        Assert.Equal(176, InvokeDelta(context, span, date, "timetuple"));
        Assert.Equal(64, InvokeDelta(context, span, time, "replace"));
        Assert.Equal(0, InvokeDelta(context, span, time, "utcoffset"));
        Assert.Equal(64, InvokeDelta(context, span, awareTime, "utcoffset"));
        Assert.Equal(64, InvokeDelta(context, span, dateTime, "date"));
        Assert.Equal(64, InvokeDelta(context, span, dateTime, "time"));
        Assert.Equal(64, InvokeDelta(context, span, dateTime, "timetz"));
        Assert.Equal(144, InvokeDelta(context, span, dateTime, "isocalendar"));
        Assert.Equal(176, InvokeDelta(context, span, dateTime, "timetuple"));
        Assert.Equal(176, InvokeDelta(context, span, dateTime, "utctimetuple"));
        Assert.Equal(0, InvokeDelta(context, span, dateTime, "utcoffset"));
        Assert.Equal(64, InvokeDelta(context, span, awareDateTime, "utcoffset"));
        Assert.Equal(64, InvokeDelta(context, span, dateTime, "astimezone"));
        Assert.Equal(64, InvokeDelta(context, span, dateTime, "replace"));
        Assert.Equal(64, InvokeDelta(context, span, zone, "utcoffset", CallArgumentValue.Positional(dateTime)));
    }

    [Fact]
    public void OperatorResultsCommitSlots()
    {
        var context = NewContext(out var span);
        var date = new PyDate(new DateOnly(2020, 1, 2));
        var dateTime = new PyDateTime(new DateTime(2020, 1, 2, 3, 4, 5));
        var delta = new PyTimedelta(TimeSpan.FromDays(1));
        var two = new BigInteger(2);

        AssertDelta(64, () => PyDateTimeOps.Add(date, delta, context, span));
        AssertDelta(64, () => PyDateTimeOps.Add(delta, delta, context, span));
        AssertDelta(64, () => PyDateTimeOps.Subtract(dateTime, dateTime, context, span));
        AssertDelta(64, () => PyDateTimeOps.Subtract(date, date, context, span));
        AssertDelta(64, () => PyDateTimeOps.Multiply(delta, two, context, span));
        AssertDelta(64, () => PyDateTimeOps.Divide(delta, two, context, span));
        AssertDelta(0, () => PyDateTimeOps.Divide(delta, delta, context, span));
        AssertDelta(64, () => PyDateTimeOps.FloorDivide(delta, two, context, span));
        AssertDelta(0, () => PyDateTimeOps.FloorDivide(delta, delta, context, span));
        AssertDelta(64, () => PyDateTimeOps.Modulo(delta, delta, context, span));
        AssertDelta(64, () => PyDateTimeOps.Negate(delta, context, span));

        void AssertDelta(long expected, Func<object> invoke)
        {
            var before = context.MemoryGovernor.CurrentCommittedBytes;
            _ = invoke();
            Assert.Equal(expected, context.MemoryGovernor.CurrentCommittedBytes - before);
        }
    }

    [Fact]
    public void TimetupleBackingIsOwned()
    {
        var context = NewContext(out var span);
        var date = new PyDate(new DateOnly(2020, 1, 2));
        Assert.True(PyMemberAccess.TryResolve(date, "timetuple", context, span, out var value));
        var bound = Assert.IsAssignableFrom<LythonRuntime.ICallable>(value);
        var result = Assert.IsType<PyTuple>(bound.Invoke([], span, context));
        Assert.NotNull(result.OwnerMemoryGovernor);
    }
}
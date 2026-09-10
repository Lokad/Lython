using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: rendered date/time text (ctime, isoformat, __format__, strftime,
/// tzname) allocates fresh strings on every access, so the results are
/// adopted into the caller governor like converted scalar renders.
/// </summary>
public sealed class DateTimeTextAccountingTests
{
    private static CallArgumentValue[] Strings(params string[] values)
    {
        var arguments = new CallArgumentValue[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            arguments[i] = CallArgumentValue.Positional(PyString.FromString(values[i]));
        }

        return arguments;
    }

    [Fact]
    public void EveryDateTimeTextMemberOwnsItsResult()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var zone = new PyTimezone(TimeSpan.FromHours(2), "EET");
        var date = new PyDate(new DateOnly(2020, 1, 2));
        var time = new PyTime(new TimeOnly(1, 2, 3), zone);
        var dateTime = new PyDateTime(new DateTime(2020, 1, 2, 3, 4, 5), zone);

        AssertOwned(date, "ctime", []);
        AssertOwned(date, "isoformat", []);
        AssertOwned(date, "__format__", Strings("%Y"));
        AssertOwned(date, "strftime", Strings("%Y"));
        AssertOwned(time, "tzname", []);
        AssertOwned(time, "isoformat", []);
        AssertOwned(time, "__format__", Strings("%H"));
        AssertOwned(time, "strftime", Strings("%H"));
        AssertOwned(dateTime, "ctime", []);
        AssertOwned(dateTime, "tzname", []);
        AssertOwned(dateTime, "isoformat", []);
        AssertOwned(dateTime, "__format__", Strings("%d"));
        AssertOwned(dateTime, "strftime", Strings("%Y"));
        AssertOwned(zone, "tzname", [CallArgumentValue.Positional(dateTime)]);

        void AssertOwned(object receiver, string name, CallArgumentValue[] arguments)
        {
            Assert.True(PyMemberAccess.TryResolve(receiver, name, context, span, out var value));
            var bound = Assert.IsAssignableFrom<LythonRuntime.ICallable>(value);
            var result = Assert.IsType<PyString>(bound.Invoke(arguments, span, context));
            Assert.NotNull(result.OwnerMemoryGovernor);
        }
    }

    [Fact]
    public void DateTimeTextValuesStillRender()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var zone = new PyTimezone(TimeSpan.FromHours(2), "EET");
        var date = new PyDate(new DateOnly(2020, 1, 2));
        var dateTime = new PyDateTime(new DateTime(2020, 1, 2, 3, 4, 5), zone);

        Assert.Equal("2020-01-02", Render(date, "isoformat", []));
        Assert.Equal("Thu Jan  2 00:00:00 2020", Render(date, "ctime", []));
        Assert.Equal("2020", Render(date, "strftime", Strings("%Y")));
        Assert.Equal("EET", Render(dateTime, "tzname", []));
        Assert.Equal("EET", Render(zone, "tzname", [CallArgumentValue.Positional(dateTime)]));

        string Render(object receiver, string name, CallArgumentValue[] arguments)
        {
            Assert.True(PyMemberAccess.TryResolve(receiver, name, context, span, out var value));
            var bound = Assert.IsAssignableFrom<LythonRuntime.ICallable>(value);
            return Assert.IsType<PyString>(bound.Invoke(arguments, span, context)).AsString();
        }
    }

}
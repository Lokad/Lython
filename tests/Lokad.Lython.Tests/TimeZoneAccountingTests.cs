using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: struct_time zone labels are owned payloads: gmtime shares the fixed
/// GMT label forever while localtime/strptime adopt their fresh labels beside
/// the shell and values tuple like other zone strings.
/// </summary>
public sealed class TimeZoneAccountingTests
{
    private static object InvokeTimeFunction(string name, LythonRuntime.ExecutionContext context, LythonSourceSpan span, params object[] arguments)
    {
        var type = typeof(LythonRuntime).GetNestedType("TimeModule", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("TimeModule not found.");
        var method = type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(name + " not found.");
        return method.Invoke(null, [arguments, span, context])!;
    }

    private static PyString ZoneOf(object structTime)
    {
        var zone = Assert.IsType<LythonRuntime.TimeStructTimeValue>(structTime).Zone;
        return Assert.IsType<PyString>(zone);
    }

    [Fact]
    public void GmtimeSharesZoneLabel()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var first = ZoneOf(InvokeTimeFunction("Gmtime", context, span));
        var second = ZoneOf(InvokeTimeFunction("Gmtime", context, span));
        Assert.Same(first, second);
        Assert.Equal("GMT", first.AsString());
    }

    [Fact]
    public void LocaltimeOwnsZoneLabel()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var zone = ZoneOf(InvokeTimeFunction("Localtime", context, span));
        Assert.Same(context.MemoryGovernor, zone.OwnerMemoryGovernor);
        Assert.Equal("UTC+01:00", zone.AsString());
        // Shell (64) plus the values tuple (176) and the zone label (128 + 9).
        Assert.Equal(377L, context.MemoryGovernor.CurrentCommittedBytes - before);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void StrptimeOwnsZoneLabel()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var zone = ZoneOf(InvokeTimeFunction(
            "Strptime",
            context,
            span,
            PyString.FromString("2024-01-02 GMT"),
            PyString.FromString("%Y-%m-%d %Z")));
        Assert.Same(context.MemoryGovernor, zone.OwnerMemoryGovernor);
        Assert.Equal("UTC", zone.AsString());
        // Shell (64) plus the values tuple (176) and the zone label (128 + 3).
        Assert.Equal(371L, context.MemoryGovernor.CurrentCommittedBytes - before);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void TznameMembersAreOwned()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var moduleType = typeof(LythonRuntime).GetNestedType("TimeModule", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("TimeModule not found.");
        var instance = moduleType.GetField("Instance", BindingFlags.Public | BindingFlags.Static)!.GetValue(null);
        var tryGetMember = moduleType.GetMethod(
            "TryGetMember",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            [typeof(string), typeof(LythonRuntime.ExecutionContext), typeof(LythonSourceSpan), typeof(object).MakeByRefType()],
            null)
            ?? throw new InvalidOperationException("TimeModule.TryGetMember not found.");
        var arguments = new object?[] { "tzname", context, span, null };
        Assert.True((bool)tryGetMember.Invoke(instance, arguments)!);
        var pair = Assert.IsType<PyTuple>(arguments[3]);
        Assert.Equal(2, pair.Count);
        foreach (var item in pair)
        {
            var zone = Assert.IsType<PyString>(item);
            Assert.Same(context.MemoryGovernor, zone.OwnerMemoryGovernor);
            Assert.Equal("UTC+01:00", zone.AsString());
        }
    }
}
using System.Reflection;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG03: the lowered member cache aliases site-stable values (one wrapper or
/// boxed value per site while the target is identical) without pooling across
/// targets; mutable data arms and instances resolve anew on every access.
/// </summary>
public sealed class MemberCacheAliasTests
{
    private static LoweredMemberExpression SiteFor(string name)
    {
        var span = new LythonSourceSpan(0, 0, 0, 0);
        return new LoweredMemberExpression(new MemberExpressionSyntax(null!, name, span), null!);
    }

    private static object Resolve(object target, LoweredMemberExpression site, LythonRuntime.ExecutionContext context)
    {
        var method = typeof(LythonRuntime).GetMethod("ResolveLoweredMemberValue", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ResolveLoweredMemberValue not found.");
        return method.Invoke(null, [site, target, context])
            ?? throw new InvalidOperationException("Resolve returned null.");
    }

    private static LythonRuntime.ExecutionContext NewContext()
        => new(new MockLythonHost(), new LythonRunOptions());

    private static object MakeZipInfo(LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var type = typeof(LythonRuntime).GetNestedType("ZipInfoCallable", BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ZipInfoCallable not found.");
        var instance = type.GetField("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null)
            ?? throw new InvalidOperationException("ZipInfoCallable.Instance not found.");
        var method = type.GetMethod("Invoke") ?? throw new InvalidOperationException("Invoke not found.");
        return method.Invoke(instance, [Array.Empty<CallArgumentValue>(), span, context])
            ?? throw new InvalidOperationException("Invoke returned null.");
    }

    [Fact]
    public void CachedControlAliases()
    {
        var context = NewContext();
        var site = SiteFor("append");
        var target = new PyList();
        Assert.Same(Resolve(target, site, context), Resolve(target, site, context));
    }

    [Fact]
    public void CounterMethodAliases()
    {
        var context = NewContext();
        var site = SiteFor("most_common");
        var target = new PyCounter();
        Assert.Same(Resolve(target, site, context), Resolve(target, site, context));
    }

    [Fact]
    public void ZipInfoMethodAliases()
    {
        var context = NewContext();
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var site = SiteFor("is_dir");
        var target = MakeZipInfo(context, span);
        Assert.Same(Resolve(target, site, context), Resolve(target, site, context));
    }

    [Fact]
    public void NormalDistMeanAliases()
    {
        var context = NewContext();
        var site = SiteFor("mean");
        var target = new LythonRuntime.StatisticsModule.PyNormalDist(0.0, 1.0);
        Assert.Same(Resolve(target, site, context), Resolve(target, site, context));
    }

    [Fact]
    public void DequeMaxlenAliases()
    {
        var context = NewContext();
        var site = SiteFor("maxlen");
        var target = new PyDeque(5);
        Assert.Same(Resolve(target, site, context), Resolve(target, site, context));
    }

    [Fact]
    public void ModuleInfoFlagAliases()
    {
        var context = NewContext();
        var site = SiteFor("ispkg");
        var target = new LythonRuntime.PkgutilModuleInfoObject(PyNone.Instance, PyString.FromString("m"), false);
        Assert.Same(Resolve(target, site, context), Resolve(target, site, context));
    }

    [Fact]
    public void ZipInfoFilenameStaysCurrent()
    {
        var context = NewContext();
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var site = SiteFor("filename");
        var target = MakeZipInfo(context, span);
        var first = Resolve(target, site, context);
        Assert.Equal("NoName", ((PyString)first).AsString());
        var setMember = target.GetType().GetMethod("SetMember") ?? throw new InvalidOperationException("SetMember not found.");
        setMember.Invoke(target, ["filename", PyString.FromString("b"), span]);
        var second = Resolve(target, site, context);
        Assert.Equal("b", ((PyString)second).AsString());
        Assert.NotSame(first, second);
    }

    [Fact]
    public void DistinctTargetsDoNotShare()
    {
        var context = NewContext();
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var site = SiteFor("append");
        var first = new PyList();
        var second = new PyList();
        var firstAppend = (LythonRuntime.ICallable)Resolve(first, site, context);
        var secondAppend = (LythonRuntime.ICallable)Resolve(second, site, context);
        Assert.NotSame(firstAppend, secondAppend);
        secondAppend.Invoke([CallArgumentValue.Positional(7)], span, context);
        Assert.Empty(first);
        Assert.Single(second);
    }
}

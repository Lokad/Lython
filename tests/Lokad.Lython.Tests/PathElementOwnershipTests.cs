using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: os.path split elements and lexical path-method results own their
/// string payloads (adopted when derived from unowned receivers) beside
/// governed backing, retiring the splitdrive/splitroot-element and deferred
/// path-method residuals as already-governed.
/// </summary>
public sealed class PathElementOwnershipTests
{
    private static LythonRuntime.ExecutionContext NewContext(out LythonSourceSpan span)
    {
        span = new LythonSourceSpan(0, 0, 0, 0);
        return new LythonRuntime.ExecutionContext(new MockLythonHost(), new LythonRunOptions());
    }

    private static object InvokeOsPath(string name, LythonRuntime.ExecutionContext context, LythonSourceSpan span, params object[] arguments)
    {
        var method = typeof(LythonRuntime).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(name + " not found.");
        return method.Invoke(null, [arguments, span, context])!;
    }

    private static PyString OwnedElement(object element, MemoryGovernor governor)
    {
        var text = Assert.IsType<PyString>(element);
        Assert.Same(governor, text.OwnerMemoryGovernor);
        return text;
    }

    private static string InvokePathMethod(object receiver, string name, LythonRuntime.ExecutionContext context, LythonSourceSpan span, params CallArgumentValue[] arguments)
    {
        Assert.True(PyMemberAccess.TryResolve(receiver, name, context, span, out var value));
        var bound = Assert.IsAssignableFrom<LythonRuntime.ICallable>(value);
        var result = Assert.IsType<PyPath>(bound.Invoke(arguments, span, context));
        Assert.Same(context.MemoryGovernor, result.Value.OwnerMemoryGovernor);
        return result.Value.AsString();
    }

    [Fact]
    public void SplitDriveElementsAreOwned()
    {
        var context = NewContext(out var span);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var result = Assert.IsType<PyTuple>(InvokeOsPath(
            "OsPathSplitDrive", context, span, PyString.FromString("/d/f0")));
        Assert.Equal(2, result.Count);
        Assert.Same(PyString.Empty, result[0]);
        Assert.Equal("/d/f0", OwnedElement(result[1], context.MemoryGovernor).AsString());
        // Tuple backing (64) plus the fresh element (128 + 5); the empty
        // drive aliases the shared empty string.
        Assert.Equal(197L, context.MemoryGovernor.CurrentCommittedBytes - before);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void SplitRootElementsAreOwned()
    {
        var context = NewContext(out var span);
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var result = Assert.IsType<PyTuple>(InvokeOsPath(
            "OsPathSplitRoot", context, span, PyString.FromString("/d/f0")));
        Assert.Equal(3, result.Count);
        Assert.Same(PyString.Empty, result[0]);
        Assert.Equal("/", OwnedElement(result[1], context.MemoryGovernor).AsString());
        Assert.Equal("d/f0", OwnedElement(result[2], context.MemoryGovernor).AsString());
        // Tuple backing (80) plus the fresh elements (128 + 1, 128 + 4).
        Assert.Equal(341L, context.MemoryGovernor.CurrentCommittedBytes - before);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void LexicalMethodResultsAdoptPayloads()
    {
        var context = NewContext(out var span);
        var receiver = new PyPath(PyString.FromString("/d"));
        Assert.Equal(
            "/d/f0",
            InvokePathMethod(receiver, "joinpath", context, span, CallArgumentValue.Positional(PyString.FromString("f0"))));
        var file = new PyPath(PyString.FromString("/d/f0.txt"));
        Assert.Equal(
            "/d/f0.py",
            InvokePathMethod(file, "with_suffix", context, span, CallArgumentValue.Positional(PyString.FromString(".py"))));
        Assert.Equal(
            "/d/f0.md",
            InvokePathMethod(file, "with_name", context, span, CallArgumentValue.Positional(PyString.FromString("f0.md"))));
        Assert.Equal(
            "f0.txt",
            InvokePathMethod(file, "relative_to", context, span, CallArgumentValue.Positional(PyString.FromString("/d"))));
    }
}
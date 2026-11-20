using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// Function custom attributes use their canonical governed dictionary. Retained
/// capacity and aliases remain funded; refused growth cannot publish a value.
/// </summary>
public sealed class FunctionAttributeAccountingTests
{
    [Fact]
    public void DeletionRetainsCapacityAndDeniesReinsertionBeforeMutation()
    {
        const long limit = 1024 * 1024;
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(limit) },regexProvider:Utf8RegexProvider.Instance);
        var function = Function(context);
        var value = new PyList([42], context.MemoryGovernor, null);
        var beforeTag = context.MemoryGovernor.CurrentAccountedBytes;
        function.TrySetMember("tag", value);
        var beforeDeletion = context.MemoryGovernor.CurrentAccountedBytes;
        Assert.True(function.TryDeleteMember("tag", null));
        Assert.False(function.TryGetMember("tag", out _));
        Assert.False(function.TryDeleteMember("tag", null));
        // Dictionary entry bookkeeping may be returned; retained backing and
        // the separately owned value must remain funded.
        Assert.InRange(context.MemoryGovernor.CurrentAccountedBytes, beforeTag + 1, beforeDeletion);
        Assert.True(function.TryGetMember("__dict__", out var attributes));
        var dictionary = Assert.IsType<PyDict>(attributes);
        var padding = limit - context.MemoryGovernor.CurrentAccountedBytes;
        context.MemoryGovernor.Reserve(padding, null);
        context.MemoryGovernor.Commit(padding);
        Assert.Equal(limit, context.MemoryGovernor.CurrentAccountedBytes);
        Assert.Equal("MemoryError", Assert.Throws<LythonRuntimeException>(() => function.TrySetMember("tag", value)).ExceptionType);
        Assert.Empty(dictionary);
        Assert.Equal(42, Assert.Single(value));
        Assert.Equal(limit, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData("__doc__")]
    [InlineData("__module__")]
    public void StandardMetadataResetUsesStorageFundedAtDefinition(string name)
    {
        const long limit = 1024 * 1024;
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(limit) },regexProvider:Utf8RegexProvider.Instance);
        var moduleName = PyString.FromString("original");
        context.Variables["__name__"] = moduleName;
        var function = Function(context);
        Assert.True(function.TryGetMember(name, out var before));
        Assert.Same(name == "__module__" ? (object)moduleName : PyNone.Instance, before);
        var padding = limit - context.MemoryGovernor.CurrentAccountedBytes;
        context.MemoryGovernor.Reserve(padding, null);
        context.MemoryGovernor.Commit(padding);
        Assert.True(function.TryDeleteMember(name, null));
        Assert.True(function.TryGetMember(name, out var after));
        Assert.Same(PyNone.Instance, after);
        Assert.Null(context.MemoryGovernor.AdmissionFailure);
        Assert.Equal(limit, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void CustomAttributeOverwriteReusesOwnedDictionaryStorage()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions(),regexProvider:Utf8RegexProvider.Instance);
        var function = Function(context);
        Assert.True(function.TrySetMember("a", PyNone.Instance));
        var afterFirst = context.MemoryGovernor.CurrentCommittedBytes;
        Assert.True(function.TrySetMember("a", PyNone.Instance));
        Assert.Equal(afterFirst, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.True(function.TrySetMember("b", PyNone.Instance));
        Assert.True(function.TrySetMember("c", PyNone.Instance));
        Assert.True(context.MemoryGovernor.CurrentCommittedBytes > afterFirst);
        Assert.True(function.TryGetMember("__dict__", out var attributes));
        Assert.Equal(3, Assert.IsType<PyDict>(attributes).Count);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnnotationDeletionRefundsOnlyUnpublishedStorage(bool readFirst)
    {
        const long limit = 1024 * 1024;
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(limit) });
        const string source = "def make():\n def f(value:int)->int:return value\n return f\nreturn make()";
        var function = Function(context, source);
        var padding = limit - context.MemoryGovernor.CurrentAccountedBytes;
        context.MemoryGovernor.Reserve(padding, null);
        context.MemoryGovernor.Commit(padding);

        PyDict? annotations = null;
        if (readFirst)
        {
            Assert.True(function.TryGetMember("__annotations__", out var value));
            annotations = Assert.IsType<PyDict>(value);
            Assert.True(function.TryGetMember("__annotations__", out var observed));
            Assert.Same(annotations, observed);
            Assert.Equal(limit, context.MemoryGovernor.CurrentAccountedBytes);
        }

        Assert.True(function.TryDeleteMember("__annotations__", null));
        if (readFirst)
        {
            Assert.Equal(limit, context.MemoryGovernor.CurrentAccountedBytes);
            Assert.Equal(2, annotations!.Count);
        }
        else Assert.InRange(context.MemoryGovernor.CurrentAccountedBytes, 0, limit - 1);
        Assert.Null(context.MemoryGovernor.AdmissionFailure);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private static PyExecutableFunction Function(LythonRuntime.ExecutionContext context, string source = "def f():pass\nreturn f")
    {
        var syntax = LythonFrontend.Compile(source).Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        return Assert.IsType<PyExecutableFunction>(
            LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
    }
}

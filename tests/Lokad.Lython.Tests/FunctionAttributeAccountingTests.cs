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
        var function = Function(context, "def f():\n 'original doc'\n return None\nreturn f");
        Assert.True(function.TryGetMember(name, out var before));
        Assert.Same(name == "__module__" ? moduleName : function.CodeObject.Docstring, Assert.IsType<PyString>(before));
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
    public void DictionaryPublishedDuringAdmissionStaysAuthoritative(bool alreadyHasOverrides)
    {
        var budget = new LythonMemoryBudget();
        using (var owner = budget.CreateScope())
        {
            var hook = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                new LythonRunOptions { MemoryBudget = hook });
            var function = Function(context);
            if (alreadyHasOverrides) function.TrySetMember("__doc__", PyString.FromString("changed"));
            var replacement = new PyDict(context.MemoryGovernor);
            hook.AfterGrowth = _ =>
            {
                hook.AfterGrowth = null;
                function.TrySetMember("__dict__", replacement);
            };

            Assert.True(function.TryGetMember("__dict__", out var observed));
            Assert.Same(replacement, observed);
            Assert.True(function.TrySetMember("tag", PyNone.Instance));
            Assert.True(function.TryGetMember("__dict__", out var alias));
            Assert.Same(replacement, alias);
            Assert.Single(replacement);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedEagerAnnotationReadRefundsPrivateDefinition(bool asynchronous)
    {
        const string source = """
            Alias = int
            def make():
                def f(value: str, other: Alias) -> int: return value
                return f
            return make
            """;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                new LythonRunOptions { MemoryBudget = owner });
            var factory = Function(context, source);
            var globals = factory.DefinitionContext.Variables;
            globals["Alias"] = PyNone.Instance;
            var call = new LythonSourceSpan(0, 1, 1, 1);
            _ = asynchronous ? await factory.InvokeAsync([], call, context) : factory.Invoke([], call, context);
            Assert.True(globals.Remove("Alias"));
            var before = context.MemoryGovernor.CurrentAccountedBytes;

            var error = asynchronous
                ? await Assert.ThrowsAsync<LythonRuntimeException>(async () => await factory.InvokeAsync([], call, context))
                : Assert.Throws<LythonRuntimeException>(() => factory.Invoke([], call, context));

            Assert.Equal("NameError", error.ExceptionType);
            Assert.Equal(source.ReplaceLineEndings("\n").IndexOf("Alias)", StringComparison.Ordinal), error.Span?.Start);
            Assert.Equal(before, context.MemoryGovernor.CurrentAccountedBytes);
            Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static PyExecutableFunction Function(LythonRuntime.ExecutionContext context, string source = "def f():pass\nreturn f")
    {
        var syntax = LythonFrontend.Compile(source).Script!;
        var code = ExecutableScript.Compile(syntax).EntryPoint;
        return Assert.IsType<PyExecutableFunction>(
            LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
    }
}

using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class LexicalPreparationBoundaryTests
{
    // Independent CPython 3.13 goldens: eager module reads and deferred bodies
    // select different first-parameter/class-cell checks for direct super access.
    private const string Call = """
        class C:
         def read(self):
          del self
          return super().__self__
        try: C().read()
        except Exception as error: print(type(error).__name__,str(error))
        """;

    public static IEnumerable<object[]> Cases()
    {
        yield return ["eager_function_default", "def marker(value=super): pass\n" + Call,
            "RuntimeError super(): arg[0] deleted\n"];
        yield return ["deferred_lambda_body", "marker=lambda: super\n" + Call,
            "UnboundLocalError cannot access local variable 'self' where it is not associated with a value\n"];
        yield return ["eager_lambda_default", "marker=lambda value=super: None\n" + Call,
            "RuntimeError super(): arg[0] deleted\n"];
        yield return ["deferred_generator_body", "marker=(super for value in [1])\n" + Call,
            "UnboundLocalError cannot access local variable 'self' where it is not associated with a value\n"];
        yield return ["eager_generator_iterable", "marker=(value for value in [super])\n" + Call,
            "RuntimeError super(): arg[0] deleted\n"];
        yield return ["eager_comprehension_binding", "marker=[None for super in [1]]\n" + Call,
            "RuntimeError super(): arg[0] deleted\n"];
        yield return ["class_suite_read", "class Other:\n marker=super\n" + Call,
            "UnboundLocalError cannot access local variable 'self' where it is not associated with a value\n"];
        yield return ["late_nested_global", "def other():\n if False:\n  global super\n" + Call,
            "RuntimeError super(): arg[0] deleted\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ModuleEvaluationAndDeferredScopesPreserveSuperErrors(string name, string source, string expected)
    {
        using var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, name + ": " + string.Join("; ", compiled.Diagnostics));
        using var sync = compiled.Run(new MockLythonHost());
        using var asyncResult = await compiled.RunAsync(new MockLythonHost());
        foreach (var result in new[] { sync, asyncResult })
        {
            Assert.True(result.Success, name + ": " + result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}

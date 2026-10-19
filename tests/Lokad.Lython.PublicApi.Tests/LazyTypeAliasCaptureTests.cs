using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class LazyTypeAliasCaptureTests
{
    public static TheoryData<string, string, string> Cases => new()
    {
        { "escaped-factory", "def outer():\n value=int\n def make():\n  type A=value\n  return A\n value=str\n return make\nalias=outer()()\nprint(alias.__value__ is str,alias.__value__ is str)", "True True\n" },
        { "transitive-factory", "def outer():\n value=int\n def middle():\n  def make():\n   type A=value\n   return A\n  return make\n value=str\n return middle\nalias=outer()()()\nprint(alias.__value__ is str)", "True\n" },
        { "generic-bound", "def outer():\n value=int\n def make():\n  type A[T:value]=list[T]\n  return A\n value=str\n return make\nalias=outer()()\nparameter=alias.__type_params__[0]\nprint(parameter.__bound__ is str,alias.__value__.__args__[0] is parameter)", "True True\n" },
        { "alias-before-rebinding", "def make(value):\n type A=value\n value=str\n return A\nalias=make(int)\nprint(alias.__value__ is str)", "True\n" },
        { "module-late-binding", "value=int\ndef make():\n type A=value\n return A\nvalue=str\nprint(make().__value__ is str)", "True\n" },
        { "nonlocal-editor", "def outer():\n value=int\n def make():\n  type A=value\n  return A\n def edit():\n  nonlocal value\n  value=str\n return make,edit\nmake,edit=outer()\nedit()\nprint(make().__value__ is str)", "True\n" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task LazyCapturesSurviveDefiningFramesAndKeepTheirOwner(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget))
        {
            Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, name + ": " + result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                    Assert.Equal("", result.StandardError);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}

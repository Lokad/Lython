using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class BoundLoopNameStorageTests
{
    [Theory]
    [InlineData("for i in [1, 2]:\n assert globals()['i'] == i\n assert locals()['i'] == i\nreturn str(i)", "2")]
    [InlineData("i = 99\nfor i in []: pass\nreturn str(i)", "99")]
    [InlineData("for missing in []: pass\ntry:\n missing\nexcept NameError:\n return 'unbound'\nreturn 'incorrect'", "unbound")]
    [InlineData("def f():\n for missing in []: pass\n try:\n  return missing\n except UnboundLocalError:\n  return 'unbound'\nreturn f()", "unbound")]
    [InlineData("value = 0\ndef f():\n global value\n for value in [3, 4]: pass\nf()\nreturn str(value)", "4")]
    [InlineData("def outer():\n value = 0\n def inner():\n  nonlocal value\n  for value in [5, 6]: pass\n inner()\n return value\nreturn str(outer())", "6")]
    [InlineData("def f():\n readers = []\n for i in [1, 2]:\n  def read(): return i\n  readers.append(read)\n return [g() for g in readers]\nreturn str(f())", "[2, 2]")]
    [InlineData("def f():\n for i in [1, 2]:\n  yield i\n  assert locals()['i'] == i\nreturn str(list(f()))", "[1, 2]")]
    [InlineData("for i in range(5):\n try:\n  if i == 1: continue\n  if i == 3: break\n finally:\n  saved = i\nreturn str([i, saved])", "[3, 3]")]
    [InlineData("i = 10\nvalues = [i for i in [1, 2]]\nreturn str([i, values])", "[10, [1, 2]]")]
    [InlineData("class C:\n for i in [7, 8]: pass\nreturn str(C.i)", "8")]
    [InlineData("def update():\n global i\n i = 50\nvalues = []\nfor i in [1, 2]:\n update()\n values.append(i)\nreturn str(values)", "[50, 50]")]
    [InlineData("def outer():\n def update():\n  nonlocal i\n  i = 50\n values = []\n for i in [1, 2]:\n  update()\n  values.append(i)\n return values\nreturn str(outer())", "[50, 50]")]
    public async Task NamesKeepTheirScopeAndNamespaceBehavior(string source, string expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid);
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.ReturnValue);
        }
    }
}

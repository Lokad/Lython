using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class DeclarationNamespaceTests
{
    // Frozen isolated CPython 3.13 output; each case also passes the preceding runtime.
    public static IEnumerable<object[]> Cases()
    {
        yield return ["failed_function_builtin", """
def reject(value): raise ValueError('stop')
try:
 @reject
 def len(value): return 99
except ValueError: pass
print(len([1,2]),'len' in globals())
""", "2 False\n"];
        yield return ["namespace_declarations", """
scope=globals()
def chosen(): return 1
class Box: pass
type Alias = int
scope['chosen']=lambda:2
scope['Box']=int
scope['Alias']=list
print(chosen(),Box('3'),Alias([4])[0])
del scope['chosen']
try: chosen()
except NameError: print('missing')
""", "2 3 4\nmissing\n"];
        yield return ["global_function_replacement", """
def chosen(): return 1
def replace():
 global chosen
 def chosen(): return 2
replace()
print(chosen())
""", "2\n"];
        yield return ["import_alias_namespace", """
import math as numbers
from math import sqrt as root
scope=globals()
scope['numbers']=None
print(numbers is None,root(9))
scope['root']=lambda value:7
print(root(9))
del scope['root']
try: root(9)
except NameError: print('missing')
""", "True 3.0\n7\nmissing\n"];
        yield return ["failed_class_builtin", """
class Base:
 def __init_subclass__(cls): raise ValueError('stop')
try:
 class list(Base): pass
except ValueError: pass
print(list((1,2)),'list' in globals())
""", "[1, 2] False\n"];
        yield return ["lazy_alias_namespace", """
type Alias = Later
class Later: pass
globals()['Later']=int
print(Alias.__value__ is int)
globals()['Alias']=str
print(Alias(4))
""", "True\n4\n"];
        yield return ["replacement_preserves_key_iterator", """
def chosen(): return 1
view=globals()
it=None
it=iter(view)
def chosen(): return 2
print(chosen(),len(list(it))>0)
""", "2 True\n"];
        yield return ["argument_rebind_preserves_target", """
def chosen(value): return 'old'
def newer(value): return 'new'
def mutate():
 global chosen
 chosen=newer
 return 7
print(chosen(mutate()),chosen(8))
""", "old new\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task DeclarationsKeepTheLivePythonNamespace(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true, false })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, name + ": " + result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}

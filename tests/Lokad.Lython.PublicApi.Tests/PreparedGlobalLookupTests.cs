using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class PreparedGlobalLookupTests
{
    // Independent CPython 3.13 controls; lookup must remain live rather than
    // freezing a global/builtin or skipping an incomplete lexical environment.
    public static IEnumerable<object[]> Cases()
    {
        yield return ["live_globals_and_builtins", """
            name = 'module'
            def read(): return name
            print(read())
            name = 'later'
            print(read())
            del name
            try: read()
            except NameError as error: print(type(error).__name__)
            def size(): return len([1, 2])
            print(size())
            len = lambda value: 77
            print(size())
            del len
            print(size())
            """, "module\nlater\nNameError\n2\n77\n2\n"];
        yield return ["transitive_capture", """
            value = 2
            def outer():
                value = 3
                def middle():
                    def inner(): return value + len([])
                    return inner
                return middle()
            read = outer()
            print(read())
            value = 9
            print(read())
            """, "3\n3\n"];
        yield return ["type_parameters", """
            T = 'module'
            def read[T](): return T.__name__
            def outer[T]():
                def inner(): return T.__name__
                return inner
            print(read(), outer()())
            """, "T T\n"];
        yield return ["comprehension_capture", """
            token = 'module'
            def make(): return [lambda: token for token in [1, 2]]
            readers = make()
            print([read() for read in readers])
            print(token)
            """, "[2, 2]\nmodule\n"];
        yield return ["class_barrier", """
            value = 'module'
            def outer():
                value = 'outer'
                class C:
                    value = 'class'
                    def read(self): return value
                return C
            print(outer()().read())
            """, "outer\n"];
        yield return ["enclosing_global_barrier", """
            value = 'module'
            def outer():
                value = 'outer'
                def middle():
                    global value
                    def inner(): return value
                    return inner
                return middle()
            print(outer()())
            """, "module\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task PreparedLoadsPreserveLiveNamespacesAndLexicalBarriers(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics));
            var compiledBalance = budget.ReservedBytes;
            using (var result = script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, name + ": " + result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(compiledBalance, budget.ReservedBytes);
            using (var result = await script.RunAsync(new MockLythonHost()))
            {
                Assert.True(result.Success, name + ": " + result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(compiledBalance, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}

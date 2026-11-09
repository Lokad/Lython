using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class GeneratorStorageSemanticsTests
{
    // Outputs independently checked with isolated CPython 3.13.
    public static TheoryData<string, string, string> Cases => new()
    {
        { "incoming-live-and-deleted-cells", """
            def outer():
                value = 1
                def produce():
                    yield value
                    yield value
                    try: yield value
                    except NameError: yield 'gone'
                def replace(new):
                    nonlocal value
                    value = new
                def forget():
                    nonlocal value
                    del value
                return produce(), replace, forget
            g, replace, forget = outer()
            print(next(g))
            replace(7)
            print(next(g))
            forget()
            print(next(g), list(g))
            """, "1\n7\ngone []\n" },
        { "suspended-local-cells", """
            def produce(value):
                def read(): return value
                def replace(new):
                    nonlocal value
                    value = new
                yield read, replace
                yield value, locals()['value']
                del value
                yield read
            g = produce(3)
            read, replace = next(g)
            replace(9)
            print(read(), next(g))
            escaped = next(g)
            print(list(g))
            try: escaped()
            except NameError: print('deleted')
            """, "9 (9, 9)\n[]\ndeleted\n" },
        { "bound-storage-outlives-call", """
            events = []
            def produce(value=3, /, *extra, scale=2, **named):
                events.append('started')
                yield value, extra, scale, named
            first = produce(7, 8, 9, scale=4, label='first')
            second = produce(label='second')
            produce.__defaults__ = (99,)
            print(events)
            print(list(second), list(first), list(produce()))
            print(events)
            """, "[]\n[(3, (), 2, {'label': 'second'})] [(7, (8, 9), 4, {'label': 'first'})] [(99, (), 2, {})]\n['started', 'started', 'started']\n" },
        { "class-barriers-and-super", """
            value = 'module'
            def outer():
                value = 'outer'
                class Base:
                    def items(self): yield value
                class Child(Base):
                    value = 'class'
                    def items(self):
                        yield from super().items()
                        yield value
                    def globals(self):
                        global value
                        yield value
                return Child()
            item = outer()
            value = 'changed'
            print(list(item.items()), list(item.globals()))
            """, "['outer', 'outer'] ['changed']\n" },
        { "class-fallback-and-generator-lambda", """
            def produce(value):
                class View:
                    def read(self): return value
                yield View()
                value = 8
                yield (lambda: (yield value))()
            g = produce(3)
            view = next(g)
            print(view.read())
            inner = next(g)
            print(view.read(), list(inner), list(g), view.read())
            """, "3\n8 [8] [] 8\n" },
        { "suspended-class-capture", """
            def produce(value):
                class View((yield 'base')):
                    def read(self): return value
                yield View()
                value = 9
                yield value
            g = produce(3)
            print(next(g))
            view = g.send(object)
            print(view.read(), next(g), view.read(), list(g), view.read())
            """, "base\n3 9 9 [] 9\n" },
        { "class-nonlocal-delete", """
            def outer():
                value = 3
                class View:
                    def read(self): return value
                    def clear(self):
                        nonlocal value
                        del value
                return View()
            view = outer()
            print(view.read())
            view.clear()
            try: view.read()
            except NameError: print('deleted')
            """, "3\ndeleted\n" },
        { "transitive-generator-capture", """
            def outer(value):
                def middle():
                    def produce():
                        yield value
                    return produce()
                return middle
            print(list(outer(7)()))
            def produce():
                if False: value = 3
                def read(): return value
                yield read
            read = next(produce())
            try: read()
            except NameError: print('unbound')
            """, "[7]\nunbound\n" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task RetainedGeneratorStatePreservesPythonBehavior(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous
                    ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                    : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
                {
                    Assert.True(result.Success, name + ": " + result.Failure?.Message + " at " + result.Failure?.Span);
                    Assert.Equal(expected, result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}

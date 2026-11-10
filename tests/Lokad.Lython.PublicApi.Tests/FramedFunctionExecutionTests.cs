using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FramedFunctionExecutionTests
{
    public static TheoryData<string, string, string> Cases => new()
    {
        { "handler-global-and-parameter", """
            def current(): return outer
            def identity(outer): return outer
            try: raise ValueError('outer')
            except ValueError as outer:
                print(current() is outer, identity('local'))
                saved = current()
            print(type(saved).__name__, 'outer' in globals())
            return ['retained']
            """, "True local\nValueError False\n" },
        { "handler-builtin-shadow", """
            def parse(text): return int(text)
            try: raise ValueError('shadow')
            except ValueError as int:
                try: parse('12')
                except TypeError as failure: print(failure.__context__ is int)
            print(parse('9'))
            return ['retained']
            """, "True\n9\n" },
        { "nested-handler-context", """
            def parse(text): return int(text)
            try: raise LookupError('outer')
            except LookupError as outer:
                try: raise KeyError('inner')
                except KeyError as inner:
                    try: parse('bad')
                    except ValueError as failure: print(failure.__context__ is inner)
                try: parse('bad')
                except ValueError as failure: print(failure.__context__ is outer)
                print(parse('11'))
            return ['retained']
            """, "True\nTrue\n11\n" },
        { "handler", """
            def parse(text): return int(text)
            def fresh(): return []
            try:
                raise LookupError('outer')
            except LookupError as outer:
                print(parse('12'))
                result = fresh()
                result.append('retained')
                try: parse('bad')
                except ValueError as failure: print(failure.__context__ is outer)
                print(result)
            print('outer' in locals())
            return result
            """, "12\nTrue\n['retained']\nFalse\n" },
        { "class-cell", """
            class Base:
                def label(self): return 'base'
            class Parser(Base):
                def label(self): return super().label()
                def parse(self, text): return int(text)
            parser = Parser()
            print(parser.label(), parser.parse('23'))
            return ['retained']
            """, "base 23\n" },
        { "annotation", """
            type Converter = lambda value: int(value)
            convert = Converter.__value__
            print(convert('31'))
            return ['retained']
            """, "31\n" },
    };

    [Theory, MemberData(nameof(Cases))]
    public async Task LexicalContextsKeepExceptionsValuesAndResultOwnership(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous
                    ? await script.RunAsync(new MockLythonHost())
                    : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                    Assert.Equal("retained", Assert.Single(Assert.IsType<List<object?>>(result.ReturnValue)));
                    Assert.True(budget.ReservedBytes > baseline);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}

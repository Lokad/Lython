using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class SharedCompilerFactTests
{
    [Fact]
    public void NestedDeclarationsKeepAllDiagnosticFamiliesAndTheirOrder()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile("""
            if False:
                result: int = 'bad'
                def use():
                    return local
                    local = 1
            def outer():
                nonlocal missing
            """, budget))
        {
            Assert.False(script.IsValid);
            var codes = script.Diagnostics.Select(diagnostic => diagnostic.Code).ToArray();
            Assert.Contains("LA1080", codes);
            Assert.Contains("LA3205", codes);
            Assert.Contains("LA3146", codes);
            Assert.True(Array.IndexOf(codes, "LA1080") < Array.IndexOf(codes, "LA3205"));
            Assert.True(Array.IndexOf(codes, "LA3205") < Array.IndexOf(codes, "LA3146"));
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void DefaultLambdaLocalReadsAreReportedOncePerDeclaration()
    {
        using var script = new LythonEngine().Compile("""
            def top(arg=lambda: first + (first := 1)): pass
            def owner():
                def nested(arg=lambda: second + (second := 1)): pass
            """);

        Assert.False(script.IsValid);
        Assert.Equal(
            ["Local variable 'first' is read before it is assigned.",
             "Local variable 'second' is read before it is assigned."],
            script.Diagnostics.Where(diagnostic => diagnostic.Code == "LA3146").Select(diagnostic => diagnostic.Message));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedImmutableTuplesRemainUsableAcrossADeferredDefinition(bool asynchronous)
    {
        // Forty linear statements describe a shared graph, not 2^40 objects.
        var source = "x = 1\n" + string.Concat(Enumerable.Repeat("x = (x, x)\n", 40)) + """
            def ready():
                return 1
            print(ready())
            for i in range(40):
                x = x[0]
            print(x)
            del x
            """;
        await Check(source, "1\n1\n", asynchronous);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MutableSiblingIsStillInvalidatedAfterSharedImmutableFacts(bool asynchronous)
    {
        var source = "pure = 1\n" + string.Concat(Enumerable.Repeat("pure = (pure, pure)\n", 32)) + """
            items = [0]
            holder = (pure, items)
            def change():
                items.append('ok')
            change()
            print(holder[1][1].upper(), holder[1] is items)
            del pure, holder
            """;
        await Check(source, "OK True\n", asynchronous);
    }

    [Fact]
    public void UnrelatedMutationPreservesDiagnosticsForAnImmutableTupleGraph()
    {
        const int depth = 12;
        var source = "pure = 1\n" + string.Concat(Enumerable.Repeat("pure = (pure, pure)\n", depth)) +
            "items = []\nmixed = (pure, items)\nitems.append(1)\nlen(mixed[0]" +
            string.Concat(Enumerable.Repeat("[1]", depth)) + ")\n";
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var compiled = new LythonEngine().Compile(source, budget))
        {
            Assert.False(compiled.IsValid);
            var diagnostic = Assert.Single(compiled.Diagnostics);
            Assert.Equal("LA3032", diagnostic.Code);
            Assert.Equal(17, diagnostic.Span?.Line);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task SharedContainerArgumentsKeepNativeCallSemantics(bool dictionary, bool asynchronous)
    {
        var update = dictionary ? "x = {'a': x, 'b': x}\n" : "x = (x, x)\n";
        var source = "x = 1\n" + string.Concat(Enumerable.Repeat(update, 40)) + """
            print(len(x), bool(x))
            del x
            """;
        await Check(source, "2 True\n", asynchronous);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReboundBuiltinCallsUseTheActualTargets(bool asynchronous)
    {
        await Check("""
            def int(value): return "word"
            print(len(int(1)))
            del int
            def len(value): return "word"
            print(len(1).upper())
            del len
            def open(value): return [value]
            print(open("name")[0])
            def bool(value): return [value]
            print(len(bool(9)))
            from operator import index as str
            print(str(7) + 1)
            input = lambda value: value + 1
            print(input(3))
            sum = lambda value: value + 2
            print(sum(3))
            """, "4\nWORD\nname\n1\n8\n4\n5\n", asynchronous);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnknownRebindingsAndOneSidedBranchesDoNotRestoreBuiltinFacts(bool asynchronous)
    {
        await Check("""
            values = [1]
            def maker(): return lambda value: values.append("ok")
            if callable(lambda: None):
                len = maker()
            len(3)
            print(values[1].upper())
            del len
            if not callable(lambda: None):
                pass
            else:
                int = lambda value: "branch"
            print(len(int(3)))
            (len,) = [lambda value: "unpacked"]
            print(len(3))
            print((len := lambda value: "walrus")(3), len(4))
            """, "OK\n6\nunpacked\nwalrus walrus\n", asynchronous);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeferredBodiesRespectParametersLexicalLocalsAndLaterModuleWrites(bool asynchronous)
    {
        await Check("""
            def use(int):
                value = int(1)
                print(len(value))
            use(lambda x: "ok")
            def late(): return len(3)
            len = lambda value: "late"
            print(late())
            del len
            def outer():
                def read(): return len(3)
                len = lambda value: "outer"
                return read()
            print(outer())
            """, "2\nlate\nouter\n", asynchronous);
    }

    [Fact]
    public void DeletionRestoresTheModuleBuiltinAndKeepsAFunctionLocalUnbound()
    {
        using var module = new LythonEngine().Compile("""
            len = lambda value: "bound"
            del len
            len(3)
            """);
        var builtinError = Assert.Single(module.Diagnostics);
        Assert.Equal("LA3032", builtinError.Code);
        Assert.Equal(3, builtinError.Span?.Line);

        using var local = new LythonEngine().Compile("""
            def read():
                len = lambda value: "bound"
                del len
                return len(3)
            """);
        var localError = Assert.Single(local.Diagnostics);
        Assert.Equal("LA3146", localError.Code);
        Assert.Equal(4, localError.Span?.Line);
    }

    private static async Task Check(string source, string expected, bool asynchronous)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            var options = new LythonRunOptions { MemoryBudget = budget };
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options)
                : script.Run(new MockLythonHost(), options))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}

using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ClassScopeDirectiveTests
{
    [Fact]
    public async Task GlobalReadsWritesAndDeletes()
    {
        await AssertOutput("""
            x=2
            class C:
             global x
             x += 3
             before=x
             del x
             try: x
             except NameError: print('missing')
            print(C.before, hasattr(C,'x'))
            try: x
            except NameError: print('module missing')
            """, "missing\n5 False\nmodule missing\n");
    }

    [Fact]
    public async Task NonlocalCellsSurviveClassConstruction()
    {
        await AssertOutput("""
            def outer():
             x=1
             def read(): return x
             class C:
              nonlocal x
              x += 2
              def bump(self):
               nonlocal x
               x += 4
               return x
             return C,read
            C,read=outer()
            print(read(), C().bump(), read(), hasattr(C,'x'))
            """, "3 7 7 False\n");
    }

    [Fact]
    public async Task NearestFunctionBindingSkipsClasses()
    {
        await AssertOutput("""
            def outer():
             x='outer'
             def middle():
              x='middle'
              class C:
               x='class'
               class D:
                nonlocal x
                x += ':changed'
               def read(self): return x
              return x,C
             y,C=middle()
             print(x,y,C.x,C().read(),hasattr(C.D,'x'))
            outer()
            """, "outer middle:changed class middle:changed False\n");
    }

    [Fact]
    public async Task GlobalBypassesEnclosingFunction()
    {
        await AssertOutput("""
            x='module'
            def outer():
             x='outer'
             class C:
              global x
              x += ':changed'
              value=x
             return x,C
            local,C=outer()
            print(x,local,C.value,hasattr(C,'x'))
            """, "module:changed outer module:changed False\n");
    }

    [Fact]
    public async Task ClassGlobalDoesNotRedirectMethods()
    {
        await AssertOutput("""
            x='module'
            def outer():
             x='outer'
             class C:
              global x
              x += ':changed'
              def read(self): return x
              class D:
               nonlocal x
               x += ':cell'
             return C
            C=outer()
            print(x,C().read(),hasattr(C.D,'x'))
            """, "module:changed outer:cell False\n");
    }

    [Fact]
    public async Task GlobalsCoverAllBindingForms()
    {
        await AssertOutput("""
            class C:
             global root,f,D,i,a,error,capture,guard,t
             from math import sqrt as root
             def f(): return 2
             class D: pass
             for i in [3]: pass
             a,local=(4,5)
             try: raise ValueError('oops')
             except ValueError as error: text=str(error)
             match 6:
              case capture if (guard := capture+1): pass
             type t = int
            print(root(9),f(),D.__name__,i,a,C.local,C.text,capture,guard,t.__value__ is int)
            print(hasattr(C,'root'),hasattr(C,'f'),hasattr(C,'D'),hasattr(C,'i'),hasattr(C,'a'),hasattr(C,'capture'),hasattr(C,'guard'),hasattr(C,'t'))
            try: error
            except NameError: print('cleared')
            """, "3.0 2 D 3 4 5 oops 6 7 True\nFalse False False False False False False False\ncleared\n");
    }

    [Fact]
    public async Task NonlocalsCoverBindingForms()
    {
        await AssertOutput("""
            def outer():
             f=None
             D=None
             i=a=capture=guard=error=0
             class C:
              nonlocal f,D,i,a,capture,guard,error
              def f(): return i
              class D: pass
              for i in [3]: pass
              a,local=(4,5)
              try: raise ValueError('oops')
              except ValueError as error: text=str(error)
              match 6:
               case capture if (guard := capture+1): pass
             print(f(),D.__name__,i,a,capture,guard,C.local,C.text,hasattr(C,'f'),hasattr(C,'capture'))
             try: error
             except NameError: print('cleared')
            outer()
            """, "3 D 3 4 6 7 5 oops False False\ncleared\n");
    }

    [Fact]
    public async Task ConditionalDirectivesApplyToWholeSuite()
    {
        await AssertOutput("""
            x='module'
            def outer():
             x='outer'
             class C:
              if False:
               global x
              x += ':updated'
             return x,C
            local,C=outer()
            print(x,local,hasattr(C,'x'))
            """, "module:updated outer False\n");
    }

    [Fact]
    public async Task NonlocalDeleteRebindsSharedCell()
    {
        await AssertOutput("""
            def outer():
             x=1
             class C:
              nonlocal x
              del x
              try: x
              except NameError: print('empty')
              x=9
              def read(self): return x
             return C
            C=outer()
            print(C().read(),hasattr(C,'x'))
            """, "empty\n9 False\n");
    }

    [Fact]
    public async Task OrdinaryClassLocalsFallBackToModule()
    {
        await AssertOutput("""
            x='module'
            def outer():
             x='outer'
             class C:
              before=x
              x='class'
              after=x
              class D: value=x
             return C
            C=outer()
            print(C.before,C.after,C.D.value)
            """, "module class outer\n");
    }

    [Fact]
    public async Task OuterWalrusProvidesNonlocalCell()
    {
        await AssertOutput("""
            def outer():
             a=(x:=1)
             class C:
              nonlocal x
              x+=2
             return x,a,C
            x,a,C=outer()
            print(x,a,hasattr(C,'x'))
            """, "3 1 False\n");
    }

    [Fact]
    public async Task DeleteOnlyOuterBindingProvidesCell()
    {
        await AssertOutput("""
            def outer():
             class C:
              nonlocal x
              x=4
             print(x,hasattr(C,'x'))
             del x
            outer()
            """, "4 False\n");
    }

    [Fact]
    public async Task GlobalGuardAndHandlerAssignments()
    {
        await AssertOutput("""
            x=0
            class C:
             global x,selected
             match 3:
              case selected if (x := selected+1): pass
             try: raise ValueError()
             except (error_type := ValueError): x += 2
            print(x,selected,hasattr(C,'x'),C.error_type is ValueError)
            """, "6 3 False True\n");
    }

    [Theory]
    [InlineData("class C:\n nonlocal x\n", "LA3201")]
    [InlineData("class C:\n global x\n nonlocal x\n", "LA3203")]
    [InlineData("def f():\n class C:\n  nonlocal missing\n", "LA3205")]
    [InlineData("class C:\n x=1\n global x\n", "LA3206")]
    [InlineData("class C:\n print(x)\n global x\n", "LA3206")]
    [InlineData("class C:\n if True:\n  x=1\n  global x\n", "LA3206")]
    [InlineData("class C:\n @x\n def f(): pass\n global x\n", "LA3206")]
    [InlineData("class C:\n def f(a=x): pass\n global x\n", "LA3206")]
    [InlineData("class C:\n global x\n x:int\n", "LA3207")]
    [InlineData("def f():\n x=0\n class C:\n  nonlocal x\n  x:int=1\n", "LA3207")]
    [InlineData("def f():\n x=0\n def g():\n  global x\n  class C:\n   nonlocal x\n", "LA3205")]
    public void InvalidDeclarationsFailBeforeEffects(string source, string code)
    {
        var compiled = new LythonEngine().Compile("print('effect')\n" + source);
        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == code);
    }

    [Fact]
    public async Task ClassDirectivesAwaitHostEffectsAndRetainCells()
    {
        var compiled = new LythonEngine().Compile("""
            from pathlib import Path
            module=0
            def outer():
                cell=1
                class C:
                    global module
                    nonlocal cell
                    module=int(Path('/value.txt').read_text())
                    cell+=module
                    def read(self):
                        return cell
                    Path('/written.txt').write_text(str(cell))
                return C
            C=outer()
            print(module,C().read(),hasattr(C,'module'),hasattr(C,'cell'))
            """);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var immediate = new MockLythonHost();
        immediate.SeedFile("/value.txt", "7");
        var sync = compiled.Run(immediate);
        Assert.True(sync.Success, sync.Failure?.Message);
        var delayed = new DelayedLythonHost();
        delayed.SeedFile("/value.txt", "7");
        var result = await compiled.RunAsync(delayed);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("7 8 False False\n", result.StandardOutput);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.Equal("8", delayed.ReadText("/written.txt"));
        Assert.True(delayed.CompletedAsynchronously >= 2);
    }

    [Fact]
    public async Task LazyTypeAliasesHonorClassDirectives()
    {
        await AssertOutput("""
            x='module'
            def outer():
             x='outer'
             class G:
              global x
              type A=x
             class N:
              nonlocal x
              type A=x
             return G,N
            G,N=outer()
            print(G.A.__value__,N.A.__value__)
            """, "module outer\n");
    }

    [Fact]
    public async Task MixedClosuresShareDeletionAndRebinding()
    {
        await AssertOutput("""
            def outer():
             x=2
             def read(): return x
             class C:
              def clear(self):
               nonlocal x
               del x
              def store(self):
               nonlocal x
               x=9
             return C,read
            C,read=outer()
            print(read())
            C().clear()
            try: read()
            except NameError: print('empty')
            C().store()
            print(read())
            """, "2\nempty\n9\n");
    }

    [Fact]
    public async Task EnclosingGlobalDirectiveStopsFreeCellLookup()
    {
        await AssertOutput("""
            x='module'
            def outer():
             x='outer'
             def middle():
              global x
              class C:
               before=x
               def read(self): return x
              def read(): return x
              return C,read
             return middle()
            C,read=outer()
            print(C.before,C().read(),read())
            """, "module module module\n");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RedirectedBindingsKeepMemoryLimits(bool nonlocal)
    {
        var source = nonlocal
            ? "def outer():\n x=None\n class C:\n  nonlocal x\n  x='a'*1000000\n outer_result=x\nouter()\n"
            : "class C:\n global x\n x='a'*1000000\n";
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        foreach (var result in new[] { compiled.Run(new MockLythonHost(), options), await compiled.RunAsync(new MockLythonHost(), options) })
        {
            Assert.False(result.Success);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.Empty(result.StandardOutput);
        }
    }

    private static async Task AssertOutput(string source, string expected)
    {
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}

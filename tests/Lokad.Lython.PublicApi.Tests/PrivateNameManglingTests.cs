using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class PrivateNameManglingTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return new object[]
        {
            "InheritanceKeepsDistinctFields",
            """
            class Base:
             def __init__(self): self.__x=1
             def read(self): return self.__x
            class Child(Base):
             def __init__(self):
              super().__init__()
              self.__x=2
             def child(self): return self.__x
            x=Child()
            print(x.read(),x.child(),x._Base__x,x._Child__x)
            """,
            "1 2 1 2\n"
        };
        yield return new object[]
        {
            "UnderscoreClassNamesAndDunderExclusions",
            """
            class __C:
             __x=1
             __x_=2
             __x__=3
            class ___:
             __x=4
            print(__C._C__x,__C._C__x_,__C.__x__,___.__x)
            print(hasattr(__C,'__x'),getattr(__C,'_C__x'))
            """,
            "1 2 3 4\nFalse 1\n"
        };
        yield return new object[]
        {
            "NestedClassesUseTheirOwnLexicalName",
            """
            class Base: pass
            class Outer:
             __Base=Base
             class __Inner(__Base):
              __value=7
              def read(self): return self.__value
             __after=8
            print(Outer._Outer__Inner.__name__,Outer._Outer__Inner().read(),Outer._Outer__after)
            __after=9
            print(__after)
            """,
            "__Inner 7 8\n9\n"
        };
        yield return new object[]
        {
            "DefinitionMetadataPreservesSourceNames",
            """
            class C:
             def __method(self): return 1
             class __Nested: pass
             type __Alias = int
             def __generator(self): yield 2
            print(C._C__method.__name__,C._C__method.__qualname__,C._C__Nested.__name__,C._C__Alias.__name__)
            print(C._C__generator.__name__,list(C()._C__generator()))
            """,
            "__method C.__method __Nested __Alias\n__generator [2]\n"
        };
        yield return new object[]
        {
            "PrivateParametersAreMangledButCallLabelsAreLiteral",
            """
            def labels(**kw): return sorted(kw)
            class C:
             def method(self,__x=1,/,*__args,__y=2,**__kwargs):
              return __x,__args,__y,sorted(__kwargs)
             def invoke(self): return labels(__x=1)
            print(C().method(3,4,_C__y=5,__x=6),C().invoke())
            try: C().method(__y=3)
            except TypeError: print('unexpected')
            """,
            "(3, (4,), 5, ['__x']) ['__x']\n"
        };
        yield return new object[]
        {
            "PrivateNamesReachNestedClosures",
            """
            class C:
             def make(self):
              __value=7
              def __read(): return __value
              def __write(value):
               nonlocal __value
               __value=value
              return __read,__write
            read,write=C().make()
            print(read(),read.__name__)
            write(9)
            print(read())
            """,
            "7 __read\n9\n"
        };
        yield return new object[]
        {
            "GlobalDirectivesAndDefinitionsUsePrivateBinding",
            """
            _C__x=1
            class C:
             global __x,__f
             __x=2
             def __f(): return __x
             def read(self): return __x
            print(_C__x,_C__f(),_C__f.__name__,C().read(),hasattr(C,'_C__x'))
            """,
            "2 2 __f 2 False\n"
        };
        yield return new object[]
        {
            "NonlocalDirectivesUsePrivateBinding",
            """
            def outer():
             _C__value=1
             class C:
              nonlocal __value
              __value=3
              def read(self): return __value
              def write(self):
               nonlocal __value
               __value=4
             return C,lambda:_C__value
            C,read=outer()
            print(C().read(),read())
            C().write()
            print(C().read(),read())
            """,
            "3 3\n4 4\n"
        };
        yield return new object[]
        {
            "FormattedFieldsAndSpecsSharePrivateContext",
            """
            class C:
             __width=4
             def __init__(self): self.__x=7
             def text(self): return f'{self.__x:{self.__width}}'
            print(repr(C().text()))
            """,
            "'   7'\n"
        };
        yield return new object[]
        {
            "ComprehensionsAndWalrusUsePrivateNames",
            """
            class C:
             def build(self):
              values=[(__current:=__i) for __i in range(3)]
              return values,__current
            print(C().build())
            """,
            "([0, 1, 2], 2)\n"
        };
        yield return new object[]
        {
            "GeneratorLocalsAndClosuresUsePrivateNames",
            """
            class C:
             def make(self):
              __x=3
              def __read(): return __x
              yield __read()
              __x=yield __read
              yield __read()
            g=C().make()
            print(next(g))
            read=next(g)
            print(read.__name__,read())
            print(g.send(9),read())
            """,
            "3\n__read 3\n9 9\n"
        };
        yield return new object[]
        {
            "PatternKeywordLabelsRemainLiteral",
            """
            class C:
             __value=1
             def read(self,other):
              match other:
               case C(__value=__capture): return __capture
              return 'missing'
            x=C()
            print(x.read(x))
            setattr(x,'__value',2)
            print(x.read(x),x._C__value)
            """,
            "missing\n2 1\n"
        };
        yield return new object[]
        {
            "BuiltinImportAliasesUsePrivateBinding",
            """
            class C:
             import math as __math
             from math import sqrt as __sqrt
             def value(self): return self.__math.floor(3.7),self.__sqrt(16)
            print(C().value(),hasattr(C,'__math'),hasattr(C,'_C__math'))
            """,
            "(3, 4.0) False True\n"
        };
        yield return new object[]
        {
            "GenericNamesKeepMetadataAndMangleBindings",
            """
            class C[__T]:
             type __Alias[__U] = tuple[__T,__U]
             def __f[__V](self,x:__T) -> __V: pass
            print(C.__type_params__[0].__name__)
            print(C._C__Alias.__name__,C._C__Alias.__type_params__[0].__name__)
            print(C._C__f.__name__,C._C__f.__type_params__[0].__name__)
            print(C._C__f.__annotations__['x'] is C.__type_params__[0])
            """,
            "__T\n__Alias __U\n__f __V\nTrue\n"
        };
        yield return new object[]
        {
            "GenericClassHeadersOnlyMangleTypeParameters",
            """
            class Base: pass
            __Base=Base
            class Outer:
             __Base=object
             class __Inner[__T:__Base](__Base):
              __field=3
              def read(self): return self.__field,__T.__name__
            print(Outer._Outer__Inner.__name__,Outer._Outer__Inner().read())
            print(Outer._Outer__Inner.__type_params__[0].__bound__ is Base)
            """,
            "__Inner (3, '__T')\nTrue\n"
        };
        yield return new object[]
        {
            "GenericBoundsReferToPrivateParameters",
            """
            class C[__U,__T:__U]: pass
            print(C.__type_params__[0].__name__,C.__type_params__[1].__name__)
            print(C.__type_params__[1].__bound__ is C.__type_params__[0])
            """,
            "__U __T\nTrue\n"
        };
        yield return new object[]
        {
            "AttributeStringsAndDeletionRemainLiteral",
            """
            class C:
             def __init__(self): self.__x=1
             def clear(self): del self.__x
            x=C()
            setattr(x,'__x',2)
            print(getattr(x,'__x'),x._C__x)
            x.clear()
            print(hasattr(x,'_C__x'),getattr(x,'__x'))
            """,
            "2 1\nFalse 2\n"
        };
        yield return new object[]
        {
            "PatternMangledCollisionsPreserveStoreOrder",
            """
            class C:
             def m(self,x):
              match x:
               case [__x,_C__x]: return __x
            print(C().m([1,2]))
            """,
            "2\n"
        };
        yield return new object[]
        {
            "PatternStarsMappingsAndAliasesKeepSourceCaptures",
            """
            class C:
             def star(self,x):
              match x:
               case [__x,*_C__x]: return __x
             def mapping(self,x):
              match x:
               case {0:__x,**_C__x}: return __x
             def alias(self,x):
              match x:
               case [__x] as _C__x: return __x
            print(C().star([1,2,3]),C().mapping({0:1,2:3}),C().alias([1]))
            """,
            "[2, 3] {2: 3} [1]\n"
        };
        yield return new object[]
        {
            "PatternOrUsesFirstAlternativeCaptureOrder",
            """
            class C:
             def m(self):
              match [1,3,4]:
               case [0,__x,_C__x] | [1,_C__x,__x]: return __x
            print(C().m())
            """,
            "3\n"
        };
        yield return new object[]
        {
            "PrivateKeywordOnlyParametersRejectRawLabels",
            """
            class C:
             def m(self,*,__x): return __x
            print(C().m(_C__x=1))
            try: C().m(__x=2)
            except TypeError: print('unexpected')
            """,
            "1\nunexpected\n"
        };
        yield return new object[]
        {
            "PrivateAnnotatedParameterMetadataUsesMangledBinding",
            """
            class C:
             def m(self,__x:int)->int: return __x
            print(sorted(C.m.__annotations__),C().m(_C__x=2))
            """,
            "['_C__x', 'return'] 2\n"
        };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task PrivateNamesFollowLexicalClass(string name, string source, string expected)
    {
        _ = name;
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }

    [Fact]
    public async Task PrivateImportsKeepLookupPathsAndBindingNamesDistinct()
    {
        var source = """
            class C:
             import __plain
             import __pkg.child
             from helper import __value as __alias
             from __plain import value as __imported
             def read(self):
              return self.__plain.value,self.__pkg.child.value,self.__alias,self.__imported
            print(C().read())
            """;
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var options = new LythonRunOptions
        {
            SourcePath = "/main.py",
            AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal)
            {
                "_C__plain", "__pkg", "__pkg.child", "helper",
                "/_C__plain.py", "/__pkg/__init__.py", "/__pkg/child.py", "/helper.py"
            }
        };
        foreach (var asynchronous in new[] { false, true })
        {
            var host = new MockLythonHost();
            host.SeedFile("/_C__plain.py", "value=1\n");
            host.SeedFile("/__pkg/__init__.py", "\n");
            host.SeedFile("/__pkg/child.py", "value=2\n");
            host.SeedFile("/helper.py", "__value=100\n_C__value=3\n");
            var result = asynchronous ? await compiled.RunAsync(host, options) : compiled.Run(host, options);
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("(1, 2, 3, 1)\n", result.StandardOutput);
        }
    }

    [Fact]
    public async Task PrivatePropertyGetterAndSetterComposeWithDelayedHost()
    {
        var source = """
            class C:
             @property
             def __value(self):
              with open('/value.txt') as f: return int(f.read())
             @__value.setter
             def __value(self,value):
              with open('/value.txt','w') as f: f.write(str(value))
             def increment(self):
              self.__value+=1
              return self.__value
            print(C().increment())
            """;
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var syncHost = new MockLythonHost();
        syncHost.SeedFile("/value.txt", "7");
        var sync = compiled.Run(syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("8\n", sync.StandardOutput);
        var host = new DelayedLythonHost();
        host.SeedFile("/value.txt", "7");
        var result = await compiled.RunAsync(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.Equal("8", host.ReadText("/value.txt"));
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Theory]
    [InlineData("class C:\n def method(self,__x,_C__x): pass\n")]
    [InlineData("class C:\n def method(self):\n  __x=1\n  global __x\n")]
    [InlineData("class C:\n nonlocal __x\n")]
    [InlineData("class C:\n def method(self,other):\n  match other:\n   case [__x,__x]: pass\n")]
    [InlineData("class C:\n def method(self,other):\n  match other:\n   case [__x] | [_C__x]: pass\n")]
    public void InvalidDeclarationsAreCheckedAfterMangling(string source)
    {
        var compiled = new LythonEngine().Compile(source);
        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Severity == LythonDiagnosticSeverity.Error);
    }

    [Fact]
    public async Task PrivateStorageKeepsMemoryLimits()
    {
        var source = "class C:\n def __init__(self): self.__value='x'*1000000\nC()\n";
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PrivateNameExpansionIsBoundedAcrossEmbeddedFields(bool formatted)
    {
        var className = new string('C', 8000);
        var statements = Enumerable.Range(0, 1100).Select(i => formatted
            ? $" self.__value{i}=f'{{self.__read{i}}}'\n"
            : $" __value{i}=0\n");
        var source = "class " + className + ":\n" + string.Concat(statements);
        Assert.True(source.Length < LythonEngine.MaxSourceLength);
        var compiled = new LythonEngine().Compile(source);
        Assert.False(compiled.IsValid);
        Assert.Contains(compiled.Diagnostics, d => d.Code == "LA0004");
        var result = compiled.Run(new MockLythonHost());
        Assert.False(result.Success);
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public async Task LongRepeatedPrivateNamesShareTheirExpandedSpelling()
    {
        var className = new string('C', 8000);
        var source = "class " + className + ":\n __value=1\n" +
            string.Concat(Enumerable.Repeat(" __value=__value\n", 1100)) +
            "print(" + className + "._" + className + "__value)\n";
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("1\n", result.StandardOutput);
        }
    }

    [Fact]
    public async Task ClassNamesNormalizeBeforePrivateNamesAreExpanded()
    {
        var compiled = new LythonEngine().Compile("class \u212A:\n __x=1\nprint(K._K__x,K.__name__)\n");
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("1 K\n", result.StandardOutput);
        }
    }
}

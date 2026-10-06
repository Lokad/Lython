using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ImplicitClassCellTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return new object[]
        {
            "InheritedMethodsKeepDefiningClass",
            """
            class C:
             def owner(self): return __class__
            class D(C): pass
            print(C().owner() is C,D().owner() is C)
            """,
            "True True\n"
        };
        yield return new object[]
        {
            "ClassNameRebindingDoesNotReplaceCell",
            """
            class C:
             def owner(self): return __class__
            saved=C
            C=7
            print(saved().owner() is saved)
            """,
            "True\n"
        };
        yield return new object[]
        {
            "ClassDecoratorReplacementKeepsOriginalCell",
            """
            saved=None
            def replace(original):
             global saved
             saved=original
             class Wrapper: owner=original.owner
             return Wrapper
            @replace
            class C:
             def owner(self): return __class__
            print(C().owner() is saved,C().owner() is C)
            """,
            "True False\n"
        };
        yield return new object[]
        {
            "StaticClassAndPropertyMethodsShareClassCell",
            """
            class C:
             @staticmethod
             def static(): return __class__
             @classmethod
             def cls(cls): return __class__
             @property
             def owner(self): return __class__
            print(C.static() is C,C.cls() is C,C().owner is C)
            """,
            "True True True\n"
        };
        yield return new object[]
        {
            "NestedClosuresAndGeneratorsEscape",
            """
            class C:
             def closures(self):
              def read(): return __class__
              return read,lambda:__class__,(__class__ for _ in range(1))
             def generate(self): yield __class__
            read,lam,gen=C().closures()
            print(read() is C,lam() is C,next(gen) is C,next(C().generate()) is C)
            """,
            "True True True True\n"
        };
        yield return new object[]
        {
            "ClassBodyAndDefaultArgumentsUseOrdinaryNamespace",
            """
            __class__='global'
            class C:
             print(__class__)
             __class__='member'
             def owner(self,value=__class__): return __class__,value
            print(C().owner()[0] is C,C().owner()[1])
            """,
            "global\nTrue member\n"
        };
        yield return new object[]
        {
            "LambdaDefinedInClassReceivesClassCell",
            """
            class C:
             owner=staticmethod(lambda:__class__)
            print(C.owner() is C)
            """,
            "True\n"
        };
        yield return new object[]
        {
            "ClassCellIsEmptyUntilTypeConstruction",
            """
            class C:
             def owner(self): return __class__
             try: owner(None)
             except NameError: print('empty')
            print(C().owner() is C)
            """,
            "empty\nTrue\n"
        };
        yield return new object[]
        {
            "NonlocalMethodsShareCellMutationAndDeletion",
            """
            __class__='global'
            class C:
             def owner(self): return __class__
             def replace(self,value):
              nonlocal __class__
              __class__=value
             def clear(self):
              nonlocal __class__
              del __class__
            saved=C
            c=C()
            c.replace(7)
            print(c.owner())
            c.clear()
            try: c.owner()
            except NameError: print('empty')
            c.replace(saved)
            print(c.owner() is saved)
            """,
            "7\nempty\nTrue\n"
        };
        yield return new object[]
        {
            "NestedClassesHaveIndependentClassCells",
            """
            class Outer:
             def owner(self): return __class__
             class Inner:
              def owner(self): return __class__
            print(Outer().owner() is Outer,Outer.Inner().owner() is Outer.Inner)
            """,
            "True True\n"
        };
        yield return new object[]
        {
            "ExplicitLocalAndGlobalBindingsOverrideImplicitLookup",
            """
            __class__='global'
            class C:
             def local(self,__class__): return __class__
             def glob(self):
              global __class__
              return __class__
            print(C().local(7),C().glob())
            """,
            "7 global\n"
        };
        yield return new object[]
        {
            "SuperUsesLexicalClassAndReceiver",
            """
            class Base:
             def value(self): return 3
            class C(Base):
             def value(self): return super().value()+1
             def inner(self):
              def call(receiver): return super().value()
              return call(self)
            class D(C): pass
            print(D().value(),D().inner())
            """,
            "4 3\n"
        };
        yield return new object[]
        {
            "GenericMethodsKeepClassAndTypeParameterCells",
            """
            class C[T]:
             def owner(self): return __class__,T
            print(C().owner()[0] is C,C().owner()[1] is C.__type_params__[0])
            """,
            "True True\n"
        };
        yield return new object[]
        {
            "NonlocalCellUpdatesAffectLaterSuperCalls",
            """
            class Base:
             def value(self): return 1
            class Other(Base): pass
            class C(Other):
             def value(self):
              nonlocal __class__
              __class__=Other
              return super().value()
            print(C().value())
            """,
            "1\n"
        };
        yield return new object[]
        {
            "ClassCellAvoidsRetainingUnrelatedClassLocals",
            """
            class C:
             hidden=7
             def owner(self):
              try: hidden
              except NameError: return __class__
            print(C().owner() is C)
            """,
            "True\n"
        };
        yield return new object[]
        {
            "LazyAliasAndDirectGeneratorExpressionReceiveClassCell",
            """
            class C:
             type A=__class__
             g=(__class__ for _ in range(1))
            print(C.A.__value__ is C,next(C.g) is C)
            """,
            "True True\n"
        };
        yield return new object[]
        {
            "NestedClassSuiteCapturesEmptyEnclosingCell",
            """
            __class__='global'
            class C:
             try:
              class D:
               print(__class__)
             except NameError: print('empty')
            """,
            "empty\n"
        };
        yield return new object[]
        {
            "NestedClassNonlocalTargetsEnclosingCell",
            """
            class C:
             class D:
              nonlocal __class__
              def owner(self): return __class__
            print(C.D().owner() is C.D)
            """,
            "True\n"
        };
        yield return new object[]
        {
            "NestedClassHeaderReadsMethodClassCell",
            """
            class C:
             def make(self):
              class D(__class__): pass
              return D
            print(issubclass(C().make(),C))
            """,
            "True\n"
        };
        yield return new object[]
        {
            "CompiledGeneratorNestedFunctionsCaptureNonlocalClassCell",
            """
            class C:
             def generate(self):
              def read():
               nonlocal __class__
               return __class__
              def replace(value):
               nonlocal __class__
               __class__=value
              yield read,replace
              yield read()
            g=C().generate()
            read,replace=next(g)
            print(read() is C)
            replace(7)
            print(next(g),read())
            """,
            "True\n7 7\n"
        };
        yield return new object[]
        {
            "LocalAndGlobalClassBindingsDoNotSupplySuperCell",
            """
            __class__=None
            class Base:pass
            class C(Base):
             def local(self):
              __class__=C
              return super()
             def glob(self):
              global __class__
              return super()
            for method in [C().local,C().glob]:
             try: method()
             except RuntimeError: print('missing')
            """,
            "missing\nmissing\n"
        };
        yield return new object[]
        {
            "LambdaSuperUsesLexicalCell",
            """
            class Base:
             def value(self): return 3
            class C(Base):
             call=staticmethod(lambda self:super().value())
            print(C.call(C()))
            """,
            "3\n"
        };
        yield return new object[]
        {
            "NonlocalClassCellCanBecomeInvalidSuperAnchor",
            """
            class Base:pass
            class C(Base):
             def fail(self):
              nonlocal __class__
              __class__=7
              return super()
            try:C().fail()
            except RuntimeError:print('invalid')
            """,
            "invalid\n"
        };
        yield return new object[]
        {
            "ClassAttributeClassNameDoesNotChangeMethodCell",
            """
            class C:
             __class__='attribute'
             def owner(self):return __class__
             type A=__class__
            print(C().owner() is C,C.A.__value__)
            """,
            "True attribute\n"
        };
        yield return new object[]
        {
            "EagerClassComprehensionsUseOrdinaryNameLookup",
            """
            __class__='global'
            class C:
             __items=[1]
             values=[x for x in __items]
             try: owners=[__class__ for x in __items]
             except NameError: print('empty')
            print(C.values,C.owners)
            """,
            "[1] ['global']\n"
        };
        yield return new object[]
        {
            "ClassGeneratorOuterIterableUsesOrdinaryNamespace",
            """
            __class__='global'
            class C:
             g=((value,__class__) for value in [__class__])
            value,owner=next(C.g)
            print(value,owner is C)
            """,
            "global True\n"
        };
        yield return new object[]
        {
            "ClassComprehensionLambdasCaptureClassAndIterationCells",
            """
            class C:
             def owner(self): return __class__
             callbacks=[lambda:(__class__,i) for i in range(2)]
            print(C.callbacks[0]()[0] is C,C.callbacks[0]()[1],C.callbacks[1]()[1])
            """,
            "True 1 1\n"
        };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ClassCellsFollowLexicalDefinition(string name, string source, string expected)
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
    public async Task EscapedClassCellSurvivesDelayedHostCalls()
    {
        var compiled = new LythonEngine().Compile("""
            class C:
             @staticmethod
             def owner():
              with open('/value.txt') as f: f.read()
              return __class__
            saved=C
            C=None
            print(saved.owner() is saved)
            """);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var syncHost = new MockLythonHost();
        syncHost.SeedFile("/value.txt", "hello");
        var sync = compiled.Run(syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("True\n", sync.StandardOutput);
        var host = new DelayedLythonHost();
        host.SeedFile("/value.txt", "hello");
        var result = await compiled.RunAsync(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Fact]
    public async Task RetainedClassCellsObeyExecutionMemoryLimit()
    {
        var compiled = new LythonEngine().Compile("""
            keep=[]
            for i in range(5000):
             class C:
              def owner(self): return __class__
             keep.append(C)
            """);
        Assert.True(compiled.IsValid);
        const long budget = 32768;
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = budget };
        foreach (var result in new[] { compiled.Run(new MockLythonHost(), options), await compiled.RunAsync(new MockLythonHost(), options) })
        {
            Assert.False(result.Success);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.True(result.PeakExecutionMemoryBytes <= budget);
            Assert.True(result.DeniedReservationBytes > 0);
        }
    }

    [Fact]
    public async Task DroppedClassCellCyclesReclaimTheirCharges()
    {
        var compiled = new LythonEngine().Compile("""
            for i in range(5000):
             class C:
              def owner(self): return __class__
            print(C().owner() is C)
            """);
        Assert.True(compiled.IsValid);
        const long budget = 524288;
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = budget };
        foreach (var result in new[] { compiled.Run(new MockLythonHost(), options), await compiled.RunAsync(new MockLythonHost(), options) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("True\n", result.StandardOutput);
            Assert.True(result.PeakExecutionMemoryBytes <= budget);
        }
    }
}

using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// Goldens independently checked under CPython 3.13.16.
public sealed class ComprehensionSuperCompatibilityTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["unbound_comprehension", """
            def outer():
                for value in []: pass
                return [value for i in [1]]
            try: outer()
            except Exception as e: print(type(e).__name__,str(e))
            """, "UnboundLocalError cannot access local variable 'value' where it is not associated with a value\n"];
        yield return ["locals_view", """
            def f():
                outer=1
                values=[(sorted(locals()),locals().get('item'),locals().get('outer')) for item in [2]]
                return values,'item' in locals()
            print(f())
            outer=1
            class C:
                outer=3
                values=[('item' in locals(),locals().get('outer'),outer) for item in [2]]
            print(C.values,hasattr(C,'item'))
            print([(locals().get('outer'),locals().get('item')) for item in [2]])
            print('item' in locals())
            """, "([(['item', 'outer'], 2, 1)], False)\n[(True, None, 1)] False\n[(None, 2)]\nFalse\n"];
        yield return ["basic", """
            class Base:
                def value(self): return 37
            class C(Base):
                def eager(self):
                    return [super().value() for _ in range(1)],{super().value() for _ in range(1)},{_:super().value() for _ in range(1)}
                def generate(self): return (super().value() for _ in range(1))
                def alias(self):
                    make=super
                    return [make().value() for _ in range(1)]
                @classmethod
                def owners(cls): return [super().__self__ is cls for _ in range(1)]
            c=C()
            for name,fn in [('eager',c.eager),('alias',c.alias),('owner',c.owners),('generator',lambda:list(c.generate()))]:
                try: print(name,fn())
                except Exception as e: print(name,type(e).__name__)
            """, "eager ([37], {37}, {0: 37})\nalias [37]\nowner [True]\ngenerator TypeError\n"];
        yield return ["class_namespace", """
            value=11
            class C:
                value=17
                values=[value for _ in range(1)]
                first=[item for item in [value]]
                readers=[lambda:item for item in [1,2]]
                try: owners=[super() for _ in range(1)]
                except Exception as e: error=type(e).__name__
            print(C.values,C.first,C.readers[0](),C.error)
            """, "[11] [17] 2 RuntimeError\n"];
        yield return ["class_target", """
            class C:
                def read(self):
                    values=[(__class__,super().__thisclass__.__name__) for __class__ in [37]]
                    return values,__class__ is C
                def generate(self): return (super().__thisclass__ for __class__ in [37])
            print(C().read())
            try: print(list(C().generate()))
            except Exception as e: print(type(e).__name__,str(e))
            """, "([(37, 'C')], True)\nRuntimeError super(): __class__ cell not found\n"];
        yield return ["escaped_factory", """
            class Base:
                def value(self): return 'base'
            class C(Base):
                def value(self): return 'C'
            class D(C): pass
            class Cursor(D):
                def __init__(self): self.count=0
                def __iter__(self): return self
                def __next__(self):
                    if self.count==2: raise StopIteration
                    self.count+=1
                    return self.count
            def factory():
                __class__=C
                def replace(value):
                    nonlocal __class__
                    __class__=value
                def read(self): return [super().value() for _ in range(1)]
                def generate(self,source):
                    values=(super().value() for _ in source)
                    del self
                    return values
                return replace,read,generate
            replace,read,generate=factory()
            g=generate(D(),Cursor())
            print(read(D()),next(g))
            replace(D)
            print(read(D()),next(g))
            """, "['base'] base\n['C'] C\n"];
        yield return ["generator_iterator", """
            class Base:
                def value(self): return self.name
            class C(Base):
                def generate(self,source): return (super().__self__ for _ in source)
                def nested(self,source): return ([super().value() for _ in range(1)] for item in source)
            class Cursor(C):
                def __init__(self): self.name='iterator';self.count=0
                def __iter__(self): return self
                def __next__(self):
                    if self.count==2: raise StopIteration
                    self.count+=1
                    return self.count
            cursor=Cursor()
            g=C().generate(cursor)
            print(next(cursor),next(g) is cursor)
            try: next(g)
            except StopIteration: print('exhausted')
            print(list(C().nested(Cursor())))
            """, "1 True\nexhausted\n[['iterator'], ['iterator']]\n"];
        yield return ["iteration_cells", """
            class Base:
                def value(self): return self.name
            class C(Base):
                def capture(self,other):
                    before=lambda:self
                    readers=[lambda:self for self in [other]]
                    print(before().name,readers[0]().name,self.name)
                def read(self,other):
                    values=[super().value() for self in [other]]
                    print(values,self.name)
                    try: [super().value() for self in [37]]
                    except Exception as e: print(type(e).__name__,self.name)
                    return [super().value() for self in []],self.name
            a=C()
            a.name='original'
            b=C()
            b.name='replacement'
            a.capture(b)
            print(a.read(b))
            """, "original replacement original\n['replacement'] original\nTypeError original\n([], 'original')\n"];
        yield return ["lexical_cells", """
            import builtins
            class C:
                def forced(self): return super()
                def eager_local(self,make):
                    values=[__class__ for __class__ in [37]]
                    print(values)
                    return make()
                def generator_local(self,make):
                    values=(super() for __class__ in [37])
                    return make()
            for name in ['eager_local','generator_local']:
                try: print(getattr(C(),name)(builtins.super).__thisclass__ is C)
                except Exception as e: print(type(e).__name__,str(e))
            """, "[37]\nRuntimeError super(): __class__ cell not found\nRuntimeError super(): __class__ cell not found\n"];
        yield return ["live_walrus", """
            class Base:
                def value(self): return self.name
            class C(Base):
                def read(self,other):
                    return [(self:=other,super().value())[1] for _ in range(1)],self.name
                def nested(self,other):
                    return [[super().value() for _ in range(1)] for self in [other]],self.name
            a=C()
            a.name='original'
            b=C()
            b.name='replacement'
            print(a.read(b))
            print(a.nested(b))
            """, "(['replacement'], 'replacement')\n([['replacement']], 'original')\n"];
        yield return ["module_reference", """
            values=[super for _ in []]
            class C:
                def deleted(self):
                    del self
                    return [super().__self__ for _ in range(1)]
            try: C().deleted()
            except Exception as e: print(type(e).__name__,str(e))
            """, "RuntimeError super(): arg[0] deleted\n"];
        yield return ["module_shadow", """
            values=[super for super in []]
            class C:
                def deleted(self):
                    del self
                    return [super().__self__ for _ in range(1)]
            try: C().deleted()
            except Exception as e: print(type(e).__name__,str(e))
            """, "RuntimeError super(): arg[0] deleted\n"];
        yield return ["nested_class_scope", """
            import builtins
            class C:
                def forced(self): return super()
                def local(self,make):
                    class D: values=[__class__ for __class__ in [37]]
                    print(D.values)
                    return make()
            try: print(C().local(builtins.super).__thisclass__ is C)
            except Exception as e: print(type(e).__name__,str(e))
            """, "[37]\nRuntimeError super(): __class__ cell not found\n"];
        yield return ["no_arguments_and_empty", """
            class C:
                @staticmethod
                def empty(): return [super() for _ in range(1)]
                def deleted(self):
                    del self
                    return [super().__self__ for _ in range(1)]
                def empty_cell(self):
                    nonlocal __class__
                    del __class__
                    return [super().__self__ for _ in range(1)]
            for name,fn in [('empty',C.empty),('deleted',C().deleted),('cell',C().empty_cell)]:
                try: fn()
                except Exception as e: print(name,type(e).__name__,str(e))
            """, "empty RuntimeError super(): no arguments\ndeleted UnboundLocalError cannot access local variable 'self' where it is not associated with a value\ncell NameError cannot access free variable '__class__' where it is not associated with a value in enclosing scope\n"];
        yield return ["private_first", """
            class Base:
                def value(self): return self.name
            class C(Base):
                def read(__first,other):
                    values=[super().value() for __first in [other]]
                    return values,__first.name
            a=C()
            a.name='original'
            b=C()
            b.name='replacement'
            print(a.read(b))
            """, "(['replacement'], 'original')\n"];
        yield return ["super_target", """
            events=[]
            class Proxy: answer=23
            def replacement(*args):
                events.append(len(args))
                return Proxy()
            class C:
                def eager(self,make):
                    del self
                    return [super().answer for super in [make]]
                def generate(self,make):
                    del self
                    return (super().answer for super in [make])
            print(C().eager(replacement),list(C().generate(replacement)),events)
            """, "[23] [23] [0, 0]\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ComprehensionScopesMatchPython(string name, string source, string expected)
    {
        var compiled = new LythonEngine(Utf8RegexProvider.Instance).Compile(source);
        Assert.True(compiled.IsValid, name + ": " + string.Join("; ", compiled.Diagnostics));
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, name + ": " + result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}

using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class LiveSuperReceiverCompatibilityTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["Positional parameters only", """
            class Base:
                def value(self): return 37
            class C(Base):
                def ordinary(self): return super().value()
                def positional(self, /): return super().value()
                def varargs(*args): return super().value()
                def keywords(**kwargs): return super().value()
                def only_keyword(*,self): return super().value()
            c=C()
            for name,fn in [('ordinary',c.ordinary),('positional',c.positional),('varargs',c.varargs),('keywords',C.keywords),('only_keyword',lambda:C.only_keyword(self=c))]:
                try: print(name,fn())
                except Exception as e: print(name,type(e).__name__,str(e))
            """, "ordinary 37\npositional 37\nvarargs RuntimeError super(): no arguments\nkeywords RuntimeError super(): no arguments\nonly_keyword RuntimeError super(): no arguments\n"];
        yield return ["Live assignments and captures", """
            class Base:
                def value(self): return self.name
            class C(Base):
                def replace(self,other):
                    self=other
                    return super().value()
                def captured(self,other):
                    def change():
                        nonlocal self
                        self=other
                    change()
                    return super().value()
                def generate(self,other):
                    yield 'pause'
                    self=other
                    yield super().value()
            a=C()
            a.name='original'
            b=C()
            b.name='replacement'
            print(a.replace(b),a.captured(b),list(a.generate(b)))
            """, "replacement replacement ['pause', 'replacement']\n"];
        yield return ["Ordinary deletion and aliases", """
            class C:
                def plain(self):
                    del self
                    return super()
                def alias(self):
                    make=super
                    del self
                    return make()
                def captured(self):
                    def remove():
                        nonlocal self
                        del self
                    remove()
                    return super()
            c=C()
            for fn in [c.plain,c.alias,c.captured]:
                try: fn()
                except Exception as e: print(type(e).__name__,str(e))
            """, "RuntimeError super(): arg[0] deleted\nRuntimeError super(): arg[0] deleted\nRuntimeError super(): arg[0] deleted\n"];
        yield return ["No arguments and no class cells", """
            def empty(): return super()
            def argument(value): return super()
            def deleted(value):
                del value
                return super()
            for fn in [empty,lambda:super(),lambda:argument(37),lambda:deleted(37)]:
                try: fn()
                except Exception as e: print(type(e).__name__,str(e))
            try: super()
            except Exception as e: print(type(e).__name__,str(e))
            """, "RuntimeError super(): no arguments\nRuntimeError super(): no arguments\nRuntimeError super(): __class__ cell not found\nRuntimeError super(): arg[0] deleted\nRuntimeError super(): no arguments\n"];
        yield return ["Suspended captured receiver", """
            class Base:
                def value(self): return self.name
            class C(Base):
                def generator(self):
                    def replace(other):
                        nonlocal self
                        self=other
                    yield replace
                    yield super().value()
                def escaped(self):
                    def replace(other):
                        nonlocal self
                        self=other
                    return replace,lambda receiver:super().value(),lambda: self
            a=C()
            a.name='original'
            b=C()
            b.name='replacement'
            gen=a.generator()
            replace=next(gen)
            replace(b)
            print(next(gen))
            replace,read,owner=a.escaped()
            replace(b)
            print(read(b),owner() is b)
            """, "replacement\nreplacement True\n"];
        yield return ["Live classmethod first local", """
            class Base:
                @classmethod
                def value(cls): return cls.__name__
            class C(Base):
                @classmethod
                def replace(cls,other):
                    cls=other
                    return super().value()
            class D(C): pass
            print(C.replace(D),D.replace(C))
            """, "D C\n"];
        yield return ["None makes unbound super", """
            class C:
                def unbound(self):
                    self=None
                    return super()
            value=C().unbound()
            print(value.__self__ is None,value.__self_class__ is None,value.__thisclass__ is C)
            print(super(C,None).__self__ is None)
            """, "True True True\nTrue\n"];
        yield return ["Live receiver with aliased builtin", """
            class Base:
                def value(self): return self.name
            class C(Base):
                def alias(self,other):
                    make=super
                    self=other
                    return make().value()
                def shadow(self,other):
                    super=lambda:other
                    self=None
                    return super().name
                def blocked(self):
                    __class__=C
                    return super()
            a=C()
            a.name='original'
            b=C()
            b.name='replacement'
            print(a.alias(b),a.shadow(b))
            try: a.blocked()
            except RuntimeError as e: print(str(e))
            """, "replacement replacement\nsuper(): __class__ cell not found\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task LiveFirstPositionalLocalMatchesPython(string name, string source, string expected)
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

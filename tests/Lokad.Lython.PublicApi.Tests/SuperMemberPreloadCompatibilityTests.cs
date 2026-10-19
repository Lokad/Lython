using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// Goldens independently checked under CPython 3.13.16.
public sealed class SuperMemberPreloadCompatibilityTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["nonlocal_declaration_cell", """
            import builtins
            class C:
                def read(self,make):
                    nonlocal __class__
                    return make()
            print(C().read(builtins.super).__thisclass__ is C)
            def factory():
                __class__=C
                def read(self,make):
                    nonlocal __class__
                    return make()
                return read
            print(factory()(C(),builtins.super).__thisclass__ is C)
            """, "True\nTrue\n"];
        yield return ["explicit_call_forms", """
            class C:
                def starred(self):
                    del self
                    return super(*[]).__self__
                def keyword(self):
                    del self
                    return super(**{}).__self__
                def explicit(self,other):
                    del self
                    return super(C,other).__self__
            c=C()
            for name in ['starred','keyword']:
                try: getattr(c,name)()
                except Exception as e: print(name,type(e).__name__,str(e))
            print(c.explicit(c) is c)
            """, "starred RuntimeError super(): arg[0] deleted\nkeyword RuntimeError super(): arg[0] deleted\nTrue\n"];
        yield return ["callee_requires_cell", """
            class C:
                def forces(self): return super()
                def other(self,make): return make()
            try: print(C().other(super).__thisclass__ is C)
            except Exception as e: print(type(e).__name__,str(e))
            """, "RuntimeError super(): __class__ cell not found\n"];
        yield return ["deleted_shapes", """
            class C:
                def ordinary(self):
                    del self
                    return super()
                def attribute(self):
                    del self
                    return super().__self__
                def excluded(self):
                    del self
                    return super().__class__
            for name in ['ordinary','attribute','excluded']:
                try: getattr(C(),name)()
                except Exception as e: print(name,type(e).__name__,str(e))
            """, "ordinary RuntimeError super(): arg[0] deleted\nattribute UnboundLocalError cannot access local variable 'self' where it is not associated with a value\nexcluded RuntimeError super(): arg[0] deleted\n"];
        yield return ["dynamic_shadow", """
            events=[]
            class Dummy:
                value=37
            def replacement():
                events.append('called')
                return Dummy()
            class C:
                def attribute(self):
                    del self
                    return super().value
            globals()['super']=replacement
            try: C().attribute()
            except Exception as e: print(type(e).__name__,str(e))
            print(events)
            """, "UnboundLocalError cannot access local variable 'self' where it is not associated with a value\n[]\n"];
        yield return ["empty_class_precedes_deleted_first", """
            class C:
                def empty(self):
                    nonlocal __class__
                    del __class__
                    del self
                    return super().__self__
            try: C().empty()
            except Exception as e: print(type(e).__name__,str(e))
            """, "NameError cannot access free variable '__class__' where it is not associated with a value in enclosing scope\n"];
        yield return ["escaped_ordinary_cells", """
            class Base:
                def value(self): return 'base'
            class C(Base):
                def value(self): return 'C'
            class D(C): pass
            def factory():
                __class__=C
                def replace(value):
                    nonlocal __class__
                    __class__=value
                def remove():
                    nonlocal __class__
                    del __class__
                def generate(self):
                    yield super().value()
                    yield super().value()
                    yield super().value()
                read=lambda self:super().value()
                return replace,remove,generate,read
            replace,remove,generate,read=factory()
            g=generate(D())
            print(next(g),read(D()))
            replace(D)
            print(next(g),read(D()))
            remove()
            for fn in [lambda:next(g),lambda:read(D())]:
                try: fn()
                except Exception as e: print(type(e).__name__,str(e))
            """, "base base\nC C\nNameError cannot access free variable '__class__' where it is not associated with a value in enclosing scope\nNameError cannot access free variable '__class__' where it is not associated with a value in enclosing scope\n"];
        yield return ["future_annotation_reference", """
            from __future__ import annotations
            def other(x:super): pass
            class C:
                def attribute(self):
                    del self
                    return super().__self__
            try: C().attribute()
            except Exception as e: print(type(e).__name__,str(e))
            """, "UnboundLocalError cannot access local variable 'self' where it is not associated with a value\n"];
        yield return ["future_local_annotation_cell", """
            from __future__ import annotations
            import builtins
            class C:
                def forces(self): return super()
                def annotation(self,make):
                    value:super
                    return make()
            try: print(C().annotation(builtins.super).__thisclass__ is C)
            except Exception as e: print(type(e).__name__,str(e))
            """, "RuntimeError super(): __class__ cell not found\n"];
        yield return ["generic_type_param", """
            class C:
                def attribute[super](self):
                    del self
                    return super().__self__
            try: C().attribute()
            except Exception as e: print(type(e).__name__)
            """, "TypeError\n"];
        yield return ["lambda_nested_private", """
            class C:
                def deleted(self):
                    def first(__arg):
                        del __arg
                        return (super()).__self__
                    return first(self)
                @staticmethod
                def empty(value):
                    return lambda first:super().__self__
            print(C.empty(1)(C()).__class__.__name__)
            try: C().deleted()
            except Exception as e: print(type(e).__name__,str(e))
            """, "C\nUnboundLocalError cannot access local variable '_C__arg' where it is not associated with a value\n"];
        yield return ["local_annotation_cell", """
            import builtins
            class C:
                def forces(self): return super()
                def annotation(self,make):
                    value:super
                    return make()
            try: print(C().annotation(builtins.super).__thisclass__ is C)
            except Exception as e: print(type(e).__name__,str(e))
            """, "True\n"];
        yield return ["local_shadow_builtin", """
            import builtins
            class C:
                def local(self,make):
                    super=make
                    del self
                    return super().__self__
            try: C().local(builtins.super)
            except Exception as e: print(type(e).__name__,str(e))
            """, "RuntimeError super(): arg[0] deleted\n"];
        yield return ["module_reference", """
            marker=super
            class C:
                def attribute(self):
                    del self
                    return super().__self__
            try: C().attribute()
            except Exception as e: print(type(e).__name__,str(e))
            """, "RuntimeError super(): arg[0] deleted\n"];
        yield return ["nested_class_free_reads", """
            import builtins
            class C:
                def direct(self,make):
                    class D: value=__class__
                    print(D.value is C)
                    return make()
                def default(self,make):
                    class D: value=lambda first=__class__:first
                    print(D.value() is C)
                    return make()
                def generator(self,make):
                    class D: value=(__class__ for _ in range(1))
                    print(next(D.value) is D)
                    return make()
            for name in ['direct','default','generator']:
                try: print(name,getattr(C(),name)(builtins.super).__thisclass__ is C)
                except Exception as e: print(name,type(e).__name__,str(e))
            """, "True\ndirect True\nTrue\ndefault True\nTrue\ngenerator RuntimeError super(): __class__ cell not found\n"];
        yield return ["nested_class_requires_cell", """
            import builtins
            class C:
                def forces(self): return super()
                def class_body(self,make):
                    class D: value=super
                    return make()
                def class_lambda(self,make):
                    class D: value=lambda self:super()
                    return make()
                def class_local(self,make):
                    class D:
                        __class__=37
                        value=__class__
                    return make()
            for name in ['class_body','class_lambda','class_local']:
                try: print(name,getattr(C(),name)(builtins.super).__thisclass__ is C)
                except Exception as e: print(name,type(e).__name__,str(e))
            """, "class_body RuntimeError super(): __class__ cell not found\nclass_lambda RuntimeError super(): __class__ cell not found\nclass_local RuntimeError super(): __class__ cell not found\n"];
        yield return ["nonlocal_shadow_builtin", """
            import builtins
            class C:
                def outer(self,make):
                    super=make
                    def inner(first):
                        del first
                        return super().__self__
                    return inner(self)
            try: C().outer(builtins.super)
            except Exception as e: print(type(e).__name__,str(e))
            """, "RuntimeError super(): arg[0] deleted\n"];
        yield return ["ordinary_call_requires_class_cell", """
            import builtins
            class B: pass
            class C(B): pass
            def factory():
                __class__=C
                def read(self):
                    make=super
                    return make().__thisclass__
                return read
            print(factory()(C()) is C)
            class Owner:
                def forces(self): return super()
                def unrelated(self,make):
                    def nested(__class__): return __class__
                    return make()
            try: Owner().unrelated(builtins.super)
            except Exception as e: print(type(e).__name__,str(e))
            """, "True\nRuntimeError super(): __class__ cell not found\n"];
        yield return ["ordinary_class_closure", """
            class Base:
                def value(self): return 37
            class C(Base): pass
            def factory():
                __class__=C
                def read(self): return super().value()
                def removed(self):
                    del self
                    return super().value()
                return read,removed
            read,removed=factory()
            try: print(read(C()))
            except Exception as e: print(type(e).__name__,str(e))
            try: removed(C())
            except Exception as e: print(type(e).__name__,str(e))
            """, "37\nUnboundLocalError cannot access local variable 'self' where it is not associated with a value\n"];
        yield return ["ordinary_closure_mutates", """
            class B:
                def value(self): return 'B'
            class C(B):
                def value(self): return 'C'
            class D(C): pass
            def factory():
                __class__=C
                def replace(value):
                    nonlocal __class__
                    __class__=value
                def read(self): return super().value()
                return replace,read
            replace,read=factory()
            print(read(D()))
            replace(D)
            print(read(D()))
            """, "B\nC\n"];
        yield return ["sibling_global", """
            def sibling():
                global super
            class C:
                def attribute(self):
                    del self
                    return super().__self__
            try: C().attribute()
            except Exception as e: print(type(e).__name__,str(e))
            """, "RuntimeError super(): arg[0] deleted\n"];
        yield return ["type_alias_reference", """
            type Alias = super
            class C:
                def attribute(self):
                    del self
                    return super().__self__
            try: C().attribute()
            except Exception as e: print(type(e).__name__,str(e))
            """, "UnboundLocalError cannot access local variable 'self' where it is not associated with a value\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task LexicalSuperReadsMatchPython(string name, string source, string expected)
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

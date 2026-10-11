using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class PropertyConstructorCompatibilityTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["positional_deleter", """
            class C:
             def get(self):return self.value
             def put(self,value):self.value=value
             def remove(self):self.value=-1
             p=property(get,put,remove)
            c=C();c.p=7;print(c.p);del c.p;print(c.p)
            """, "7\n-1\n"];
        yield return ["keyword_deleter", """
            class C:
             def remove(self):print('removed')
             p=property(fdel=remove)
            del C().p
            """, "removed\n"];
        yield return ["positional_doc", """
            def f(obj):return 3
            p=property(f,None,None,'explicit')
            print(p.__doc__,p.fget is f,p.fset is None,p.fdel is None)
            """, "explicit True True True\n"];
        yield return ["keyword_doc", """
            def f(obj):return 3
            p=property(doc='explicit',fget=f)
            print(p.__doc__,p.fget is f)
            """, "explicit True\n"];
        yield return ["empty_constructor", """
            p=property()
            print(p.fget,p.fset,p.fdel,p.__doc__)
            """, "None None None None\n"];
        yield return ["getter_documentation", """
            def f(obj):
             'getter doc'
             return 3
            print(property(f).__doc__,property(f,doc=None).__doc__)
            """, "getter doc getter doc\n"];
        yield return ["arbitrary_doc_alias", """
            doc=[]
            p=property(doc=doc)
            doc.append(3)
            print(p.__doc__ is doc,p.__doc__)
            """, "True [3]\n"];
        yield return ["missing_getter", """
            class C:p=property(doc='empty')
            try:print(C().p)
            except AttributeError:print('missing')
            print(C.p.fget is None)
            """, "missing\nTrue\n"];
        yield return ["missing_setter_and_deleter", """
            class C:
             p=property(lambda obj:3)
            c=C()
            try:c.p=7
            except AttributeError:print('setter missing')
            try:del c.p
            except AttributeError:print('deleter missing')
            """, "setter missing\ndeleter missing\n"];
        yield return ["non_callable_getter_deferred", """
            class C:p=property(1,doc='bad')
            print('constructed',C.p.fget)
            try:print(C().p)
            except TypeError:print('not callable')
            """, "constructed 1\nnot callable\n"];
        yield return ["non_callable_setter_deferred", """
            class C:p=property(None,2,doc='bad')
            print('constructed',C.p.fset)
            try:C().p=7
            except TypeError:print('not callable')
            """, "constructed 2\nnot callable\n"];
        yield return ["non_callable_deleter_deferred", """
            class C:p=property(None,None,3,doc='bad')
            print('constructed',C.p.fdel)
            try:del C().p
            except TypeError:print('not callable')
            """, "constructed 3\nnot callable\n"];
        yield return ["callable_object", """
            class Getter:
             def __call__(self,obj):return obj.value
            getter=Getter()
            class C:p=property(getter)
            c=C();c.value=7
            print(c.p,C.p.fget is getter)
            """, "7 True\n"];
        yield return ["accessor_descriptor_is_not_bound", """
            class Getter:
             def __get__(self,obj,owner):raise ValueError('do not bind')
             def __call__(self,obj):return obj.value
            getter=Getter()
            class C:p=property(getter)
            c=C();c.value=7
            print(c.p)
            """, "7\n"];
        yield return ["bound_method_accessor", """
            class Getter:
             def get(self,obj):return obj.value
            getter=Getter()
            class C:p=property(getter.get)
            c=C();c.value=7
            print(c.p)
            """, "7\n"];
        yield return ["getter_derivative_doc", """
            def first(obj):
             'first'
             return 1
            def second(obj):
             'second'
             return 2
            p=property(first)
            q=p.getter(second)
            print(p.__doc__,q.__doc__,p.fget is first,q.fget is second,p is q)
            """, "first second True True False\n"];
        yield return ["setter_derivative_doc", """
            def first(obj):
             'first'
             return 1
            def put(obj,value):pass
            p=property(first)
            q=p.setter(put)
            print(q.__doc__,q.fget is first,q.fset is put,p.fset is None)
            """, "first True True True\n"];
        yield return ["explicit_doc_derivatives", """
            def f(obj):
             'getter'
             return 3
            doc=[]
            p=property(f,doc=doc)
            q=p.getter(lambda obj:7).setter(lambda obj,value:None).deleter(lambda obj:None)
            print(q.__doc__ is doc,p.__doc__ is doc,q.fset is not None,q.fdel is not None)
            """, "True True True True\n"];
        yield return ["getter_none_derivative", """
            def f(obj):
             'getter'
             return 3
            p=property(f)
            q=p.getter(None)
            print(q.fget is f,q.__doc__,p.fget is f)
            """, "True getter True\n"];
        yield return ["setter_non_callable_derivative", """
            p=property().setter(1)
            print('constructed',p.fset)
            class C:x=p
            try:C().x=7
            except TypeError:print('not callable')
            """, "constructed 1\nnot callable\n"];
        yield return ["deleter_none_derivative", """
            p=property(fdel=lambda obj:None)
            q=p.deleter(None)
            print(q.fdel is None,p.fdel is not None)
            """, "False True\n"];
        yield return ["readonly_accessor_metadata", """
            p=property()
            for name in ['fget','fset','fdel']:
             try:setattr(p,name,7)
             except AttributeError:print(name,'readonly')
            """, "fget readonly\nfset readonly\nfdel readonly\n"];
        yield return ["mutable_doc_alias", """
            p=property(doc='first')
            doc=[]
            p.__doc__=doc
            print(p.__doc__ is doc)
            doc.append(7)
            print(p.__doc__)
            """, "True\n[7]\n"];
        yield return ["doc_mutation_derivative", """
            def f(obj):
             'getter'
             return 3
            p=property(f)
            p.__doc__='mutated'
            print(p.__doc__,p.setter(lambda obj,value:None).__doc__)
            """, "mutated getter\n"];
        yield return ["delete_doc", """
            p=property(doc='first')
            del p.__doc__
            print(p.__doc__)
            """, "None\n"];
        yield return ["missing_getter_doc", """
            class Getter:
             def __getattribute__(self,name):
              if name=='__doc__':raise AttributeError('missing')
              return object.__getattribute__(self,name)
             def __call__(self,obj):return 3
            p=property(Getter())
            print(p.__doc__)
            """, "None\n"];
        yield return ["getter_doc_exception", """
            class Getter:
             def __getattribute__(self,name):
              if name=='__doc__':raise ValueError('doc error')
              return object.__getattribute__(self,name)
             def __call__(self,obj):return 3
            try:property(Getter())
            except ValueError:print('doc error')
            """, "doc error\n"];
        yield return ["getter_doc_object", """
            doc=[]
            class Getter:
             def __getattribute__(self,name):
              if name=='__doc__':return doc
              return object.__getattribute__(self,name)
             def __call__(self,obj):return 3
            p=property(Getter())
            print(p.__doc__ is doc)
            """, "True\n"];
        yield return ["starred_constructor", """
            def f(obj):return 7
            p=property(*[f,None,None],**{'doc':'starred'})
            print(p.fget is f,p.__doc__)
            """, "True starred\n"];
        yield return ["changed_getter_doc_on_derivative", """
            def f(obj):
             'original'
             return 3
            p=property(f)
            f.__doc__='changed'
            q=p.deleter(lambda obj:None)
            print(p.__doc__,q.__doc__)
            """, "original changed\n"];
        yield return ["new_getter_for_empty_property", """
            def f(obj):
             'new getter'
             return 7
            q=property().getter(f)
            print(q.__doc__,q.fget is f)
            """, "new getter True\n"];
        yield return ["duplicate_constructor_argument", """
            def ctor():return property
            try:ctor()(None,fget=None)
            except TypeError:print('duplicate')
            """, "duplicate\n"];
        yield return ["unknown_constructor_argument", """
            def ctor():return property
            try:ctor()(unknown=None)
            except TypeError:print('unknown')
            """, "unknown\n"];
        yield return ["too_many_constructor_arguments", """
            def ctor():return property
            try:ctor()(None,None,None,None,None)
            except TypeError:print('too many')
            """, "too many\n"];
        yield return ["missing_decorator_argument", """
            p=property()
            try:p.getter()
            except TypeError:print('missing')
            """, "missing\n"];
        yield return ["keyword_decorator_argument", """
            p=property()
            try:p.setter(fset=None)
            except TypeError:print('keyword')
            """, "keyword\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task MatchesPythonConstructionMetadataAndAccess(string name, string source, string expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, name + ": " + result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}

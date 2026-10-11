using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class LiveGlobalNamespaceTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["identity", """
            print(globals() is globals())
            """, "True\n"];
        yield return ["later_binding", """
            scope=globals()
            value=3
            print(scope['value'])
            """, "3\n"];
        yield return ["write_existing", """
            value=1
            scope=globals()
            scope['value']=3
            print(value)
            """, "3\n"];
        yield return ["same_function_namespace", """
            def get_scope():return globals()
            print(get_scope() is globals())
            """, "True\n"];
        yield return ["live_key_view", """
            scope=globals()
            keys=scope.keys()
            added=3
            print('added' in keys,'scope' in keys)
            """, "True True\n"];
        yield return ["delete_binding", """
            value=3
            scope=globals()
            del scope['value']
            try:print(value)
            except NameError:print('missing')
            """, "missing\n"];
        yield return ["update", """
            value=1
            globals().update(value=3)
            print(value)
            """, "3\n"];
        yield return ["pop_and_setdefault", """
            value=3
            scope=globals()
            print(scope.pop('value'))
            try:print(value)
            except NameError:print('missing')
            print(scope.setdefault('created',7))
            print(created)
            """, "3\nmissing\n7\n7\n"];
        yield return ["copy_is_independent", """
            value=1
            scope=globals()
            snapshot=scope.copy()
            value=3
            print(scope['value'],snapshot['value'])
            snapshot['value']=8
            print(value)
            """, "3 1\n3\n"];
        yield return ["builtin_shadow_and_delete", """
            scope=globals()
            print('len' in scope)
            scope['len']=lambda value:99
            print(len([1]))
            del scope['len']
            print(len([1]),'len' in scope)
            """, "False\n99\n1 False\n"];
        yield return ["generator_reads_binding", """
            scope=globals()
            value=1
            def generate():yield value
            scope['value']=3
            print(next(generate()))
            """, "3\n"];
        yield return ["clear_keeps_namespace_identity", """
            def clear_scope():
                scope=globals()
                scope['value']=3
                scope.clear()
                print('value' in scope,scope is globals())
            clear_scope()
            """, "False True\n"];
        yield return ["non_string_keys", """
            scope=globals()
            scope[1]='extra'
            print(globals()[1],isinstance(scope,dict))
            """, "extra True\n"];
        yield return ["source_store_and_delete", """
            scope=globals()
            value=3
            value=7
            print(scope['value'])
            del value
            print('value' in scope)
            """, "7\nFalse\n"];
        yield return ["global_statement", """
            value=1
            scope=globals()
            def change():
                global value
                value=9
            change()
            print(scope['value'])
            """, "9\n"];
        yield return ["definitions_after_view", """
            scope=globals()
            def get():return 3
            class Box:pass
            print(scope['get'] is get,scope['Box'] is Box)
            """, "True True\n"];
        yield return ["escaped_local_view", """
            def capture():
                local=7
                return globals()
            scope=capture()
            value=3
            print('local' in scope,scope['value'],scope is globals())
            """, "False 3 True\n"];
        yield return ["class_scope_view", """
            scope=globals()
            class Box:
                local=7
                view=globals()
            print(Box.view is scope,'local' in scope)
            """, "True False\n"];
        yield return ["clear_and_repopulate", """
            def clear(scope):
                scope.clear()
                scope['value']=3
                return scope
            scope=clear(globals())
            print(value,scope is globals())
            """, "3 True\n"];
        yield return ["iterator_source_growth", """
            scope=globals()
            iterator=iter(scope)
            value=3
            try:next(iterator)
            except RuntimeError:print('changed')
            """, "changed\n"];
        yield return ["explicit_builtin_alias", """
            len=len
            scope=globals()
            print('len' in scope,scope['len'] is len)
            del len
            print('len' in scope,len([1]))
            """, "True True\nFalse 1\n"];
        yield return ["equal_protocol_key", """
            value=1
            scope=globals()
            class Key:
                def __hash__(self):return hash('value')
                def __eq__(self,other):return other=='value'
            key=Key()
            scope[key]=3
            print(value,scope['value'])
            del scope[key]
            print('value' in scope)
            """, "3 3\nFalse\n"];
        yield return ["compiler_temporaries_are_private", """
            print(1<2<3)
            scope=globals()
            print(2<3<4)
            match 3:
                case value:pass
            print([key for key in scope if key.startswith('<')])
            """, "True\nTrue\n[]\n"];
        yield return ["extra_key_cannot_change_compiler_temporary", """
            scope=globals()
            def stop():
                scope['<chain_left:0>']=99
                return 2
            print(0<stop()<3,scope['<chain_left:0>'])
            """, "True 99\n"];
        yield return ["with_temporary_survives_fallback", """
            scope=globals()
            class Manager:
                def __enter__(self):return 7
                def __exit__(self,*args):return False
            with Manager() as value:
                def get():return value
                print(value,[key for key in scope if key.startswith('<')])
            print(get())
            """, "7 []\n7\n"];
        yield return ["class_annotation_namespace", """
            from __future__ import annotations
            scope=globals()
            x:Module
            class C:
                global __annotations__
                y:Class
            print(scope['__annotations__'],C.__annotations__)
            """, "{'x': 'Module'} {'y': 'Class'}\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task GlobalDictionarySharesActualModuleNamespace(string name, string source, string expected)
    {
        var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { script.Run(new MockLythonHost()), await script.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}

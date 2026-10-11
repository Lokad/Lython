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

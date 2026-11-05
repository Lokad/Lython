using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class UnhashableKeyOriginTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["slice([],1)", """
            from collections import namedtuple
            import functools
            bad=slice([],1)
            for action in [lambda:hash(bad),lambda:{}.get(bad),lambda:{bad:1},lambda:bad in set()]:
             try:action()
             except TypeError as error:print(str(error))
            @functools.cache
            def cached(value):return 1
            try:cached(bad)
            except TypeError as error:print(str(error))
            """, "unhashable type: 'list'\nunhashable type: 'list'\nunhashable type: 'list'\nunhashable type: 'list'\nunhashable type: 'list'\n"];
        yield return ["(slice([],1),)", """
            from collections import namedtuple
            import functools
            bad=(slice([],1),)
            for action in [lambda:hash(bad),lambda:{}.get(bad),lambda:{bad:1},lambda:bad in set()]:
             try:action()
             except TypeError as error:print(str(error))
            @functools.cache
            def cached(value):return 1
            try:cached(bad)
            except TypeError as error:print(str(error))
            """, "unhashable type: 'list'\nunhashable type: 'list'\nunhashable type: 'list'\nunhashable type: 'list'\nunhashable type: 'list'\n"];
        yield return ["namedtuple('Row','field')([])", """
            from collections import namedtuple
            import functools
            bad=namedtuple('Row','field')([])
            for action in [lambda:hash(bad),lambda:{}.get(bad),lambda:{bad:1},lambda:bad in set()]:
             try:action()
             except TypeError as error:print(str(error))
            @functools.cache
            def cached(value):return 1
            try:cached(bad)
            except TypeError as error:print(str(error))
            """, "unhashable type: 'list'\nunhashable type: 'list'\nunhashable type: 'list'\nunhashable type: 'list'\nunhashable type: 'list'\n"];
        yield return ["generated dataclass wrapped in tuple", """
            from collections import namedtuple
            import functools
            from dataclasses import dataclass
            @dataclass(unsafe_hash=True)
            class Row:
             field: object
            bad=(Row([]),)
            for action in [lambda:hash(bad),lambda:{}.get(bad),lambda:{bad:1},lambda:bad in set()]:
             try:action()
             except TypeError as error:print(str(error))
            @functools.cache
            def cached(value):return 1
            try:cached(bad)
            except TypeError as error:print(str(error))
            """, "unhashable type: 'list'\nunhashable type: 'list'\nunhashable type: 'list'\nunhashable type: 'list'\nunhashable type: 'list'\n"];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task NestedUnhashabilityNamesTheFailingValue(string name, string source, string expected)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics));
        using var synchronous = script.Run(new MockLythonHost());
        Assert.True(synchronous.Success, name + ": " + synchronous.Failure?.Message);
        Assert.Equal(expected, synchronous.StandardOutput);
        using var asynchronous = await script.RunAsync(new MockLythonHost());
        Assert.True(asynchronous.Success, name + ": " + asynchronous.Failure?.Message);
        Assert.Equal(expected, asynchronous.StandardOutput);
    }
}

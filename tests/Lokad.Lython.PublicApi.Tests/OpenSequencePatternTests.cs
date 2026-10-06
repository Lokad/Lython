using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class OpenSequencePatternTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return new object[]
        {
            "pattern_open_star",
            """
            match [1,2]:
             case *rest,:print(rest)
            """,
            "[1, 2]\n"
        };
        yield return new object[]
        {
            "pattern_open_star_after",
            """
            match [1,2]:
             case head,*rest:print(head,rest)
            """,
            "1 [2]\n"
        };
        yield return new object[]
        {
            "pattern_open_pair",
            """
            match [1,2]:
             case a,b:print(a,b)
            """,
            "1 2\n"
        };
        yield return new object[]
        {
            "pattern_open_singleton",
            """
            match [1]:
             case a,:print(a)
            """,
            "1\n"
        };
        yield return new object[]
        {
            "pair_and_trailing_comma",
            """
            for value in [(1,2),[3,4]]:
             match value:
              case a,b,:print(a,b)
            """,
            "1 2\n3 4\n"
        };
        yield return new object[]
        {
            "star_positions",
            """
            match [1,2,3]:
             case head,*rest:print(head,rest)
            match [1,2,3]:
             case *rest,last:print(rest,last)
            match [1,2,3,4]:
             case first,*middle,last,:print(first,middle,last)
            """,
            "1 [2, 3]\n[1, 2] 3\n1 [2, 3] 4\n"
        };
        yield return new object[]
        {
            "singleton_and_star_only",
            """
            for value in [(1,),[],[2,3]]:
             match value:
              case item,:print('one',item)
              case *rest,:print('all',rest)
            """,
            "one 1\nall []\nall [2, 3]\n"
        };
        yield return new object[]
        {
            "negative_subject_controls",
            """
            for value in ['ab',b'ab',{'a':1},3,[],[1,2,3]]:
             match value:
              case a,b:print('pair',a,b)
              case _:print('miss')
            """,
            "miss\nmiss\nmiss\nmiss\nmiss\nmiss\n"
        };
        yield return new object[]
        {
            "or_and_as_items",
            """
            match [2,3,4]:
             case 1|2 as first,*rest:print(first,rest)
            match [1,2]:
             case a,b as alias:print(a,b,alias)
            match [1,2]:
             case a as first,b as last,:print(a,first,b,last)
            """,
            "2 [3, 4]\n1 2 2\n1 1 2 2\n"
        };
        yield return new object[]
        {
            "nested_patterns",
            """
            match [(1,2,3),4]:
             case (a,*tail),b:print(a,tail,b)
            match [{'a':7},8]:
             case {'a':value},last,:print(value,last)
            """,
            "1 [2, 3] 4\n7 8\n"
        };
        yield return new object[]
        {
            "guard_bindings",
            """
            match [1,2,3]:
             case first,*rest if (saved:=rest) and False:print('bad')
             case _:print(first,rest,saved)
            print(first,rest,saved)
            """,
            "1 [2, 3] [2, 3]\n1 [2, 3] [2, 3]\n"
        };
        yield return new object[]
        {
            "class_private_captures",
            """
            class C:
             match [1,2]:
              case __first,__second:pass
            print(C._C__first,C._C__second)
            """,
            "1 2\n"
        };
        yield return new object[]
        {
            "directed_bindings",
            """
            a=0
            def outer():
             b=0
             def inner():
              global a
              nonlocal b
              match [1,2]:
               case a,b:pass
             inner()
             return b
            print(outer(),a)
            """,
            "2 1\n"
        };
        yield return new object[]
        {
            "soft_keyword_captures",
            """
            match [1,2]:
             case match,case,:print(match,case)
            """,
            "1 2\n"
        };
        yield return new object[]
        {
            "explicit_line_joining",
            """
            match [1,2]:
             case a, \
              b:print(a,b)
            """,
            "1 2\n"
        };
        yield return new object[]
        {
            "wildcard_star",
            """
            match [1,2,3]:
             case first,*_,:print(first)
            """,
            "1\n"
        };
        yield return new object[]
        {
            "generator_guard",
            """
            def generate():
             match [1,2,3]:
              case head,*tail if (yield tail):yield head
            g=generate()
            print(next(g),g.send(True))
            """,
            "[2, 3] 1\n"
        };
    }

    [Fact]
    public async Task OpenCapturesSurviveDelayedGuardsInFunctionsAndClasses()
    {
        var compiled = new LythonEngine().Compile("""
            def guard(first,rest):
             with open('/value.txt') as f:f.read()
             print(first,rest)
             return False
            def run():
             match [1,2,3]:
              case head,*tail if guard(head,tail):pass
              case _:print(head,tail)
            run()
            class C:
             match [4,5,6]:
              case __head,*__tail if guard(__head,__tail):pass
              case _:print(__head,__tail)
            print(C._C__head,C._C__tail)
            """);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var syncHost = new MockLythonHost();
        syncHost.SeedFile("/value.txt", "hello");
        var sync = compiled.Run(syncHost);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("1 [2, 3]\n1 [2, 3]\n4 [5, 6]\n4 [5, 6]\n4 [5, 6]\n", sync.StandardOutput);
        var host = new DelayedLythonHost();
        host.SeedFile("/value.txt", "hello");
        var result = await compiled.RunAsync(host);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.True(host.CompletedAsynchronously > 0);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task OpenSequencesFollowPython(string name, string source, string expected)
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

    [Theory]
    [InlineData("*rest")]
    [InlineData("a,*left,*right")]
    [InlineData("a,a")]
    [InlineData("a,*rest as alias")]
    [InlineData("a, | b")]
    [InlineData("(1 as a)|(2 as b),tail")]
    public void InvalidPatternsFailBeforeEffects(string pattern)
    {
        var compiled = new LythonEngine().Compile("print('effect')\nmatch [1,2]:\n case " + pattern + ":pass");
        Assert.False(compiled.IsValid);
    }
}

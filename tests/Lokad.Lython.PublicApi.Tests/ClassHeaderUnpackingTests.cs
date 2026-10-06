using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ClassHeaderUnpackingTests
{
    [Fact]
    public async Task ExpandedBasesAndOptionsPreserveClassConstructionOrder()
    {
        await AssertOutput("""
            events = []
            class A:
                def __init_subclass__(cls, tag, **options):
                    events.append(("init", tag, options))
            class B:
                def value(self):
                    return 3
            def mark(label, value):
                events.append(label)
                return value
            def decorate(cls):
                events.append("decorate")
                return cls
            @mark("decorator", decorate)
            class C(tag=mark("tag", 1), *mark("bases", [A]), *[B], **mark("options", {"extra": 2})):
                events.append("body")
            print(events)
            print(C().value(), isinstance(C(), A), isinstance(C(), B))
            """, "['decorator', 'bases', 'tag', 'options', 'body', ('init', 1, {'extra': 2}), 'decorate']\n3 True True\n");
    }

    [Fact]
    public async Task MappingKeysAreDrainedBeforeValuesAndDuplicatesSkipTheValue()
    {
        await AssertOutput("""
            events = []
            class Base:
                def __init_subclass__(cls, **options):
                    events.append(("init", options))
            class Mapping:
                def keys(self):
                    events.append("keys")
                    def values():
                        events.append("key1")
                        yield "a"
                        events.append("key2")
                        yield "b"
                    return values()
                def __getitem__(self, key):
                    events.append(("get", key))
                    return 1
            class C(Base, **Mapping()):
                events.append("body")
            print(events)
            events.clear()
            try:
                class D(Base, a=1, **Mapping()):
                    events.append("wrong body")
            except TypeError:
                print(events)
            """, "['keys', 'key1', 'key2', ('get', 'a'), ('get', 'b'), 'body', ('init', {'a': 1, 'b': 1})]\n['keys', 'key1', 'key2']\n");
    }

    [Fact]
    public async Task OrdinaryCallsShareTheMappingProtocolOrdering()
    {
        await AssertOutput("""
            events = []
            class Mapping:
                def keys(self):
                    events.append("keys")
                    yield "a"
                    events.append("end keys")
                def __getitem__(self, key):
                    events.append("get")
                    return 1
            def run(**options):
                print(options)
            run(**Mapping())
            print(events)
            events.clear()
            try:
                run(a=2, **Mapping())
            except TypeError:
                print(events)
            """, "{'a': 1}\n['keys', 'end keys', 'get']\n['keys', 'end keys']\n");
    }

    [Fact]
    public async Task NonStringMappingKeysFetchTheirValuesBeforeTypeValidation()
    {
        await AssertOutput("""
            events = []
            class Mapping:
                def keys(self):
                    return [1]
                def __getitem__(self, key):
                    events.append(key)
                    return 2
            try:
                class C(**Mapping()):
                    events.append("wrong body")
            except TypeError:
                print(events)
            """, "[1]\n");
    }

    [Fact]
    public async Task HeaderWalrusBindingsRemainInTheDefiningFunction()
    {
        await AssertOutput("""
            def run():
                class Base:
                    pass
                class C(*[chosen := Base]):
                    pass
                print(chosen is Base, isinstance(C(), Base))
            run()
            """, "True True\n");
    }

    [Fact]
    public async Task GenericHeadersExpandInsideTheirTypeParameterScope()
    {
        await AssertOutput("""
            class Base[T]:
                pass
            class C[T](*[Base[T]]):
                pass
            print(C.__type_params__[0].__name__, C.__orig_bases__[0].__origin__ is Base)
            """, "T True\n");
    }

    [Fact]
    public async Task FailingKeyIterationDoesNotFetchValuesOrEnterTheClassBody()
    {
        await AssertOutput("""
            events = []
            class Mapping:
                def keys(self):
                    events.append("keys")
                    yield "a"
                    raise KeyError("keys")
                def __getitem__(self, key):
                    events.append("wrong get")
                    return 1
            try:
                class C(**Mapping()):
                    events.append("wrong body")
            except TypeError as error:
                print(type(error).__name__, error.__context__ is None, events)
            """, "TypeError True ['keys']\n");
    }

    [Fact]
    public async Task DuplicateExpandedBasesFailAfterTheClassSuite()
    {
        await AssertOutput("""
            events = []
            class Base:
                pass
            try:
                class C(*[Base], *[Base]):
                    events.append("body")
            except TypeError:
                print(events)
            """, "['body']\n");
    }

    [Fact]
    public async Task SpecialTypingBasesCanBeExpanded()
    {
        await AssertOutput("""
            from typing import NamedTuple
            class Row(*[NamedTuple]):
                value: int
            print(Row(3), Row(3).value)
            class Empty(*[]):
                pass
            print(isinstance(Empty(), object))
            """, "Row(value=3) 3\nTrue\n");
    }

    [Theory]
    [InlineData(false, "()", "KeyError")]
    [InlineData(true, "()", "KeyError")]
    [InlineData(false, "('oops',)", "TypeError")]
    [InlineData(true, "('oops',)", "TypeError")]
    [InlineData(false, "(1, 2)", "KeyError")]
    [InlineData(true, "(1, 2)", "KeyError")]
    [InlineData(false, "(None,)", "TypeError")]
    [InlineData(true, "(None,)", "TypeError")]
    public async Task MappingKeyErrorsFollowPythonCallArgumentFormatting(bool classHeader, string arguments, string expectedType)
    {
        var body = classHeader ? "class C(**Mapping()): pass" : "(lambda **options: None)(**Mapping())";
        var source = "class Mapping:\n    def keys(self):\n        raise KeyError(*" + arguments + ")\ntry:\n    " + body +
            "\nexcept Exception as error:\n    print(type(error).__name__, error.__context__ is None)\n";
        await AssertOutput(source, expectedType + " True\n");
    }

    [Fact]
    public async Task UnhandledClassKeywordsRaiseAfterTheClassSuite()
    {
        await AssertOutput("""
            events = []
            try:
                class C(**{"unexpected": 1}):
                    events.append("body")
            except TypeError:
                print(events)
            """, "['body']\n");
    }

    [Theory]
    [InlineData("*1")]
    [InlineData("**1")]
    [InlineData("**{1: 2}")]
    [InlineData("flag=1, **{'flag': 2}")]
    [InlineData("**{'flag': 1}, **{'flag': 2}")]
    public async Task InvalidUnpackingFailsBeforeTheClassSuite(string arguments)
    {
        var compiled = new LythonEngine().Compile("class C(" + arguments + "):\n    print('body')\n");
        Assert.True(compiled.IsValid);
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.False(result.Success);
            Assert.Equal("TypeError", result.Failure?.ExceptionType);
            Assert.Empty(result.StandardOutput);
        }
    }

    [Theory]
    [InlineData("flag=1, object")]
    [InlineData("**{}, *[object]")]
    [InlineData("flag=1, flag=2")]
    public async Task InvalidArgumentOrderAndRepeatedExplicitKeywordsFailAtCompilation(string arguments)
    {
        var compiled = new LythonEngine().Compile("print('effects')\nclass C(" + arguments + "):\n    pass\n");
        Assert.False(compiled.IsValid);
        var result = await compiled.RunAsync(new MockLythonHost());
        Assert.False(result.Success);
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public async Task ExpandedMetaclassOptionsRemainExplicitlyUnsupported()
    {
        var compiled = new LythonEngine().Compile("class C(**{'metaclass': type}):\n    print('body')\n");
        Assert.True(compiled.IsValid);
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.False(result.Success);
            Assert.Equal("TypeError", result.Failure?.ExceptionType);
            Assert.Contains("metaclass", result.Failure?.Message, StringComparison.Ordinal);
            Assert.Empty(result.StandardOutput);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BasesMappingsSubclassHooksAndDecoratorsAwaitDelayedHosts(bool nestedClass)
    {
        var body = """
            @decorate
            class C(*bases(), flag=int(Path("/number.txt").read_text()), **Options()):
                events.append("body")
            print(C.flag, C.extra, events)
            """;
        var source = """
            from pathlib import Path
            events = []
            class Base:
                def __init_subclass__(cls, flag, extra):
                    events.append("init")
                    Path("/written.txt").write_text(str((flag, extra)))
                    cls.flag = flag
                    cls.extra = extra
            def bases():
                events.append("bases")
                Path("/number.txt").read_text()
                yield Base
            class Options:
                @property
                def keys(self):
                    events.append("keys")
                    Path("/number.txt").read_text()
                    return self.iter_keys
                def iter_keys(self):
                    events.append("key")
                    Path("/number.txt").read_text()
                    yield "extra"
                def __getitem__(self, key):
                    events.append("get")
                    return int(Path("/number.txt").read_text())
            def decorate(cls):
                events.append("decorate")
                Path("/number.txt").read_text()
                return cls
            """ + "\n" + (nestedClass ? "class Outer:\n" + string.Join("\n", body.Split('\n').Select(line => "    " + line)) : body);
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var immediate = new MockLythonHost();
        immediate.SeedFile("/number.txt", "2");
        var sync = compiled.Run(immediate);
        Assert.True(sync.Success, sync.Failure?.Message);
        var delayed = new DelayedLythonHost();
        delayed.SeedFile("/number.txt", "2");
        var result = await compiled.RunAsync(delayed);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("2 2 ['bases', 'keys', 'key', 'get', 'body', 'init', 'decorate']\n", result.StandardOutput);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.Equal(immediate.ReadText("/written.txt"), delayed.ReadText("/written.txt"));
        Assert.True(delayed.CompletedAsynchronously >= 7);
    }

    [Fact]
    public async Task DelayedDuplicateMappingsDoNotFetchTheDuplicateValue()
    {
        var compiled = new LythonEngine().Compile("""
            from pathlib import Path
            class Options:
                def keys(self):
                    Path("/number.txt").read_text()
                    return ["flag"]
                def __getitem__(self, key):
                    print("wrong get")
                    return int(Path("/number.txt").read_text())
            try:
                class C(flag=1, **Options()):
                    print("wrong body")
            except TypeError:
                print("caught")
            """);
        Assert.True(compiled.IsValid);
        var delayed = new DelayedLythonHost();
        delayed.SeedFile("/number.txt", "2");
        var result = await compiled.RunAsync(delayed);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("caught\n", result.StandardOutput);
        Assert.True(delayed.CompletedAsynchronously >= 1);
    }

    [Theory]
    [InlineData("bases")]
    [InlineData("keys")]
    public async Task UnpackedHeadersGovernGrowthBeforeClassConstruction(string kind)
    {
        var source = """
            class Base:
                pass
            class Mapping:
                def keys(self):
                    return ("k" + str(i) for i in range(100000))
                def __getitem__(self, key):
                    return 1
            """ + "\nclass C(" + (kind == "bases" ? "*(Base for _ in range(100000))" : "**Mapping()") + "):\n    print('body')\n";
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid);
        foreach (var (options, exceptionType) in new[]
        {
            (new LythonRunOptions { MaxCollectionSize = 3 }, "RuntimeError"),
            (new LythonRunOptions { MaxExecutionMemoryBytes = 262144 }, "MemoryError"),
        })
        {
            foreach (var result in new[] { compiled.Run(new MockLythonHost(), options), await compiled.RunAsync(new MockLythonHost(), options) })
            {
                Assert.False(result.Success);
                Assert.Equal(exceptionType, result.Failure?.ExceptionType);
                Assert.Empty(result.StandardOutput);
            }
        }
    }

    private static async Task AssertOutput(string source, string expected)
    {
        var compiled = new LythonEngine().Compile(source);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        foreach (var result in new[] { compiled.Run(new MockLythonHost()), await compiled.RunAsync(new MockLythonHost()) })
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }
}

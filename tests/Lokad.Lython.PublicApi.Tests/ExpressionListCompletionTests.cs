using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ExpressionListCompletionTests
{
    [Fact]
    public async Task AugmentedExpressionListsPreserveInPlaceIdentity()
    {
        await AssertOutput("""
            xs = [0]
            alias = xs
            xs += 1, 2
            print(xs, xs is alias)
            xs += *[3, 4], 5, *range(6, 8),
            print(xs, xs is alias)
            """, "[0, 1, 2] True\n[0, 1, 2, 3, 4, 5, 6, 7] True\n");
    }

    [Fact]
    public async Task AttributeReceiverAndDescriptorEvaluateBeforeTheExpressionList()
    {
        await AssertOutput("""
            events = []
            class Store:
                def __init__(self):
                    self._value = [0]
                @property
                def value(self):
                    events.append("get")
                    return self._value
                @value.setter
                def value(self, value):
                    events.append("set")
                    self._value = value
            store = Store()
            def receiver():
                events.append("receiver")
                return store
            def mark(value):
                events.append(value)
                return value
            receiver().value += mark(1), *mark([2, 3]),
            print(events, store._value)
            """, "['receiver', 'get', 1, [2, 3], 'set'] [0, 1, 2, 3]\n");
    }

    [Fact]
    public async Task SubscriptReceiverAndIndexEvaluateOnce()
    {
        await AssertOutput("""
            events = []
            values = [[0]]
            def receiver():
                events.append("receiver")
                return values
            def index():
                events.append("index")
                return 0
            def mark(value):
                events.append(value)
                return value
            receiver()[index()] += mark(1), *mark([2, 3]),
            print(events, values)
            """, "['receiver', 'index', 1, [2, 3]] [[0, 1, 2, 3]]\n");
    }

    [Fact]
    public async Task StandaloneStarredExpressionListsEvaluateInOrder()
    {
        await AssertOutput("""
            events = []
            def values(label):
                events.append(label)
                return range(2)
            *values("first"), *values("second"),
            *values("third"),
            print(events)
            """, "['first', 'second', 'third']\n");
    }

    [Fact]
    public async Task FailedUnpackingDoesNotApplyTheAugmentedStore()
    {
        await AssertOutput("""
            xs = [0]
            events = []
            def mark(value):
                events.append(value)
                return value
            try:
                xs += mark(1), *mark(2),
            except TypeError:
                print(xs, events)
            """, "[0] [1, 2]\n");
    }

    [Theory]
    [InlineData("*xs")]
    [InlineData("*xs = [1, 2]")]
    [InlineData("xs += *[1, 2]")]
    public async Task BareStarredFormsRemainInvalidBeforeEffects(string statement)
    {
        var compiled = new LythonEngine().Compile("print('effects')\nxs = []\n" + statement + "\n");
        Assert.False(compiled.IsValid);
        var result = await compiled.RunAsync(new MockLythonHost());
        Assert.False(result.Success);
        Assert.Empty(result.StandardOutput);
    }

    [Theory]
    [InlineData("xs += *range(10),")]
    [InlineData("*range(10),")]
    public async Task ExpressionListUnpackingRespectsCollectionLimits(string statement)
    {
        var compiled = new LythonEngine().Compile("xs = []\n" + statement + "\n");
        Assert.True(compiled.IsValid);
        foreach (var result in new[]
        {
            compiled.Run(new MockLythonHost(), new LythonRunOptions { MaxCollectionSize = 3 }),
            await compiled.RunAsync(new MockLythonHost(), new LythonRunOptions { MaxCollectionSize = 3 }),
        })
        {
            Assert.False(result.Success);
            Assert.Equal("RuntimeError", result.Failure?.ExceptionType);
            Assert.Contains("maximum collection size exceeded", result.Failure?.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task AugmentedDescriptorAndExpressionListAwaitDelayedHosts()
    {
        var compiled = new LythonEngine().Compile("""
            from pathlib import Path
            class Store:
                def __init__(self):
                    self._value = [0]
                @property
                def value(self):
                    Path("/number.txt").read_text()
                    return self._value
                @value.setter
                def value(self, value):
                    Path("/written.txt").write_text(str(value))
                    self._value = value
            store = Store()
            store.value += int(Path("/number.txt").read_text()),
            print(store._value)
            """);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var immediate = new MockLythonHost();
        immediate.SeedFile("/number.txt", "2");
        var sync = compiled.Run(immediate);
        Assert.True(sync.Success, sync.Failure?.Message);
        var delayed = new DelayedLythonHost();
        delayed.SeedFile("/number.txt", "2");
        var result = await compiled.RunAsync(delayed);
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("[0, 2]\n", result.StandardOutput);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.Equal(immediate.ReadText("/written.txt"), delayed.ReadText("/written.txt"));
        Assert.True(delayed.CompletedAsynchronously >= 3);
    }

    [Theory]
    [InlineData("cached")]
    [InlineData("descriptor")]
    [InlineData("getattr")]
    [InlineData("getattribute")]
    public async Task AugmentedReadsAwaitAttributeProtocolsAndCacheOnce(string protocol)
    {
        var declaration = protocol switch
        {
            "cached" => """
                from functools import cached_property
                class Store:
                    @cached_property
                    def value(self):
                        events.append("get")
                        Path("/number.txt").read_text()
                        return [0]
                """,
            "descriptor" => """
                class Descriptor:
                    def __get__(self, instance, owner):
                        events.append("get")
                        Path("/number.txt").read_text()
                        return instance._value
                    def __set__(self, instance, value):
                        events.append("set")
                        Path("/written.txt").write_text(str(value))
                        instance._value = value
                class Store:
                    value = Descriptor()
                    def __init__(self):
                        self._value = [0]
                """,
            "getattr" => """
                class Store:
                    def __getattr__(self, name):
                        events.append("get")
                        Path("/number.txt").read_text()
                        return [0]
                """,
            _ => """
                class Store:
                    def __init__(self):
                        self.value = [0]
                    def __getattribute__(self, name):
                        if name == "value":
                            events.append("get")
                            Path("/number.txt").read_text()
                        return object.__getattribute__(self, name)
                """,
        };
        var compiled = new LythonEngine().Compile("from pathlib import Path\nevents = []\n" + declaration + "\n" + """
            store = Store()
            store.value += int(Path("/number.txt").read_text()),
            print(store.value)
            print(events)
            """);
        Assert.True(compiled.IsValid, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var immediate = new MockLythonHost();
        immediate.SeedFile("/number.txt", "2");
        var sync = compiled.Run(immediate);
        Assert.True(sync.Success, sync.Failure?.Message);
        var delayed = new DelayedLythonHost();
        delayed.SeedFile("/number.txt", "2");
        var result = await compiled.RunAsync(delayed);
        Assert.True(result.Success, result.Failure?.Message);
        var expectedEvents = protocol switch
        {
            "descriptor" => "['get', 'set', 'get']",
            "getattribute" => "['get', 'get']",
            _ => "['get']",
        };
        Assert.Equal("[0, 2]\n" + expectedEvents + "\n", result.StandardOutput);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        if (protocol == "descriptor")
            Assert.Equal(immediate.ReadText("/written.txt"), delayed.ReadText("/written.txt"));
        Assert.True(delayed.CompletedAsynchronously >= 2);
    }

    [Theory]
    [InlineData("xs += *range(100000),")]
    [InlineData("*range(100000),")]
    public async Task ExpressionListsReserveMemoryBeforeUnpackingGrowth(string statement)
    {
        var compiled = new LythonEngine().Compile("xs = []\n" + statement + "\n");
        Assert.True(compiled.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 262144 };
        foreach (var result in new[]
        {
            compiled.Run(new MockLythonHost(), options),
            await compiled.RunAsync(new MockLythonHost(), options),
        })
        {
            Assert.False(result.Success);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.Empty(result.StandardOutput);
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

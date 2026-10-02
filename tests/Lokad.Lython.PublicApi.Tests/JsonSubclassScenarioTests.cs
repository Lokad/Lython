using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N39 (part 1): guest subclasses of json.JSONEncoder/JSONDecoder use the
// ordinary class machinery (construction, super(), isinstance) with engine
// peers behind inherited members. cls= dispatch lands separately. Each case
// runs funded in sync and async modes; expectations verified vs CPython 3.13.2.
public sealed class JsonSubclassScenarioTests
{
    private static async Task<object?[]> RunBothModes(string script)
    {
        var compiled = new LythonEngine().Compile(script);
        Assert.True(compiled.IsValid);
        var sync = compiled.Run(new MockLythonHost());
        Assert.True(sync.Success, sync.Failure?.Message);
        var asyncResult = await compiled.RunAsync(new MockLythonHost());
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        return [sync.ReturnValue, asyncResult.ReturnValue];
    }

    [Fact]
    public async Task EncoderSubclassOverridesDefault()
    {
        var results = await RunBothModes(
            """
            import json
            class SetEncoder(json.JSONEncoder):
                def default(self, obj):
                    if isinstance(obj, set):
                        return sorted(obj)
                    return super().default(obj)
            e = SetEncoder()
            out = []
            out.append(e.encode({2, 1}))
            out.append(str(isinstance(e, json.JSONEncoder)))
            out.append(str(isinstance(e, SetEncoder)))
            out.append(str(issubclass(SetEncoder, json.JSONEncoder)))
            out.append(str(type(e) == SetEncoder))
            try:
                e.default(object())
                out.append("ok")
            except TypeError as exc:
                out.append(str(exc))
            return "|".join(out)
            """);
        foreach (var result in results)
        {
            Assert.Equal("[1, 2]|True|True|True|True|Object of type object is not JSON serializable", result);
        }
    }

    [Fact]
    public async Task DecoderSubclassConfiguresHooks()
    {
        var results = await RunBothModes(
            """
            import json
            class Strict(json.JSONDecoder):
                def __init__(self):
                    super().__init__(parse_int=lambda t: 99)
            d = Strict()
            out = []
            out.append(str(d.decode("[1, 2]")))
            out.append(str(isinstance(d, json.JSONDecoder)))
            out.append(str(issubclass(Strict, json.JSONDecoder)))
            return "|".join(out)
            """);
        foreach (var result in results)
        {
            Assert.Equal("[99, 99]|True|True", result);
        }
    }

    [Fact]
    public async Task MethodOverridesDispatchWithoutBypass()
    {
        var results = await RunBothModes(
            """
            import json
            class Wrap(json.JSONEncoder):
                def encode(self, o):
                    return "W:" + super().encode(o)
            class It(json.JSONEncoder):
                def iterencode(self, o):
                    return ["S:"] + list(super().iterencode(o))
            class Raw(json.JSONDecoder):
                def raw_decode(self, s, idx=0):
                    v, e = super().raw_decode(s, idx)
                    return (v, e + 1000)
            class Dec(json.JSONDecoder):
                def decode(self, s):
                    return ("D", super().decode(s))
            out = []
            out.append(Wrap().encode({"a": 1}))
            out.append("".join(It().iterencode([1])))
            v, e = Raw().raw_decode("[1]")
            out.append(str(e))
            out.append(str(Dec().decode("[1]")))
            return "|".join(out)
            """);
        foreach (var result in results)
        {
            Assert.Equal("W:{\"a\": 1}|S:[1]|1003|('D', [1])", result);
        }
    }

    [Fact]
    public async Task SuperInitBindsOptions()
    {
        var results = await RunBothModes(
            """
            import json
            class Sorted(json.JSONEncoder):
                def __init__(self, **kw):
                    super().__init__(sort_keys=True, **kw)
            out = []
            out.append(Sorted().encode({"b": 1, "a": 2}))
            out.append(str(Sorted().sort_keys))
            try:
                Sorted(bogus=1)
                out.append("ok")
            except TypeError:
                out.append("K")
            try:
                Sorted(True)
                out.append("ok")
            except TypeError:
                out.append("P")
            return "|".join(out)
            """);
        foreach (var result in results)
        {
            Assert.Equal("{\"a\": 2, \"b\": 1}|True|K|P", result);
        }
    }
    [Fact]
    public async Task ChainedSubclassesShareBehavior()
    {
        var results = await RunBothModes(
            """
            import json
            class A(json.JSONEncoder):
                def default(self, o):
                    return "A"
            class B(A):
                pass
            out = []
            out.append(B().encode(object()))
            out.append(B().default(object()))
            out.append(str(isinstance(B(), json.JSONEncoder)))
            out.append(str(isinstance(B(), A)))
            out.append(str(issubclass(B, json.JSONEncoder)))
            out.append(str(issubclass(B, A)))
            return "|".join(out)
            """);
        foreach (var result in results)
        {
            Assert.Equal("\"A\"|A|True|True|True|True", result);
        }
    }

    [Fact]
    public async Task HookAndOverridePrecedenceMatchesCpython()
    {
        var results = await RunBothModes(
            """
            import json
            def hook(o):
                return "HOOK"
            class H(json.JSONEncoder):
                def __init__(self, **kw):
                    super().__init__(default=hook, **kw)
            class Both(json.JSONEncoder):
                def __init__(self, **kw):
                    super().__init__(default=hook, **kw)
                def default(self, o):
                    return "OVERRIDE"
            out = []
            out.append(H().encode({1}))
            out.append(str(H().default({1})))
            out.append(Both().encode({1}))
            out.append(str(Both().default({1})))
            return "|".join(out)
            """);
        foreach (var result in results)
        {
            Assert.Equal("\"HOOK\"|HOOK|\"HOOK\"|HOOK", result);
        }
    }

    [Fact]
    public async Task SkippedSuperFailsAttributeError()
    {
        var results = await RunBothModes(
            """
            import json
            class E(json.JSONEncoder):
                def __init__(self):
                    pass
            out = []
            out.append(str(type(E.__new__(E)) == E))
            try:
                E().encode({})
                out.append("ok")
            except AttributeError as exc:
                out.append("A" + str("has no attribute" in str(exc)))
            try:
                E().default({})
                out.append("ok")
            except AttributeError:
                out.append("D")
            return "|".join(out)
            """);
        foreach (var result in results)
        {
            Assert.Equal("True|ATrue|D", result);
        }
    }

    [Fact]
    public async Task SubclassCallbackExceptionsPropagate()
    {
        var results = await RunBothModes(
            """
            import json
            def boom(o):
                raise RuntimeError("cb")
            class Wrap(json.JSONEncoder):
                def encode(self, o):
                    return super().encode(o)
            out = []
            try:
                Wrap(default=boom).encode({1, 2})
                out.append("ok")
            except RuntimeError:
                out.append("R")
            class Strict(json.JSONDecoder):
                def __init__(self):
                    super().__init__(object_hook=boom)
            try:
                Strict().decode("{}")
                out.append("ok")
            except RuntimeError:
                out.append("J")
            return "|".join(out)
            """);
        foreach (var result in results)
        {
            Assert.Equal("R|J", result);
        }
    }
    [Fact]
    public async Task ClsDispatchDecodesThroughSelectedClass()
    {
        var results = await RunBothModes(
            """
            import json
            import json as j
            class Strict(json.JSONDecoder):
                def __init__(self, **kw):
                    super().__init__(parse_int=lambda t: 99, **kw)
            class Wrap(json.JSONDecoder):
                def decode(self, s):
                    return ("W", super().decode(s))
            out = []
            out.append(str(json.loads("[1, 2]", cls=Strict)))
            out.append(str(json.loads("[1]", cls=Wrap)))
            out.append(str(json.loads("[1]", cls=j.JSONDecoder)))
            out.append(str(json.loads("[1]", cls=json.JSONDecoder)))
            from json import JSONDecoder as JD
            out.append(str(json.loads("[1]", cls=JD)))
            try:
                json.loads("{}", cls=json.JSONEncoder)
                out.append("ok")
            except AttributeError:
                out.append("K")
            try:
                json.loads("{}", cls=int)
                out.append("ok")
            except NotImplementedError:
                out.append("N")
            class Plain:
                pass
            try:
                json.loads("{}", cls=Plain)
                out.append("ok")
            except NotImplementedError:
                out.append("P")
            return "|".join(out)
            """);
        foreach (var result in results)
        {
            Assert.Equal("[99, 99]|('W', [1])|[1]|[1]|[1]|K|N|P", result);
        }
    }

    [Fact]
    public async Task ClsDispatchEncodesThroughSelectedClass()
    {
        var results = await RunBothModes(
            """
            import json
            from json import JSONEncoder as JE
            class SetEncoder(json.JSONEncoder):
                def default(self, obj):
                    if isinstance(obj, set):
                        return sorted(obj)
                    return super().default(obj)
            class Tag(json.JSONEncoder):
                def __init__(self, tag="T", **kw):
                    super().__init__(**kw)
                    self.tag = tag
                def default(self, o):
                    return self.tag
            class Wrap(json.JSONEncoder):
                def encode(self, o):
                    return "W:" + super().encode(o)
            out = []
            out.append(json.dumps({2, 1}, cls=SetEncoder))
            out.append(json.dumps({2, 1}, cls=SetEncoder, sort_keys=True))
            out.append(json.dumps({2}, cls=Tag, tag="X"))
            out.append(json.dumps({2}, cls=Tag))
            out.append(json.dumps([1], cls=JE))
            out.append(json.dumps([1], cls=Wrap))
            try:
                json.dumps({}, cls=json.JSONDecoder)
                out.append("ok")
            except TypeError:
                out.append("K")
            try:
                json.dumps({}, cls=int)
                out.append("ok")
            except NotImplementedError:
                out.append("N")
            return "|".join(out)
            """);
        foreach (var result in results)
        {
            Assert.Equal("[1, 2]|[1, 2]|\"X\"|\"T\"|[1]|W:[1]|K|N", result);
        }
    }
    [Fact]
    public async Task ClsMalformedCallsFailLikeCpython()
    {
        var results = await RunBothModes(
            """
            import json
            class SetEncoder(json.JSONEncoder):
                def default(self, obj):
                    if isinstance(obj, set):
                        return sorted(obj)
                    return super().default(obj)
            out = []
            try:
                json.dumps([1], cls=SetEncoder, bogus=1)
                out.append("ok")
            except TypeError as exc:
                out.append("T" + str("bogus" in str(exc)))
            try:
                json.loads("[1]", cls=SetEncoder)
                out.append("ok")
            except AttributeError:
                out.append("K")
            try:
                json.dumps([1], cls=SetEncoder, default=42)
                out.append("ok")
            except TypeError:
                out.append("H")
            def boom(o):
                raise RuntimeError("cb")
            try:
                json.dumps({1}, cls=SetEncoder, default=boom)
                out.append("ok")
            except RuntimeError:
                out.append("R")
            try:
                json.loads("[1]", bogus=1)
                out.append("ok")
            except TypeError as exc:
                out.append("B" + str("bogus" in str(exc)))
            return "|".join(out)
            """);
        foreach (var result in results)
        {
            Assert.Equal("TTrue|K|H|R|BTrue", result);
        }
    }

    [Fact]
    public async Task ClsFileRoundTripThroughSelectedClasses()
    {
        var host = new MockLythonHost();
        host.SeedFile("/input.json", "[1, 2]");
        var compiled = new LythonEngine().Compile(
            """
            import json
            class Strict(json.JSONDecoder):
                def __init__(self, **kw):
                    super().__init__(parse_int=lambda t: 99, **kw)
            class SetEncoder(json.JSONEncoder):
                def default(self, obj):
                    if isinstance(obj, set):
                        return sorted(obj)
                    return super().default(obj)
            with open("/input.json", "r") as handle:
                loaded = json.load(handle, cls=Strict)
            with open("/out.txt", "w") as handle:
                json.dump({2, 1}, handle, cls=SetEncoder)
            return str(loaded)
            """);
        Assert.True(compiled.IsValid);
        var sync = compiled.Run(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("[99, 99]", sync.ReturnValue);
        Assert.Equal("[1, 2]", host.ReadText("/out.txt"));

        var host2 = new MockLythonHost();
        host2.SeedFile("/input.json", "[1, 2]");
        var asyncResult = await compiled.RunAsync(host2);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("[99, 99]", asyncResult.ReturnValue);
        Assert.Equal("[1, 2]", host2.ReadText("/out.txt"));
    }
    [Fact]
    public async Task SubclassInstancesReleasePerCall()
    {
        // Repeated construction, use and drop of subclass instances must not
        // accumulate peers or option state against the budget.
        var script = new LythonEngine().Compile(
            """
            import json
            class SetEncoder(json.JSONEncoder):
                def default(self, obj):
                    if isinstance(obj, set):
                        return sorted(obj)
                    return super().default(obj)
            n = 0
            for _ in range(2000):
                if json.dumps({1}, cls=SetEncoder) == "[1]":
                    n = n + 1
            return n
            """);
        Assert.True(script.IsValid);
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 4194304 };
        var sync = script.Run(new MockLythonHost(), options);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal(new BigInteger(2000), sync.ReturnValue);
        var asyncResult = await script.RunAsync(new MockLythonHost(), options);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal(new BigInteger(2000), asyncResult.ReturnValue);
    }
}

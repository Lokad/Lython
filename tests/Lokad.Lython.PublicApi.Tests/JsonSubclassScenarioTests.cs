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
}

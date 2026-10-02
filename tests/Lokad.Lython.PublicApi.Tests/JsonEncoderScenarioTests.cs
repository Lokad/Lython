using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N38 (part 1): ordinary JSONEncoder class identity, keyword-only
// construction, encode()/default() and cls= dispatch through the default
// class. Incremental iterencode streaming lands separately. Each case runs
// funded in sync and async modes; expectations verified vs CPython 3.13.2.
public sealed class JsonEncoderScenarioTests
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
    public async Task EncoderInstancesAreReusableAndIndependent()
    {
        var results = await RunBothModes(
            """
            import json
            import json as j
            e = json.JSONEncoder()
            checks = []
            checks.append(str(isinstance(e, json.JSONEncoder)))
            checks.append(str(isinstance(e, j.JSONEncoder)))
            checks.append(str(type(e) == json.JSONEncoder))
            checks.append(str(e.encode.__self__ is e))
            checks.append(str(e.default.__self__ is e))
            checks.append(e.encode({"b": 1}))
            checks.append(e.encode({"b": 1}))
            other = json.JSONEncoder(sort_keys=True)
            checks.append(other.encode({"b": 1, "a": 2}))
            checks.append(e.encode({"b": 1, "a": 2}))
            return "|".join(checks)
            """);
        foreach (var result in results)
        {
            Assert.Equal("True|True|True|True|True|{\"b\": 1}|{\"b\": 1}|{\"a\": 2, \"b\": 1}|{\"b\": 1, \"a\": 2}", result);
        }
    }

    [Fact]
    public async Task EncoderConstructorBindsKeywordOnlyOptions()
    {
        var results = await RunBothModes(
            """
            import json
            out = []
            try:
                json.JSONEncoder(True)
                out.append("ok")
            except TypeError:
                out.append("P")
            try:
                json.JSONEncoder(foo=1)
                out.append("ok")
            except TypeError:
                out.append("K")
            try:
                json.JSONEncoder(indent=3.5)
                out.append("ok")
            except TypeError:
                out.append("I")
            try:
                json.JSONEncoder(indent=33)
                out.append("ok")
            except OverflowError:
                out.append("O")
            try:
                json.JSONEncoder(separators=(",",))
                out.append("ok")
            except TypeError:
                out.append("S")
            try:
                json.JSONEncoder(default=42)
                out.append("ok")
            except TypeError:
                out.append("H")
            return "|".join(out)
            """);
        foreach (var result in results)
        {
            Assert.Equal("P|K|I|O|S|H", result);
        }
    }

    [Fact]
    public async Task EncoderAttributesExposeConstructionOptions()
    {
        var results = await RunBothModes(
            """
            import json
            e = json.JSONEncoder()
            out = []
            out.append("|".join([str(e.skipkeys), str(e.ensure_ascii), str(e.check_circular), str(e.allow_nan), str(e.sort_keys), str(e.indent), e.item_separator, e.key_separator]))
            e2 = json.JSONEncoder(indent=2, sort_keys=True, skipkeys=1)
            out.append("|".join([str(e2.indent), e2.item_separator, e2.key_separator, str(e2.sort_keys), str(e2.skipkeys)]))
            e3 = json.JSONEncoder(indent="\t", separators=(",", ":"))
            out.append("|".join([e3.indent, e3.item_separator, e3.key_separator]))
            return "\n".join(out)
            """);
        foreach (var result in results)
        {
            Assert.Equal("False|True|True|True|False|None|, |: \n2|,|: |True|1\n\t|,|:", result);
        }
    }

    [Fact]
    public async Task EncodeMatchesCpythonOutputs()
    {
        var results = await RunBothModes(
            """
            import json
            out = []
            out.append(json.JSONEncoder(sort_keys=True).encode({"b": 1, "a": 2}))
            out.append(json.JSONEncoder(separators=(",", ":")).encode({"a": [1, 2]}))
            out.append(json.JSONEncoder(indent=2).encode({"a": 1}))
            out.append(json.JSONEncoder(ensure_ascii=False).encode({"e": "é"}))
            out.append(json.JSONEncoder(skipkeys=True).encode({(1, 2): "a", "b": 1}))
            out.append(json.JSONEncoder().encode((1, "a", None)))
            return "\n".join(out)
            """);
        foreach (var result in results)
        {
            Assert.Equal("{\"a\": 2, \"b\": 1}\n{\"a\":[1,2]}\n{\n  \"a\": 1\n}\n{\"e\": \"é\"}\n{\"b\": 1}\n[1, \"a\", null]", result);
        }
    }
    [Fact]
    public async Task EncodeErrorsMatchCpythonCategories()
    {
        var results = await RunBothModes(
            """
            import json
            e = json.JSONEncoder()
            out = []
            try:
                json.JSONEncoder(allow_nan=False).encode(float("nan"))
                out.append("ok")
            except ValueError:
                out.append("N")
            cycle = []
            cycle.append(cycle)
            try:
                e.encode(cycle)
                out.append("ok")
            except ValueError:
                out.append("C")
            try:
                e.encode({(1, 2): "a"})
                out.append("ok")
            except TypeError:
                out.append("K")
            try:
                e.encode({1, 2})
                out.append("ok")
            except TypeError as exc:
                out.append("S" + str("set" in str(exc)))
            return "|".join(out)
            """);
        foreach (var result in results)
        {
            Assert.Equal("N|C|K|STrue", result);
        }
    }

    [Fact]
    public async Task BaseDefaultRaisesWhileHooksExtend()
    {
        var results = await RunBothModes(
            """
            import json
            e = json.JSONEncoder()
            out = []
            try:
                e.default({1, 2})
                out.append("ok")
            except TypeError as exc:
                out.append(str(exc))
            class Foo:
                pass
            try:
                e.default(Foo())
                out.append("ok")
            except TypeError as exc:
                out.append(str(exc))
            try:
                e.default([1])
                out.append("ok")
            except TypeError as exc:
                out.append(str(exc))
            hooked = json.JSONEncoder(default=lambda o: sorted(o))
            out.append(hooked.encode({2, 1}))
            out.append(str(hooked.default({2, 1})))
            out.append(json.JSONEncoder(default=lambda o: "H").encode(1))
            try:
                json.JSONEncoder(default=lambda o: 1 // 0).encode({1, 2})
                out.append("ok")
            except ZeroDivisionError:
                out.append("Z")
            try:
                json.JSONEncoder(default=lambda o: o).encode({1, 2})
                out.append("ok")
            except ValueError:
                out.append("V")
            return "|".join(out)
            """);
        foreach (var result in results)
        {
            Assert.Equal("Object of type set is not JSON serializable|Object of type Foo is not JSON serializable|Object of type list is not JSON serializable|[1, 2]|[1, 2]|1|Z|V", result);
        }
    }

    [Fact]
    public async Task DumpDispatchAcceptsDefaultEncoderClass()
    {
        var results = await RunBothModes(
            """
            import json
            out = []
            out.append(str(json.dumps({"b": 1}, cls=json.JSONEncoder, sort_keys=True) == '{"b": 1}'))
            out.append(str(json.dumps({"a": 1}, cls=None) == '{"a": 1}'))
            try:
                json.dumps({}, cls=int)
                out.append("ok")
            except NotImplementedError:
                out.append("N")
            return "|".join(out)
            """);
        foreach (var result in results)
        {
            Assert.Equal("True|True|N", result);
        }
    }

    [Fact]
    public async Task DumpToFileHonorsEncoderClass()
    {
        var host = new MockLythonHost();
        var compiled = new LythonEngine().Compile(
            """
            import json
            with open("/out.txt", "w") as handle:
                json.dump({"b": 1, "a": 2}, handle, cls=json.JSONEncoder, sort_keys=True)
            """);
        Assert.True(compiled.IsValid);
        var sync = compiled.Run(host);
        Assert.True(sync.Success, sync.Failure?.Message);
        Assert.Equal("{\"a\": 2, \"b\": 1}", host.ReadText("/out.txt"));

        var host2 = new MockLythonHost();
        var asyncResult = await compiled.RunAsync(host2);
        Assert.True(asyncResult.Success, asyncResult.Failure?.Message);
        Assert.Equal("{\"a\": 2, \"b\": 1}", host2.ReadText("/out.txt"));
    }
}

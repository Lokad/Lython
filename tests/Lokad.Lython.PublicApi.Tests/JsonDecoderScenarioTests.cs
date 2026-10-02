using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

// N37: ordinary JSONDecoder instances with decode()/raw_decode(), the
// keyword-only constructor bundle, CPython index contracts and cls=
// dispatch through the default class. Each case runs funded in sync and
// async modes; expectations below were verified against CPython 3.13.2.
public sealed class JsonDecoderScenarioTests
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
    public async Task RawDecodeReadsPrefixAndLeavesSuffix()
    {
        var results = await RunBothModes(
            """
            import json
            source = '{"count":2}\nTrailing text'
            value, end = json.JSONDecoder().raw_decode(source)
            return "|".join([str(value == {"count": 2}), str(end), repr(source[end:])])
            """);
        foreach (var result in results)
        {
            Assert.Equal("True|11|'\\nTrailing text'", result);
        }
    }

    [Fact]
    public async Task RawDecodeEndsMatchCpythonPrefixes()
    {
        var results = await RunBothModes(
            """
            import json
            d = json.JSONDecoder()
            cases = ["0x", "1e", "truefalse", " {}", " {}", '"\U0001f600"tail', '"ab"cd', "-12.5e3;", '{"a": "}{"} tail']
            ends = []
            for i, text in enumerate(cases):
                idx = 1 if i == 4 else 0
                try:
                    v, e = d.raw_decode(text, idx)
                    ends.append(str(e))
                except json.JSONDecodeError as exc:
                    ends.append("E" + str(exc.pos))
            return "|".join(ends)
            """);
        foreach (var result in results)
        {
            Assert.Equal("1|1|4|E0|3|3|4|7|11", result);
        }
    }

    [Fact]
    public async Task DecodeHandlesWholeDocumentBoundaries()
    {
        var results = await RunBothModes(
            """
            import json
            d = json.JSONDecoder()
            out = []
            out.append(str(d.decode('  {"a": 1}  ') == {"a": 1}))
            try:
                d.decode('{"a":1} trailing')
            except json.JSONDecodeError as exc:
                out.append("E" + str(exc.pos))
            try:
                d.decode("")
            except json.JSONDecodeError as exc:
                out.append("E" + str(exc.pos))
            try:
                d.decode("   ")
            except json.JSONDecodeError as exc:
                out.append("E" + str(exc.pos))
            return "|".join(out)
            """);
        foreach (var result in results)
        {
            Assert.Equal("True|E8|E0|E3", result);
        }
    }

    [Fact]
    public async Task RawDecodeIndexContractMatchesCpython()
    {
        var results = await RunBothModes(
            """
            import json
            d = json.JSONDecoder()
            out = []
            v, e = d.raw_decode(' {}', 1)
            out.append(str(e))
            v, e = d.raw_decode(' {}', idx=1)
            out.append(str(e))
            for bad in ["x", None, 1.0]:
                try:
                    d.raw_decode("{}", bad)
                    out.append("ok")
                except TypeError:
                    out.append("T")
            try:
                d.raw_decode("{}", 10**30)
                out.append("ok")
            except OverflowError:
                out.append("O")
            try:
                d.raw_decode("{}", -1)
                out.append("ok")
            except ValueError:
                out.append("V")
            try:
                d.raw_decode('{"a":1}', 100)
                out.append("ok")
            except json.JSONDecodeError as exc:
                out.append("J" + str(exc.pos) + ":" + str(exc.lineno) + ":" + str(exc.colno))
            return "|".join(out)
            """);
        foreach (var result in results)
        {
            Assert.Equal("3|3|T|T|T|O|V|J100:1:101", result);
        }
    }
    [Fact]
    public async Task DecoderInstancesAreReusableAndIndependent()
    {
        var results = await RunBothModes(
            """
            import json
            import json as j
            d = json.JSONDecoder()
            checks = []
            checks.append(str(isinstance(d, json.JSONDecoder)))
            checks.append(str(isinstance(d, j.JSONDecoder)))
            checks.append(str(type(d) == json.JSONDecoder))
            checks.append(str(d.decode.__self__ is d))
            checks.append(str(d.raw_decode.__self__ is d))
            a, ea = d.raw_decode("[1] tail")
            b, eb = d.raw_decode("[2] tail")
            checks.append("|".join([str(a), str(ea), str(b), str(eb)]))
            e1 = json.JSONDecoder()
            e2 = json.JSONDecoder()
            checks.append("|".join([str(e1.raw_decode("[1]")[0]), str(e2.raw_decode("[2]")[0])]))
            return "|".join(checks)
            """);
        foreach (var result in results)
        {
            Assert.Equal("True|True|True|True|True|[1]|3|[2]|3|[1]|[2]", result);
        }
    }

    [Fact]
    public async Task DecoderConstructorBindsKeywordOnlyOptions()
    {
        var results = await RunBothModes(
            """
            import json
            out = []
            try:
                json.JSONDecoder(True)
                out.append("ok")
            except TypeError:
                out.append("P")
            try:
                json.JSONDecoder(foo=1)
                out.append("ok")
            except TypeError:
                out.append("K")
            try:
                json.JSONDecoder(object_hook=123)
                out.append("ok")
            except TypeError:
                out.append("H")
            d = json.JSONDecoder(strict=False)
            v, e = d.raw_decode('"a\tb"')
            out.append(str(e))
            try:
                json.JSONDecoder().raw_decode('"a\tb"')
                out.append("ok")
            except json.JSONDecodeError as exc:
                out.append("S" + str(exc.pos))
            return "|".join(out)
            """);
        foreach (var result in results)
        {
            Assert.Equal("P|K|H|5|S2", result);
        }
    }

    [Fact]
    public async Task DecoderHooksApplyToNestedValues()
    {
        var results = await RunBothModes(
            """
            import json
            checks = []
            checks.append(json.JSONDecoder(parse_constant=lambda t: "C" + t).decode('[NaN, {"x": Infinity}]')[1]["x"])
            checks.append(json.JSONDecoder(object_pairs_hook=lambda p: p).decode('{"b": 1, "a": 2}')[0][0])
            checks.append(str(json.JSONDecoder().decode('{"a": 1, "a": 2}') == {"a": 2}))
            checks.append(str(json.JSONDecoder(object_hook=lambda o: "H", object_pairs_hook=lambda p: "P").decode('{"a": 1}') == "P"))
            try:
                json.JSONDecoder(object_hook=lambda o: 1 // 0).decode('{"a": 1}')
                checks.append("ok")
            except ZeroDivisionError:
                checks.append("Z")
            checks.append(str(json.JSONDecoder(parse_int=lambda t: int(t) * 2).decode('[1, {"n": 2}]')[1]["n"] == 4))
            return "|".join(checks)
            """);
        foreach (var result in results)
        {
            Assert.Equal("CInfinity|b|True|True|Z|True", result);
        }
    }

    [Fact]
    public async Task LoadDispatchAcceptsDefaultDecoderClass()
    {
        var results = await RunBothModes(
            """
            import json
            out = []
            out.append(str(json.loads('{"a": 1}', cls=json.JSONDecoder) == {"a": 1}))
            out.append(str(json.loads('{"a": 1}', cls=None) == {"a": 1}))
            out.append(str(json.loads('"a\tb"', cls=json.JSONDecoder, strict=False) == "a\tb"))
            try:
                json.loads('{}', cls=int)
                out.append("ok")
            except NotImplementedError:
                out.append("N")
            return "|".join(out)
            """);
        foreach (var result in results)
        {
            Assert.Equal("True|True|True|N", result);
        }
    }

    [Fact]
    public async Task ErrorFieldsUseStringIndicesAfterNonzeroIdx()
    {
        var results = await RunBothModes(
            """
            import json
            d = json.JSONDecoder()
            out = []
            try:
                d.raw_decode("xx[1,", 2)
                out.append("ok")
            except json.JSONDecodeError as exc:
                out.append(str(exc.pos) + ":" + str(exc.lineno) + ":" + str(exc.colno) + ":" + str(exc.doc == "xx[1,"))
            try:
                d.raw_decode('{"a":12', 0)
                out.append("ok")
            except json.JSONDecodeError as exc:
                out.append(str(exc.pos) + ":" + str(exc.lineno) + ":" + str(exc.colno))
            v, e = d.raw_decode('"\U0001f600"tail')
            out.append(str(e) + ":" + str("\U0001f600" == v))
            return "|".join(out)
            """);
        foreach (var result in results)
        {
            Assert.Equal("5:1:6:True|7:1:8|3:True", result);
        }
    }
}

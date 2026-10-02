using System.Numerics;
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
    [Fact]
    public async Task IterencodeStreamsLazilyWithJoinParity()
    {
        var results = await RunBothModes(
            """
            import json
            calls = []
            def hook(o):
                calls.append(o)
                return [1]
            e = json.JSONEncoder(default=hook)
            it = e.iterencode([{"k": 1}, {1, 2}])
            out = []
            out.append(next(it))
            out.append(str(calls))
            out.append(next(it))
            out.append(str(calls))
            rest = list(it)
            out.append(str(calls))
            out.append(str("".join([x for x in e.iterencode([{"k": 1}, {1, 2}])]) == e.encode([{"k": 1}, {1, 2}])))
            it2 = e.iterencode([1])
            out.append(next(it2))
            try:
                while True:
                    out.append(next(it2))
            except StopIteration:
                out.append("STOP")
            out.append(str(iter(e.iterencode([])) is not None))
            return "|".join(out)
            """);
        foreach (var result in results)
        {
            Assert.Equal("[|[]|{|[]|[{1, 2}]|True|[|1|]|STOP|True", result);
        }
    }

    [Fact]
    public async Task IterencodeRecoversAfterPartialFailure()
    {
        var results = await RunBothModes(
            """
            import json
            def hook(o):
                raise RuntimeError("boom")
            it = json.JSONEncoder(default=hook).iterencode([1, {1, 2}, 3])
            collected = [next(it), next(it)]
            out = ["".join(collected)]
            try:
                list(it)
                out.append("NO-FAIL")
            except RuntimeError:
                out.append("boom")
            try:
                next(it)
                out.append("NO-STOP")
            except StopIteration:
                out.append("STOP")
            out.append(str(json.JSONEncoder(default=hook).encode([1, 2, 3]) == "[1, 2, 3]"))
            return "|".join(out)
            """);
        foreach (var result in results)
        {
            Assert.Equal("[1|boom|STOP|True", result);
        }
    }

    [Fact]
    public async Task IterencodeAbandonAndInterleave()
    {
        var results = await RunBothModes(
            """
            import json
            e = json.JSONEncoder()
            out = []
            it = e.iterencode([1, {2}, 3])
            out.append(next(it))
            del it
            hooked = json.JSONEncoder(default=lambda o: "H")
            out.append(str(hooked.encode([1, {2}, 3]) == '[1, "H", 3]'))
            a = e.iterencode({"x": [1, 2]})
            b = e.iterencode([10, 20])
            sa = []
            sb = []
            for _ in range(100):
                try:
                    sa.append(next(a))
                except StopIteration:
                    pass
                try:
                    sb.append(next(b))
                except StopIteration:
                    pass
            out.append(str("".join(sa) == e.encode({"x": [1, 2]})))
            out.append(str("".join(sb) == e.encode([10, 20])))
            return "|".join(out)
            """);
        foreach (var result in results)
        {
            Assert.Equal("[|True|True|True", result);
        }
    }
    [Fact]
    public async Task IterencodeErrorsMirrorEncode()
    {
        var results = await RunBothModes(
            """
            import json
            e = json.JSONEncoder()
            out = []
            try:
                list(e.iterencode({(1, 2): 1}))
                out.append("ok")
            except TypeError:
                out.append("T")
            try:
                list(json.JSONEncoder(allow_nan=False).iterencode([float("nan")]))
                out.append("ok")
            except ValueError:
                out.append("V")
            cycle = []
            cycle.append(cycle)
            try:
                list(e.iterencode(cycle))
                out.append("ok")
            except ValueError:
                out.append("C")
            deep = 1
            for _ in range(600):
                deep = [deep]
            try:
                e.encode(deep)
                out.append("ok")
            except RecursionError:
                out.append("R")
            try:
                "".join(e.iterencode(deep))
                out.append("ok")
            except RecursionError:
                out.append("R")
            return "|".join(out)
            """);
        foreach (var result in results)
        {
            Assert.Equal("T|V|C|R|R", result);
        }
    }

    [Fact]
    public async Task StreamedDropSucceedsWhileRetainedDenies()
    {
        var drop = new LythonEngine().Compile(
            """
            import json
            e = json.JSONEncoder()
            big = list(range(20000))
            total = 0
            for c in e.iterencode(big):
                total = total + len(c)
            return total
            """);
        Assert.True(drop.IsValid);
        var dropOptions = new LythonRunOptions { MaxExecutionMemoryBytes = 8388608 };
        var dropSync = drop.Run(new MockLythonHost(), dropOptions);
        Assert.True(dropSync.Success, dropSync.Failure?.Message);
        Assert.Equal(new BigInteger(128890), dropSync.ReturnValue);
        var dropAsync = await drop.RunAsync(new MockLythonHost(), dropOptions);
        Assert.True(dropAsync.Success, dropAsync.Failure?.Message);
        Assert.Equal(new BigInteger(128890), dropAsync.ReturnValue);

        var denyOptions = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var join = new LythonEngine().Compile(
            """
            import json
            return len("".join(json.JSONEncoder().iterencode(list(range(20000)))))
            """);
        Assert.True(join.IsValid);
        var joinSync = join.Run(new MockLythonHost(), denyOptions);
        Assert.False(joinSync.Success);
        Assert.Equal("MemoryError", joinSync.Failure?.ExceptionType);
        var joinAsync = await join.RunAsync(new MockLythonHost(), denyOptions);
        Assert.False(joinAsync.Success);
        Assert.Equal("MemoryError", joinAsync.Failure?.ExceptionType);

        var dumps = new LythonEngine().Compile(
            """
            import json
            return len(json.dumps(list(range(20000))))
            """);
        Assert.True(dumps.IsValid);
        var dumpsSync = dumps.Run(new MockLythonHost(), denyOptions);
        Assert.False(dumpsSync.Success);
        Assert.Equal("MemoryError", dumpsSync.Failure?.ExceptionType);
    }

    [Fact]
    public async Task DeniedAdvanceFinishesIteratorButRetryRecovers()
    {
        var script = new LythonEngine().Compile(
            """
            import json
            it = json.JSONEncoder().iterencode(["xy" * 1000] * 2000)
            kept = []
            denied = False
            try:
                for c in it:
                    kept.append(c)
            except MemoryError:
                denied = True
            try:
                next(it)
                stopped = False
            except StopIteration:
                stopped = True
            return str(denied) + "|" + str(stopped)
            """);
        Assert.True(script.IsValid);
        // Retaining ~4MB of chunks under 1MB denies mid-drain while the
        // ~4KB document itself fits; the failed iterator is finished.
        var tiny = new LythonRunOptions { MaxExecutionMemoryBytes = 1048576 };
        var deniedSync = script.Run(new MockLythonHost(), tiny);
        Assert.True(deniedSync.Success, deniedSync.Failure?.Message);
        Assert.Equal("True|True", deniedSync.ReturnValue);
        var deniedAsync = await script.RunAsync(new MockLythonHost(), tiny);
        Assert.True(deniedAsync.Success, deniedAsync.Failure?.Message);
        Assert.Equal("True|True", deniedAsync.ReturnValue);

        var funded = new LythonEngine().Compile(
            """
            import json
            return len("".join(json.JSONEncoder().iterencode(["xy" * 1000] * 2000)))
            """);
        Assert.True(funded.IsValid);
        var fundedSync = funded.Run(new MockLythonHost());
        Assert.True(fundedSync.Success, fundedSync.Failure?.Message);
        Assert.Equal(new BigInteger(4008000), fundedSync.ReturnValue);
    }
}

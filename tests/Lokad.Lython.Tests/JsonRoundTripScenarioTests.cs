using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class JsonRoundTripScenarioTests
{
    [Fact]
    public void UpdateObjectFixture_RunsSuccessfully()
    {
        var fixture = FixtureLoader.Load(Path.Combine("Workflows", "Json", "UpdateObject"));
        var host = new MockLythonHost();

        foreach (var file in fixture.InputFiles)
        {
            host.SeedFile(file.Key, file.Value);
        }

        var result = new LythonEngine().Run(fixture.Script, host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Empty(result.Diagnostics);

        foreach (var expected in fixture.ExpectedFiles)
        {
            FixtureAssertions.AssertTextEqual(expected.Value, host.ReadText(expected.Key));
        }
    }

    [Fact]
    public void BigInteger_RoundTripsThroughJson()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import json
text = json.dumps({"n": 123456789012345678901234567890})
value = json.loads(text)
__lython_file = open("/out.txt", "w")
__lython_file.write(text + "\n" + str(value["n"]))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal(
            "{\"n\":123456789012345678901234567890}\n123456789012345678901234567890",
            host.ReadText("/out.txt"));
    }

    [Fact]
    public void NestedJson_RoundTripsBooleansNullArraysAndObjects()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import json
text = json.dumps({"ok": True, "items": [1, None, {"name": "alpha"}], "pair": ["x", 2]})
value = json.loads(text)
__lython_file = open("/out.txt", "w")
__lython_file.write(text + "\n" + str(value["items"][2]["name"]) + "|" + str(value["pair"][1]) + "|" + str(value["ok"]))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal(
            "{\"ok\":true,\"items\":[1,null,{\"name\":\"alpha\"}],\"pair\":[\"x\",2]}\nalpha|2|True",
            host.ReadText("/out.txt"));
    }

    [Fact]
    public void InvalidJson_FailsWithJsonDecodeError()
    {
        var result = new LythonEngine().Run(
            """
import json
json.loads("{")
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("JSONDecodeError", result.Failure!.ExceptionType);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("3.14")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("None")]
    public void JsonLoads_NonStringInput_FailsWithTypeError(string expression)
    {
        var result = new LythonEngine().Run(
            "import json\njson.loads(" + expression + ")",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("json.loads(s, *, ...) expects a string argument", StringComparison.Ordinal));
    }

    [Fact]
    public void InvalidJsonEscape_FailsWithJsonDecodeError()
    {
        var result = new LythonEngine().Run(
            """
import json
json.loads("[\"abc\\y\"]")
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("JSONDecodeError", result.Failure!.ExceptionType);
    }

    [Fact]
    public void JsonLoads_AcceptsSurroundingWhitespace()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import json
value = json.loads(" \n {\"a\": 1, \"b\": [true, null]} \t ")
__lython_file = open("/out.txt", "w")
__lython_file.write(str(value["a"]) + "|" + str(value["b"][0]) + "|" + str(value["b"][1] is None))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("1|True|True", host.ReadText("/out.txt"));
    }

    [Fact]
    public void JsonRoundTrip_PreservesStringEscapes()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import json
text = json.dumps({"text": "a\"b\nc\\d"})
value = json.loads(text)
__lython_file = open("/out.txt", "w")
__lython_file.write(text + "\n" + value["text"].replace("\n", "<n>"))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal(
            "{\"text\":\"a\\\"b\\nc\\\\d\"}\na\"b<n>c\\d",
            host.ReadText("/out.txt"));
    }

    [Fact]
    public void JsonDumps_SupportsScalarRootValues()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import json
vals = []
vals.append(json.dumps(True))
vals.append(json.dumps(None))
vals.append(json.dumps("alpha"))
vals.append(json.dumps(3.5))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("true|null|\"alpha\"|3.5", host.ReadText("/out.txt"));
    }

    [Fact]
    public void JsonLoads_SupportsEmptyRoots()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import json
vals = []
vals.append(str(json.loads("{}")))
vals.append(str(json.loads("[]")))
vals.append(json.loads("\"\""))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal("{}|[]|", host.ReadText("/out.txt"));
    }

    [Theory]
    [InlineData("True")]
    [InlineData("FALSE")]
    [InlineData("Null")]
    public void JsonLoads_InvalidConstantCase_FailsWithJsonDecodeError(string token)
    {
        var result = new LythonEngine().Run(
            "import json\njson.loads(\"" + token + "\")",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("JSONDecodeError", result.Failure!.ExceptionType);
    }

    [Fact]
    public void JsonRoundTrip_PreservesUnicodeStrings()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import json
text = json.dumps({"emoji": "😀", "word": "café"})
plain = json.dumps({"emoji": "😀", "word": "café"}, ensure_ascii=False)
value = json.loads(text)
__lython_file = open("/out.txt", "w")
__lython_file.write(text + "\n" + plain + "\n" + value["emoji"] + "|" + value["word"])
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("{\"emoji\":\"\\ud83d\\ude00\",\"word\":\"caf\\u00e9\"}\n{\"emoji\":\"😀\",\"word\":\"café\"}\n😀|café", host.ReadText("/out.txt"));
    }

    [Fact]
    public void JsonDumps_SupportedNonStringDictionaryKeys_AreConverted()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import json
__lython_file = open("/out.txt", "w")
__lython_file.write(json.dumps({1: "one", None: "nil"}))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("{\"1\":\"one\",\"null\":\"nil\"}", host.ReadText("/out.txt"));
    }

    [Fact]
    public void JsonDumps_UnsupportedDictionaryKeys_FailWithTypeErrorUnlessSkipped()
    {
        var result = new LythonEngine().Run(
            """
import json
json.dumps({(1, 2): "pair"})
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("TypeError", result.Failure!.ExceptionType);
        Assert.Contains("dictionary keys", result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonExpandedSurface_LoadDumpHooksFormattingAndDecodeErrors()
    {
        var host = new MockLythonHost();
        host.SeedFile("/input.json", "{\"n\": 2, \"items\": [1, 2]}");

        var result = new LythonEngine().Run(
            """
import json
import math
import re
from decimal import Decimal

def parse_int(text):
    return int(text) + 10

def object_hook(obj):
    obj["hooked"] = True
    return obj

def pairs_hook(pairs):
    return {"count": len(pairs), "first": pairs[0][0]}

def fallback(obj):
    return "fallback:" + obj.group(0)

with open("/input.json", "r") as handle:
    loaded = json.load(handle, parse_int=parse_int, object_hook=object_hook)

with open("/pretty.json", "w") as handle:
    dumped_none = json.dump({"b": 2, "a": ["é", 1]}, handle, indent=2, sort_keys=True, ensure_ascii=False)

vals = []
vals.append(str(loaded["n"]))
vals.append(str(loaded["items"][0]))
vals.append(str(loaded["hooked"]))
vals.append(str(dumped_none is None))
vals.append(open("/pretty.json").read())
vals.append(json.dumps({"x": re.search("a+", "caa")}, default=fallback))
vals.append(str(json.loads("1.5", parse_float=lambda text: "F" + text)))
vals.append(str(json.loads("NaN", parse_constant=lambda text: "C" + text)))
vals.append(str(json.loads("{\"z\": 1}", object_pairs_hook=pairs_hook)))
vals.append(json.dumps({"d": Decimal("1.20")}))
vals.append(json.dumps({(1, 2): "skip", "ok": 1}, skipkeys=True))
cycle = []
cycle.append(cycle)
try:
    json.dumps(cycle)
except ValueError as err:
    vals.append(err.type)
try:
    json.dumps([math.nan], allow_nan=False)
except ValueError as err:
    vals.append("nan:" + err.type)
__lython_file = open("/out.txt", "w")
__lython_file.write("\n---\n".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal(
            "12\n---\n11\n---\nTrue\n---\nTrue\n---\n{\n  \"a\": [\n    \"é\",\n    1\n  ],\n  \"b\": 2\n}\n---\n{\"x\":\"fallback:aa\"}\n---\nF1.5\n---\nCNaN\n---\n{'count': 1, 'first': z}\n---\n{\"d\":1.20}\n---\n{\"ok\":1}\n---\nValueError\n---\nnan:ValueError",
            host.ReadText("/out.txt"));
    }

    [Fact]
    public void JsonDecodeError_IsCatchableAndExposesLocationFields()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import json

try:
    json.loads("{")
except json.JSONDecodeError as exc:
    vals = [exc.type, str(exc.msg != ""), exc.doc, str(exc.pos), str(exc.lineno), str(exc.colno), str(len(exc.args))]
    __lython_file = open("/out.txt", "w")
    __lython_file.write("|".join(vals))
    __lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("JSONDecodeError|True|{|1|1|2|3", host.ReadText("/out.txt"));
    }

    [Fact]
    public void JsonExpandedSurface_InvalidOptionsFailAtCompileTime()
    {
        var result = new LythonEngine().Run(
            """
import json

json.load(open("/in.json", "w"))
json.dump({}, open("/out.json", "r"))
json.loads("{}", parse_int=1)
json.dumps({}, indent=[])
json.dumps({}, separators=(",", 1))
json.dumps({}, cls="bad")
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.Null(result.Failure);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("file is not open for reading", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("file is not open for writing", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("parse_int=... expects a callable or None", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("indent=...", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("separators=...", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("custom encoder/decoder classes are not supported", StringComparison.Ordinal));
    }
}

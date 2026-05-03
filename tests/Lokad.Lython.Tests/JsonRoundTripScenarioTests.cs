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
write_text("/out.txt", text + "\n" + str(value["n"]))
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
write_text("/out.txt", text + "\n" + str(value["items"][2]["name"]) + "|" + str(value["pair"][1]) + "|" + str(value["ok"]))
""",
            host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Equal(
            "{\"ok\":true,\"items\":[1,null,{\"name\":\"alpha\"}],\"pair\":[\"x\",2]}\nalpha|2|True",
            host.ReadText("/out.txt"));
    }

    [Fact]
    public void InvalidJson_FailsWithValueError()
    {
        var result = new LythonEngine().Run(
            """
import json
json.loads("{")
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("ValueError", result.Failure!.ExceptionType);
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
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("json.loads(s) expects a string argument", StringComparison.Ordinal));
    }

    [Fact]
    public void InvalidJsonEscape_FailsWithValueError()
    {
        var result = new LythonEngine().Run(
            """
import json
json.loads("[\"abc\\y\"]")
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("ValueError", result.Failure!.ExceptionType);
    }

    [Fact]
    public void JsonLoads_AcceptsSurroundingWhitespace()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import json
value = json.loads(" \n {\"a\": 1, \"b\": [true, null]} \t ")
write_text("/out.txt", str(value["a"]) + "|" + str(value["b"][0]) + "|" + str(value["b"][1] is None))
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
write_text("/out.txt", text + "\n" + value["text"].replace("\n", "<n>"))
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
write_text("/out.txt", "|".join(vals))
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
write_text("/out.txt", "|".join(vals))
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
    public void JsonLoads_InvalidConstantCase_FailsWithValueError(string token)
    {
        var result = new LythonEngine().Run(
            "import json\njson.loads(\"" + token + "\")",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("ValueError", result.Failure!.ExceptionType);
    }

    [Fact]
    public void JsonRoundTrip_PreservesUnicodeStrings()
    {
        var host = new MockLythonHost();

        var result = new LythonEngine().Run(
            """
import json
text = json.dumps({"emoji": "😀", "word": "café"})
value = json.loads(text)
write_text("/out.txt", text + "\n" + value["emoji"] + "|" + value["word"])
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Null(result.Failure);
        Assert.Equal("{\"emoji\":\"😀\",\"word\":\"café\"}\n😀|café", host.ReadText("/out.txt"));
    }

    [Fact]
    public void JsonDumps_NonStringDictionaryKeys_FailWithTypeError()
    {
        var result = new LythonEngine().Run(
            """
import json
json.dumps({1: "one"})
""",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("TypeError", result.Failure!.ExceptionType);
        Assert.Contains("dictionary keys to be strings", result.Failure.Message, StringComparison.Ordinal);
    }
}

using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class RuntimeSkeletonTests
{
    [Fact]
    public void Compile_EmptySource_YieldsValidCompiledScript()
    {
        var engine = new LythonEngine();

        var compiled = engine.Compile(string.Empty);

        Assert.True(compiled.IsValid);
        Assert.Empty(compiled.Diagnostics);
        Assert.Equal(string.Empty, compiled.Source);
    }

    [Fact]
    public void Compile_LocalStyleImport_RemainsValid()
    {
        var engine = new LythonEngine();

        var compiled = engine.Compile("import os");

        Assert.True(compiled.IsValid);
        Assert.Empty(compiled.Diagnostics);
    }

    [Fact]
    public void Run_ReadTextRegexSubWriteText_UpdatesMockVfs()
    {
        var engine = new LythonEngine();
        var host = new MockLythonHost();
        host.SeedFile("/input.txt", "alpha beta alpha");

        const string script = """
import re
text = read_text("/input.txt")
text = re.sub("alpha", "omega", text)
write_text("/output.txt", text)
""";

        var result = engine.Run(script, host);

        Assert.True(result.Success);
        Assert.Null(result.Failure);
        Assert.Empty(result.Diagnostics);
        Assert.Equal("omega beta omega", host.ReadText("/output.txt"));
    }

    [Fact]
    public void Run_InvalidRegex_ReturnsRuntimeFailure()
    {
        var engine = new LythonEngine();
        var host = new MockLythonHost();
        host.SeedFile("/input.txt", "alpha");

        const string script = """
import re
text = read_text("/input.txt")
write_text("/output.txt", re.sub("(", "x", text))
""";

        var result = engine.Run(script, host);

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("ValueError", result.Failure!.ExceptionType);
        Assert.Empty(result.Diagnostics);
    }
}

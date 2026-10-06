using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class MatchSubjectExpressionListTests
{
    [Fact]
    public async Task TupleAndSingletonSubjectsUsePythonTupleSemantics()
    {
        await AssertOutput("""
            match 1, 2:
                case (a, b):
                    print(a, b)
            match 3,:
                case value:
                    print(value, type(value).__name__)
            match 4:
                case value:
                    print(value, type(value).__name__)
            """, "1 2\n(3,) tuple\n4 int\n");
    }

    [Fact]
    public async Task StarredSubjectsSupportEmptyAndMultipleUnpackings()
    {
        await AssertOutput("""
            match *range(2), 2, *[3, 4],:
                case (first, *rest):
                    print(first, rest)
            match *[],:
                case ():
                    print("empty")
            match *(range(2) if True else []),:
                case values:
                    print(values)
            """, "0 [1, 2, 3, 4]\nempty\n(0, 1)\n");
    }

    [Fact]
    public async Task SubjectsEvaluateAndDrainUnpackingsOnceBeforeCases()
    {
        await AssertOutput("""
            events = []
            def mark(value):
                events.append(value)
                return value
            def values():
                events.append("start")
                yield mark(1)
                yield mark(2)
                events.append("end")
            match mark(0), *values(), mark(3):
                case (9, *rest):
                    print("wrong")
                case value:
                    print(value, events)
            """, "(0, 1, 2, 3) [0, 'start', 1, 2, 'end', 3]\n");
    }

    [Fact]
    public async Task SubjectWalrusAssignmentsBindInTheEnclosingScope()
    {
        await AssertOutput("""
            x = 0
            match x := 1, y := 2:
                case (a, b):
                    print(x, y, a, b)
            def run():
                match x := 3, *[4],:
                    case values:
                        print(x, values)
                return x
            print(run(), x)
            """, "1 2 1 2\n3 (3, 4)\n3 1\n");
    }

    [Fact]
    public async Task FailedSubjectUnpackingRunsNoCaseBody()
    {
        await AssertOutput("""
            events = []
            def mark(value):
                events.append(value)
                return value
            try:
                match mark(1), *mark(2),:
                    case value:
                        events.append("case")
            except TypeError:
                print(events)
            """, "[1, 2]\n");
    }

    [Theory]
    [InlineData("*range(2)")]
    [InlineData("*range(2) or [],")]
    [InlineData("*range(2) if True else [],")]
    [InlineData("1,,")]
    public async Task InvalidSubjectListsFailBeforeEffects(string subject)
    {
        var compiled = new LythonEngine().Compile("print('effects')\nmatch " + subject + ":\n    case _: pass\n");
        Assert.False(compiled.IsValid);
        var result = await compiled.RunAsync(new MockLythonHost());
        Assert.False(result.Success);
        Assert.Empty(result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SubjectsAwaitDelayedHostsInModuleAndClassBodies(bool classBody)
    {
        var body = """
            match int(Path("/number.txt").read_text()), *values(),:
                case (2, 2, 2):
                    print("matched")
            """;
        var source = """
            from pathlib import Path
            def values():
                yield int(Path("/number.txt").read_text())
                yield int(Path("/number.txt").read_text())
            """ + "\n" + (classBody ? "class C:\n" + string.Join("\n", body.Split('\n').Select(line => "    " + line)) : body);
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
        Assert.Equal("matched\n", result.StandardOutput);
        Assert.Equal(sync.StandardOutput, result.StandardOutput);
        Assert.True(delayed.CompletedAsynchronously >= 3);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SubjectUnpackingRespectsCollectionAndMemoryBudgets(bool classBody)
    {
        var body = "match *range(100000),:\n    case _: print('case')\n";
        var source = classBody ? "class C:\n" + string.Join("\n", body.Split('\n').Select(line => "    " + line)) : body;
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

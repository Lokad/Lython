using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class HostRequirementBindingTests
{
    [Fact]
    public async Task CachedFactsFollowAllHostCapabilityStatesAndPreserveDiagnosticOrder()
    {
        const string source = "print('effect')\nname=input()\nimport subprocess\nprint(name,subprocess.check_output(['tool'],text=True))";
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(";", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            foreach (var hasInput in new[] { false, true })
            foreach (var hasRunner in new[] { false, true })
            {
                var host = new MockLythonHost();
                if (hasInput) host.SeedStandardInput("seed\n");
                if (hasRunner) { host.EnableSubprocess(); host.SeedSubprocessResult(["tool"], 0, "answer"); }
                using (var result = asynchronous ? await script.RunAsync(host) : script.Run(host))
                {
                    Assert.Equal(hasInput && hasRunner, result.Success);
                    if (result.Success) Assert.Equal("effect\nseed answer\n", result.StandardOutput);
                    else
                    {
                        Assert.Null(result.Failure);
                        Assert.Empty(result.StandardOutput);
                        var expected = new List<string>();
                        if (!hasInput) expected.Add("LA3040");
                        if (!hasRunner) { expected.Add("LA3041"); expected.Add("LA3041"); }
                        Assert.Equal(expected, result.Diagnostics.Select(diagnostic => diagnostic.Code));
                        Assert.Equal(result.Diagnostics.Select(diagnostic => diagnostic.Span!.Line).Order(),
                            result.Diagnostics.Select(diagnostic => diagnostic.Span!.Line));
                    }
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    public static TheoryData<string, string> Cases => new()
    {
        { "def input(): return 'ok'\nprint(input())", "ok\n" },
        { "input=lambda: 'lambda'\nprint(input())", "lambda\n" },
        { "def answer(): return 'alias'\ninput=answer\nprint(input())", "alias\n" },
        { "input=lambda: 'bound'\nsaved=input\ndel input\ninput=saved\nprint(input())", "bound\n" },
        { "def input(): return 'value'\nprint([input() for i in range(2)])", "['value', 'value']\n" },
        { "class Callable:\n def __call__(self):return 'instance'\ninput=Callable()\nprint(input())", "instance\n" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ModuleInputBindingsDoNotRequireHostStdin(string source, string expected)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(";", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("print('before')\ninput()\ninput=lambda:'later'")]
    [InlineData("input=lambda:'bound'\ndel input\nprint('before')\ninput()")]
    [InlineData("if False:input=lambda:'unused'\nprint('before')\ninput()")]
    public async Task UncertainInputCallsStillAuthorizeTheRealBuiltinAtRuntime(string source)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(";", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.False(result.Success);
                    Assert.NotNull(result.Failure);
                    Assert.Equal("RuntimeError", result.Failure.ExceptionType);
                    Assert.Equal("standard input is not available.", result.Failure.Message);
                    Assert.Equal("before\n", result.StandardOutput);
                    Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "LA3040");
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task UnshadowedInputRemainsRejectedBeforeEffects()
    {
        using var script = new LythonEngine().Compile("print('unreachable')\ninput()");
        Assert.True(script.IsValid);
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.False(result.Success);
            Assert.Null(result.Failure);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "LA3040");
            Assert.Empty(result.StandardOutput);
        }
    }
}

using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class HostRequirementBindingTests
{
    [Fact]
    public async Task ReachedOperationsFollowLiveHostCapabilities()
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
                        Assert.NotNull(result.Failure);
                        Assert.Equal("effect\n", result.StandardOutput);
                        Assert.Empty(result.Diagnostics);
                        Assert.Equal(LythonFailureReason.HostCapabilityUnavailable, result.FailureAttribution?.Reason);
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
        { "print(False and input())", "False\n" },
        { "print(True or input())", "True\n" },
        { "assert True,input()\nprint('ok')", "ok\n" },
        { "class Reader:\n def read(self):return 'local'\nclass Namespace:pass\nsys=Namespace();sys.stdin=Reader()\nprint(sys.stdin.read())", "local\n" },
        { "class Reader:\n def readline(self):return 'local'\nclass Namespace:pass\nsys=Namespace();sys.stdin=Reader()\nprint(sys.stdin.readline())", "local\n" },
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
                    Assert.Empty(result.Diagnostics);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

}

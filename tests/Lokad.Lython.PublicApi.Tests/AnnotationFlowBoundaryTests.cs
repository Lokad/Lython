using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class AnnotationFlowBoundaryTests
{
    public static TheoryData<string, string> RequiredDiagnostics => new()
    {
        { "value = 'old'\nif True:\n    value = 1\nelse:\n    value = 2\nresult: str = value", "LA1080" },
        { "try:\n    value = 1\nfinally:\n    value = 2\nresult: str = value", "LA1080" },
        { "with open('input.txt') as source:\n    value = 1\nresult: str = value", "LA1080" },
        { "match 1:\n    case 1:\n        value = 1\n        result: str = value\n    case _: pass", "LA1080" },
        { "if False:\n    result: int = 'bad'", "LA1080" },
        { "class Outer:\n    class Inner:\n        result: int = 'bad'", "LA1080" },
        { "def outer():\n    def inner(value: int = 'bad'): pass\n    return inner", "LA1081" },
        { "def outer():\n    def inner() -> int:\n        value = 'bad'\n        return value\n    return inner", "LA1082" },
        { "def value() -> str:\n    if True: result = 1\n    else: result = 2\n    return result", "LA1082" },
        { "def value() -> str:\n    try: result = 1\n    finally: result = 2\n    return result", "LA1082" },
        { "value = 0\nif True:\n    marker: int = 1\n    value = 'bad'\nelse:\n    value = 'bad'\nresult: int = value", "LA1080" },
    };

    [Theory]
    [MemberData(nameof(RequiredDiagnostics))]
    public void LaterChecksRetainPrecedingControlFlowAndNestedScopes(string source, string code)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.False(script.IsValid);
            Assert.Contains(script.Diagnostics, diagnostic => diagnostic.Code == code);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    public static TheoryData<string, string> ValidFlow => new()
    {
        { "value = 'old'\nfor item in [1]:\n    value = item\nresult: int = value\nprint(result)", "1\n" },
        { "value = 'old'\nwhile isinstance(value, str):\n    value = 1\nresult: int = value\nprint(result)", "1\n" },
        { "value = 'old'\ntry:\n    value = 1\nexcept Exception:\n    value = 2\nresult: int = value\nprint(result)", "1\n" },
        { "value = 'old'\nmatch 1:\n    case 1: value = 1\n    case _: value = 2\nresult: int = value\nprint(result)", "1\n" },
        { "def value() -> str:\n    class Inner:\n        def number() -> int: return 1\n    return 'ok'\nprint(value())", "ok\n" },
        { "def value() -> int:\n    return 1\nresult: str\nprint(value())", "1\n" },
        { "def helper(value): return value + 1\nprint([helper(i) for i in range(3)])", "[1, 2, 3]\n" },
    };

    [Theory]
    [MemberData(nameof(ValidFlow))]
    public async Task ScopeAndLoopFactsDoNotCreateFalseAnnotationErrors(string source, string expected)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(diagnostic => diagnostic.Message)));
            var baseline = budget.ReservedBytes;
            using (var result = script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            using (var result = await script.RunAsync(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}

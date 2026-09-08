using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ShortCircuitRefinementTests
{
    private const string CompatibilitySource = """
def optional_list(flag):
    value = None
    if flag:
        value = [3]
    return value

def guarded_or(flag):
    latest = None
    if flag:
        latest = (1, "present")
    return latest is None or 2 > latest[0]

def guarded_and(flag):
    value = optional_list(flag)
    return value is not None and value[0] == 3

def guarded_truthiness(flag):
    value = optional_list(flag)
    return value and value[0]

def guarded_conditional(flag):
    value = optional_list(flag)
    return value[0] if value is not None else 0

def guarded_early_return(flag):
    value = optional_list(flag)
    if value is None:
        return 0
    return value[0]

def guarded_assert():
    value = optional_list(True)
    assert not (value is None)
    return value[0]

def guarded_while():
    value = optional_list(True)
    total = 0
    while value is not None:
        total += value[0]
        value = None
    return total

return "|".join([
    str(guarded_or(False)), str(guarded_or(True)),
    str(guarded_and(False)), str(guarded_and(True)),
    str(guarded_truthiness(False)), str(guarded_truthiness(True)),
    str(guarded_conditional(False)), str(guarded_conditional(True)),
    str(guarded_early_return(False)), str(guarded_early_return(True)),
    str(guarded_assert()), str(guarded_while())
])
""";

    [Fact]
    public void OptionalValuesRefineAcrossShortCircuitAndBranchConditions()
    {
        var compiled = new LythonEngine().Compile(CompatibilitySource);
        Assert.True(compiled.IsValid, string.Join(" | ", compiled.Diagnostics.Select(d => $"{d.Code}: {d.Message}")));

        var result = compiled.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("True|True|False|True|None|3|0|3|0|3|3|3", result.ReturnValue);
    }

    [Fact]
    public async Task RunAsync_UsesTheSameGuardedProgram()
    {
        var result = await new LythonEngine().RunAsync(CompatibilitySource, new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message ?? string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal("True|True|False|True|None|3|0|3|0|3|3|3", result.ReturnValue);
    }

    [Fact]
    public void NestedLogicalConditionsPropagateOnlySoundCommonFacts()
    {
        var compiled = new LythonEngine().Compile(
            """
def inspect(flag):
    value = None
    if flag:
        value = [1]

    if value is not None and (not (value is None) and value[0] == 1):
        return value[0]

    if not (value is None or value[0] != 1):
        return value[0]

    return 0

safe = [value[0] for value in [None, [1]] if value is not None]
never = None
while never is not None:
    safe.append(never[0])
""");

        Assert.True(compiled.IsValid, string.Join(" | ", compiled.Diagnostics.Select(d => $"{d.Code}: {d.Message}")));
    }

    [Fact]
    public void UnsafeShortCircuitOrderingsRemainDiagnosed()
    {
        var compiled = new LythonEngine().Compile(
            """
none_value = None
bad_or = none_value is not None or none_value[0] == 1
bad_and = none_value is None and none_value[0] == 1
unconditional = none_value[0]

def maybe_bad(flag):
    value = None
    if flag:
        value = [1]
    return value is not None or value[0] == 1
""");

        Assert.False(compiled.IsValid);
        Assert.Equal(4, compiled.Diagnostics.Count(d => d.Code == "LA3115"));
    }
}


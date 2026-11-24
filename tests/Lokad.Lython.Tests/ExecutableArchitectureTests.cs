using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ExecutableArchitectureTests
{
    [Fact]
    public async Task UnconditionalRaiseKeepsLexicalFactsAndGeneratorCleanup()
    {
        const string source = """
            def run(valid):
                try:
                    assert valid, 'invalid'
                    raise ValueError('expected')
                    dead_local = valid + 100
                    yield dead_local
                except AssertionError:
                    print('assertion')
                finally:
                    print('cleanup')

            for valid in (False, True):
                pending = run(valid)
                print('created')
                try:
                    next(pending)
                except (StopIteration, ValueError) as error:
                    print(type(error).__name__)
            """;
        var frontend = LythonFrontend.Compile(source);
        var module = ExecutableScript.Compile(frontend.Script.RequireNotNull()).EntryPoint;
        var function = Assert.Single(module.FunctionBindings());
        var code = function.CodeObject;
        Assert.True(function.IsGenerator);
        Assert.Contains("dead_local", code.LocalNames);
        Assert.DoesNotContain(code.Instructions, instruction => instruction.OpCode == ExecutableOpCode.Yield);
        var assertion = Assert.Single(code.Instructions, instruction => instruction.OpCode == ExecutableOpCode.FailAssertion);
        Assert.Single(code.Instructions, instruction => instruction.OpCode == ExecutableOpCode.Jump && instruction.Span == assertion.Span);

        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            const string expected = "created\nassertion\ncleanup\nStopIteration\ncreated\ncleanup\nValueError\n";
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

    [Fact]
    public void ClosedScalarReturnReadsCapturedOperandFromItsLiveCell()
    {
        const string source = """
            def factory(step):
                def apply(base):
                    return (base - step) * 2
                return apply
            """;
        var frontend = LythonFrontend.Compile(source);
        var module = ExecutableScript.Compile(frontend.Script.RequireNotNull()).EntryPoint;
        var factory = Assert.Single(module.FunctionBindings()).CodeObject;
        var helper = Assert.Single(factory.FunctionBindings()).CodeObject;

        Assert.True(helper.HasDirectOperandReturn);
        Assert.Equal([ExecutableOpCode.Binary, ExecutableOpCode.Binary, ExecutableOpCode.Return],
            helper.Instructions.Select(instruction => instruction.OpCode));
        var captured = ~helper.Instructions[0].SecondInput - helper.CaptureOperandBase;
        Assert.Equal("step", helper.ClosureNames[captured]);
        var span = helper.OperandReadSpan(0, 1);
        Assert.Equal("step", source.ReplaceLineEndings("\n").Substring(span.Start, span.Length));
    }

    [Fact]
    public void ExecutableScript_CompilesSimpleAssignmentsIntoLocalsAndConstants()
    {
        var frontend = LythonFrontend.Compile("""
x = 1
y = x + 2
y
""",regexProvider:Utf8RegexProvider.Instance);

        var syntax = frontend.Script.RequireNotNull();
        var executable = ExecutableScript.Compile(syntax);
        var entry = executable.EntryPoint;

        Assert.Equal(["x", "y"], entry.LocalNames);
        Assert.Empty(entry.Names);
        Assert.Equal(2, entry.Constants.Length);
        Assert.Contains(new System.Numerics.BigInteger(1), entry.Constants);
        Assert.Contains(new System.Numerics.BigInteger(2), entry.Constants);

        var instructions = entry.Instructions;
        Assert.Collection(
            instructions,
            instruction =>
            {
                Assert.Equal(ExecutableOpCode.StoreLocal, instruction.OpCode);
                Assert.Equal(new System.Numerics.BigInteger(1), entry.Constants[~instruction.InputStart]);
            },
            instruction =>
            {
                Assert.Equal(ExecutableOpCode.Binary, instruction.OpCode);
                Assert.Equal(BinaryOperatorSyntax.Add, instruction.BinaryOperator);
                Assert.Equal(entry.LocalNameToSlot["x"], instruction.InputStart);
                Assert.Equal(new System.Numerics.BigInteger(2), entry.Constants[~instruction.SecondInput]);
            },
            instruction => Assert.Equal(ExecutableOpCode.StoreLocal, instruction.OpCode),
            instruction => Assert.Equal(ExecutableOpCode.LoadLocal, instruction.OpCode),
            instruction => Assert.Equal(ExecutableOpCode.PopTop, instruction.OpCode),
            instruction => Assert.Equal(ExecutableOpCode.Complete, instruction.OpCode));
    }

    [Fact]
    public void ExecutableScript_TypesAugmentedAndSliceInstructionPayloads()
    {
        var frontend = LythonFrontend.Compile("""
total = 1
total += 2
values = [0, 1, 2, 3, 4]
values[1:4:2]
""",regexProvider:Utf8RegexProvider.Instance);

        var syntax = frontend.Script.RequireNotNull();
        var executable = ExecutableScript.Compile(syntax);
        var instructions = executable.EntryPoint.Instructions.ToArray();

        var augmented = Assert.Single(instructions, instruction => instruction.OpCode == ExecutableOpCode.Augmented);
        Assert.Equal(AugmentedAssignmentOperatorSyntax.Add, augmented.AugmentedOperator);
        Assert.True(augmented.HasResultConsumer);
        var store = instructions[Array.IndexOf(instructions, augmented) + 1];
        Assert.Equal(executable.EntryPoint.LocalNameToSlot["total"], store.LocalSlot);
        Assert.True(store.LocalSlot < executable.EntryPoint.FirstTemporarySlot);
        Assert.True(augmented.ResultSlot >= executable.EntryPoint.FirstTemporarySlot);
        Assert.Equal(augmented.ResultSlot + 1, augmented.LiveExtent);

        var slice = Assert.Single(instructions, instruction => instruction.OpCode == ExecutableOpCode.Slice);
        Assert.Equal(
            ExecutableSliceParts.Start | ExecutableSliceParts.End | ExecutableSliceParts.Step,
            slice.SliceParts);
    }

    [Fact]
    public void ExecutableScript_InternsLargeConstantTablesWithoutChangingOrder()
    {
        var source = string.Join(
            Environment.NewLine,
            Enumerable.Range(0, 4_000)
                .Select(index => $"value_{index} = \"constant-{index}\"")
                .Append("duplicate = \"constant-0\""));

        var frontend = LythonFrontend.Compile(source,regexProvider:Utf8RegexProvider.Instance);
        var syntax = frontend.Script.RequireNotNull();
        var executable = ExecutableScript.Compile(syntax);

        Assert.Equal(4_000, executable.EntryPoint.Constants.Length);
        Assert.Equal(PyString.FromString("constant-0"), executable.EntryPoint.Constants[0]);
        Assert.Equal(PyString.FromString("constant-3999"), executable.EntryPoint.Constants[^1]);
    }

    [Fact]
    public void ExecutableScript_CompilesConditionalAndLoopControlFlow()
    {
        var frontend = LythonFrontend.Compile("""
count = 0
if ready:
    count = count + 1
else:
    count = count + 2

while count:
    count = count - 1
else:
    count = 99
""",regexProvider:Utf8RegexProvider.Instance);

        var syntax = frontend.Script.RequireNotNull();
        var executable = ExecutableScript.Compile(syntax);
        var entry = executable.EntryPoint;

        Assert.Equal(["count"], entry.LocalNames);
        Assert.Contains(entry.Instructions, instruction => instruction.OpCode == ExecutableOpCode.JumpIfFalse);
        Assert.Contains(entry.Instructions, instruction => instruction.OpCode == ExecutableOpCode.Jump);
        Assert.Contains(entry.Instructions, instruction =>
            instruction.OpCode == ExecutableOpCode.Binary && instruction.BinaryOperator == BinaryOperatorSyntax.Subtract);
    }

    [Fact]
    public void ExecutableScript_NormalizesJumpChainsAndPrunesDeadBlocks()
    {
        var frontend = LythonFrontend.Compile("""
if True:
    value = 1
else:
    value = 2

return value
""",regexProvider:Utf8RegexProvider.Instance);

        var syntax = frontend.Script.RequireNotNull();
        var executable = ExecutableScript.Compile(syntax);

        Assert.DoesNotContain(
            executable.EntryPoint.Instructions,
            instruction => instruction.OpCode == ExecutableOpCode.Jump &&
                           executable.EntryPoint.Instructions[instruction.TargetIndex].OpCode == ExecutableOpCode.Jump);
    }

    [Fact]
    public void ExecutableSubsetScript_ExecutesThroughExecutablePipeline()
    {
        var frontend = LythonFrontend.Compile("""
count = 0
if ready:
    count = count + 1
else:
    count = count + 2

while count > 1:
    count = count - 1

return count
""",regexProvider:Utf8RegexProvider.Instance);

        var syntax = frontend.Script.RequireNotNull();
        var executable = ExecutableScript.Compile(syntax);
        var result = new LythonRuntime().Run(
            executable,
            new MockLythonHost(),
            new LythonRunOptions
            {
                Globals = new Dictionary<string, object?> { ["ready"] = true }
            });

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(1), result.ReturnValue);
    }

    [Fact]
    public void ExecutableSubset_ForLoopBreakContinueAndElse_ExecuteThroughExecutablePipeline()
    {
        var frontend = LythonFrontend.Compile("""
total = 0
for item in [1, 2, 3, 4]:
    if item == 2:
        continue
    if item == 4:
        break
    total = total + item
else:
    total = 99

return total
""",regexProvider:Utf8RegexProvider.Instance);

        var syntax = frontend.Script.RequireNotNull();
        var executable = ExecutableScript.Compile(syntax);
        var result = new LythonRuntime().Run(executable, new MockLythonHost(), null);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(4), result.ReturnValue);
    }

    [Fact]
    public void ExecutableSubset_BuiltinCall_ExecutesThroughExecutablePipeline()
    {
        var frontend = LythonFrontend.Compile("""
return len([1, 2, 3])
""",regexProvider:Utf8RegexProvider.Instance);

        var syntax = frontend.Script.RequireNotNull();
        var executable = ExecutableScript.Compile(syntax);
        var result = new LythonRuntime().Run(executable, new MockLythonHost(), null);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(3), result.ReturnValue);
    }

    [Fact]
    public void ExecutableSubset_ImportMemberCall_ExecutesThroughExecutablePipeline()
    {
        var frontend = LythonFrontend.Compile("""
import math
return math.sqrt(9)
""",regexProvider:Utf8RegexProvider.Instance);

        var syntax = frontend.Script.RequireNotNull();
        var executable = ExecutableScript.Compile(syntax);
        Assert.True(executable.EntryPoint.LookupCacheCount > 0);
        Assert.Single(executable.EntryPoint.CallSites);
        var result = new LythonRuntime().Run(executable, new MockLythonHost(), null);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(3.0, result.ReturnValue);
    }

    [Fact]
    public void ExecutableSubset_DictTupleAndSubscript_ExecuteThroughExecutablePipeline()
    {
        var frontend = LythonFrontend.Compile("""
payload = {"items": (1, 2, 3)}
return payload["items"][1]
""",regexProvider:Utf8RegexProvider.Instance);

        var syntax = frontend.Script.RequireNotNull();
        var executable = ExecutableScript.Compile(syntax);
        var result = new LythonRuntime().Run(executable, new MockLythonHost(), null);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(2), result.ReturnValue);
    }

    [Fact]
    public void ExecutableSubset_SetLiteralAndSlice_ExecuteThroughExecutablePipeline()
    {
        var frontend = LythonFrontend.Compile("""
ok = 2 in {1, 2, 3}
part = [1, 2, 3, 4][1:3]
value = part[0] + part[1]
if ok:
    return value
return 0
""",regexProvider:Utf8RegexProvider.Instance);

        var syntax = frontend.Script.RequireNotNull();
        var executable = ExecutableScript.Compile(syntax);
        var result = new LythonRuntime().Run(executable, new MockLythonHost(), null);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(5), result.ReturnValue);
    }

    [Fact]
    public void ExecutableSubset_AugmentedAndChainedAssignments_ExecuteThroughExecutablePipeline()
    {
        var frontend = LythonFrontend.Compile("""
a = b = 1
a += 2
return a + b
""",regexProvider:Utf8RegexProvider.Instance);

        var syntax = frontend.Script.RequireNotNull();
        var executable = ExecutableScript.Compile(syntax);
        var result = new LythonRuntime().Run(executable, new MockLythonHost(), null);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(4), result.ReturnValue);
    }

    [Fact]
    public void ExecutableSubset_UnpackingAssignments_ExecuteThroughExecutablePipeline()
    {
        var frontend = LythonFrontend.Compile("""
first, *middle, last = [1, 2, 3, 4]
return first + len(middle) + last
""",regexProvider:Utf8RegexProvider.Instance);

        var syntax = frontend.Script.RequireNotNull();
        var executable = ExecutableScript.Compile(syntax);
        var result = new LythonRuntime().Run(executable, new MockLythonHost(), null);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(7), result.ReturnValue);
    }

    [Fact]
    public void ExecutableSubset_FunctionDefinitions_ExecuteThroughExecutablePipeline()
    {
        var frontend = LythonFrontend.Compile("""
def helper(value, step=1):
    return value + step

return helper(2)
""",regexProvider:Utf8RegexProvider.Instance);

        var syntax = frontend.Script.RequireNotNull();
        var executable = ExecutableScript.Compile(syntax);
        Assert.Single(executable.EntryPoint.FunctionBindings());
        Assert.NotNull(executable.EntryPoint.FunctionBindings()[0].CodeObject);
        var result = new LythonRuntime().Run(executable, new MockLythonHost(), null);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(3), result.ReturnValue);
    }

    [Fact]
    public void ExecutableSubset_NestedExecutableFunctions_ExecuteThroughExecutablePipeline()
    {
        var frontend = LythonFrontend.Compile("""
def outer(value):
    def inner(step=2):
        return value + step
    return inner()

return outer(3)
""",regexProvider:Utf8RegexProvider.Instance);

        var syntax = frontend.Script.RequireNotNull();
        var executable = ExecutableScript.Compile(syntax);
        Assert.Single(executable.EntryPoint.FunctionBindings());
        Assert.NotNull(executable.EntryPoint.FunctionBindings()[0].CodeObject);
        var result = new LythonRuntime().Run(executable, new MockLythonHost(), null);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(5), result.ReturnValue);
    }

    [Fact]
    public void ExecutableSubset_NestedExecutableFunctions_UseClosureSlots()
    {
        var frontend = LythonFrontend.Compile("""
def outer(value):
    def inner():
        return value
    value = value + 1
    return inner()

return outer(3)
""",regexProvider:Utf8RegexProvider.Instance);

        var syntax = frontend.Script.RequireNotNull();
        var executable = ExecutableScript.Compile(syntax);
        var outer = Assert.Single(executable.EntryPoint.FunctionBindings());
        var inner = Assert.Single(outer.CodeObject.RequireNotNull().FunctionBindings());
        var innerCode = inner.CodeObject.RequireNotNull();

        Assert.Equal(["value"], innerCode.ClosureNames);
        Assert.Contains(
            innerCode.Instructions,
            instruction => instruction.OpCode == ExecutableOpCode.LoadClosure);

        var result = new LythonRuntime().Run(executable, new MockLythonHost(), null);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(4), result.ReturnValue);
    }

    [Fact]
    public void ExecutableSubset_TryExceptElseFinally_ExecutesThroughExecutablePipeline()
    {
        var frontend = LythonFrontend.Compile("""
value = 0
try:
    value = 1
except ValueError:
    value = 2
else:
    value = value + 3
finally:
    value = value + 4

return value
""",regexProvider:Utf8RegexProvider.Instance);

        var syntax = frontend.Script.RequireNotNull();
        var executable = ExecutableScript.Compile(syntax);

        Assert.NotEmpty(executable.EntryPoint.ExceptionRegions);

        var result = new LythonRuntime().Run(executable, new MockLythonHost(), null);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(8), result.ReturnValue);
    }

    [Fact]
    public void ExecutableSubset_TryExceptFinally_CatchesExceptionsThroughExecutableRegions()
    {
        var frontend = LythonFrontend.Compile("""
value = 0
try:
    raise ValueError("boom")
except ValueError as ex:
    value = len(ex.message)
finally:
    value = value + 1

return value
""",regexProvider:Utf8RegexProvider.Instance);

        var syntax = frontend.Script.RequireNotNull();
        var executable = ExecutableScript.Compile(syntax);
        var result = new LythonRuntime().Run(executable, new MockLythonHost(), null);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(5), result.ReturnValue);
    }

    [Fact]
    public void ExecutableSubset_UnmatchedInnerHandlerContinuesToOuterHandler()
    {
        var frontend = LythonFrontend.Compile("""
value = "unhandled"
try:
    try:
        raise ValueError("boom")
    except TypeError:
        value = "inner"
except Exception:
    value = "outer"

return value
""",regexProvider:Utf8RegexProvider.Instance);

        var syntax = frontend.Script.RequireNotNull();
        var executable = ExecutableScript.Compile(syntax);
        var result = new LythonRuntime().Run(executable, new MockLythonHost(), null);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("outer", result.ReturnValue);
    }

    [Fact]
    public void ExecutableSubset_ExceptionHandlersRestoreEnclosingLoopStackDepth()
    {
        var frontend = LythonFrontend.Compile("""
values = []
for value in [0, 1]:
    try:
        values.append(str(4 // value))
    except ZeroDivisionError as ex:
        values.append(ex.type)
return "|".join(values)
""",regexProvider:Utf8RegexProvider.Instance);

        var syntax = frontend.Script.RequireNotNull();
        var executable = ExecutableScript.Compile(syntax);
        var result = new LythonRuntime().Run(executable, new MockLythonHost(), null);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("ZeroDivisionError|4", result.ReturnValue);
    }

    [Fact]
    public void ExecutableSubset_TryFinally_HonorsReturnThroughExecutableRegions()
    {
        var frontend = LythonFrontend.Compile("""
try:
    return 1
finally:
    marker = 2
""",regexProvider:Utf8RegexProvider.Instance);

        var syntax = frontend.Script.RequireNotNull();
        var executable = ExecutableScript.Compile(syntax);
        var result = new LythonRuntime().Run(executable, new MockLythonHost(), null);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(1), result.ReturnValue);
    }

    [Fact]
    public void ExecutableSubset_WithStatement_ExecutesThroughExecutablePipeline()
    {
        var frontend = LythonFrontend.Compile("""
with open("/sample.txt", "w") as handle:
    handle.write("alpha")

return open("/sample.txt").read()
""",regexProvider:Utf8RegexProvider.Instance);

        var syntax = frontend.Script.RequireNotNull();
        var executable = ExecutableScript.Compile(syntax);
        var result = new LythonRuntime().Run(executable, new MockLythonHost(), null);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("alpha", result.ReturnValue);
    }

    [Fact]
    public void ExecutableSubset_WithStatement_InvokesUnsuppressingExitOnce()
    {
        var frontend = LythonFrontend.Compile("""
events = []

class Manager:
    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc, traceback):
        events.append(exc_type.__name__)
        return False

try:
    with Manager():
        raise ValueError("boom")
except ValueError:
    events.append("caught")

return "|".join(events)
""",regexProvider:Utf8RegexProvider.Instance);

        var syntax = frontend.Script.RequireNotNull();
        var executable = ExecutableScript.Compile(syntax);
        var result = new LythonRuntime().Run(executable, new MockLythonHost(), null);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("ValueError|caught", result.ReturnValue);
    }

    [Fact]
    public void ExecutableSubset_MatchStatement_ExecutesThroughExecutablePipeline()
    {
        var frontend = LythonFrontend.Compile("""
def choose(value):
    match value:
        case {"x": x, "y": y} if x < y:
            return x + y
        case _:
            return 0

return choose({"x": 2, "y": 5})
""",regexProvider:Utf8RegexProvider.Instance);

        var syntax = frontend.Script.RequireNotNull();
        var executable = ExecutableScript.Compile(syntax);
        var result = new LythonRuntime().Run(executable, new MockLythonHost(), null);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(7), result.ReturnValue);
    }

    [Fact]
    public void ExecutableSubset_StarredCalls_PreserveExpansionOrder()
    {
        var frontend = LythonFrontend.Compile("""
return sorted(*[[3, 1, 2]], reverse=True)
""",regexProvider:Utf8RegexProvider.Instance);

        var syntax = frontend.Script.RequireNotNull();
        var executable = ExecutableScript.Compile(syntax);
        var result = new LythonRuntime().Run(executable, new MockLythonHost(), null);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new object?[] { new System.Numerics.BigInteger(3), new System.Numerics.BigInteger(2), new System.Numerics.BigInteger(1) }, result.ReturnValue);
    }

    [Fact]
    public void Engine_ExecutesAnOrdinaryHelper()
    {
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run("""
def helper():
    return 7

return helper()
""", new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(7), result.ReturnValue);
    }

    [Fact]
    public void Engine_ExecutesNestedStarredCall()
    {
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run("""
return len(sorted(*[[3, 1, 2]], reverse=True))
""", new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(3), result.ReturnValue);
    }

    [Fact]
    public void ExecutableSubset_TupleForLoopTarget_ExecutesThroughExecutablePipeline()
    {
        var frontend = LythonFrontend.Compile("""
total = 0
for left, right in [(1, 2), (3, 4)]:
    total = total + left + right

return total
""",regexProvider:Utf8RegexProvider.Instance);

        var syntax = frontend.Script.RequireNotNull();
        var executable = ExecutableScript.Compile(syntax);
        var result = new LythonRuntime().Run(executable, new MockLythonHost(), null);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(10), result.ReturnValue);
    }

    [Fact]
    public void Engine_ExecutesRepresentativeImportAndHelper()
    {
        var result = new LythonEngine(Utf8RegexProvider.Instance).Run("""
import json

def helper(values, suffix="!"):
    total = 0
    for value in values:
        if value % 2 == 0:
            continue
        total += value
    return f"{total}{suffix}"

payload = {"items": [1, 2, 3], "ok": True}
text = json.dumps(payload)
return helper(payload["items"]) + "|" + text
""", new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("4!|{\"items\": [1, 2, 3], \"ok\": true}", result.ReturnValue);
    }
}

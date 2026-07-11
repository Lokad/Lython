using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class LoweringArchitectureSubsystemTests
{
    [Fact]
    public void LoweredScript_CapturesTopLevelImportsAndFunctions()
    {
        var frontend = LythonFrontend.Compile("""
import json

def helper():
    if True:
        return 1
    return 1

value = 2
""");

        var lowered = LoweredScript.Lower(frontend.Script!);

        Assert.Single(lowered.TopLevelImports);
        Assert.Single(lowered.TopLevelFunctions);
        Assert.Equal(lowered.Syntax.Statements.Count, lowered.Statements.Count);
        Assert.IsType<LoweredImportStatement>(lowered.Statements[0]);
        var function = Assert.IsType<LoweredFunctionDefinitionStatement>(lowered.Statements[1]);
        var assignment = Assert.IsType<LoweredAssignmentStatement>(lowered.Statements[2]);
        var loweredIf = Assert.IsType<LoweredIfStatement>(function.Body[0]);
        Assert.IsType<LoweredBooleanLiteralExpression>(loweredIf.Condition);
        Assert.Contains(function.Body, statement => statement is LoweredReturnStatement);
        Assert.IsType<LoweredIntegerLiteralExpression>(assignment.Expression);
    }

    [Fact]
    public void LoweredScript_LowersControlFlowBodiesRecursively()
    {
        var frontend = LythonFrontend.Compile("""
for item in [1]:
    with open("x") as handle:
        try:
            print = item
        except Exception:
            pass
""");

        var lowered = LoweredScript.Lower(frontend.Script!);
        var loop = Assert.IsType<LoweredForStatement>(Assert.Single(lowered.Statements));
        Assert.IsType<LoweredListLiteralExpression>(loop.Iterable);
        var withStatement = Assert.IsType<LoweredWithStatement>(Assert.Single(loop.Body));
        Assert.IsType<LoweredCallExpression>(withStatement.ContextExpression);
        var tryStatement = Assert.IsType<LoweredTryStatement>(Assert.Single(withStatement.Body));
        Assert.Single(tryStatement.TryBody);
        Assert.Single(tryStatement.ExceptBody!);
    }

    [Fact]
    public void LoweredScript_LowersFormattedStringsAndComprehensions()
    {
        var frontend = LythonFrontend.Compile("""
values = [f"{item:03d}" for item in [1, 2] if item]
mapping = {item: f"{item!s}" for item in [1, 2]}
unique = {f"{item}" for item in [1, 2] if item}
""");

        var lowered = LoweredScript.Lower(frontend.Script!);
        var listAssignment = Assert.IsType<LoweredAssignmentStatement>(lowered.Statements[0]);
        var listComprehension = Assert.IsType<LoweredListComprehensionExpression>(listAssignment.Expression);
        var listFormatted = Assert.IsType<LoweredFormattedStringExpression>(listComprehension.ItemExpression);
        var listPart = Assert.IsType<LoweredFormattedStringExpressionPart>(Assert.Single(listFormatted.Parts));
        Assert.Equal("03d", listPart.FormatSpecifier);
        Assert.Single(listComprehension.Clauses);
        Assert.IsType<LoweredListLiteralExpression>(listComprehension.Clauses[0].Iterable);
        Assert.IsType<LoweredIdentifierExpression>(listComprehension.Clauses[0].Condition);

        var dictAssignment = Assert.IsType<LoweredAssignmentStatement>(lowered.Statements[1]);
        var dictComprehension = Assert.IsType<LoweredDictComprehensionExpression>(dictAssignment.Expression);
        Assert.IsType<LoweredIdentifierExpression>(dictComprehension.KeyExpression);
        var dictFormatted = Assert.IsType<LoweredFormattedStringExpression>(dictComprehension.ValueExpression);
        var dictPart = Assert.IsType<LoweredFormattedStringExpressionPart>(Assert.Single(dictFormatted.Parts));
        Assert.Equal('s', dictPart.Conversion);

        var setAssignment = Assert.IsType<LoweredAssignmentStatement>(lowered.Statements[2]);
        var setComprehension = Assert.IsType<LoweredSetComprehensionExpression>(setAssignment.Expression);
        Assert.IsType<LoweredFormattedStringExpression>(setComprehension.ItemExpression);
        Assert.Single(setComprehension.Clauses);
        Assert.IsType<LoweredListLiteralExpression>(setComprehension.Clauses[0].Iterable);
        Assert.IsType<LoweredIdentifierExpression>(setComprehension.Clauses[0].Condition);
    }

    [Fact]
    public void LoweredScript_LowersMemberCallAndOperatorExpressions()
    {
        var frontend = LythonFrontend.Compile("""
value = helper.items[1] + (-count if ready else 0)
result = helper.run(*items, **mapping)
ok = left < middle < right
""");

        var lowered = LoweredScript.Lower(frontend.Script!);

        var valueAssignment = Assert.IsType<LoweredAssignmentStatement>(lowered.Statements[0]);
        var binary = Assert.IsType<LoweredBinaryExpression>(valueAssignment.Expression);
        var subscript = Assert.IsType<LoweredSubscriptExpression>(binary.Left);
        Assert.IsType<LoweredMemberExpression>(subscript.Target);
        var parenthesized = Assert.IsType<LoweredParenthesizedExpression>(binary.Right);
        var conditional = Assert.IsType<LoweredConditionalExpression>(parenthesized.Inner);
        Assert.IsType<LoweredUnaryExpression>(conditional.Consequent);

        var resultAssignment = Assert.IsType<LoweredAssignmentStatement>(lowered.Statements[1]);
        var call = Assert.IsType<LoweredCallExpression>(resultAssignment.Expression);
        Assert.IsType<LoweredMemberExpression>(call.Target);
        Assert.Contains(call.Arguments, argument => argument.Kind == CallArgumentKind.StarredList);
        Assert.Contains(call.Arguments, argument => argument.Kind == CallArgumentKind.StarredDictionary);

        var comparisonAssignment = Assert.IsType<LoweredAssignmentStatement>(lowered.Statements[2]);
        var chained = Assert.IsType<LoweredChainedComparisonExpression>(comparisonAssignment.Expression);
        Assert.Equal(3, chained.Operands.Count);
    }

    [Fact]
    public void LoweredScript_LowersLambdaAndSimpleControlStatements()
    {
        var frontend = LythonFrontend.Compile("""
fn = lambda x, *, step=1: x + step
assert ready, "bad"
del items[0]
if ready:
    pass
else:
    raise Error("bad")
""");

        var lowered = LoweredScript.Lower(frontend.Script!);

        var lambdaAssignment = Assert.IsType<LoweredAssignmentStatement>(lowered.Statements[0]);
        var lambda = Assert.IsType<LoweredLambdaExpression>(lambdaAssignment.Expression);
        Assert.IsType<LoweredBinaryExpression>(lambda.Body);
        Assert.Equal(FunctionParameterKind.KeywordOnly, lambda.Lambda.Parameters[1].Kind);

        var assertStatement = Assert.IsType<LoweredAssertStatement>(lowered.Statements[1]);
        Assert.IsType<LoweredIdentifierExpression>(assertStatement.Condition);
        Assert.IsType<LoweredStringLiteralExpression>(assertStatement.Message);

        var deleteStatement = Assert.IsType<LoweredDeleteStatement>(lowered.Statements[2]);
        Assert.IsType<LoweredSubscriptExpression>(deleteStatement.Target);

        var ifStatement = Assert.IsType<LoweredIfStatement>(lowered.Statements[3]);
        Assert.IsType<LoweredPassStatement>(Assert.Single(ifStatement.ThenStatements));
        Assert.IsType<LoweredRaiseStatement>(Assert.Single(ifStatement.ElseStatements!));
    }

    [Fact]
    public void LoweredScript_LowersAssignmentExpressionsAndLoopElse()
    {
        var frontend = LythonFrontend.Compile("""
if (n := 1):
    pass

for item in [1]:
    pass
else:
    pass

while False:
    pass
else:
    pass
""");

        var lowered = LoweredScript.Lower(frontend.Script!);

        var ifStatement = Assert.IsType<LoweredIfStatement>(lowered.Statements[0]);
        var condition = Assert.IsType<LoweredParenthesizedExpression>(ifStatement.Condition);
        Assert.IsType<LoweredAssignmentExpression>(condition.Inner);

        var forStatement = Assert.IsType<LoweredForStatement>(lowered.Statements[1]);
        Assert.NotNull(forStatement.ElseStatements);
        Assert.Single(forStatement.ElseStatements!);

        var whileStatement = Assert.IsType<LoweredWhileStatement>(lowered.Statements[2]);
        Assert.NotNull(whileStatement.ElseStatements);
        Assert.Single(whileStatement.ElseStatements!);
    }

    [Fact]
    public void LoweredScript_LowersGeneratorsBytesNestedDefsAndMemberAssignments()
    {
        var frontend = LythonFrontend.Compile("""
def outer():
    def inner(*, suffix="!"):
        return (b"ab"[0], (x for x in [1, 2]), suffix)
    return inner

helper.value = b"x"
""");

        var lowered = LoweredScript.Lower(frontend.Script!);
        var outer = Assert.IsType<LoweredFunctionDefinitionStatement>(lowered.Statements[0]);
        var inner = Assert.IsType<LoweredFunctionDefinitionStatement>(outer.Body[0]);
        Assert.Equal(FunctionParameterKind.KeywordOnly, inner.Parameters[0].Kind);

        var returnStatement = Assert.IsType<LoweredReturnStatement>(inner.Body[0]);
        var tuple = Assert.IsType<LoweredTupleLiteralExpression>(returnStatement.Expression);
        Assert.IsType<LoweredBytesLiteralExpression>(Assert.IsType<LoweredSubscriptExpression>(tuple.Items[0]).Target);
        Assert.IsType<LoweredGeneratorExpression>(tuple.Items[1]);

        var assignment = Assert.IsType<LoweredAssignmentStatement>(lowered.Statements[1]);
        Assert.Equal("value", assignment.MemberName);
        Assert.IsType<LoweredBytesLiteralExpression>(assignment.Expression);
    }

    [Fact]
    public void LoweredScript_LowersMatchStatementsWithoutGenericFallback()
    {
        var frontend = LythonFrontend.Compile("""
import pathlib

match pathlib.Path("/docs/guide.md"):
    case pathlib.Path(name=name, suffix=suffix):
        value = name
    case _:
        value = suffix
""");

        var lowered = LoweredScript.Lower(frontend.Script!);
        var matchStatement = Assert.IsType<LoweredMatchStatement>(lowered.Statements[1]);
        Assert.IsType<LoweredCallExpression>(matchStatement.Subject);
        Assert.DoesNotContain(FlattenStatements(lowered.Statements), statement => statement is LoweredOtherStatement);
    }

    [Fact]
    public void LoweredScript_DoesNotUseGenericFallbacksForRepresentativeSupportedSubset()
    {
        var frontend = LythonFrontend.Compile("""
import json, re

def helper(xs, prefix="v"):
    total = 0
    for item in xs:
        total += item
    return f"{prefix}:{total}"

values = [helper([1, 2]), helper([3], prefix="x")]
mapping = {item: item.upper() for item in values if item}
assert values[0].startswith("v")
""");

        var lowered = LoweredScript.Lower(frontend.Script!);
        Assert.DoesNotContain(FlattenStatements(lowered.Statements), statement => statement is LoweredOtherStatement);
        Assert.DoesNotContain(FlattenExpressions(lowered.Statements), expression => expression is LoweredOtherExpression);
    }

    [Fact]
    public void LoweredScript_DoesNotUseGenericFallbacksForOrdinaryParserShapes()
    {
        var frontend = LythonFrontend.Compile("""
from __future__ import annotations

match = "alpha"
case = "beta"
value = "a" + \
    "b"

def helper(*parts, sep="|", suffix):
    typed: list[str] = ["x"]
    return sep.join(parts) + suffix + typed[0]

line = [match, case][0].upper().splitlines(True)[0]
result = helper(*["a", "b"], suffix="?")
""");

        var lowered = LoweredScript.Lower(frontend.Script!);
        Assert.DoesNotContain(FlattenStatements(lowered.Statements), statement => statement is LoweredOtherStatement);
        Assert.DoesNotContain(FlattenExpressions(lowered.Statements), expression => expression is LoweredOtherExpression);
    }

    [Fact]
    public void ExecutableScript_CompilesSimpleAssignmentsIntoLocalsAndConstants()
    {
        var frontend = LythonFrontend.Compile("""
x = 1
y = x + 2
y
""");

        var lowered = LoweredScript.Lower(frontend.Script!);
        var executable = ExecutableScript.Compile(lowered);
        var entry = executable.EntryPoint;

        Assert.Equal(["x", "y"], entry.LocalNames);
        Assert.Empty(entry.Names);
        Assert.Equal(2, entry.Constants.Count);
        Assert.Contains(new System.Numerics.BigInteger(1), entry.Constants);
        Assert.Contains(new System.Numerics.BigInteger(2), entry.Constants);
        Assert.Single(entry.Blocks);

        var instructions = entry.Blocks[0].Instructions;
        Assert.Collection(
            instructions,
            instruction => Assert.Equal(ExecutableOpCode.LoadConst, instruction.OpCode),
            instruction => Assert.Equal(ExecutableOpCode.StoreLocal, instruction.OpCode),
            instruction => Assert.Equal(ExecutableOpCode.LoadLocal, instruction.OpCode),
            instruction => Assert.Equal(ExecutableOpCode.LoadConst, instruction.OpCode),
            instruction =>
            {
                Assert.Equal(ExecutableOpCode.Binary, instruction.OpCode);
                Assert.Equal(ExecutableBinaryOperator.Add, instruction.BinaryOperator);
            },
            instruction => Assert.Equal(ExecutableOpCode.StoreLocal, instruction.OpCode),
            instruction => Assert.Equal(ExecutableOpCode.LoadLocal, instruction.OpCode),
            instruction => Assert.Equal(ExecutableOpCode.PopTop, instruction.OpCode),
            instruction => Assert.Equal(ExecutableOpCode.ReturnNone, instruction.OpCode));
    }

    [Fact]
    public void ExecutableScript_CompilesIfAndWhileIntoExplicitBlocksAndJumps()
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
""");

        var lowered = LoweredScript.Lower(frontend.Script!);
        var executable = ExecutableScript.Compile(lowered);
        var entry = executable.EntryPoint;

        Assert.Equal(["count"], entry.LocalNames);
        Assert.True(entry.Blocks.Count >= 7);
        Assert.Contains(entry.Blocks, block => block.Instructions.Any(instruction => instruction.OpCode == ExecutableOpCode.JumpIfFalse));
        Assert.Contains(entry.Blocks, block => block.Instructions.Any(instruction => instruction.OpCode == ExecutableOpCode.Jump));
        Assert.Contains(entry.Blocks, block => block.Instructions.Any(instruction =>
            instruction.OpCode == ExecutableOpCode.Binary && instruction.BinaryOperator == ExecutableBinaryOperator.Subtract));
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
""");

        var lowered = LoweredScript.Lower(frontend.Script!);
        var executable = ExecutableScript.Compile(lowered);

        Assert.DoesNotContain(
            executable.EntryPoint.Blocks.SelectMany(block => block.Instructions),
            instruction => instruction.OpCode == ExecutableOpCode.Jump &&
                           executable.EntryPoint.Blocks[instruction.A].Instructions.Count == 1 &&
                           executable.EntryPoint.Blocks[instruction.A].Instructions[0].OpCode == ExecutableOpCode.Jump);
    }

    [Fact]
    public void ExecutableScript_CompilesRepresentativeFallbackShapes()
    {
        var frontend = LythonFrontend.Compile("""
class Box:
    pass

value = sorted(*[[3, 1, 2]], reverse=True)
""");

        var lowered = LoweredScript.Lower(frontend.Script!);
        var executable = ExecutableScript.Compile(lowered);

        Assert.NotEmpty(executable.EntryPoint.StatementFallbacks);
        Assert.NotEmpty(executable.EntryPoint.ExpressionFallbacks);
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
""");

        var lowered = LoweredScript.Lower(frontend.Script!);
        var executable = ExecutableScript.Compile(lowered);
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
""");

        var lowered = LoweredScript.Lower(frontend.Script!);
        var executable = ExecutableScript.Compile(lowered);
        var result = new LythonRuntime().Run(executable, new MockLythonHost(), null);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(4), result.ReturnValue);
    }

    [Fact]
    public void ExecutableSubset_BuiltinCall_ExecutesThroughExecutablePipeline()
    {
        var frontend = LythonFrontend.Compile("""
return len([1, 2, 3])
""");

        var lowered = LoweredScript.Lower(frontend.Script!);
        var executable = ExecutableScript.Compile(lowered);
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
""");

        var lowered = LoweredScript.Lower(frontend.Script!);
        var executable = ExecutableScript.Compile(lowered);
        Assert.True(executable.EntryPoint.MemberCacheCount > 0);
        Assert.True(executable.EntryPoint.CallCacheCount > 0);
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
""");

        var lowered = LoweredScript.Lower(frontend.Script!);
        var executable = ExecutableScript.Compile(lowered);
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
""");

        var lowered = LoweredScript.Lower(frontend.Script!);
        var executable = ExecutableScript.Compile(lowered);
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
""");

        var lowered = LoweredScript.Lower(frontend.Script!);
        var executable = ExecutableScript.Compile(lowered);
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
""");

        var lowered = LoweredScript.Lower(frontend.Script!);
        var executable = ExecutableScript.Compile(lowered);
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
""");

        var lowered = LoweredScript.Lower(frontend.Script!);
        var executable = ExecutableScript.Compile(lowered);
        Assert.Single(executable.EntryPoint.Functions);
        Assert.NotNull(executable.EntryPoint.Functions[0].CodeObject);
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
""");

        var lowered = LoweredScript.Lower(frontend.Script!);
        var executable = ExecutableScript.Compile(lowered);
        Assert.Single(executable.EntryPoint.Functions);
        Assert.NotNull(executable.EntryPoint.Functions[0].CodeObject);
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
""");

        var lowered = LoweredScript.Lower(frontend.Script!);
        var executable = ExecutableScript.Compile(lowered);
        var outer = Assert.Single(executable.EntryPoint.Functions);
        var inner = Assert.Single(outer.CodeObject!.Functions);

        Assert.Equal(["value"], inner.CodeObject!.ClosureNames);
        Assert.Contains(
            inner.CodeObject.Blocks.SelectMany(block => block.Instructions),
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
""");

        var lowered = LoweredScript.Lower(frontend.Script!);
        var executable = ExecutableScript.Compile(lowered);

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
""");

        var lowered = LoweredScript.Lower(frontend.Script!);
        var executable = ExecutableScript.Compile(lowered);
        var result = new LythonRuntime().Run(executable, new MockLythonHost(), null);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(5), result.ReturnValue);
    }

    [Fact]
    public void ExecutableSubset_TryFinally_HonorsReturnThroughExecutableRegions()
    {
        var frontend = LythonFrontend.Compile("""
try:
    return 1
finally:
    marker = 2
""");

        var lowered = LoweredScript.Lower(frontend.Script!);
        var executable = ExecutableScript.Compile(lowered);
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
""");

        var lowered = LoweredScript.Lower(frontend.Script!);
        var executable = ExecutableScript.Compile(lowered);
        var result = new LythonRuntime().Run(executable, new MockLythonHost(), null);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("alpha", result.ReturnValue);
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
""");

        var lowered = LoweredScript.Lower(frontend.Script!);
        var executable = ExecutableScript.Compile(lowered);
        var result = new LythonRuntime().Run(executable, new MockLythonHost(), null);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(7), result.ReturnValue);
    }

    [Fact]
    public void ExecutableSubset_StarredCalls_ExecuteThroughFallbackInsideExecutablePipeline()
    {
        var frontend = LythonFrontend.Compile("""
return sorted(*[[3, 1, 2]], reverse=True)
""");

        var lowered = LoweredScript.Lower(frontend.Script!);
        var executable = ExecutableScript.Compile(lowered);
        var result = new LythonRuntime().Run(executable, new MockLythonHost(), null);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new object?[] { new System.Numerics.BigInteger(3), new System.Numerics.BigInteger(2), new System.Numerics.BigInteger(1) }, result.ReturnValue);
    }

    [Fact]
    public void Engine_FallsBackToLoweredInterpreter_WhenExecutableIrIsNotAvailable()
    {
        var result = new LythonEngine().Run("""
def helper():
    return 7

return helper()
""", new MockLythonHost());

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(7), result.ReturnValue);
    }

    [Fact]
    public void Engine_UsesExecutablePipeline_ForStarredCallViaFallbackExpression()
    {
        var result = new LythonEngine().Run("""
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
""");

        var lowered = LoweredScript.Lower(frontend.Script!);
        var executable = ExecutableScript.Compile(lowered);
        var result = new LythonRuntime().Run(executable, new MockLythonHost(), null);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(new System.Numerics.BigInteger(10), result.ReturnValue);
    }

    [Fact]
    public void SupportedSubsetScript_ExecutesThroughLoweredPipelineWithoutFallbacks()
    {
        var result = new LythonEngine().Run("""
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
        Assert.Equal("4!|{\"items\":[1,2,3],\"ok\":true}", result.ReturnValue);
    }

    private static IEnumerable<LoweredStatement> FlattenStatements(IEnumerable<LoweredStatement> statements)
    {
        foreach (var statement in statements)
        {
            yield return statement;
            switch (statement)
            {
                case LoweredFunctionDefinitionStatement functionDefinition:
                    foreach (var nested in FlattenStatements(functionDefinition.Body))
                    {
                        yield return nested;
                    }
                    break;
                case LoweredIfStatement ifStatement:
                    foreach (var nested in FlattenStatements(ifStatement.ThenStatements))
                    {
                        yield return nested;
                    }

                    if (ifStatement.ElseStatements is not null)
                    {
                        foreach (var nested in FlattenStatements(ifStatement.ElseStatements))
                        {
                            yield return nested;
                        }
                    }
                    break;
                case LoweredForStatement forStatement:
                    foreach (var nested in FlattenStatements(forStatement.Body))
                    {
                        yield return nested;
                    }
                    if (forStatement.ElseStatements is not null)
                    {
                        foreach (var nested in FlattenStatements(forStatement.ElseStatements))
                        {
                            yield return nested;
                        }
                    }
                    break;
                case LoweredWhileStatement whileStatement:
                    foreach (var nested in FlattenStatements(whileStatement.Body))
                    {
                        yield return nested;
                    }
                    if (whileStatement.ElseStatements is not null)
                    {
                        foreach (var nested in FlattenStatements(whileStatement.ElseStatements))
                        {
                            yield return nested;
                        }
                    }
                    break;
                case LoweredWithStatement withStatement:
                    foreach (var nested in FlattenStatements(withStatement.Body))
                    {
                        yield return nested;
                    }
                    break;
                case LoweredTryStatement tryStatement:
                    foreach (var nested in FlattenStatements(tryStatement.TryBody))
                    {
                        yield return nested;
                    }

                    if (tryStatement.ExceptBody is not null)
                    {
                        foreach (var nested in FlattenStatements(tryStatement.ExceptBody))
                        {
                            yield return nested;
                        }
                    }

                    if (tryStatement.ElseBody is not null)
                    {
                        foreach (var nested in FlattenStatements(tryStatement.ElseBody))
                        {
                            yield return nested;
                        }
                    }

                    if (tryStatement.FinallyBody is not null)
                    {
                        foreach (var nested in FlattenStatements(tryStatement.FinallyBody))
                        {
                            yield return nested;
                        }
                    }
                    break;
            }
        }
    }

    private static IEnumerable<LoweredExpression> FlattenExpressions(IEnumerable<LoweredStatement> statements)
    {
        foreach (var statement in FlattenStatements(statements))
        {
            switch (statement)
            {
                case LoweredFunctionDefinitionStatement functionDefinition:
                    foreach (var parameter in functionDefinition.Parameters)
                    {
                        if (parameter.DefaultValue is not null)
                        {
                            foreach (var expression in FlattenExpressions(parameter.DefaultValue))
                            {
                                yield return expression;
                            }
                        }
                    }
                    break;
                case LoweredAssignmentStatement assignment:
                    if (assignment.Expression is not null)
                    {
                        foreach (var expression in FlattenExpressions(assignment.Expression))
                        {
                            yield return expression;
                        }
                    }

                    if (assignment.Annotation is not null)
                    {
                        foreach (var expression in FlattenExpressions(assignment.Annotation))
                        {
                            yield return expression;
                        }
                    }

                    if (assignment.Target is not null)
                    {
                        foreach (var expression in FlattenExpressions(assignment.Target))
                        {
                            yield return expression;
                        }
                    }

                    if (assignment.Index is not null)
                    {
                        foreach (var expression in FlattenExpressions(assignment.Index))
                        {
                            yield return expression;
                        }
                    }
                    break;
                case LoweredExpressionStatement expressionStatement:
                    foreach (var expression in FlattenExpressions(expressionStatement.Expression))
                    {
                        yield return expression;
                    }
                    break;
                case LoweredIfStatement ifStatement:
                    foreach (var expression in FlattenExpressions(ifStatement.Condition))
                    {
                        yield return expression;
                    }
                    break;
                case LoweredForStatement forStatement:
                    foreach (var expression in FlattenExpressions(forStatement.Iterable))
                    {
                        yield return expression;
                    }
                    break;
                case LoweredWhileStatement whileStatement:
                    foreach (var expression in FlattenExpressions(whileStatement.Condition))
                    {
                        yield return expression;
                    }
                    break;
                case LoweredWithStatement withStatement:
                    foreach (var expression in FlattenExpressions(withStatement.ContextExpression))
                    {
                        yield return expression;
                    }
                    break;
                case LoweredAssertStatement assertStatement:
                    foreach (var expression in FlattenExpressions(assertStatement.Condition))
                    {
                        yield return expression;
                    }

                    if (assertStatement.Message is not null)
                    {
                        foreach (var expression in FlattenExpressions(assertStatement.Message))
                        {
                            yield return expression;
                        }
                    }
                    break;
                case LoweredDeleteStatement deleteStatement:
                    foreach (var expression in FlattenExpressions(deleteStatement.Target))
                    {
                        yield return expression;
                    }
                    break;
                case LoweredReturnStatement returnStatement when returnStatement.Expression is not null:
                    foreach (var expression in FlattenExpressions(returnStatement.Expression))
                    {
                        yield return expression;
                    }
                    break;
                case LoweredRaiseStatement raiseStatement:
                    foreach (var expression in FlattenExpressions(raiseStatement.Expression))
                    {
                        yield return expression;
                    }
                    break;
            }
        }
    }

    private static IEnumerable<LoweredExpression> FlattenExpressions(LoweredExpression expression)
    {
        yield return expression;

        switch (expression)
        {
            case LoweredFormattedStringExpression formatted:
                foreach (var part in formatted.Parts.OfType<LoweredFormattedStringExpressionPart>())
                {
                    foreach (var nested in FlattenExpressions(part.Expression))
                    {
                        yield return nested;
                    }
                }
                break;
            case LoweredParenthesizedExpression parenthesized:
                foreach (var nested in FlattenExpressions(parenthesized.Inner))
                {
                    yield return nested;
                }
                break;
            case LoweredListLiteralExpression list:
                foreach (var item in list.Items.SelectMany(FlattenExpressions))
                {
                    yield return item;
                }
                break;
            case LoweredTupleLiteralExpression tuple:
                foreach (var item in tuple.Items.SelectMany(FlattenExpressions))
                {
                    yield return item;
                }
                break;
            case LoweredSetLiteralExpression set:
                foreach (var item in set.Items.SelectMany(FlattenExpressions))
                {
                    yield return item;
                }
                break;
            case LoweredListComprehensionExpression listComprehension:
                foreach (var nested in FlattenExpressions(listComprehension.ItemExpression))
                {
                    yield return nested;
                }
                foreach (var clause in listComprehension.Clauses)
                {
                    foreach (var nested in FlattenExpressions(clause.Iterable))
                    {
                        yield return nested;
                    }
                    if (clause.Condition is not null)
                    {
                        foreach (var nested in FlattenExpressions(clause.Condition))
                        {
                            yield return nested;
                        }
                    }
                }
                break;
            case LoweredSetComprehensionExpression setComprehension:
                foreach (var nested in FlattenExpressions(setComprehension.ItemExpression))
                {
                    yield return nested;
                }
                foreach (var clause in setComprehension.Clauses)
                {
                    foreach (var nested in FlattenExpressions(clause.Iterable))
                    {
                        yield return nested;
                    }
                    if (clause.Condition is not null)
                    {
                        foreach (var nested in FlattenExpressions(clause.Condition))
                        {
                            yield return nested;
                        }
                    }
                }
                break;
            case LoweredGeneratorExpression generator:
                foreach (var nested in FlattenExpressions(generator.ItemExpression))
                {
                    yield return nested;
                }
                foreach (var clause in generator.Clauses)
                {
                    foreach (var nested in FlattenExpressions(clause.Iterable))
                    {
                        yield return nested;
                    }
                    if (clause.Condition is not null)
                    {
                        foreach (var nested in FlattenExpressions(clause.Condition))
                        {
                            yield return nested;
                        }
                    }
                }
                break;
            case LoweredDictLiteralExpression dict:
                foreach (var pair in dict.Items)
                {
                    foreach (var nested in FlattenExpressions(pair.Key))
                    {
                        yield return nested;
                    }
                    foreach (var nested in FlattenExpressions(pair.Value))
                    {
                        yield return nested;
                    }
                }
                break;
            case LoweredDictComprehensionExpression dictComprehension:
                foreach (var nested in FlattenExpressions(dictComprehension.KeyExpression))
                {
                    yield return nested;
                }
                foreach (var nested in FlattenExpressions(dictComprehension.ValueExpression))
                {
                    yield return nested;
                }
                foreach (var clause in dictComprehension.Clauses)
                {
                    foreach (var nested in FlattenExpressions(clause.Iterable))
                    {
                        yield return nested;
                    }
                    if (clause.Condition is not null)
                    {
                        foreach (var nested in FlattenExpressions(clause.Condition))
                        {
                            yield return nested;
                        }
                    }
                }
                break;
            case LoweredMemberExpression member:
                foreach (var nested in FlattenExpressions(member.Target))
                {
                    yield return nested;
                }
                break;
            case LoweredCallExpression call:
                foreach (var nested in FlattenExpressions(call.Target))
                {
                    yield return nested;
                }
                foreach (var argument in call.Arguments.SelectMany(argument => FlattenExpressions(argument.Expression)))
                {
                    yield return argument;
                }
                break;
            case LoweredSubscriptExpression subscript:
                foreach (var nested in FlattenExpressions(subscript.Target))
                {
                    yield return nested;
                }
                foreach (var nested in FlattenExpressions(subscript.Index))
                {
                    yield return nested;
                }
                break;
            case LoweredSliceExpression slice:
                foreach (var nested in FlattenExpressions(slice.Target))
                {
                    yield return nested;
                }
                if (slice.Start is not null)
                {
                    foreach (var nested in FlattenExpressions(slice.Start))
                    {
                        yield return nested;
                    }
                }
                if (slice.End is not null)
                {
                    foreach (var nested in FlattenExpressions(slice.End))
                    {
                        yield return nested;
                    }
                }
                if (slice.Step is not null)
                {
                    foreach (var nested in FlattenExpressions(slice.Step))
                    {
                        yield return nested;
                    }
                }
                break;
            case LoweredBinaryExpression binary:
                foreach (var nested in FlattenExpressions(binary.Left))
                {
                    yield return nested;
                }
                foreach (var nested in FlattenExpressions(binary.Right))
                {
                    yield return nested;
                }
                break;
            case LoweredChainedComparisonExpression chained:
                foreach (var nested in chained.Operands.SelectMany(FlattenExpressions))
                {
                    yield return nested;
                }
                break;
            case LoweredUnaryExpression unary:
                foreach (var nested in FlattenExpressions(unary.Operand))
                {
                    yield return nested;
                }
                break;
            case LoweredConditionalExpression conditional:
                foreach (var nested in FlattenExpressions(conditional.Consequent))
                {
                    yield return nested;
                }
                foreach (var nested in FlattenExpressions(conditional.Condition))
                {
                    yield return nested;
                }
                foreach (var nested in FlattenExpressions(conditional.Alternative))
                {
                    yield return nested;
                }
                break;
            case LoweredLambdaExpression lambda:
                foreach (var nested in FlattenExpressions(lambda.Body))
                {
                    yield return nested;
                }
                break;
        }
    }
}

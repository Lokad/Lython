using Lokad.Lython.Frontend;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Used only by active synchronous lowered function frames. Keep these routes
    // aligned with the existing shared dispatcher and evaluation operations.
    // Checkpoints remain in the entry points and the existing evaluation methods.
    private static LoweredBlockFlow DispatchLoweredStatement(
        LoweredStatement statement,
        ExecutionContext context)
    {
        switch (statement)
        {
            case LoweredTypeAliasStatement alias:
                ExecuteTypeAlias(alias, context);
                return default;
            case LoweredImportStatement importStatement:
                ExecuteLoweredImport(importStatement, context);
                return default;
            case LoweredScopeDirectiveStatement:
                return default;
            case LoweredFunctionDefinitionStatement functionDefinition:
                ExecuteLoweredFunctionDefinition(functionDefinition, context);
                return default;
            case LoweredClassDefinitionStatement classDefinition:
                ExecuteLoweredClassDefinition(classDefinition, context);
                return default;
            case LoweredAssignmentStatement assignment:
                ExecuteLoweredAssignment(assignment, context);
                return default;
            case LoweredExpressionStatement expression:
                _ = EvaluateLoweredExpression(expression.Expression, context);
                return default;
            case LoweredIfStatement ifStatement:
                ExecuteLoweredIfStatement(ifStatement, context);
                return default;
            case LoweredForStatement forStatement:
                ExecuteLoweredForStatement(forStatement, context);
                return default;
            case LoweredWhileStatement whileStatement:
                ExecuteLoweredWhileStatement(whileStatement, context);
                return default;
            case LoweredMatchStatement matchStatement:
                ExecuteLoweredMatchStatement(matchStatement, context);
                return default;
            case LoweredWithStatement withStatement:
                ExecuteLoweredWithStatement(withStatement, context);
                return default;
            case LoweredTryStatement tryStatement:
                ExecuteLoweredTryStatement(tryStatement, context);
                return default;
            case LoweredPassStatement:
                return default;
            case LoweredBreakStatement:
                throw new BreakSignal();
            case LoweredContinueStatement:
                throw new ContinueSignal();
            case LoweredAssertStatement assertStatement:
                ExecuteLoweredAssertStatement(assertStatement, context);
                return default;
            case LoweredDeleteStatement deleteStatement:
                ExecuteLoweredDeleteStatement(deleteStatement, context);
                return default;
            case LoweredReturnStatement returnStatement:
                if (returnStatement.Expression is null)
                {
                    return new LoweredBlockFlow(null, new ReturnSignal(PyNone.Instance));
                }

                var returnValue = EvaluateLoweredExpression(returnStatement.Expression, context);
                return new LoweredBlockFlow(null, new ReturnSignal(RuntimeValue(returnValue)));
            case LoweredRaiseStatement raiseStatement:
                ExecuteLoweredRaiseStatement(raiseStatement, context);
                return default;
            case LoweredOtherStatement other:
                throw new InvalidOperationException($"Generic lowered statement fallback reached for supported execution: {other.Syntax.GetType().Name}");
            default:
                throw new InvalidOperationException($"Unknown lowered statement kind: {statement.GetType().Name}");
        }
    }

    private static object DispatchLoweredExpression(
        LoweredExpression expression,
        ExecutionContext context)
    {
        switch (expression)
        {
            case LoweredCapturedExpression captured:
                return captured.Value;
            case LoweredUnpackedTypeExpression unpacked:
                var unpackedValue = EvaluateLoweredExpression(unpacked.Value, context);
                return unpackedValue is PyTypeParameter { Kind: TypeParameterKind.TypeVarTuple } or PyGenericAlias
                    ? new PyUnpackedType(unpackedValue) : throw new LythonRuntimeException("TypeError", "Type annotation cannot be unpacked", unpacked.Span);
            case LoweredIdentifierExpression identifier:
                return ResolveIdentifier(identifier.Identifier, context);
            case LoweredStringLiteralExpression text:
                return ValidateLoweredString(SharedStringLiteral(text), context, text.Span);
            case LoweredBytesLiteralExpression bytes:
                var bytesValue = SharedBytesLiteral(bytes);
                context.ObserveValue(bytesValue, bytes.Span);
                return bytesValue;
            case LoweredIntegerLiteralExpression integer:
                return SharedIntegerMagnitude(integer);
            case LoweredImaginaryLiteralExpression imaginary:
                return PyComplex.Create(0, Numbers.PyNumberOps.ParseFloat(imaginary.Literal.ValueText[..^1]), context, imaginary.Span);
            case LoweredFloatLiteralExpression floating:
                return ParseFloat(floating.Literal);
            case LoweredBooleanLiteralExpression boolean:
                return boolean.Literal.Value;
            case LoweredNoneLiteralExpression:
                return PyNone.Instance;
            case LoweredEllipsisLiteralExpression:
                return PyEllipsis.Instance;
            case LoweredFormattedStringExpression formatted:
                return EvaluateLoweredFormattedString(formatted, context);
            case LoweredParenthesizedExpression parenthesized:
                return EvaluateLoweredExpression(parenthesized.Inner, context);
            case LoweredListLiteralExpression list:
                return ValidateLoweredCollection(CreateLoweredListLiteral(list, context), context, list.Span);
            case LoweredListComprehensionExpression comprehension:
                return EvaluateLoweredListComprehension(comprehension, context);
            case LoweredGeneratorExpression generator:
            {
                // Acquire the outermost iterable eagerly, exactly as the synchronous
                // construction site does; user __iter__ still resolves synchronously
                // here (async-capable resolution belongs to a separate change).
                var outer = EvaluateLoweredExpression(generator.Clauses[0].Iterable, context);
                var producedAsync = new PyGeneratorExpression(
                    generator.Clauses,
                    generator.ItemExpression,
                    context.FunctionClosureContext,
                    generator.Span,
                    LythonRuntime.ToSequence(outer, generator.Clauses[0].Iterable.Span, context));
                producedAsync.AttachAsyncOuter(outer);
                context.Services.State.CallTemporaries.TrackFreshMutable(producedAsync, PyIteratorBase.IteratorValueBytes);
                return producedAsync;
            }
            case LoweredTupleLiteralExpression tuple:
                return ValidateLoweredCollection(CreateLoweredTupleLiteral(tuple, context), context, tuple.Span);
            case LoweredSetLiteralExpression set:
                return EvaluateLoweredSetLiteral(set, context);
            case LoweredSetComprehensionExpression comprehension:
                return EvaluateLoweredSetComprehension(comprehension, context);
            case LoweredDictLiteralExpression dictionary:
                return EvaluateLoweredDictLiteral(dictionary, context);
            case LoweredDictComprehensionExpression comprehension:
                return EvaluateLoweredDictComprehension(comprehension, context);
            case LoweredMemberExpression member:
                return ResolveLoweredMember(member, context);
            case LoweredCallExpression call:
                return InvokeLoweredCall(call, context);
            case LoweredSubscriptExpression subscript:
                return EvaluateLoweredSubscript(subscript, context);
            case LoweredSliceExpression slice:
                return EvaluateLoweredSlice(slice, context);
            case LoweredSliceValueExpression slice:
                return EvaluateLoweredSliceValue(slice, context);
            case LoweredBinaryExpression binary:
                return EvaluateLoweredBinary(binary, context);
            case LoweredChainedComparisonExpression chained:
                return EvaluateLoweredChainedComparison(chained, context);
            case LoweredUnaryExpression unary:
                return EvaluateLoweredUnary(unary, context);
            case LoweredConditionalExpression conditional:
                var condition = EvaluateLoweredExpression(conditional.Condition, context);
                return IsTruthy(condition, context, conditional.Condition.Span)
                    ? EvaluateLoweredExpression(conditional.Consequent, context)
                    : EvaluateLoweredExpression(conditional.Alternative, context);
            case LoweredAssignmentExpression assignment:
                return EvaluateLoweredAssignmentExpression(assignment, context);
            case LoweredLambdaExpression lambda:
                return CreateLoweredLambda(lambda, context);
            case LoweredOtherExpression other:
                throw new InvalidOperationException($"Generic lowered expression fallback reached for supported execution: {other.Expression.GetType().Name}");
            default:
                throw new InvalidOperationException($"Unknown lowered expression type: {expression.GetType().Name}");
        }
    }

}

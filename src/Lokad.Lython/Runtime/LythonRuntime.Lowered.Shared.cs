using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private delegate ValueTask<ControlSignal?> LoweredStatementBlockExecutor(
        IReadOnlyList<LoweredStatement> statements,
        ExecutionContext context);

    private delegate ValueTask<object> LoweredExpressionEvaluator(LoweredExpression expression);

    private static async ValueTask DispatchLoweredStatementAsync(
        LoweredStatement statement,
        ExecutionContext context,
        ILoweredStatementExecution execution)
    {
        switch (statement)
        {
            case LoweredImportStatement importStatement:
                await execution.ExecuteImportAsync(importStatement, context).ConfigureAwait(false);
                return;
            case LoweredScopeDirectiveStatement:
                return;
            case LoweredFunctionDefinitionStatement functionDefinition:
                await execution.ExecuteFunctionDefinitionAsync(functionDefinition, context).ConfigureAwait(false);
                return;
            case LoweredClassDefinitionStatement classDefinition:
                await execution.ExecuteClassDefinitionAsync(classDefinition, context).ConfigureAwait(false);
                return;
            case LoweredAssignmentStatement assignment:
                await execution.ExecuteAssignmentAsync(assignment, context).ConfigureAwait(false);
                return;
            case LoweredExpressionStatement expression:
                _ = await execution.EvaluateExpressionAsync(expression.Expression, context).ConfigureAwait(false);
                return;
            case LoweredIfStatement ifStatement:
                await execution.ExecuteIfAsync(ifStatement, context).ConfigureAwait(false);
                return;
            case LoweredForStatement forStatement:
                await execution.ExecuteForAsync(forStatement, context).ConfigureAwait(false);
                return;
            case LoweredWhileStatement whileStatement:
                await execution.ExecuteWhileAsync(whileStatement, context).ConfigureAwait(false);
                return;
            case LoweredMatchStatement matchStatement:
                await execution.ExecuteMatchAsync(matchStatement, context).ConfigureAwait(false);
                return;
            case LoweredWithStatement withStatement:
                await execution.ExecuteWithAsync(withStatement, context).ConfigureAwait(false);
                return;
            case LoweredTryStatement tryStatement:
                await execution.ExecuteTryAsync(tryStatement, context).ConfigureAwait(false);
                return;
            case LoweredPassStatement:
                return;
            case LoweredBreakStatement:
                throw new BreakSignal();
            case LoweredContinueStatement:
                throw new ContinueSignal();
            case LoweredAssertStatement assertStatement:
                await execution.ExecuteAssertAsync(assertStatement, context).ConfigureAwait(false);
                return;
            case LoweredDeleteStatement deleteStatement:
                await execution.ExecuteDeleteAsync(deleteStatement, context).ConfigureAwait(false);
                return;
            case LoweredReturnStatement returnStatement:
                if (returnStatement.Expression is null)
                {
                    throw new ReturnSignal(PyNone.Instance);
                }

                var returnValue = await execution.EvaluateExpressionAsync(returnStatement.Expression, context).ConfigureAwait(false);
                throw new ReturnSignal(RuntimeValue(returnValue));
            case LoweredRaiseStatement raiseStatement:
                await execution.ExecuteRaiseAsync(raiseStatement, context).ConfigureAwait(false);
                return;
            case LoweredOtherStatement other:
                throw new InvalidOperationException($"Generic lowered statement fallback reached for supported execution: {other.Syntax.GetType().Name}");
            default:
                throw new InvalidOperationException($"Unknown lowered statement kind: {statement.GetType().Name}");
        }
    }

    private static async ValueTask<object> DispatchLoweredExpressionAsync(
        LoweredExpression expression,
        ExecutionContext context,
        ILoweredExpressionExecution execution)
    {
        switch (expression)
        {
            case LoweredIdentifierExpression identifier:
                return ResolveIdentifier(identifier.Identifier, context);
            case LoweredStringLiteralExpression text:
                return ValidateLoweredString(CreateString(text.Literal.Value, context, text.Span), context, text.Span);
            case LoweredBytesLiteralExpression bytes:
                return CreateBytes(bytes.Literal.Value.ToArray(), context, bytes.Span);
            case LoweredIntegerLiteralExpression integer:
                return ParseInteger(integer.Literal);
            case LoweredFloatLiteralExpression floating:
                return ParseFloat(floating.Literal);
            case LoweredBooleanLiteralExpression boolean:
                return boolean.Literal.Value;
            case LoweredNoneLiteralExpression:
                return PyNone.Instance;
            case LoweredFormattedStringExpression formatted:
                return await execution.EvaluateFormattedAsync(formatted, context).ConfigureAwait(false);
            case LoweredParenthesizedExpression parenthesized:
                return await execution.EvaluateExpressionAsync(parenthesized.Inner, context).ConfigureAwait(false);
            case LoweredListLiteralExpression list:
                return await execution.EvaluateListLiteralAsync(list, context).ConfigureAwait(false);
            case LoweredListComprehensionExpression comprehension:
                return await execution.EvaluateListComprehensionAsync(comprehension, context).ConfigureAwait(false);
            case LoweredGeneratorExpression generator:
            {
                // Acquire the outermost iterable eagerly, exactly as the synchronous
                // construction site does; user __iter__ still resolves synchronously
                // here (async-capable resolution belongs to a separate change).
                var outer = await execution.EvaluateExpressionAsync(generator.Clauses[0].Iterable, context).ConfigureAwait(false);
                return new PyGeneratorExpression(
                    generator.Clauses,
                    generator.ItemExpression,
                    context,
                    generator.Span,
                    LythonRuntime.ToSequence(outer, generator.Clauses[0].Iterable.Span, context));
            }
            case LoweredTupleLiteralExpression tuple:
                return await execution.EvaluateTupleLiteralAsync(tuple, context).ConfigureAwait(false);
            case LoweredSetLiteralExpression set:
                return await execution.EvaluateSetLiteralAsync(set, context).ConfigureAwait(false);
            case LoweredSetComprehensionExpression comprehension:
                return await execution.EvaluateSetComprehensionAsync(comprehension, context).ConfigureAwait(false);
            case LoweredDictLiteralExpression dictionary:
                return await execution.EvaluateDictionaryLiteralAsync(dictionary, context).ConfigureAwait(false);
            case LoweredDictComprehensionExpression comprehension:
                return await execution.EvaluateDictionaryComprehensionAsync(comprehension, context).ConfigureAwait(false);
            case LoweredMemberExpression member:
                return await execution.ResolveMemberAsync(member, context).ConfigureAwait(false);
            case LoweredCallExpression call:
                return await execution.InvokeCallAsync(call, context).ConfigureAwait(false);
            case LoweredSubscriptExpression subscript:
                return await execution.EvaluateSubscriptAsync(subscript, context).ConfigureAwait(false);
            case LoweredSliceExpression slice:
                return await execution.EvaluateSliceAsync(slice, context).ConfigureAwait(false);
            case LoweredBinaryExpression binary:
                return await execution.EvaluateBinaryAsync(binary, context).ConfigureAwait(false);
            case LoweredChainedComparisonExpression chained:
                return await execution.EvaluateChainedComparisonAsync(chained, context).ConfigureAwait(false);
            case LoweredUnaryExpression unary:
                return await execution.EvaluateUnaryAsync(unary, context).ConfigureAwait(false);
            case LoweredConditionalExpression conditional:
                var condition = await execution.EvaluateExpressionAsync(conditional.Condition, context).ConfigureAwait(false);
                return await execution.IsTruthyAsync(condition, context, conditional.Condition.Span).ConfigureAwait(false)
                    ? await execution.EvaluateExpressionAsync(conditional.Consequent, context).ConfigureAwait(false)
                    : await execution.EvaluateExpressionAsync(conditional.Alternative, context).ConfigureAwait(false);
            case LoweredAssignmentExpression assignment:
                return await execution.EvaluateAssignmentAsync(assignment, context).ConfigureAwait(false);
            case LoweredLambdaExpression lambda:
                return await execution.CreateLambdaAsync(lambda, context).ConfigureAwait(false);
            case LoweredOtherExpression other:
                throw new InvalidOperationException($"Generic lowered expression fallback reached for supported execution: {other.Expression.GetType().Name}");
            default:
                throw new InvalidOperationException($"Unknown lowered expression type: {expression.GetType().Name}");
        }
    }

    private sealed class LoweredFormattedStringBuilder
    {
        private readonly ExecutionContext _context;
        private readonly LythonSourceSpan _span;
        private readonly GovernedByteBuilder _builder;

        public LoweredFormattedStringBuilder(ExecutionContext context, LythonSourceSpan span)
        {
            _context = context;
            _span = span;
            _builder = new GovernedByteBuilder(context.MemoryGovernor, span);
        }

        public void AppendText(string text) => _builder.AppendString(text);

        public void AppendValue(object value, char? conversion, string? formatSpecifier)
            => _builder.Append(FormatInterpolatedStringPart(value, conversion, formatSpecifier, _context, _span));

        public PyString Complete()
        {
            var value = _builder.ToPyStringAndRelease();
            _context.ObserveString(value, _span);
            return value;
        }
    }

    private static async ValueTask<PyString> EvaluateLoweredFormattedStringPartsCoreAsync(
        IReadOnlyList<LoweredFormattedStringPart> parts,
        ExecutionContext context,
        LythonSourceSpan span,
        LoweredExpressionEvaluator evaluateExpression)
    {
        var builder = new LoweredFormattedStringBuilder(context, span);
        foreach (var part in parts)
        {
            switch (part)
            {
                case LoweredFormattedStringTextPart text:
                    builder.AppendText(text.Text);
                    break;
                case LoweredFormattedStringExpressionPart expression:
                    var formatSpecifier = expression.FormatSpecifierParts is null
                        ? expression.FormatSpecifier
                        : (await EvaluateLoweredFormattedStringPartsCoreAsync(
                            expression.FormatSpecifierParts,
                            context,
                            span,
                            evaluateExpression).ConfigureAwait(false)).AsString();
                    builder.AppendValue(
                        await evaluateExpression(expression.Expression).ConfigureAwait(false),
                        expression.Conversion,
                        formatSpecifier);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown lowered formatted string part: {part.GetType().Name}");
            }
        }

        return builder.Complete();
    }

    private static async ValueTask ExecuteTryStatementCoreAsync(
        LoweredTryStatement statement,
        ExecutionContext context,
        LoweredStatementBlockExecutor executeStatements)
    {
        ControlSignal? pendingControl = null;
        ReturnSignal? pendingReturn = null;
        LythonRuntimeException? pendingException = null;

        try
        {
            pendingControl = await executeStatements(statement.TryBody, context).ConfigureAwait(false);
            if (pendingControl is null && statement.ElseBody is not null)
            {
                pendingControl = await executeStatements(statement.ElseBody, context).ConfigureAwait(false);
            }
        }
        catch (ReturnSignal signal)
        {
            pendingReturn = signal;
        }
        catch (LythonRuntimeException ex)
        {
            if (statement.ExceptBody is not null &&
                MatchesCaughtException(statement.Syntax.ExceptionTypeNames, ex, context, statement.Span))
            {
                var exceptContext = new ExecutionContext(context);
                var pyException = CreatePythonExceptionInstance(ex);
                if (statement.Syntax.ExceptionVariableName is not null)
                {
                    StoreName(statement.Syntax.ExceptionVariableName, pyException, exceptContext, statement.Span);
                }

                var previousException = context.Services.SetCurrentException(pyException);
                try
                {
                    pendingControl = await executeStatements(statement.ExceptBody, exceptContext).ConfigureAwait(false);
                }
                finally
                {
                    context.Services.SetCurrentException(previousException);
                }
            }
            else
            {
                pendingException = ex;
            }
        }
        finally
        {
            if (statement.FinallyBody is not null)
            {
                try
                {
                    var finalSignal = await executeStatements(statement.FinallyBody, context).ConfigureAwait(false);
                    if (finalSignal is not null)
                    {
                        // Python's finally suite wins over every pending exit from try/except.
                        pendingControl = finalSignal;
                        pendingReturn = null;
                        pendingException = null;
                    }
                }
                catch (ReturnSignal signal)
                {
                    pendingReturn = signal;
                    pendingControl = null;
                    pendingException = null;
                }
                catch (LythonRuntimeException ex)
                {
                    pendingException = ex;
                    pendingControl = null;
                    pendingReturn = null;
                }
            }
        }

        if (pendingException is not null)
        {
            throw pendingException;
        }

        if (pendingReturn is not null)
        {
            throw pendingReturn;
        }

        if (pendingControl is not null)
        {
            throw pendingControl;
        }
    }

    private static PyFunction CreateLoweredFunction(
        LoweredFunctionDefinitionStatement functionDefinition,
        ExecutionContext context,
        Dictionary<string, object> defaults)
        => new(
            functionDefinition.Syntax.Name,
            functionDefinition.Parameters,
            functionDefinition.Body,
            context.FunctionClosureContext,
            defaults,
            ScopeDirectiveFactsCollector.ForFunction(functionDefinition.Syntax));

    private static PyType CreateLoweredClassType(
        LoweredClassDefinitionStatement classDefinition,
        IReadOnlyList<PyType> resolvedBases,
        ExecutionContext classContext,
        ExecutionContext definingContext)
    {
        static void StoreAnnotations(
            ClassDefinitionStatementSyntax syntax,
            Dictionary<string, object> members,
            ExecutionContext context)
        {
            PyDict? annotations = null;
            foreach (var statement in syntax.Body.OfType<AnnotatedAssignmentStatementSyntax>())
            {
                annotations ??= new PyDict(context.MemoryGovernor, syntax.Span);
                annotations.SetItem(PyString.FromString(statement.Name), PyDataclass.CreateAnnotationValue(statement.Annotation));
            }

            if (annotations is not null)
            {
                members["__annotations__"] = annotations;
            }
        }

        StoreAnnotations(classDefinition.Syntax, classContext.Variables, classContext);

        PyType type;
        try
        {
            type = new PyType(
                classDefinition.Syntax.Name,
                resolvedBases,
                new Dictionary<string, object>(classContext.Variables, StringComparer.Ordinal));
        }
        catch (InvalidOperationException ex)
        {
            throw new LythonRuntimeException("TypeError", ex.Message, classDefinition.Span);
        }

        if (definingContext.TryGetBuiltinType("type", out var metaType))
        {
            type.SetMetaType(metaType);
        }

        PyDataclass.Apply(type, classDefinition.Syntax, classContext.Variables, classContext, classDefinition.Span);
        type.InitializeClassMembers(definingContext, classDefinition.Span);
        return type;
    }

    private static PyString ValidateLoweredString(PyString text, ExecutionContext context, LythonSourceSpan span)
    {
        context.ObserveString(text, span);
        return text;
    }

    private static T ValidateLoweredCollection<T>(T collection, ExecutionContext context, LythonSourceSpan span)
        where T : IReadOnlyCollection<object>
    {
        context.ObserveCollectionCount(collection.Count, span);
        return collection;
    }

    private static object ReadLoweredSubscript(
        object target,
        object index,
        LythonSourceSpan span,
        ExecutionContext context)
    {
        if (target is PyDefaultDict defaultDict)
        {
            return defaultDict.GetOrCreate(ValidateDictionaryKey(index, span), context, span);
        }

        return PyIndexing.ReadIndex(target, index, span);
    }

    private static object ResolveLoweredMemberValue(
        LoweredMemberExpression member,
        object target,
        ExecutionContext context)
    {
        if (context.State.TryReadRuntimeMemberCache(member, target, out var value))
        {
            return value;
        }

        if (!TryResolveRuntimeMember(target, member.Member.MemberName, context, member.Span, out value))
        {
            throw PyMemberAccess.CreateMissingMemberError(target, member.Member.MemberName, member.Span);
        }

        if (CanCacheRuntimeMemberTarget(target))
        {
            context.State.WriteRuntimeMemberCache(member, target, value);
        }

        return value;
    }

    private static object EvaluateLoweredBinaryOperator(
        LoweredBinaryExpression binary,
        object left,
        object right,
        ExecutionContext context)
        => EvaluateBinaryOperator(binary.Binary.Operator, left, right, context, binary.Span);

    private static object EvaluateLoweredUnaryOperator(
        LoweredUnaryExpression unary,
        object operand,
        ExecutionContext context)
        => EvaluateUnaryOperator(unary.Unary.Operator, operand, context, unary.Span);

    private static void ThrowLoweredRaisedValue(object raised, LythonSourceSpan span)
    {
        if (raised is not PyException instance)
        {
            throw RuntimeErrors.RaiseExpectsException(span);
        }

        throw new LythonRuntimeException(instance.Identity, instance.Message, span, null, instance.Value);
    }

    private static LoweredFunctionParameter[] LowerLambdaParameters(LoweredLambdaExpression lambda)
        => lambda.Lambda.Parameters
            .Select(parameter => new LoweredFunctionParameter(
                parameter.Name,
                parameter.Kind,
                parameter.Annotation is null ? null : LoweredScript.LowerStandaloneExpression(parameter.Annotation),
                parameter.DefaultValue is null ? null : LoweredScript.LowerStandaloneExpression(parameter.DefaultValue)))
            .ToArray();

    private static object StoreLoweredAssignmentResult(
        LoweredAssignmentExpression assignment,
        object value,
        ExecutionContext context)
    {
        StoreName(assignment.Assignment.Name, value, context, assignment.Span);
        return value;
    }

    private static void ValidateClassKeywordArguments(CallArgumentValue[] arguments, LythonSourceSpan span)
    {
        foreach (var argument in arguments)
        {
            if (string.Equals(argument.KeywordName, "metaclass", StringComparison.Ordinal))
            {
                throw new LythonRuntimeException(
                    "TypeError",
                    "class(..., metaclass=...) is not supported by Lython.",
                    span);
            }
        }
    }
}

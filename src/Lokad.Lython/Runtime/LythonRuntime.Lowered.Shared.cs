using Lokad.Lython.Frontend;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;
using System.Numerics;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // String and bytes literals evaluate to shared compile-time constants like
    // the executable engine's interned constants: rebuilding (and recharging)
    // them per evaluation made every loop-carried literal sticky in
    // tree-walked runs, where expressions evaluate directly instead of loading
    // constants. Entries key on the lowered node, bounding them by program
    // text; values stay ungoverned exactly like the executable constants, and
    // length limits still apply per evaluation through ValidateLoweredString.
    private static readonly ConditionalWeakTable<LoweredExpression, object> SharedLiteralCache = new();

    private static PyString SharedStringLiteral(LoweredStringLiteralExpression text)
        => (PyString)SharedLiteralCache.GetValue(
            text,
            static node => PyString.FromString(((LoweredStringLiteralExpression)node).Literal.Value));

    private static PyBytes SharedBytesLiteral(LoweredBytesLiteralExpression bytes)
        => (PyBytes)SharedLiteralCache.GetValue(
            bytes,
            static node => new PyBytes(((LoweredBytesLiteralExpression)node).Literal.Value.ToArray()));

    // Heap integer literals evaluate to shared compile-time magnitudes like
    // interned strings: the parsed value is bounded by program text and
    // immutable, so rebuilding (and recharging) it per evaluation made every
    // loop-carried big literal sticky in tree-walked runs. Entries key on the
    // lowered node; values unbox into fresh boxes per evaluation, so identity
    // stays non-aliased and only magnitudes are shared.
    private static BigInteger SharedIntegerMagnitude(LoweredIntegerLiteralExpression integer)
        => (BigInteger)SharedLiteralCache.GetValue(
            integer,
            static node => ParseInteger(((LoweredIntegerLiteralExpression)node).Literal));

    private delegate ValueTask<LoweredBlockFlow> LoweredStatementBlockExecutor(
        IReadOnlyList<LoweredStatement> statements,
        ExecutionContext context);

    private delegate ValueTask<object> LoweredExpressionEvaluator(LoweredExpression expression);

    private static async ValueTask<LoweredBlockFlow> DispatchLoweredStatementAsync(
        LoweredStatement statement,
        ExecutionContext context,
        ILoweredStatementExecution execution)
    {
        switch (statement)
        {
            case LoweredImportStatement importStatement:
                await execution.ExecuteImportAsync(importStatement, context).ConfigureAwait(false);
                return default;
            case LoweredScopeDirectiveStatement:
                return default;
            case LoweredFunctionDefinitionStatement functionDefinition:
                await execution.ExecuteFunctionDefinitionAsync(functionDefinition, context).ConfigureAwait(false);
                return default;
            case LoweredClassDefinitionStatement classDefinition:
                await execution.ExecuteClassDefinitionAsync(classDefinition, context).ConfigureAwait(false);
                return default;
            case LoweredAssignmentStatement assignment:
                await execution.ExecuteAssignmentAsync(assignment, context).ConfigureAwait(false);
                return default;
            case LoweredExpressionStatement expression:
                _ = await execution.EvaluateExpressionAsync(expression.Expression, context).ConfigureAwait(false);
                return default;
            case LoweredIfStatement ifStatement:
                await execution.ExecuteIfAsync(ifStatement, context).ConfigureAwait(false);
                return default;
            case LoweredForStatement forStatement:
                await execution.ExecuteForAsync(forStatement, context).ConfigureAwait(false);
                return default;
            case LoweredWhileStatement whileStatement:
                await execution.ExecuteWhileAsync(whileStatement, context).ConfigureAwait(false);
                return default;
            case LoweredMatchStatement matchStatement:
                await execution.ExecuteMatchAsync(matchStatement, context).ConfigureAwait(false);
                return default;
            case LoweredWithStatement withStatement:
                await execution.ExecuteWithAsync(withStatement, context).ConfigureAwait(false);
                return default;
            case LoweredTryStatement tryStatement:
                await execution.ExecuteTryAsync(tryStatement, context).ConfigureAwait(false);
                return default;
            case LoweredPassStatement:
                return default;
            case LoweredBreakStatement:
                throw new BreakSignal();
            case LoweredContinueStatement:
                throw new ContinueSignal();
            case LoweredAssertStatement assertStatement:
                await execution.ExecuteAssertAsync(assertStatement, context).ConfigureAwait(false);
                return default;
            case LoweredDeleteStatement deleteStatement:
                await execution.ExecuteDeleteAsync(deleteStatement, context).ConfigureAwait(false);
                return default;
            case LoweredReturnStatement returnStatement:
                if (returnStatement.Expression is null)
                {
                    return new LoweredBlockFlow(null, new ReturnSignal(PyNone.Instance));
                }

                var returnValue = await execution.EvaluateExpressionAsync(returnStatement.Expression, context).ConfigureAwait(false);
                return new LoweredBlockFlow(null, new ReturnSignal(RuntimeValue(returnValue)));
            case LoweredRaiseStatement raiseStatement:
                await execution.ExecuteRaiseAsync(raiseStatement, context).ConfigureAwait(false);
                return default;
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
                return ValidateLoweredString(SharedStringLiteral(text), context, text.Span);
            case LoweredBytesLiteralExpression bytes:
                return SharedBytesLiteral(bytes);
            case LoweredIntegerLiteralExpression integer:
                return SharedIntegerMagnitude(integer);
            case LoweredFloatLiteralExpression floating:
                return ParseFloat(floating.Literal);
            case LoweredBooleanLiteralExpression boolean:
                return boolean.Literal.Value;
            case LoweredNoneLiteralExpression:
                return PyNone.Instance;
            case LoweredEllipsisLiteralExpression:
                return PyEllipsis.Instance;
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
                var producedAsync = new PyGeneratorExpression(
                    generator.Clauses,
                    generator.ItemExpression,
                    context,
                    generator.Span,
                    LythonRuntime.ToSequence(outer, generator.Clauses[0].Iterable.Span, context));
                context.Services.State.CallTemporaries.TrackFreshMutable(producedAsync, PyIteratorBase.IteratorValueBytes);
                return producedAsync;
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
            => _builder.Append(PyRendering.OwnJoinItem(FormatInterpolatedStringPart(value, conversion, formatSpecifier, _context, _span), _context));

        public PyString Complete()
        {
            var value = _builder.ToPyStringAndRelease();
            _context.ObserveString(value, _span);
            // M05: same ownership as the executable f-string display: dropped
            // results reclaim on sweep while retained results stay charged.
            _context.Services.State.CallTemporaries.TrackFreshString(value, _span);
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

    private static async ValueTask<LoweredBlockFlow> ExecuteTryStatementCoreAsync(
        LoweredTryStatement statement,
        ExecutionContext context,
        LoweredStatementBlockExecutor executeStatements)
    {
        ControlSignal? pendingControl = null;
        ReturnSignal? pendingReturn = null;
        LythonRuntimeException? pendingException = null;

        try
        {
            var tryFlow = await executeStatements(statement.TryBody, context).ConfigureAwait(false);
            pendingControl = tryFlow.Control;
            pendingReturn = tryFlow.Return;
            if (pendingControl is null && pendingReturn is null && statement.ElseBody is not null)
            {
                var elseFlow = await executeStatements(statement.ElseBody, context).ConfigureAwait(false);
                pendingControl = elseFlow.Control;
                pendingReturn = elseFlow.Return;
            }
        }
        catch (ReturnSignal signal)
        {
            pendingReturn = signal;
        }
        catch (LythonRuntimeException ex)
        {
            LoweredExceptClause? matchedClause = null;
            foreach (var candidate in statement.ExceptClauses)
            {
                if (MatchesCaughtException(candidate.Syntax.ExceptionTypeNames, ex, context, statement.Span))
                {
                    matchedClause = candidate;
                    break;
                }
            }

            if (matchedClause is not null)
            {
                // Handler suites share the enclosing scope like CPython: only
                // the `as` variable is suite-local (deleted below). A child
                // context would hide every handler assignment from the code
                // after the statement, which the executable engine never does.
                var pyException = CreatePythonExceptionInstance(ex);
                if (matchedClause.Syntax.ExceptionVariableName is not null)
                {
                    ChargeBoundException(context.MemoryGovernor, statement.Span);
                    StoreName(matchedClause.Syntax.ExceptionVariableName, pyException, context, statement.Span);
                }

                var previousException = context.Services.SetCurrentException(pyException);
                var handlerVariableName = matchedClause.Syntax.ExceptionVariableName;
                try
                {
                    var handlerFlow = await executeStatements(matchedClause.Body, context).ConfigureAwait(false);
                    pendingControl = handlerFlow.Control;
                    pendingReturn = handlerFlow.Return;
                }
                finally
                {
                    if (handlerVariableName is not null)
                    {
                        _ = DeleteName(handlerVariableName, context, statement.Span);
                    }

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
                // A finally suite runs with the pending exception active like
                // CPython, so raises inside it chain the in-flight exception.
                var inFlightException = pendingException is null
                    ? null
                    : CreatePythonExceptionInstance(pendingException);
                var previousActiveException = inFlightException is null
                    ? null
                    : context.Services.SetCurrentException(inFlightException);
                try
                {
                    try
                    {
                        var finalFlow = await executeStatements(statement.FinallyBody, context).ConfigureAwait(false);
                        if (finalFlow.Control is not null || finalFlow.Return is not null)
                        {
                            // Python's finally suite wins over every pending exit from try/except.
                            pendingControl = finalFlow.Control;
                            pendingReturn = finalFlow.Return;
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
                finally
                {
                    if (inFlightException is not null)
                    {
                        context.Services.SetCurrentException(previousActiveException);
                    }
                }
            }
        }

        if (pendingException is not null)
        {
            throw pendingException;
        }

        return new LoweredBlockFlow(pendingControl, pendingReturn);
    }

    private static PyFunction CreateLoweredFunction(
        LoweredFunctionDefinitionStatement functionDefinition,
        ExecutionContext context,
        Dictionary<string, object> defaults)
    {
        var function = new PyFunction(
            functionDefinition.Syntax.Name,
            functionDefinition.Parameters,
            functionDefinition.Body,
            context.FunctionClosureContext,
            defaults,
            ScopeDirectiveFactsCollector.ForFunction(functionDefinition.Syntax));
        ChargeFunctionValue(context, functionDefinition.Span);
        ChargeDefaultArguments(functionDefinition.Parameters.Count(static p => p.DefaultValue is not null), context.MemoryGovernor, functionDefinition.Span);
        var closureRetentionBytes = ChargeClosureRetention(context.FunctionClosureContext, context.MemoryGovernor, functionDefinition.Span);
        TrackFunctionValue(function, functionDefinition.Parameters.Count(static p => p.DefaultValue is not null), closureRetentionBytes, context, functionDefinition.Span);
        PyFunctionBase.CaptureFunctionDocstring(function, functionDefinition.Body, context, functionDefinition.Span);
        return function;
    }

    private static PyType CreateLoweredClassType(
        LoweredClassDefinitionStatement classDefinition,
        IReadOnlyList<PyType> resolvedBases,
        ExecutionContext classContext,
        ExecutionContext definingContext)
    {
        // Class docstrings follow the function rule; an explicit __doc__ assignment
        // in the body wins since it already ran. The entry rides the member charge.
        if (PyFunctionBase.LeadingDocstring(classDefinition.Body) is { } docText &&
            !classContext.Variables.ContainsKey("__doc__"))
        {
            classContext.Variables["__doc__"] = PyString.FromString(docText, definingContext.MemoryGovernor, classDefinition.Span);
        }

        static void StoreAnnotations(
            ClassDefinitionStatementSyntax syntax,
            Dictionary<string, object> members,
            ExecutionContext context)
        {
            PyDict? annotations = null;
            foreach (var statement in syntax.Body.OfType<AnnotatedAssignmentStatementSyntax>())
            {
                if (statement.Target is not NameAssignmentTargetSyntax name)
                {
                    continue;
                }

                annotations ??= new PyDict(context.MemoryGovernor, syntax.Span);
                annotations.SetItem(PyString.FromString(name.Name), PyDataclass.CreateAnnotationValue(statement.Annotation));
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
                new Dictionary<string, object>(classContext.Variables, StringComparer.Ordinal),
                definingContext.MemoryGovernor,
                classDefinition.Span);
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
        ChargeClassTypeValue(classContext.Variables.Count, definingContext.MemoryGovernor, classDefinition.Span);
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
        // Display literals are freshly built: dropped ones release through the
        // pool once collected, and a denied registry charge refunds the snapshot.
        switch (collection)
        {
            case PyList list when list.OwnerMemoryGovernor is not null:
                context.Services.State.CallTemporaries.TrackFreshMutable(list, list.CommittedStorageBytes);
                break;
            case PyTuple tuple when tuple.OwnerMemoryGovernor is not null:
                context.Services.State.CallTemporaries.TrackFreshMutable(tuple, tuple.CommittedStorageBytes);
                break;
        }

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

        return PyIndexing.ReadIndex(target, index, span, context);
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
            throw PyMemberAccess.CreateMissingMemberError(target, member.Member.MemberName, member.Span, context);
        }

        if (CanCacheRuntimeMemberValue(target, value))
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

    private static PyException? CoerceRaiseCause(object value, LythonSourceSpan span, ExecutionContext context)
    {
        if (value is PyNone)
        {
            return null;
        }

        if (value is PyException instance)
        {
            return instance;
        }

        if (value is ExceptionTypeValue typeValue &&
            typeValue.Invoke([], span, context) is PyException constructed)
        {
            return constructed;
        }

        throw new LythonRuntimeException("TypeError", "exception causes must derive from BaseException", span);
    }

    // An explicit raise chains the active handler exception as its context
    // like CPython; re-raising the active instance itself leaves the link
    // unchanged instead of building a self-cycle.
    private static void AttachImplicitRaiseChain(
        LythonRuntimeException thrown,
        PyException instance,
        ExecutionContext context)
    {
        var current = context.Services.CurrentException;
        thrown.PythonContext = ReferenceEquals(instance, current) ? null : current;
        thrown.PythonExplicitArgs = instance.ArgsOverride ?? instance.ExplicitArgs;
    }

    private static void ThrowReraisedException(LythonSourceSpan span, ExecutionContext context)
    {
        var current = context.Services.CurrentException;
        if (current is null)
        {
            throw RuntimeErrors.Runtime("No active exception to reraise", span);
        }

        // A bare raise continues the active chain instead of starting a new
        // one, so cause, context and suppression travel with the value.
        throw new LythonRuntimeException(current.Identity, current.Message, span, null, current.Value)
        {
            PythonCause = current.Cause,
            PythonContext = current.Context,
            SuppressPythonContext = current.SuppressContext,
        };
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

using System.Text.RegularExpressions;
using Lokad.Lython.Frontend;
using System.Numerics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static object RuntimeValue(object? value) => value ?? PyNone.Instance;

    internal static PyString ReadGovernedHostText(string path, ExecutionContext context, LythonSourceSpan? span)
    {
        context.RegisterHostCall(span);
        var stat = context.HostStat(path, span);
        if (stat.Exists && stat.IsFile && context.Limits.MaxHostReadBytes is { } maxHostReadBytes &&
            stat.Size > new BigInteger(maxHostReadBytes))
        {
            throw RuntimeErrors.Runtime($"host text read exceeded maximum bytes ({maxHostReadBytes})", span);
        }

        context.RegisterHostCall(span);
        var utf8 = context.ReadTextUtf8(path, span);
        if (context.Limits.MaxHostReadBytes is { } maxReadBytes && utf8.Length > maxReadBytes)
        {
            throw RuntimeErrors.Runtime($"host text read exceeded maximum bytes ({maxReadBytes})", span);
        }

        var text = CreateUtf8String(utf8, context, span);
        text = PyStringOps.NormalizeNewlines(text);
        if (text.OwnerMemoryGovernor is null && text.Utf8Bytes.Length > 0)
        {
            text = CreateString(text.AsString(), context, span);
        }
        context.ObserveString(text, span);
        return text;
    }

    internal static async ValueTask<PyString> ReadGovernedHostTextAsync(string path, ExecutionContext context, LythonSourceSpan? span)
    {
        context.RegisterHostCall(span);
        var stat = await context.HostStatAsync(path, span).ConfigureAwait(false);
        if (stat.Exists && stat.IsFile && context.Limits.MaxHostReadBytes is { } maxHostReadBytes &&
            stat.Size > new BigInteger(maxHostReadBytes))
        {
            throw RuntimeErrors.Runtime($"host text read exceeded maximum bytes ({maxHostReadBytes})", span);
        }

        context.RegisterHostCall(span);
        var utf8 = await context.ReadTextUtf8Async(path, span).ConfigureAwait(false);
        if (context.Limits.MaxHostReadBytes is { } maxReadBytes && utf8.Length > maxReadBytes)
        {
            throw RuntimeErrors.Runtime($"host text read exceeded maximum bytes ({maxReadBytes})", span);
        }

        var text = CreateUtf8String(utf8, context, span);
        text = PyStringOps.NormalizeNewlines(text);
        if (text.OwnerMemoryGovernor is null && text.Utf8Bytes.Length > 0)
        {
            text = CreateString(text.AsString(), context, span);
        }

        context.ObserveString(text, span);
        return text;
    }

    internal static ReadOnlyMemory<byte> ReadGovernedHostBytes(string path, ExecutionContext context, LythonSourceSpan? span)
    {
        context.RegisterHostCall(span);
        var stat = context.HostStat(path, span);
        if (stat.Exists && stat.IsFile && context.Limits.MaxHostReadBytes is { } maxHostReadBytes &&
            stat.Size > new BigInteger(maxHostReadBytes))
        {
            throw RuntimeErrors.Runtime($"host binary read exceeded maximum bytes ({maxHostReadBytes})", span);
        }

        context.RegisterHostCall(span);
        var payload = context.ReadHostBytes(path, span);
        if (context.Limits.MaxHostReadBytes is { } maxReadBytes && payload.Length > maxReadBytes)
        {
            throw RuntimeErrors.Runtime($"host binary read exceeded maximum bytes ({maxReadBytes})", span);
        }

        context.MemoryGovernor.EnsureCanReserve(PyBytes.EstimateApproximateBytes(payload.Length), span);
        return payload;
    }

    internal static async ValueTask<ReadOnlyMemory<byte>> ReadGovernedHostBytesAsync(string path, ExecutionContext context, LythonSourceSpan? span)
    {
        context.RegisterHostCall(span);
        var stat = await context.HostStatAsync(path, span).ConfigureAwait(false);
        if (stat.Exists && stat.IsFile && context.Limits.MaxHostReadBytes is { } maxHostReadBytes &&
            stat.Size > new BigInteger(maxHostReadBytes))
        {
            throw RuntimeErrors.Runtime($"host binary read exceeded maximum bytes ({maxHostReadBytes})", span);
        }

        context.RegisterHostCall(span);
        var payload = await context.ReadHostBytesAsync(path, span).ConfigureAwait(false);
        if (context.Limits.MaxHostReadBytes is { } maxReadBytes && payload.Length > maxReadBytes)
        {
            throw RuntimeErrors.Runtime($"host binary read exceeded maximum bytes ({maxReadBytes})", span);
        }

        context.MemoryGovernor.EnsureCanReserve(PyBytes.EstimateApproximateBytes(payload.Length), span);
        return payload;
    }

    internal static PyTuple CreateTuple(int count, Func<int, object> itemFactory, ExecutionContext context, LythonSourceSpan? span)
    {
        if (count == 0)
        {
            return PyTuple.Empty;
        }

        context.MemoryGovernor.EnsureCanReserve(PyTuple.EstimateApproximateBytes(count), span);
        var items = new object[count];
        for (var i = 0; i < count; i++)
        {
            items[i] = itemFactory(i);
        }

        return PyTuple.FromOwnedArray(items, context.MemoryGovernor, span);
    }

    internal static async ValueTask<PyTuple> CreateTupleAsync(int count, Func<int, ValueTask<object>> itemFactory, ExecutionContext context, LythonSourceSpan? span)
    {
        if (count == 0)
        {
            return PyTuple.Empty;
        }

        context.MemoryGovernor.EnsureCanReserve(PyTuple.EstimateApproximateBytes(count), span);
        var items = new object[count];
        for (var i = 0; i < count; i++)
        {
            items[i] = await itemFactory(i).ConfigureAwait(false);
        }

        return PyTuple.FromOwnedArray(items, context.MemoryGovernor, span);
    }

    internal static PyBytes CreateBytes(byte[] bytes, ExecutionContext context, LythonSourceSpan? span)
    {
        return bytes.Length == 0
            ? new PyBytes(Array.Empty<byte>())
            : new PyBytes(bytes, context.MemoryGovernor, span);
    }

    internal static PyString CreateString(string text, ExecutionContext context, LythonSourceSpan? span)
    {
        return text.Length == 0
            ? PyString.Empty
            : PyString.FromString(text, context.MemoryGovernor, span);
    }

    internal static PyString CreateUtf8String(ReadOnlyMemory<byte> utf8, ExecutionContext context, LythonSourceSpan? span)
    {
        return utf8.Length == 0
            ? PyString.Empty
            : PyString.FromUtf8(utf8, context.MemoryGovernor, span);
    }

    public LythonExecutionResult Run(
        LoweredScript script,
        ILythonHost host,
        LythonRunOptions? options)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(host);

        ExecutionContext? context = null;
        try
        {
            context = new ExecutionContext(host, options);
            var signal = ExecuteStatements(script.Statements, context);
            if (signal is BreakSignal or ContinueSignal)
            {
                throw RuntimeErrors.TopLevelLoopControl(null);
            }

            return new LythonExecutionResult(
                success: true,
                returnValue: null,
                standardOutput: CaptureStandardOutput(context),
                standardError: CaptureStandardError(context),
                exitCode: null,
                diagnostics: Array.Empty<LythonDiagnostic>(),
                failure: null);
        }
        catch (ReturnSignal signal)
        {
            try
            {
                return new LythonExecutionResult(
                    success: true,
                    returnValue: NormalizePublicValue(signal.Value, options),
                    standardOutput: context is null ? string.Empty : CaptureStandardOutput(context),
                    standardError: context is null ? string.Empty : CaptureStandardError(context),
                    exitCode: null,
                    diagnostics: Array.Empty<LythonDiagnostic>(),
                    failure: null);
            }
            catch (ProjectionException ex)
            {
                return new LythonExecutionResult(
                    success: false,
                    returnValue: null,
                    standardOutput: context is null ? string.Empty : CaptureStandardOutput(context),
                    standardError: context is null ? string.Empty : CaptureStandardError(context),
                    exitCode: null,
                    diagnostics: Array.Empty<LythonDiagnostic>(),
                    failure: new LythonRuntimeFailure("ProjectionError", ex.Message, null, Array.Empty<LythonStackFrame>(), context?.SourcePath));
            }
        }
        catch (LythonRuntimeException ex)
        {
            ex.SetSourcePathIfMissing(context?.SourcePath);
            return new LythonExecutionResult(
                success: false,
                returnValue: null,
                standardOutput: context is null ? string.Empty : CaptureStandardOutput(context),
                standardError: context is null ? string.Empty : CaptureStandardError(context),
                exitCode: GetExitCode(ex),
                diagnostics: Array.Empty<LythonDiagnostic>(),
                failure: RuntimeFailureProjection.ToPublicFailure(ex));
        }
    }

    private static string CaptureStandardOutput(ExecutionContext context)
    {
        try
        {
            return context.State.StandardOutput.ToPyString().AsString();
        }
        catch (LythonRuntimeException)
        {
            return string.Empty;
        }
    }

    private static string CaptureStandardError(ExecutionContext context)
    {
        try
        {
            return context.State.StandardError.ToPyString().AsString();
        }
        catch (LythonRuntimeException)
        {
            return string.Empty;
        }
    }

    private static int? GetExitCode(LythonRuntimeException exception)
    {
        if (!string.Equals(exception.ExceptionType, "SystemExit", StringComparison.Ordinal))
        {
            return null;
        }

        return exception.Payload switch
        {
            null => 0,
            PyNone => 0,
            BigInteger integer when integer >= int.MinValue && integer <= int.MaxValue => (int)integer,
            BigInteger => 1,
            int integer => integer,
            _ => 1
        };
    }

    internal static ControlSignal? ExecuteStatements(IReadOnlyList<StatementSyntax> statements, ExecutionContext context)
    {
        try
        {
            foreach (var statement in statements)
            {
                ExecuteStatement(statement, context);
            }

            return null;
        }
        catch (LythonRuntimeException ex)
        {
            ex.SetSourcePathIfMissing(context.SourcePath);
            throw;
        }
        catch (ControlSignal signal)
        {
            return signal;
        }
    }

    private static object EvaluateExpression(ExpressionSyntax expression, ExecutionContext context)
    {
        context.EnterInterpreterFrame(expression.Span);
        try
        {
            var value = expression switch
            {
                IdentifierExpressionSyntax identifier => ResolveIdentifier(identifier, context),
                StringLiteralExpressionSyntax literal => CreateString(literal.Value, context, literal.Span),
                IntegerLiteralExpressionSyntax integer => ParseInteger(integer),
                FloatLiteralExpressionSyntax floating => ParseFloat(floating),
                BooleanLiteralExpressionSyntax boolean => boolean.Value,
                NoneLiteralExpressionSyntax => PyNone.Instance,
                FormattedStringExpressionSyntax formatted => EvaluateFormattedString(formatted, context),
                ListLiteralExpressionSyntax list => CreateListLiteral(list, context),
                ListComprehensionExpressionSyntax listComprehension => EvaluateListComprehension(listComprehension, context),
                GeneratorExpressionSyntax generator => EvaluateGeneratorExpression(generator, context),
                DictLiteralExpressionSyntax dict => EvaluateDictLiteral(dict, context),
                SetLiteralExpressionSyntax set => EvaluateSetLiteral(set, context),
                DictComprehensionExpressionSyntax dictComprehension => EvaluateDictComprehension(dictComprehension, context),
                TupleLiteralExpressionSyntax tuple => CreateTuple(
                    tuple.Items.Count,
                    i => RuntimeValue(EvaluateExpression(tuple.Items[i], context)),
                    context,
                    tuple.Span),
                ParenthesizedExpressionSyntax parenthesized => EvaluateExpression(parenthesized.Inner, context),
                BytesLiteralExpressionSyntax bytes => CreateBytes(bytes.Value.ToArray(), context, bytes.Span),
                MemberExpressionSyntax member => ResolveMember(member, context),
                CallExpressionSyntax call => InvokeCall(call, context),
                SubscriptExpressionSyntax subscript => EvaluateSubscript(subscript, context),
                SliceExpressionSyntax slice => EvaluateSlice(slice, context),
                BinaryExpressionSyntax binary => EvaluateBinary(binary, context),
                ChainedComparisonExpressionSyntax chainedComparison => EvaluateChainedComparison(chainedComparison, context),
                UnaryExpressionSyntax unary => EvaluateUnary(unary, context),
                ConditionalExpressionSyntax conditional => EvaluateConditional(conditional, context),
                AssignmentExpressionSyntax assignment => EvaluateAssignmentExpression(assignment, context),
                LambdaExpressionSyntax lambda => CreateLambda(lambda, context),
                _ => throw new InvalidOperationException($"Unknown expression type: {expression.GetType().Name}")
            };

            context.ObserveValue(value, expression.Span);
            return value;
        }
        catch (LythonRuntimeException ex)
        {
            ex.SetSourcePathIfMissing(context.SourcePath);
            throw;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or RegexParseException)
        {
            var runtime = new LythonRuntimeException("RuntimeError", ex.Message, expression.Span);
            runtime.SetSourcePathIfMissing(context.SourcePath);
            throw runtime;
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

    private static PyList CreateListLiteral(ListLiteralExpressionSyntax list, ExecutionContext context)
    {
        context.MemoryGovernor.EnsureCanReserve(EstimateObjectArrayBytes(list.Items.Count), list.Span);
        var items = new object[list.Items.Count];
        for (var i = 0; i < list.Items.Count; i++)
        {
            items[i] = RuntimeValue(EvaluateExpression(list.Items[i], context));
        }

        return new PyList(items, context.MemoryGovernor, list.Span);
    }

    private static object EvaluateBinary(BinaryExpressionSyntax binary, ExecutionContext context)
    {
        if (binary.Operator == BinaryOperatorSyntax.Or)
        {
            var leftValue = EvaluateExpression(binary.Left, context);
            return IsTruthy(leftValue)
                ? leftValue!
                : EvaluateExpression(binary.Right, context)!;
        }

        if (binary.Operator == BinaryOperatorSyntax.And)
        {
            var leftValue = EvaluateExpression(binary.Left, context);
            return !IsTruthy(leftValue)
                ? leftValue!
                : EvaluateExpression(binary.Right, context)!;
        }

        var left = EvaluateExpression(binary.Left, context);
        var right = EvaluateExpression(binary.Right, context);

        return binary.Operator switch
        {
            BinaryOperatorSyntax.Add => EvaluateAdd(left, right, context, binary.Span),
            BinaryOperatorSyntax.Subtract => EvaluateSubtract(left, right, binary.Span),
            BinaryOperatorSyntax.Multiply => EvaluateMultiply(left, right, context, binary.Span),
            BinaryOperatorSyntax.Divide => EvaluateDivide(left, right, binary.Span),
            BinaryOperatorSyntax.FloorDivide => EvaluateFloorDivide(left, right, binary.Span),
            BinaryOperatorSyntax.Modulo => EvaluateModulo(left, right, binary.Span),
            BinaryOperatorSyntax.Power => EvaluatePower(left, right, context, binary.Span),
            BinaryOperatorSyntax.BitwiseOr => EvaluateBitwiseOr(left, right, binary.Span),
            BinaryOperatorSyntax.BitwiseXor => EvaluateBitwiseXor(left, right, binary.Span),
            BinaryOperatorSyntax.BitwiseAnd => EvaluateBitwiseAnd(left, right, binary.Span),
            BinaryOperatorSyntax.LeftShift => EvaluateLeftShift(left, right, context, binary.Span),
            BinaryOperatorSyntax.RightShift => EvaluateRightShift(left, right, binary.Span),
            BinaryOperatorSyntax.Less => Compare(left, right, binary.Span) < 0,
            BinaryOperatorSyntax.LessEqual => Compare(left, right, binary.Span) <= 0,
            BinaryOperatorSyntax.Greater => Compare(left, right, binary.Span) > 0,
            BinaryOperatorSyntax.GreaterEqual => Compare(left, right, binary.Span) >= 0,
            BinaryOperatorSyntax.Is => ReferenceEquals(left, right),
            BinaryOperatorSyntax.IsNot => !ReferenceEquals(left, right),
            BinaryOperatorSyntax.In => Contains(right, left, binary.Span),
            BinaryOperatorSyntax.NotIn => !Contains(right, left, binary.Span),
            BinaryOperatorSyntax.Equal => AreEqual(left, right),
            BinaryOperatorSyntax.NotEqual => !AreEqual(left, right),
            _ => throw new InvalidOperationException($"Unknown binary operator: {binary.Operator}")
        };
    }

    private static void ExecuteAssertStatement(AssertStatementSyntax statement, ExecutionContext context)
    {
        if (IsTruthy(EvaluateExpression(statement.Condition, context)))
        {
            return;
        }

        var message = statement.Message is null
            ? string.Empty
            : ToInterpolatedPyString(EvaluateExpression(statement.Message, context), context).AsString();
        throw new LythonRuntimeException("AssertionError", message, statement.Span);
    }

    private static void ExecuteDeleteStatement(DeleteStatementSyntax statement, ExecutionContext context)
    {
        switch (statement.Target)
        {
            case IdentifierExpressionSyntax identifier:
                if (!DeleteName(identifier.Name, context, statement.Span))
                {
                    throw new LythonRuntimeException("NameError", $"Name '{identifier.Name}' is not defined.", statement.Span);
                }

                return;

            case SubscriptExpressionSyntax subscript:
                var target = EvaluateExpression(subscript.Target, context);
                var index = EvaluateExpression(subscript.Index, context);
                switch (target)
                {
                    case IDeletablePySubscriptableValue subscriptable:
                        subscriptable.DeleteSubscript(index, statement.Span);
                        return;

                    case IMutablePySequenceValue sequence:
                        sequence.RemoveAt(PyIndexing.NormalizeIndex(index, sequence.Count, statement.Span));
                        return;

                    case PyDict dict:
                        if (!dict.Remove(ValidateDictionaryKey(index, statement.Span)))
                        {
                            throw new LythonRuntimeException("KeyError", "Key was not found.", statement.Span);
                        }

                        return;

                    case PyDefaultDict defaultDict:
                        if (!defaultDict.Remove(ValidateDictionaryKey(index, statement.Span)))
                        {
                            throw new LythonRuntimeException("KeyError", "Key was not found.", statement.Span);
                        }

                        return;

                    case PyCounter counter:
                        _ = counter.Remove(ValidateDictionaryKey(index, statement.Span));
                        return;

                    case PyTuple:
                        throw new LythonRuntimeException("TypeError", "Tuple does not support item deletion.", statement.Span);

                    case string:
                        throw new LythonRuntimeException("TypeError", "String does not support item deletion.", statement.Span);

                    default:
                        throw new LythonRuntimeException("TypeError", "Object does not support item deletion.", statement.Span);
                }

            case SliceExpressionSyntax slice:
                ExecuteSliceDeletion(
                    EvaluateExpression(slice.Target, context),
                    slice.Start is null ? null : EvaluateExpression(slice.Start, context),
                    slice.End is null ? null : EvaluateExpression(slice.End, context),
                    slice.Step is null ? null : EvaluateExpression(slice.Step, context),
                    statement.Span);
                return;

            case MemberExpressionSyntax member:
                var memberTarget = EvaluateExpression(member.Target, context);
                if (!PyMemberAccess.TryDelete(memberTarget, member.MemberName, context, statement.Span))
                {
                    throw new LythonRuntimeException("TypeError", "Object does not support attribute deletion.", statement.Span);
                }

                return;

            default:
                throw new LythonRuntimeException("RuntimeError", "Unsupported delete target.", statement.Span);
        }
    }

    private static bool EvaluateChainedComparison(ChainedComparisonExpressionSyntax chainedComparison, ExecutionContext context)
    {
        var left = EvaluateExpression(chainedComparison.Operands[0], context);
        for (var i = 0; i < chainedComparison.Operators.Count; i++)
        {
            var right = EvaluateExpression(chainedComparison.Operands[i + 1], context);
            if (!EvaluateComparisonOperator(left, right, chainedComparison.Operators[i], chainedComparison.Span))
            {
                return false;
            }

            left = right;
        }

        return true;
    }

    private static bool EvaluateComparisonOperator(object left, object right, BinaryOperatorSyntax op, LythonSourceSpan span)
    {
        return op switch
        {
            BinaryOperatorSyntax.Less => Compare(left, right, span) < 0,
            BinaryOperatorSyntax.LessEqual => Compare(left, right, span) <= 0,
            BinaryOperatorSyntax.Greater => Compare(left, right, span) > 0,
            BinaryOperatorSyntax.GreaterEqual => Compare(left, right, span) >= 0,
            BinaryOperatorSyntax.Is => ReferenceEquals(left, right),
            BinaryOperatorSyntax.IsNot => !ReferenceEquals(left, right),
            BinaryOperatorSyntax.In => Contains(right, left, span),
            BinaryOperatorSyntax.NotIn => !Contains(right, left, span),
            BinaryOperatorSyntax.Equal => AreEqual(left, right),
            BinaryOperatorSyntax.NotEqual => !AreEqual(left, right),
            _ => throw new InvalidOperationException($"Unsupported chained comparison operator: {op}")
        };
    }

    private static object CreateLambda(LambdaExpressionSyntax lambda, ExecutionContext context)
    {
        var loweredParameters = lambda.Parameters
            .Select(parameter => new LoweredFunctionParameter(
                parameter.Name,
                parameter.Kind,
                parameter.Annotation is null ? null : LoweredScript.LowerStandaloneExpression(parameter.Annotation),
                parameter.DefaultValue is null ? null : LoweredScript.LowerStandaloneExpression(parameter.DefaultValue)))
            .ToArray();
        return new LambdaFunction(
            loweredParameters,
            LoweredScript.LowerStandaloneExpression(lambda.Body),
            context,
            BuildDefaultArgumentMap(loweredParameters, expression => EvaluateLoweredExpression(expression, context)));
    }

    private static void ExecuteAugmentedAssignment(AugmentedAssignmentStatementSyntax statement, ExecutionContext context)
    {
        var target = ResolveAugmentedAssignmentTarget(statement.Target, context);
        var right = EvaluateExpression(statement.Expression, context);
        var updated = EvaluateAugmentedAssignment(target.CurrentValue, right, statement.Operator, context, statement.Span);
        target.Store(updated);
    }

    private sealed record AugmentedAssignmentTargetReference(object CurrentValue, Action<object> Store);

    private static void ExecuteAnnotatedAssignment(AnnotatedAssignmentStatementSyntax statement, ExecutionContext context)
    {
        if (statement.Expression is not null)
        {
            StoreName(statement.Name, EvaluateExpression(statement.Expression, context), context, statement.Span);
        }
    }

    private static void ExecuteChainedAssignment(ChainedAssignmentStatementSyntax statement, ExecutionContext context)
    {
        var value = EvaluateExpression(statement.Expression, context);
        foreach (var target in statement.Targets)
        {
            AssignTarget(target, value, context);
        }
    }

    private static void ExecuteSubscriptAssignment(SubscriptAssignmentStatementSyntax statement, ExecutionContext context)
    {
        var target = EvaluateExpression(statement.Target, context);
        var index = EvaluateExpression(statement.Index, context);
        var value = EvaluateExpression(statement.Expression, context);

        switch (target)
        {
            case IMutablePySubscriptableValue subscriptable:
                subscriptable.SetSubscript(index, value, statement.Span);
                return;

            case IMutablePySequenceValue sequence:
                sequence.SetItem(PyIndexing.NormalizeIndex(index, sequence.Count, statement.Span), value);
                return;

            case PyDict dict:
                dict.AttachMemoryGovernor(context.MemoryGovernor, statement.Span);
                dict.SetItem(ValidateDictionaryKey(index, statement.Span), value);
                context.ObserveCollectionCount(dict.Count, statement.Span);
                return;

            case PyDefaultDict defaultDict:
                defaultDict.AttachMemoryGovernor(context.MemoryGovernor, statement.Span);
                defaultDict.SetItem(ValidateDictionaryKey(index, statement.Span), value);
                context.ObserveCollectionCount(defaultDict.Count, statement.Span);
                return;

            case PyCounter counter:
                counter.AttachMemoryGovernor(context.MemoryGovernor, statement.Span);
                counter.SetItem(ValidateDictionaryKey(index, statement.Span), value);
                context.ObserveCollectionCount(counter.Count, statement.Span);
                return;

            case PyTuple:
                throw new LythonRuntimeException("TypeError", "Tuple does not support item assignment.", statement.Span);

            case string:
                throw new LythonRuntimeException("TypeError", "String does not support item assignment.", statement.Span);

            default:
                throw new LythonRuntimeException("TypeError", "Object does not support item assignment.", statement.Span);
        }
    }

    private static void ExecuteSliceAssignment(SliceAssignmentStatementSyntax statement, ExecutionContext context)
    {
        var target = EvaluateExpression(statement.Target, context);
        var start = statement.Start is null ? null : EvaluateExpression(statement.Start, context);
        var end = statement.End is null ? null : EvaluateExpression(statement.End, context);
        var step = statement.Step is null ? null : EvaluateExpression(statement.Step, context);
        var value = EvaluateExpression(statement.Expression, context);
        ExecuteSliceAssignment(target, start, end, step, value, statement.Span, context);
    }

    private static void ExecuteMemberAssignment(MemberAssignmentStatementSyntax statement, ExecutionContext context)
    {
        var target = EvaluateExpression(statement.Target, context);
        var value = EvaluateExpression(statement.Expression, context);
        if (!PyMemberAccess.TryAssign(target, statement.MemberName, value, context, statement.Span))
        {
            throw new LythonRuntimeException("TypeError", "Object does not support attribute assignment.", statement.Span);
        }
    }

    private static void AssignTarget(AssignmentTargetSyntax target, object value, ExecutionContext context)
    {
        switch (target)
        {
            case NameAssignmentTargetSyntax name:
                StoreName(name.Name, value, context, name.Span);
                return;
            case UnpackingAssignmentTargetGroupSyntax unpacking:
                AssignTargets(unpacking.Targets, value, unpacking.Span, context);
                return;
            case SubscriptAssignmentTargetSyntax subscript:
                AssignSubscriptTarget(subscript, value, context);
                return;
            case SliceAssignmentTargetSyntax slice:
                AssignSliceTarget(slice, value, context);
                return;
            case MemberAssignmentTargetSyntax member:
                AssignMemberTarget(member, value, context);
                return;
            default:
                throw new InvalidOperationException($"Unsupported assignment target syntax: {target.GetType().Name}");
        }
    }

    private static void AssignSubscriptTarget(SubscriptAssignmentTargetSyntax subscript, object value, ExecutionContext context)
    {
        var target = EvaluateExpression(subscript.Target, context);
        var index = EvaluateExpression(subscript.Index, context);
        SetSubscriptValue(target, index, value, subscript.Span, context);
    }

    private static void SetSubscriptValue(object target, object index, object value, LythonSourceSpan span, ExecutionContext context)
    {
        switch (target)
        {
            case IMutablePySubscriptableValue subscriptable:
                subscriptable.SetSubscript(index, value, span);
                return;

            case IMutablePySequenceValue sequence:
                sequence.SetItem(PyIndexing.NormalizeIndex(index, sequence.Count, span), value);
                return;
            case PyDict dict:
                dict.AttachMemoryGovernor(context.MemoryGovernor, span);
                dict.SetItem(ValidateDictionaryKey(index, span), value);
                return;
            case PyDefaultDict defaultDict:
                defaultDict.AttachMemoryGovernor(context.MemoryGovernor, span);
                defaultDict.SetItem(ValidateDictionaryKey(index, span), value);
                return;
            case PyCounter counter:
                counter.AttachMemoryGovernor(context.MemoryGovernor, span);
                counter.SetItem(ValidateDictionaryKey(index, span), value);
                return;
            case PyTuple:
                throw new LythonRuntimeException("TypeError", "Tuple does not support item assignment.", span);
            case string:
                throw new LythonRuntimeException("TypeError", "String does not support item assignment.", span);
            default:
                throw new LythonRuntimeException("TypeError", "Object does not support item assignment.", span);
        }
    }

    private static void AssignSliceTarget(SliceAssignmentTargetSyntax slice, object value, ExecutionContext context)
    {
        ExecuteSliceAssignment(
            EvaluateExpression(slice.Target, context),
            slice.Start is null ? null : EvaluateExpression(slice.Start, context),
            slice.End is null ? null : EvaluateExpression(slice.End, context),
            slice.Step is null ? null : EvaluateExpression(slice.Step, context),
            value,
            slice.Span,
            context);
    }

    private static void AssignMemberTarget(MemberAssignmentTargetSyntax member, object value, ExecutionContext context)
    {
        var target = EvaluateExpression(member.Target, context);
        SetMemberValue(target, member.MemberName, value, member.Span, context);
    }

    private static void SetMemberValue(object target, string memberName, object value, LythonSourceSpan span, ExecutionContext context)
    {
        if (!PyMemberAccess.TryAssign(target, memberName, value, context, span))
        {
            throw new LythonRuntimeException("TypeError", "Object does not support attribute assignment.", span);
        }
    }

    private static AugmentedAssignmentTargetReference ResolveAugmentedAssignmentTarget(AssignmentTargetSyntax target, ExecutionContext context)
    {
        switch (target)
        {
            case NameAssignmentTargetSyntax name:
                var currentValue = ResolveName(name.Name, name.Span, context);

                return new AugmentedAssignmentTargetReference(
                    currentValue,
                    value => StoreName(name.Name, value, context, name.Span));

            case SubscriptAssignmentTargetSyntax subscript:
                var subscriptTarget = EvaluateExpression(subscript.Target, context);
                var index = EvaluateExpression(subscript.Index, context);
                var subscriptValue = ReadSubscriptValue(subscriptTarget, index, subscript.Span, context);
                return new AugmentedAssignmentTargetReference(
                    subscriptValue,
                    value => SetSubscriptValue(subscriptTarget, index, value, subscript.Span, context));

            case SliceAssignmentTargetSyntax slice:
                var sliceTarget = EvaluateExpression(slice.Target, context);
                var start = slice.Start is null ? null : EvaluateExpression(slice.Start, context);
                var end = slice.End is null ? null : EvaluateExpression(slice.End, context);
                var step = slice.Step is null ? null : EvaluateExpression(slice.Step, context);
                var sliceValue = PyIndexing.ReadSlice(sliceTarget, start, end, step, slice.Span);
                return new AugmentedAssignmentTargetReference(
                    sliceValue,
                    value => ExecuteSliceAssignment(sliceTarget, start, end, step, value, slice.Span, context));

            case MemberAssignmentTargetSyntax member:
                var memberTarget = EvaluateExpression(member.Target, context);
                if (!TryResolveRuntimeMember(memberTarget, member.MemberName, context, member.Span, out var memberValue))
                {
                    throw PyMemberAccess.CreateMissingMemberError(memberTarget, member.MemberName, member.Span);
                }

                return new AugmentedAssignmentTargetReference(
                    memberValue,
                    value => SetMemberValue(memberTarget, member.MemberName, value, member.Span, context));

            default:
                throw new LythonRuntimeException("TypeError", "Unsupported augmented assignment target.", target.Span);
        }
    }

    private static object ReadSubscriptValue(object target, object index, LythonSourceSpan span, ExecutionContext context)
    {
        if (target is PyDefaultDict defaultDict)
        {
            return defaultDict.GetOrCreate(ValidateDictionaryKey(index, span), context, span);
        }

        return PyIndexing.ReadIndex(target, index, span);
    }

    private static void ExecuteSliceAssignment(
        object target,
        object? start,
        object? end,
        object? step,
        object value,
        LythonSourceSpan span,
        ExecutionContext context)
    {
        if (target is PyList list)
        {
            list.AttachMemoryGovernor(context.MemoryGovernor, span);
            var values = ToSequence(value, span).ToArray();
            var bounds = PyIndexing.NormalizeSliceBounds(list.Count, start, end, step, span);
            list.SetSlice(bounds, values, span);
            context.ObserveCollectionCount(list.Count, span);
            return;
        }

        if (target is PyTuple)
        {
            throw new LythonRuntimeException("TypeError", "Tuple does not support slice assignment.", span);
        }

        if (PyStringOps.TryAsString(target, out _))
        {
            throw new LythonRuntimeException("TypeError", "String does not support slice assignment.", span);
        }

        throw new LythonRuntimeException("TypeError", "Object does not support slice assignment.", span);
    }

    private static void ExecuteSliceDeletion(
        object target,
        object? start,
        object? end,
        object? step,
        LythonSourceSpan span)
    {
        if (target is PyList list)
        {
            var bounds = PyIndexing.NormalizeSliceBounds(list.Count, start, end, step, span);
            list.DeleteSlice(bounds);
            return;
        }

        if (target is PyTuple)
        {
            throw new LythonRuntimeException("TypeError", "Tuple does not support slice deletion.", span);
        }

        if (PyStringOps.TryAsString(target, out _))
        {
            throw new LythonRuntimeException("TypeError", "String does not support slice deletion.", span);
        }

        throw new LythonRuntimeException("TypeError", "Object does not support slice deletion.", span);
    }

    private static void ExecuteUnpackingAssignment(UnpackingAssignmentStatementSyntax statement, ExecutionContext context)
    {
        AssignTargets(statement.Targets, EvaluateExpression(statement.Expression, context), statement.Expression.Span, context);
    }

    private static void ExecuteWithStatement(
        WithStatementSyntax statement,
        LoweredExpression? loweredContextExpression,
        IReadOnlyList<LoweredStatement>? loweredBody,
        ExecutionContext context)
    {
        _ = PyContextManagers.ExecuteWith(
            loweredContextExpression is null
                ? EvaluateExpression(statement.ContextExpression, context)
                : EvaluateLoweredExpression(loweredContextExpression, context),
            statement.VariableName,
            loweredBody ?? LoweredScript.Lower(new ScriptSyntax(statement.Body)).Statements,
            statement.Span,
            statement.ContextExpression.Span,
            context);
    }

    private static async ValueTask ExecuteWithStatementAsync(
        WithStatementSyntax statement,
        LoweredExpression loweredContextExpression,
        IReadOnlyList<LoweredStatement> loweredBody,
        ExecutionContext context)
    {
        _ = await PyContextManagers.ExecuteWithAsync(
                await EvaluateLoweredExpressionAsync(loweredContextExpression, context).ConfigureAwait(false),
                statement.VariableName,
                loweredBody,
                statement.Span,
                statement.ContextExpression.Span,
                context)
            .ConfigureAwait(false);
    }

    private static object EvaluateAugmentedAssignment(
        object currentValue,
        object right,
        AugmentedAssignmentOperatorSyntax op,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        if (op == AugmentedAssignmentOperatorSyntax.Add &&
            currentValue is PyList currentList)
        {
            currentList.AddRange(ToSequence(right, span));
            return currentList;
        }

        if (op == AugmentedAssignmentOperatorSyntax.Multiply &&
            currentValue is PyList multipliedList &&
            right is BigInteger repeatCount)
        {
            multipliedList.RepeatInPlace(ToListRepeatCount(repeatCount, span), span);
            context.ObserveCollectionCount(multipliedList.Count, span);
            return multipliedList;
        }

        if (currentValue is PySet currentSet && right is PySet rightSet)
        {
            switch (op)
            {
                case AugmentedAssignmentOperatorSyntax.BitwiseOr:
                    currentSet.UnionWith(rightSet);
                    return currentSet;
                case AugmentedAssignmentOperatorSyntax.BitwiseAnd:
                    currentSet.IntersectWith(rightSet);
                    return currentSet;
                case AugmentedAssignmentOperatorSyntax.BitwiseXor:
                    currentSet.SymmetricExceptWith(rightSet);
                    return currentSet;
            }
        }

        return op switch
        {
            AugmentedAssignmentOperatorSyntax.Add => EvaluateAdd(currentValue, right, context, span),
            AugmentedAssignmentOperatorSyntax.Subtract => EvaluateSubtract(currentValue, right, span),
            AugmentedAssignmentOperatorSyntax.Multiply => EvaluateMultiply(currentValue, right, context, span),
            AugmentedAssignmentOperatorSyntax.Divide => EvaluateDivide(currentValue, right, span),
            AugmentedAssignmentOperatorSyntax.FloorDivide => EvaluateFloorDivide(currentValue, right, span),
            AugmentedAssignmentOperatorSyntax.Modulo => EvaluateModulo(currentValue, right, span),
            AugmentedAssignmentOperatorSyntax.Power => EvaluatePower(currentValue, right, context, span),
            AugmentedAssignmentOperatorSyntax.BitwiseOr => EvaluateBitwiseOr(currentValue, right, span),
            AugmentedAssignmentOperatorSyntax.BitwiseXor => EvaluateBitwiseXor(currentValue, right, span),
            AugmentedAssignmentOperatorSyntax.BitwiseAnd => EvaluateBitwiseAnd(currentValue, right, span),
            AugmentedAssignmentOperatorSyntax.LeftShift => EvaluateLeftShift(currentValue, right, context, span),
            AugmentedAssignmentOperatorSyntax.RightShift => EvaluateRightShift(currentValue, right, span),
            _ => throw new InvalidOperationException($"Unsupported augmented assignment operator: {op}")
        };
    }

    private static object EvaluateConditional(ConditionalExpressionSyntax conditional, ExecutionContext context)
    {
        return IsTruthy(EvaluateExpression(conditional.Condition, context))
            ? EvaluateExpression(conditional.Consequent, context)
            : EvaluateExpression(conditional.Alternative, context);
    }

    private static void ExecuteMatchStatement(MatchStatementSyntax statement, ExecutionContext context)
    {
        var subject = EvaluateExpression(statement.Subject, context);
        ExecuteMatch(statement, subject, context);
    }

    private static void ExecuteMatch(MatchStatementSyntax statement, object subject, ExecutionContext context)
    {
        foreach (var matchCase in statement.Cases)
        {
            var bindings = new Dictionary<string, object>(StringComparer.Ordinal);
            if (!TryMatchPattern(matchCase.Pattern, subject, context, bindings))
            {
                continue;
            }

            if (matchCase.Guard is not null)
            {
                var guardContext = new ExecutionContext(context);
                foreach (var pair in bindings)
                {
                    guardContext.Variables[pair.Key] = pair.Value;
                }

                if (!IsTruthy(EvaluateExpression(matchCase.Guard, guardContext)))
                {
                    continue;
                }
            }

            foreach (var pair in bindings)
            {
                StoreName(pair.Key, pair.Value, context, matchCase.Span);
            }

            var signal = ExecuteStatements(matchCase.Body, context);
            if (signal is not null)
            {
                throw signal;
            }

            return;
        }
    }

    private static bool TryMatchPattern(PatternSyntax pattern, object subject, ExecutionContext context, Dictionary<string, object> bindings)
    {
        switch (pattern)
        {
            case MatchValuePatternSyntax valuePattern:
                return AreEqual(subject, EvaluateExpression(valuePattern.Expression, context));

            case MatchSingletonPatternSyntax singletonPattern:
                return singletonPattern.Value switch
                {
                    MatchSingletonKind.None => subject is PyNone,
                    MatchSingletonKind.True => subject is bool boolean && boolean,
                    MatchSingletonKind.False => subject is bool boolean && !boolean,
                    _ => false
                };

            case MatchCapturePatternSyntax capturePattern:
                return TryBindPatternName(capturePattern.Name, subject, bindings);

            case MatchWildcardPatternSyntax:
                return true;

            case MatchSequencePatternSyntax sequencePattern:
                return TryMatchSequencePattern(sequencePattern, subject, context, bindings);

            case MatchMappingPatternSyntax mappingPattern:
                return TryMatchMappingPattern(mappingPattern, subject, context, bindings);

            case MatchClassPatternSyntax classPattern:
                return TryMatchClassPattern(classPattern, subject, context, bindings);

            case MatchStarPatternSyntax starPattern:
                return starPattern.Name is null || TryBindPatternName(starPattern.Name, subject, bindings);

            case MatchAsPatternSyntax asPattern:
                return TryMatchPattern(asPattern.Pattern, subject, context, bindings)
                    && TryBindPatternName(asPattern.Name, subject, bindings);

            case MatchOrPatternSyntax orPattern:
                foreach (var candidate in orPattern.Patterns)
                {
                    var branchBindings = new Dictionary<string, object>(bindings, StringComparer.Ordinal);
                    if (!TryMatchPattern(candidate, subject, context, branchBindings))
                    {
                        continue;
                    }

                    bindings.Clear();
                    foreach (var pair in branchBindings)
                    {
                        bindings[pair.Key] = pair.Value;
                    }

                    return true;
                }

                return false;

            default:
                throw new InvalidOperationException($"Unknown pattern type: {pattern.GetType().Name}");
        }
    }

    private static bool TryMatchSequencePattern(
        MatchSequencePatternSyntax pattern,
        object subject,
        ExecutionContext context,
        Dictionary<string, object> bindings)
    {
        if (!TryGetPatternSequence(subject, out var items))
        {
            return false;
        }

        var starIndex = pattern.Items
            .Select((item, index) => (item, index))
            .FirstOrDefault(pair => pair.item is MatchStarPatternSyntax).index;
        var hasStar = pattern.Items.Any(item => item is MatchStarPatternSyntax);

        if (!hasStar)
        {
            if (items.Count != pattern.Items.Count)
            {
                return false;
            }

            for (var i = 0; i < pattern.Items.Count; i++)
            {
                if (!TryMatchPattern(pattern.Items[i], items[i], context, bindings))
                {
                    return false;
                }
            }

            return true;
        }

        var beforeCount = starIndex;
        var afterCount = pattern.Items.Count - starIndex - 1;
        if (items.Count < beforeCount + afterCount)
        {
            return false;
        }

        for (var i = 0; i < beforeCount; i++)
        {
            if (!TryMatchPattern(pattern.Items[i], items[i], context, bindings))
            {
                return false;
            }
        }

        var starPattern = (MatchStarPatternSyntax)pattern.Items[starIndex];
        var starItems = items.Skip(beforeCount).Take(items.Count - beforeCount - afterCount).ToArray();
        if (starPattern.Name is not null &&
            !TryBindPatternName(
                starPattern.Name,
                new PyList(starItems, context.MemoryGovernor, pattern.Span),
                bindings))
        {
            return false;
        }

        for (var i = 0; i < afterCount; i++)
        {
            var patternIndex = starIndex + 1 + i;
            var itemIndex = items.Count - afterCount + i;
            if (!TryMatchPattern(pattern.Items[patternIndex], items[itemIndex], context, bindings))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryMatchMappingPattern(
        MatchMappingPatternSyntax pattern,
        object subject,
        ExecutionContext context,
        Dictionary<string, object> bindings)
    {
        if (subject is not PyDict dict)
        {
            return false;
        }

        var matchedKeys = new HashSet<object>(new PyValueComparer());
        foreach (var item in pattern.Items)
        {
            var key = ValidateDictionaryKey(EvaluateExpression(item.Key, context), item.Key.Span, context.MemoryGovernor);
            if (!dict.TryGetValue(key, out var value))
            {
                return false;
            }

            matchedKeys.Add(key);
            if (!TryMatchPattern(item.Pattern, value, context, bindings))
            {
                return false;
            }
        }

        if (pattern.RestName is not null)
        {
            var rest = new PyDict(context.MemoryGovernor, pattern.Span);
            foreach (var pair in dict)
            {
                if (!matchedKeys.Contains(pair.Key))
                {
                    rest.SetItem(pair.Key, pair.Value);
                }
            }

            if (!TryBindPatternName(pattern.RestName, rest, bindings))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryMatchClassPattern(
        MatchClassPatternSyntax pattern,
        object subject,
        ExecutionContext context,
        Dictionary<string, object> bindings)
    {
        var classValue = TryResolvePatternClassValue(pattern.ClassExpression, context);
        if (classValue is PyType runtimeType)
        {
            if (subject is not PyInstance instance || !instance.Type.IsSubtypeOf(runtimeType))
            {
                return false;
            }

            if (pattern.PositionalPatterns.Count != 0)
            {
                if (!runtimeType.TryGetMatchArgs(out var matchArgs))
                {
                    throw new LythonRuntimeException(
                        "TypeError",
                        $"Class '{runtimeType.Name}' does not define __match_args__ for positional class patterns.",
                        pattern.Span);
                }

                if (pattern.PositionalPatterns.Count > matchArgs.Count)
                {
                    throw new LythonRuntimeException(
                        "TypeError",
                        $"Class '{runtimeType.Name}' accepts {matchArgs.Count} positional class pattern argument(s), {pattern.PositionalPatterns.Count} given.",
                        pattern.Span);
                }

                for (var i = 0; i < pattern.PositionalPatterns.Count; i++)
                {
                    if (!PyMemberAccess.TryResolve(subject, matchArgs[i], context, pattern.Span, out var memberValue) ||
                        !TryMatchPattern(pattern.PositionalPatterns[i], RuntimeValue(memberValue), context, bindings))
                    {
                        return false;
                    }
                }
            }

            foreach (var keyword in pattern.KeywordPatterns)
            {
                if (!PyMemberAccess.TryResolve(subject, keyword.Name, context, keyword.Pattern.Span, out var memberValue))
                {
                    return false;
                }

                if (!TryMatchPattern(keyword.Pattern, RuntimeValue(memberValue), context, bindings))
                {
                    return false;
                }
            }

            return true;
        }

        if (!TryGetPatternClassName(pattern.ClassExpression, out var className))
        {
            throw new LythonRuntimeException(
                "TypeError",
                "match class patterns require a simple class name or dotted class name.",
                pattern.Span);
        }

        if (!DoesSubjectMatchPatternClass(className, subject))
        {
            return false;
        }

        if (pattern.PositionalPatterns.Count != 0)
        {
            return className switch
            {
                "list" when subject is PyList => TryMatchSequencePattern(
                    new MatchSequencePatternSyntax(pattern.PositionalPatterns, pattern.Span),
                    subject,
                    context,
                    bindings),
                "tuple" when subject is PyTuple => TryMatchSequencePattern(
                    new MatchSequencePatternSyntax(pattern.PositionalPatterns, pattern.Span),
                    subject,
                    context,
                    bindings),
                _ => throw new LythonRuntimeException(
                    "TypeError",
                    $"match class pattern '{className}(...)' does not support positional subpatterns in Lython.",
                    pattern.Span)
            };
        }

        foreach (var keyword in pattern.KeywordPatterns)
        {
            if (!PyMemberAccess.TryResolve(subject, keyword.Name, context, keyword.Pattern.Span, out var memberValue))
            {
                return false;
            }

            memberValue = RuntimeValue(memberValue);
            if (!TryMatchPattern(keyword.Pattern, memberValue, context, bindings))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGetPatternSequence(object subject, out IReadOnlyList<object> items)
    {
        switch (subject)
        {
            case PyList list:
                items = list.ToArray();
                return true;
            case PyTuple tuple:
                items = tuple.ToArray();
                return true;
            default:
                items = Array.Empty<object>();
                return false;
        }
    }

    private static bool TryBindPatternName(string name, object value, Dictionary<string, object> bindings)
    {
        value = RuntimeValue(value);
        if (bindings.TryGetValue(name, out var existing))
        {
            return AreEqual(existing, value);
        }

        bindings[name] = value;
        return true;
    }

    private static bool TryGetPatternClassName(ExpressionSyntax expression, out string className)
    {
        switch (expression)
        {
            case IdentifierExpressionSyntax identifier:
                className = identifier.Name;
                return true;
            case MemberExpressionSyntax member when TryGetPatternClassName(member.Target, out var prefix):
                className = prefix + "." + member.MemberName;
                return true;
            default:
                className = string.Empty;
                return false;
        }
    }

    private static object? TryResolvePatternClassValue(ExpressionSyntax expression, ExecutionContext context)
    {
        return expression switch
        {
            IdentifierExpressionSyntax identifier => ResolveIdentifier(identifier, context),
            MemberExpressionSyntax member => ResolveMember(member, context),
            _ => null
        };
    }

    private static bool DoesSubjectMatchPatternClass(string className, object subject)
    {
        return className switch
        {
            "list" => subject is PyList,
            "tuple" => subject is PyTuple,
            "dict" => subject is PyDict,
            "set" => subject is PySet,
            "str" => subject is PyString or string,
            "bytes" => subject is PyBytes,
            "bool" => subject is bool,
            "int" => subject is BigInteger or int or bool,
            "float" => subject is double,
            "pathlib.Path" => subject is PyPath,
            "datetime.timedelta" => subject is PyTimedelta,
            "datetime.date" => subject is PyDate,
            "datetime.time" => subject is PyTime,
            "datetime.datetime" => subject is PyDateTime,
            "datetime.timezone" => subject is PyTimezone,
            "re.Match" => subject is ReMatchObject,
            "re.Pattern" => subject is RePatternObject,
            _ => false
        };
    }

    private static IReadOnlyList<PyType> ResolveClassBases(object[] baseValues, LythonSourceSpan span, ExecutionContext context)
    {
        if (baseValues.Length == 0)
        {
            return context.TryGetBuiltinType("object", out var rootType)
                ? [rootType]
                : Array.Empty<PyType>();
        }

        var bases = new List<PyType>(baseValues.Length);
        foreach (var baseValue in baseValues)
        {
            if (baseValue is PyTypingAlias { IsInertClassBase: true })
            {
                continue;
            }

            if (baseValue is not PyType type)
            {
                throw new LythonRuntimeException("TypeError", "Class bases must be user-defined Lython classes.", span);
            }

            bases.Add(type);
        }

        if (bases.Count == 0)
        {
            return context.TryGetBuiltinType("object", out var rootType)
                ? [rootType]
                : Array.Empty<PyType>();
        }

        return bases;
    }

    private static object EvaluateAssignmentExpression(AssignmentExpressionSyntax assignment, ExecutionContext context)
    {
        var value = EvaluateExpression(assignment.Expression, context);
        StoreName(assignment.Name, value, context, assignment.Span);
        return value;
    }

    private static object EvaluateUnary(UnaryExpressionSyntax unary, ExecutionContext context)
    {
        var operand = EvaluateExpression(unary.Operand, context);
        return unary.Operator switch
        {
            UnaryOperatorSyntax.Not => !IsTruthy(operand),
            UnaryOperatorSyntax.Plus => EvaluateUnaryPlus(operand, unary.Span),
            UnaryOperatorSyntax.Minus => EvaluateUnaryMinus(operand, unary.Span),
            UnaryOperatorSyntax.BitwiseNot => EvaluateBitwiseNot(operand, unary.Span),
            _ => throw new InvalidOperationException($"Unknown unary operator: {unary.Operator}")
        };
    }

    private static BigInteger ParseInteger(IntegerLiteralExpressionSyntax integer)
    {
        return PyNumberOps.ParseInteger(integer.ValueText);
    }

    private static double ParseFloat(FloatLiteralExpressionSyntax floating)
    {
        return PyNumberOps.ParseFloat(floating.ValueText);
    }

    private static object EvaluateAdd(object left, object right, ExecutionContext context, LythonSourceSpan span)
    {
        if (left is PyDecimal || right is PyDecimal)
        {
            return PyDecimalOps.Add(left, right, span);
        }

        if (PyStringOps.TryAsString(left, out var leftText) && PyStringOps.TryAsString(right, out var rightText))
        {
            return ConcatStrings(leftText, rightText, context, span);
        }

        if (left is PyList leftList && right is PyList rightList)
        {
            var governor = leftList.OwnerMemoryGovernor ?? rightList.OwnerMemoryGovernor;
            var allocationSpan = leftList.AllocationSpan ?? rightList.AllocationSpan;
            var result = governor is null
                ? new PyList(leftList)
                : new PyList(leftList.ToArray(), governor, allocationSpan);
            result.AddRange(rightList);
            return result;
        }

        if (left is PyTuple leftTuple && right is PyTuple rightTuple)
        {
            var governor = leftTuple.OwnerMemoryGovernor ?? rightTuple.OwnerMemoryGovernor;
            var allocationSpan = leftTuple.AllocationSpan ?? rightTuple.AllocationSpan;
            return governor is null
                ? new PyTuple(leftTuple.Concat(rightTuple))
                : new PyTuple(leftTuple.Concat(rightTuple), governor, allocationSpan);
        }

        if (left is PySet leftSet && right is PySet rightSet)
        {
            var governor = leftSet.OwnerMemoryGovernor ?? rightSet.OwnerMemoryGovernor;
            var allocationSpan = leftSet.AllocationSpan ?? rightSet.AllocationSpan;
            var result = governor is null
                ? new PySet(leftSet)
                : new PySet(leftSet, governor, allocationSpan);
            result.UnionWith(rightSet);
            return result;
        }

        if (left is PyCounter leftCounter && right is PyCounter rightCounter)
        {
            return BuildCounterBinaryResult(leftCounter, rightCounter, (lhs, rhs) => lhs + rhs, keepPositiveOnly: true, span);
        }

        if (left is PyTimedelta or PyDate or PyDateTime || right is PyTimedelta or PyDate or PyDateTime)
        {
            return PyDateTimeOps.Add(left, right, span);
        }

        if (!TryGetNumericOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '+'.", span);
        }

        return PyNumberOps.Add(lhs, rhs);
    }

    internal static object AddRuntimeValues(object left, object right, ExecutionContext context, LythonSourceSpan span)
        => EvaluateAdd(left, right, context, span);

    private static object EvaluateSubtract(object left, object right, LythonSourceSpan span)
    {
        if (left is PySet leftSet && right is PySet rightSet)
        {
            var governor = leftSet.OwnerMemoryGovernor ?? rightSet.OwnerMemoryGovernor;
            var allocationSpan = leftSet.AllocationSpan ?? rightSet.AllocationSpan;
            var result = governor is null
                ? new PySet(leftSet)
                : new PySet(leftSet, governor, allocationSpan);
            result.ExceptWith(rightSet);
            return result;
        }

        if (left is PyCounter leftCounter && right is PyCounter rightCounter)
        {
            return BuildCounterBinaryResult(leftCounter, rightCounter, (lhs, rhs) => lhs - rhs, keepPositiveOnly: true, span);
        }

        if (left is PyDecimal || right is PyDecimal)
        {
            return PyDecimalOps.Subtract(left, right, span);
        }

        if (left is PyTimedelta or PyDate or PyDateTime || right is PyTimedelta or PyDate or PyDateTime)
        {
            return PyDateTimeOps.Subtract(left, right, span);
        }

        if (!TryGetNumericOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '-'.", span);
        }

        return PyNumberOps.Subtract(lhs, rhs);
    }

    private static object EvaluateMultiply(object left, object right, ExecutionContext context, LythonSourceSpan span)
    {
        if (left is PyDecimal || right is PyDecimal)
        {
            return PyDecimalOps.Multiply(left, right, span);
        }

        if (PyStringOps.TryAsString(left, out var leftText) && right is BigInteger rightCount)
        {
            return RepeatString(leftText, rightCount, context, span);
        }

        if (PyStringOps.TryAsString(right, out var rightText) && left is BigInteger leftCount)
        {
            return RepeatString(rightText, leftCount, context, span);
        }

        if (left is PyList leftList && right is BigInteger rightRepeatCount)
        {
            return RepeatList(leftList, rightRepeatCount, context, span);
        }

        if (right is PyList rightList && left is BigInteger leftRepeatCount)
        {
            return RepeatList(rightList, leftRepeatCount, context, span);
        }

        if (left is PyTimedelta || right is PyTimedelta)
        {
            return PyDateTimeOps.Multiply(left, right, span);
        }

        if (!TryGetNumericOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '*'.", span);
        }

        return PyNumberOps.Multiply(lhs, rhs);
    }

    private static object EvaluateDivide(object left, object right, LythonSourceSpan span)
    {
        if (left is PyDecimal || right is PyDecimal)
        {
            return PyDecimalOps.Divide(left, right, span);
        }

        if (left is PyPath leftPath)
        {
            if (right is PyPath rightPath)
            {
                return new PyPath(PathOps.Join(leftPath.Value, rightPath.Value));
            }

            if (PyStringOps.TryAsString(right, out var rightText))
            {
                return new PyPath(PathOps.Join(leftPath.Value, rightText));
            }
        }

        if (left is PyTimedelta || right is PyTimedelta)
        {
            return PyDateTimeOps.Divide(left, right, span);
        }

        if (!TryGetNumericOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '/'.", span);
        }

        try
        {
            return PyNumberOps.TrueDivide(lhs, rhs);
        }
        catch (DivideByZeroException)
        {
            throw new LythonRuntimeException("ValueError", "division by zero", span);
        }
    }

    private static object EvaluateFloorDivide(object left, object right, LythonSourceSpan span)
    {
        if (left is PyTimedelta || right is PyTimedelta)
        {
            return PyDateTimeOps.FloorDivide(left, right, span);
        }

        if (!TryGetNumericOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '//'.", span);
        }

        try
        {
            return PyNumberOps.FloorDivide(lhs, rhs);
        }
        catch (DivideByZeroException)
        {
            throw new LythonRuntimeException("ValueError", "integer division or modulo by zero", span);
        }
    }

    private static object EvaluateModulo(object left, object right, LythonSourceSpan span)
    {
        if (left is PyDecimal || right is PyDecimal)
        {
            return PyDecimalOps.Modulo(left, right, span);
        }

        if (!TryGetNumericOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '%'.", span);
        }

        try
        {
            return PyNumberOps.Modulo(lhs, rhs);
        }
        catch (DivideByZeroException)
        {
            throw new LythonRuntimeException("ValueError", "integer division or modulo by zero", span);
        }
    }

    private static object EvaluatePower(object left, object right, ExecutionContext context, LythonSourceSpan span)
    {
        if (left is PyDecimal || right is PyDecimal)
        {
            return PyDecimalOps.Power(left, right, span);
        }

        if (!TryGetNumericOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '**'.", span);
        }

        try
        {
            GuardIntegerPower(lhs, rhs, context, span);
            return PyNumberOps.Power(lhs, rhs);
        }
        catch (OverflowException)
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '**'.", span);
        }
    }

    public async Task<LythonExecutionResult> RunAsync(
        LoweredScript script,
        ILythonHost host,
        LythonRunOptions? options)
    {
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(host);

        ExecutionContext? context = null;
        try
        {
            context = new ExecutionContext(host, options);
            await Task.Yield();
            var signal = await ExecuteStatementsAsync(script.Statements, context).ConfigureAwait(false);
            if (signal is BreakSignal or ContinueSignal)
            {
                throw RuntimeErrors.TopLevelLoopControl(null);
            }

            return new LythonExecutionResult(
                success: true,
                returnValue: null,
                standardOutput: CaptureStandardOutput(context),
                standardError: CaptureStandardError(context),
                exitCode: null,
                diagnostics: Array.Empty<LythonDiagnostic>(),
                failure: null);
        }
        catch (ReturnSignal signal)
        {
            try
            {
                return new LythonExecutionResult(
                    success: true,
                    returnValue: NormalizePublicValue(signal.Value, options),
                    standardOutput: context is null ? string.Empty : CaptureStandardOutput(context),
                    standardError: context is null ? string.Empty : CaptureStandardError(context),
                    exitCode: null,
                    diagnostics: Array.Empty<LythonDiagnostic>(),
                    failure: null);
            }
            catch (ProjectionException ex)
            {
                return new LythonExecutionResult(
                    success: false,
                    returnValue: null,
                    standardOutput: context is null ? string.Empty : CaptureStandardOutput(context),
                    standardError: context is null ? string.Empty : CaptureStandardError(context),
                    exitCode: null,
                    diagnostics: Array.Empty<LythonDiagnostic>(),
                    failure: new LythonRuntimeFailure("ProjectionError", ex.Message, null, Array.Empty<LythonStackFrame>(), context?.SourcePath));
            }
        }
        catch (LythonRuntimeException ex)
        {
            ex.SetSourcePathIfMissing(context?.SourcePath);
            return new LythonExecutionResult(
                success: false,
                returnValue: null,
                standardOutput: context is null ? string.Empty : CaptureStandardOutput(context),
                standardError: context is null ? string.Empty : CaptureStandardError(context),
                exitCode: GetExitCode(ex),
                diagnostics: Array.Empty<LythonDiagnostic>(),
                failure: RuntimeFailureProjection.ToPublicFailure(ex));
        }
    }

    private static void GuardIntegerPower(PyNumber lhs, PyNumber rhs, ExecutionContext context, LythonSourceSpan span)
    {
        if (lhs.IsFloat || rhs.IsFloat || rhs.Integer < BigInteger.Zero)
        {
            return;
        }

        var baseBits = RuntimeMemoryEstimates.GetMagnitudeBitLength(lhs.Integer);
        var exponent = rhs.Integer;
        var resultBits = baseBits switch
        {
            0 => 0,
            1 => 1,
            _ when exponent > long.MaxValue => long.MaxValue,
            _ => RuntimeMemoryEstimates.SaturatingMultiply(baseBits, (long)exponent)
        };

        GuardIntegerResultBytes(RuntimeMemoryEstimates.EstimateBigIntegerBytesFromBitCount(resultBits), context, span);
    }

    private static void GuardIntegerLeftShift(BigInteger lhs, BigInteger rhs, ExecutionContext context, LythonSourceSpan span)
    {
        if (rhs < BigInteger.Zero)
        {
            return;
        }

        var lhsBits = RuntimeMemoryEstimates.GetMagnitudeBitLength(lhs);
        var shiftBits = rhs > long.MaxValue ? long.MaxValue : (long)rhs;
        var resultBits = RuntimeMemoryEstimates.SaturatingAdd(lhsBits, shiftBits);
        GuardIntegerResultBytes(RuntimeMemoryEstimates.EstimateBigIntegerBytesFromBitCount(resultBits), context, span);
    }

    private static void GuardIntegerResultBytes(long estimatedBytes, ExecutionContext context, LythonSourceSpan span)
    {
        if (context.Limits.MaxExecutionMemoryBytes is not { } maxBytes)
        {
            return;
        }

        var current = context.Services.LegacyApproximateMemoryDiagnostics.CurrentBytes;
        if (RuntimeMemoryEstimates.SaturatingAdd(current, estimatedBytes) > maxBytes)
        {
            throw RuntimeErrors.Runtime($"execution memory budget exceeded ({maxBytes})", span);
        }
    }

    private static object EvaluateBitwiseOr(object left, object right, LythonSourceSpan span)
    {
        if (left is PyCounter leftCounter && right is PyCounter rightCounter)
        {
            return BuildCounterBinaryResult(leftCounter, rightCounter, BigInteger.Max, keepPositiveOnly: true, span);
        }

        if (left is PySet leftSet && right is PySet rightSet)
        {
            var governor = leftSet.OwnerMemoryGovernor ?? rightSet.OwnerMemoryGovernor;
            var allocationSpan = leftSet.AllocationSpan ?? rightSet.AllocationSpan;
            var result = governor is null
                ? new PySet(leftSet)
                : new PySet(leftSet, governor, allocationSpan);
            result.UnionWith(rightSet);
            return result;
        }

        if (!TryGetIntegerOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '|'.", span);
        }

        return PyNumberOps.BitwiseOr(lhs, rhs);
    }

    private static object EvaluateBitwiseXor(object left, object right, LythonSourceSpan span)
    {
        if (left is PySet leftSet && right is PySet rightSet)
        {
            var governor = leftSet.OwnerMemoryGovernor ?? rightSet.OwnerMemoryGovernor;
            var allocationSpan = leftSet.AllocationSpan ?? rightSet.AllocationSpan;
            var result = governor is null
                ? new PySet(leftSet)
                : new PySet(leftSet, governor, allocationSpan);
            result.SymmetricExceptWith(rightSet);
            return result;
        }

        if (!TryGetIntegerOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '^'.", span);
        }

        return PyNumberOps.BitwiseXor(lhs, rhs);
    }

    private static object EvaluateBitwiseAnd(object left, object right, LythonSourceSpan span)
    {
        if (left is PyCounter leftCounter && right is PyCounter rightCounter)
        {
            return BuildCounterBinaryResult(leftCounter, rightCounter, BigInteger.Min, keepPositiveOnly: true, span);
        }

        if (left is PySet leftSet && right is PySet rightSet)
        {
            var governor = leftSet.OwnerMemoryGovernor ?? rightSet.OwnerMemoryGovernor;
            var allocationSpan = leftSet.AllocationSpan ?? rightSet.AllocationSpan;
            var result = governor is null
                ? new PySet(leftSet)
                : new PySet(leftSet, governor, allocationSpan);
            result.IntersectWith(rightSet);
            return result;
        }

        if (!TryGetIntegerOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '&'.", span);
        }

        return PyNumberOps.BitwiseAnd(lhs, rhs);
    }

    private static object EvaluateLeftShift(object left, object right, ExecutionContext context, LythonSourceSpan span)
    {
        if (!TryGetIntegerOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '<<'.", span);
        }

        try
        {
            GuardIntegerLeftShift(lhs, rhs, context, span);
            return PyNumberOps.LeftShift(lhs, rhs);
        }
        catch (InvalidOperationException ex) when (ex.Message == "negative shift count")
        {
            throw new LythonRuntimeException("ValueError", ex.Message, span);
        }
        catch (OverflowException)
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '<<'.", span);
        }
    }

    private static object EvaluateRightShift(object left, object right, LythonSourceSpan span)
    {
        if (!TryGetIntegerOperands(left, right, out var lhs, out var rhs))
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '>>'.", span);
        }

        try
        {
            return PyNumberOps.RightShift(lhs, rhs);
        }
        catch (InvalidOperationException ex) when (ex.Message == "negative shift count")
        {
            throw new LythonRuntimeException("ValueError", ex.Message, span);
        }
        catch (OverflowException)
        {
            throw new LythonRuntimeException("TypeError", "Operands are not compatible with '>>'.", span);
        }
    }

    private static object EvaluateUnaryPlus(object operand, LythonSourceSpan span)
    {
        if (operand is PyCounter positiveCounter)
        {
            return BuildCounterUnaryResult(positiveCounter, count => count, keepPositiveOnly: true, span);
        }

        if (operand is PyDecimal)
        {
            return operand;
        }

        if (!PyNumberOps.TryAsNumber(operand, out _))
        {
            throw new LythonRuntimeException("TypeError", "Operand is not numeric.", span);
        }

        return operand!;
    }

    private static object EvaluateUnaryMinus(object operand, LythonSourceSpan span)
    {
        if (operand is PyCounter negativeCounter)
        {
            return BuildCounterUnaryResult(negativeCounter, count => -count, keepPositiveOnly: true, span);
        }

        if (operand is PyTimedelta)
        {
            return PyDateTimeOps.Negate(operand, span);
        }

        if (operand is PyDecimal decimalValue)
        {
            return new PyDecimal(-decimalValue.Value);
        }

        if (!PyNumberOps.TryAsNumber(operand, out var numeric))
        {
            throw new LythonRuntimeException("TypeError", "Operand is not numeric.", span);
        }

        return PyNumberOps.Negate(numeric);
    }

    private static PyCounter BuildCounterUnaryResult(
        PyCounter source,
        Func<BigInteger, BigInteger> transform,
        bool keepPositiveOnly,
        LythonSourceSpan span)
    {
        var result = CreateCounterResult(source, null, span);
        foreach (var pair in source.Items)
        {
            var count = transform(ExpectCounterCount(pair.Value, span));
            if (keepPositiveOnly && count <= 0)
            {
                continue;
            }

            result.SetItem(pair.Key, count);
        }

        return result;
    }

    private static PyCounter BuildCounterBinaryResult(
        PyCounter left,
        PyCounter right,
        Func<BigInteger, BigInteger, BigInteger> combine,
        bool keepPositiveOnly,
        LythonSourceSpan span)
    {
        var result = CreateCounterResult(left, right, span);
        foreach (var key in UnionCounterKeys(left, right))
        {
            var count = combine(left.GetIntegerCountOrZero(key, span), right.GetIntegerCountOrZero(key, span));
            if (keepPositiveOnly && count <= 0)
            {
                continue;
            }

            result.SetItem(key, count);
        }

        return result;
    }

    private static PyCounter CreateCounterResult(PyCounter left, PyCounter? right, LythonSourceSpan span)
    {
        var governor = left.OwnerMemoryGovernor ?? right?.OwnerMemoryGovernor;
        var allocationSpan = left.AllocationSpan ?? right?.AllocationSpan ?? span;
        return governor is null ? new PyCounter() : new PyCounter(governor, allocationSpan);
    }

    private static IReadOnlyList<object> UnionCounterKeys(PyCounter left, PyCounter right)
    {
        var keys = new List<object>();
        foreach (var key in left.Keys.Concat(right.Keys))
        {
            if (!keys.Any(existing => AreEqual(existing, key)))
            {
                keys.Add(key);
            }
        }

        return keys;
    }

    private static object EvaluateBitwiseNot(object operand, LythonSourceSpan span)
    {
        if (!PyNumberOps.TryAsInteger(operand, out var integer))
        {
            throw new LythonRuntimeException("TypeError", "Operand is not an integer.", span);
        }

        return PyNumberOps.BitwiseNot(integer);
    }

    private static bool TryGetNumericOperands(object left, object right, out PyNumber lhs, out PyNumber rhs)
    {
        if (PyNumberOps.TryAsNumber(left, out lhs) && PyNumberOps.TryAsNumber(right, out rhs))
        {
            return true;
        }

        lhs = default;
        rhs = default;
        return false;
    }

    private static bool TryGetIntegerOperands(object left, object right, out BigInteger lhs, out BigInteger rhs)
    {
        if (PyNumberOps.TryAsInteger(left, out lhs) && PyNumberOps.TryAsInteger(right, out rhs))
        {
            return true;
        }

        lhs = default;
        rhs = default;
        return false;
    }

    private static PyString ConcatStrings(PyString left, PyString right, ExecutionContext context, LythonSourceSpan span)
    {
        var resultLength = RuntimeMemoryEstimates.SaturatingAdd(left.Length, right.Length);
        if (context.Limits.MaxStringLength is { } maxStringLength && resultLength > maxStringLength)
        {
            throw RuntimeErrors.Runtime($"maximum string length exceeded ({maxStringLength})", span);
        }

        return left.Concat(right);
    }

    private static PyString RepeatString(PyString text, BigInteger count, ExecutionContext context, LythonSourceSpan span)
    {
        if (count <= BigInteger.Zero)
        {
            return PyString.Empty;
        }

        if (count > int.MaxValue)
        {
            throw new LythonRuntimeException("RuntimeError", "String repetition is too large.", span);
        }

        var resultLength = RuntimeMemoryEstimates.SaturatingMultiply(text.Length, (long)count);
        if (context.Limits.MaxStringLength is { } maxStringLength && resultLength > maxStringLength)
        {
            throw RuntimeErrors.Runtime($"maximum string length exceeded ({maxStringLength})", span);
        }

        return text.Repeat((int)count);
    }

    private static PyList RepeatList(PyList list, BigInteger count, ExecutionContext context, LythonSourceSpan span)
    {
        var repeatCount = ToListRepeatCount(count, span);
        if (repeatCount == 0 || list.Count == 0)
        {
            return new PyList([], context.MemoryGovernor, span);
        }

        var totalLength = (long)list.Count * repeatCount;
        if (totalLength > int.MaxValue)
        {
            throw new LythonRuntimeException("RuntimeError", "List repetition is too large.", span);
        }

        var result = new PyList([], context.MemoryGovernor, span);
        for (var i = 0; i < repeatCount; i++)
        {
            result.AddRange(list);
            context.ObserveCollectionCount(result.Count, span);
        }

        return result;
    }

    private static int ToListRepeatCount(BigInteger count, LythonSourceSpan span)
    {
        if (count <= BigInteger.Zero)
        {
            return 0;
        }

        if (count > int.MaxValue)
        {
            throw new LythonRuntimeException("RuntimeError", "List repetition is too large.", span);
        }

        return (int)count;
    }

    private static object EvaluateDictLiteral(DictLiteralExpressionSyntax dict, ExecutionContext context)
    {
        var result = new PyDict(context.MemoryGovernor, dict.Span);
        foreach (var item in dict.Items)
        {
            result.SetItem(
                ValidateDictionaryKey(EvaluateExpression(item.Key, context), item.Key.Span, context.MemoryGovernor),
                EvaluateExpression(item.Value, context));
        }

        return result;
    }

    private static object EvaluateSetLiteral(SetLiteralExpressionSyntax set, ExecutionContext context)
    {
        var result = new PySet(context.MemoryGovernor, set.Span);
        foreach (var item in set.Items)
        {
            result.Add(ValidateSetItem(EvaluateExpression(item, context), set.Span, context.MemoryGovernor));
        }

        context.ObserveCollectionCount(result.Count, set.Span);
        return result;
    }

    private static object EvaluateFormattedString(FormattedStringExpressionSyntax formatted, ExecutionContext context)
    {
        var builder = new Utf8ValueBuilder(context.MemoryGovernor, formatted.Span);
        foreach (var part in formatted.Parts)
        {
            switch (part)
            {
                case FormattedStringTextPartSyntax text:
                    builder.AppendString(text.Text);
                    break;
                case FormattedStringExpressionPartSyntax expression:
                    builder.Append(FormatInterpolatedStringPart(
                        EvaluateExpression(expression.Expression, context),
                        expression.Conversion,
                        expression.FormatSpecifier,
                        context,
                        formatted.Span));
                    break;
                default:
                    throw new InvalidOperationException($"Unknown formatted string part: {part.GetType().Name}");
            }
        }

        var value = builder.ToPyString();
        context.ObserveString(value, formatted.Span);
        return value;
    }

    private static PyString ToInterpolatedPyString(object value, ExecutionContext context)
        => PyRendering.ToInterpolatedPyString(value, new PyRenderingContext(context));

    private static string ToInterpolatedString(object value, ExecutionContext context)
        => PyRendering.ToInterpolatedString(value, new PyRenderingContext(context));

    private static PyString ToPythonPyString(object value, ExecutionContext context)
        => PyRendering.ToPythonPyString(value, new PyRenderingContext(context));

    private static string ToPythonString(object value, ExecutionContext context)
        => PyRendering.ToPythonString(value, new PyRenderingContext(context));

    private static PyString ToReprPyString(object value, ExecutionContext context)
        => PyRendering.ToReprPyString(value, new PyRenderingContext(context));

    private static PyString FormatInterpolatedStringPart(
        object value,
        char? conversion,
        string? formatSpecifier,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        var converted = conversion switch
        {
            null => null,
            's' => ToInterpolatedPyString(value, context),
            'r' or 'a' => ToReprPyString(value, context),
            _ => throw new LythonRuntimeException("ValueError", $"Unknown conversion specifier '!{conversion}'.", span)
        };

        if (string.IsNullOrEmpty(formatSpecifier))
        {
            return converted ?? ToInterpolatedPyString(value, context);
        }

        var formatted = FormatInterpolatedStringValue(
            converted ?? value,
            formatSpecifier,
            context,
            span);
        return PyString.FromString(formatted, context.MemoryGovernor, span);
    }

    private static string FormatInterpolatedStringValue(
        object value,
        string formatSpecifier,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        var spec = ParseInterpolatedFormatSpecifier(formatSpecifier, span);
        if (TryFormatNumericValue(value, spec, context, span, out var numericText, out var numericPrefixLength))
        {
            return ApplyInterpolatedFormatPadding(numericText, spec, numericPrefixLength, numeric: true, span);
        }

        if (RequiresNumericFormat(spec))
        {
            throw new LythonRuntimeException("ValueError", $"Format code '{spec.Type}' requires a numeric value.", span);
        }

        if (spec.Sign is not null || spec.Alternate || spec.Grouping is not null || spec.Align == '=')
        {
            throw new LythonRuntimeException("ValueError", $"Invalid format specifier '{formatSpecifier}' for string value.", span);
        }

        var text = value switch
        {
            PyString pyString => pyString.AsString(),
            string raw => raw,
            _ => ToInterpolatedString(value, context)
        };

        if (spec.Type is not null and not 's')
        {
            throw new LythonRuntimeException("ValueError", $"Unknown format code '{spec.Type}' for string value.", span);
        }

        if (spec.Precision is { } precision)
        {
            text = text.Length <= precision ? text : text[..precision];
        }

        return ApplyInterpolatedFormatPadding(text, spec, numericPrefixLength: 0, numeric: false, span);
    }

    private static bool TryFormatNumericValue(
        object value,
        InterpolatedFormatSpecifier spec,
        ExecutionContext context,
        LythonSourceSpan span,
        out string text,
        out int numericPrefixLength)
    {
        text = string.Empty;
        numericPrefixLength = 0;

        if (TryGetIntegerFormatValue(value, out var integer))
        {
            if (spec.Type is 'f' or 'F' or 'g' or 'G' or '%')
            {
                return TryFormatFloatingValue((double)integer, spec, span, out text, out numericPrefixLength);
            }

            text = FormatIntegerValue(integer, spec, span, out numericPrefixLength);
            return true;
        }

        if (value is double floating)
        {
            if (spec.Type is 'd' or 'b' or 'o' or 'x' or 'X' or 'n')
            {
                throw new LythonRuntimeException("ValueError", $"Format code '{spec.Type}' requires an integer value.", span);
            }

            return TryFormatFloatingValue(floating, spec, span, out text, out numericPrefixLength);
        }

        if (value is PyDecimal decimalValue)
        {
            if (spec.Type is 'd' or 'b' or 'o' or 'x' or 'X' or 'n')
            {
                throw new LythonRuntimeException("ValueError", $"Format code '{spec.Type}' requires an integer value.", span);
            }

            return TryFormatFloatingValue((double)decimalValue.Value, spec, span, out text, out numericPrefixLength);
        }

        _ = context;
        return false;
    }

    private static bool TryGetIntegerFormatValue(object value, out BigInteger integer)
    {
        switch (value)
        {
            case BigInteger bigInteger:
                integer = bigInteger;
                return true;
            case int intValue:
                integer = new BigInteger(intValue);
                return true;
            case bool boolValue:
                integer = boolValue ? BigInteger.One : BigInteger.Zero;
                return true;
            default:
                integer = BigInteger.Zero;
                return false;
        }
    }

    private static string FormatIntegerValue(
        BigInteger value,
        InterpolatedFormatSpecifier spec,
        LythonSourceSpan span,
        out int numericPrefixLength)
    {
        if (spec.Precision is not null)
        {
            throw new LythonRuntimeException("ValueError", "Precision is not allowed in integer format specifiers.", span);
        }

        var type = spec.Type ?? 'd';
        var negative = value.Sign < 0;
        var magnitude = BigInteger.Abs(value);
        string digits;
        string prefix;
        switch (type)
        {
            case 'd':
            case 'n':
                digits = magnitude.ToString(CultureInfo.InvariantCulture);
                prefix = string.Empty;
                break;
            case 'b':
                digits = ToUnsignedBaseString(magnitude, 2, upper: false);
                prefix = spec.Alternate ? "0b" : string.Empty;
                break;
            case 'o':
                digits = ToUnsignedBaseString(magnitude, 8, upper: false);
                prefix = spec.Alternate ? "0o" : string.Empty;
                break;
            case 'x':
                digits = ToUnsignedBaseString(magnitude, 16, upper: false);
                prefix = spec.Alternate ? "0x" : string.Empty;
                break;
            case 'X':
                digits = ToUnsignedBaseString(magnitude, 16, upper: true);
                prefix = spec.Alternate ? "0X" : string.Empty;
                break;
            default:
                throw new LythonRuntimeException("ValueError", $"Unknown integer format code '{type}'.", span);
        }

        if (spec.Grouping is { } grouping)
        {
            if (type is not ('d' or 'n'))
            {
                throw new LythonRuntimeException("ValueError", "Grouping is only supported for decimal integer formatting.", span);
            }

            digits = GroupDigits(digits, grouping);
        }

        var sign = FormatNumericSign(negative, spec.Sign);
        numericPrefixLength = sign.Length + prefix.Length;
        return sign + prefix + digits;
    }

    private static bool TryFormatFloatingValue(
        double value,
        InterpolatedFormatSpecifier spec,
        LythonSourceSpan span,
        out string text,
        out int numericPrefixLength)
    {
        var type = spec.Type;
        var precision = spec.Precision;
        if (type is 's' or 'b' or 'o' or 'x' or 'X' or 'd' or 'n')
        {
            text = string.Empty;
            numericPrefixLength = 0;
            return false;
        }

        if (spec.Alternate)
        {
            throw new LythonRuntimeException("ValueError", "Alternate floating-point formatting is not supported.", span);
        }

        var formatted = type switch
        {
            null when precision is null => value.ToString(CultureInfo.InvariantCulture),
            null => value.ToString("G" + precision.Value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
            'f' => value.ToString("F" + (precision ?? 6).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
            'F' => value.ToString("F" + (precision ?? 6).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
            'g' => value.ToString("G" + (precision ?? 6).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
            'G' => value.ToString("G" + (precision ?? 6).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
            '%' => (value * 100.0).ToString("F" + (precision ?? 6).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) + "%",
            _ => throw new LythonRuntimeException("ValueError", $"Unknown floating-point format code '{type}'.", span)
        };

        if (type == 'F')
        {
            formatted = formatted.ToUpperInvariant();
        }

        if (spec.Grouping is { } grouping)
        {
            formatted = GroupFloatingDigits(formatted, grouping);
        }

        text = ApplyNumericSign(formatted, spec.Sign);
        numericPrefixLength = GetNumericPrefixLength(text);
        return true;
    }

    private static InterpolatedFormatSpecifier ParseInterpolatedFormatSpecifier(string text, LythonSourceSpan span)
    {
        var index = 0;
        char? fill = null;
        char? align = null;

        if (index + 1 < text.Length && IsFormatAlign(text[index + 1]))
        {
            fill = text[index];
            align = text[index + 1];
            index += 2;
        }
        else if (index < text.Length && IsFormatAlign(text[index]))
        {
            align = text[index];
            index++;
        }

        char? sign = null;
        if (index < text.Length && text[index] is '+' or '-' or ' ')
        {
            sign = text[index++];
        }

        var alternate = false;
        if (index < text.Length && text[index] == '#')
        {
            alternate = true;
            index++;
        }

        var zeroPad = false;
        if (index < text.Length && text[index] == '0')
        {
            zeroPad = true;
            index++;
        }

        int? width = null;
        var widthStart = index;
        while (index < text.Length && char.IsAsciiDigit(text[index]))
        {
            index++;
        }

        if (index > widthStart)
        {
            width = int.Parse(text[widthStart..index], CultureInfo.InvariantCulture);
        }

        char? grouping = null;
        if (index < text.Length && text[index] is ',' or '_')
        {
            grouping = text[index++];
        }

        int? precision = null;
        if (index < text.Length && text[index] == '.')
        {
            index++;
            var precisionStart = index;
            while (index < text.Length && char.IsAsciiDigit(text[index]))
            {
                index++;
            }

            if (precisionStart == index)
            {
                throw new LythonRuntimeException("ValueError", $"Invalid format specifier '{text}'.", span);
            }

            precision = int.Parse(text[precisionStart..index], CultureInfo.InvariantCulture);
        }

        char? type = null;
        if (index < text.Length)
        {
            type = text[index++];
        }

        if (index != text.Length)
        {
            throw new LythonRuntimeException("ValueError", $"Invalid format specifier '{text}'.", span);
        }

        return new InterpolatedFormatSpecifier(fill, align, sign, alternate, zeroPad, width, grouping, precision, type);
    }

    private static bool RequiresNumericFormat(InterpolatedFormatSpecifier spec)
        => spec.Type is 'd' or 'b' or 'o' or 'x' or 'X' or 'n' or 'f' or 'F' or 'g' or 'G' or '%';

    private static bool IsFormatAlign(char value)
        => value is '<' or '>' or '^' or '=';

    private static string ApplyInterpolatedFormatPadding(
        string text,
        InterpolatedFormatSpecifier spec,
        int numericPrefixLength,
        bool numeric,
        LythonSourceSpan span)
    {
        if (spec.Width is not { } width || text.Length >= width)
        {
            return text;
        }

        var align = spec.Align ?? (numeric ? '>' : '<');
        var fill = spec.Fill ?? (spec.ZeroPad ? '0' : ' ');
        if (numeric && spec.ZeroPad && spec.Align is null)
        {
            align = '=';
            fill = '0';
        }

        if (align == '=' && !numeric)
        {
            throw new LythonRuntimeException("ValueError", "'=' alignment requires a numeric value.", span);
        }

        var padding = width - text.Length;
        var padText = new string(fill, padding);
        return align switch
        {
            '<' => text + padText,
            '>' => padText + text,
            '^' => new string(fill, padding / 2) + text + new string(fill, padding - padding / 2),
            '=' => text[..numericPrefixLength] + padText + text[numericPrefixLength..],
            _ => throw new LythonRuntimeException("ValueError", $"Unknown alignment option '{align}'.", span)
        };
    }

    private static string FormatNumericSign(bool negative, char? sign)
        => negative
            ? "-"
            : sign switch
            {
                '+' => "+",
                ' ' => " ",
                _ => string.Empty
            };

    private static string ApplyNumericSign(string text, char? sign)
    {
        if (text.StartsWith("-", StringComparison.Ordinal) ||
            text.StartsWith("+", StringComparison.Ordinal) ||
            text.StartsWith(" ", StringComparison.Ordinal))
        {
            return text;
        }

        return sign switch
        {
            '+' => "+" + text,
            ' ' => " " + text,
            _ => text
        };
    }

    private static int GetNumericPrefixLength(string text)
        => text.Length > 0 && text[0] is '-' or '+' or ' ' ? 1 : 0;

    private static string ToUnsignedBaseString(BigInteger value, int radix, bool upper)
    {
        if (value.IsZero)
        {
            return "0";
        }

        const string lowerDigits = "0123456789abcdef";
        const string upperDigits = "0123456789ABCDEF";
        var digits = upper ? upperDigits : lowerDigits;
        var builder = new StringBuilder();
        while (value > BigInteger.Zero)
        {
            value = BigInteger.DivRem(value, radix, out var remainder);
            builder.Insert(0, digits[(int)remainder]);
        }

        return builder.ToString();
    }

    private static string GroupDigits(string digits, char separator)
    {
        var builder = new StringBuilder(digits.Length + digits.Length / 3);
        for (var i = 0; i < digits.Length; i++)
        {
            if (i > 0 && (digits.Length - i) % 3 == 0)
            {
                builder.Append(separator);
            }

            builder.Append(digits[i]);
        }

        return builder.ToString();
    }

    private static string GroupFloatingDigits(string text, char separator)
    {
        var signLength = GetNumericPrefixLength(text);
        var exponentIndex = text.IndexOfAny(['e', 'E']);
        var mantissaEnd = exponentIndex >= 0 ? exponentIndex : text.Length;
        var decimalIndex = text.IndexOf('.');
        if (decimalIndex < 0 || decimalIndex > mantissaEnd)
        {
            decimalIndex = mantissaEnd;
        }

        var grouped = GroupDigits(text[signLength..decimalIndex], separator);
        return text[..signLength] + grouped + text[decimalIndex..];
    }

    private sealed record InterpolatedFormatSpecifier(
        char? Fill,
        char? Align,
        char? Sign,
        bool Alternate,
        bool ZeroPad,
        int? Width,
        char? Grouping,
        int? Precision,
        char? Type);

    private static object ResolveIdentifier(IdentifierExpressionSyntax identifier, ExecutionContext context)
        => ResolveName(identifier.Name, identifier.Span, context);

    internal static object ResolveName(string name, LythonSourceSpan span, ExecutionContext context)
    {
        if (context.ScopeFacts.IsGlobal(name))
        {
            var globalContext = GetGlobalContext(context);
            if (globalContext.CurrentExecutableFrame is not null &&
                globalContext.CurrentExecutableFrame.TryResolveLocalOrClosure(name, out var executableValue))
            {
                return executableValue;
            }

            if (globalContext.Variables.TryGetValue(name, out var globalValue))
            {
                return globalValue;
            }

            throw RuntimeErrors.NameNotDefined(name, span);
        }

        if (context.TryGetNonlocalTarget(name, out var nonlocalContext))
        {
            if (nonlocalContext.Variables.TryGetValue(name, out var nonlocalValue))
            {
                return nonlocalValue;
            }

            throw RuntimeErrors.NameNotDefined(name, span);
        }

        for (var current = context; current is not null; current = current.Parent)
        {
            if (current.CurrentExecutableFrame is not null &&
                current.CurrentExecutableFrame.TryResolveLocalOrClosure(name, out var executableValue))
            {
                return executableValue;
            }

            if (current.Variables.TryGetValue(name, out var value))
            {
                return value;
            }
        }

        throw RuntimeErrors.NameNotDefined(name, span);
    }

    internal static void StoreName(string name, object value, ExecutionContext context, LythonSourceSpan span)
    {
        var storageContext = ResolveNameStorageContext(name, context, span);
        storageContext.Variables[name] = value;
        storageContext.CurrentExecutableFrame?.TryStoreLocalOrClosure(name, value);
    }

    internal static bool DeleteName(string name, ExecutionContext context, LythonSourceSpan span)
    {
        var storageContext = ResolveNameStorageContext(name, context, span);
        var removed = storageContext.Variables.Remove(name);
        removed |= storageContext.CurrentExecutableFrame?.TryDeleteLocalOrClosure(name) == true;

        return removed;
    }

    internal static ExecutionContext ResolveNameStorageContext(string name, ExecutionContext context, LythonSourceSpan span)
    {
        if (context.ScopeFacts.IsGlobal(name))
        {
            return GetGlobalContext(context);
        }

        if (context.TryGetNonlocalTarget(name, out var nonlocalContext))
        {
            return nonlocalContext;
        }

        return context;
    }

    internal static ExecutionContext GetGlobalContext(ExecutionContext context)
    {
        var current = context;
        while (current.Parent is not null)
        {
            current = current.Parent;
        }

        return current;
    }

    private static object ResolveMember(MemberExpressionSyntax member, ExecutionContext context)
    {
        var target = EvaluateExpression(member.Target, context);
        if (TryResolveRuntimeMember(target, member.MemberName, context, member.Span, out var value))
        {
            return value;
        }

        throw PyMemberAccess.CreateMissingMemberError(target, member.MemberName, member.Span);
    }

    internal static bool TryResolveRuntimeMember(object target, string memberName, ExecutionContext context, LythonSourceSpan span, out object value)
        => PyMemberAccess.TryResolve(target, memberName, context, span, out value);

    private static object EvaluateSubscript(SubscriptExpressionSyntax subscript, ExecutionContext context)
    {
        var target = EvaluateExpression(subscript.Target, context);
        var index = EvaluateExpression(subscript.Index, context);
        if (target is PyDefaultDict defaultDict)
        {
            return defaultDict.GetOrCreate(ValidateDictionaryKey(index, subscript.Span), context, subscript.Span);
        }

        return PyIndexing.ReadIndex(target, index, subscript.Span);
    }

    private static object EvaluateSlice(SliceExpressionSyntax slice, ExecutionContext context)
    {
        var target = EvaluateExpression(slice.Target, context);
        var start = slice.Start is null ? null : EvaluateExpression(slice.Start, context);
        var end = slice.End is null ? null : EvaluateExpression(slice.End, context);
        var step = slice.Step is null ? null : EvaluateExpression(slice.Step, context);

        return PyIndexing.ReadSlice(target, start, end, step, slice.Span);
    }

    internal static bool IsTruthy(object value) => PyTruthiness.IsTruthy(value);

    internal static IEnumerable<object> ToSequence(object value, LythonSourceSpan span)
        => PyIteration.ToSequence(value, span);

    internal static bool AreEqual(object left, object right) => PyEquality.AreEqual(left, right);

    private static int Compare(object left, object right, LythonSourceSpan span) => PyComparison.Compare(left, right, span);

    private static bool Contains(object container, object candidate, LythonSourceSpan span) => PyContainment.Contains(container, candidate, span);

    private static object EvaluateListComprehension(ListComprehensionExpressionSyntax comprehension, ExecutionContext context)
    {
        var result = new PyList([], context.MemoryGovernor, comprehension.Span);
        EvaluateComprehensionClauses(
            comprehension.Clauses,
            0,
            context,
            scope => result.Add(RuntimeValue(EvaluateExpression(comprehension.ItemExpression, scope))));

        context.ObserveCollectionCount(result.Count, comprehension.Span);
        return result;
    }

    private static object EvaluateDictComprehension(DictComprehensionExpressionSyntax comprehension, ExecutionContext context)
    {
        var result = new PyDict(context.MemoryGovernor, comprehension.Span);
        EvaluateComprehensionClauses(
            comprehension.Clauses,
            0,
            context,
            scope =>
            {
                var key = ValidateDictionaryKey(EvaluateExpression(comprehension.KeyExpression, scope), comprehension.KeyExpression.Span, scope.MemoryGovernor);
                result.SetItem(key, RuntimeValue(EvaluateExpression(comprehension.ValueExpression, scope)));
            });

        context.ObserveCollectionCount(result.Count, comprehension.Span);
        return result;
    }

    private static object EvaluateGeneratorExpression(GeneratorExpressionSyntax generator, ExecutionContext context)
    {
        return new PyGeneratorExpression(
            generator.Clauses.Select(clause => new LoweredComprehensionClause(
                clause.Target,
                LoweredScript.LowerStandaloneExpression(clause.Iterable),
                clause.Condition is null ? null : LoweredScript.LowerStandaloneExpression(clause.Condition),
                clause.Span)).ToArray(),
            LoweredScript.LowerStandaloneExpression(generator.ItemExpression),
            context,
            generator.Span);
    }

    private static void EvaluateComprehensionClauses(
        IReadOnlyList<ComprehensionClauseSyntax> clauses,
        int index,
        ExecutionContext context,
        Action<ExecutionContext> emit)
    {
        var clause = clauses[index];
        var iterable = EvaluateExpression(clause.Iterable, context);

        foreach (var item in ToSequence(iterable, clause.Iterable.Span))
        {
            var scope = new ExecutionContext(context);
            AssignLoopTarget(clause.Target, item, clause.Iterable.Span, scope);

            if (clause.Condition is not null &&
                !IsTruthy(EvaluateExpression(clause.Condition, scope)))
            {
                continue;
            }

            if (index + 1 == clauses.Count)
            {
                emit(scope);
            }
            else
            {
                EvaluateComprehensionClauses(clauses, index + 1, scope, emit);
            }
        }
    }

    internal static void AssignLoopTarget(
        LoopTargetSyntax target,
        object value,
        LythonSourceSpan span,
        ExecutionContext context)
    {
        switch (target)
        {
            case LoopNameTargetSyntax name:
                StoreName(name.Name, value, context, span);
                return;
            case LoopTupleTargetSyntax tuple:
                var values = MaterializeSequenceForUnpacking(value, span);
                if (values.Length != tuple.Items.Count)
                {
                    throw new LythonRuntimeException("ValueError", "unpacking assignment has the wrong number of values", span);
                }

                for (var i = 0; i < tuple.Items.Count; i++)
                {
                    AssignLoopTarget(tuple.Items[i], values[i], span, context);
                }

                return;
            default:
                throw new InvalidOperationException($"Unsupported loop target type: {target.GetType().Name}");
        }
    }

    private static void AssignTargets(
        IReadOnlyList<UnpackingTargetSyntax> targets,
        object value,
        LythonSourceSpan span,
        ExecutionContext context)
    {
        if (targets.Count == 1 && !targets[0].IsStarred)
        {
            StoreName(targets[0].Name, value, context, span);
            return;
        }

        var values = MaterializeSequenceForUnpacking(value, span);
        var starredIndex = -1;
        for (var i = 0; i < targets.Count; i++)
        {
            if (targets[i].IsStarred)
            {
                starredIndex = i;
                break;
            }
        }

        if (starredIndex < 0)
        {
            if (values.Length != targets.Count)
            {
                throw new LythonRuntimeException("ValueError", "unpacking assignment has the wrong number of values", span);
            }

            for (var i = 0; i < targets.Count; i++)
            {
                StoreName(targets[i].Name, values[i], context, span);
            }

            return;
        }

        var required = targets.Count - 1;
        if (values.Length < required)
        {
            throw new LythonRuntimeException("ValueError", "unpacking assignment has the wrong number of values", span);
        }

        for (var i = 0; i < starredIndex; i++)
        {
            StoreName(targets[i].Name, values[i], context, span);
        }

        var starredCount = values.Length - required;
        var starredItems = new object[starredCount];
        Array.Copy(values, starredIndex, starredItems, 0, starredCount);
        StoreName(targets[starredIndex].Name, new PyList(starredItems, context.MemoryGovernor, span), context, span);

        for (var i = starredIndex + 1; i < targets.Count; i++)
        {
            var offset = values.Length - (targets.Count - i);
            StoreName(targets[i].Name, values[offset], context, span);
        }
    }

    private static object[] MaterializeSequenceForUnpacking(object value, LythonSourceSpan span)
    {
        if (value is object[] array)
        {
            return array;
        }

        if (value is PyTuple tuple)
        {
            return tuple.ToArray();
        }

        if (value is PyList list)
        {
            return list.ToArray();
        }

        var items = new List<object>();
        foreach (var item in ToSequence(value, span))
        {
            items.Add(item);
        }

        return [.. items];
    }

    internal sealed partial class ExecutionContext
    {
        public ExecutionContext(ILythonHost host, LythonRunOptions? options)
        {
            Services = new ExecutionServices(new ExecutionState(host, options));
            var sourcePath = options?.SourcePath is null ? null : PathOps.Normalize(options.SourcePath, host.Cwd);
            SourcePath = sourcePath;
            Frame = new ExecutionFrame(parent: null, CreateBuiltinVariables(sourcePath));
            ParentContext = null;
            FunctionClosureContext = this;
            ScopeFacts = ScopeDirectiveFacts.Empty;
            NonlocalTargets = new Dictionary<string, ExecutionContext>(StringComparer.Ordinal);

            var globals = options?.Globals;
            if (globals is null)
            {
                return;
            }

            foreach (var pair in globals)
            {
                Frame.Variables[pair.Key] = NormalizeRuntimeValue(pair.Value, this);
            }
        }

        public ExecutionContext(ExecutionContext parent)
        {
            Services = parent.Services;
            SourcePath = parent.SourcePath;
            Frame = new ExecutionFrame(parent.Frame, new Dictionary<string, object>(StringComparer.Ordinal));
            ParentContext = parent;
            FunctionClosureContext = this;
            ScopeFacts = ScopeDirectiveFacts.Empty;
            NonlocalTargets = new Dictionary<string, ExecutionContext>(StringComparer.Ordinal);
        }

        public ExecutionContext(ExecutionContext parent, ScopeDirectiveFacts scopeFacts)
        {
            Services = parent.Services;
            SourcePath = parent.SourcePath;
            Frame = new ExecutionFrame(parent.Frame, new Dictionary<string, object>(StringComparer.Ordinal));
            ParentContext = parent;
            FunctionClosureContext = this;
            ScopeFacts = scopeFacts;
            NonlocalTargets = ResolveNonlocalTargets(parent, scopeFacts);
        }

        public ExecutionContext(ExecutionContext template, bool moduleScope, string? sourcePath = null)
        {
            _ = moduleScope;
            Services = template.Services;
            SourcePath = sourcePath;
            Frame = new ExecutionFrame(parent: null, CreateBuiltinVariables(sourcePath));
            ParentContext = null;
            FunctionClosureContext = this;
            ScopeFacts = ScopeDirectiveFacts.Empty;
            NonlocalTargets = new Dictionary<string, ExecutionContext>(StringComparer.Ordinal);
        }

        public ExecutionContext(ExecutionContext parent, bool classBodyScope)
        {
            _ = classBodyScope;
            Services = parent.Services;
            SourcePath = parent.SourcePath;
            Frame = new ExecutionFrame(parent.Frame, new Dictionary<string, object>(StringComparer.Ordinal));
            ParentContext = parent;
            FunctionClosureContext = parent.FunctionClosureContext;
            ScopeFacts = ScopeDirectiveFacts.Empty;
            NonlocalTargets = new Dictionary<string, ExecutionContext>(StringComparer.Ordinal);
        }

        public ExecutionServices Services { get; }

        public ExecutionFrame Frame { get; }

        public string? SourcePath { get; }

        public ExecutionState State => Services.State;

        public ILythonHost Host => Services.Host;

        public ExecutionContext? Parent => ParentContext;

        public ExecutionContext? ParentContext { get; }

        public ExecutionContext FunctionClosureContext { get; }

        internal ScopeDirectiveFacts ScopeFacts { get; }

        private Dictionary<string, ExecutionContext> NonlocalTargets { get; }

        internal ExecutableFrameState? CurrentExecutableFrame { get; private set; }

        public PyType? ImplicitSuperAnchorType { get; private set; }

        public object? ImplicitSuperReceiver { get; private set; }

        public ExecutionLimits Limits => Services.Limits;

        public MemoryGovernor MemoryGovernor => Services.MemoryGovernor;

        public Dictionary<string, object> Variables => Frame.Variables;

        public PyDecimalContext DecimalContext => State.DecimalContext;

        public void SetDecimalContext(PyDecimalContext context) => State.DecimalContext = context;

        internal bool TryGetNonlocalTarget(string name, out ExecutionContext context)
            => NonlocalTargets.TryGetValue(name, out context!);

        private static Dictionary<string, ExecutionContext> ResolveNonlocalTargets(ExecutionContext parent, ScopeDirectiveFacts scopeFacts)
        {
            var targets = new Dictionary<string, ExecutionContext>(StringComparer.Ordinal);
            foreach (var name in scopeFacts.NonlocalNames)
            {
                for (var current = parent; current is not null && current.Parent is not null; current = current.Parent)
                {
                    if (current.ScopeFacts.LocalNames.Contains(name))
                    {
                        targets[name] = current;
                        break;
                    }
                }

                if (!targets.ContainsKey(name))
                {
                    throw RuntimeErrors.NameNotDefined(name, null);
                }
            }

            return targets;
        }

        internal void EnterExecutableSlots(ExecutableFrameState frame) => CurrentExecutableFrame = frame;

        internal void LeaveExecutableSlots(ExecutableFrameState? previous) => CurrentExecutableFrame = previous;

        public void BindImplicitSuper(PyType anchorType, object receiver)
        {
            ImplicitSuperAnchorType = anchorType;
            ImplicitSuperReceiver = receiver;
        }

        public ReadOnlyMemory<byte> ReadTextUtf8(string path, LythonSourceSpan? span)
            => AwaitHost(() => Host.ReadTextUtf8Async(path, Limits.CancellationToken), "read_text", span);

        public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, LythonSourceSpan? span)
            => AwaitHostAsync(() => Host.ReadTextUtf8Async(path, Limits.CancellationToken), "read_text", span);

        public void WriteTextUtf8(string path, ReadOnlyMemory<byte> utf8, LythonSourceSpan? span)
            => AwaitHost(() => Host.WriteTextUtf8Async(path, utf8, Limits.CancellationToken), "write_text", span);

        public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, LythonSourceSpan? span)
            => AwaitHostAsync(() => Host.WriteTextUtf8Async(path, utf8, Limits.CancellationToken), "write_text", span);

        public void AppendTextUtf8(string path, ReadOnlyMemory<byte> utf8, LythonSourceSpan? span)
            => AwaitHost(() => Host.AppendTextUtf8Async(path, utf8, Limits.CancellationToken), "append_text", span);

        public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, LythonSourceSpan? span)
            => AwaitHostAsync(() => Host.AppendTextUtf8Async(path, utf8, Limits.CancellationToken), "append_text", span);

        public ReadOnlyMemory<byte> ReadHostBytes(string path, LythonSourceSpan? span)
            => AwaitHost(() => Host.ReadBytesAsync(path, Limits.CancellationToken), "read_bytes", span);

        public ValueTask<ReadOnlyMemory<byte>> ReadHostBytesAsync(string path, LythonSourceSpan? span)
            => AwaitHostAsync(() => Host.ReadBytesAsync(path, Limits.CancellationToken), "read_bytes", span);

        public void WriteHostBytes(string path, ReadOnlyMemory<byte> payload, LythonSourceSpan? span)
            => AwaitHost(() => Host.WriteBytesAsync(path, payload, Limits.CancellationToken), "write_bytes", span);

        public ValueTask WriteHostBytesAsync(string path, ReadOnlyMemory<byte> payload, LythonSourceSpan? span)
            => AwaitHostAsync(() => Host.WriteBytesAsync(path, payload, Limits.CancellationToken), "write_bytes", span);

        public bool HostExists(string path, LythonSourceSpan? span)
            => AwaitHost(() => Host.ExistsAsync(path, Limits.CancellationToken), "exists", span);

        public ValueTask<bool> HostExistsAsync(string path, LythonSourceSpan? span)
            => AwaitHostAsync(() => Host.ExistsAsync(path, Limits.CancellationToken), "exists", span);

        public IReadOnlyList<string> HostListDir(string path, LythonSourceSpan? span)
            => AwaitHost(() => Host.ListDirAsync(path, Limits.CancellationToken), "listdir", span);

        public ValueTask<IReadOnlyList<string>> HostListDirAsync(string path, LythonSourceSpan? span)
            => AwaitHostAsync(() => Host.ListDirAsync(path, Limits.CancellationToken), "listdir", span);

        public void HostMkDir(string path, LythonSourceSpan? span)
            => AwaitHost(() => Host.MkDirAsync(path, Limits.CancellationToken), "mkdir", span);

        public ValueTask HostMkDirAsync(string path, LythonSourceSpan? span)
            => AwaitHostAsync(() => Host.MkDirAsync(path, Limits.CancellationToken), "mkdir", span);

        public void HostRemove(string path, LythonSourceSpan? span)
            => AwaitHost(() => Host.RemoveAsync(path, Limits.CancellationToken), "remove", span);

        public ValueTask HostRemoveAsync(string path, LythonSourceSpan? span)
            => AwaitHostAsync(() => Host.RemoveAsync(path, Limits.CancellationToken), "remove", span);

        public void HostCopy(string source, string destination, LythonSourceSpan? span)
            => AwaitHost(() => Host.CopyAsync(source, destination, Limits.CancellationToken), "copy", span);

        public ValueTask HostCopyAsync(string source, string destination, LythonSourceSpan? span)
            => AwaitHostAsync(() => Host.CopyAsync(source, destination, Limits.CancellationToken), "copy", span);

        public void HostMove(string source, string destination, LythonSourceSpan? span)
            => AwaitHost(() => Host.MoveAsync(source, destination, Limits.CancellationToken), "move", span);

        public ValueTask HostMoveAsync(string source, string destination, LythonSourceSpan? span)
            => AwaitHostAsync(() => Host.MoveAsync(source, destination, Limits.CancellationToken), "move", span);

        public LythonPathStat HostStat(string path, LythonSourceSpan? span)
            => AwaitHost(() => Host.StatAsync(path, Limits.CancellationToken), "stat", span);

        public ValueTask<LythonPathStat> HostStatAsync(string path, LythonSourceSpan? span)
            => AwaitHostAsync(() => Host.StatAsync(path, Limits.CancellationToken), "stat", span);

        public LythonSubprocessResult RunSubprocess(LythonSubprocessRequest request, LythonSourceSpan? span)
        {
            var runner = Host.SubprocessRunner;
            if (runner is null)
            {
                throw new LythonRuntimeException("RuntimeError", "subprocess is not available in this host.", span);
            }

            return AwaitHost(() => runner.RunAsync(request, Limits.CancellationToken), "subprocess.run", span);
        }

        public ValueTask<LythonSubprocessResult> RunSubprocessAsync(LythonSubprocessRequest request, LythonSourceSpan? span)
        {
            var runner = Host.SubprocessRunner;
            if (runner is null)
            {
                throw new LythonRuntimeException("RuntimeError", "subprocess is not available in this host.", span);
            }

            return AwaitHostAsync(() => runner.RunAsync(request, Limits.CancellationToken), "subprocess.run", span);
        }

        private T AwaitHost<T>(Func<ValueTask<T>> operation, string name, LythonSourceSpan? span)
        {
            try
            {
                var valueTask = operation();
                if (valueTask.IsCompletedSuccessfully)
                {
                    return valueTask.Result;
                }

                var task = valueTask.AsTask();
                if (!task.IsCompleted)
                {
                    throw RuntimeErrors.Runtime($"{name} completed asynchronously; use RunAsync with asynchronous hosts.", span);
                }

                if (task.IsCanceled)
                {
                    throw RuntimeErrors.Runtime("execution canceled", span);
                }

                if (task.IsFaulted)
                {
                    throw task.Exception?.InnerException ?? new InvalidOperationException($"{name} failed.");
                }

                return task.Result;
            }
            catch (OperationCanceledException)
            {
                throw RuntimeErrors.Runtime("execution canceled", span);
            }
            catch (LythonRuntimeException)
            {
                throw;
            }
            catch (NotSupportedException ex) when (ex.Message.Contains("binary file I/O", StringComparison.OrdinalIgnoreCase))
            {
                throw RuntimeErrors.Runtime("host binary file I/O is not available in this host.", span);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                throw RuntimeErrors.Host(name, ex, span);
            }
        }

        private void AwaitHost(Func<ValueTask> operation, string name, LythonSourceSpan? span)
        {
            try
            {
                var task = operation().AsTask();
                if (!task.IsCompleted)
                {
                    throw RuntimeErrors.Runtime($"{name} completed asynchronously; use RunAsync with asynchronous hosts.", span);
                }

                if (task.IsCanceled)
                {
                    throw RuntimeErrors.Runtime("execution canceled", span);
                }

                if (task.IsFaulted)
                {
                    throw task.Exception?.InnerException ?? new InvalidOperationException($"{name} failed.");
                }
            }
            catch (OperationCanceledException)
            {
                throw RuntimeErrors.Runtime("execution canceled", span);
            }
            catch (LythonRuntimeException)
            {
                throw;
            }
            catch (NotSupportedException ex) when (ex.Message.Contains("binary file I/O", StringComparison.OrdinalIgnoreCase))
            {
                throw RuntimeErrors.Runtime("host binary file I/O is not available in this host.", span);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                throw RuntimeErrors.Host(name, ex, span);
            }
        }

        private static async ValueTask<T> AwaitHostAsync<T>(Func<ValueTask<T>> operation, string name, LythonSourceSpan? span)
        {
            try
            {
                return await operation().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw RuntimeErrors.Runtime("execution canceled", span);
            }
            catch (LythonRuntimeException)
            {
                throw;
            }
            catch (NotSupportedException ex) when (ex.Message.Contains("binary file I/O", StringComparison.OrdinalIgnoreCase))
            {
                throw RuntimeErrors.Runtime("host binary file I/O is not available in this host.", span);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                throw RuntimeErrors.Host(name, ex, span);
            }
        }

        private static async ValueTask AwaitHostAsync(Func<ValueTask> operation, string name, LythonSourceSpan? span)
        {
            try
            {
                await operation().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw RuntimeErrors.Runtime("execution canceled", span);
            }
            catch (LythonRuntimeException)
            {
                throw;
            }
            catch (NotSupportedException ex) when (ex.Message.Contains("binary file I/O", StringComparison.OrdinalIgnoreCase))
            {
                throw RuntimeErrors.Runtime("host binary file I/O is not available in this host.", span);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                throw RuntimeErrors.Host(name, ex, span);
            }
        }

        public bool TryGetBuiltinType(string name, out PyType type)
        {
            if (TryGetBuiltin(name, out var value) && value is PyType resolved)
            {
                type = resolved;
                return true;
            }

            type = null!;
            return false;
        }

        public bool TryGetBuiltin(string name, out object value)
        {
            for (var current = this; current is not null; current = current.ParentContext)
            {
                if (current.Frame.Variables.TryGetValue(name, out value!))
                {
                    return true;
                }
            }

            value = null!;
            return false;
        }

        private Dictionary<string, object> CreateBuiltinVariables(string? sourcePath)
        {
            var objectMembers = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["__new__"] = new PyStaticMethod(new ObjectNewMethod()),
                ["__init__"] = new ObjectInitMethod(),
                ["__init_subclass__"] = new PyClassMethod(new ObjectInitSubclassMethod()),
                ["__getattribute__"] = new ObjectGetAttrMethod(),
                ["__setattr__"] = new ObjectSetAttrMethod(),
                ["__delattr__"] = new ObjectDelAttrMethod()
            };
            var objectType = new PyType("object", Array.Empty<PyType>(), objectMembers);
            var typeType = new PyType("type", [objectType], new Dictionary<string, object>(StringComparer.Ordinal));
            objectType.SetMetaType(typeType);
            typeType.SetMetaType(typeType);

            var builtins = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["object"] = objectType,
                ["type"] = typeType,
                ["read_text"] = new BuiltinCallable("read_text", ReadText, ReadTextAsync, ["path"]),
                ["write_text"] = new BuiltinCallable("write_text", WriteText, WriteTextAsync, ["path", "text"]),
                ["append_text"] = new BuiltinCallable("append_text", AppendText, AppendTextAsync, ["path", "text"]),
                ["open"] = new BuiltinCallable("open", Open, OpenAsync, ["path", "mode", "encoding", "newline", "errors"], requiredCount: 1),
                ["print"] = new PrintCallable(),
                ["input"] = new BuiltinCallable("input", Input, InputAsync, ["prompt"], requiredCount: 0),
                ["str"] = new BuiltinCallable("str", Str, ["value"]),
                ["repr"] = new BuiltinCallable("repr", Repr, ["value"]),
                ["len"] = new BuiltinCallable("len", Len),
                ["sorted"] = new BuiltinCallable("sorted", Sorted, SortedAsync, ["iterable", "key", "reverse"], requiredCount: 1),
                ["any"] = new BuiltinCallable("any", Any),
                ["all"] = new BuiltinCallable("all", All),
                ["min"] = new BuiltinCallable("min", Min),
                ["max"] = new BuiltinCallable("max", Max),
                ["sum"] = new BuiltinCallable("sum", Sum, ["iterable", "start"], requiredCount: 1),
                ["range"] = new BuiltinCallable("range", Range),
                ["enumerate"] = new BuiltinCallable("enumerate", Enumerate),
                ["zip"] = new BuiltinCallable("zip", Zip),
                ["next"] = new BuiltinCallable("next", Next, ["iterator", "default"], requiredCount: 1),
                ["exists"] = new BuiltinCallable("exists", Exists, ExistsAsync, ["path"]),
                ["listdir"] = new BuiltinCallable("listdir", ListDir, ListDirAsync, ["path"]),
                ["mkdir"] = new BuiltinCallable("mkdir", MkDir, MkDirAsync, ["path"]),
                ["remove"] = new BuiltinCallable("remove", Remove, RemoveAsync, ["path"]),
                ["copy"] = new BuiltinCallable("copy", Copy, CopyAsync, ["source", "destination"]),
                ["move"] = new BuiltinCallable("move", Move, MoveAsync, ["source", "destination"]),
                ["cwd"] = new BuiltinCallable("cwd", Cwd),
                ["join_path"] = new BuiltinCallable("join_path", JoinPath),
                ["dirname"] = new BuiltinCallable("dirname", DirName, ["path"]),
                ["basename"] = new BuiltinCallable("basename", BaseName, ["path"]),
                ["stat"] = new BuiltinCallable("stat", Stat, StatAsync, ["path"]),
                ["Exception"] = new ExceptionTypeValue("Exception"),
                ["TypeError"] = new ExceptionTypeValue("TypeError"),
                ["ValueError"] = new ExceptionTypeValue("ValueError"),
                ["KeyError"] = new ExceptionTypeValue("KeyError"),
                ["IndexError"] = new ExceptionTypeValue("IndexError"),
                ["RuntimeError"] = new ExceptionTypeValue("RuntimeError"),
                ["AssertionError"] = new ExceptionTypeValue("AssertionError"),
                ["ImportError"] = new ExceptionTypeValue("ImportError"),
                ["NameError"] = new ExceptionTypeValue("NameError"),
                ["AttributeError"] = new ExceptionTypeValue("AttributeError"),
                ["FileNotFoundError"] = new ExceptionTypeValue("FileNotFoundError"),
                ["OSError"] = new ExceptionTypeValue("OSError"),
                ["StopIteration"] = new ExceptionTypeValue("StopIteration"),
                ["ZeroDivisionError"] = new ExceptionTypeValue("ZeroDivisionError"),
                ["NotImplementedError"] = new ExceptionTypeValue("NotImplementedError"),
                ["OverflowError"] = new ExceptionTypeValue("OverflowError"),
                ["SystemExit"] = new ExceptionTypeValue("SystemExit"),
                ["bool"] = new BuiltinCallable("bool", Bool, ["value"]),
                ["int"] = new BuiltinCallable("int", Int, ["value"]),
                ["float"] = new BuiltinCallable("float", Float, ["value"]),
                ["bytes"] = new BuiltinCallable("bytes", Bytes, ["value"], requiredCount: 0),
                ["staticmethod"] = new BuiltinCallable("staticmethod", StaticMethod, ["func"], requiredCount: 1),
                ["classmethod"] = new BuiltinCallable("classmethod", ClassMethod, ["func"], requiredCount: 1),
                ["property"] = new BuiltinCallable("property", Property, ["fget", "fset"], requiredCount: 0),
                ["super"] = new BuiltinCallable("super", Super),
                ["isinstance"] = new BuiltinCallable("isinstance", IsInstance, ["value", "type"], requiredCount: 2),
                ["issubclass"] = new BuiltinCallable("issubclass", IsSubclass, ["type", "base"], requiredCount: 2),
                ["list"] = new BuiltinCallable("list", List, ["iterable"], requiredCount: 0),
                ["tuple"] = new BuiltinCallable("tuple", Tuple, ["iterable"], requiredCount: 0),
                ["dict"] = new BuiltinCallable("dict", Dict, ["iterable"], requiredCount: 0),
                ["set"] = new BuiltinCallable("set", Set, ["iterable"], requiredCount: 0),
            };

            if (!string.IsNullOrWhiteSpace(sourcePath))
            {
                builtins["__file__"] = PyString.FromString(PathOps.Normalize(sourcePath));
            }

            return builtins;
        }

        private static object NormalizeRuntimeValue(object? value, ExecutionContext context)
        {
            return value switch
            {
                null => PyNone.Instance,
                PyNone none => none,
                string text => CreateString(text, context, null),
                byte[] bytes => CreateBytes(bytes.ToArray(), context, null),
                PyBytes bytes => CreateBytes(bytes.ToArray(), context, null),
                PyList list => NormalizePyList(list, context),
                PyTuple tuple => NormalizePyTuple(tuple, context),
                List<object?> list => NormalizeObjectList(list, context),
                object?[] tuple => NormalizeObjectArray(tuple, context),
                PySet set => NormalizePySet(set, context),
                HashSet<object?> set => NormalizeObjectSet(set, context),
                Dictionary<string, object?> dict => NormalizeStringKeyDictionary(dict, context),
                Dictionary<object, object?> dict => NormalizeObjectKeyDictionary(dict, context),
                PyDict dict => NormalizePyDict(dict, context),
                _ => value
            };
        }

        private static PyList NormalizePyList(PyList list, ExecutionContext context)
        {
            var items = new object[list.Count];
            for (var i = 0; i < list.Count; i++)
            {
                items[i] = NormalizeRuntimeValue(list[i], context);
            }

            return new PyList(items, context.MemoryGovernor, null);
        }

        private static PyTuple NormalizePyTuple(PyTuple tuple, ExecutionContext context)
        {
            var items = new object[tuple.Count];
            for (var i = 0; i < tuple.Count; i++)
            {
                items[i] = NormalizeRuntimeValue(tuple[i], context);
            }

            return new PyTuple(items, context.MemoryGovernor, null);
        }

        private static PyList NormalizeObjectList(List<object?> list, ExecutionContext context)
        {
            var items = new object[list.Count];
            for (var i = 0; i < list.Count; i++)
            {
                items[i] = NormalizeRuntimeValue(list[i], context);
            }

            return new PyList(items, context.MemoryGovernor, null);
        }

        private static PyTuple NormalizeObjectArray(object?[] tuple, ExecutionContext context)
        {
            var items = new object[tuple.Length];
            for (var i = 0; i < tuple.Length; i++)
            {
                items[i] = NormalizeRuntimeValue(tuple[i], context);
            }

            return new PyTuple(items, context.MemoryGovernor, null);
        }

        private static PySet NormalizePySet(PySet set, ExecutionContext context)
        {
            var normalized = new PySet(context.MemoryGovernor, null);
            foreach (var item in set)
            {
                normalized.Add(RuntimeValue(NormalizeRuntimeValue(item, context)));
            }

            return normalized;
        }

        private static PySet NormalizeObjectSet(HashSet<object?> set, ExecutionContext context)
        {
            var normalized = new PySet(context.MemoryGovernor, null);
            foreach (var item in set)
            {
                normalized.Add(RuntimeValue(NormalizeRuntimeValue(item, context)));
            }

            return normalized;
        }

        private static PyDict NormalizeStringKeyDictionary(Dictionary<string, object?> dict, ExecutionContext context)
        {
            var normalized = new PyDict(context.MemoryGovernor, null);
            foreach (var pair in dict)
            {
                normalized.SetItem(CreateString(pair.Key, context, null), NormalizeRuntimeValue(pair.Value, context));
            }

            return normalized;
        }

        private static PyDict NormalizeObjectKeyDictionary(Dictionary<object, object?> dict, ExecutionContext context)
        {
            var normalized = new PyDict(context.MemoryGovernor, null);
            foreach (var pair in dict)
            {
                normalized.SetItem(ValidateDictionaryKey(NormalizeRuntimeValue(pair.Key, context), null, context.MemoryGovernor), NormalizeRuntimeValue(pair.Value, context));
            }

            return normalized;
        }

        private static PyDict NormalizePyDict(PyDict dict, ExecutionContext context)
        {
            var normalized = new PyDict(context.MemoryGovernor, null);
            foreach (var pair in dict)
            {
                normalized.SetItem(ValidateDictionaryKey(NormalizeRuntimeValue(pair.Key, context), null, context.MemoryGovernor), NormalizeRuntimeValue(pair.Value, context));
            }

            return normalized;
        }

        public void CheckExecutionBudget(LythonSourceSpan? span) => Services.CheckExecutionBudget(span);

        public void ObserveValue(object value, LythonSourceSpan? span) => Services.ObserveValue(value, span);

        public void RegisterHostCall(LythonSourceSpan? span) => Services.RegisterHostCall(span);

        public void EnterFunctionCall(LythonSourceSpan? span) => Services.EnterFunctionCall(span);

        public void LeaveFunctionCall() => Services.LeaveFunctionCall();

        public void EnterInterpreterFrame(LythonSourceSpan? span) => Services.EnterInterpreterFrame(span);

        public void LeaveInterpreterFrame() => Services.LeaveInterpreterFrame();

        public void ObserveString(PyString text, LythonSourceSpan? span) => Services.ObserveString(text, span);

        public void ObserveCollectionCount(int count, LythonSourceSpan? span) => Services.ObserveCollectionCount(count, span);

        private static long EstimateApproximateStringBytes(PyString text) => 32L + text.Utf8Bytes.Length;

        internal static long EstimateApproximateValueBytes(object value)
        {
            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            return EstimateApproximateValueBytesCore(value, visited);
        }

        private static long EstimateApproximateValueBytesCore(object value, HashSet<object> visited)
        {
            return value switch
            {
                PyNone => 8,
                bool => 8,
                BigInteger integer => RuntimeMemoryEstimates.EstimateBigIntegerBytes(integer),
                double => 16,
                PyString text => EstimateApproximateStringBytes(text),
                string text => EstimateApproximateStringBytes(PyString.FromString(text)),
                PyBytes bytes => PyBytes.EstimateApproximateBytes(bytes.Length),
                PyList list => EstimateApproximateListBytes(list, visited),
                PyTuple tuple => EstimateApproximateTupleBytes(tuple, visited),
                PyDict dict => EstimateApproximateDictionaryBytes(dict, visited),
                PySet set => EstimateApproximateSetBytes(set, visited),
                ReFindAllResult matches => 32 + EstimateApproximateListBytes(matches.Items, visited),
                TextFileHandle handle => 64 + EstimateApproximateStringBytes(PyString.FromString(handle.Path)) + EstimateApproximateStringBytes(PyString.FromString(handle.Mode)),
                DictKeysView or DictValuesView or DictItemsView => 64,
                _ => 64
            };
        }

        private static long EstimateApproximateListBytes(IEnumerable<object> items, HashSet<object> visited)
        {
            long total = 64;
            foreach (var item in items)
            {
                total += 16;
                total += EstimateApproximateNestedValueBytes(item, visited);
            }

            return total;
        }

        private static long EstimateApproximateTupleBytes(PyTuple tuple, HashSet<object> visited)
        {
            long total = 48;
            foreach (var item in tuple)
            {
                total += 16;
                total += EstimateApproximateNestedValueBytes(item, visited);
            }

            return total;
        }

        private static long EstimateApproximateDictionaryBytes(PyDict dict, HashSet<object> visited)
        {
            long total = 96;
            foreach (var pair in dict)
            {
                total += 48;
                total += EstimateApproximateNestedValueBytes(pair.Key, visited);
                total += EstimateApproximateNestedValueBytes(pair.Value, visited);
            }

            return total;
        }

        private static long EstimateApproximateSetBytes(PySet set, HashSet<object> visited)
        {
            long total = 96;
            foreach (var item in set)
            {
                total += 32;
                total += EstimateApproximateNestedValueBytes(item, visited);
            }

            return total;
        }

        private static long EstimateApproximateNestedValueBytes(object value, HashSet<object> visited)
        {
            if (value is PyNone or PyString or string or bool or BigInteger or double)
            {
                return EstimateApproximateValueBytesCore(value, visited);
            }

            if (!visited.Add(value))
            {
                return 0;
            }

            return EstimateApproximateValueBytesCore(value, visited);
        }
    }

    private static object ValidateSetItem(object value, LythonSourceSpan span, MemoryGovernor? governor = null)
    {
        var normalized = value switch
        {
            PyTuple tuple => NormalizeValidatedTuple(tuple, item => ValidateSetItem(item, span, governor), governor, span),
            IPyHashableValue or string or bool or BigInteger or double => value,
            _ => throw RuntimeErrors.SetElementsMustBeHashable(span)
        };

        return EnsureHashableValue(normalized, span, "set elements must be hashable.");
    }

    internal static object ValidateDictionaryKey(object value, LythonSourceSpan? span, MemoryGovernor? governor = null)
    {
        var normalized = value switch
        {
            string text => PyString.FromString(text),
            PyTuple tuple => NormalizeValidatedTuple(tuple, item => ValidateDictionaryKey(item, span, governor), governor, span),
            IPyHashableValue or bool or BigInteger or double => value,
            _ => throw new LythonRuntimeException("TypeError", "dictionary keys must be hashable.", span)
        };

        return EnsureHashableValue(normalized, span, "dictionary keys must be hashable.");
    }

    private static object EnsureHashableValue(object value, LythonSourceSpan? span, string message)
    {
        try
        {
            _ = PyValueComparer.Instance.GetHashCode(value);
            return value;
        }
        catch (InvalidOperationException)
        {
            throw new LythonRuntimeException("TypeError", message, span);
        }
    }

    private static PyTuple NormalizeValidatedTuple(PyTuple tuple, Func<object, object> normalize, MemoryGovernor? governor, LythonSourceSpan? span)
    {
        var items = new object[tuple.Count];
        for (var i = 0; i < tuple.Count; i++)
        {
            items[i] = normalize(tuple[i]);
        }

        return governor is null ? new PyTuple(items) : new PyTuple(items, governor, span);
    }

    private sealed record SortKeyValue(object Value, object Key);

    private static long EstimateObjectArrayBytes(int count) => 32L + (16L * count);

    internal sealed class ExecutionLimits
    {
        private ExecutionLimits()
        {
        }

        public CancellationToken CancellationToken { get; init; }

        public int? MaxExecutionSteps { get; init; }

        public int? MaxRecursionDepth { get; init; }

        public int? MaxHostCalls { get; init; }

        public int? MaxCollectionSize { get; init; }

        public int? MaxStringLength { get; init; }

        public int? MaxHostReadBytes { get; init; }

        public int? MaxStandardOutputBytes { get; init; }

        public int? MaxStandardErrorBytes { get; init; }

        public long? MaxExecutionMemoryBytes { get; init; }

        public const int MaxInterpreterDepth = 512;

        public int ExecutionStepCount { get; set; }

        public int CurrentRecursionDepth { get; set; }

        public int CurrentInterpreterDepth { get; set; }

        public int HostCallCount { get; set; }

        public static ExecutionLimits FromOptions(LythonRunOptions? options)
        {
            var useDefaultLimits = options?.DisableDefaultLimits != true;
            return new ExecutionLimits
            {
                CancellationToken = options?.CancellationToken ?? CancellationToken.None,
                MaxExecutionSteps = PositiveOrDefault(options?.MaxExecutionSteps, useDefaultLimits ? LythonRunOptions.DefaultMaxExecutionSteps : null),
                MaxRecursionDepth = PositiveOrDefault(options?.MaxRecursionDepth, useDefaultLimits ? LythonRunOptions.DefaultMaxRecursionDepth : null),
                MaxHostCalls = PositiveOrDefault(options?.MaxHostCalls, useDefaultLimits ? LythonRunOptions.DefaultMaxHostCalls : null),
                MaxCollectionSize = PositiveOrDefault(options?.MaxCollectionSize, useDefaultLimits ? LythonRunOptions.DefaultMaxCollectionSize : null),
                MaxStringLength = PositiveOrDefault(options?.MaxStringLength, useDefaultLimits ? LythonRunOptions.DefaultMaxStringLength : null),
                MaxHostReadBytes = PositiveOrDefault(options?.MaxHostReadBytes, useDefaultLimits ? LythonRunOptions.DefaultMaxHostReadBytes : null),
                MaxStandardOutputBytes = PositiveOrDefault(options?.MaxStandardOutputBytes, useDefaultLimits ? LythonRunOptions.DefaultMaxStandardOutputBytes : null),
                MaxStandardErrorBytes = PositiveOrDefault(options?.MaxStandardErrorBytes, useDefaultLimits ? LythonRunOptions.DefaultMaxStandardErrorBytes : null),
                MaxExecutionMemoryBytes = PositiveOrDefault(options?.MaxExecutionMemoryBytes, useDefaultLimits ? LythonRunOptions.DefaultMaxExecutionMemoryBytes : null),
            };
        }

        private static int? PositiveOrDefault(int? value, int? defaultValue)
        {
            return value is > 0 ? value : defaultValue;
        }

        private static long? PositiveOrDefault(long? value, long? defaultValue)
        {
            return value is > 0 ? value : defaultValue;
        }
    }

    internal abstract class ControlSignal : Exception
    {
    }

    internal sealed class BreakSignal : ControlSignal
    {
    }

    internal sealed class ContinueSignal : ControlSignal
    {
    }

    internal sealed class ReturnSignal : Exception
    {
        public ReturnSignal(object value)
        {
            Value = value;
        }

        public object Value { get; }
    }
}

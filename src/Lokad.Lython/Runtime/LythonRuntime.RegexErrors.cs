using System.Globalization;
using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static readonly LythonCallableSignature PatternErrorSignature = LythonCallableSignature.Create(
        "PatternError.__init__", ["msg", "pattern", "pos"], requiredCount: 1);
    private static readonly PyString ErrorMessageKey = PyString.FromString("msg");
    private static readonly PyString ErrorPatternKey = PyString.FromString("pattern");
    private static readonly PyString ErrorPositionKey = PyString.FromString("pos");
    private static readonly PyString ErrorLineKey = PyString.FromString("lineno");
    private static readonly PyString ErrorColumnKey = PyString.FromString("colno");
    private static readonly PyString ErrorNumberFormat = PyString.FromString("%d");
    private static readonly PyBytes ErrorByteNewline = new([(byte)'\n']);

    private static PyDict PatternErrorFields(object message, object pattern, object position,
        MemoryGovernor? governor, LythonSourceSpan? span)
    {
        var fields = governor is null ? new PyDict() : new PyDict(governor, span);
        fields.SetItem(ErrorMessageKey, message);
        fields.SetItem(ErrorPatternKey, pattern);
        fields.SetItem(ErrorPositionKey, position);
        fields.SetItem(ErrorLineKey, PyNone.Instance);
        fields.SetItem(ErrorColumnKey, PyNone.Instance);
        return fields;
    }

    private static PyException PatternErrorValue(PyDict fields, object summary,
        MemoryGovernor? governor, LythonSourceSpan? span)
    {
        var args = governor is null ? PyTuple.FromOwnedArray([summary]) : new PyTuple([summary], governor, span);
        var identity = ModuleException("re", "PatternError");
        var value = governor is null ? new PyException(identity, string.Empty, null, args)
            : PyException.CreateOwned(identity, string.Empty, null, governor, span, args);
        // These are Python attributes, not fixed slots: __dict__ aliases, writes
        // and deletion must all observe the same dictionary. Args stay separate.
        value.CustomDict = fields;
        return value;
    }

    internal static LythonRuntimeException CreatePatternErrorFailure(string message, PyString? pattern, int? position,
        MemoryGovernor? governor, CancellationToken token, LythonSourceSpan? span)
    {
        CheckPatternErrorWork(governor, token, span);
        var text = governor is null ? PyString.FromString(message) : PyString.FromString(message, governor, span);
        var fields = PatternErrorFields(text, (object?)pattern ?? PyNone.Instance,
            position is { } p ? new BigInteger(p) : PyNone.Instance, governor, span);
        var summary = text;
        if (pattern is not null && position is { } index)
        {
            var location = PatternErrorLocation(pattern, new BigInteger(index), governor, token, span);
            var column = (long)index - location.LastNewline;
            fields.SetItem(ErrorLineKey, new BigInteger(location.Line));
            fields.SetItem(ErrorColumnKey, new BigInteger(column));
            var builder = governor is null ? new GovernedByteBuilder() : new GovernedByteBuilder(governor, span);
            try
            {
                builder.Append(text);
                builder.AppendAscii(" at position " + index.ToString(CultureInfo.InvariantCulture));
                if (location.HasNewline)
                    builder.AppendAscii(string.Create(CultureInfo.InvariantCulture,
                        $" (line {location.Line}, column {column})"));
                summary = builder.ToPyStringAndRelease();
            }
            finally { builder.Release(); }
        }
        CheckPatternErrorWork(governor, token, span);
        // The CLR control error and guest args borrow this admitted summary.
        var bytes = checked(32L + 2L * summary.Utf8Bytes.Length);
        governor?.Reserve(bytes, span);
        string rendered;
        try { rendered = summary.AsString(); }
        catch { governor?.ReleaseReserved(bytes); throw; }
        governor?.Commit(bytes);
        return new LythonRuntimeException(ModuleException("re", "PatternError"), rendered, span)
        {
            OriginalPythonException = PatternErrorValue(fields, summary, governor, span)
        };
    }

    private static void CheckPatternErrorWork(MemoryGovernor? governor, CancellationToken token, LythonSourceSpan? span)
    {
        governor?.ThrowIfAdmissionFailed();
        if (token.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
    }

    // count/rfind(0, pos) use a clamped slice end; colno uses the ORIGINAL pos.
    // UTF-8 newline bytes cannot occur inside a multibyte scalar.
    private static (long Line, int LastNewline, bool HasNewline) PatternErrorLocation(object pattern, BigInteger position,
        MemoryGovernor? governor, CancellationToken token, LythonSourceSpan? span)
    {
        var text = pattern as PyString;
        var source = text is not null ? text.Utf8Bytes.Span : ((PyBytes)pattern).Bytes;
        var length = text?.Length ?? source.Length;
        var end = position.Sign >= 0 ? position >= length ? length : (int)position
            : position <= -length ? 0 : length + (int)position;
        long line = 1;
        var last = -1;
        var scalar = -1;
        var hasNewline = false;
        for (var offset = 0; offset < source.Length; offset++)
        {
            if ((offset & 1023) == 0) CheckPatternErrorWork(governor, token, span);
            var value = source[offset];
            if (text is null || (value & 0xc0) != 0x80) scalar++;
            if (value != (byte)'\n') continue;
            hasNewline = true;
            if (scalar < end) { line++; last = scalar; }
        }
        return (line, last, hasNewline);
    }

    private static async ValueTask<object> CreatePatternErrorInstanceAsync(CallArgumentValue[] arguments,
        LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        var values = CallBinder.BindNamedArguments(arguments, span, PatternErrorSignature, PythonCallableKind.Function);
        var message = values[0];
        var pattern = values.Length > 1 ? values[1] : PyNone.Instance;
        var position = values.Length > 2 ? values[2] : PyNone.Instance;
        var fields = PatternErrorFields(message, pattern, position, context.MemoryGovernor, span);
        object summary = message;
        if (pattern is not PyNone && position is not PyNone)
        {
            var rendering = new PyRenderingContext(context);
            var text = asynchronous ? await PyRendering.ToInterpolatedPyStringAsync(message, rendering).ConfigureAwait(false)
                : PyRendering.ToInterpolatedPyString(message, rendering);
            var number = await PatternErrorNumberAsync(position, context, span, asynchronous).ConfigureAwait(false);
            var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
            try
            {
                builder.Append(text); builder.AppendAscii(" at position "); builder.Append(number);
                object line, column;
                bool hasNewline;
                if (pattern is PyString or PyBytes)
                {
                    // Native indices have no callbacks. Other index objects are
                    // converted once for count and again for rfind, like Python.
                    var first = await PatternErrorIndexAsync(position, context, span, asynchronous).ConfigureAwait(false);
                    var location = PatternErrorLocation(pattern, first, context.MemoryGovernor, context.State.CancellationToken, span);
                    line = new BigInteger(location.Line);
                    var second = PyNumberOps.TryAsInteger(position, out _) ? first
                        : await PatternErrorIndexAsync(position, context, span, asynchronous).ConfigureAwait(false);
                    var last = first == second ? location.LastNewline
                        : PatternErrorLocation(pattern, second, context.MemoryGovernor, context.State.CancellationToken, span).LastNewline;
                    column = asynchronous
                        ? await EvaluateBinaryOperatorAsync(BinaryOperatorSyntax.Subtract, position, new BigInteger(last), context, span).ConfigureAwait(false)
                        : EvaluateBinaryOperator(BinaryOperatorSyntax.Subtract, position, new BigInteger(last), context, span);
                    hasNewline = location.HasNewline;
                }
                else
                {
                    var newline = ErrorByteNewline;
                    var count = await PatternErrorMemberAsync(pattern, "count", newline, position, context, span, asynchronous).ConfigureAwait(false);
                    line = asynchronous ? await EvaluateBinaryOperatorAsync(BinaryOperatorSyntax.Add, count, BigInteger.One, context, span).ConfigureAwait(false)
                        : EvaluateBinaryOperator(BinaryOperatorSyntax.Add, count, BigInteger.One, context, span);
                    var last = await PatternErrorMemberAsync(pattern, "rfind", newline, position, context, span, asynchronous).ConfigureAwait(false);
                    column = asynchronous ? await EvaluateBinaryOperatorAsync(BinaryOperatorSyntax.Subtract, position, last, context, span).ConfigureAwait(false)
                        : EvaluateBinaryOperator(BinaryOperatorSyntax.Subtract, position, last, context, span);
                    var contains = asynchronous ? await EvaluateBinaryOperatorAsync(BinaryOperatorSyntax.In, newline, pattern, context, span).ConfigureAwait(false)
                        : EvaluateBinaryOperator(BinaryOperatorSyntax.In, newline, pattern, context, span);
                    hasNewline = asynchronous ? await IsTruthyAsync(contains, context, span).ConfigureAwait(false) : IsTruthy(contains, context, span);
                }
                fields.SetItem(ErrorLineKey, line); fields.SetItem(ErrorColumnKey, column);
                if (hasNewline)
                {
                    builder.AppendAscii(" (line ");
                    builder.Append(await PatternErrorNumberAsync(line, context, span, asynchronous).ConfigureAwait(false));
                    builder.AppendAscii(", column ");
                    builder.Append(await PatternErrorNumberAsync(column, context, span, asynchronous).ConfigureAwait(false));
                    builder.Append((byte)')');
                }
                summary = builder.ToPyStringAndRelease();
            }
            finally { builder.Release(); }
        }
        context.CheckExecution(span);
        return PatternErrorValue(fields, summary, context.MemoryGovernor, span);
    }

    private static async ValueTask<PyString> PatternErrorNumberAsync(object value, ExecutionContext context,
        LythonSourceSpan span, bool asynchronous)
    {
        if (value is PyInstance)
            value = asynchronous ? await IntAsync([value], span, context).ConfigureAwait(false) : Int([value], span, context);
        return FormatPercentString(ErrorNumberFormat, value, context, span);
    }

    private static async ValueTask<BigInteger> PatternErrorIndexAsync(object value, ExecutionContext context,
        LythonSourceSpan span, bool asynchronous)
    {
        var indexed = asynchronous ? await ConvertIntegerIndexAsync(value, context, span).ConfigureAwait(false)
            : TryConvertIntegerIndex(value, context, span, out var converted) ? converted : value;
        if (PyNumberOps.TryAsInteger(indexed, out var index)) return index;
        throw RuntimeErrors.Type("'" + RuntimeErrors.OperandTypeName(value) + "' object cannot be interpreted as an integer", span);
    }

    private static async ValueTask<object> PatternErrorMemberAsync(object pattern, string name, object newline, object position,
        ExecutionContext context, LythonSourceSpan span, bool asynchronous)
    {
        var member = asynchronous ? await TryResolveRuntimeMemberAsync(pattern, name, context, span).ConfigureAwait(false)
            : TryResolveRuntimeMember(pattern, name, context, span, out var value) ? (true, value) : (false, (object)PyNone.Instance);
        if (!member.Item1) throw PyMemberAccess.CreateMissingMemberError(pattern, name, span, context);
        CallArgumentValue[] args = [CallArgumentValue.Positional(newline), CallArgumentValue.Positional(BigInteger.Zero), CallArgumentValue.Positional(position)];
        return asynchronous ? await InvokeCallableTargetAsync(member.Item2, span, span, context, () => new(args)).ConfigureAwait(false)
            : InvokeCallableTarget(member.Item2, span, span, context, () => args);
    }
}

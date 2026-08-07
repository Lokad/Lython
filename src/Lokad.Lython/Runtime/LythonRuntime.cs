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

    private static bool AreIdentical(object left, object right)
        => ReferenceEquals(left, right) || left is bool leftBoolean && right is bool rightBoolean && leftBoolean == rightBoolean;

    internal static PyString ReadGovernedHostText(string path, ExecutionContext context, LythonSourceSpan? span)
        => ReadGovernedHostText(path, context, span, TextErrorMode.Strict, TextNewlineMode.TranslateUniversal);

    internal static PyString ReadGovernedHostText(string path, ExecutionContext context, LythonSourceSpan? span, TextErrorMode errors)
        => ReadGovernedHostText(path, context, span, errors, TextNewlineMode.TranslateUniversal);

    internal static PyString ReadGovernedHostText(
        string path,
        ExecutionContext context,
        LythonSourceSpan? span,
        TextErrorMode errors,
        TextNewlineMode newline)
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

        var text = DecodeUtf8Text(utf8, context, span, errors, newline);
        if (text.OwnerMemoryGovernor is null && text.Utf8Bytes.Length > 0)
        {
            text = CreateString(text.AsString(), context, span);
        }
        context.ObserveString(text, span);
        return text;
    }

    internal static ValueTask<PyString> ReadGovernedHostTextAsync(string path, ExecutionContext context, LythonSourceSpan? span)
        => ReadGovernedHostTextAsync(path, context, span, TextErrorMode.Strict, TextNewlineMode.TranslateUniversal);

    internal static ValueTask<PyString> ReadGovernedHostTextAsync(string path, ExecutionContext context, LythonSourceSpan? span, TextErrorMode errors)
        => ReadGovernedHostTextAsync(path, context, span, errors, TextNewlineMode.TranslateUniversal);

    internal static async ValueTask<PyString> ReadGovernedHostTextAsync(
        string path,
        ExecutionContext context,
        LythonSourceSpan? span,
        TextErrorMode errors,
        TextNewlineMode newline)
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

        var text = DecodeUtf8Text(utf8, context, span, errors, newline);
        if (text.OwnerMemoryGovernor is null && text.Utf8Bytes.Length > 0)
        {
            text = CreateString(text.AsString(), context, span);
        }

        context.ObserveString(text, span);
        return text;
    }

    internal static GovernedHostBytes ReadGovernedHostBytes(string path, ExecutionContext context, LythonSourceSpan? span)
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

        return new GovernedHostBytes(payload, context.MemoryGovernor, span);
    }

    internal static async ValueTask<GovernedHostBytes> ReadGovernedHostBytesAsync(string path, ExecutionContext context, LythonSourceSpan? span)
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

        return new GovernedHostBytes(payload, context.MemoryGovernor, span);
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

        ExecutionContext? context = null;
        try
        {
            context = new ExecutionContext(host, options);
            var signal = ExecuteStatements(script.Statements, context);
            if (signal is BreakSignal or ContinueSignal)
            {
                throw RuntimeErrors.TopLevelLoopControl(null);
            }

            return CreateSuccessfulResult(context, null);
        }
        catch (ReturnSignal signal)
        {
            return CreateReturnedResult(signal, context, options);
        }
        catch (LythonRuntimeException ex)
        {
            return CreateRuntimeFailureResult(ex, context);
        }
    }

    private static LythonExecutionResult CreateSuccessfulResult(ExecutionContext context, object? returnValue)
        => new(
            outcome: LythonExecutionOutcome.Succeeded,
            returnValue: returnValue,
            standardOutput: CaptureStandardOutput(context),
            standardError: CaptureStandardError(context),
            exitCode: null,
            diagnostics: Array.Empty<LythonDiagnostic>(),
            failure: null);

    private static LythonExecutionResult CreateReturnedResult(ReturnSignal signal, ExecutionContext? context, LythonRunOptions? options)
    {
        try
        {
            return new LythonExecutionResult(
                outcome: LythonExecutionOutcome.Succeeded,
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
                outcome: LythonExecutionOutcome.RuntimeFailed,
                returnValue: null,
                standardOutput: context is null ? string.Empty : CaptureStandardOutput(context),
                standardError: context is null ? string.Empty : CaptureStandardError(context),
                exitCode: 1,
                diagnostics: Array.Empty<LythonDiagnostic>(),
                failure: new LythonRuntimeFailure("ProjectionError", ex.Message, null, Array.Empty<LythonStackFrame>(), context?.SourcePath));
        }
    }

    private static LythonExecutionResult CreateRuntimeFailureResult(LythonRuntimeException exception, ExecutionContext? context)
    {
        exception.SetSourcePathIfMissing(context?.SourcePath);
        return new LythonExecutionResult(
            outcome: LythonExecutionOutcome.RuntimeFailed,
            returnValue: null,
            standardOutput: context is null ? string.Empty : CaptureStandardOutput(context),
            standardError: context is null ? string.Empty : CaptureStandardError(context),
            exitCode: GetExitCode(exception),
            diagnostics: Array.Empty<LythonDiagnostic>(),
            failure: RuntimeFailureProjection.ToPublicFailure(exception));
    }

    private static string CaptureStandardOutput(ExecutionContext context)
    {
        try
        {
            return Encoding.UTF8.GetString(context.State.StandardOutput.WrittenSpan);
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
            return Encoding.UTF8.GetString(context.State.StandardError.WrittenSpan);
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
            return 1;
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

    public async Task<LythonExecutionResult> RunAsync(
        LoweredScript script,
        ILythonHost host,
        LythonRunOptions? options)
    {

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

            return CreateSuccessfulResult(context, null);
        }
        catch (ReturnSignal signal)
        {
            return CreateReturnedResult(signal, context, options);
        }
        catch (LythonRuntimeException ex)
        {
            return CreateRuntimeFailureResult(ex, context);
        }
    }

}

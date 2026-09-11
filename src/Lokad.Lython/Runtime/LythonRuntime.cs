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
    /// <summary>Normalizes Python's null result and rejects text that bypassed the governed host boundary.</summary>
    internal static object RuntimeValue(object? value)
        => value switch
        {
            null => PyNone.Instance,
            string => throw new InvalidOperationException("Raw CLR strings must be normalized to PyString at the runtime boundary."),
            _ => value
        };

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
        return ReadGovernedHostBytesAfterStat(path, stat, context, span);
    }

    internal static GovernedHostBytes ReadGovernedHostBytesAfterStat(
        string path,
        LythonPathStat stat,
        ExecutionContext context,
        LythonSourceSpan? span)
    {
        // The caller already registered and observed this stat (for example to
        // distinguish missing files from directories); reuse it for the size
        // precheck instead of stating again, then register and perform only
        // the read. The post-read actual-length check still guards against
        // growth between the two host observations.
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
        return await ReadGovernedHostBytesAfterStatAsync(path, stat, context, span).ConfigureAwait(false);
    }

    internal static async ValueTask<GovernedHostBytes> ReadGovernedHostBytesAfterStatAsync(
        string path,
        LythonPathStat stat,
        ExecutionContext context,
        LythonSourceSpan? span)
    {
        // Same reuse contract as the synchronous twin above.
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

            return CreateSuccessfulResult(context, null, options);
        }
        catch (ReturnSignal signal)
        {
            return CreateReturnedResult(signal, context, options);
        }
        catch (LythonRuntimeException ex)
        {
            return CreateRuntimeFailureResult(ex, context, options);
        }
    }

    private static LythonExecutionResult CreateSuccessfulResult(ExecutionContext context, object? returnValue, LythonRunOptions? options)
    {
        // One projection budget covers the return value and both captures.
        // Captures run first so partial output survives a return-value overrun
        // without further allocation past the budget.
        var budget = CreateProjectionBudget(options);
        string standardOutput = string.Empty;
        string standardError = string.Empty;
        try
        {
            standardOutput = CaptureStandardOutput(context, budget);
            standardError = CaptureStandardError(context, budget);
            return AttachPeaks(LythonExecutionResult.Succeeded(
                returnValue: NormalizePublicValue(returnValue, budget),
                standardOutput: standardOutput,
                standardError: standardError,
                diagnostics: Array.Empty<LythonDiagnostic>()), context, budget);
        }
        catch (ProjectionException ex)
        {
            return AttachPeaks(LythonExecutionResult.RuntimeFailed(
                exitCode: 1,
                failure: new LythonRuntimeFailure("ProjectionError", ex.Message, null, Array.Empty<LythonStackFrame>(), context?.SourcePath),
                standardOutput: standardOutput,
                standardError: standardError,
                diagnostics: Array.Empty<LythonDiagnostic>()), context, budget);
        }
    }

    private static LythonExecutionResult CreateReturnedResult(ReturnSignal signal, ExecutionContext? context, LythonRunOptions? options)
    {
        var budget = CreateProjectionBudget(options);
        string standardOutput = string.Empty;
        string standardError = string.Empty;
        try
        {
            standardOutput = CaptureStandardOutput(context, budget);
            standardError = CaptureStandardError(context, budget);
            return AttachPeaks(LythonExecutionResult.Succeeded(
                returnValue: NormalizePublicValue(signal.Value, budget),
                standardOutput: standardOutput,
                standardError: standardError,
                diagnostics: Array.Empty<LythonDiagnostic>()), context, budget);
        }
        catch (ProjectionException ex)
        {
            return AttachPeaks(LythonExecutionResult.RuntimeFailed(
                exitCode: 1,
                failure: new LythonRuntimeFailure("ProjectionError", ex.Message, null, Array.Empty<LythonStackFrame>(), context?.SourcePath),
                standardOutput: standardOutput,
                standardError: standardError,
                diagnostics: Array.Empty<LythonDiagnostic>()), context, budget);
        }
    }

    private static LythonExecutionResult CreateRuntimeFailureResult(LythonRuntimeException exception, ExecutionContext? context, LythonRunOptions? options)
    {
        exception.SetSourcePathIfMissing(context?.SourcePath);
        // Failure details share the run budget with the retained output copies
        // below (MG23): oversized messages truncate with an explicit marker and
        // keep their type, while captures keep whatever partial output fits.
        var budget = CreateProjectionBudget(options);
        string standardOutput = string.Empty;
        string standardError = string.Empty;
        try
        {
            standardOutput = CaptureStandardOutput(context, budget);
            standardError = CaptureStandardError(context, budget);
        }
        catch (ProjectionException)
        {
        }

        LythonRuntimeFailure failure;
        try
        {
            failure = RuntimeFailureProjection.ToPublicFailure(exception, budget, context);
        }
        catch (ProjectionException)
        {
            // Even the truncated minimum overran (captures consumed the budget):
            // keep the original type with empty details, mirroring the success
            // path's fixed ProjectionError fallback allocation.
            failure = new LythonRuntimeFailure(exception.ExceptionType, string.Empty, exception.Span, [], exception.SourcePath);
        }

        return AttachPeaks(
            LythonExecutionResult.RuntimeFailed(
                exitCode: GetExitCode(exception),
                failure: failure,
                standardOutput: standardOutput,
                standardError: standardError,
                diagnostics: Array.Empty<LythonDiagnostic>()),
            context,
            budget);

        static int GetExitCode(LythonRuntimeException exception)
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
    }

    private static LythonExecutionResult AttachPeaks(LythonExecutionResult result, ExecutionContext? context, ProjectionBudget? budget)
    {
        result.PeakExecutionMemoryBytes = context?.State.MemoryGovernor.PeakAccountedBytes ?? 0;
        result.PeakProjectionMemoryBytes = budget?.CurrentBytes ?? 0;
        return result;
    }

    private static string CaptureStandardOutput(ExecutionContext? context, ProjectionBudget? budget)
    {
        if (context is null)
        {
            return string.Empty;
        }

        try
        {
            var written = context.State.StandardOutput.WrittenSpan;
            // UTF-8 bytes upper-bound the decoded UTF-16 units, so reserve
            // before decoding; an overrun surfaces as ProjectionError above.
            // Empty output stays free like other empty values.
            if (written.Length > 0)
            {
                budget?.Reserve(32L + (2L * written.Length));
            }

            return Encoding.UTF8.GetString(written);
        }
        catch (LythonRuntimeException)
        {
            return string.Empty;
        }
    }

    private static string CaptureStandardError(ExecutionContext? context, ProjectionBudget? budget)
    {
        if (context is null)
        {
            return string.Empty;
        }

        try
        {
            var written = context.State.StandardError.WrittenSpan;
            if (written.Length > 0)
            {
                budget?.Reserve(32L + (2L * written.Length));
            }

            return Encoding.UTF8.GetString(written);
        }
        catch (LythonRuntimeException)
        {
            return string.Empty;
        }
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

            return CreateSuccessfulResult(context, null, options);
        }
        catch (ReturnSignal signal)
        {
            return CreateReturnedResult(signal, context, options);
        }
        catch (LythonRuntimeException ex)
        {
            return CreateRuntimeFailureResult(ex, context, options);
        }
    }

}

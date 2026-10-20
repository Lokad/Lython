using System.Text.RegularExpressions;
using Lokad.Lython.Frontend;
using System.Numerics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private readonly ILythonRegexProvider? _regexProvider;
    internal LythonRuntime(ILythonRegexProvider? regexProvider = null) => _regexProvider = regexProvider;

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

    // Hold known acquisition storage across host work, including suspension;
    // other executions draw from the same allowance. Result/copy ownership is
    // admitted before relinquishing this conservative overlap reservation.
    private static MemoryGovernor.TemporaryMemoryReservation? ReserveExecutionMemoryForKnownLength(LythonPathStat stat, long estimateOverhead, ExecutionContext context, LythonSourceSpan? span)
    {
        if (!stat.Exists || !stat.IsFile || stat.Size <= BigInteger.Zero)
        {
            return null;
        }

        var estimate = stat.Size > new BigInteger(long.MaxValue - estimateOverhead)
            ? long.MaxValue - 1
            : (long)stat.Size + estimateOverhead;
        return context.MemoryGovernor.ReserveTemporary(estimate, span);
    }

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
        context.CheckExecution(span);
        var stat = context.HostStat(path, span);

        using var acquisition = ReserveExecutionMemoryForKnownLength(stat, 128L, context, span);
        context.CheckExecution(span);
        var utf8 = context.ReadTextUtf8(path, span);

        var text = DecodeUtf8Text(utf8, context, span, errors, newline);
        if (text.OwnerMemoryGovernor is null && text.Utf8Bytes.Length > 0)
        {
            text = CreateString(text.AsString(), context, span);
        }
        context.CheckExecution(span);
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
        context.CheckExecution(span);
        var stat = await context.HostStatAsync(path, span).ConfigureAwait(false);

        using var acquisition = ReserveExecutionMemoryForKnownLength(stat, 128L, context, span);
        context.CheckExecution(span);
        var utf8 = await context.ReadTextUtf8Async(path, span).ConfigureAwait(false);

        var text = DecodeUtf8Text(utf8, context, span, errors, newline);
        if (text.OwnerMemoryGovernor is null && text.Utf8Bytes.Length > 0)
        {
            text = CreateString(text.AsString(), context, span);
        }

        context.CheckExecution(span);
        return text;
    }

    internal static GovernedHostBytes ReadGovernedHostBytes(string path, ExecutionContext context, LythonSourceSpan? span)
    {
        context.CheckExecution(span);
        var stat = context.HostStat(path, span);
        return ReadGovernedHostBytesAfterStat(path, stat, context, span);
    }

    internal static GovernedHostBytes ReadGovernedHostBytesAfterStat(
        string path,
        LythonPathStat stat,
        ExecutionContext context,
        LythonSourceSpan? span)
    {

        using var acquisition = ReserveExecutionMemoryForKnownLength(stat, 32L, context, span);
        context.CheckExecution(span);
        var payload = context.ReadHostBytes(path, span);

        return new GovernedHostBytes(payload, context.MemoryGovernor, span);
    }

    internal static async ValueTask<GovernedHostBytes> ReadGovernedHostBytesAsync(string path, ExecutionContext context, LythonSourceSpan? span)
    {
        context.CheckExecution(span);
        var stat = await context.HostStatAsync(path, span).ConfigureAwait(false);
        return await ReadGovernedHostBytesAfterStatAsync(path, stat, context, span).ConfigureAwait(false);
    }

    internal static async ValueTask<GovernedHostBytes> ReadGovernedHostBytesAfterStatAsync(
        string path,
        LythonPathStat stat,
        ExecutionContext context,
        LythonSourceSpan? span)
    {

        using var acquisition = ReserveExecutionMemoryForKnownLength(stat, 32L, context, span);
        context.CheckExecution(span);
        var payload = await context.ReadHostBytesAsync(path, span).ConfigureAwait(false);

        return new GovernedHostBytes(payload, context.MemoryGovernor, span);
    }

    internal static PyTuple CreateTuple(int count, Func<int, object> itemFactory, ExecutionContext context, LythonSourceSpan? span)
    {
        if (count == 0)
        {
            return PyTuple.Empty;
        }

        using var preflight9258 = context.MemoryGovernor.ReserveTemporary(PyTuple.EstimateApproximateBytes(count), span);
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

        using var preflight9843 = context.MemoryGovernor.ReserveTemporary(PyTuple.EstimateApproximateBytes(count), span);
        var items = new object[count];
        for (var i = 0; i < count; i++)
        {
            items[i] = await itemFactory(i).ConfigureAwait(false);
        }

        return PyTuple.FromOwnedArray(items, context.MemoryGovernor, span);
    }

    internal static PyBytes CreateBytes(byte[] bytes, ExecutionContext context, LythonSourceSpan? span)
        => CreateBytes(bytes, context.Services, span);

    internal static PyBytes CreateBytes(byte[] bytes, ExecutionServices services, LythonSourceSpan? span)
    {
        if (bytes.Length == 0)
        {
            return new PyBytes(Array.Empty<byte>());
        }

        var result = new PyBytes(bytes, services.MemoryGovernor, span);
        return result;
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

    // MG25: synchronous execution hops to a dedicated thread with a large
    // stack. Python-level nesting burns kilobytes of CLR stack per level
    // while the depth caps allow hundreds of levels, and even a catchable
    // trip needs room to throw and unwind hundreds of live frames; on small
    // host stacks neither the caps nor the probe backstop can fit. The
    // dedicated reserve makes the caps (and their deterministic errors)
    // reachable on every host, keeps funded depths working, and keeps
    // behavior identical across 1MB/8MB host stacks. 16MB covers the worst
    // measured level cost (order 12KB, Debug-fat) through both caps several
    // times over with unwind room to spare. The async entry below hops the
    // same way for its synchronous prefix. Unexpected failures marshal
    // back with their original stack preserved.
    private const int SyncExecutionStackBytes = 16 * 1024 * 1024;
    private static readonly ExecutionThreads ExecutionWorkers = new(
        SyncExecutionStackBytes, Math.Clamp(Environment.ProcessorCount, 1, 8), TimeSpan.FromSeconds(30));

    private static LythonExecutionResult RunOnDedicatedStack(Func<LythonExecutionResult> runner)
    {
        return ExecutionWorkers.Run(runner);
    }

    // Async counterpart of the dedicated-stack hop above: the synchronous
    // prefix (argument binding, the first interpreter frames, and any deep
    // recursion that never genuinely yields) runs with the large reserve,
    // while continuations after genuine host yields resume on pool threads.
    // Those hops start shallow with the persisted counters intact, so the
    // caps keep bounding them. At the first incomplete await the startup
    // worker returns to the bounded idle pool instead of parking through I/O.
    // Scheduling never blocks the caller through a CPU-only prefix; completion
    // catches prefix/continuation failures without pumping the caller's context.
    internal static Task<LythonExecutionResult> RunAsyncOnDedicatedStack(Func<Task<LythonExecutionResult>> runner)
        => ExecutionWorkers.RunAsync(runner);

    private static LythonExecutionResult CreateSuccessfulResult(ExecutionContext context, object? returnValue, LythonRunOptions? options)
    {
        if (context.MemoryGovernor.AdmissionFailure is { } admissionFailure)
            return CreateRuntimeFailureResult(admissionFailure, context, options);
        // One projection budget covers the return value and both captures.
        // Captures run first so partial output survives a return-value overrun
        // without further allocation past the budget.
        ProjectionBudget? budget = null;
        string standardOutput = string.Empty;
        string standardError = string.Empty;
        try
        {
            budget = CreateProjectionBudget(options);
            standardOutput = CaptureStandardOutput(context, budget);
            standardError = CaptureStandardError(context, budget);
            return AttachAccounting(LythonExecutionResult.Succeeded(
                returnValue: NormalizePublicValue(returnValue, budget),
                standardOutput: standardOutput,
                standardError: standardError,
                diagnostics: Array.Empty<LythonDiagnostic>()), context, budget);
        }
        catch (ProjectionException ex)
        {
            return CreateProjectionFailureResult(ex, context, budget, standardOutput, standardError);
        }
    }

    private static LythonExecutionResult CreateReturnedResult(ReturnSignal signal, ExecutionContext? context, LythonRunOptions? options)
    {
        if (context?.MemoryGovernor.AdmissionFailure is { } admissionFailure)
            return CreateRuntimeFailureResult(admissionFailure, context, options);
        ProjectionBudget? budget = null;
        string standardOutput = string.Empty;
        string standardError = string.Empty;
        try
        {
            budget = CreateProjectionBudget(options);
            standardOutput = CaptureStandardOutput(context, budget);
            standardError = CaptureStandardError(context, budget);
            return AttachAccounting(LythonExecutionResult.Succeeded(
                returnValue: NormalizePublicValue(signal.Value, budget),
                standardOutput: standardOutput,
                standardError: standardError,
                diagnostics: Array.Empty<LythonDiagnostic>()), context, budget);
        }
        catch (ProjectionException ex)
        {
            return CreateProjectionFailureResult(ex, context, budget, standardOutput, standardError);
        }
    }

    private static LythonExecutionResult CreateProjectionFailureResult(
        ProjectionException exception, ExecutionContext? context, ProjectionBudget? budget,
        string standardOutput, string standardError)
    {
        var refusal = context?.MemoryGovernor.AdmissionFailure;
        var failure = refusal is null
            ? new LythonRuntimeFailure("ProjectionError", exception.Message, null, [], context?.SourcePath)
                { Attribution = exception.Attribution }
            : new LythonRuntimeFailure(refusal.ExceptionType, refusal.Message, refusal.Span, [], context?.SourcePath)
                { Attribution = refusal.Attribution };
        return AttachAccounting(LythonExecutionResult.RuntimeFailed(
            exitCode: 1,
            failure: failure,
            standardOutput: standardOutput,
            standardError: standardError,
            diagnostics: Array.Empty<LythonDiagnostic>()), context, budget);
    }

    internal static LythonExecutionResult CancellationFailure(LythonRunOptions? options)
    {
        var error = RuntimeErrors.Cancelled(null);
        error.SetSourcePathIfMissing(options?.SourcePath);
        return CreateRuntimeFailureResult(error, null, options);
    }

    private static async ValueTask<LythonExecutionResult> CreateRuntimeFailureResultAsync(
        LythonRuntimeException exception, ExecutionContext? context, LythonRunOptions? options)
    {
        context?.State.CloseRegexIterators();
        exception = context?.MemoryGovernor.AdmissionFailure ?? exception;
        var message = RuntimeFailureProjection.RenderFailureMessage(exception, context);
        if (context is not null && !context.Services.State.CancellationRequested)
            await context.Services.State.TryCloseOpenFileWritersAsync(context).ConfigureAwait(false);
        return CreateRuntimeFailureResult(exception, context, options, message);
    }

    private static LythonExecutionResult CreateRuntimeFailureResult(
        LythonRuntimeException exception, ExecutionContext? context, LythonRunOptions? options, string? renderedMessage = null)
    {
        context?.State.CloseRegexIterators();
        exception = context?.MemoryGovernor.AdmissionFailure ?? exception;
        // Diagnostics may run a lazy assertion/key protocol. Finish it before
        // closing their writers/capturing output. The async path already awaited
        // that cleanup and supplies its message. Cancellation skips publication.
        var message = renderedMessage;
        if (message is null)
        {
            message = RuntimeFailureProjection.RenderFailureMessage(exception, context);
            if (context is not null && !context.Services.State.CancellationRequested)
                context.Services.State.TryCloseOpenFileWriters(context);
        }
        if (context?.MemoryGovernor.AdmissionFailure is { } refusal)
        {
            exception = refusal;
            message = refusal.Message;
        }
        exception.SetSourcePathIfMissing(context?.SourcePath);
        // Failure details share the run budget with the retained output copies
        // below (MG23): oversized messages truncate with an explicit marker and
        // keep their type, while captures keep whatever partial output fits.
        ProjectionBudget? budget;
        try { budget = CreateProjectionBudget(options); }
        catch (ProjectionException error)
        {
            return CreateProjectionFailureResult(error, context, null, string.Empty, string.Empty);
        }
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

        if (budget?.AdmissionFailure is { } captureRefusal && GetExitCode(exception) == 0)
            return CreateProjectionFailureResult(captureRefusal, context, budget, standardOutput, standardError);

        LythonRuntimeFailure failure;
        try
        {
            failure = RuntimeFailureProjection.ToPublicFailure(exception, budget, context, message);
        }
        catch (ProjectionException)
        {
            // Even the truncated minimum overran (captures consumed the budget):
            // keep the original type with empty details, mirroring the success
            // path's fixed ProjectionError fallback allocation.
            failure = new LythonRuntimeFailure(exception.ExceptionType, string.Empty, exception.Span, [], exception.SourcePath)
                { Attribution = exception.Attribution };
        }

        if (budget?.AdmissionFailure is { } detailRefusal && GetExitCode(exception) == 0)
            return CreateProjectionFailureResult(detailRefusal, context, budget, standardOutput, standardError);

        return AttachAccounting(
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

    private static LythonExecutionResult AttachAccounting(LythonExecutionResult result, ExecutionContext? context, ProjectionBudget? budget)
    {
        result.PeakExecutionMemoryBytes = context?.State.MemoryGovernor.PeakAccountedBytes ?? 0;
        result.PeakProjectionMemoryBytes = budget?.CurrentBytes ?? 0;
        result.DeniedReservationBytes = context?.State.MemoryGovernor.LastDeniedReservationBytes ?? 0;
        if (context is not null)
        {
            var state = context.State;
            result.Consumption = new(
                state.StandardOutput.Length,
                state.StandardError.Length);
        }
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

    public async Task<LythonExecutionResult> RunAsync(
        ExecutableScript script,
        ILythonHost host,
        LythonRunOptions? options)
    {
        ExecutionContext? context = null;
        try
        {
            // Compiled definitions share the synchronous slot/cell contract.
            // Code-object fallback facts still request mirroring when needed.
            context = new ExecutionContext(host, options, _regexProvider);
            InitializeModuleAnnotations(script.AnnotationInitializationSpan, context);
            // MG25: no initial yield here. Yielding first would abandon the
            // calling thread (in particular the dedicated large stack the
            // public async entry runs on) to the pool before doing any work,
            // so deep recursion that never genuinely yields would nest on a
            // small pool thread instead. Genuine host delays still yield.
            var returned = await ExecuteExecutableCodeObjectAsync(script.EntryPoint, context).ConfigureAwait(false);

            // Successful execution publishes outstanding writers instead of
            // silently discarding accepted writes; the await keeps delayed hosts
            // honest, and a publication failure converts to a failure result below.
            await context.Services.State.CloseOpenFileWritersAsync(context).ConfigureAwait(false);

            if (returned is not null)
            {
                return CreateReturnedResult(new ReturnSignal(returned), context, options);
            }

            return CreateSuccessfulResult(context, null, options);
        }
        catch (ReturnSignal signal)
        {
            // A top-level return still publishes outstanding writers; a
            // publication failure replaces the return with a failure.
            try
            {
                if (context is not null)
                {
                    await context.Services.State.CloseOpenFileWritersAsync(context).ConfigureAwait(false);
                }
            }
            catch (LythonRuntimeException ex)
            {
                return await CreateRuntimeFailureResultAsync(ex, context, options).ConfigureAwait(false);
            }

            return CreateReturnedResult(signal, context, options);
        }
        catch (LythonRuntimeException ex)
        {
            return await CreateRuntimeFailureResultAsync(ex, context, options).ConfigureAwait(false);
        }
    }

}

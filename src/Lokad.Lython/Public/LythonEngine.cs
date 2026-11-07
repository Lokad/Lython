using System.Threading;
using System.Threading.Tasks;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;

namespace Lokad.Lython;

/// <summary>Compiles and executes Python source using Lython's host-mediated runtime.</summary>
public sealed class LythonEngine
{
    private readonly ILythonRegexProvider? _regexProvider;

    /// <summary>Creates an engine using a borrowed Python regex provider, when supplied.</summary>
    /// <remarks>The provider must outlive scripts and executions. No implicit regex dialect is installed.</remarks>
    public LythonEngine(ILythonRegexProvider? regexProvider) => _regexProvider = regexProvider;

    /// <summary>Creates an engine without an implicit regex provider.</summary>
    public LythonEngine() : this(null) { }

    /// <summary>The maximum delimiter nesting accepted by the contained frontend.</summary>
    public const int MaxSyntaxNesting = 512;

    /// <summary>The maximum number of recursively nested unary operators accepted by the contained frontend.</summary>
    public const int MaxUnaryOperatorNesting = 256;

    /// <summary>Compiles source and returns both the reusable script and all frontend diagnostics.</summary>
    public LythonCompiledScript Compile(string source)
        => Compile(source, null);

    /// <summary>
    /// Compiles under a borrowed budget. The script owns its estimate until
    /// disposed, including its bounded host-diagnostic cache. Refused admission
    /// returns an invalid script with a compilation diagnostic.
    /// </summary>
    public LythonCompiledScript Compile(string source, ILythonMemoryBudget? memoryBudget)
        => Compile(source, memoryBudget, default);

    /// <summary>Compiles cooperatively using the current token. Cancellation returns an invalid script whose run reports cancellation.</summary>
    public LythonCompiledScript Compile(string source, ILythonMemoryBudget? memoryBudget, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        var owner = (memoryBudget ?? new LythonMemoryBudget()).CreateScope();
        CompiledRegexPrograms? regexPrograms = null;
        // Rough AST/lowered-code capacity plus the four-entry diagnostic cache.
        // Borrowed source is not copied; this deliberately overestimates.
        var estimate = RuntimeMemoryEstimates.EstimateCompilationBytes(source.Length);
        try
        {
            if (cancellationToken.IsCancellationRequested) return Cancelled();
            if (!owner.TryReserve(estimate, out _))
                return Denied(new(LythonFailureReason.SharedMemoryAllowance, Requested: estimate));
            var governor = new MemoryGovernor(owner);
            regexPrograms = new(governor);
            var compiled = CompileCore(source, governor, _regexProvider, cancellationToken, regexPrograms);
            if (!compiled.IsValid || !regexPrograms.HasPrograms)
            {
                regexPrograms.Dispose();
                regexPrograms = null;
            }
            return compiled.OwnMemoryScope(owner, memoryBudget, regexPrograms);
        }
        catch (LythonRuntimeException error) when (error.Attribution?.Reason == LythonFailureReason.SharedMemoryAllowance)
        {
            regexPrograms?.Dispose();
            return Denied(error.Attribution, error.Span, error.Message);
        }
        catch (LythonRuntimeException error) when (error.Attribution?.Reason == LythonFailureReason.Cancelled)
        {
            regexPrograms?.Dispose();
            return Cancelled();
        }
        catch { regexPrograms?.Dispose(); owner.Dispose(); throw; }

        LythonCompiledScript Cancelled()
        {
            IReadOnlyList<LythonDiagnostic> diagnostics = [new("LA2011", "Compilation was cancelled.", LythonDiagnosticSeverity.Error, null)];
            return new LythonCompiledScript(source, diagnostics, false,
                (_, options) => LythonRuntime.CancellationFailure(options),
                (_, options) => Task.FromResult(LythonRuntime.CancellationFailure(options)))
                .OwnMemoryScope(owner, memoryBudget);
        }

        LythonCompiledScript Denied(LythonFailureAttribution? attribution = null, LythonSourceSpan? span = null,
            string message = "Shared memory budget refused compilation storage.")
        {
            IReadOnlyList<LythonDiagnostic> denied = [new("LA0008",
                message, LythonDiagnosticSeverity.Error, span)];
            LythonExecutionResult Failure() => LythonExecutionResult.CompilationFailed(1, "", "", denied,
                attribution ?? new(LythonFailureReason.SharedMemoryAllowance));
            return new LythonCompiledScript(source, denied, false, (_, _) => Failure(),
                (_, _) => Task.FromResult(Failure())).OwnMemoryScope(owner, memoryBudget);
        }

    }

    private static LythonCompiledScript CompileCore(string source, MemoryGovernor governor, ILythonRegexProvider? regexProvider,
        CancellationToken cancellationToken, CompiledRegexPrograms regexPrograms)
    {
        var frontend = LythonFrontend.Compile(source, governor, regexProvider, cancellationToken, regexPrograms);
        governor.ThrowIfAdmissionFailed();
        PythonTokenization.CheckCancellation(cancellationToken, 0);
        var diagnostics = frontend.Diagnostics;
        var isValid = diagnostics.All(static d => d.Severity != LythonDiagnosticSeverity.Error);
        var script = frontend.Script;
        LoweredScript? loweredScript = null;
        try
        {
            if (isValid && script is not null) loweredScript = LoweredScript.Lower(script);
        }
        catch (GeneratorLoweringException exception)
        {
            diagnostics = diagnostics.Append(new LythonDiagnostic("LA2000", exception.Message, LythonDiagnosticSeverity.Error, exception.Span)).ToArray();
            isValid = false;
        }
        governor.ThrowIfAdmissionFailed();
        PythonTokenization.CheckCancellation(cancellationToken, 0);
        // Invalid source never executes. Valid source always uses executable code;
        // compiler defects must surface instead of selecting another engine.
        var executableScript = loweredScript is null ? null : ExecutableScript.Compile(loweredScript);
        governor.ThrowIfAdmissionFailed();
        PythonTokenization.CheckCancellation(cancellationToken, 0);
        // Only the code object's real lowered consumers survive compilation.
        var retainedRegexPrograms = isValid && regexPrograms.HasPrograms ? regexPrograms : null;

        LythonExecutionResult? CreatePreExecutionFailure()
        {
            if (!isValid)
            {
                return LythonExecutionResult.CompilationFailed(
                    exitCode: 1,
                    standardOutput: string.Empty,
                    standardError: string.Empty,
                    diagnostics: diagnostics,
                    attribution: diagnostics.Any(d => d.Code == "LA2010")
                        ? new(LythonFailureReason.HostCapabilityUnavailable) : new(LythonFailureReason.Compilation));
            }

            return null;
        }

        return new LythonCompiledScript(
            source,
            diagnostics,
            isValid,
            (host, options) =>
            {
                var preExecutionFailure = CreatePreExecutionFailure();
                if (preExecutionFailure is not null)
                {
                    return preExecutionFailure;
                }

                var runtime = new LythonRuntime(regexProvider, retainedRegexPrograms);
                return runtime.Run(executableScript.RequireNotNull(), host, options);
            },
            async (host, options) =>
            {
                var preExecutionFailure = CreatePreExecutionFailure();
                if (preExecutionFailure is not null)
                {
                    return preExecutionFailure;
                }

                var runtime = new LythonRuntime(regexProvider, retainedRegexPrograms);
                return await runtime.RunAsync(executableScript.RequireNotNull(), host, options).ConfigureAwait(false);
            });
    }

    /// <summary>Compiles and synchronously runs source with default options.</summary>
    public LythonExecutionResult Run(
        string source,
        ILythonHost host)
    {
        using var compiled = Compile(source, null);
        return compiled.Run(host);
    }

    /// <summary>Compiles and synchronously runs source with initial globals.</summary>
    public LythonExecutionResult Run(
        string source,
        ILythonHost host,
        IReadOnlyDictionary<string, object?> globals)
    {
        using var compiled = Compile(source, null);
        return compiled.Run(host, new LythonRunOptions { Globals = globals });
    }

    /// <summary>Compiles and synchronously runs source with explicit execution options.</summary>
    public LythonExecutionResult Run(
        string source,
        ILythonHost host,
        LythonRunOptions options)
    {
        using var compiled = Compile(source, options.MemoryBudget, options.CancellationToken);
        return compiled.Run(host, options);
    }

    /// <summary>Compiles and asynchronously runs source with default options.</summary>
    public async Task<LythonExecutionResult> RunAsync(
        string source,
        ILythonHost host)
    {
        using var compiled = Compile(source, null);
        return await compiled.RunAsync(host).ConfigureAwait(false);
    }

    /// <summary>Compiles and asynchronously runs source with an external cancellation token.</summary>
    public async Task<LythonExecutionResult> RunAsync(
        string source,
        ILythonHost host,
        CancellationToken cancellationToken)
    {
        return await RunAsync(source, host, new LythonRunOptions { CancellationToken = cancellationToken }).ConfigureAwait(false);
    }

    /// <summary>Compiles and asynchronously runs source with initial globals.</summary>
    public async Task<LythonExecutionResult> RunAsync(
        string source,
        ILythonHost host,
        IReadOnlyDictionary<string, object?> globals)
    {
        using var compiled = Compile(source, null);
        return await compiled.RunAsync(host, globals).ConfigureAwait(false);
    }

    /// <summary>Compiles and asynchronously runs source with initial globals and cancellation.</summary>
    public async Task<LythonExecutionResult> RunAsync(
        string source,
        ILythonHost host,
        IReadOnlyDictionary<string, object?> globals,
        CancellationToken cancellationToken)
    {
        return await RunAsync(source, host, new LythonRunOptions { Globals = globals, CancellationToken = cancellationToken }).ConfigureAwait(false);
    }

    /// <summary>Compiles and asynchronously runs source with explicit execution options.</summary>
    public async Task<LythonExecutionResult> RunAsync(
        string source,
        ILythonHost host,
        LythonRunOptions options)
    {
        using var compiled = Compile(source, options.MemoryBudget, options.CancellationToken);
        return await compiled.RunAsync(host, options).ConfigureAwait(false);
    }

    /// <summary>Compiles and asynchronously runs source with options and an additional cancellation token.</summary>
    public async Task<LythonExecutionResult> RunAsync(
        string source,
        ILythonHost host,
        LythonRunOptions options,
        CancellationToken cancellationToken)
    {
        var merged = LythonCompiledScript.MergeCancellation(options, cancellationToken, out var linked);
        using (linked)
        {
            using var compiled = Compile(source, merged?.MemoryBudget, merged?.CancellationToken ?? default);
            return await compiled.RunAsync(host, merged ?? new LythonRunOptions()).ConfigureAwait(false);
        }
    }
}

using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;

namespace Lokad.Lython;

/// <summary>Compiles and executes Python source using Lython's host-mediated runtime.</summary>
public sealed class LythonEngine
{
    /// <summary>The maximum source length accepted by the contained frontend.</summary>
    public const int MaxSourceLength = 1_000_000;

    /// <summary>Maximum total characters in distinct expanded private names during one compilation.</summary>
    public const int MaxPrivateNameExpansionLength = 8_000_000;

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
    {
        ArgumentNullException.ThrowIfNull(source);
        var owner = (memoryBudget ?? new LythonMemoryBudget()).CreateScope();
        // Rough AST/lowered-code capacity plus the four-entry diagnostic cache.
        // Borrowed source is not copied; this deliberately overestimates.
        var estimate = RuntimeMemoryEstimates.EstimateCompilationBytes(source.Length);
        try
        {
            if (!owner.TryReserve(estimate, out _))
            {
                IReadOnlyList<LythonDiagnostic> denied = [new("LA0008",
                    "Shared memory budget refused compilation storage.", LythonDiagnosticSeverity.Error, null)];
                LythonExecutionResult Failure() => LythonExecutionResult.CompilationFailed(1, "", "", denied,
                    new(LythonFailureReason.SharedMemoryAllowance));
                return new LythonCompiledScript(source, denied, false, (_, _) => Failure(),
                    (_, _) => Task.FromResult(Failure())).OwnMemoryScope(owner, memoryBudget);
            }
            return CompileCore(source).OwnMemoryScope(owner, memoryBudget);
        }
        catch { owner.Dispose(); throw; }
    }

    private static LythonCompiledScript CompileCore(string source)
    {
        var frontend = LythonFrontend.Compile(source);
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
        // Invalid source never executes. Valid source always uses executable code;
        // compiler defects must surface instead of selecting another engine.
        var executableScript = loweredScript is null ? null : ExecutableScript.Compile(loweredScript);
        // Only the code object's real lowered consumers survive compilation.

        // Host-requirement diagnostics depend on the script plus exactly two
        // host capability flags (standard input and subprocess support), so
        // they are computed once per capability state and shared across
        // invocations. The fingerprint re-reads both flags on every call, so
        // a host that gains capabilities between runs re-analyzes instead of
        // serving a stale denial. The cache lives on the compiled script
        // (at most four small entries) and is safe for concurrent runs.
        var hostRequirementCache = new ConcurrentDictionary<(bool HasStandardInput, bool HasSubprocessRunner), IReadOnlyList<LythonDiagnostic>>();

        IReadOnlyList<LythonDiagnostic> GetHostDiagnostics(ILythonHost host)
        {
            var fingerprint = (host.StandardInput is not null, host.SubprocessRunner is not null);
            return hostRequirementCache.GetOrAdd(fingerprint, _ => StaticAnalyzer.AnalyzeHostRequirements(script!, host));
        }

        LythonExecutionResult? CreatePreExecutionFailure(ILythonHost host)
        {
            if (!isValid)
            {
                return LythonExecutionResult.CompilationFailed(
                    exitCode: 1,
                    standardOutput: string.Empty,
                    standardError: string.Empty,
                    diagnostics: diagnostics);
            }

            if (script is null)
            {
                return null;
            }

            var hostDiagnostics = GetHostDiagnostics(host);
            if (hostDiagnostics.Count == 0)
            {
                return null;
            }

            return LythonExecutionResult.CompilationFailed(
                exitCode: 1,
                standardOutput: string.Empty,
                standardError: string.Empty,
                diagnostics: diagnostics.Concat(hostDiagnostics).ToArray(),
                attribution: new(LythonFailureReason.HostCapabilityUnavailable));
        }

        return new LythonCompiledScript(
            source,
            diagnostics,
            isValid,
            (host, options) =>
            {
                var preExecutionFailure = CreatePreExecutionFailure(host);
                if (preExecutionFailure is not null)
                {
                    return preExecutionFailure;
                }

                var runtime = new LythonRuntime();
                return runtime.Run(executableScript.RequireNotNull(), host, options);
            },
            async (host, options) =>
            {
                var preExecutionFailure = CreatePreExecutionFailure(host);
                if (preExecutionFailure is not null)
                {
                    return preExecutionFailure;
                }

                var runtime = new LythonRuntime();
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
        using var compiled = Compile(source, options.MemoryBudget);
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
        using var compiled = Compile(source, null);
        return await compiled.RunAsync(host, cancellationToken).ConfigureAwait(false);
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
        using var compiled = Compile(source, null);
        return await compiled.RunAsync(host, globals, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Compiles and asynchronously runs source with explicit execution options.</summary>
    public async Task<LythonExecutionResult> RunAsync(
        string source,
        ILythonHost host,
        LythonRunOptions options)
    {
        using var compiled = Compile(source, options.MemoryBudget);
        return await compiled.RunAsync(host, options).ConfigureAwait(false);
    }

    /// <summary>Compiles and asynchronously runs source with options and an additional cancellation token.</summary>
    public async Task<LythonExecutionResult> RunAsync(
        string source,
        ILythonHost host,
        LythonRunOptions options,
        CancellationToken cancellationToken)
    {
        using var compiled = Compile(source, options.MemoryBudget);
        return await compiled.RunAsync(host, options, cancellationToken).ConfigureAwait(false);
    }
}

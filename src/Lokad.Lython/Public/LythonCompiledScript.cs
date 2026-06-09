using System.Threading;
using System.Threading.Tasks;

namespace Lokad.Lython;

public sealed class LythonCompiledScript
{
    private readonly Func<ILythonHost, LythonRunOptions?, LythonExecutionResult> _runner;
    private readonly Func<ILythonHost, LythonRunOptions?, Task<LythonExecutionResult>> _asyncRunner;

    internal LythonCompiledScript(
        string source,
        IReadOnlyList<LythonDiagnostic> diagnostics,
        bool isValid,
        Func<ILythonHost, LythonRunOptions?, LythonExecutionResult> runner,
        Func<ILythonHost, LythonRunOptions?, Task<LythonExecutionResult>> asyncRunner)
    {
        Source = source;
        Diagnostics = diagnostics;
        IsValid = isValid;
        _runner = runner;
        _asyncRunner = asyncRunner;
    }

    public string Source { get; }

    public IReadOnlyList<LythonDiagnostic> Diagnostics { get; }

    public bool IsValid { get; }

    public LythonExecutionResult Run(
        ILythonHost host,
        IReadOnlyDictionary<string, object?>? globals = null)
    {
        return Run(host, new LythonRunOptions { Globals = globals });
    }

    public LythonExecutionResult Run(
        ILythonHost host,
        LythonRunOptions? options)
    {
        ArgumentNullException.ThrowIfNull(host);
        return _runner(host, options);
    }

    public Task<LythonExecutionResult> RunAsync(
        ILythonHost host,
        IReadOnlyDictionary<string, object?>? globals = null,
        CancellationToken cancellationToken = default)
    {
        return RunAsync(
            host,
            new LythonRunOptions
            {
                Globals = globals,
                Args = null,
                SourcePath = null,
                CancellationToken = cancellationToken
            });
    }

    public Task<LythonExecutionResult> RunAsync(
        ILythonHost host,
        LythonRunOptions? options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        var mergedOptions = MergeCancellation(options, cancellationToken, out var linkedCancellation);
        return RunAndDisposeAsync(host, mergedOptions, linkedCancellation);

        async Task<LythonExecutionResult> RunAndDisposeAsync(
            ILythonHost runHost,
            LythonRunOptions? runOptions,
            CancellationTokenSource? linkedCancellationSource)
        {
            try
            {
                return await _asyncRunner(runHost, runOptions).ConfigureAwait(false);
            }
            finally
            {
                linkedCancellationSource?.Dispose();
            }
        }
    }

    private static LythonRunOptions? MergeCancellation(
        LythonRunOptions? options,
        CancellationToken cancellationToken,
        out CancellationTokenSource? linkedCancellation)
    {
        linkedCancellation = null;
        if (!cancellationToken.CanBeCanceled)
        {
            return options;
        }

        var effectiveCancellation = cancellationToken;
        if (options?.CancellationToken.CanBeCanceled == true)
        {
            linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(options.CancellationToken, cancellationToken);
            effectiveCancellation = linkedCancellation.Token;
        }

        return new LythonRunOptions
        {
            Globals = options?.Globals,
            Args = options?.Args,
            Environment = options?.Environment,
            SourcePath = options?.SourcePath,
            CancellationToken = effectiveCancellation,
            DisableDefaultLimits = options?.DisableDefaultLimits ?? false,
            DisableLocalModuleImports = options?.DisableLocalModuleImports ?? false,
            AllowedLocalModules = options?.AllowedLocalModules,
            MaxExecutionSteps = options?.MaxExecutionSteps,
            MaxRecursionDepth = options?.MaxRecursionDepth,
            MaxHostCalls = options?.MaxHostCalls,
            MaxCollectionSize = options?.MaxCollectionSize,
            MaxStringLength = options?.MaxStringLength,
            MaxHostReadBytes = options?.MaxHostReadBytes,
            MaxStandardOutputBytes = options?.MaxStandardOutputBytes,
            MaxStandardErrorBytes = options?.MaxStandardErrorBytes,
            MaxExecutionMemoryBytes = options?.MaxExecutionMemoryBytes,
            MaxProjectionMemoryBytes = options?.MaxProjectionMemoryBytes
        };
    }
}

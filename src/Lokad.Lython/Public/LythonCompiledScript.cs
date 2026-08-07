using System.Threading;
using System.Threading.Tasks;

namespace Lokad.Lython;

/// <summary>Represents compiled Lython source that can be run repeatedly with different hosts or options.</summary>
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

    /// <summary>Gets the original Python source.</summary>
    public string Source { get; }

    /// <summary>Gets diagnostics produced while compiling the source.</summary>
    public IReadOnlyList<LythonDiagnostic> Diagnostics { get; }

    /// <summary>Gets whether the script has no error diagnostics and is eligible to run.</summary>
    public bool IsValid { get; }

    /// <summary>Runs the script synchronously with default options.</summary>
    public LythonExecutionResult Run(
        ILythonHost host)
    {
        return RunCore(host, null);
    }

    /// <summary>Runs the script synchronously with the supplied initial globals.</summary>
    public LythonExecutionResult Run(
        ILythonHost host,
        IReadOnlyDictionary<string, object?> globals)
    {
        return Run(host, new LythonRunOptions { Globals = globals });
    }

    /// <summary>Runs the script synchronously with explicit execution options.</summary>
    public LythonExecutionResult Run(
        ILythonHost host,
        LythonRunOptions options)
    {
        return RunCore(host, options);
    }

    /// <summary>Runs the script asynchronously with default options.</summary>
    public Task<LythonExecutionResult> RunAsync(
        ILythonHost host)
    {
        return RunAsyncCore(host, null, CancellationToken.None);
    }

    /// <summary>Runs the script asynchronously with an external cancellation token.</summary>
    public Task<LythonExecutionResult> RunAsync(
        ILythonHost host,
        CancellationToken cancellationToken)
    {
        return RunAsyncCore(host, null, cancellationToken);
    }

    /// <summary>Runs the script asynchronously with the supplied initial globals.</summary>
    public Task<LythonExecutionResult> RunAsync(
        ILythonHost host,
        IReadOnlyDictionary<string, object?> globals)
    {
        return RunAsync(host, globals, CancellationToken.None);
    }

    /// <summary>Runs the script asynchronously with initial globals and an external cancellation token.</summary>
    public Task<LythonExecutionResult> RunAsync(
        ILythonHost host,
        IReadOnlyDictionary<string, object?> globals,
        CancellationToken cancellationToken)
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

    /// <summary>Runs the script asynchronously with explicit execution options.</summary>
    public Task<LythonExecutionResult> RunAsync(
        ILythonHost host,
        LythonRunOptions options)
    {
        return RunAsyncCore(host, options, CancellationToken.None);
    }

    /// <summary>Runs the script asynchronously with options and an additional cancellation token.</summary>
    public Task<LythonExecutionResult> RunAsync(
        ILythonHost host,
        LythonRunOptions options,
        CancellationToken cancellationToken)
    {
        return RunAsyncCore(host, options, cancellationToken);
    }

    private LythonExecutionResult RunCore(ILythonHost host, LythonRunOptions? options)
    {
        return _runner(host, options);
    }

    private Task<LythonExecutionResult> RunAsyncCore(
        ILythonHost host,
        LythonRunOptions? options,
        CancellationToken cancellationToken)
    {
        var mergedOptions = MergeCancellation(options, cancellationToken, out var linkedCancellation);
        return RunAndDisposeAsync(host, mergedOptions, linkedCancellation);

        static LythonRunOptions? MergeCancellation(
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
}

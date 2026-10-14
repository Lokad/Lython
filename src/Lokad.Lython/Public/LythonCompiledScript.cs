using System.Threading;
using System.Threading.Tasks;
using Lokad.Lython.Runtime;

namespace Lokad.Lython;

/// <summary>Represents compiled Lython source that can be run repeatedly with different hosts or options.</summary>
public sealed class LythonCompiledScript : IDisposable
{
    private Func<ILythonHost, LythonRunOptions?, LythonExecutionResult>? _runner;
    private Func<ILythonHost, LythonRunOptions?, Task<LythonExecutionResult>>? _asyncRunner;
    private ILythonMemoryScope? _memoryScope;
    private ILythonMemoryBudget? _defaultMemoryBudget;
    private string? _source;
    private IReadOnlyList<LythonDiagnostic>? _diagnostics;

    internal LythonCompiledScript(
        string source,
        IReadOnlyList<LythonDiagnostic> diagnostics,
        bool isValid,
        Func<ILythonHost, LythonRunOptions?, LythonExecutionResult> runner,
        Func<ILythonHost, LythonRunOptions?, Task<LythonExecutionResult>> asyncRunner)
    {
        _source = source;
        _diagnostics = diagnostics;
        IsValid = isValid;
        _runner = runner;
        _asyncRunner = asyncRunner;
    }

    /// <summary>Gets the original Python source.</summary>
    public string Source => _source ?? throw new ObjectDisposedException(nameof(LythonCompiledScript));

    /// <summary>Gets diagnostics produced while compiling the source.</summary>
    public IReadOnlyList<LythonDiagnostic> Diagnostics => _diagnostics ?? throw new ObjectDisposedException(nameof(LythonCompiledScript));

    internal LythonCompiledScript OwnMemoryScope(ILythonMemoryScope owner, ILythonMemoryBudget? defaultBudget)
    {
        _memoryScope = owner;
        _defaultMemoryBudget = defaultBudget;
        return this;
    }

    /// <summary>
    /// Releases compiled storage and its estimate. Await all runs and relinquish
    /// extracted diagnostic aliases first; disposal does not cancel active runs.
    /// </summary>
    public void Dispose()
    {
        _runner = null;
        _asyncRunner = null;
        _source = null;
        _diagnostics = null;
        _defaultMemoryBudget = null;
        Interlocked.Exchange(ref _memoryScope, null)?.Dispose();
    }

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
        var runner = _runner ?? throw new ObjectDisposedException(nameof(LythonCompiledScript));
        options ??= new LythonRunOptions();
        var budget = options.MemoryBudget ?? _defaultMemoryBudget ?? new LythonMemoryBudget();
        using var execution = budget.CreateScope();
        var resultOwner = (options.ResultMemoryBudget ?? budget).CreateScope();
        try
        {
            return runner(host, options.WithMemoryOwners(execution, resultOwner)).OwnMemoryScope(resultOwner);
        }
        catch { resultOwner.Dispose(); throw; }
    }

    private Task<LythonExecutionResult> RunAsyncCore(
        ILythonHost host,
        LythonRunOptions? options,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_asyncRunner is null, this);
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

            return options is null
                ? new LythonRunOptions { CancellationToken = effectiveCancellation }
                : options.WithCancellation(effectiveCancellation);
        }

        async Task<LythonExecutionResult> RunAndDisposeAsync(
            ILythonHost runHost,
            LythonRunOptions? runOptions,
            CancellationTokenSource? linkedCancellationSource)
        {
            ILythonMemoryScope? execution = null;
            ILythonMemoryScope? resultOwner = null;
            var returned = false;
            try
            {
                runOptions ??= new LythonRunOptions();
                var budget = runOptions.MemoryBudget ?? _defaultMemoryBudget ?? new LythonMemoryBudget();
                execution = budget.CreateScope();
                resultOwner = (runOptions.ResultMemoryBudget ?? budget).CreateScope();
                // MG25: the synchronous prefix of an async run (including deep
                // recursion that never genuinely yields) executes on the same
                // dedicated large stack as synchronous runs; continuations
                // after genuine yields resume on pool threads with fresh
                // stacks while the persisted counters keep bounding them.
                var effective = runOptions.WithMemoryOwners(execution, resultOwner);
                var runner = _asyncRunner ?? throw new ObjectDisposedException(nameof(LythonCompiledScript));
                var result = await LythonRuntime.RunAsyncOnDedicatedStack(() => runner(runHost, effective)).ConfigureAwait(false);
                returned = true;
                return result.OwnMemoryScope(resultOwner);
            }
            finally
            {
                if (!returned) resultOwner?.Dispose();
                execution?.Dispose();
                linkedCancellationSource?.Dispose();
            }
        }
    }
}

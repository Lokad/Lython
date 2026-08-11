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

    /// <summary>The maximum delimiter nesting accepted by the contained frontend.</summary>
    public const int MaxSyntaxNesting = 512;

    /// <summary>The maximum number of recursively nested unary operators accepted by the contained frontend.</summary>
    public const int MaxUnaryOperatorNesting = 256;

    /// <summary>Compiles source and returns both the reusable script and all frontend diagnostics.</summary>
    public LythonCompiledScript Compile(string source)
    {
        var frontend = LythonFrontend.Compile(source);
        var diagnostics = frontend.Diagnostics;
        var isValid = diagnostics.All(static d => d.Severity != LythonDiagnosticSeverity.Error);
        var script = frontend.Script;
        var loweredScript = script is null ? null : LoweredScript.Lower(script);
        ExecutableScript? executableScript = null;
        if (loweredScript is not null)
        {
            try
            {
                executableScript = ExecutableScript.Compile(loweredScript);
            }
            catch (ExecutableLoweringFallbackException)
            {
                executableScript = null;
            }
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

            var hostDiagnostics = StaticAnalyzer.AnalyzeHostRequirements(script, host);
            if (hostDiagnostics.Count == 0)
            {
                return null;
            }

            return LythonExecutionResult.CompilationFailed(
                exitCode: 1,
                standardOutput: string.Empty,
                standardError: string.Empty,
                diagnostics: diagnostics.Concat(hostDiagnostics).ToArray());
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
                return executableScript is not null
                    ? runtime.Run(executableScript, host, options)
                    : runtime.Run(loweredScript.RequireNotNull(), host, options);
            },
            async (host, options) =>
            {
                var preExecutionFailure = CreatePreExecutionFailure(host);
                if (preExecutionFailure is not null)
                {
                    return preExecutionFailure;
                }

                var runtime = new LythonRuntime();
                return await runtime.RunAsync(loweredScript.RequireNotNull(), host, options).ConfigureAwait(false);
            });
    }

    /// <summary>Compiles and synchronously runs source with default options.</summary>
    public LythonExecutionResult Run(
        string source,
        ILythonHost host)
    {
        return Compile(source).Run(host);
    }

    /// <summary>Compiles and synchronously runs source with initial globals.</summary>
    public LythonExecutionResult Run(
        string source,
        ILythonHost host,
        IReadOnlyDictionary<string, object?> globals)
    {
        return Compile(source).Run(host, new LythonRunOptions { Globals = globals });
    }

    /// <summary>Compiles and synchronously runs source with explicit execution options.</summary>
    public LythonExecutionResult Run(
        string source,
        ILythonHost host,
        LythonRunOptions options)
    {
        return Compile(source).Run(host, options);
    }

    /// <summary>Compiles and asynchronously runs source with default options.</summary>
    public Task<LythonExecutionResult> RunAsync(
        string source,
        ILythonHost host)
    {
        return Compile(source).RunAsync(host);
    }

    /// <summary>Compiles and asynchronously runs source with an external cancellation token.</summary>
    public Task<LythonExecutionResult> RunAsync(
        string source,
        ILythonHost host,
        CancellationToken cancellationToken)
    {
        return Compile(source).RunAsync(host, cancellationToken);
    }

    /// <summary>Compiles and asynchronously runs source with initial globals.</summary>
    public Task<LythonExecutionResult> RunAsync(
        string source,
        ILythonHost host,
        IReadOnlyDictionary<string, object?> globals)
    {
        return Compile(source).RunAsync(host, globals);
    }

    /// <summary>Compiles and asynchronously runs source with initial globals and cancellation.</summary>
    public Task<LythonExecutionResult> RunAsync(
        string source,
        ILythonHost host,
        IReadOnlyDictionary<string, object?> globals,
        CancellationToken cancellationToken)
    {
        return Compile(source).RunAsync(host, globals, cancellationToken);
    }

    /// <summary>Compiles and asynchronously runs source with explicit execution options.</summary>
    public Task<LythonExecutionResult> RunAsync(
        string source,
        ILythonHost host,
        LythonRunOptions options)
    {
        return Compile(source).RunAsync(host, options);
    }

    /// <summary>Compiles and asynchronously runs source with options and an additional cancellation token.</summary>
    public Task<LythonExecutionResult> RunAsync(
        string source,
        ILythonHost host,
        LythonRunOptions options,
        CancellationToken cancellationToken)
    {
        return Compile(source).RunAsync(host, options, cancellationToken);
    }
}

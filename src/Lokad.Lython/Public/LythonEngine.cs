using System.Threading;
using System.Threading.Tasks;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;

namespace Lokad.Lython;

public sealed class LythonEngine
{
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
            catch (NotSupportedException)
            {
                executableScript = null;
            }
        }

        LythonExecutionResult? CreatePreExecutionFailure(ILythonHost host)
        {
            if (!isValid)
            {
                return new LythonExecutionResult(
                    outcome: LythonExecutionOutcome.CompilationFailed,
                    returnValue: null,
                    standardOutput: string.Empty,
                    standardError: string.Empty,
                    exitCode: 1,
                    diagnostics: diagnostics,
                    failure: null);
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

            return new LythonExecutionResult(
                outcome: LythonExecutionOutcome.CompilationFailed,
                returnValue: null,
                standardOutput: string.Empty,
                standardError: string.Empty,
                exitCode: 1,
                diagnostics: diagnostics.Concat(hostDiagnostics).ToArray(),
                failure: null);
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

    public LythonExecutionResult Run(
        string source,
        ILythonHost host)
    {
        return Compile(source).Run(host);
    }

    public LythonExecutionResult Run(
        string source,
        ILythonHost host,
        IReadOnlyDictionary<string, object?> globals)
    {
        return Compile(source).Run(host, new LythonRunOptions { Globals = globals });
    }

    public LythonExecutionResult Run(
        string source,
        ILythonHost host,
        LythonRunOptions options)
    {
        return Compile(source).Run(host, options);
    }

    public Task<LythonExecutionResult> RunAsync(
        string source,
        ILythonHost host)
    {
        return Compile(source).RunAsync(host);
    }

    public Task<LythonExecutionResult> RunAsync(
        string source,
        ILythonHost host,
        CancellationToken cancellationToken)
    {
        return Compile(source).RunAsync(host, cancellationToken);
    }

    public Task<LythonExecutionResult> RunAsync(
        string source,
        ILythonHost host,
        IReadOnlyDictionary<string, object?> globals)
    {
        return Compile(source).RunAsync(host, globals);
    }

    public Task<LythonExecutionResult> RunAsync(
        string source,
        ILythonHost host,
        IReadOnlyDictionary<string, object?> globals,
        CancellationToken cancellationToken)
    {
        return Compile(source).RunAsync(host, globals, cancellationToken);
    }

    public Task<LythonExecutionResult> RunAsync(
        string source,
        ILythonHost host,
        LythonRunOptions options)
    {
        return Compile(source).RunAsync(host, options);
    }

    public Task<LythonExecutionResult> RunAsync(
        string source,
        ILythonHost host,
        LythonRunOptions options,
        CancellationToken cancellationToken)
    {
        return Compile(source).RunAsync(host, options, cancellationToken);
    }
}

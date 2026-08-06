using System.Threading;
using System.Threading.Tasks;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;

namespace Lokad.Lython;

public sealed class LythonEngine
{
    public LythonCompiledScript Compile(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

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

        return new LythonCompiledScript(
            source,
            diagnostics,
            isValid,
            (host, options) =>
            {
                if (!isValid)
                {
                    return new LythonExecutionResult(
                        success: false,
                        returnValue: null,
                        standardOutput: string.Empty,
                        standardError: string.Empty,
                        exitCode: 1,
                        diagnostics: diagnostics,
                        failure: null);
                }

                if (script is not null)
                {
                    var hostDiagnostics = StaticAnalyzer.AnalyzeHostRequirements(script, host);
                    if (hostDiagnostics.Count != 0)
                    {
                        var allDiagnostics = diagnostics.Concat(hostDiagnostics).ToArray();
                        return new LythonExecutionResult(
                            success: false,
                            returnValue: null,
                            standardOutput: string.Empty,
                            standardError: string.Empty,
                            exitCode: 1,
                            diagnostics: allDiagnostics,
                            failure: null);
                    }
                }

                var runtime = new LythonRuntime();
                return executableScript is not null
                    ? runtime.Run(executableScript, host, options)
                    : runtime.Run(loweredScript!, host, options);
            },
            async (host, options) =>
            {
                if (!isValid)
                {
                    return new LythonExecutionResult(
                        success: false,
                        returnValue: null,
                        standardOutput: string.Empty,
                        standardError: string.Empty,
                        exitCode: 1,
                        diagnostics: diagnostics,
                        failure: null);
                }

                if (script is not null)
                {
                    var hostDiagnostics = StaticAnalyzer.AnalyzeHostRequirements(script, host);
                    if (hostDiagnostics.Count != 0)
                    {
                        var allDiagnostics = diagnostics.Concat(hostDiagnostics).ToArray();
                        return new LythonExecutionResult(
                            success: false,
                            returnValue: null,
                            standardOutput: string.Empty,
                            standardError: string.Empty,
                            exitCode: 1,
                            diagnostics: allDiagnostics,
                            failure: null);
                    }
                }

                var runtime = new LythonRuntime();
                return await runtime.RunAsync(loweredScript!, host, options).ConfigureAwait(false);
            });
    }

    public LythonExecutionResult Run(
        string source,
        ILythonHost host,
        IReadOnlyDictionary<string, object?>? globals = null)
    {
        return Compile(source).Run(host, new LythonRunOptions { Globals = globals });
    }

    public LythonExecutionResult Run(
        string source,
        ILythonHost host,
        LythonRunOptions? options)
    {
        return Compile(source).Run(host, options);
    }

    public Task<LythonExecutionResult> RunAsync(
        string source,
        ILythonHost host,
        IReadOnlyDictionary<string, object?>? globals = null,
        CancellationToken cancellationToken = default)
    {
        return Compile(source).RunAsync(host, globals, cancellationToken);
    }

    public Task<LythonExecutionResult> RunAsync(
        string source,
        ILythonHost host,
        LythonRunOptions? options,
        CancellationToken cancellationToken = default)
    {
        return Compile(source).RunAsync(host, options, cancellationToken);
    }
}

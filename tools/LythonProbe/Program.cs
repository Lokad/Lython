using System.Diagnostics;
using System.Text.Json;

using Lokad.Lython;

const string PythonWrapper = """
import contextlib
import io
import json
import sys

source = sys.stdin.read()
stdout = io.StringIO()
stderr = io.StringIO()
failure = None
exit_code = None
try:
    with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
        exec(compile(source, "<probe>", "exec"), {"__name__": "__main__"})
    success = True
except BaseException as exception:
    success = False
    failure = {
        "ExceptionType": type(exception).__name__,
        "Message": str(exception),
    }
    if isinstance(exception, SystemExit):
        exit_code = exception.code if isinstance(exception.code, int) else 1

print(json.dumps({
    "Success": success,
    "StandardOutput": stdout.getvalue(),
    "StandardError": stderr.getvalue(),
    "ExitCode": exit_code,
    "Diagnostics": [],
    "Failure": failure,
}, ensure_ascii=False))
""";

var parsed = ParseArguments(args);
if (parsed.Error is not null)
{
    Console.Error.WriteLine(parsed.Error);
    Console.Error.WriteLine("Run LythonProbe --help for usage.");
    return 2;
}

if (parsed.ShowHelp)
{
    PrintHelp();
    return 0;
}

string[] snippets;
try
{
    snippets = ReadSnippets(parsed);
}
catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"Could not read probe input: {exception.Message}");
    return 2;
}

if (snippets.Length == 0)
{
    Console.Error.WriteLine("No Python source was provided.");
    return 2;
}

var engine = new LythonEngine();
var host = new PureProbeHost();
var exitCode = 0;

for (var index = 0; index < snippets.Length; index++)
{
    var source = snippets[index];
    var lython = RunLython(engine, host, source);
    ProbeResult? python = null;

    if (parsed.ComparePython)
    {
        try
        {
            python = RunPython(parsed.PythonCommand, source);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or JsonException)
        {
            Console.Error.WriteLine($"Could not run CPython: {exception.Message}");
            return 2;
        }
    }

    var matches = python is null || Equivalent(lython, python);
    if (!lython.Success || !matches)
    {
        exitCode = 1;
    }

    if (parsed.JsonOutput || parsed.BatchJson)
    {
        Console.WriteLine(JsonSerializer.Serialize(new ProbeReport(index, lython, python, matches)));
    }
    else
    {
        PrintHumanReport(lython, python, matches);
    }
}

return exitCode;

static ParsedArguments ParseArguments(string[] arguments)
{
    string? code = null;
    string? file = null;
    var batchJson = false;
    var jsonOutput = false;
    var comparePython = false;
    var pythonCommand = "python";

    for (var i = 0; i < arguments.Length; i++)
    {
        switch (arguments[i])
        {
            case "-h" or "--help":
                return new ParsedArguments(true, null, null, false, false, false, pythonCommand, null);
            case "-c" or "--code":
                if (++i >= arguments.Length)
                {
                    return ParsedArguments.Failure("Missing source after -c/--code.");
                }

                code = arguments[i];
                break;
            case "--batch-json":
                batchJson = true;
                break;
            case "--json":
                jsonOutput = true;
                break;
            case "--compare-python":
                comparePython = true;
                break;
            case "--python":
                if (++i >= arguments.Length)
                {
                    return ParsedArguments.Failure("Missing executable after --python.");
                }

                pythonCommand = arguments[i];
                break;
            default:
                if (arguments[i].StartsWith("-", StringComparison.Ordinal))
                {
                    return ParsedArguments.Failure($"Unknown option: {arguments[i]}");
                }

                if (file is not null)
                {
                    return ParsedArguments.Failure("Only one source file may be provided.");
                }

                file = arguments[i];
                break;
        }
    }

    var sourceCount = (code is null ? 0 : 1) + (file is null ? 0 : 1);
    if (sourceCount > 1)
    {
        return ParsedArguments.Failure("Use only one of -c/--code, a source file, or stdin.");
    }

    if (batchJson && sourceCount != 0)
    {
        return ParsedArguments.Failure("--batch-json reads its JSON array from stdin and cannot be combined with -c or a source file.");
    }

    return new ParsedArguments(false, code, file, batchJson, jsonOutput, comparePython, pythonCommand, null);
}

static string[] ReadSnippets(ParsedArguments arguments)
{
    if (arguments.Code is not null)
    {
        return [arguments.Code];
    }

    if (arguments.File is not null)
    {
        return [File.ReadAllText(arguments.File)];
    }

    var input = Console.In.ReadToEnd().TrimStart('\uFEFF');
    if (!arguments.BatchJson)
    {
        return [input];
    }

    return JsonSerializer.Deserialize<string[]>(input)
        ?? throw new JsonException("Expected a JSON array of Python source strings.");
}

static ProbeResult RunLython(LythonEngine engine, ILythonHost host, string source)
{
    LythonExecutionResult result;
    try
    {
        result = engine.Run(source, host, new LythonRunOptions());
    }
    catch (Exception exception)
    {
        return new ProbeResult(
            Success: false,
            StandardOutput: string.Empty,
            StandardError: string.Empty,
            ExitCode: null,
            Diagnostics: [],
            Failure: new ProbeFailure(
                $"CLR:{exception.GetType().FullName}",
                exception.Message));
    }

    return new ProbeResult(
        result.Success,
        result.StandardOutput,
        result.StandardError,
        result.ExitCode,
        result.Diagnostics.Select(d => new ProbeDiagnostic(d.Code, d.Message)).ToArray(),
        result.Failure is null
            ? null
            : new ProbeFailure(result.Failure.ExceptionType, result.Failure.Message));
}

static ProbeResult RunPython(string pythonCommand, string source)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = pythonCommand,
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
    };
    startInfo.ArgumentList.Add("-I");
    startInfo.ArgumentList.Add("-X");
    startInfo.ArgumentList.Add("utf8");
    startInfo.ArgumentList.Add("-c");
    startInfo.ArgumentList.Add(PythonWrapper);

    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException($"Could not start CPython executable '{pythonCommand}'.");
    process.StandardInput.Write(source);
    process.StandardInput.Close();

    var output = process.StandardOutput.ReadToEnd();
    var error = process.StandardError.ReadToEnd();
    process.WaitForExit();

    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException(
            $"CPython probe wrapper exited with {process.ExitCode}: {error.Trim()}");
    }

    return JsonSerializer.Deserialize<ProbeResult>(output)
        ?? throw new JsonException("CPython probe wrapper returned no result.");
}

static bool Equivalent(ProbeResult left, ProbeResult right) =>
    left.Success == right.Success &&
    left.StandardOutput == right.StandardOutput &&
    left.StandardError == right.StandardError &&
    left.ExitCode == right.ExitCode &&
    left.Failure?.ExceptionType == right.Failure?.ExceptionType &&
    left.Failure?.Message == right.Failure?.Message;

static void PrintHumanReport(ProbeResult lython, ProbeResult? python, bool matches)
{
    PrintResult("Lython", lython);
    if (python is not null)
    {
        PrintResult("CPython", python);
        Console.WriteLine(matches ? "Parity: match" : "Parity: DIFFERENT");
    }
}

static void PrintResult(string name, ProbeResult result)
{
    Console.WriteLine($"== {name} ==");
    Console.WriteLine($"Success: {result.Success}");
    if (result.StandardOutput.Length != 0)
    {
        Console.WriteLine("stdout:");
        Console.Write(result.StandardOutput);
        if (!result.StandardOutput.EndsWith('\n'))
        {
            Console.WriteLine();
        }
    }

    if (result.StandardError.Length != 0)
    {
        Console.WriteLine("stderr:");
        Console.Write(result.StandardError);
        if (!result.StandardError.EndsWith('\n'))
        {
            Console.WriteLine();
        }
    }

    foreach (var diagnostic in result.Diagnostics)
    {
        Console.WriteLine($"diagnostic {diagnostic.Code}: {diagnostic.Message}");
    }

    if (result.Failure is not null)
    {
        Console.WriteLine($"failure: {result.Failure.ExceptionType}: {result.Failure.Message}");
    }

    if (result.ExitCode is not null)
    {
        Console.WriteLine($"exit code: {result.ExitCode}");
    }
}

static void PrintHelp()
{
    Console.WriteLine("""
LythonProbe runs small, independent Python snippets through Lython's public API.

Usage:
  LythonProbe -c <source> [--json] [--compare-python]
  LythonProbe <script.py> [--json] [--compare-python]
  <source> | LythonProbe [--json] [--compare-python]
  <json-array> | LythonProbe --batch-json [--compare-python]

Options:
  -c, --code <source>   Run source supplied on the command line.
  --batch-json          Read a JSON array of source strings and emit JSON lines.
  --json                Emit a structured JSON result for a single probe.
  --compare-python      Also run each snippet under isolated local CPython.
  --python <executable> Override the CPython command used for comparison.
  -h, --help            Show this help.

The deterministic probe host supplies fixed clocks but no filesystem, standard
streams, subprocess, or local-import capabilities. Use the xUnit harness for
host-mediated scenarios. --compare-python executes the supplied source in the
local CPython process and should only be used with trusted audit snippets.
""");
}

sealed record ParsedArguments(
    bool ShowHelp,
    string? Code,
    string? File,
    bool BatchJson,
    bool JsonOutput,
    bool ComparePython,
    string PythonCommand,
    string? Error)
{
    public static ParsedArguments Failure(string error) =>
        new(false, null, null, false, false, false, "python", error);
}

sealed record ProbeReport(int Index, ProbeResult Lython, ProbeResult? CPython, bool Matches);

sealed record ProbeResult(
    bool Success,
    string StandardOutput,
    string StandardError,
    int? ExitCode,
    ProbeDiagnostic[] Diagnostics,
    ProbeFailure? Failure);

sealed record ProbeDiagnostic(string Code, string Message);

sealed record ProbeFailure(string ExceptionType, string Message);

sealed class PureProbeHost : ILythonHost
{
    public string Cwd => "/";

    public DateTimeOffset LocalNow => new(2024, 1, 2, 3, 4, 5, TimeSpan.FromHours(1));

    public DateTimeOffset UtcNow => new(2024, 1, 2, 2, 4, 5, TimeSpan.Zero);

    public ValueTask<ReadOnlyMemory<byte>> ReadTextUtf8Async(string path, CancellationToken cancellationToken) =>
        throw Unsupported($"read files: {path}");

    public ValueTask WriteTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken) =>
        throw Unsupported($"write files: {path}");

    public ValueTask AppendTextUtf8Async(string path, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken) =>
        throw Unsupported($"append files: {path}");

    public ValueTask<bool> ExistsAsync(string path, CancellationToken cancellationToken) => ValueTask.FromResult(false);

    public ValueTask<IReadOnlyList<string>> ListDirAsync(string path, CancellationToken cancellationToken) =>
        throw Unsupported($"list directories: {path}");

    public ValueTask MkDirAsync(string path, CancellationToken cancellationToken) =>
        throw Unsupported($"create directories: {path}");

    public ValueTask RemoveAsync(string path, CancellationToken cancellationToken) =>
        throw Unsupported($"remove paths: {path}");

    public ValueTask CopyAsync(string source, string destination, CancellationToken cancellationToken) =>
        throw Unsupported($"copy paths: {source} -> {destination}");

    public ValueTask MoveAsync(string source, string destination, CancellationToken cancellationToken) =>
        throw Unsupported($"move paths: {source} -> {destination}");

    public ValueTask<LythonPathStat> StatAsync(string path, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new LythonPathStat(
            Exists: false,
            IsFile: false,
            IsDir: false,
            Size: 0,
            ModifiedAt: string.Empty));

    private static InvalidOperationException Unsupported(string operation) =>
        new($"Pure probe host cannot {operation}.");
}

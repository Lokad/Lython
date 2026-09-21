using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;

using Lokad.Lython;

// Single deadline for CPython launch-to-drain: audit snippets are small, so a
// generous fixed budget bounds hung children without tuning per snippet.
const int PythonProbeTimeoutSeconds = 60;

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
        code = exception.code
        # Like real CPython: bare/None exits 0, other non-ints exit 1.
        exit_code = int(code) if isinstance(code, int) else (0 if code is None else 1)

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
    var lython = await RunLythonAsync(engine, host, source, parsed);
    ProbeResult? python = null;

    if (parsed.ComparePython)
    {
        try
        {
            python = RunPython(parsed.PythonCommand, source);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or JsonException or TimeoutException or Win32Exception)
        {
            Console.Error.WriteLine($"Could not run CPython: {exception.Message}");
            return 2;
        }
    }

    var matches = python is null || Equivalent(lython, python);
    // A static Lython rejection is a supported-subset boundary, not an exact
    // semantic comparison: label it explicitly while preserving both raw results.
    string? note = matches || python is null || lython.Diagnostics.Length == 0
        ? null
        : "subset-rejection: Lython statically rejected this snippet; CPython ran it.";
    if (!lython.Success || !matches)
    {
        exitCode = 1;
    }

    if (parsed.JsonOutput || parsed.BatchJson)
    {
        Console.WriteLine(JsonSerializer.Serialize(new ProbeReport(index, lython, python, matches, note, python is not null, new ProbeRunOptions(parsed.MaxMemoryBytes, parsed.RunAsync))));
    }
    else
    {
        PrintHumanReport(lython, python, matches, note, parsed);
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
    long? maxMemoryBytes = null;
    var runAsync = false;

    for (var i = 0; i < arguments.Length; i++)
    {
        switch (arguments[i])
        {
            case "-h" or "--help":
                return new ParsedArguments(true, null, null, false, false, false, pythonCommand, null, false, null);
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
            case "--max-memory-bytes":
                if (++i >= arguments.Length)
                {
                    return ParsedArguments.Failure("Missing byte count after --max-memory-bytes.");
                }

                if (!long.TryParse(arguments[i], out var parsedBudget) || parsedBudget <= 0)
                {
                    return ParsedArguments.Failure("Invalid byte count after --max-memory-bytes: expected a positive integer.");
                }

                maxMemoryBytes = parsedBudget;
                break;
            case "--async":
                runAsync = true;
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

    return new ParsedArguments(false, code, file, batchJson, jsonOutput, comparePython, pythonCommand, maxMemoryBytes, runAsync, null);
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

static string TypesetSpan(LythonSourceSpan span) =>
    "line " + span.Line + ", column " + span.Column;

static string TypesetFrame(LythonStackFrame frame) =>
    frame.Span is null ? frame.FunctionName : frame.FunctionName + " (line " + frame.Span.Line + ")";

static async Task<ProbeResult> RunLythonAsync(LythonEngine engine, ILythonHost host, string source, ParsedArguments parsed)
{
    var options = parsed.MaxMemoryBytes is long budget
        ? new LythonRunOptions { MaxExecutionMemoryBytes = budget }
        : new LythonRunOptions();
    LythonExecutionResult result;
    try
    {
        result = parsed.RunAsync
            ? await engine.RunAsync(source, host, options)
            : engine.Run(source, host, options);
    }
    catch (Exception exception)
    {
        return new ProbeResult(
            Success: false,
            StandardOutput: string.Empty,
            StandardError: string.Empty,
            ExitCode: null,
            Diagnostics: [],
            PeakExecutionMemoryBytes: null,
            PeakProjectionMemoryBytes: null,
            DeniedReservationBytes: null,
            Failure: new ProbeFailure(
                $"CLR:{exception.GetType().FullName}",
                exception.Message,
                null,
                null));
    }

    return new ProbeResult(
        result.Success,
        result.StandardOutput,
        result.StandardError,
        result.ExitCode,
        result.Diagnostics.Select(d => new ProbeDiagnostic(d.Code, d.Message)).ToArray(),
        result.PeakExecutionMemoryBytes,
        result.PeakProjectionMemoryBytes,
        result.DeniedReservationBytes,
        result.Failure is null
            ? null
            : new ProbeFailure(
                result.Failure.ExceptionType,
                result.Failure.Message,
                result.Failure.Span is null ? null : TypesetSpan(result.Failure.Span),
                result.Failure.StackTrace.Select(TypesetFrame).ToArray()));
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

    using var process = StartPythonProcess(pythonCommand, startInfo);

    // Drain both streams concurrently: sequential drains can deadlock when the
    // child fills the pipe nobody is reading. The drains and the deadline
    // start BEFORE stdin delivery, so a child that never consumes a
    // pipe-sized input trips the deadline instead of wedging a synchronous
    // write issued first. One deadline covers delivery, completion and both
    // drains; on expiry the owned child process tree is killed.
    var stdoutTask = process.StandardOutput.ReadToEndAsync();
    var stderrTask = process.StandardError.ReadToEndAsync();
    var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(PythonProbeTimeoutSeconds);
    var stdinTask = WriteAndCloseStandardInputAsync(process, source);
    if (!process.WaitForExit(RemainingMs(deadline)))
    {
        KillProcessTree(process);
        throw new TimeoutException($"CPython probe did not complete within {PythonProbeTimeoutSeconds} seconds.");
    }

    if (!Task.WaitAll([stdoutTask, stderrTask, stdinTask], RemainingMs(deadline)))
    {
        KillProcessTree(process);
        throw new TimeoutException($"CPython probe output drain did not complete within {PythonProbeTimeoutSeconds} seconds.");
    }

    process.WaitForExit();
    var output = stdoutTask.Result;
    var error = stderrTask.Result;

    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException(
            $"CPython probe wrapper exited with {process.ExitCode}: {error.Trim()}");
    }

    return JsonSerializer.Deserialize<ProbeResult>(output)
        ?? throw new JsonException("CPython probe wrapper returned no result.");
}

static Process StartPythonProcess(string pythonCommand, ProcessStartInfo startInfo)
{
    try
    {
        return Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start CPython executable '{pythonCommand}'.");
    }
    catch (Win32Exception exception)
    {
        // Missing executables surface as Win32Exception, outside the IO filter.
        throw new InvalidOperationException($"Could not start CPython executable '{pythonCommand}': {exception.Message}", exception);
    }
}

// Asynchronous stdin delivery under the probe deadline. The wrapper may
// exit (or be reaped on timeout) without consuming all input, so a broken
// pipe surfaces here as a quiet completion and never masks the run outcome.
static async Task WriteAndCloseStandardInputAsync(Process process, string source)
{
    try
    {
        await process.StandardInput.WriteAsync(source).ConfigureAwait(false);
        await process.StandardInput.FlushAsync().ConfigureAwait(false);
    }
    catch (Exception exception) when (exception is IOException or InvalidOperationException or OperationCanceledException)
    {
    }
    finally
    {
        try
        {
            process.StandardInput.Close();
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
        }
    }
}

static int RemainingMs(DateTime deadline)
    => (int)Math.Max(0, (deadline - DateTime.UtcNow).TotalMilliseconds);

static void KillProcessTree(Process process)
{
    try
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }
    }
    catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
    {
    }

    process.WaitForExit(5000);
}

static bool Equivalent(ProbeResult left, ProbeResult right) =>
    left.Success == right.Success &&
    left.StandardOutput == right.StandardOutput &&
    left.StandardError == right.StandardError &&
    left.ExitCode == right.ExitCode &&
    left.Failure?.ExceptionType == right.Failure?.ExceptionType &&
    left.Failure?.Message == right.Failure?.Message;

static void PrintHumanReport(ProbeResult lython, ProbeResult? python, bool matches, string? note, ParsedArguments parsed)
{
    PrintResult("Lython", lython);
    if (parsed.RunAsync)
    {
        Console.WriteLine("mode: async");
    }
    if (python is not null)
    {
        PrintResult("CPython", python);
        Console.WriteLine(matches ? "Parity: match" : "Parity: DIFFERENT");
        if (note is not null)
        {
            Console.WriteLine(note);
        }
    }
    else
    {
        Console.WriteLine("Parity: not compared");
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
        if (result.Failure.Span is not null)
        {
            Console.WriteLine($"at {result.Failure.Span}");
        }
        if (result.Failure.Frames is not null)
        {
            foreach (var frame in result.Failure.Frames)
            {
                Console.WriteLine($"  at {frame}");
            }
        }
    }
    if (result.PeakExecutionMemoryBytes is not null)
    {
        Console.WriteLine($"peak execution memory: {result.PeakExecutionMemoryBytes} bytes");
    }
    if (result.PeakProjectionMemoryBytes is not null)
    {
        Console.WriteLine($"peak projection memory: {result.PeakProjectionMemoryBytes} bytes");
    }
    if (result.DeniedReservationBytes is not null && result.DeniedReservationBytes != 0)
    {
        Console.WriteLine($"denied reservation: {result.DeniedReservationBytes} bytes");
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
  LythonProbe -c <source> [--json] [--compare-python] [--max-memory-bytes N] [--async]
  LythonProbe <script.py> [--json] [--compare-python] [--max-memory-bytes N] [--async]
  <source> | LythonProbe [--json] [--compare-python] [--max-memory-bytes N] [--async]
  <json-array> | LythonProbe --batch-json [--compare-python] [--max-memory-bytes N] [--async]

Options:
  -c, --code <source>   Run source supplied on the command line.
  --batch-json          Read a JSON array of source strings and emit JSON lines.
  --json                Emit a structured JSON result for a single probe.
  --compare-python      Also run each snippet under isolated local CPython.
  --python <executable> Override the CPython command used for comparison.
  --max-memory-bytes N  Cap accounted execution memory (bytes, positive integer).
  --async               Run snippets through the asynchronous execution path.
  -h, --help            Show this help.

Exit codes:
  0  Every snippet succeeded (and matched CPython when compared).
  1  A snippet failed, mismatched, or exceeded its budget.
  2  Usage, input, batch, or tool error (including an invalid budget).

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
    long? MaxMemoryBytes,
    bool RunAsync,
    string? Error)
{
    public static ParsedArguments Failure(string error) =>
        new(false, null, null, false, false, false, "python", null, false, error);
}

sealed record ProbeReport(int Index, ProbeResult Lython, ProbeResult? CPython, bool Matches, string? Note, bool Compared, ProbeRunOptions Options);

sealed record ProbeResult(
    bool Success,
    string StandardOutput,
    string StandardError,
    int? ExitCode,
    ProbeDiagnostic[] Diagnostics,
    long? PeakExecutionMemoryBytes,
    long? PeakProjectionMemoryBytes,
    long? DeniedReservationBytes,
    ProbeFailure? Failure);

sealed record ProbeRunOptions(long? MaxMemoryBytes, bool Async);

sealed record ProbeDiagnostic(string Code, string Message);

sealed record ProbeFailure(string ExceptionType, string Message, string? Span, string[]? Frames);

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
            kind: LythonPathKind.Missing,
            size: 0,
            modifiedAt: null));

    private static InvalidOperationException Unsupported(string operation) =>
        new($"Pure probe host cannot {operation}.");
}

using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class SubprocessModule : PyModule
    {
        public static readonly SubprocessModule Instance = new();

        private SubprocessModule() : base("subprocess")
        {
        }

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "run" => new BuiltinCallable(LythonKnownCallableSignatures.SubprocessRun, Run),
                _ => null!
            };

            return value is not null;
        }
    }

    private static object Run(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (context.Host.SubprocessRunner is null)
        {
            throw new LythonRuntimeException("RuntimeError", "subprocess is not available in this host.", span);
        }

        if (arguments.Length is < 1 or > 6)
        {
            throw new LythonRuntimeException("TypeError", "subprocess.run(args[, input][, cwd][, timeout][, check][, capture_output]) expects between one and six arguments.", span);
        }

        var args = ParseSubprocessArgs(arguments[0], span);
        var stdin = arguments.Length >= 2 && arguments[1] is not PyNone && arguments[1] is not null
            ? PyStringOps.TryAsString(arguments[1], out var input)
                ? input.Utf8Bytes
                : throw new LythonRuntimeException("TypeError", "subprocess.run(..., input=...) expects a string or None.", span)
            : ReadOnlyMemory<byte>.Empty;
        var cwd = arguments.Length >= 3 && arguments[2] is not PyNone && arguments[2] is not null
            ? PyStringOps.TryAsString(arguments[2], out var cwdText)
                ? cwdText.AsString()
                : throw new LythonRuntimeException("TypeError", "subprocess.run(..., cwd=...) expects a string or None.", span)
            : null;
        int? timeout = arguments.Length >= 4 && arguments[3] is not PyNone && arguments[3] is not null
            ? ParseOptionalInt(arguments[3], "subprocess.run(..., timeout=...)", span)
            : null;
        var check = arguments.Length >= 5 && arguments[4] is not PyNone && arguments[4] is not null
            ? arguments[4] is bool boolValue
                ? boolValue
                : throw new LythonRuntimeException("TypeError", "subprocess.run(..., check=...) expects a bool or None.", span)
            : false;
        _ = arguments.Length >= 6 && arguments[5] is not PyNone && arguments[5] is not null
            ? arguments[5] is bool captureOutput
                ? captureOutput
                : throw new LythonRuntimeException("TypeError", "subprocess.run(..., capture_output=...) expects a bool or None.", span)
            : false;

        context.RegisterHostCall(span);
        var result = context.RunSubprocess(new LythonSubprocessRequest(
            Args: args,
            Cwd: cwd,
            Environment: null,
            StandardInputUtf8: stdin,
            TimeoutMilliseconds: timeout,
            MaxOutputBytes: context.Limits.MaxStringLength),
            span);

        var stdout = result.StandardOutputUtf8.Length == 0
            ? PyString.Empty
            : PyString.FromUtf8(result.StandardOutputUtf8, context.MemoryGovernor, span);
        var stderr = result.StandardErrorUtf8.Length == 0
            ? PyString.Empty
            : PyString.FromUtf8(result.StandardErrorUtf8, context.MemoryGovernor, span);
        context.ObserveString(stdout, span);
        context.ObserveString(stderr, span);

        var completed = new PyCompletedProcess(new BigInteger(result.ReturnCode), stdout, stderr);
        if (check && result.ReturnCode != 0)
        {
            throw new LythonRuntimeException("RuntimeError", $"subprocess.run(...) failed with return code {result.ReturnCode}.", span, payload: completed);
        }

        return completed;
    }

    private static IReadOnlyList<string> ParseSubprocessArgs(object value, LythonSourceSpan span)
    {
        if (PyStringOps.TryAsString(value, out _))
        {
            throw new LythonRuntimeException("TypeError", "subprocess.run(args) expects an iterable of strings, not a single string.", span);
        }

        var items = new List<string>();
        foreach (var item in ToSequence(value, span))
        {
            if (!PyStringOps.TryAsString(item, out var text))
            {
                throw new LythonRuntimeException("TypeError", "subprocess.run(args) expects an iterable of strings.", span);
            }

            items.Add(text.AsString());
        }

        if (items.Count == 0)
        {
            throw new LythonRuntimeException("ValueError", "subprocess.run(args) expects at least one command part.", span);
        }

        return items;
    }

    private static int ParseOptionalInt(object value, string owner, LythonSourceSpan span)
    {
        return value switch
        {
            int integer => integer,
            BigInteger integer when integer >= int.MinValue && integer <= int.MaxValue => (int)integer,
            _ => throw new LythonRuntimeException("TypeError", $"{owner} expects an integer.", span)
        };
    }
}

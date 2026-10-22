using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Runtime.Numbers;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private readonly record struct BoundSubprocessArguments(object[] Values)
    {
        private static readonly SubprocessRunArgumentLayout Layout = SubprocessRunArgumentLayout.Standard;

        public bool HasArgs => Values.Length > Layout.Args;
        public object Args => GetArgument(Values, Layout.Args);
        public bool HasInput => HasArgument(Values, Layout.Input);
        public object Input => GetArgument(Values, Layout.Input);
        public object CurrentDirectory => GetArgument(Values, Layout.CurrentDirectory);
        public bool HasTimeout => HasArgument(Values, Layout.Timeout);
        public object Timeout => GetArgument(Values, Layout.Timeout);
        public object Check => GetArgument(Values, Layout.Check);
        public object CaptureOutput => GetArgument(Values, Layout.CaptureOutput);
        public bool HasStandardInput => HasArgument(Values, Layout.StandardInput);
        public object StandardInput => GetArgument(Values, Layout.StandardInput);
        public bool HasStandardOutput => HasArgument(Values, Layout.StandardOutput);
        public object StandardOutput => GetArgument(Values, Layout.StandardOutput);
        public bool HasStandardError => HasArgument(Values, Layout.StandardError);
        public object StandardError => GetArgument(Values, Layout.StandardError);
        public object Shell => GetArgument(Values, Layout.Shell);
        public object Text => GetArgument(Values, Layout.Text);
        public bool HasEncoding => HasArgument(Values, Layout.Encoding);
        public object Encoding => GetArgument(Values, Layout.Encoding);
        public bool HasErrors => HasArgument(Values, Layout.Errors);
        public object Errors => GetArgument(Values, Layout.Errors);
        public object Environment => GetArgument(Values, Layout.Environment);
        public object UniversalNewlines => GetArgument(Values, Layout.UniversalNewlines);
    }

    private readonly record struct SubprocessInvocation(
        string Owner,
        LythonSubprocessRequest Request,
        object Check,
        SubprocessCompletionKind CompletionKind,
        object Args,
        long StorageBytes,
        SubprocessTimeout? Timeout);

    private readonly record struct SubprocessInvocationPolicy(
        SubprocessCompletionKind Completion,
        SubprocessCheckPolicy Check,
        SubprocessStandardOutputPolicy StandardOutput)
    {
        public static readonly SubprocessInvocationPolicy Run = new(
            SubprocessCompletionKind.CompletedProcess,
            SubprocessCheckPolicy.FromArguments,
            SubprocessStandardOutputPolicy.FromArguments);

        public static readonly SubprocessInvocationPolicy Call = new(
            SubprocessCompletionKind.ReturnCode,
            SubprocessCheckPolicy.Disabled,
            SubprocessStandardOutputPolicy.FromArguments);

        public static readonly SubprocessInvocationPolicy CheckCall = new(
            SubprocessCompletionKind.ReturnCode,
            SubprocessCheckPolicy.Enabled,
            SubprocessStandardOutputPolicy.FromArguments);

        public static readonly SubprocessInvocationPolicy CheckOutput = new(
            SubprocessCompletionKind.Stdout,
            SubprocessCheckPolicy.Enabled,
            SubprocessStandardOutputPolicy.ForcePipe);

        public static readonly SubprocessInvocationPolicy Popen = new(
            SubprocessCompletionKind.CompletedProcess,
            SubprocessCheckPolicy.Disabled,
            SubprocessStandardOutputPolicy.FromArguments);
    }

    private enum SubprocessCompletionKind
    {
        CompletedProcess,
        ReturnCode,
        Stdout,
    }

    private enum SubprocessCheckPolicy
    {
        FromArguments,
        Disabled,
        Enabled,
    }

    private enum SubprocessStandardOutputPolicy
    {
        FromArguments,
        ForcePipe,
    }

    private static async ValueTask<(IReadOnlyList<string> Args, long StorageBytes)> ParseSubprocessArgsAsync(
        object value, bool shell, string owner, LythonSourceSpan span, ExecutionContext context,
        MemoryGovernor.TemporaryMemoryReservation storage, bool asynchronous)
    {
        long bytes = 128;
        storage.Grow(bytes, span);
        if (PyStringOps.TryAsString(value, out var command) || value is PyPath)
        {
            if (!shell) throw RuntimeErrors.Type($"{owner}(args) expects an iterable of strings or Paths, not a single string or Path.", span);
            command = value is PyPath path ? path.Value : command;
            return ([DecodeSubprocessString(command!, storage, span, ref bytes)], bytes);
        }
        var items = new List<string>();
        if (asynchronous)
            await foreach (var item in ToSequenceAsync(value, span, context).ConfigureAwait(false)) Add(item);
        else foreach (var item in ToSequence(value, span, context)) Add(item);
        void Add(object item)
        {
            context.CheckExecution(span);
            var text = item is PyPath path ? path.Value : PyStringOps.TryAsString(item, out var part) ? part
                : throw RuntimeErrors.Type($"{owner}(args) expects an iterable of strings or Paths.", span);
            items.Add(DecodeSubprocessString(text, storage, span, ref bytes));
        }
        if (items.Count == 0) throw new LythonRuntimeException("ValueError", $"{owner}(args) expects at least one command part.", span);
        return (items, bytes);
    }

    // Includes native string metadata and conservative table/list capacity and
    // replacement headroom. The caller retains this funding for every borrower.
    private static string DecodeSubprocessString(PyString text, MemoryGovernor.TemporaryMemoryReservation storage,
        LythonSourceSpan span, ref long bytes)
    {
        var charge = checked(128L + 2L * text.GetUtf16CodeUnitCount());
        storage.Grow(charge, span);
        bytes = checked(bytes + charge);
        return text.AsString();
    }

    private static async ValueTask<bool> ParseSubprocessTextModeAsync(BoundSubprocessArguments arguments,
        LythonSourceSpan span, ExecutionContext context, bool asynchronous)
    {
        var text = asynchronous ? await IsTruthyAsync(arguments.Text, context, span).ConfigureAwait(false) : IsTruthy(arguments.Text, context, span);
        var universal = asynchronous ? await IsTruthyAsync(arguments.UniversalNewlines, context, span).ConfigureAwait(false) : IsTruthy(arguments.UniversalNewlines, context, span);
        if (arguments.Text is not PyNone && arguments.UniversalNewlines is not PyNone && text != universal)
            throw new LythonRuntimeException(ModuleException("subprocess", "SubprocessError"), "Cannot disambiguate text and universal_newlines with different truth values.", span);
        return text || universal || arguments.HasEncoding || arguments.HasErrors;
    }

    private static LythonSubprocessTextEncoding ParseSubprocessEncoding(BoundSubprocessArguments arguments, string owner, LythonSourceSpan span)
    {
        var value = arguments.Encoding;
        if (value is PyNone)
        {
            return LythonSubprocessTextEncoding.Utf8;
        }

        var encodingMode = ParseTextEncoding(value, owner, span);
        return encodingMode switch
        {
            TextEncodingMode.Utf8 => LythonSubprocessTextEncoding.Utf8,
            TextEncodingMode.Utf8Bom => LythonSubprocessTextEncoding.Utf8WithSignature,
            _ => throw new LythonRuntimeException(
                "ValueError",
                $"{owner}(...) only supports encoding='utf-8' or 'utf-8-sig' because the subprocess host boundary is UTF-8-shaped.",
                span),
        };
    }

    private static LythonSubprocessTextErrorMode ParseSubprocessErrors(BoundSubprocessArguments arguments, string owner, LythonSourceSpan span)
    {
        var value = arguments.Errors;
        if (value is PyNone)
        {
            return LythonSubprocessTextErrorMode.Strict;
        }

        return ParseTextErrors(value, owner, span) switch
        {
            TextErrorMode.Ignore => LythonSubprocessTextErrorMode.Ignore,
            TextErrorMode.Replace => LythonSubprocessTextErrorMode.Replace,
            TextErrorMode.BackslashReplace => LythonSubprocessTextErrorMode.BackslashReplace,
            _ => LythonSubprocessTextErrorMode.Strict
        };
    }

    private static string TextErrorName(LythonSubprocessTextErrorMode mode)
        => mode switch
        {
            LythonSubprocessTextErrorMode.Ignore => "ignore",
            LythonSubprocessTextErrorMode.Replace => "replace",
            LythonSubprocessTextErrorMode.BackslashReplace => "backslashreplace",
            _ => "strict",
        };

    private static (IReadOnlyDictionary<string, string> Environment, long StorageBytes) ParseSubprocessEnvironment(
        object value, string owner, LythonSourceSpan span, ExecutionContext context,
        MemoryGovernor.TemporaryMemoryReservation storage)
    {
        var source = value as PyDict;
        if (value is not PyNone && source is null)
            throw RuntimeErrors.Type($"{owner}(..., env=...) expects a dictionary of strings or None.", span);
        var bytes = checked(128L + 128L * (source?.Count ?? context.State.Environment.Count));
        storage.Grow(bytes, span);
        if (source is null) return (new Dictionary<string, string>(context.State.Environment, StringComparer.Ordinal), bytes);
        var environment = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in source)
        {
            context.CheckExecution(span);
            if (!PyStringOps.TryAsString(pair.Key, out var key) || !PyStringOps.TryAsString(pair.Value, out var item))
                throw RuntimeErrors.Type($"{owner}(..., env=...) expects string keys and values.", span);
            environment[DecodeSubprocessString(key, storage, span, ref bytes)] = DecodeSubprocessString(item, storage, span, ref bytes);
        }
        return (environment, bytes);
    }

    private static string? ParseSubprocessCwd(
        object value,
        string owner,
        LythonSourceSpan span,
        ExecutionContext context)
    {
        if (value is PyNone)
        {
            return PathOps.RequireContainedPath(PathOps.Normalize(context.Host.Cwd), span);
        }

        if (value is PyPath path)
        {
            return NormalizeSubprocessCwd(path.Value.AsString(), context, span);
        }

        if (PyStringOps.TryAsString(value, out var text))
        {
            return NormalizeSubprocessCwd(text.AsString(), context, span);
        }

        throw new LythonRuntimeException("TypeError", $"{owner}(..., cwd=...) expects a string, Path, or None.", span);

        static string NormalizeSubprocessCwd(string cwd, ExecutionContext context, LythonSourceSpan span)
            => PathOps.RequireContainedPath(PathOps.Normalize(cwd, context.Host.Cwd), span);
    }

    private static LythonSubprocessStreamMode ParseSubprocessInputMode(object value, string owner, LythonSourceSpan span)
    {
        if (value is PyNone)
        {
            return LythonSubprocessStreamMode.Inherit;
        }

        var constant = ParseSubprocessStreamConstant(value, owner, "stdin", span);
        return constant switch
        {
            SubprocessPipe => LythonSubprocessStreamMode.Pipe,
            SubprocessDevNull => LythonSubprocessStreamMode.DevNull,
            SubprocessStdout => throw new LythonRuntimeException("ValueError", $"{owner}(..., stdin=...) does not support subprocess.STDOUT.", span),
            _ => throw new LythonRuntimeException("ValueError", $"{owner}(..., stdin=...) expects subprocess.PIPE, subprocess.DEVNULL, or None.", span)
        };
    }

    private static LythonSubprocessStreamMode ParseSubprocessOutputMode(object value, string owner, string parameterName, LythonSourceSpan span)
    {
        if (value is PyNone)
        {
            return LythonSubprocessStreamMode.Inherit;
        }

        var constant = ParseSubprocessStreamConstant(value, owner, parameterName, span);
        return constant switch
        {
            SubprocessPipe => LythonSubprocessStreamMode.Pipe,
            SubprocessDevNull => LythonSubprocessStreamMode.DevNull,
            SubprocessStdout when parameterName == "stderr" => LythonSubprocessStreamMode.StandardOutput,
            SubprocessStdout => throw new LythonRuntimeException("ValueError", $"{owner}(..., stdout=...) does not support subprocess.STDOUT.", span),
            _ => throw new LythonRuntimeException("ValueError", $"{owner}(..., {parameterName}=...) expects subprocess.PIPE, subprocess.DEVNULL, subprocess.STDOUT for stderr, or None.", span)
        };
    }

    private static int ParseSubprocessStreamConstant(object value, string owner, string parameterName, LythonSourceSpan span)
    {
        if (value is int integer)
        {
            return integer;
        }

        if (value is BigInteger bigInteger && bigInteger >= int.MinValue && bigInteger <= int.MaxValue)
        {
            return (int)bigInteger;
        }

        throw new LythonRuntimeException("TypeError", $"{owner}(..., {parameterName}=...) expects a subprocess stream constant or None.", span);
    }

    private readonly record struct SubprocessTimeout(TimeSpan Duration, object Value);

    private static SubprocessTimeout? ParseSubprocessTimeout(object value, string owner, LythonSourceSpan span)
    {
        if (value is PyNone) return null;
        if (!PyNumberOps.TryAsNumber(value, out var number))
            throw RuntimeErrors.Type($"{owner}(..., timeout=...) expects a number of seconds or None.", span);
        if (!PyNumberOps.TryToDoubleChecked(number, out var seconds) || !double.IsFinite(seconds))
            throw new LythonRuntimeException("OverflowError", "Subprocess timeout is outside the supported TimeSpan range.", span);
        try { return new SubprocessTimeout(TimeSpan.FromSeconds(Math.Max(0, seconds)), value); }
        catch (OverflowException) { throw new LythonRuntimeException("OverflowError", "Subprocess timeout is outside the supported TimeSpan range.", span); }
    }

    private static LythonRuntimeException CreateSubprocessTimeoutExpired(object args, SubprocessTimeout? timeout,
        ExecutionContext context, LythonSourceSpan span)
        => new(ModuleException("subprocess", "TimeoutExpired"), "Command timed out.", span, innerException: null,
            payload: CreateTimeoutExpiredPayload(args, timeout?.Value ?? PyNone.Instance, PyNone.Instance, PyNone.Instance, context, span));

    private static bool HasArgument(object[] arguments, int index)
    {
        if (index >= arguments.Length)
        {
            return false;
        }

        return arguments[index] is not null and not PyNone;
    }

    private static object GetArgument(object[] arguments, int index)
        => index < arguments.Length ? arguments[index] : PyNone.Instance;
}

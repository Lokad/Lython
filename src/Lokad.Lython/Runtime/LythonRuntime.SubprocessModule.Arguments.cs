using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

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
        bool Check,
        SubprocessCompletionKind CompletionKind);

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

    private static IReadOnlyList<string> ParseSubprocessArgs(object value, bool shell, string owner, LythonSourceSpan span, ExecutionContext context)
    {
        if (PyStringOps.TryAsString(value, out var commandText))
        {
            if (shell)
            {
                return [commandText.AsString()];
            }

            throw new LythonRuntimeException("TypeError", $"{owner}(args) expects an iterable of strings, not a single string.", span);
        }

        if (value is PyPath commandPath)
        {
            if (shell)
            {
                return [commandPath.Value.AsString()];
            }

            throw new LythonRuntimeException("TypeError", $"{owner}(args) expects an iterable of strings or Paths, not a single Path.", span);
        }

        var items = new List<string>();
        foreach (var item in ToSequence(value, span, context))
        {
            if (item is PyPath path)
            {
                items.Add(path.Value.AsString());
                continue;
            }

            if (!PyStringOps.TryAsString(item, out var text))
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(args) expects an iterable of strings or Paths.", span);
            }

            items.Add(text.AsString());
        }

        if (items.Count == 0)
        {
            throw new LythonRuntimeException("ValueError", $"{owner}(args) expects at least one command part.", span);
        }

        return items;
    }

    private static bool ParseSubprocessTextMode(BoundSubprocessArguments arguments, string owner, LythonSourceSpan span)
    {
        var text = ParseSubprocessOptionalBool(arguments.Text, false, owner, "text", span);
        var universalNewlines = ParseSubprocessOptionalBool(arguments.UniversalNewlines, false, owner, "universal_newlines", span);
        return text || universalNewlines ||
            arguments.HasEncoding ||
            arguments.HasErrors;
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
            TextEncodingMode.Utf8Bom => LythonSubprocessTextEncoding.Utf8WithSignature,
            TextEncodingMode.Latin1 => throw new LythonRuntimeException(
                "ValueError",
                $"{owner}(...) only supports encoding='utf-8' or 'utf-8-sig' because the subprocess host boundary is UTF-8-shaped.",
                span),
            _ => LythonSubprocessTextEncoding.Utf8
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

    private static IReadOnlyDictionary<string, string> ParseSubprocessEnvironment(
        object value,
        string owner,
        LythonSourceSpan span,
        IReadOnlyDictionary<string, string> defaultEnvironment)
    {
        if (value is PyNone)
        {
            return new Dictionary<string, string>(defaultEnvironment, StringComparer.Ordinal);
        }

        if (value is not PyDict dict)
        {
            throw new LythonRuntimeException("TypeError", $"{owner}(..., env=...) expects a dictionary of strings or None.", span);
        }

        var environment = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in dict)
        {
            if (!PyStringOps.TryAsString(pair.Key, out var key) ||
                !PyStringOps.TryAsString(pair.Value, out var envValue))
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(..., env=...) expects string keys and values.", span);
            }

            environment[key.AsString()] = envValue.AsString();
        }

        return environment;
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

    private static bool ParseSubprocessOptionalBool(object value, bool defaultValue, string owner, string parameterName, LythonSourceSpan span)
    {
        if (value is PyNone)
        {
            return defaultValue;
        }

        return value is bool boolValue
            ? boolValue
            : throw new LythonRuntimeException("TypeError", $"{owner}(..., {parameterName}=...) expects a bool or None.", span);
    }

    private static int ParseOptionalInt(object value, string owner, LythonSourceSpan span)
    {
        return value switch
        {
            int integer => integer,
            BigInteger integer when integer >= int.MinValue && integer <= int.MaxValue => (int)integer,
            PyNone => 0,
            _ => throw new LythonRuntimeException("TypeError", $"{owner} expects an integer.", span)
        };
    }

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

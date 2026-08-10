using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
using System.Text.RegularExpressions;
using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal interface INamedRuntimeCallable
{
    string Name { get; }
}

internal sealed partial class LythonRuntime
{
    private static readonly PyString DefaultPrintSeparator = PyString.FromOwnedUtf8([(byte)' ']);
    private static readonly PyString DefaultPrintEnding = PyString.FromOwnedUtf8([(byte)'\n']);

    private static object InvokeCall(CallExpressionSyntax call, ExecutionContext context)
    {
        var target = EvaluateExpression(call.Target, context);
        return InvokeCallableTarget(
            target,
            call.Target.Span,
            call.Span,
            context,
            () => ExpandCallArguments(call.Arguments, context));
    }

    private static CallArgumentValue[] ExpandCallArguments(
        IReadOnlyList<CallArgumentSyntax> arguments,
        ExecutionContext context)
        => CallExpansion.ExpandRawArguments(arguments, context, EvaluateExpression);

    private static object InvokeCallableTarget(
        object target,
        LythonSourceSpan targetSpan,
        LythonSourceSpan callSpan,
        ExecutionContext context,
        Func<CallArgumentValue[]> expandArguments)
    {
        if (target is not ICallable callable)
        {
            throw RuntimeErrors.NotCallable(targetSpan);
        }

        context.EnterInterpreterFrame(callSpan);
        try
        {
            context.CheckExecutionBudget(callSpan);
            var arguments = expandArguments();
            return RuntimeValue(callable.Invoke(arguments, callSpan, context));
        }
        catch (RegexParseException ex)
        {
            throw new LythonRuntimeException("ValueError", ex.Message, callSpan);
        }
        catch (LythonRuntimeException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            throw RuntimeErrors.Runtime(ex.Message, callSpan);
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

    private static async ValueTask<object> InvokeCallableTargetAsync(
        object target,
        LythonSourceSpan targetSpan,
        LythonSourceSpan callSpan,
        ExecutionContext context,
        Func<ValueTask<CallArgumentValue[]>> expandArguments)
    {
        if (target is not ICallable callable)
        {
            throw RuntimeErrors.NotCallable(targetSpan);
        }

        context.EnterInterpreterFrame(callSpan);
        try
        {
            context.CheckExecutionBudget(callSpan);
            var arguments = await expandArguments().ConfigureAwait(false);
            return RuntimeValue(await callable.InvokeAsync(arguments, callSpan, context).ConfigureAwait(false));
        }
        catch (RegexParseException ex)
        {
            throw new LythonRuntimeException("ValueError", ex.Message, callSpan);
        }
        catch (LythonRuntimeException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            throw RuntimeErrors.Runtime(ex.Message, callSpan);
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

    private static readonly IReadOnlyDictionary<string, string[]> ExceptionBaseNames =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Exception"] = ["BaseException"],
            ["SystemExit"] = ["BaseException"],
            ["ArithmeticError"] = ["Exception"],
            ["AssertionError"] = ["Exception"],
            ["AttributeError"] = ["Exception"],
            ["ImportError"] = ["Exception"],
            ["LookupError"] = ["Exception"],
            ["MemoryError"] = ["Exception"],
            ["NameError"] = ["Exception"],
            ["OSError"] = ["Exception", "IOError", "EnvironmentError"],
            ["RuntimeError"] = ["Exception"],
            ["StopIteration"] = ["Exception"],
            ["SyntaxError"] = ["Exception"],
            ["TypeError"] = ["Exception"],
            ["ValueError"] = ["Exception"],
            ["Warning"] = ["Exception"],
            ["ZeroDivisionError"] = ["ArithmeticError"],
            ["OverflowError"] = ["ArithmeticError"],
            ["KeyError"] = ["LookupError"],
            ["IndexError"] = ["LookupError"],
            ["ModuleNotFoundError"] = ["ImportError"],
            ["FileNotFoundError"] = ["OSError"],
            ["FileExistsError"] = ["OSError"],
            ["IsADirectoryError"] = ["OSError"],
            ["NotADirectoryError"] = ["OSError"],
            ["PermissionError"] = ["OSError"],
            ["TimeoutError"] = ["OSError"],
            ["IOError"] = ["OSError", "EnvironmentError"],
            ["EnvironmentError"] = ["OSError", "IOError"],
            ["NotImplementedError"] = ["RuntimeError"],
            ["RecursionError"] = ["RuntimeError"],
            ["UnicodeError"] = ["ValueError"],
            ["UnicodeEncodeError"] = ["UnicodeError"],
            ["UnicodeDecodeError"] = ["UnicodeError"],
            ["UnicodeTranslateError"] = ["UnicodeError"],
            ["JSONDecodeError"] = ["ValueError"],
            ["BadGzipFile"] = ["OSError"],
            ["SubprocessError"] = ["Exception"],
            ["CalledProcessError"] = ["SubprocessError"],
            ["TimeoutExpired"] = ["SubprocessError"],
        };

    private static bool MatchesExceptionTypeName(string caughtTypeName, string thrownTypeName)
        => string.Equals(caughtTypeName, thrownTypeName, StringComparison.Ordinal) ||
           IsExceptionSubtype(thrownTypeName, caughtTypeName) ||
           (IsRegexPatternErrorName(caughtTypeName) && IsRegexPatternErrorName(thrownTypeName));

    private static bool IsExceptionSubtype(string thrownTypeName, string caughtTypeName)
    {
        var pending = new Stack<string>();
        pending.Push(thrownTypeName);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!seen.Add(current))
            {
                continue;
            }

            if (!ExceptionBaseNames.TryGetValue(current, out var bases))
            {
                continue;
            }

            foreach (var baseName in bases)
            {
                if (string.Equals(baseName, caughtTypeName, StringComparison.Ordinal))
                {
                    return true;
                }

                pending.Push(baseName);
            }
        }

        return false;
    }

    private static bool IsRegexPatternErrorName(string typeName)
        => string.Equals(typeName, "error", StringComparison.Ordinal) ||
           string.Equals(typeName, "PatternError", StringComparison.Ordinal);

    private static object InvokeCallableTarget(
        object target,
        LythonSourceSpan targetSpan,
        LythonSourceSpan callSpan,
        ExecutionContext context,
        CallArgumentValue[] arguments)
        => InvokeCallableTarget(target, targetSpan, callSpan, context, () => arguments);

    internal interface ICallable
    {
        object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context);

        ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => ValueTask.FromResult(Invoke(arguments, span, context));
    }

    private sealed class BuiltinCallable : ICallable, INamedRuntimeCallable, IPyRenderableValue, IPyHashableValue, IPyDynamicAttributes
    {
        private readonly Func<object[], LythonSourceSpan, ExecutionContext, object> _implementation;
        private readonly Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>>? _asyncImplementation;
        private readonly LythonCallableSignature _signature;
        private readonly IReadOnlyDictionary<string, int>? _parameterIndices;

        public BuiltinCallable(LythonCallableSignature signature, Func<object[], LythonSourceSpan, ExecutionContext, object> implementation) : this(signature, implementation, null) { }

        public BuiltinCallable(
            LythonCallableSignature signature,
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>>? asyncImplementation)
        {
            _signature = signature;
            _implementation = implementation;
            _asyncImplementation = asyncImplementation;
            _parameterIndices = signature.ParameterNames is null ? null : CallBinder.GetParameterIndices(signature.ParameterNames);
        }

        public BuiltinCallable(string name, Func<object[], LythonSourceSpan, ExecutionContext, object> implementation) : this(new LythonCallableSignature(name), implementation) { }

        public BuiltinCallable(string name, Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, string[]? parameterNames) : this(name, implementation, parameterNames, null) { }

        public BuiltinCallable(
            string name,
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            string[]? parameterNames,
            int? requiredCount)
            : this(new LythonCallableSignature(name, parameterNames, requiredCount), implementation)
        {
        }

        public BuiltinCallable(string name, Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation) : this(name, implementation, asyncImplementation, null, null) { }

        public BuiltinCallable(string name, Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation, string[]? parameterNames) : this(name, implementation, asyncImplementation, parameterNames, null) { }

        public BuiltinCallable(
            string name,
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation,
            string[]? parameterNames,
            int? requiredCount)
            : this(new LythonCallableSignature(name, parameterNames, requiredCount), implementation, asyncImplementation)
        {
        }

        public string Name => _signature.Name;

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString(Name);
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public int GetPyHashCode() => RuntimeHelpers.GetHashCode(this);

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (IsBuiltinTypeName(Name) && name is "__name__" or "__qualname__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (IsBuiltinTypeName(Name) && name == "__module__")
            {
                value = PyString.FromString("builtins");
                return true;
            }

            value = PyNone.Instance;
            return false;
        }
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var positional = CallBinder.BindNamedArguments(arguments, span, _signature, PythonCallableKind.Builtin, _parameterIndices);
            return _implementation(positional, span, context);
        }

        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var positional = CallBinder.BindNamedArguments(arguments, span, _signature, PythonCallableKind.Builtin, _parameterIndices);
            return _asyncImplementation is null
                ? _implementation(positional, span, context)
                : await _asyncImplementation(positional, span, context).ConfigureAwait(false);
        }
    }

    private sealed class MinMaxCallable(bool isMin) : ICallable, INamedRuntimeCallable, IPyRenderableValue, IPyHashableValue
    {
        public string Name => isMin ? "min" : "max";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return MinMax(arguments, isMin, span, context);
        }

        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return MinMaxAsync(arguments, isMin, span, context);
        }

        public PyString RenderPython(PyRenderingContext context) => PyString.FromString(Name);

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public int GetPyHashCode() => RuntimeHelpers.GetHashCode(this);

    }

    private sealed class ZipCallable : ICallable, INamedRuntimeCallable, IPyRenderableValue, IPyHashableValue
    {
        public string Name => "zip";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return Zip(arguments, span);
        }

        public PyString RenderPython(PyRenderingContext context) => PyString.FromString(Name);
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
        public int GetPyHashCode() => RuntimeHelpers.GetHashCode(this);
    }

    private sealed class DictCallable : ICallable, INamedRuntimeCallable, IPyRenderableValue, IPyHashableValue
    {
        public string Name => "dict";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return Dict(arguments, span, context);
        }

        public PyString RenderPython(PyRenderingContext context) => PyString.FromString(Name);
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
        public int GetPyHashCode() => RuntimeHelpers.GetHashCode(this);
    }

    private sealed class OpenCallable : ICallable, INamedRuntimeCallable, IPyRenderableValue, IPyHashableValue
    {
        private static readonly LythonCallableSignature CallSignature = new(
            "open",
            ["file", "mode", "buffering", "encoding", "errors", "newline", "closefd", "opener"],
            RequiredCount: 1);

        public string Name => "open";

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString(Name);
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public int GetPyHashCode() => RuntimeHelpers.GetHashCode(this);

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return Open(BindArguments(arguments, span), span, context);
        }

        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return await OpenAsync(BindArguments(arguments, span), span, context).ConfigureAwait(false);
        }

        private static BoundOpenArguments BindArguments(CallArgumentValue[] arguments, LythonSourceSpan span)
            => BoundOpenArguments.From(CallBinder.BindNamedArgumentsWithPresence(arguments, span, CallSignature, PythonCallableKind.Builtin));
    }

    private sealed class PrintCallable : ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);

            var positional = new List<object>(arguments.Length);
            PyString separator = DefaultPrintSeparator;
            PyString ending = DefaultPrintEnding;
            object? outputTarget = null;
            var flush = false;
            var seenSeparator = false;
            var seenEnding = false;
            var seenFile = false;
            var seenFlush = false;

            foreach (var argument in arguments)
            {
                if (argument.IsPositional)
                {
                    positional.Add(argument.Value);
                    continue;
                }

                switch (argument.KeywordName)
                {
                    case "sep":
                        if (seenSeparator)
                        {
                            throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "print", "sep", span);
                        }

                        separator = ReferenceEquals(argument.Value, PyNone.Instance)
                            ? DefaultPrintSeparator
                            : PyStringOps.TryAsString(argument.Value, out var separatorValue)
                                ? separatorValue
                                : throw new LythonRuntimeException("TypeError", "print(..., sep=...) expects a string or None.", span);
                        seenSeparator = true;
                        break;

                    case "end":
                        if (seenEnding)
                        {
                            throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "print", "end", span);
                        }

                        ending = ReferenceEquals(argument.Value, PyNone.Instance)
                            ? DefaultPrintEnding
                            : PyStringOps.TryAsString(argument.Value, out var endingValue)
                                ? endingValue
                                : throw new LythonRuntimeException("TypeError", "print(..., end=...) expects a string or None.", span);
                        seenEnding = true;
                        break;

                    case "file":
                        if (seenFile)
                        {
                            throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "print", "file", span);
                        }

                        outputTarget = argument.Value switch
                        {
                            PyNone => null,
                            ExecutionContext.TextFileHandle handle => handle,
                            HostTextOutputHandle handle => handle,
                            PopenInputStream handle => handle,
                            _ => throw new LythonRuntimeException(
                                "TypeError",
                                "print(..., file=...) expects a writable Lython text stream, writable file handle, or None.",
                                span)
                        };
                        seenFile = true;
                        break;

                    case "flush":
                        if (seenFlush)
                        {
                            throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "print", "flush", span);
                        }

                        flush = IsTruthy(argument.Value);
                        seenFlush = true;
                        break;

                    default:
                        throw CallErrors.UnexpectedKeyword(PythonCallableKind.Builtin, "print", argument.KeywordName, span);
                }
            }

            for (var i = 0; i < positional.Count; i++)
            {
                if (i > 0)
                {
                    AppendOutput(separator, context, outputTarget, span);
                }

                AppendOutput(ToInterpolatedPyString(positional[i], context), context, outputTarget, span);
            }

            AppendOutput(ending, context, outputTarget, span);
            if (flush)
            {
                FlushOutput(context, outputTarget, span);
            }

            return PyNone.Instance;
        }

        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);

            var positional = new List<object>(arguments.Length);
            PyString separator = DefaultPrintSeparator;
            PyString ending = DefaultPrintEnding;
            object? outputTarget = null;
            var flush = false;
            var seenSeparator = false;
            var seenEnding = false;
            var seenFile = false;
            var seenFlush = false;

            foreach (var argument in arguments)
            {
                if (argument.IsPositional)
                {
                    positional.Add(argument.Value);
                    continue;
                }

                switch (argument.KeywordName)
                {
                    case "sep":
                        if (seenSeparator)
                        {
                            throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "print", "sep", span);
                        }

                        separator = ReferenceEquals(argument.Value, PyNone.Instance)
                            ? DefaultPrintSeparator
                            : PyStringOps.TryAsString(argument.Value, out var separatorValue)
                                ? separatorValue
                                : throw new LythonRuntimeException("TypeError", "print(..., sep=...) expects a string or None.", span);
                        seenSeparator = true;
                        break;

                    case "end":
                        if (seenEnding)
                        {
                            throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "print", "end", span);
                        }

                        ending = ReferenceEquals(argument.Value, PyNone.Instance)
                            ? DefaultPrintEnding
                            : PyStringOps.TryAsString(argument.Value, out var endingValue)
                                ? endingValue
                                : throw new LythonRuntimeException("TypeError", "print(..., end=...) expects a string or None.", span);
                        seenEnding = true;
                        break;

                    case "file":
                        if (seenFile)
                        {
                            throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "print", "file", span);
                        }

                        outputTarget = argument.Value switch
                        {
                            PyNone => null,
                            ExecutionContext.TextFileHandle handle => handle,
                            HostTextOutputHandle handle => handle,
                            PopenInputStream handle => handle,
                            _ => throw new LythonRuntimeException(
                                "TypeError",
                                "print(..., file=...) expects a writable Lython text stream, writable file handle, or None.",
                                span)
                        };
                        seenFile = true;
                        break;

                    case "flush":
                        if (seenFlush)
                        {
                            throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "print", "flush", span);
                        }

                        flush = IsTruthy(argument.Value);
                        seenFlush = true;
                        break;

                    default:
                        throw CallErrors.UnexpectedKeyword(PythonCallableKind.Builtin, "print", argument.KeywordName, span);
                }
            }

            for (var i = 0; i < positional.Count; i++)
            {
                if (i > 0)
                {
                    await AppendOutputAsync(separator, context, outputTarget, span).ConfigureAwait(false);
                }

                await AppendOutputAsync(ToInterpolatedPyString(positional[i], context), context, outputTarget, span).ConfigureAwait(false);
            }

            await AppendOutputAsync(ending, context, outputTarget, span).ConfigureAwait(false);
            if (flush)
            {
                await FlushOutputAsync(context, outputTarget, span).ConfigureAwait(false);
            }

            return PyNone.Instance;
        }

        private static void AppendOutput(PyString value, ExecutionContext context, object? outputTarget, LythonSourceSpan span)
        {
            if (outputTarget is null)
            {
                _ = context.State.Stdout.Write(value, span);
            }

            else if (outputTarget is ExecutionContext.TextFileHandle fileHandle)
            {
                _ = fileHandle.Write(value);
            }

            else if (outputTarget is HostTextOutputHandle outputHandle)
            {
                _ = outputHandle.Write(value, span);
            }
            else if (outputTarget is PopenInputStream popenInput)
            {
                _ = popenInput.Write(value, span);
            }
        }

        private static void FlushOutput(ExecutionContext context, object? outputTarget, LythonSourceSpan span)
        {
            if (outputTarget is null)
            {
                _ = context.State.Stdout.Flush(span);
            }
            else if (outputTarget is ExecutionContext.TextFileHandle fileHandle)
            {
                _ = fileHandle.Flush();
            }
            else if (outputTarget is HostTextOutputHandle outputHandle)
            {
                _ = outputHandle.Flush(span);
            }
            else if (outputTarget is PopenInputStream popenInput)
            {
                popenInput.FlushValue(span);
            }
        }

        private static async ValueTask AppendOutputAsync(PyString value, ExecutionContext context, object? outputTarget, LythonSourceSpan span)
        {
            if (outputTarget is null)
            {
                _ = await context.State.Stdout.WriteAsync(value, span).ConfigureAwait(false);
            }

            else if (outputTarget is ExecutionContext.TextFileHandle fileHandle)
            {
                _ = fileHandle.Write(value);
            }

            else if (outputTarget is HostTextOutputHandle outputHandle)
            {
                _ = await outputHandle.WriteAsync(value, span).ConfigureAwait(false);
            }
            else if (outputTarget is PopenInputStream popenInput)
            {
                _ = popenInput.Write(value, span);
            }
        }

        private static async ValueTask FlushOutputAsync(ExecutionContext context, object? outputTarget, LythonSourceSpan span)
        {
            if (outputTarget is null)
            {
                _ = await context.State.Stdout.FlushAsync(span).ConfigureAwait(false);
            }
            else if (outputTarget is ExecutionContext.TextFileHandle fileHandle)
            {
                _ = await fileHandle.FlushAsync().ConfigureAwait(false);
            }
            else if (outputTarget is HostTextOutputHandle outputHandle)
            {
                _ = await outputHandle.FlushAsync(span).ConfigureAwait(false);
            }
            else if (outputTarget is PopenInputStream popenInput)
            {
                popenInput.FlushValue(span);
            }
        }
    }

}

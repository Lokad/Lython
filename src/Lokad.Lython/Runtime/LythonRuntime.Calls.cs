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

    private abstract class BoundArgumentsCallable : ICallable
    {
        private readonly PythonCallableKind _callableKind;

        protected BoundArgumentsCallable(
            LythonCallableSignature signature,
            PythonCallableKind callableKind)
        {
            Signature = signature;
            _callableKind = callableKind;
        }

        protected LythonCallableSignature Signature { get; }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var positional = CallBinder.BindNamedArguments(arguments, span, Signature, _callableKind);
            return InvokeBound(positional, span, context);
        }

        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var positional = CallBinder.BindNamedArguments(arguments, span, Signature, _callableKind);
            return await InvokeBoundAsync(positional, span, context).ConfigureAwait(false);
        }

        /// <summary>Invokes the callable after named arguments have been bound into positional slots.</summary>
        protected abstract object InvokeBound(object[] arguments, LythonSourceSpan span, ExecutionContext context);

        /// <summary>Invokes the callable asynchronously after named arguments have been bound into positional slots.</summary>
        protected virtual ValueTask<object> InvokeBoundAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => ValueTask.FromResult(InvokeBound(arguments, span, context));
    }

    private sealed class BuiltinCallable : BoundArgumentsCallable, INamedRuntimeCallable, IPyRenderableValue, IPyHashableValue, IPyDynamicAttributes
    {
        private readonly Func<object[], LythonSourceSpan, ExecutionContext, object> _implementation;
        private readonly Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>>? _asyncImplementation;

        private BuiltinCallable(
            LythonCallableSignature signature,
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>>? asyncImplementation)
            : base(signature, PythonCallableKind.Builtin)
        {
            _implementation = implementation;
            _asyncImplementation = asyncImplementation;
        }

        public static BuiltinCallable Create(LythonCallableSignature signature, Func<object[], LythonSourceSpan, ExecutionContext, object> implementation)
            => new(signature, implementation, null);

        public static BuiltinCallable Create(
            LythonCallableSignature signature,
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation)
            => new(signature, implementation, asyncImplementation);

        public static BuiltinCallable Create(string name, Func<object[], LythonSourceSpan, ExecutionContext, object> implementation)
            => Create(LythonCallableSignature.Create(name), implementation);

        public static BuiltinCallable Create(string name, Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, string[]? parameterNames)
            => Create(name, implementation, parameterNames, null);

        public static BuiltinCallable Create(
            string name,
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            string[]? parameterNames,
            int? requiredCount)
            => Create(LythonCallableSignature.Create(name, parameterNames, requiredCount), implementation);

        public static BuiltinCallable Create(string name, Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation)
            => Create(name, implementation, asyncImplementation, null, null);

        public static BuiltinCallable Create(string name, Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation, string[]? parameterNames)
            => Create(name, implementation, asyncImplementation, parameterNames, null);

        public static BuiltinCallable Create(
            string name,
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation,
            string[]? parameterNames,
            int? requiredCount)
            => Create(LythonCallableSignature.Create(name, parameterNames, requiredCount), implementation, asyncImplementation);

        public string Name => Signature.Name;

        /// <inheritdoc />
        protected override object InvokeBound(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => _implementation(arguments, span, context);

        /// <inheritdoc />
        protected override ValueTask<object> InvokeBoundAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => _asyncImplementation is null
                ? ValueTask.FromResult(_implementation(arguments, span, context))
                : _asyncImplementation(arguments, span, context);

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
    }

    private sealed class BoundCallable : BoundArgumentsCallable
    {
        private readonly Func<object[], LythonSourceSpan, ExecutionContext, object> _implementation;
        private readonly Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>>? _asyncImplementation;

        private BoundCallable(
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            LythonCallableSignature signature,
            Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>>? asyncImplementation)
            : base(signature, PythonCallableKind.Method)
        {
            _implementation = implementation;
            _asyncImplementation = asyncImplementation;
        }

        public static BoundCallable Create(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, LythonCallableSignature signature)
            => new(implementation, signature, null);

        public static BoundCallable Create(
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            LythonCallableSignature signature,
            Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation)
            => new(implementation, signature, asyncImplementation);

        public static BoundCallable Create(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation)
            => Create(implementation, LythonCallableSignature.Create("bound method"));

        public static BoundCallable Create(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, string? name)
            => Create(implementation, name, null, null);

        public static BoundCallable Create(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, string? name, string[]? parameterNames)
            => Create(implementation, name, parameterNames, null);

        public static BoundCallable Create(
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            string? name,
            string[]? parameterNames,
            int? requiredCount)
            => Create(implementation, LythonCallableSignature.Create(name ?? "bound method", parameterNames, requiredCount));

        public static BoundCallable Create(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation)
            => Create(implementation, asyncImplementation, null, null, null);

        public static BoundCallable Create(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation, string? name)
            => Create(implementation, asyncImplementation, name, null, null);

        public static BoundCallable Create(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation, string? name, string[]? parameterNames)
            => Create(implementation, asyncImplementation, name, parameterNames, null);

        public static BoundCallable Create(
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation,
            string? name,
            string[]? parameterNames,
            int? requiredCount)
            => Create(implementation, LythonCallableSignature.Create(name ?? "bound method", parameterNames, requiredCount), asyncImplementation);

        public static ICallable CreateNoArguments<TReceiver>(
            TReceiver receiver,
            string name,
            Func<TReceiver, LythonSourceSpan, ExecutionContext, object> implementation)
            => NoArgumentsReceiverBoundCallable<TReceiver>.Create(receiver, name, implementation, null);

        public static ICallable CreateNoArguments<TReceiver>(
            TReceiver receiver,
            string name,
            Func<TReceiver, LythonSourceSpan, ExecutionContext, object> implementation,
            Func<TReceiver, LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation)
            => NoArgumentsReceiverBoundCallable<TReceiver>.Create(receiver, name, implementation, asyncImplementation);

        /// <inheritdoc />
        protected override object InvokeBound(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => _implementation(arguments, span, context);

        /// <inheritdoc />
        protected override ValueTask<object> InvokeBoundAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => _asyncImplementation is null
                ? ValueTask.FromResult(_implementation(arguments, span, context))
                : _asyncImplementation(arguments, span, context);
    }

    private sealed class NoArgumentsReceiverBoundCallable<TReceiver> : ICallable
    {
        private readonly Func<TReceiver, LythonSourceSpan, ExecutionContext, object> _implementation;
        private readonly Func<TReceiver, LythonSourceSpan, ExecutionContext, ValueTask<object>>? _asyncImplementation;
        private readonly TReceiver _receiver;

        private NoArgumentsReceiverBoundCallable(
            TReceiver receiver,
            string name,
            Func<TReceiver, LythonSourceSpan, ExecutionContext, object> implementation,
            Func<TReceiver, LythonSourceSpan, ExecutionContext, ValueTask<object>>? asyncImplementation)
        {
            Name = name;
            _receiver = receiver;
            _implementation = implementation;
            _asyncImplementation = asyncImplementation;
        }

        public static NoArgumentsReceiverBoundCallable<TReceiver> Create(
            TReceiver receiver,
            string name,
            Func<TReceiver, LythonSourceSpan, ExecutionContext, object> implementation,
            Func<TReceiver, LythonSourceSpan, ExecutionContext, ValueTask<object>>? asyncImplementation)
            => new(receiver, name, implementation, asyncImplementation);

        private string Name { get; }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            RejectArguments(arguments, span);
            return _implementation(_receiver, span, context);
        }

        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            RejectArguments(arguments, span);
            return _asyncImplementation is null
                ? ValueTask.FromResult(_implementation(_receiver, span, context))
                : _asyncImplementation(_receiver, span, context);
        }

        private void RejectArguments(CallArgumentValue[] arguments, LythonSourceSpan span)
        {
            // Validate the raw call here to preserve each method's Python-shaped
            // error text and avoid materializing a bound empty argument array.
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", $"{Name}() expects no arguments.", span);
            }
        }
    }

    private sealed class MinMaxCallable(ExtremumOperation operation) : ICallable, INamedRuntimeCallable, IPyRenderableValue, IPyHashableValue
    {
        public string Name => operation == ExtremumOperation.Minimum ? "min" : "max";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return MinMax(arguments, operation, span, context);
        }

        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return MinMaxAsync(arguments, operation, span, context);
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
        private static readonly LythonCallableSignature CallSignature = LythonCallableSignature.Create(
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
            var bound = BindArguments(arguments, span);
            var hasWrittenValue = false;
            foreach (var argument in arguments)
            {
                if (!argument.IsPositional)
                {
                    continue;
                }

                if (hasWrittenValue)
                {
                    AppendOutput(bound.Separator, context, bound.OutputTarget, span);
                }

                AppendOutput(ToInterpolatedPyString(argument.Value, context), context, bound.OutputTarget, span);
                hasWrittenValue = true;
            }

            AppendOutput(bound.Ending, context, bound.OutputTarget, span);
            if (bound.Flush)
            {
                FlushOutput(context, bound.OutputTarget, span);
            }

            return PyNone.Instance;
        }

        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var bound = BindArguments(arguments, span);
            var hasWrittenValue = false;
            foreach (var argument in arguments)
            {
                if (!argument.IsPositional)
                {
                    continue;
                }

                if (hasWrittenValue)
                {
                    await AppendOutputAsync(bound.Separator, context, bound.OutputTarget, span).ConfigureAwait(false);
                }

                await AppendOutputAsync(ToInterpolatedPyString(argument.Value, context), context, bound.OutputTarget, span).ConfigureAwait(false);
                hasWrittenValue = true;
            }

            await AppendOutputAsync(bound.Ending, context, bound.OutputTarget, span).ConfigureAwait(false);
            if (bound.Flush)
            {
                await FlushOutputAsync(context, bound.OutputTarget, span).ConfigureAwait(false);
            }

            return PyNone.Instance;
        }

        private static BoundPrintArguments BindArguments(CallArgumentValue[] arguments, LythonSourceSpan span)
        {
            var separator = DefaultPrintSeparator;
            var ending = DefaultPrintEnding;
            PrintOutputTarget outputTarget = StandardOutputTarget.Instance;
            var flush = false;
            var seenSeparator = false;
            var seenEnding = false;
            var seenFile = false;
            var seenFlush = false;

            foreach (var argument in arguments)
            {
                if (argument.IsPositional)
                {
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
                            PyNone => StandardOutputTarget.Instance,
                            ExecutionContext.TextFileHandle handle => new TextFileOutputTarget(handle),
                            HostTextOutputHandle handle => new HostOutputTarget(handle),
                            PopenInputStream handle => new PopenInputOutputTarget(handle),
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

            return new BoundPrintArguments(separator, ending, outputTarget, flush);
        }

        private static void AppendOutput(PyString value, ExecutionContext context, PrintOutputTarget outputTarget, LythonSourceSpan span)
        {
            switch (outputTarget)
            {
                case StandardOutputTarget:
                    _ = context.State.Stdout.Write(value, span);
                    break;
                case TextFileOutputTarget file:
                    _ = file.Handle.Write(value);
                    break;
                case HostOutputTarget host:
                    _ = host.Handle.Write(value, span);
                    break;
                case PopenInputOutputTarget process:
                    _ = process.Handle.Write(value, span);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported print output target.");
            }
        }

        private static void FlushOutput(ExecutionContext context, PrintOutputTarget outputTarget, LythonSourceSpan span)
        {
            switch (outputTarget)
            {
                case StandardOutputTarget:
                    _ = context.State.Stdout.Flush(span);
                    break;
                case TextFileOutputTarget file:
                    _ = file.Handle.Flush();
                    break;
                case HostOutputTarget host:
                    _ = host.Handle.Flush(span);
                    break;
                case PopenInputOutputTarget process:
                    process.Handle.FlushValue(span);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported print output target.");
            }
        }

        private static async ValueTask AppendOutputAsync(PyString value, ExecutionContext context, PrintOutputTarget outputTarget, LythonSourceSpan span)
        {
            switch (outputTarget)
            {
                case StandardOutputTarget:
                    _ = await context.State.Stdout.WriteAsync(value, span).ConfigureAwait(false);
                    break;
                case TextFileOutputTarget file:
                    _ = file.Handle.Write(value);
                    break;
                case HostOutputTarget host:
                    _ = await host.Handle.WriteAsync(value, span).ConfigureAwait(false);
                    break;
                case PopenInputOutputTarget process:
                    _ = process.Handle.Write(value, span);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported print output target.");
            }
        }

        private static async ValueTask FlushOutputAsync(ExecutionContext context, PrintOutputTarget outputTarget, LythonSourceSpan span)
        {
            switch (outputTarget)
            {
                case StandardOutputTarget:
                    _ = await context.State.Stdout.FlushAsync(span).ConfigureAwait(false);
                    break;
                case TextFileOutputTarget file:
                    _ = await file.Handle.FlushAsync().ConfigureAwait(false);
                    break;
                case HostOutputTarget host:
                    _ = await host.Handle.FlushAsync(span).ConfigureAwait(false);
                    break;
                case PopenInputOutputTarget process:
                    process.Handle.FlushValue(span);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported print output target.");
            }
        }

        private readonly record struct BoundPrintArguments(
            PyString Separator,
            PyString Ending,
            PrintOutputTarget OutputTarget,
            bool Flush);

        private abstract record PrintOutputTarget
        {
            private protected PrintOutputTarget() { }
        }

        private sealed record StandardOutputTarget : PrintOutputTarget
        {
            public static StandardOutputTarget Instance { get; } = new();
        }

        private sealed record TextFileOutputTarget(ExecutionContext.TextFileHandle Handle) : PrintOutputTarget;

        private sealed record HostOutputTarget(HostTextOutputHandle Handle) : PrintOutputTarget;

        private sealed record PopenInputOutputTarget(PopenInputStream Handle) : PrintOutputTarget;
    }

}

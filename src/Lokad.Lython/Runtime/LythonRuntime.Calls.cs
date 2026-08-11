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

    private abstract class DelegateBoundArgumentsCallable : BoundArgumentsCallable
    {
        private readonly Func<object[], LythonSourceSpan, ExecutionContext, object> _implementation;
        private readonly Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>>? _asyncImplementation;

        protected DelegateBoundArgumentsCallable(
            LythonCallableSignature signature,
            PythonCallableKind callableKind,
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>>? asyncImplementation)
            : base(signature, callableKind)
        {
            _implementation = implementation;
            _asyncImplementation = asyncImplementation;
        }

        /// <inheritdoc />
        protected sealed override object InvokeBound(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => _implementation(arguments, span, context);

        /// <inheritdoc />
        protected sealed override ValueTask<object> InvokeBoundAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => _asyncImplementation is null
                ? ValueTask.FromResult(_implementation(arguments, span, context))
                : _asyncImplementation(arguments, span, context);
    }

    private sealed class BuiltinCallable : DelegateBoundArgumentsCallable, INamedRuntimeCallable, IPyRenderableValue, IPyHashableValue, IPyDynamicAttributes
    {
        private BuiltinCallable(
            LythonCallableSignature signature,
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>>? asyncImplementation)
            : base(signature, PythonCallableKind.Builtin, implementation, asyncImplementation)
        { }

        public static BuiltinCallable Create(LythonCallableSignature signature, Func<object[], LythonSourceSpan, ExecutionContext, object> implementation)
            => new(signature, implementation, null);

        public static BuiltinCallable Create(
            LythonCallableSignature signature,
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation)
            => new(signature, implementation, asyncImplementation);

        public static BuiltinCallable Create(string name, Func<object[], LythonSourceSpan, ExecutionContext, object> implementation)
            => Create(LythonCallableSignature.Create(name), implementation);

        public static BuiltinCallable Create(string name, Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, string[] parameterNames)
            => Create(LythonCallableSignature.Create(name, parameterNames), implementation);

        public static BuiltinCallable Create(
            string name,
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            string[] parameterNames,
            int requiredCount)
            => Create(LythonCallableSignature.Create(name, parameterNames, requiredCount), implementation);

        public static BuiltinCallable Create(string name, Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation)
            => Create(LythonCallableSignature.Create(name), implementation, asyncImplementation);

        public static BuiltinCallable Create(string name, Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation, string[] parameterNames)
            => Create(LythonCallableSignature.Create(name, parameterNames), implementation, asyncImplementation);

        public static BuiltinCallable Create(
            string name,
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation,
            string[] parameterNames,
            int requiredCount)
            => Create(LythonCallableSignature.Create(name, parameterNames, requiredCount), implementation, asyncImplementation);

        public string Name => Signature.Name;

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

    private sealed class BoundCallable : DelegateBoundArgumentsCallable
    {
        private BoundCallable(
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            LythonCallableSignature signature,
            Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>>? asyncImplementation)
            : base(signature, PythonCallableKind.Method, implementation, asyncImplementation)
        { }

        public static BoundCallable Create(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, LythonCallableSignature signature)
            => new(implementation, signature, null);

        public static BoundCallable Create(
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            LythonCallableSignature signature,
            Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation)
            => new(implementation, signature, asyncImplementation);

        public static BoundCallable Create(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation)
            => Create(implementation, LythonCallableSignature.Create("bound method"));

        public static BoundCallable Create(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, string name)
            => Create(implementation, LythonCallableSignature.Create(name));

        public static BoundCallable Create(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, string name, string[] parameterNames)
            => Create(implementation, LythonCallableSignature.Create(name, parameterNames));

        public static BoundCallable Create(
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            string name,
            string[] parameterNames,
            int requiredCount)
            => Create(implementation, LythonCallableSignature.Create(name, parameterNames, requiredCount));

        public static BoundCallable Create(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation)
            => Create(implementation, LythonCallableSignature.Create("bound method"), asyncImplementation);

        public static BoundCallable Create(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation, string name)
            => Create(implementation, LythonCallableSignature.Create(name), asyncImplementation);

        public static BoundCallable Create(Func<object[], LythonSourceSpan, ExecutionContext, object> implementation, Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation, string name, string[] parameterNames)
            => Create(implementation, LythonCallableSignature.Create(name, parameterNames), asyncImplementation);

        public static BoundCallable Create(
            Func<object[], LythonSourceSpan, ExecutionContext, object> implementation,
            Func<object[], LythonSourceSpan, ExecutionContext, ValueTask<object>> asyncImplementation,
            string name,
            string[] parameterNames,
            int requiredCount)
            => Create(implementation, LythonCallableSignature.Create(name, parameterNames, requiredCount), asyncImplementation);

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
            requiredCount: 1);

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

}

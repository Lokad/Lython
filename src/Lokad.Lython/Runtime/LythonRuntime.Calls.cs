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
            () => CallExpansion.ExpandRawArguments(call.Arguments, context, EvaluateExpression));
    }

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

        return InvokeCallable(
            callable,
            callSpan,
            context,
            new DeferredCallArguments(expandArguments));
    }

    private static object InvokeCallable<TArguments>(
        ICallable callable,
        LythonSourceSpan callSpan,
        ExecutionContext context,
        TArguments arguments)
        where TArguments : struct, ICallArguments
    {
        context.EnterInterpreterFrame(callSpan);
        try
        {
            context.CheckExecutionBudget(callSpan);
            return RuntimeValue(callable.Invoke(arguments.Expand(), callSpan, context));
        }
        catch (Exception ex) when (TryTranslateCallableException(ex, callSpan, out var translated))
        {
            throw translated;
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
        catch (Exception ex) when (TryTranslateCallableException(ex, callSpan, out var translated))
        {
            throw translated;
        }
        finally
        {
            context.LeaveInterpreterFrame();
        }
    }

    private static bool TryTranslateCallableException(
        Exception exception,
        LythonSourceSpan callSpan,
        [MaybeNullWhen(false)] out LythonRuntimeException translated)
    {
        translated = exception switch
        {
            RegexParseException regex => new LythonRuntimeException("ValueError", regex.Message, callSpan),
            InvalidOperationException invalidOperation => RuntimeErrors.Runtime(invalidOperation.Message, callSpan),
            _ => null
        };
        return translated is not null;
    }

    internal static readonly IReadOnlyDictionary<PythonExceptionIdentity, PythonExceptionIdentity[]> ExceptionBaseIdentities =
        new Dictionary<PythonExceptionIdentity, PythonExceptionIdentity[]>
        {
            [BuiltinException("Exception")] = [BuiltinException("BaseException")],
            [BuiltinException("SystemExit")] = [BuiltinException("BaseException")],
            [BuiltinException("ArithmeticError")] = [BuiltinException("Exception")],
            [BuiltinException("AssertionError")] = [BuiltinException("Exception")],
            [BuiltinException("AttributeError")] = [BuiltinException("Exception")],
            [BuiltinException("ImportError")] = [BuiltinException("Exception")],
            [BuiltinException("LookupError")] = [BuiltinException("Exception")],
            [BuiltinException("MemoryError")] = [BuiltinException("Exception")],
            [BuiltinException("NameError")] = [BuiltinException("Exception")],
            [BuiltinException("OSError")] = [BuiltinException("Exception")],
            [BuiltinException("RuntimeError")] = [BuiltinException("Exception")],
            [BuiltinException("StopIteration")] = [BuiltinException("Exception")],
            [BuiltinException("SyntaxError")] = [BuiltinException("Exception")],
            [BuiltinException("TypeError")] = [BuiltinException("Exception")],
            [BuiltinException("ValueError")] = [BuiltinException("Exception")],
            [BuiltinException("Warning")] = [BuiltinException("Exception")],
            [BuiltinException("ZeroDivisionError")] = [BuiltinException("ArithmeticError")],
            [BuiltinException("OverflowError")] = [BuiltinException("ArithmeticError")],
            [BuiltinException("KeyError")] = [BuiltinException("LookupError")],
            [BuiltinException("IndexError")] = [BuiltinException("LookupError")],
            [BuiltinException("ModuleNotFoundError")] = [BuiltinException("ImportError")],
            [BuiltinException("FileNotFoundError")] = [BuiltinException("OSError")],
            [BuiltinException("FileExistsError")] = [BuiltinException("OSError")],
            [BuiltinException("IsADirectoryError")] = [BuiltinException("OSError")],
            [BuiltinException("NotADirectoryError")] = [BuiltinException("OSError")],
            [BuiltinException("PermissionError")] = [BuiltinException("OSError")],
            [BuiltinException("TimeoutError")] = [BuiltinException("OSError")],
            [BuiltinException("IOError")] = [BuiltinException("OSError")],
            [BuiltinException("EnvironmentError")] = [BuiltinException("OSError")],
            [BuiltinException("NotImplementedError")] = [BuiltinException("RuntimeError")],
            [BuiltinException("RecursionError")] = [BuiltinException("RuntimeError")],
            [BuiltinException("UnicodeError")] = [BuiltinException("ValueError")],
            [BuiltinException("UnicodeEncodeError")] = [BuiltinException("UnicodeError")],
            [BuiltinException("UnicodeDecodeError")] = [BuiltinException("UnicodeError")],
            [BuiltinException("UnicodeTranslateError")] = [BuiltinException("UnicodeError")],
            [ModuleException("argparse", "ArgumentError")] = [BuiltinException("Exception")],
            [ModuleException("argparse", "ArgumentTypeError")] = [BuiltinException("Exception")],
            [ModuleException("copy", "Error")] = [BuiltinException("Exception")],
            [ModuleException("csv", "Error")] = [BuiltinException("Exception")],
            [ModuleException("dataclasses", "FrozenInstanceError")] = [BuiltinException("AttributeError")],
            [ModuleException("decimal", "DecimalException")] = [BuiltinException("ArithmeticError")],
            [ModuleException("decimal", "InvalidOperation")] = [ModuleException("decimal", "DecimalException")],
            [ModuleException("decimal", "DivisionByZero")] = [ModuleException("decimal", "DecimalException"), BuiltinException("ZeroDivisionError")],
            [ModuleException("decimal", "Inexact")] = [ModuleException("decimal", "DecimalException")],
            [ModuleException("decimal", "Rounded")] = [ModuleException("decimal", "DecimalException")],
            [ModuleException("decimal", "Overflow")] = [ModuleException("decimal", "Inexact"), ModuleException("decimal", "Rounded")],
            [ModuleException("decimal", "Underflow")] = [ModuleException("decimal", "Inexact"), ModuleException("decimal", "Rounded"), ModuleException("decimal", "Subnormal")],
            [ModuleException("decimal", "Subnormal")] = [ModuleException("decimal", "DecimalException")],
            [ModuleException("decimal", "Clamped")] = [ModuleException("decimal", "DecimalException")],
            [ModuleException("decimal", "FloatOperation")] = [ModuleException("decimal", "DecimalException"), BuiltinException("TypeError")],
            [ModuleException("gzip", "BadGzipFile")] = [BuiltinException("OSError")],
            [ModuleException("json", "JSONDecodeError")] = [BuiltinException("ValueError")],
            [ModuleException("openpyxl.utils.exceptions", "CellCoordinatesException")] = [BuiltinException("ValueError")],
            [ModuleException("openpyxl.utils.exceptions", "IllegalCharacterError")] = [BuiltinException("ValueError")],
            [ModuleException("openpyxl.utils.exceptions", "InvalidFileException")] = [BuiltinException("Exception")],
            [ModuleException("openpyxl.utils.exceptions", "NamedRangeException")] = [BuiltinException("Exception")],
            [ModuleException("openpyxl.utils.exceptions", "ReadOnlyWorkbookException")] = [BuiltinException("Exception")],
            [ModuleException("openpyxl.utils.exceptions", "SheetTitleException")] = [BuiltinException("Exception")],
            [ModuleException("openpyxl.utils.exceptions", "WorkbookAlreadySaved")] = [BuiltinException("Exception")],
            [ModuleException("re", "PatternError")] = [BuiltinException("Exception")],
            [ModuleException("shutil", "Error")] = [BuiltinException("OSError")],
            [ModuleException("shutil", "SameFileError")] = [ModuleException("shutil", "Error")],
            [ModuleException("statistics", "StatisticsError")] = [BuiltinException("ValueError")],
            [ModuleException("subprocess", "SubprocessError")] = [BuiltinException("Exception")],
            [ModuleException("subprocess", "CalledProcessError")] = [ModuleException("subprocess", "SubprocessError")],
            [ModuleException("subprocess", "TimeoutExpired")] = [ModuleException("subprocess", "SubprocessError")],
            [ModuleException("zipfile", "BadZipFile")] = [BuiltinException("Exception")],
            [ModuleException("zipfile", "LargeZipFile")] = [BuiltinException("Exception")],
        };

    private static PythonExceptionIdentity BuiltinException(string typeName)
        => PythonExceptionIdentity.Builtin(typeName);

    internal static PythonExceptionIdentity ModuleException(string moduleName, string typeName)
        => PythonExceptionIdentity.Module(moduleName, typeName);

    private static bool MatchesExceptionType(
        PythonExceptionIdentity caughtIdentity,
        PythonExceptionIdentity thrownIdentity)
        => caughtIdentity == thrownIdentity || IsExceptionSubtype(thrownIdentity, caughtIdentity);

    private static bool IsExceptionSubtype(
        PythonExceptionIdentity thrownIdentity,
        PythonExceptionIdentity caughtIdentity)
    {
        var pending = new Stack<PythonExceptionIdentity>();
        pending.Push(thrownIdentity);
        var seen = new HashSet<PythonExceptionIdentity>();
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!seen.Add(current))
            {
                continue;
            }

            if (!ExceptionBaseIdentities.TryGetValue(current, out var bases))
            {
                continue;
            }

            foreach (var baseName in bases)
            {
                if (baseName == caughtIdentity)
                {
                    return true;
                }

                pending.Push(baseName);
            }
        }

        return false;
    }

    private static object InvokeCallableTarget(
        object target,
        LythonSourceSpan targetSpan,
        LythonSourceSpan callSpan,
        ExecutionContext context,
        CallArgumentValue[] arguments)
    {
        if (target is not ICallable callable)
        {
            throw RuntimeErrors.NotCallable(targetSpan);
        }

        return InvokeCallable(
            callable,
            callSpan,
            context,
            new PreparedCallArguments(arguments));
    }

    private interface ICallArguments
    {
        /// <summary>Supplies arguments after the target has been confirmed callable and its interpreter frame entered.</summary>
        CallArgumentValue[] Expand();
    }

    private readonly record struct DeferredCallArguments(Func<CallArgumentValue[]> ExpandArguments) : ICallArguments
    {
        public CallArgumentValue[] Expand() => ExpandArguments();
    }

    private readonly record struct PreparedCallArguments(CallArgumentValue[] Arguments) : ICallArguments
    {
        public CallArgumentValue[] Expand() => Arguments;
    }

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

        /// <summary>
        /// Invokes the callable asynchronously after named arguments have been bound into positional slots.
        /// The default implementation runs the synchronous body inline and returns an already-completed task;
        /// overrides that await host operations must honor the execution cancellation token. Callers own the
        /// returned task and await it exactly once.
        /// </summary>
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

        public static BuiltinCallable CreateUnsupported(string qualifiedName)
            => Create(
                qualifiedName,
                (_, span, _) => throw new LythonRuntimeException(
                    "NotImplementedError",
                    qualifiedName + " is unsupported by Lython.",
                    span));

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
                value = ExceptionTypeValue.SharedModuleLabel("builtins");
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
            return Zip(arguments, span, context);
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

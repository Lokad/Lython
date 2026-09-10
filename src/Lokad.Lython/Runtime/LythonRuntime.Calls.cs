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
            [BuiltinException("GeneratorExit")] = [BuiltinException("BaseException")],
            [BuiltinException("KeyboardInterrupt")] = [BuiltinException("BaseException")],
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

    // Builtin type constructors expose CPython-style __bases__/__mro__ resolved
    // through the run builtins table (bool derives int, the rest derive object).
    // Hierarchy tuples build once per callable and alias stably like CPython.
    private static readonly IReadOnlyDictionary<string, string[]> BuiltinTypeBaseNames =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["bool"] = ["int"],
            ["int"] = ["object"],
            ["float"] = ["object"],
            ["list"] = ["object"],
            ["tuple"] = ["object"],
            ["dict"] = ["object"],
            ["set"] = ["object"],
            ["str"] = ["object"],
            ["bytes"] = ["object"],
            ["decimal.Decimal"] = ["object"],
            ["decimal.DecimalTuple"] = ["tuple"],
            ["decimal.Context"] = ["object"],
            ["range"] = ["object"],
            ["slice"] = ["object"],
            ["zip"] = ["object"],
            ["staticmethod"] = ["object"],
            ["classmethod"] = ["object"],
            ["property"] = ["object"],
            ["super"] = ["object"],
        };

    internal sealed record BuiltinTypeHierarchy(PyTuple Bases, PyTuple Mro);

    // Module singletons (and their members) are shared across runs, so a cached
    // hierarchy must be keyed by its owning run: base objects come from the run
    // builtins table and go stale for any other run.
    internal sealed record OwnedTypeHierarchy(ExecutionState Owner, BuiltinTypeHierarchy Hierarchy);

    internal static bool TryGetOwnedHierarchy(
        string name,
        object self,
        ExecutionContext context,
        LythonSourceSpan span,
        ref OwnedTypeHierarchy? cache,
        out BuiltinTypeHierarchy hierarchy)
    {
        if (cache is not null && ReferenceEquals(cache.Owner, context.State))
        {
            hierarchy = cache.Hierarchy;
            return true;
        }

        var built = BuildBuiltinTypeHierarchy(name, self, context, span);
        if (built is null)
        {
            hierarchy = null!;
            return false;
        }

        cache = new OwnedTypeHierarchy(context.State, built);
        hierarchy = built;
        return true;
    }

    internal static BuiltinTypeHierarchy? BuildBuiltinTypeHierarchy(
        string name,
        object self,
        ExecutionContext context,
        LythonSourceSpan span)
    {
        if (!BuiltinTypeBaseNames.TryGetValue(name, out var baseNames))
        {
            return null;
        }

        var bases = new object[baseNames.Length];
        for (var i = 0; i < baseNames.Length; i++)
        {
            if (!context.TryGetBuiltin(baseNames[i], out var baseValue) || baseValue is null)
            {
                return null;
            }

            bases[i] = baseValue;
        }

        // __mro__ always terminates at object like CPython.
        if (!context.TryGetBuiltin("object", out var objectBase) || objectBase is null)
        {
            return null;
        }

        var mroLength = bases.Length + 1;
        if (bases.Length == 0 || !ReferenceEquals(bases[bases.Length - 1], objectBase))
        {
            mroLength++;
        }

        var mro = new object[mroLength];
        mro[0] = self;
        Array.Copy(bases, 0, mro, 1, bases.Length);
        if (mroLength > bases.Length + 1)
        {
            mro[mroLength - 1] = objectBase;
        }

        return new BuiltinTypeHierarchy(
            new PyTuple(bases, context.MemoryGovernor, span),
            new PyTuple(mro, context.MemoryGovernor, span));
    }

    // Singleton types deriving object resolve __bases__/__mro__ per read: the
    // object base comes from the run builtins table, so tuples cannot be shared
    // across runs.
    internal static bool TryGetObjectBases(
        object self,
        ExecutionContext context,
        LythonSourceSpan span,
        string member,
        [MaybeNullWhen(false)] out object value)
    {
        if (!context.TryGetBuiltin("object", out var obj) || obj is null)
        {
            value = PyNone.Instance;
            return false;
        }

        if (member == "__bases__")
        {
            value = new PyTuple([obj], context.MemoryGovernor, span);
            return true;
        }

        value = new PyTuple([self, obj], context.MemoryGovernor, span);
        return true;
    }

    // Builtin functions report their host module like CPython (len -> builtins,
    // math.sqrt -> math), resolving through the run import registry so identity
    // matches the imported module object (registering on first touch like import).
    internal static bool TryGetCallableModule(
        string callableName,
        ExecutionContext context,
        [MaybeNullWhen(false)] out PyModule module)
    {
        var dot = callableName.LastIndexOf((char)46);
        var moduleName = dot < 0 ? "builtins" : callableName.Substring(0, dot);
        return TryResolveKnownImportedModule(moduleName, context, out module);
    }

    // Core values report their run type object like CPython: plain names resolve
    // through builtins, dotted names through the defining module registry.
    // Core values report their run type object like CPython (plain names resolve
    // through builtins, dotted names through the defining module registry).
    internal static bool TryGetValueClass(
        object value,
        ExecutionContext context,
        [MaybeNullWhen(false)] out object classValue)
    {
        // Type-denoting objects report the type builtin itself.
        if (value is BuiltinCallable { Name: var ctorName } && BuiltinTypeBaseNames.ContainsKey(ctorName) ||
            value is DictCallable or ZipCallable ||
            value is ReModule.RegexFlagFactory or ZipInfoCallable or ZipFileCallable ||
            value is PathlibModule.PathlibPathType or PyBuiltinRuntimeType ||
            value is ExceptionTypeValue)
        {
            classValue = TryGetBuiltinOrNull(context, "type");
            return classValue is not null;
        }

        object? resolved = value switch
        {
            PyList => TryGetBuiltinOrNull(context, "list"),
            PyDict => TryGetBuiltinOrNull(context, "dict"),
            PyTuple => TryGetBuiltinOrNull(context, "tuple"),
            PySet => TryGetBuiltinOrNull(context, "set"),
            PyString => TryGetBuiltinOrNull(context, "str"),
            string => TryGetBuiltinOrNull(context, "str"),
            PyBytes => TryGetBuiltinOrNull(context, "bytes"),
            byte[] => TryGetBuiltinOrNull(context, "bytes"),
            BigInteger => TryGetBuiltinOrNull(context, "int"),
            int => TryGetBuiltinOrNull(context, "int"),
            double => TryGetBuiltinOrNull(context, "float"),
            bool => TryGetBuiltinOrNull(context, "bool"),
            PyDecimal => TryGetModuleMemberOrNull(context, "decimal", "Decimal"),
            PyDate => TryGetModuleMemberOrNull(context, "datetime", "date"),
            PyTime => TryGetModuleMemberOrNull(context, "datetime", "time"),
            PyDateTime => TryGetModuleMemberOrNull(context, "datetime", "datetime"),
            PyTimedelta => TryGetModuleMemberOrNull(context, "datetime", "timedelta"),
            PyTimezone => TryGetModuleMemberOrNull(context, "datetime", "timezone"),
            PyPath => TryGetModuleMemberOrNull(context, "pathlib", "Path"),
            TimeStructTimeValue => TryGetModuleMemberOrNull(context, "time", "struct_time"),
            LythonRuntime.StatisticsModule.PyNormalDist => TryGetModuleMemberOrNull(context, "statistics", "NormalDist"),
            LythonRuntime.RandomModule.PyRandom => TryGetModuleMemberOrNull(context, "random", "Random"),
            PyNone => PyType.NoneType,
            PyModule => PyType.ModuleType,
            LythonRuntime.RePatternObject => PyType.RegexPatternType,
            LythonRuntime.ReMatchObject => PyType.RegexMatchType,
            PyStaticMethod => TryGetBuiltinOrNull(context, "staticmethod"),
            PyClassMethod => TryGetBuiltinOrNull(context, "classmethod"),
            PyType type => type.MetaType ?? TryGetBuiltinOrNull(context, "type"),
            PyFunctionBase => PyType.FunctionType,
            LambdaFunction => PyType.FunctionType,
            IPySlotWrapper => PyType.WrapperDescriptorType,
            ObjectNewMethod => PyType.BuiltinFunctionType,
            PyBoundMethod method => method.Function switch
            {
                IPySlotWrapper => PyType.MethodWrapperType,
                IPyBoundEngineMethod => PyType.BuiltinFunctionType,
                _ => PyType.MethodType,
            },
            BuiltinCallable => PyType.BuiltinFunctionType,
            IPyBoundEngineMethod => PyType.BuiltinFunctionType,
            MinMaxCallable => PyType.BuiltinFunctionType,
            OpenCallable => PyType.BuiltinFunctionType,
            PrintCallable => PyType.BuiltinFunctionType,
            PyDateTimeOps.TypeMemberCallable => PyType.BuiltinFunctionType,
            PyDataclass.DataclassInitMethod => PyType.FunctionType,
            PyDataclass.DataclassReprMethod => PyType.FunctionType,
            PyDataclass.DataclassEqMethod => PyType.FunctionType,
            PyDataclass.DataclassOrderMethod => PyType.FunctionType,
            PyDataclass.DataclassHashMethod => PyType.FunctionType,
            PyDataclass.DataclassFrozenSetAttrMethod => PyType.FunctionType,
            PyDataclass.DataclassFrozenDelAttrMethod => PyType.FunctionType,
            TotalOrderingMethod => PyType.FunctionType,
            _ => null,
        };

        classValue = resolved;
        return resolved is not null;
    }

    private static object? TryGetBuiltinOrNull(ExecutionContext context, string name)
    {
        return context.TryGetBuiltin(name, out var value) ? value : null;
    }

    private static object? TryGetModuleMemberOrNull(ExecutionContext context, string moduleName, string memberName)
    {
        if (!context.State.ImportedModules.TryGetValue(moduleName, out var module) ||
            !module.TryGetCachedMember(memberName, out var value) ||
            value is null)
        {
            return null;
        }

        return value;
    }

    // Marks bound engine-method wrappers (per-access or receiver-bound) so member
    // resolution treats them uniformly without naming generic instantiations.
    internal interface IPyBoundEngineMethod
    {
    }

    // Types expose their own stable __new__ slot like CPython (int.__new__
    // is int.__new__, qualified by the type, bound to it). Construction
    // through the slot is unsupported; type(...) builds values. Wrappers
    // are cached as fixed type metadata, beside their owning type object.
    internal sealed class TypeNewMethod : ICallable, IPyDynamicAttributes, IPyBoundEngineMethod
    {
        private readonly object _owner;
        private readonly string _shortName;

        internal TypeNewMethod(object owner, string shortName)
        {
            _owner = owner;
            _shortName = shortName;
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__")
            {
                value = PyString.FromString("__new__");
                return true;
            }

            if (name is "__qualname__")
            {
                value = PyString.FromString(_shortName + ".__new__");
                return true;
            }

            if (name is "__module__")
            {
                value = PyNone.Instance;
                return true;
            }

            if (name is "__self__")
            {
                value = _owner;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            _ = arguments;
            throw new LythonRuntimeException("TypeError", "type.__new__ construction is unsupported in Lython; call type(...) instead.", span);
        }
    }

    // Shared choke point for type __new__ slots: builtin type-table entries
    // denote constructors with their own slot, and the core runtime types
    // (function, method, module, NoneType) bind their singleton like CPython.
    // Anything else keeps its own rules.
    internal static bool TryGetTypeNewSlot(object classValue, [MaybeNullWhen(false)] out object value)
    {
        if (classValue is BuiltinCallable ctor && BuiltinTypeBaseNames.ContainsKey(ctor.Name))
        {
            ctor.NewSlot ??= new TypeNewMethod(ctor, BuiltinCallable.ShortCallableName(ctor.Name));
            value = ctor.NewSlot;
            return true;
        }

        if (classValue is DictCallable dictCallable && BuiltinTypeBaseNames.ContainsKey(dictCallable.Name))
        {
            dictCallable.NewSlot ??= new TypeNewMethod(dictCallable, dictCallable.Name);
            value = dictCallable.NewSlot;
            return true;
        }

        if (classValue is ZipCallable zipCallable && BuiltinTypeBaseNames.ContainsKey(zipCallable.Name))
        {
            zipCallable.NewSlot ??= new TypeNewMethod(zipCallable, zipCallable.Name);
            value = zipCallable.NewSlot;
            return true;
        }

        if (ReferenceEquals(classValue, PyType.FunctionType))
        {
            _functionTypeNewSlot ??= new TypeNewMethod(classValue, PyType.FunctionType.Name);
            value = _functionTypeNewSlot;
            return true;
        }

        if (ReferenceEquals(classValue, PyType.MethodType))
        {
            _methodTypeNewSlot ??= new TypeNewMethod(classValue, PyType.MethodType.Name);
            value = _methodTypeNewSlot;
            return true;
        }

        if (ReferenceEquals(classValue, PyType.ModuleType))
        {
            _moduleTypeNewSlot ??= new TypeNewMethod(classValue, PyType.ModuleType.Name);
            value = _moduleTypeNewSlot;
            return true;
        }

        if (ReferenceEquals(classValue, PyType.NoneType))
        {
            _noneTypeNewSlot ??= new TypeNewMethod(classValue, PyType.NoneType.Name);
            value = _noneTypeNewSlot;
            return true;
        }

        // Datetime, random and tzinfo runtime types own their slot too,
        // qualified by the short type name like CPython.
        if (classValue is PyBuiltinRuntimeType datetimeType &&
            (ReferenceEquals(datetimeType, PyDateTimeOps.TimedeltaType) ||
             ReferenceEquals(datetimeType, PyDateTimeOps.DateType) ||
             ReferenceEquals(datetimeType, PyDateTimeOps.TimeType) ||
             ReferenceEquals(datetimeType, PyDateTimeOps.DateTimeType) ||
             ReferenceEquals(datetimeType, PyDateTimeOps.TimezoneType) ||
             ReferenceEquals(datetimeType, PyDateTimeOps.TzInfoType) ||
             ReferenceEquals(datetimeType, LythonRuntime.RandomModule.RandomType)))
        {
            _datetimeTypeNewSlots ??= new Dictionary<PyBuiltinRuntimeType, TypeNewMethod>(ReferenceEqualityComparer.Instance);
            if (!_datetimeTypeNewSlots.TryGetValue(datetimeType, out var datetimeSlot))
            {
                datetimeSlot = new TypeNewMethod(datetimeType, BuiltinCallable.ShortCallableName(datetimeType.Name));
                _datetimeTypeNewSlots[datetimeType] = datetimeSlot;
            }

            value = datetimeSlot;
            return true;
        }

        // struct_time owns its slot through its dedicated singleton, like
        // the builtin constructors above.
        if (classValue is TimeStructTimeType structTimeType)
        {
            structTimeType.NewSlot ??= new TypeNewMethod(structTimeType, BuiltinCallable.ShortCallableName(structTimeType.Name));
            value = structTimeType.NewSlot;
            return true;
        }

        value = PyNone.Instance;
        return false;
    }

    // Shared choke point for builtin exception __new__ slots: the caller
    // resolves the defining type along the builtin hierarchy; the wrapper
    // is cached on that type object like constructor slots.
    internal static bool TryGetExceptionNewSlot(ExceptionTypeValue typeValue, string shortName, [MaybeNullWhen(false)] out object value)
    {
        typeValue.NewSlot ??= new TypeNewMethod(typeValue, shortName);
        value = typeValue.NewSlot;
        return true;
    }

    private static TypeNewMethod? _functionTypeNewSlot;
    private static TypeNewMethod? _methodTypeNewSlot;
    private static TypeNewMethod? _moduleTypeNewSlot;
    private static TypeNewMethod? _noneTypeNewSlot;
    private static Dictionary<PyBuiltinRuntimeType, TypeNewMethod>? _datetimeTypeNewSlots;

    private sealed class BuiltinCallable : DelegateBoundArgumentsCallable, INamedRuntimeCallable, IPyRenderableValue, IPyHashableValue, IPyDynamicAttributes, IPyContextualDynamicAttributes
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

        private OwnedTypeHierarchy? _hierarchy;

        internal TypeNewMethod? NewSlot { get; set; }

        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            if ((name == "__bases__" || name == "__mro__") &&
                TryGetOwnedHierarchy(Signature.Name, this, context, span, ref _hierarchy, out var hierarchy))
            {
                value = name == "__mro__" ? hierarchy.Mro : hierarchy.Bases;
                return true;
            }

            // Table members are type constructors even where the legacy gate does
            // not cover them (range, slice, decimal.Decimal); they report the shared
            // builtins or module label like CPython.
            if (name == "__self__" && !BuiltinTypeBaseNames.ContainsKey(Signature.Name) &&
                TryGetCallableModule(Signature.Name, context, out var selfModule))
            {
                value = selfModule;
                return true;
            }

            if (name == "__module__" && BuiltinTypeBaseNames.ContainsKey(Signature.Name))
            {
                value = ExceptionTypeValue.SharedModuleLabel(CallableModuleName(Signature.Name));
                return true;
            }

            return TryGetMember(name, out value);
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            // Every builtin callable exposes CPython-style __name__/__module__:
            // the short name and the dotted module part (shared builtins for
            // top-level names). __name__ stays fresh per read like CPython;
            // __module__ reuses the shared label catalog where it hits.
            if (name is "__name__" or "__qualname__")
            {
                value = PyString.FromString(ShortCallableName(Name));
                return true;
            }

            if (name == "__module__")
            {
                value = ExceptionTypeValue.SharedModuleLabel(CallableModuleName(Name));
                return true;
            }

            if (name == "__new__" && LythonRuntime.TryGetTypeNewSlot(this, out value))
            {
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        internal static string ShortCallableName(string name)
        {
            var dot = name.LastIndexOf('.');
            return dot < 0 ? name : name.Substring(dot + 1);
        }

        private static string CallableModuleName(string name)
        {
            var dot = name.LastIndexOf('.');
            return dot < 0 ? "builtins" : name.Substring(0, dot);
        }
    }

    private sealed class BoundCallable : DelegateBoundArgumentsCallable, IPyDynamicAttributes, IPyBoundEngineMethod
    {
        // Bound engine methods expose CPython-style __name__/__module__ like
        // C-implemented methods: the short decorated name and None, since every
        // wrapper is engine-implemented (CPython reports the class module only
        // for Python-implemented methods such as Random.gauss).
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name == "__name__")
            {
                value = PyString.FromString(ShortMethodName(Signature.Name));
                return true;
            }

            // __qualname__ keeps the full dotted path like CPython bound
            // methods (list.append), falling back to the short name for
            // decorated display names (gzip file.read([size]) -> read).
            if (name == "__qualname__")
            {
                value = PyString.FromString(QualMethodName(Signature.Name));
                return true;
            }

            if (name == "__module__")
            {
                value = PyNone.Instance;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        private static string ShortMethodName(string name)
        {
            var dot = name.LastIndexOf('.');
            var tail = dot < 0 ? name : name.Substring(dot + 1);
            var cut = tail.IndexOfAny([' ', '(', '[']);
            return cut < 0 ? tail : tail.Substring(0, cut);
        }

        private static string QualMethodName(string name)
        {
            var cut = name.IndexOfAny([' ', '(', '[']);
            return cut < 0 ? name : ShortMethodName(name);
        }

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

    private sealed class NoArgumentsReceiverBoundCallable<TReceiver> : ICallable, IPyDynamicAttributes, IPyBoundEngineMethod
    {
        // Bound engine methods expose CPython-style __name__/__module__ like
        // C-implemented methods: the short decorated name and None, since every
        // wrapper is engine-implemented (CPython reports the class module only
        // for Python-implemented methods such as Random.gauss).
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name == "__self__")
            {
                value = _receiver;
                return true;
            }

            if (name == "__class__")
            {
                value = PyType.BuiltinFunctionType;
                return true;
            }

            if (name == "__name__")
            {
                value = PyString.FromString(ShortMethodName(Name));
                return true;
            }

            // __qualname__ keeps the full dotted path like CPython bound
            // methods (list.append), falling back to the short name for
            // decorated display names (gzip file.read([size]) -> read).
            if (name == "__qualname__")
            {
                value = PyString.FromString(QualMethodName(Name));
                return true;
            }

            if (name == "__module__")
            {
                value = PyNone.Instance;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        private static string ShortMethodName(string name)
        {
            var dot = name.LastIndexOf('.');
            var tail = dot < 0 ? name : name.Substring(dot + 1);
            var cut = tail.IndexOfAny([' ', '(', '[']);
            return cut < 0 ? tail : tail.Substring(0, cut);
        }

        private static string QualMethodName(string name)
        {
            var cut = name.IndexOfAny([' ', '(', '[']);
            return cut < 0 ? name : ShortMethodName(name);
        }

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

    private sealed class MinMaxCallable(ExtremumOperation operation) : ICallable, INamedRuntimeCallable, IPyRenderableValue, IPyHashableValue, IPyDynamicAttributes, IPyContextualDynamicAttributes
    {
        public string Name => operation == ExtremumOperation.Minimum ? "min" : "max";

        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            if (name == "__self__" && TryGetCallableModule(Name, context, out var module))
            {
                value = module;
                return true;
            }

            return TryGetMember(name, out value);
        }
        // Singleton builtins expose CPython-style __name__/__module__ like
        // BuiltinCallable: the fixed name and the shared builtins label.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__" or "__qualname__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name == "__module__")
            {
                value = ExceptionTypeValue.SharedModuleLabel("builtins");
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

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

    private sealed class ZipCallable : ICallable, INamedRuntimeCallable, IPyRenderableValue, IPyHashableValue, IPyDynamicAttributes, IPyContextualDynamicAttributes
    {
        public string Name => "zip";

        private OwnedTypeHierarchy? _hierarchy;

        internal TypeNewMethod? NewSlot { get; set; }

        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            if ((name == "__bases__" || name == "__mro__") &&
                TryGetOwnedHierarchy("zip", this, context, span, ref _hierarchy, out var hierarchy))
            {
                value = name == "__mro__" ? hierarchy.Mro : hierarchy.Bases;
                return true;
            }

            return TryGetMember(name, out value);
        }

        // Singleton builtins expose CPython-style __name__/__module__ like
        // BuiltinCallable: the fixed name and the shared builtins label.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__" or "__qualname__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name == "__module__")
            {
                value = ExceptionTypeValue.SharedModuleLabel("builtins");
                return true;
            }

            if (name == "__new__" && LythonRuntime.TryGetTypeNewSlot(this, out value))
            {
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return Zip(arguments, span, context);
        }

        public PyString RenderPython(PyRenderingContext context) => PyString.FromString(Name);
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
        public int GetPyHashCode() => RuntimeHelpers.GetHashCode(this);
    }

    private sealed class DictCallable : ICallable, INamedRuntimeCallable, IPyRenderableValue, IPyHashableValue, IPyDynamicAttributes, IPyContextualDynamicAttributes
    {
        public string Name => "dict";

        private OwnedTypeHierarchy? _hierarchy;

        internal TypeNewMethod? NewSlot { get; set; }

        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            if ((name == "__bases__" || name == "__mro__") &&
                TryGetOwnedHierarchy("dict", this, context, span, ref _hierarchy, out var hierarchy))
            {
                value = name == "__mro__" ? hierarchy.Mro : hierarchy.Bases;
                return true;
            }

            return TryGetMember(name, out value);
        }

        // Singleton builtins expose CPython-style __name__/__module__ like
        // BuiltinCallable: the fixed name and the shared builtins label.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__" or "__qualname__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name == "__module__")
            {
                value = ExceptionTypeValue.SharedModuleLabel("builtins");
                return true;
            }

            if (name == "__new__" && LythonRuntime.TryGetTypeNewSlot(this, out value))
            {
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return Dict(arguments, span, context);
        }

        public PyString RenderPython(PyRenderingContext context) => PyString.FromString(Name);
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
        public int GetPyHashCode() => RuntimeHelpers.GetHashCode(this);
    }

    private sealed class OpenCallable : ICallable, INamedRuntimeCallable, IPyRenderableValue, IPyHashableValue, IPyDynamicAttributes, IPyContextualDynamicAttributes
    {
        private static readonly LythonCallableSignature CallSignature = LythonCallableSignature.Create(
            "open",
            ["file", "mode", "buffering", "encoding", "errors", "newline", "closefd", "opener"],
            requiredCount: 1);

        public string Name => "open";

        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            if (name == "__self__" && TryGetCallableModule(Name, context, out var module))
            {
                value = module;
                return true;
            }

            return TryGetMember(name, out value);
        }
        // Singleton builtins expose CPython-style __name__/__module__ like
        // BuiltinCallable: the fixed name and the shared builtins label.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__" or "__qualname__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name == "__module__")
            {
                value = ExceptionTypeValue.SharedModuleLabel("builtins");
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

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

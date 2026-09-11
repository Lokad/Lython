using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Calls;
using System.Text;
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
            PyIsoCalendarDate => PyDateTimeOps.IsoCalendarDateType,
            PyTimedelta => TryGetModuleMemberOrNull(context, "datetime", "timedelta"),
            PyTimezone => TryGetModuleMemberOrNull(context, "datetime", "timezone"),
            PyPath => TryGetModuleMemberOrNull(context, "pathlib", "Path"),
            // Struct-time values resolve through the dedicated singleton rather
            // than the import registry, so they keep their class without an
            // import; the registry holds the same object, preserving identity.
            TimeStructTimeValue => TimeStructTimeType.Instance,
            LythonRuntime.StatisticsModule.PyNormalDist => TryGetModuleMemberOrNull(context, "statistics", "NormalDist"),
            LythonRuntime.RandomModule.PyRandom => TryGetModuleMemberOrNull(context, "random", "Random"),
            PyNone => PyType.NoneType,
            PyEllipsis => PyType.EllipsisType,
            PyNotImplemented => PyType.NotImplementedType,
            PyModule => PyType.ModuleType,
            PyPartial => TryGetModuleMemberOrNull(context, "functools", "partial"),
            PyCounter => TryGetModuleMemberOrNull(context, "collections", "Counter"),
            PyException exception when exception.Identity.IsBuiltin => TryGetBuiltinOrNull(context, exception.Identity.TypeName),
            PyException exception => TryGetModuleMemberOrNull(context, exception.Identity.ModuleName, exception.Identity.TypeName),
            PyDefaultDict => TryGetModuleMemberOrNull(context, "collections", "defaultdict"),
            PyDeque => TryGetModuleMemberOrNull(context, "collections", "deque"),
            PyChainMap => TryGetModuleMemberOrNull(context, "collections", "ChainMap"),
            CollectionsCallable member when member.Name is "collections.namedtuple" => PyType.FunctionType,
            CollectionsCallable => TryGetBuiltinOrNull(context, "type"),
            PartialFactory => TryGetBuiltinOrNull(context, "type"),
            PartialMethodFactory => TryGetBuiltinOrNull(context, "type"),
            PyTypingAlias alias when alias.ShortName is "NamedTuple" or "TypedDict" => PyType.FunctionType,
            INamedRuntimeCallable callable when callable.Name is "typing.TypeVar" or "typing.NewType" => TryGetBuiltinOrNull(context, "type"),
            INamedRuntimeCallable callable when callable.Name is "typing.cast" or "typing.get_origin" or "typing.get_args" => PyType.FunctionType,
            PyTyping.NewTypeIdentityCallable => PyTyping.NewTypeType,
            LythonRuntime.RePatternObject => PyType.RegexPatternType,
            LythonRuntime.ReMatchObject => PyType.RegexMatchType,
            PyStaticMethod => TryGetBuiltinOrNull(context, "staticmethod"),
            PyClassMethod => TryGetBuiltinOrNull(context, "classmethod"),
            PyType type => type.MetaType ?? TryGetBuiltinOrNull(context, "type"),
            PyFunctionBase => PyType.FunctionType,
            LambdaFunction => PyType.FunctionType,
            FunctionNewMethod => PyType.FunctionType,
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
            IPyRawBoundCallable => PyType.BuiltinFunctionType,
            BuiltinTypeMethod => PyType.BuiltinFunctionType,
            UnboundTypeMethod => PyType.MethodDescriptorType,
            BuiltinDataDescriptor dataDescriptor => dataDescriptor.Kind == DataDescriptorKind.Member ? PyType.MemberDescriptorType : PyType.GetSetDescriptorType,
            PyRange => TryGetBuiltinOrNull(context, "range"),
            TupleGetter => PyType.TupleGetterType,
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

    // __init_subclass__ binds the type itself like CPython: user and builtin
    // types, exception types and member factories that denote types bind their
    // own object, while the function-shaped namedtuple factory binds function
    // like its __new__ slot; every other value binds its value class.
    internal static bool TryGetBoundSubclassTarget(object target, ExecutionContext context, [MaybeNullWhen(false)] out object typeValue)
    {
        typeValue = target switch
        {
            PyType => target,
            BuiltinCallable ctor when BuiltinTypeBaseNames.ContainsKey(ctor.Name) => target,
            DictCallable => target,
            ZipCallable => target,
            ExceptionTypeValue => target,
            PathlibModule.PathlibPathType => target,
            PyBuiltinRuntimeType => target,
            TimeStructTimeType => target,
            CollectionsCallable member when member.Name is "collections.namedtuple" => PyType.FunctionType,
            CollectionsCallable => target,
            PartialFactory => target,
            PartialMethodFactory => target,
            PyNamedTupleType => target,
            PyNamedTupleObject namedTuple => namedTuple.Type,
            _ => null,
        };

        if (typeValue is not null)
        {
            return true;
        }

        return TryGetValueClass(target, context, out typeValue) && typeValue is not null;
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

    // Unbound builtin type methods behave like CPython method descriptors:
    // list.append takes its receiver as the first argument, validates it
    // against the owning type, then delegates to the bound member path so
    // call semantics stay identical. Wrappers cache per constructor beside
    // the __new__ slots and carry no run state beyond the owning callable.
    // There is deliberately no __self__ or __module__ (CPython raises
    // AttributeError for both) and no __doc__ (the engine keeps no doc
    // corpus); __get__ and __text_signature__ stay out for the same reason.
    internal sealed class UnboundTypeMethod : ICallable, IPyDynamicAttributes, IPyHashableValue, IPyRenderableValue
    {
        private readonly object _owner;
        private readonly string _ownerName;
        private readonly string _memberName;
        private readonly string _qualifiedName;

        internal UnboundTypeMethod(object owner, string ownerName, string memberName)
        {
            _owner = owner;
            _ownerName = ownerName;
            _memberName = memberName;
            _qualifiedName = ownerName + "." + memberName;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var receiverIndex = -1;
            for (var i = 0; i < arguments.Length; i++)
            {
                if (arguments[i].IsPositional)
                {
                    receiverIndex = i;
                    break;
                }
            }

            if (receiverIndex < 0)
            {
                throw new LythonRuntimeException("TypeError", "unbound method " + _qualifiedName + "() needs an argument", span);
            }

            var receiver = arguments[receiverIndex].Value;
            var boundCallable = (ICallable)BindReceiver(receiver, span, context);

            var rest = new CallArgumentValue[arguments.Length - 1];
            Array.Copy(arguments, 0, rest, 0, receiverIndex);
            Array.Copy(arguments, receiverIndex + 1, rest, receiverIndex, rest.Length - receiverIndex);
            return boundCallable.Invoke(rest, span, context);
        }

        internal object BindReceiver(object? receiver, LythonSourceSpan span, ExecutionContext context)
        {
            if (receiver is null || !DoesObjectMatchBuiltinType(_ownerName, receiver))
            {
                throw new LythonRuntimeException("TypeError", "descriptor '" + _memberName + "' for '" + _ownerName + "' objects doesn't apply to a '" + PythonTypeName(receiver, context) + "' object", span);
            }

            if (!TryResolveRuntimeMember(receiver, _memberName, context, span, out var bound) || bound is not ICallable)
            {
                throw PyMemberAccess.CreateMissingMemberError(receiver, _memberName, span, context);
            }

            return bound;
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name == "__name__")
            {
                value = PyString.FromString(_memberName);
                return true;
            }

            if (name == "__qualname__")
            {
                value = PyString.FromString(_qualifiedName);
                return true;
            }

            if (name == "__objclass__")
            {
                value = _owner;
                return true;
            }

            if (name == "__get__")
            {
                value = new PyBoundMethod(this, UnboundGetMethod.Instance);
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<method '" + _memberName + "' of '" + _ownerName + "' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public int GetPyHashCode() => HashCode.Combine(RuntimeHelpers.GetHashCode(_owner), StringComparer.Ordinal.GetHashCode(_memberName));

        internal static string PythonTypeName(object? receiver, ExecutionContext context)
        {
            // User instances report their class short name like CPython; every
            // other shape resolves through the shared value-class helper.
            if (receiver is PyInstance instance)
            {
                return instance.Type.Name;
            }

            if (receiver is not null &&
                TryGetValueClass(receiver, context, out var classValue) &&
                classValue is not null)
            {
                return classValue switch
                {
                    BuiltinCallable builtin => BuiltinCallable.ShortCallableName(builtin.Name),
                    PyBuiltinRuntimeType runtimeType => BuiltinCallable.ShortCallableName(runtimeType.Name),
                    PyType type => type.Name,
                    INamedRuntimeCallable named => BuiltinCallable.ShortCallableName(named.Name),
                    _ => "object",
                };
            }

            return "object";
        }
    }

    // The __get__ slot of unbound descriptors behaves like CPython method
    // wrappers: binding a receiver returns its bound member, binding None
    // against an explicit type returns the descriptor itself, and arity,
    // keyword and receiver failures use the exact wrapper texts. One shared
    // instance serves every descriptor: all member reads are constant and
    // calls delegate to the descriptor riding in as the bound self. There is
    // deliberately no __module__ or __doc__ like the other engine slot
    // wrappers (the engine keeps no doc corpus).
    internal sealed class UnboundGetMethod : ICallable, IPyDynamicAttributes, IPySlotWrapper
    {
        internal static readonly UnboundGetMethod Instance = new();

        private UnboundGetMethod()
        {
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name == "__name__")
            {
                value = PyString.FromString("__get__");
                return true;
            }

            if (name == "__qualname__")
            {
                value = PyString.FromString("method_descriptor.__get__");
                return true;
            }

            if (name == "__objclass__")
            {
                value = PyType.MethodDescriptorType;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            foreach (var argument in arguments)
            {
                if (argument.IsKeyword)
                {
                    throw new LythonRuntimeException("TypeError", "wrapper __get__() takes no keyword arguments", span);
                }
            }

            // arguments[0] is the descriptor the bound method prepended.
            var positionals = arguments.Length - 1;
            if (positionals < 1)
            {
                throw new LythonRuntimeException("TypeError", " expected at least 1 argument, got 0", span);
            }

            if (positionals > 2)
            {
                throw new LythonRuntimeException("TypeError", " expected at most 2 arguments, got " + positionals + "", span);
            }

            if (arguments[0].Value is not UnboundTypeMethod descriptor)
            {
                throw new LythonRuntimeException("TypeError", "__get__(None, None) is invalid", span);
            }

            var target = arguments[1].Value;
            if (target is PyNone)
            {
                if (positionals == 2 && arguments[2].Value is not PyNone)
                {
                    return descriptor;
                }

                throw new LythonRuntimeException("TypeError", "__get__(None, None) is invalid", span);
            }

            // Binding drops the owner argument like CPython and resolves
            // through the shared helper, so receiver validation and bound
            // member construction stay in one place.
            return descriptor.BindReceiver(arguments[1].Value, span, context);
        }
    }

    // Builtin numeric data attributes behave like CPython getset
    // descriptors: the short __name__, the qualified owner.member name,
    // the owning type as __objclass__, per-member documentation, and
    // __get__/__set__/__delete__ slots. Descriptors are not callable;
    // binding resolves through the instance member tables. Wrappers cache
    // per constructor, so identity holds like CPython.
    // Getset descriptors carry documentation and writable-shaped errors;
    // member descriptors report None docs and read-only errors.
    internal enum DataDescriptorKind
    {
        GetSet,
        Member,
    }

    internal sealed class BuiltinDataDescriptor : IPyDynamicAttributes, IPyRenderableValue, IPyHashableValue
    {
        private readonly object _owner;
        private readonly string _ownerName;
        private readonly string _memberName;
        private readonly string _qualifiedName;
        private readonly string? _documentation;
        private readonly DataDescriptorKind _kind;

        internal BuiltinDataDescriptor(object owner, string ownerName, string memberName, string? documentation, DataDescriptorKind kind)
        {
            _owner = owner;
            _ownerName = ownerName;
            _memberName = memberName;
            _qualifiedName = ownerName + "." + memberName;
            _documentation = documentation;
            _kind = kind;
        }

        internal string OwnerName => _ownerName;

        internal string MemberName => _memberName;

        internal DataDescriptorKind Kind => _kind;

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name == "__name__")
            {
                value = PyString.FromString(_memberName);
                return true;
            }

            if (name == "__qualname__")
            {
                value = PyString.FromString(_qualifiedName);
                return true;
            }

            if (name == "__objclass__")
            {
                value = _owner;
                return true;
            }

            if (name == "__doc__")
            {
                value = _documentation is null ? PyNone.Instance : PyString.FromString(_documentation);
                return true;
            }

            if (name == "__get__")
            {
                value = new PyBoundMethod(this, _kind == DataDescriptorKind.Member ? DataGetMethod.Member : DataGetMethod.GetSet);
                return true;
            }

            if (name == "__set__")
            {
                value = new PyBoundMethod(this, _kind == DataDescriptorKind.Member ? DataSetMethod.Member : DataSetMethod.GetSet);
                return true;
            }

            if (name == "__delete__")
            {
                value = new PyBoundMethod(this, _kind == DataDescriptorKind.Member ? DataDeleteMethod.Member : DataDeleteMethod.GetSet);
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            var kindName = _kind == DataDescriptorKind.Member ? "member" : "attribute";
            return PyString.FromString("<" + kindName + " '" + _memberName + "' of '" + _ownerName + "' objects>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public int GetPyHashCode() => HashCode.Combine(RuntimeHelpers.GetHashCode(_owner), StringComparer.Ordinal.GetHashCode(_memberName));
    }

    // The __get__ slot of builtin data descriptors binds like CPython: a
    // receiver resolves through the instance member tables, None against an
    // explicit type returns the descriptor itself, and arity, keyword and
    // receiver failures use the exact wrapper texts. One shared instance
    // serves every descriptor like the method-descriptor slot.
    internal sealed class DataGetMethod : ICallable, IPyDynamicAttributes, IPySlotWrapper
    {
        internal static readonly DataGetMethod GetSet = new(DataDescriptorKind.GetSet);

        internal static readonly DataGetMethod Member = new(DataDescriptorKind.Member);

        private readonly DataDescriptorKind _kind;

        private DataGetMethod(DataDescriptorKind kind)
        {
            _kind = kind;
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name == "__name__")
            {
                value = PyString.FromString("__get__");
                return true;
            }

            if (name == "__qualname__")
            {
                value = PyString.FromString((_kind == DataDescriptorKind.Member ? "member_descriptor" : "getset_descriptor") + ".__get__");
                return true;
            }

            if (name == "__objclass__")
            {
                value = _kind == DataDescriptorKind.Member ? PyType.MemberDescriptorType : PyType.GetSetDescriptorType;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            foreach (var argument in arguments)
            {
                if (argument.IsKeyword)
                {
                    throw new LythonRuntimeException("TypeError", "wrapper __get__() takes no keyword arguments", span);
                }
            }

            // arguments[0] is the descriptor the bound method prepended.
            var positionals = arguments.Length - 1;
            if (positionals < 1)
            {
                throw new LythonRuntimeException("TypeError", " expected at least 1 argument, got 0", span);
            }

            if (positionals > 2)
            {
                throw new LythonRuntimeException("TypeError", " expected at most 2 arguments, got " + positionals + "", span);
            }

            if (arguments[0].Value is not BuiltinDataDescriptor descriptor)
            {
                throw new LythonRuntimeException("TypeError", "__get__(None, None) is invalid", span);
            }

            var target = arguments[1].Value;
            if (target is PyNone)
            {
                if (positionals == 2 && arguments[2].Value is not PyNone)
                {
                    return descriptor;
                }

                throw new LythonRuntimeException("TypeError", "__get__(None, None) is invalid", span);
            }

            if (!DoesObjectMatchBuiltinType(descriptor.OwnerName, target))
            {
                throw new LythonRuntimeException("TypeError", "descriptor '" + descriptor.MemberName + "' for '" + descriptor.OwnerName + "' objects doesn't apply to a '" + UnboundTypeMethod.PythonTypeName(target, context) + "' object", span);
            }

            if (!TryResolveRuntimeMember(target, descriptor.MemberName, context, span, out var bound))
            {
                throw PyMemberAccess.CreateMissingMemberError(target, descriptor.MemberName, span, context);
            }

            return bound;
        }
    }

    // The __set__ slot of builtin data descriptors always fails like CPython:
    // numeric data attributes are read-only. One shared instance serves
    // every descriptor; the bound descriptor names the attribute.
    internal sealed class DataSetMethod : ICallable, IPyDynamicAttributes, IPySlotWrapper
    {
        internal static readonly DataSetMethod GetSet = new(DataDescriptorKind.GetSet);

        internal static readonly DataSetMethod Member = new(DataDescriptorKind.Member);

        private readonly DataDescriptorKind _kind;

        private DataSetMethod(DataDescriptorKind kind)
        {
            _kind = kind;
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name == "__name__")
            {
                value = PyString.FromString("__set__");
                return true;
            }

            if (name == "__qualname__")
            {
                value = PyString.FromString((_kind == DataDescriptorKind.Member ? "member_descriptor" : "getset_descriptor") + ".__set__");
                return true;
            }

            if (name == "__objclass__")
            {
                value = _kind == DataDescriptorKind.Member ? PyType.MemberDescriptorType : PyType.GetSetDescriptorType;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            foreach (var argument in arguments)
            {
                if (argument.IsKeyword)
                {
                    throw new LythonRuntimeException("TypeError", "wrapper __set__() takes no keyword arguments", span);
                }
            }

            if (arguments.Length - 1 != 2)
            {
                throw new LythonRuntimeException("TypeError", " expected 2 arguments, got " + (arguments.Length - 1), span);
            }

            if (arguments[0].Value is not BuiltinDataDescriptor descriptor)
            {
                throw new LythonRuntimeException("TypeError", "__set__(None, None) is invalid", span);
            }

            if (_kind == DataDescriptorKind.Member)
            {
                throw new LythonRuntimeException("AttributeError", "readonly attribute", span);
            }

            throw new LythonRuntimeException("AttributeError", "attribute '" + descriptor.MemberName + "' of '" + descriptor.OwnerName + "' objects is not writable", span);
        }
    }

    // The __delete__ slot mirrors __set__: numeric data attributes cannot
    // be deleted either. Separate singleton so __name__ reports correctly.
    internal sealed class DataDeleteMethod : ICallable, IPyDynamicAttributes, IPySlotWrapper
    {
        internal static readonly DataDeleteMethod GetSet = new(DataDescriptorKind.GetSet);

        internal static readonly DataDeleteMethod Member = new(DataDescriptorKind.Member);

        private readonly DataDescriptorKind _kind;

        private DataDeleteMethod(DataDescriptorKind kind)
        {
            _kind = kind;
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name == "__name__")
            {
                value = PyString.FromString("__delete__");
                return true;
            }

            if (name == "__qualname__")
            {
                value = PyString.FromString((_kind == DataDescriptorKind.Member ? "member_descriptor" : "getset_descriptor") + ".__delete__");
                return true;
            }

            if (name == "__objclass__")
            {
                value = _kind == DataDescriptorKind.Member ? PyType.MemberDescriptorType : PyType.GetSetDescriptorType;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            foreach (var argument in arguments)
            {
                if (argument.IsKeyword)
                {
                    throw new LythonRuntimeException("TypeError", "wrapper __delete__() takes no keyword arguments", span);
                }
            }

            if (arguments.Length - 1 != 1)
            {
                throw new LythonRuntimeException("TypeError", "expected 1 argument, got " + (arguments.Length - 1), span);
            }

            if (arguments[0].Value is not BuiltinDataDescriptor descriptor)
            {
                throw new LythonRuntimeException("TypeError", "__delete__(None, None) is invalid", span);
            }

            if (_kind == DataDescriptorKind.Member)
            {
                throw new LythonRuntimeException("AttributeError", "readonly attribute", span);
            }

            throw new LythonRuntimeException("AttributeError", "attribute '" + descriptor.MemberName + "' of '" + descriptor.OwnerName + "' objects is not writable", span);
        }
    }

    // Builtin classmethods (dict.fromkeys, bytes.fromhex) and staticmethods
    // (str.maketrans) behave like CPython bound-to-type builtins: the short
    // __name__, the qualified __qualname__, a None __module__, and the owning
    // type as __self__ for classmethods (staticmethods expose none), with
    // value equality by owner and member. Classmethods stay fresh per read
    // like CPython (identity never holds); the staticmethod shape is one
    // shared object. There is deliberately no __doc__ like the other engine
    // shapes (the engine keeps no doc corpus). Keyword arguments always fail
    // with the qualified wrapper text; arity and value validation live in
    // each implementation with the exact CPython texts.
    internal sealed class BuiltinTypeMethod : ICallable, IPyDynamicAttributes, IPyContextualDynamicAttributes, IPyHashableValue
    {
        private readonly string _ownerName;
        private readonly string _memberName;
        private readonly bool _bindsOwner;
        private readonly Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, object> _implementation;
        private readonly string _qualifiedName;

        // str.maketrans carries no run state (names plus static
        // implementation), so one process-wide object serves every read like
        // CPython, where the static shape is identical everywhere.
        internal static readonly BuiltinTypeMethod StrMaketrans = new("str", "maketrans", bindsOwner: false, StrMaketransImpl);

        internal static readonly BuiltinTypeMethod BytesMaketrans = new("bytes", "maketrans", bindsOwner: false, BytesMaketransImpl);

        internal BuiltinTypeMethod(string ownerName, string memberName, bool bindsOwner, Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, object> implementation)
        {
            _ownerName = ownerName;
            _memberName = memberName;
            _bindsOwner = bindsOwner;
            _implementation = implementation;
            _qualifiedName = ownerName + "." + memberName;
        }

        internal string OwnerName => _ownerName;

        internal string MemberName => _memberName;

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return _implementation(arguments, span, context);
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name == "__name__")
            {
                value = PyString.FromString(_memberName);
                return true;
            }

            if (name == "__qualname__")
            {
                value = PyString.FromString(_qualifiedName);
                return true;
            }

            if (name == "__module__")
            {
                value = PyNone.Instance;
                return true;
            }

            // Staticmethod shapes report a None __self__ like CPython
            // instead of missing.
            if (name == "__self__" && !_bindsOwner)
            {
                value = PyNone.Instance;
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            if (name == "__self__" && _bindsOwner &&
                context.TryGetBuiltin(_ownerName, out var owner) && owner is not null)
            {
                value = owner;
                return true;
            }

            return TryGetMember(name, out value);
        }

        public int GetPyHashCode() => HashCode.Combine(StringComparer.Ordinal.GetHashCode(_ownerName), StringComparer.Ordinal.GetHashCode(_memberName));
    }

    // Shared choke point for builtin classmethods and staticmethods: only the
    // modeled (owner, member) pairs serve wrappers, so every other miss keeps
    // the shared missing-member error.
    private static bool TryGetBuiltinTypeMethod(object owner, string ownerName, string memberName, [MaybeNullWhen(false)] out object value)
    {
        _ = owner;
        value = (ownerName, memberName) switch
        {
            ("dict", "fromkeys") => new BuiltinTypeMethod(ownerName, memberName, bindsOwner: true, DictFromKeys),
            ("bytes", "fromhex") => new BuiltinTypeMethod(ownerName, memberName, bindsOwner: true, BytesFromHex),
            ("str", "maketrans") => BuiltinTypeMethod.StrMaketrans,
            ("bytes", "maketrans") => BuiltinTypeMethod.BytesMaketrans,
            ("int", "from_bytes") => new BuiltinTypeMethod(ownerName, memberName, bindsOwner: true, IntFromBytes),
            ("bool", "from_bytes") => new BuiltinTypeMethod(ownerName, memberName, bindsOwner: true, BoolFromBytes),
            ("float", "fromhex") => new BuiltinTypeMethod(ownerName, memberName, bindsOwner: true, FloatFromHex),
            _ => null,
        };

        if (value is null)
        {
            value = PyNone.Instance;
            return false;
        }

        return true;
    }

    internal static void RejectKeywordArguments(string qualifiedName, CallArgumentValue[] arguments, LythonSourceSpan span)
    {
        foreach (var argument in arguments)
        {
            if (argument.IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", qualifiedName + "() takes no keyword arguments", span);
            }
        }
    }

    private static object[] PositionalArguments(CallArgumentValue[] arguments)
    {
        var positional = new object[arguments.Length];
        for (var i = 0; i < arguments.Length; i++)
        {
            positional[i] = arguments[i].Value;
        }

        return positional;
    }

    private static object DictFromKeys(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        RejectKeywordArguments("dict.fromkeys", arguments, span);
        return DictFromKeysPositional(PositionalArguments(arguments), span, context);
    }

    private static object DictFromKeysPositional(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length < 1)
        {
            throw new LythonRuntimeException("TypeError", "fromkeys expected at least 1 argument, got 0", span);
        }

        if (arguments.Length > 2)
        {
            throw new LythonRuntimeException("TypeError", "fromkeys expected at most 2 arguments, got " + arguments.Length, span);
        }

        var value = arguments.Length == 2 ? arguments[1] : PyNone.Instance;
        var result = new PyDict(context.MemoryGovernor, span);
        foreach (var key in ToSequence(arguments[0], span, context))
        {
            result.SetItem(ValidateDictionaryKey(key, span, context.MemoryGovernor), value);
        }

        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static object BytesFromHex(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        RejectKeywordArguments("bytes.fromhex", arguments, span);
        return BytesFromHexPositional(PositionalArguments(arguments), span, context);
    }

    private static object BytesFromHexPositional(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "bytes.fromhex() takes exactly one argument (" + arguments.Length + " given)", span);
        }

        if (arguments[0] is not PyString text)
        {
            throw new LythonRuntimeException("TypeError", "fromhex() argument must be str, not " + UnboundTypeMethod.PythonTypeName(arguments[0], context), span);
        }

        // Positions count in the original string (spaces included): a space
        // directly inside a pair is invalid, while a lone trailing nibble
        // reports one past the end like CPython.
        var source = text.AsString();
        var bytes = new List<byte>();
        var index = 0;
        while (true)
        {
            while (index < source.Length && IsHexSpace(source[index]))
            {
                index++;
            }

            if (index >= source.Length)
            {
                break;
            }

            var high = HexValue(source[index]);
            if (high < 0)
            {
                throw InvalidHex(index);
            }

            index++;
            if (index >= source.Length)
            {
                throw InvalidHex(source.Length);
            }

            var low = HexValue(source[index]);
            if (low < 0)
            {
                throw InvalidHex(index);
            }

            index++;
            bytes.Add((byte)((high << 4) | low));
        }

        return CreateBytes([.. bytes], context, span);

        LythonRuntimeException InvalidHex(int position) => new("ValueError", "non-hexadecimal number found in fromhex() arg at position " + position, span);
    }

    private static bool IsHexSpace(char value)
        => value is ' ' or '\t' or '\n' or '\v' or '\f' or '\r';

    private static int HexValue(char value)
    {
        if (value >= '0' && value <= '9')
        {
            return value - '0';
        }

        if (value >= 'a' && value <= 'f')
        {
            return value - 'a' + 10;
        }

        if (value >= 'A' && value <= 'F')
        {
            return value - 'A' + 10;
        }

        return -1;
    }

    private static object StrMaketransImpl(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        RejectKeywordArguments("str.maketrans", arguments, span);
        return StrMaketransPositional(PositionalArguments(arguments), span, context);
    }

    private static object StrMaketransPositional(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length < 1)
        {
            throw new LythonRuntimeException("TypeError", "maketrans expected at least 1 argument, got 0", span);
        }

        if (arguments.Length > 3)
        {
            throw new LythonRuntimeException("TypeError", "maketrans expected at most 3 arguments, got " + arguments.Length, span);
        }

        var result = new PyDict(context.MemoryGovernor, span);
        if (arguments.Length == 1)
        {
            if (arguments[0] is not PyDict table)
            {
                throw new LythonRuntimeException("TypeError", "if you give only one argument to maketrans it must be a dict", span);
            }

            foreach (var pair in table)
            {
                result.SetItem(MaketransKey(pair.Key, span), pair.Value);
            }

            context.ObserveCollectionCount(result.Count, span);
            return result;
        }

        if (arguments[0] is not PyString from)
        {
            throw new LythonRuntimeException("TypeError", "first maketrans argument must be a string if there is a second argument", span);
        }

        if (arguments[1] is not PyString to)
        {
            throw new LythonRuntimeException("TypeError", "maketrans() argument 2 must be str, not " + UnboundTypeMethod.PythonTypeName(arguments[1], context), span);
        }

        var fromRunes = ToRunes(from);
        var toRunes = ToRunes(to);
        if (fromRunes.Count != toRunes.Count)
        {
            throw new LythonRuntimeException("ValueError", "the first two maketrans arguments must have equal length", span);
        }

        for (var i = 0; i < fromRunes.Count; i++)
        {
            result.SetItem(new BigInteger(fromRunes[i].Value), new BigInteger(toRunes[i].Value));
        }

        if (arguments.Length == 3)
        {
            if (arguments[2] is not PyString deletions)
            {
                throw new LythonRuntimeException("TypeError", "maketrans() argument 3 must be str, not " + UnboundTypeMethod.PythonTypeName(arguments[2], context), span);
            }

            foreach (var rune in ToRunes(deletions))
            {
                result.SetItem(new BigInteger(rune.Value), PyNone.Instance);
            }
        }

        context.ObserveCollectionCount(result.Count, span);
        return result;
    }

    private static List<Rune> ToRunes(PyString text)
    {
        var runes = new List<Rune>();
        foreach (var rune in text.AsString().EnumerateRunes())
        {
            runes.Add(rune);
        }

        return runes;
    }

    // Translate-table keys are single code points or integers; values ride
    // through unvalidated like CPython (translate() rejects them instead).
    private static object MaketransKey(object key, LythonSourceSpan span)
    {
        if (key is PyString text)
        {
            var runes = ToRunes(text);
            if (runes.Count != 1)
            {
                throw new LythonRuntimeException("ValueError", "string keys in translate table must be of length 1", span);
            }

            return new BigInteger(runes[0].Value);
        }

        if (key is BigInteger or int or bool)
        {
            return key;
        }

        throw new LythonRuntimeException("TypeError", "keys in translate table must be strings or integers", span);
    }

    // int.to_bytes and int.from_bytes share the (first, byteorder, signed)
    // shape: the first two ride positionally or by keyword, signed is
    // keyword-only, with the exact CPython diagnostics.
    private static void ParseByteOrderArguments(string method, string firstName, CallArgumentValue[] arguments, LythonSourceSpan span, out object? first, out object? byteorder, out object? signed)
    {
        first = null;
        byteorder = null;
        signed = null;
        var haveFirst = false;
        var haveOrder = false;
        var totalPositionals = 0;
        foreach (var argument in arguments)
        {
            if (argument.IsPositional)
            {
                totalPositionals++;
            }
        }

        var positionalIndex = 0;
        foreach (var argument in arguments)
        {
            if (!argument.IsKeyword)
            {
                positionalIndex++;
                if (positionalIndex == 1)
                {
                    first = argument.Value;
                    haveFirst = true;
                }
                else if (positionalIndex == 2)
                {
                    byteorder = argument.Value;
                    haveOrder = true;
                }
                else
                {
                    throw new LythonRuntimeException("TypeError", method + "() takes at most 2 positional arguments (" + totalPositionals + " given)", span);
                }

                continue;
            }

            if (argument.KeywordName == firstName)
            {
                if (haveFirst)
                {
                    throw new LythonRuntimeException("TypeError", "argument for " + method + "() given by name ('" + firstName + "') and position (1)", span);
                }

                first = argument.Value;
                haveFirst = true;
            }
            else if (argument.KeywordName == "byteorder")
            {
                if (haveOrder)
                {
                    throw new LythonRuntimeException("TypeError", "argument for " + method + "() given by name ('byteorder') and position (2)", span);
                }

                byteorder = argument.Value;
                haveOrder = true;
            }
            else if (argument.KeywordName == "signed")
            {
                signed = argument.Value;
            }
            else
            {
                throw new LythonRuntimeException("TypeError", method + "() got an unexpected keyword argument '" + argument.KeywordName + "'", span);
            }
        }
    }

    private static bool ParseByteOrder(object? byteorder, string method, ExecutionContext context, LythonSourceSpan span)
    {
        if (byteorder is null)
        {
            return true;
        }

        if (byteorder is not PyString text)
        {
            throw new LythonRuntimeException("TypeError", method + "() argument 'byteorder' must be str, not " + UnboundTypeMethod.PythonTypeName(byteorder, context), span);
        }

        var order = text.AsString();
        if (string.Equals(order, "little", StringComparison.Ordinal))
        {
            return true;
        }

        if (string.Equals(order, "big", StringComparison.Ordinal))
        {
            return false;
        }

        throw new LythonRuntimeException("ValueError", "byteorder must be either 'little' or 'big'", span);
    }

    // __index__-style operands (including user slots through the shared
    // protocol) with the exact CPython rejection text.
    private static BigInteger InterpretByteInteger(object? value, ExecutionContext context, LythonSourceSpan span)
    {
        var coerced = CoerceIndexProtocol(value, context, span);
        return coerced switch
        {
            BigInteger big => big,
            int small => new BigInteger(small),
            bool flag => flag ? BigInteger.One : BigInteger.Zero,
            _ => throw new LythonRuntimeException("TypeError", "'" + UnboundTypeMethod.PythonTypeName(value, context) + "' object cannot be interpreted as an integer", span),
        };
    }

    private static object IntToBytes(BigInteger value, CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        ParseByteOrderArguments("to_bytes", "length", arguments, span, out var lengthValue, out var orderValue, out var signedValue);
        var length = lengthValue is null ? BigInteger.One : InterpretByteInteger(lengthValue, context, span);
        if (length < 0)
        {
            throw new LythonRuntimeException("ValueError", "length argument must be non-negative", span);
        }

        if (length > int.MaxValue)
        {
            throw new LythonRuntimeException("OverflowError", "bytes object is too large", span);
        }

        var littleEndian = ParseByteOrder(orderValue, "to_bytes", context, span);
        var signed = signedValue is not null && IsTruthy(signedValue);
        var requiredBits = value.Sign < 0
            ? value == BigInteger.MinusOne ? 1 : BigInteger.Subtract(BigInteger.Abs(value), BigInteger.One).GetBitLength() + 1
            : value.GetBitLength() + 1;
        if (value.Sign >= 0 && !signed)
        {
            requiredBits = value.GetBitLength();
        }

        if (value.Sign < 0 && !signed)
        {
            throw new LythonRuntimeException("OverflowError", "can't convert negative int to unsigned", span);
        }

        if (BigInteger.Multiply(8, length) < requiredBits)
        {
            throw new LythonRuntimeException("OverflowError", "int too big to convert", span);
        }

        var size = (int)length;
        var raw = new byte[size];
        var remaining = value;
        var count = 0;
        while (count < size && !remaining.IsZero && remaining != BigInteger.MinusOne)
        {
            raw[count] = (byte)(remaining & 0xFF);
            remaining >>= 8;
            count++;
        }

        if (!remaining.IsZero && remaining != BigInteger.MinusOne)
        {
            throw new LythonRuntimeException("OverflowError", "int too big to convert", span);
        }

        if (value.Sign < 0)
        {
            Array.Fill(raw, (byte)0xFF, count, size - count);
        }

        if (!littleEndian)
        {
            Array.Reverse(raw);
        }

        return CreateBytes(raw, context, span);
    }

    private static object IntFromBytes(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        ParseByteOrderArguments("from_bytes", "bytes", arguments, span, out var source, out var orderValue, out var signedValue);
        if (source is null)
        {
            throw new LythonRuntimeException("TypeError", "from_bytes() missing required argument 'bytes' (pos 1)", span);
        }

        var littleEndian = ParseByteOrder(orderValue, "from_bytes", context, span);
        var signed = signedValue is not null && IsTruthy(signedValue);
        var raw = source switch
        {
            PyBytes bytes => bytes.ToArray(),
            PyString => throw new LythonRuntimeException("TypeError", "cannot convert '" + UnboundTypeMethod.PythonTypeName(source, context) + "' object to bytes", span),
            _ => FromBytesOperands(source, context, span),
        };

        if (!littleEndian)
        {
            Array.Reverse(raw);
        }

        var magnitude = BigInteger.Zero;
        var shift = 0;
        foreach (var octet in raw)
        {
            magnitude |= ((BigInteger)octet) << shift;
            shift += 8;
        }

        if (signed && raw.Length > 0 && (raw[raw.Length - 1] & 0x80) != 0)
        {
            magnitude -= BigInteger.One << (8 * raw.Length);
        }

        return magnitude;
    }

    private static byte[] FromBytesOperands(object source, ExecutionContext context, LythonSourceSpan span)
    {
        var octets = new List<byte>();
        try
        {
            foreach (var item in ToSequence(source, span, context))
            {
                var number = InterpretByteInteger(item, context, span);
                if (number < 0 || number > 255)
                {
                    throw new LythonRuntimeException("ValueError", "bytes must be in range(0, 256)", span);
                }

                octets.Add((byte)number);
            }
        }
        catch (LythonRuntimeException ex) when (ex.ExceptionType == "TypeError" &&
            (ex.Message == "Object is not iterable." ||
                (source is PyInstance instance && ex.Message == "'" + instance.Type.Name + "' object is not iterable")))
        {
            throw new LythonRuntimeException("TypeError", "cannot convert '" + UnboundTypeMethod.PythonTypeName(source, context) + "' object to bytes", span);
        }

        return [.. octets];
    }

    private static object FloatFromHex(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        RejectKeywordArguments("float.fromhex", arguments, span);
        var positional = PositionalArguments(arguments);
        if (positional.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "float.fromhex() takes exactly one argument (" + positional.Length + " given)", span);
        }

        if (positional[0] is not PyString text)
        {
            throw new LythonRuntimeException("TypeError", "bad argument type for built-in operation", span);
        }

        return FloatFromHex(text.AsString(), span);
    }

    private static object BoolFromBytes(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        // IntFromBytes always returns the BigInteger magnitude.
        var number = (BigInteger)IntFromBytes(arguments, span, context);
        return !number.IsZero;
    }

    private static object BytesMaketransImpl(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        _ = context;
        RejectKeywordArguments("bytes.maketrans", arguments, span);
        var positional = PositionalArguments(arguments);
        if (positional.Length != 2)
        {
            throw new LythonRuntimeException("TypeError", "maketrans expected 2 arguments, got " + positional.Length, span);
        }

        if (positional[0] is not PyBytes from || positional[1] is not PyBytes to)
        {
            var offender = positional[0] is not PyBytes ? positional[0] : positional[1];
            throw new LythonRuntimeException("TypeError", "a bytes-like object is required, not '" + UnboundTypeMethod.PythonTypeName(offender, context) + "'", span);
        }

        var source = from.ToArray();
        var target = to.ToArray();
        if (source.Length != target.Length)
        {
            throw new LythonRuntimeException("ValueError", "maketrans arguments must have same length", span);
        }

        var table = new byte[256];
        for (var i = 0; i < 256; i++)
        {
            table[i] = (byte)i;
        }

        for (var i = 0; i < source.Length; i++)
        {
            table[source[i]] = target[i];
        }

        return CreateBytes(table, context, span);
    }

    // Shared choke point for unbound builtin type methods: only constructors
    // for types with instance member tables (list, str, bytes, dict, set)
    // serve descriptors, and only for members their tables resolve to a
    // callable on a probe receiver, so every other miss keeps the shared
    // missing-member error.
    private static bool TryGetUnboundTypeMethod(object owner, string ownerName, ref Dictionary<string, UnboundTypeMethod>? cache, string memberName, [MaybeNullWhen(false)] out object value)
    {
        value = PyNone.Instance;
        var probe = ownerName switch
        {
            "list" => (object)new PyList(),
            "str" => PyString.FromString(""),
            "bytes" => new PyBytes([]),
            "dict" => new PyDict(),
            "tuple" => new PyTuple(Array.Empty<object>()),
            "int" => BigInteger.Zero,
            "float" => 0.0,
            "range" => new PyRange(BigInteger.Zero, BigInteger.Zero, BigInteger.One),
            "set" => new PySet(),
            _ => null,
        };

        if (probe is null ||
            !PyMemberAccess.TryResolveInstanceTableMember(probe, memberName, out var resolved) ||
            resolved is not ICallable)
        {
            return false;
        }

        cache ??= new Dictionary<string, UnboundTypeMethod>(StringComparer.Ordinal);
        if (!cache.TryGetValue(memberName, out var method))
        {
            method = new UnboundTypeMethod(owner, ownerName, memberName);
            cache[memberName] = method;
        }

        value = method;
        return true;
    }

    // Shared choke point for unbound builtin data descriptors: int and float
    // expose their scalar data attributes as getset descriptors like CPython.
    // Only members the instance tables resolve to a plain value qualify, so
    // methods keep flowing through the descriptor choke above; bool shares
    // int's cache through the delegation below.
    private static bool TryGetUnboundDataDescriptor(object owner, string ownerName, ref Dictionary<string, BuiltinDataDescriptor>? cache, string memberName, [MaybeNullWhen(false)] out object value)
    {
        value = PyNone.Instance;
        (string? documentation, DataDescriptorKind? kind) = (ownerName, memberName) switch
        {
            ("int", "real") => ("the real part of a complex number", DataDescriptorKind.GetSet),
            ("int", "imag") => ("the imaginary part of a complex number", DataDescriptorKind.GetSet),
            ("int", "numerator") => ("the numerator of a rational number in lowest terms", DataDescriptorKind.GetSet),
            ("int", "denominator") => ("the denominator of a rational number in lowest terms", DataDescriptorKind.GetSet),
            ("float", "real") => ("the real part of a complex number", DataDescriptorKind.GetSet),
            ("float", "imag") => ("the imaginary part of a complex number", DataDescriptorKind.GetSet),
            ("range", "start") => ((string?)null, DataDescriptorKind.Member),
            ("range", "stop") => ((string?)null, DataDescriptorKind.Member),
            ("range", "step") => ((string?)null, DataDescriptorKind.Member),
            _ => ((string?)null, (DataDescriptorKind?)null),
        };

        if (kind is null)
        {
            return false;
        }

        var probe = ownerName switch
        {
            "int" => (object)BigInteger.Zero,
            "float" => (object)0.0,
            "range" => (object)new PyRange(BigInteger.Zero, BigInteger.Zero, BigInteger.One),
            _ => null,
        };

        if (probe is null ||
            !PyMemberAccess.TryResolveInstanceTableMember(probe, memberName, out var resolved) ||
            resolved is ICallable)
        {
            return false;
        }

        cache ??= new Dictionary<string, BuiltinDataDescriptor>(StringComparer.Ordinal);
        if (!cache.TryGetValue(memberName, out var descriptor))
        {
            descriptor = new BuiltinDataDescriptor(owner, ownerName, memberName, documentation, kind.Value);
            cache[memberName] = descriptor;
        }

        value = descriptor;
        return true;
    }

    // Marks bound engine-method wrappers (per-access or receiver-bound) so member
    // resolution treats them uniformly without naming generic instantiations.
    internal interface IPyBoundEngineMethod
    {
    }

    // Marks raw bound callables carrying names (dict/defaultdict update,
    // bytes translate, int to_bytes) so equality treats them uniformly
    // without naming the private wrapper.
    internal interface IPyRawBoundCallable
    {
        string? BoundName { get; }

        object? BoundReceiver { get; }
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
            // Construction routes to the owning type like CPython
            // (int.__new__(int, value) builds int(value)); exception slots
            // forward to the receiving subclass instead. Anything else
            // fails explicitly since cls-threading is unsupported.
            if (arguments.Length == 0 || arguments[0].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "type.__new__ construction expects the type as its first argument in Lython.", span);
            }

            if (ReferenceEquals(arguments[0].Value, _owner) &&
                _owner is LythonRuntime.ICallable ownerCallable)
            {
                return ownerCallable.Invoke(arguments[1..], span, context);
            }

            if (_owner is LythonRuntime.ExceptionTypeValue &&
                arguments[0].Value is LythonRuntime.ExceptionTypeValue subclassCtor)
            {
                return subclassCtor.Invoke(arguments[1..], span, context);
            }

            throw new LythonRuntimeException("TypeError", "type.__new__ construction expects the type as its first argument in Lython.", span);
        }
    }

    // Pure-Python-modeled types expose their __new__ slot as a plain function
    // like CPython (no __self__, function identity); wrappers are cached on
    // the owning type object beside it.
    internal sealed class FunctionNewMethod : ICallable, IPyDynamicAttributes
    {
        private readonly object _owner;
        private readonly string _shortName;

        internal FunctionNewMethod(object owner, string shortName)
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

            value = PyNone.Instance;
            return false;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            // Construction routes to the owning type like CPython
            // (NT.__new__(NT, value) builds NT(value)); anything else fails
            // explicitly since cls-threading is unsupported.
            if (arguments.Length == 0 || arguments[0].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "type.__new__ construction expects the type as its first argument in Lython.", span);
            }

            if (ReferenceEquals(arguments[0].Value, _owner) &&
                _owner is LythonRuntime.ICallable ownerCallable)
            {
                return ownerCallable.Invoke(arguments[1..], span, context);
            }

            throw new LythonRuntimeException("TypeError", "type.__new__ construction expects the type as its first argument in Lython.", span);
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

        if (ReferenceEquals(classValue, PyType.EllipsisType))
        {
            _ellipsisTypeNewSlot ??= new TypeNewMethod(classValue, PyType.EllipsisType.Name);
            value = _ellipsisTypeNewSlot;
            return true;
        }

        if (ReferenceEquals(classValue, PyType.NotImplementedType))
        {
            _notImplementedTypeNewSlot ??= new TypeNewMethod(classValue, PyType.NotImplementedType.Name);
            value = _notImplementedTypeNewSlot;
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

        // deque owns its slot through its stable collections member, like
        // the builtin constructors above.
        if (classValue is CollectionsCallable dequeCallable && dequeCallable.Name is "collections.deque")
        {
            dequeCallable.NewSlot ??= new TypeNewMethod(dequeCallable, "deque");
            value = dequeCallable.NewSlot;
            return true;
        }

        // The partial factory owns its slot through its global singleton,
        // like the builtin constructors above.
        if (classValue is PartialFactory partialFactory)
        {
            partialFactory.NewSlot ??= new TypeNewMethod(partialFactory, "partial");
            value = partialFactory.NewSlot;
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
    private static TypeNewMethod? _ellipsisTypeNewSlot;
    private static TypeNewMethod? _notImplementedTypeNewSlot;
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

        private Dictionary<string, UnboundTypeMethod>? _unboundMethods;
        private Dictionary<string, BuiltinDataDescriptor>? _unboundDataDescriptors;

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

            // Builtin constructors expose classmethods (dict.fromkeys,
            // bytes.fromhex) and staticmethods (str.maketrans) like CPython
            // through one shared choke. It runs before the unbound
            // descriptors because the instance tables serve both shapes.
            if (TryGetBuiltinTypeMethod(this, Signature.Name, name, out value))
            {
                return true;
            }

            // Type constructors expose their instance methods as unbound
            // descriptors like CPython (list.append takes its receiver as
            // the first argument); wrappers cache per constructor.
            if (BuiltinTypeBaseNames.ContainsKey(Signature.Name) &&
                TryGetUnboundTypeMethod(this, Signature.Name, ref _unboundMethods, name, out value))
            {
                return true;
            }

            // Builtin numeric types expose their scalar data attributes as
            // getset descriptors like CPython (int.real binds through the
            // shared data slots); wrappers cache per constructor.
            if ((Signature.Name is "int" or "float" or "range") &&
                TryGetUnboundDataDescriptor(this, Signature.Name, ref _unboundDataDescriptors, name, out value))
            {
                return true;
            }

            // bool shares int's method surface like CPython
            // (bool.bit_length is int.bit_length): own dunders resolved
            // above and bool.from_bytes in the choke above, so whatever
            // remains delegates to the int constructor's cache.
            if (Signature.Name == "bool" &&
                !TryGetMember(name, out value) &&
                context.TryGetBuiltin("int", out var intType) &&
                intType is BuiltinCallable intCallable &&
                intCallable.TryGetMember(name, context, span, out value))
            {
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

    private sealed class BoundCallable : DelegateBoundArgumentsCallable, IPyDynamicAttributes, IPyBoundEngineMethod, IPyHashableValue
    {
        public int GetPyHashCode() => HashCode.Combine(RuntimeHelpers.GetHashCode(_receiver), StringComparer.Ordinal.GetHashCode(ShortMethodName(Signature.Name)));

        // The receiver threads in at member-resolution time (see
        // TryResolveRuntimeMember), since every instance is fresh per
        // access and closures alone cannot report it.
        private object? _receiver;

        internal void AttachReceiver(object receiver) => _receiver = receiver;

        // Bound engine methods expose CPython-style __name__/__module__ like
        // C-implemented methods: the short decorated name and None, since every
        // wrapper is engine-implemented (CPython reports the class module only
        // for Python-implemented methods such as Random.gauss).
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name == "__self__" && _receiver is not null)
            {
                value = _receiver;
                return true;
            }

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

    private sealed class NoArgumentsReceiverBoundCallable<TReceiver> : ICallable, IPyDynamicAttributes, IPyBoundEngineMethod, IPyHashableValue
    {
        public int GetPyHashCode() => HashCode.Combine(RuntimeHelpers.GetHashCode(_receiver), StringComparer.Ordinal.GetHashCode(ShortMethodName(Name)));

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

        private Dictionary<string, UnboundTypeMethod>? _unboundMethods;

        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            if ((name == "__bases__" || name == "__mro__") &&
                TryGetOwnedHierarchy("dict", this, context, span, ref _hierarchy, out var hierarchy))
            {
                value = name == "__mro__" ? hierarchy.Mro : hierarchy.Bases;
                return true;
            }

            // dict.fromkeys rides the shared classmethod choke like the
            // builtin constructors above. It runs before the unbound
            // descriptors because the instance table serves both shapes.
            if (TryGetBuiltinTypeMethod(this, Name, name, out value))
            {
                return true;
            }

            // The dict constructor exposes its instance methods as unbound
            // descriptors like CPython, sharing the builtin choke and cache.
            if (TryGetUnboundTypeMethod(this, Name, ref _unboundMethods, name, out value))
            {
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

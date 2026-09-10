using System.Globalization;
using System.Numerics;
using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    // Fixed sys vocabulary and identity-stable singletons: share every
    // constant forever like the pkgutil labels, so per-access reads alias
    // stably instead of allocating fresh objects on every access.
    private static readonly PyString DisplayVersionText = PyString.FromString(LythonPythonVersion.DisplayVersion);
    private static readonly PyString LythonNameText = PyString.FromString("lython");
    private static readonly PyString LittleEndianText = PyString.FromString("little");
    private static readonly PyString Utf8EncodingText = PyString.FromString("utf-8");
    private static readonly PyString ReleaseLevelText = PyString.FromString(LythonPythonVersion.ReleaseLevel);
    private static readonly PyString CacheTagText = PyString.FromString(LythonPythonVersion.CacheTag);

    private sealed class SysModule : PyModule, IPyContextualDynamicAttributes
    {
        private static readonly PyNamedTupleType VersionInfoType = new(
            "sys.version_info",
            ["major", "minor", "micro", "releaselevel", "serial"]);

        private static readonly PyNamedTupleObject SharedVersionInfo = VersionInfoType.CreateFromValues(
            [
                new BigInteger(LythonPythonVersion.Major),
                new BigInteger(LythonPythonVersion.Minor),
                new BigInteger(LythonPythonVersion.Micro),
                ReleaseLevelText,
                new BigInteger(LythonPythonVersion.Serial)
            ],
            span: null);
        private static readonly SysImplementationObject SharedImplementation = new(SharedVersionInfo, new BigInteger(LythonPythonVersion.HexVersion));

        private readonly ExecutionContext _context;
        private readonly PyList _argv;
        private readonly PyString _prefix;
        private readonly PyString _basePrefix;
        private readonly PyList _path;
        private readonly PyTuple _builtinModuleNames;
        private readonly PySet _stdlibModuleNames;
        private readonly HostTextInputHandle _stdin;
        private readonly HostTextOutputHandle _stdout;
        private readonly HostTextOutputHandle _stderr;
        private PyDict? _modules;

        public SysModule(ExecutionContext context) : base("sys")
        {
            _context = context;
            var state = context.State;
            var args = new object[state.Args.Count];
            for (var i = 0; i < args.Length; i++)
            {
                args[i] = state.Args[i];
            }

            _argv = new PyList(args, state.MemoryGovernor, null);
            _prefix = PyString.FromString(context.Host.Cwd, state.MemoryGovernor, null);
            _basePrefix = PyString.FromString(context.Host.Cwd, state.MemoryGovernor, null);
            _path = new PyList([PyString.FromString(ContainedImportBaseDirectory(context), state.MemoryGovernor, null)], state.MemoryGovernor, null);
            var builtinNames = EnumerateDiscoverableBuiltinModuleNames(context).Order(StringComparer.Ordinal).ToArray();
            _builtinModuleNames = new PyTuple(builtinNames.Select(name => PyString.FromString(name, state.MemoryGovernor, null)).ToArray(), state.MemoryGovernor, null);
            _stdlibModuleNames = new PySet(builtinNames.Select(name => PyString.FromString(name, state.MemoryGovernor, null)), state.MemoryGovernor);
            _stdin = state.Stdin;
            _stdout = state.Stdout;
            _stderr = state.Stderr;
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "argv" => _argv,
                "stdin" => _stdin,
                "stdout" => _stdout,
                "stderr" => _stderr,
                "version" => DisplayVersionText,
                "version_info" => SharedVersionInfo,
                "hexversion" => new BigInteger(LythonPythonVersion.HexVersion),
                "implementation" => SharedImplementation,
                "platform" => LythonNameText,
                "maxsize" => new BigInteger(long.MaxValue),
                "byteorder" => LittleEndianText,
                "prefix" => _prefix,
                "base_prefix" => _basePrefix,
                "executable" => LythonNameText,
                "path" => _path,
                "modules" => GetModules(),
                "builtin_module_names" => _builtinModuleNames,
                "stdlib_module_names" => _stdlibModuleNames,
                "exit" => BuiltinCallable.Create(LythonKnownCallableSignatures.SysExit, Exit),
                "getdefaultencoding" => BuiltinCallable.Create(LythonKnownCallableSignatures.SysGetDefaultEncoding, GetDefaultEncoding),
                "exc_info" => BuiltinCallable.Create(LythonKnownCallableSignatures.SysExcInfo, ExcInfo),
                "getsizeof" => BuiltinCallable.Create(LythonKnownCallableSignatures.SysGetSizeOf, GetSizeOf),
                "settrace" => UnsupportedSysCallable(LythonKnownCallableSignatures.SysSetTrace, "sys.settrace(...) is not supported by Lython."),
                "setprofile" => UnsupportedSysCallable(LythonKnownCallableSignatures.SysSetProfile, "sys.setprofile(...) is not supported by Lython."),
                "setrecursionlimit" => UnsupportedSysCallable(LythonKnownCallableSignatures.SysSetRecursionLimit, "sys.setrecursionlimit(...) is not supported by Lython; use LythonRunOptions.MaxRecursionDepth."),
                "addaudithook" => UnsupportedSysCallable(LythonKnownCallableSignatures.SysAddAuditHook, "sys.addaudithook(...) is not supported by Lython."),
                "audit" => UnsupportedSysCallable(LythonKnownCallableSignatures.SysAudit, "sys.audit(...) is not supported by Lython."),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TryGetMember(
            string name,
            ExecutionContext context,
            LythonSourceSpan span,
            [MaybeNullWhen(false)] out object value)
        {
            _ = context;
            _ = span;
            return TryGetMember(name, out value);
        }

        private PyDict GetModules()
        {
            _modules ??= CreateModulesSnapshot(_context);
            foreach (var pair in _context.State.ImportedModules)
            {
                _modules.SetItem(PyString.FromString(pair.Key, _context.MemoryGovernor), pair.Value);
            }

            return _modules;
        }

        private static object Exit(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length > 1)
            {
                throw new LythonRuntimeException("TypeError", "sys.exit([code]) expects zero or one argument.", span);
            }

            var value = arguments.Length == 0 ? PyNone.Instance : arguments[0];
            throw new LythonRuntimeException("SystemExit", FormatSystemExitMessage(value), span, innerException: null, payload: value);
        }

        private static object GetDefaultEncoding(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "sys.getdefaultencoding() expects no arguments.", span);
            }

            return Utf8EncodingText;
        }

        private static object ExcInfo(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "sys.exc_info() expects no arguments.", span);
            }

            return context.Services.CurrentException is { } exception
                ? new PyTuple(
                    [
                        new ExceptionTypeValue(exception.Identity),
                        exception,
                        PyNone.Instance
                    ],
                    context.MemoryGovernor,
                    span)
                : PyTuple.FromOwnedArray([PyNone.Instance, PyNone.Instance, PyNone.Instance], context.MemoryGovernor, span);
        }

        private static object GetSizeOf(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = span;
            _ = context;
            if (arguments.Length is < 1 or > 2)
            {
                throw new LythonRuntimeException("TypeError", "sys.getsizeof(object[, default]) expects one or two arguments.", span);
            }

            if (TryEstimateSize(arguments[0], out var size))
            {
                return new BigInteger(size);
            }

            return arguments.Length == 2 ? arguments[1] : BigInteger.Zero;
        }

        private static bool TryEstimateSize(object value, out long size)
        {
            size = value switch
            {
                PyNone => 16,
                bool => 16,
                BigInteger integer => RuntimeMemoryEstimates.EstimateBigIntegerBytes(integer),
                double => 24,
                PyString text => 40 + text.Utf8Bytes.Length,
                PyBytes bytes => 40 + bytes.Length,
                PyPath path => 40 + path.Value.Utf8Bytes.Length,
                PyList list => 32 + (16L * list.Count),
                PyTuple tuple => PyTuple.EstimateApproximateBytes(tuple.Count),
                PyDict dict => 64 + (32L * dict.Count),
                PySet set => 80 + (24L * set.Count),
                PyException => 64,
                PyModule => 64,
                HostTextInputHandle or HostTextOutputHandle => 32,
                _ => 0
            };

            return size > 0;
        }

        private static BuiltinCallable UnsupportedSysCallable(LythonCallableSignature signature, string message)
            => BuiltinCallable.Create(signature, (_, span, _) => throw new LythonRuntimeException("NotImplementedError", message, span));

        private static string ContainedImportBaseDirectory(ExecutionContext context)
            => context.SourcePath is null ? context.Host.Cwd : PathOps.Parent(context.SourcePath);


        private static PyDict CreateModulesSnapshot(ExecutionContext context)
        {
            var modules = new PyDict(context.MemoryGovernor);
            foreach (var name in EnumerateDiscoverableBuiltinModuleNames(context).Order(StringComparer.Ordinal))
            {
                var module = ResolveBuiltinModule(name, context);
                if (module is not null)
                {
                    modules.SetItem(PyString.FromString(name, context.MemoryGovernor), module);
                }
            }

            foreach (var pair in context.State.ImportedModules.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                modules.SetItem(PyString.FromString(pair.Key, context.MemoryGovernor), pair.Value);
            }

            return modules;
        }
    }

    private sealed class SysImplementationObject : IPyDynamicAttributes, IPyRenderableValue
    {
        private readonly PyNamedTupleObject _version;
        private readonly BigInteger _hexversion;

        public SysImplementationObject(PyNamedTupleObject version, BigInteger hexversion)
        {
            _version = version;
            _hexversion = hexversion;
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "name" => LythonNameText,
                "version" => _version,
                "hexversion" => _hexversion,
                "cache_tag" => CacheTagText,
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString($"namespace(name='lython', cache_tag='{LythonPythonVersion.CacheTag}')");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class DataclassesModule : PyModule
    {
        public static readonly DataclassesModule Instance = new();

        private DataclassesModule()
            : base("dataclasses")
        {
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name == "dataclass")
            {
                value = PyDataclass.DataclassCallable;
                return true;
            }

            if (name == "Field")
            {
                value = PyDataclass.FieldType;
                return true;
            }

            if (name == "field")
            {
                value = PyDataclass.FieldCallable;
                return true;
            }

            if (name == "make_dataclass")
            {
                value = PyDataclass.MakeDataclassCallable;
                return true;
            }

            if (name == "is_dataclass")
            {
                value = BuiltinCallable.Create(
                    LythonKnownCallableSignatures.DataclassesIsDataclass,
                    PyDataclass.IsDataclass);
                return true;
            }

            if (name == "fields")
            {
                value = BuiltinCallable.Create(
                    LythonKnownCallableSignatures.DataclassesFields,
                    PyDataclass.Fields);
                return true;
            }

            if (name == "asdict")
            {
                value = BuiltinCallable.Create(
                    LythonKnownCallableSignatures.DataclassesAsDict,
                    PyDataclass.AsDict);
                return true;
            }

            if (name == "astuple")
            {
                value = BuiltinCallable.Create(
                    LythonKnownCallableSignatures.DataclassesAsTuple,
                    PyDataclass.AsTuple);
                return true;
            }

            if (name == "replace")
            {
                value = PyDataclass.ReplaceCallable;
                return true;
            }

            if (name == "MISSING")
            {
                value = PyDataclassMissing.Instance;
                return true;
            }

            if (name == "KW_ONLY")
            {
                value = PyDataclassKwOnlyMarker.Instance;
                return true;
            }

            if (name == "InitVar")
            {
                value = PyDataclassInitVarMarker.Instance;
                return true;
            }

            if (name == "FrozenInstanceError")
            {
                value = new ExceptionTypeValue(ModuleException("dataclasses", "FrozenInstanceError"));
                return true;
            }

            value = PyNone.Instance;
            return false;
        }
    }

    private sealed class TypingModule : PyModule
    {
        public static readonly TypingModule Instance = new();

        private TypingModule()
            : base("typing")
        {
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            => PyTyping.TryGetMember(name, out value);
    }

}

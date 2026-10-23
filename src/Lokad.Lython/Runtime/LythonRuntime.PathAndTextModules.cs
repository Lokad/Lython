using System.Globalization;
using System.Numerics;
using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static object PathClass(PyPath path) => path.IsPure ? PathlibModule.PurePosixPathType : PathlibModule.PosixPathType;

    private sealed class PathlibModule : PyModule
    {
        public static readonly PathlibModule Instance = new();
        internal static readonly PathlibPathType PathType = new("Path", isSupported: true);
        internal static readonly PathlibPathType PurePathType = new("PurePath", isSupported: true);
        internal static readonly PathlibPathType PurePosixPathType = new("PurePosixPath", isSupported: true);
        internal static readonly PathlibPathType PosixPathType = new("PosixPath", isSupported: true);
        internal static readonly PathlibPathType PureWindowsPathType = new("PureWindowsPath", isSupported: false);
        internal static readonly PathlibPathType WindowsPathType = new("WindowsPath", isSupported: false);

        private PathlibModule() : base("pathlib")
        {
        }

        public override IReadOnlyList<string> ExportedNames
            => ["Path", "PurePath", "PurePosixPath", "PosixPath", "PureWindowsPath", "WindowsPath"];

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "Path" => PathType,
                "PurePath" => PurePathType,
                "PurePosixPath" => PurePosixPathType,
                "PosixPath" => PosixPathType,
                "PureWindowsPath" => PureWindowsPathType,
                "WindowsPath" => WindowsPathType,
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static async ValueTask<object> CreatePath(CallArgumentValue[] arguments, bool pure,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
            try
            {
                PyString result = PyStringOps.DotLiteral;
                for (var i = 0; i < arguments.Length; i++)
                {
                    if (arguments[i].IsKeyword) throw CallErrors.NoKeywordArguments(PythonCallableKind.Builtin, "pathlib.Path", span);
                    var part = asynchronous
                        ? await CoercePathLikeAsync(arguments[i].Value, context, span, "pathlib.Path").ConfigureAwait(false)
                        : CoercePathLike(arguments[i].Value, context, span, "pathlib.Path");
                    if (arguments.Length == 1) result = PathOps.NormalizeLexical(part, context, span);
                    else PathOps.AppendNormalized(builder, part, context, span);
                }
                if (arguments.Length > 1) result = PathOps.Finish(builder);
                return OwnPathResult(result, result, context.MemoryGovernor, span, pure);
            }
            finally { builder.Release(); }
        }

        internal sealed class PathlibPathType : ICallable, IPyDynamicAttributes, IPyContextualDynamicAttributes, IPyRenderableValue, INamedRuntimeCallable
        {
            private readonly bool _isSupported;
            private readonly PyString _nameValue;

            public PathlibPathType(string shortName, bool isSupported)
            {
                ShortName = shortName;
                _isSupported = isSupported;
                _nameValue = PyString.FromString(shortName);
            }

            public string ShortName { get; }

            public string Name => $"pathlib.{ShortName}";

            internal LythonRuntime.FunctionNewMethod? NewSlot { get; set; }

            internal LythonRuntime.FunctionNewMethod GetNewSlot() => NewSlot ??= new LythonRuntime.FunctionNewMethod(this, ShortName);

            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
                => InvokeCore(arguments, span, context, false).GetAwaiter().GetResult();

            public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
                => InvokeCore(arguments, span, context, true);

            private ValueTask<object> InvokeCore(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
            {
                context.CheckExecution(span);
                if (!_isSupported) throw new LythonRuntimeException("NotImplementedError", $"{Name} is not supported by Lython's normalized POSIX-like path model.", span);
                return CreatePath(arguments, ShortName.StartsWith("Pure", StringComparison.Ordinal), span, context, asynchronous);
            }

            // Path types resolve __module__ through the shared label catalog and
            // __bases__/__mro__ per read: bases mix sibling type singletons with the
            // run builtins table, so tuples cannot be shared across runs.
            public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
            {
            if (name == "__module__")
            {
                value = ExceptionTypeValue.SharedModuleLabel("pathlib");
                return true;
            }

            if (name == "__bases__" || name == "__mro__")
            {
                var bases = GetBaseObjects(ShortName, context);
                if (bases is null)
                {
                    value = PyNone.Instance;
                    return false;
                }

                if (name == "__bases__")
                {
                    value = new PyTuple(bases, context.MemoryGovernor, span);
                    return true;
                }

                // __mro__ always terminates at object like CPython.
                if (!context.TryGetBuiltin("object", out var objectBase) || objectBase is null)
                {
                    value = PyNone.Instance;
                    return false;
                }

                object[] mro = ShortName switch
                {
                    "PosixPath" => [this, PathType, PurePosixPathType, PurePathType, objectBase],
                    "Path" or "PurePosixPath" => [this, PurePathType, objectBase],
                    _ => [this, objectBase]
                };

                value = new PyTuple(mro, context.MemoryGovernor, span);
                return true;
            }

            return TryGetMember(name, out value);
            }

            private static object[]? GetBaseObjects(string shortName, ExecutionContext context)
            {
            if (!context.TryGetBuiltin("object", out var obj) || obj is null)
            {
                return null;
            }

            return shortName switch
            {
                "PurePath" => [obj],
                "Path" => [PathlibModule.PurePathType],
                "PurePosixPath" => [PathlibModule.PurePathType],
                "PosixPath" => [PathlibModule.PathType, PathlibModule.PurePosixPathType],
                "PureWindowsPath" => [PathlibModule.PurePathType],
                "WindowsPath" => [PathlibModule.PathType],
                _ => [obj],
            };
            }

            public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch
                {
                    "cwd" when _isSupported && !ShortName.StartsWith("Pure", StringComparison.Ordinal) => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", $"{Name}.cwd() expects no arguments.", span);
                        }

                        context.CheckExecution(span);
                        return OwnPathResult(PathOps.Absolute(PyStringOps.DotLiteral, false, context, span),PyString.Empty,context.MemoryGovernor,span);
                    }, $"{Name}.cwd", []),
                    "home" when _isSupported && !ShortName.StartsWith("Pure", StringComparison.Ordinal) => BoundCallable.Create((arguments, span, _) =>
                    {
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", $"{Name}.home() expects no arguments.", span);
                        }

                        throw new LythonRuntimeException("NotImplementedError", $"{Name}.home() is not supported by Lython; the host does not expose an ambient user home directory.", span);
                    }, $"{Name}.home", []),
                    "__name__" => _nameValue,
                    "__new__" => GetNewSlot(),
                    _ => MissingMemberValue.Instance
                };

                return !ReferenceEquals(value, MissingMemberValue.Instance);
            }
            public PyString RenderPython(PyRenderingContext context)
            {
                _ = context;
                return PyString.FromString($"<class 'pathlib.{ShortName}'>");
            }

            public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        }
    }
}

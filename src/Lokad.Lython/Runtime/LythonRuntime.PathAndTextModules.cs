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
    private sealed class PathlibModule : PyModule
    {
        public static readonly PathlibModule Instance = new();
        private static readonly PathlibPathType PathType = new("Path", isSupported: true);
        private static readonly PathlibPathType PurePathType = new("PurePath", isSupported: true);
        private static readonly PathlibPathType PurePosixPathType = new("PurePosixPath", isSupported: true);
        private static readonly PathlibPathType PosixPathType = new("PosixPath", isSupported: true);
        private static readonly PathlibPathType PureWindowsPathType = new("PureWindowsPath", isSupported: false);
        private static readonly PathlibPathType WindowsPathType = new("WindowsPath", isSupported: false);

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

        private static object CreatePath(IReadOnlyList<object> arguments, LythonSourceSpan span)
        {
            if (arguments.Count == 0)
            {
                return new PyPath(PyStringOps.DotLiteral);
            }

            PyString? path = null;
            foreach (var argument in arguments)
            {
                PyString segment;
                if (argument is PyPath pyPath)
                {
                    segment = pyPath.Value;
                }
                else if (!PyStringOps.TryAsString(argument, out segment))
                {
                    throw new LythonRuntimeException("TypeError", "pathlib.Path([path][, ...]) expects string or Path arguments.", span);
                }

                path = path is null ? segment : PathOps.Join(path, segment);
            }

            return new PyPath(PathOps.NormalizeLexical(path.RequireNotNull()));
        }

        private sealed class PathlibPathType : ICallable, IPyDynamicAttributes, IPyRenderableValue, INamedRuntimeCallable
        {
            private readonly bool _isSupported;

            public PathlibPathType(string shortName, bool isSupported)
            {
                ShortName = shortName;
                _isSupported = isSupported;
            }

            public string ShortName { get; }

            public string Name => $"pathlib.{ShortName}";

            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecutionBudget(span);
                if (!_isSupported)
                {
                    throw UnsupportedWindowsPath(span);
                }

                var values = new object[arguments.Length];
                for (var i = 0; i < arguments.Length; i++)
                {
                    if (arguments[i].IsKeyword)
                    {
                        throw CallErrors.NoKeywordArguments(PythonCallableKind.Builtin, Name, span);
                    }

                    values[i] = arguments[i].Value;
                }

                return CreatePath(values, span);
            }

            public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch
                {
                    "cwd" when _isSupported => new BoundCallable((arguments, span, context) =>
                    {
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", $"{Name}.cwd() expects no arguments.", span);
                        }

                        context.RegisterHostCall(span);
                        return new PyPath(PyString.FromString(PathOps.Normalize(context.Host.Cwd)));
                    }, $"{Name}.cwd", []),
                    "home" when _isSupported => new BoundCallable((arguments, span, _) =>
                    {
                        if (arguments.Length != 0)
                        {
                            throw new LythonRuntimeException("TypeError", $"{Name}.home() expects no arguments.", span);
                        }

                        throw new LythonRuntimeException("NotImplementedError", $"{Name}.home() is not supported by Lython; the host does not expose an ambient user home directory.", span);
                    }, $"{Name}.home", []),
                    "__name__" => PyString.FromString(ShortName),
                    _ => MissingMemberValue.Instance
                };

                return !ReferenceEquals(value, MissingMemberValue.Instance);
            }

            public bool TrySetMember(string name, object value)
            {
                _ = name;
                _ = value;
                return false;
            }

            public PyString RenderPython(PyRenderingContext context)
            {
                _ = context;
                return PyString.FromString($"<class 'pathlib.{ShortName}'>");
            }

            public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

            private LythonRuntimeException UnsupportedWindowsPath(LythonSourceSpan span)
                => new(
                    "NotImplementedError",
                    $"{Name} is not supported by Lython's normalized POSIX-like path model.",
                    span);
        }
    }
}

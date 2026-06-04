using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class PkgutilModule : PyModule
    {
        public static readonly PkgutilModule Instance = new();

        private PkgutilModule() : base("pkgutil")
        {
        }

        public override bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "iter_modules" => new BuiltinCallable(LythonKnownCallableSignatures.PkgutilIterModules, IterModules),
                "walk_packages" => new BuiltinCallable(LythonKnownCallableSignatures.PkgutilWalkPackages, WalkPackages),
                "find_loader" => new BuiltinCallable(LythonKnownCallableSignatures.PkgutilFindLoader, FindLoader),
                "get_loader" => new BuiltinCallable(LythonKnownCallableSignatures.PkgutilGetLoader, GetLoader),
                _ => null!,
            };

            return value is not null;
        }

        private static object IterModules(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var options = ParseDiscoveryArguments(arguments, "pkgutil.iter_modules", span, allowOnError: false);
            return options.Path is null || options.Path is PyNone
                ? CreateModuleInfoList(options.Prefix, context, span)
                : new PyList([], context.MemoryGovernor, span);
        }

        private static object WalkPackages(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var options = ParseDiscoveryArguments(arguments, "pkgutil.walk_packages", span, allowOnError: true);
            return options.Path is null || options.Path is PyNone
                ? CreateModuleInfoList(options.Prefix, context, span)
                : new PyList([], context.MemoryGovernor, span);
        }

        private static object FindLoader(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "pkgutil.find_loader(fullname) expects one argument.", span);
            }

            var fullname = RequireModuleName(arguments[0], "pkgutil.find_loader(fullname)", span);
            return TryCreateLoader(fullname, context, out var loader)
                ? loader
                : PyNone.Instance;
        }

        private static object GetLoader(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "pkgutil.get_loader(module_or_name) expects one argument.", span);
            }

            var fullname = arguments[0] is PyModule module
                ? module.Name
                : RequireModuleName(arguments[0], "pkgutil.get_loader(module_or_name)", span);

            return TryCreateLoader(fullname, context, out var loader)
                ? loader
                : PyNone.Instance;
        }

        private static DiscoveryOptions ParseDiscoveryArguments(
            object[] arguments,
            string owner,
            LythonSourceSpan span,
            bool allowOnError)
        {
            var maximumArguments = allowOnError ? 3 : 2;
            if (arguments.Length > maximumArguments)
            {
                throw new LythonRuntimeException(
                    "TypeError",
                    allowOnError
                        ? $"{owner}([path][, prefix][, onerror]) expects zero to three arguments."
                        : $"{owner}([path][, prefix]) expects zero to two arguments.",
                    span);
            }

            var prefix = PyString.Empty;
            if (arguments.Length >= 2 && arguments[1] is not PyNone)
            {
                if (!PyStringOps.TryAsString(arguments[1], out prefix))
                {
                    throw new LythonRuntimeException("TypeError", $"{owner}(..., prefix=...) expects a string.", span);
                }
            }

            return new DiscoveryOptions(arguments.Length == 0 ? null : arguments[0], prefix);
        }

        private static PyList CreateModuleInfoList(PyString prefix, ExecutionContext context, LythonSourceSpan span)
            => new(EnumerateDiscoverableModuleInfos(prefix, context), context.MemoryGovernor, span);

        private static IEnumerable<object> EnumerateDiscoverableModuleInfos(PyString prefix, ExecutionContext context)
        {
            var names = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var name in EnumerateDiscoverableBuiltinModuleNames(context))
            {
                names.Add(name);
            }

            foreach (var name in EnumerateDiscoverableLocalModuleNames(context))
            {
                names.Add(name);
            }

            var prefixText = prefix.AsString();
            foreach (var name in names)
            {
                yield return new PkgutilModuleInfoObject(
                    PyNone.Instance,
                    PyString.FromString(prefixText + name),
                    isPackage: false);
            }
        }

        private static IEnumerable<string> EnumerateDiscoverableLocalModuleNames(ExecutionContext context)
        {
            if (context.State.DisableLocalModuleImports ||
                context.State.AllowedLocalModules is null)
            {
                yield break;
            }

            var names = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var entry in context.State.AllowedLocalModules)
            {
                var name = TryGetLocalModuleName(entry);
                if (name is null)
                {
                    continue;
                }

                var modulePath = ResolveLocalModulePath(name, context);
                var allowlistPath = ResolveLocalModuleAllowlistPath(modulePath, context);
                if (IsLocalModuleImportAllowed(name, modulePath, allowlistPath, context))
                {
                    names.Add(name);
                }
            }

            foreach (var name in names)
            {
                yield return name;
            }
        }

        private static string? TryGetLocalModuleName(string allowlistEntry)
        {
            if (string.IsNullOrWhiteSpace(allowlistEntry))
            {
                return null;
            }

            var normalized = allowlistEntry.Replace('\\', '/').TrimEnd('/');
            if (normalized.Length == 0)
            {
                return null;
            }

            var basename = PathOps.BaseName(normalized);
            var name = basename.EndsWith(".py", StringComparison.Ordinal)
                ? basename[..^3]
                : basename;

            return IsSimpleModuleName(name) ? name : null;
        }

        private static bool IsSimpleModuleName(string name)
        {
            if (name.Length == 0 || name.Contains('.') || name.Contains('/'))
            {
                return false;
            }

            if (!IsIdentifierStart(name[0]))
            {
                return false;
            }

            for (var i = 1; i < name.Length; i++)
            {
                if (!IsIdentifierPart(name[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsIdentifierStart(char ch)
            => ch == '_' || (ch >= 'A' && ch <= 'Z') || (ch >= 'a' && ch <= 'z');

        private static bool IsIdentifierPart(char ch)
            => IsIdentifierStart(ch) || (ch >= '0' && ch <= '9');

        private static string RequireModuleName(object value, string owner, LythonSourceSpan span)
        {
            if (!PyStringOps.TryAsString(value, out var text))
            {
                throw new LythonRuntimeException("TypeError", $"{owner} expects a module name string.", span);
            }

            return text.AsString();
        }

        private static bool TryCreateLoader(string fullname, ExecutionContext context, out object loader)
        {
            if (IsDiscoverableBuiltinModuleName(fullname, context) ||
                EnumerateDiscoverableLocalModuleNames(context).Contains(fullname, StringComparer.Ordinal))
            {
                loader = new PkgutilLoaderObject(PyString.FromString(fullname));
                return true;
            }

            loader = PyNone.Instance;
            return false;
        }

        private readonly record struct DiscoveryOptions(object? Path, PyString Prefix);
    }

    internal sealed class PkgutilModuleInfoObject : IPyIterableValue, IPyRenderableValue, IPyDynamicAttributes
    {
        public PkgutilModuleInfoObject(object moduleFinder, PyString name, bool isPackage)
        {
            ModuleFinder = moduleFinder;
            Name = name;
            IsPackage = isPackage;
        }

        public object ModuleFinder { get; }

        public PyString Name { get; }

        public bool IsPackage { get; }

        public IEnumerable<object> Iterate()
        {
            yield return ModuleFinder;
            yield return Name;
            yield return IsPackage;
        }

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "module_finder" => ModuleFinder,
                "name" => Name,
                "ispkg" => IsPackage,
                _ => null!,
            };

            return value is not null;
        }

        public bool TrySetMember(string name, object value)
        {
            _ = name;
            _ = value;
            return false;
        }

        public PyString RenderPython(PyRenderingContext context)
            => PyString.FromString($"ModuleInfo(module_finder=None, name={PyRendering.ToReprPyString(Name, context).AsString()}, ispkg={(IsPackage ? "True" : "False")})");

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    internal sealed class PkgutilLoaderObject : IPyRenderableValue, IPyDynamicAttributes
    {
        public PkgutilLoaderObject(PyString name)
        {
            Name = name;
        }

        public PyString Name { get; }

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "name" => Name,
                "fullname" => Name,
                "ispkg" => false,
                _ => null!,
            };

            return value is not null;
        }

        public bool TrySetMember(string name, object value)
        {
            _ = name;
            _ = value;
            return false;
        }

        public PyString RenderPython(PyRenderingContext context)
            => PyString.FromString($"<lython loader for {PyRendering.ToReprPyString(Name, context).AsString()}>");

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }
}

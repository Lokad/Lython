using System.Numerics;
using System.Collections.Frozen;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class PkgutilModule : PyModule
    {
        public static readonly PkgutilModule Instance = new();

        private static readonly DiscoveredModule[] BuiltinDescriptors =
            CreateBuiltinDescriptors(GetKnownBuiltinModuleNames());
        private static readonly DiscoveredModule[] CapabilityFreeBuiltinDescriptors =
            CreateBuiltinDescriptors(GetKnownBuiltinModuleNames()
                .Where(static name => !string.Equals(name, "subprocess", StringComparison.Ordinal))
                .ToArray());
        private static readonly FrozenDictionary<string, DiscoveredModule> BuiltinDescriptorsByName =
            BuiltinDescriptors.ToFrozenDictionary(static descriptor => descriptor.Name, StringComparer.Ordinal);
        private static readonly FrozenDictionary<string, DiscoveredModule> CapabilityFreeBuiltinDescriptorsByName =
            CapabilityFreeBuiltinDescriptors.ToFrozenDictionary(static descriptor => descriptor.Name, StringComparer.Ordinal);

        private PkgutilModule() : base("pkgutil")
        {
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "ModuleInfo" => BuiltinCallable.Create(LythonKnownCallableSignatures.PkgutilModuleInfo, ModuleInfo),
                "iter_modules" => BuiltinCallable.Create(LythonKnownCallableSignatures.PkgutilIterModules, IterModules),
                "walk_packages" => BuiltinCallable.Create(LythonKnownCallableSignatures.PkgutilWalkPackages, WalkPackages),
                "find_loader" => BuiltinCallable.Create(LythonKnownCallableSignatures.PkgutilFindLoader, FindLoader),
                "get_loader" => BuiltinCallable.Create(LythonKnownCallableSignatures.PkgutilGetLoader, GetLoader),
                "extend_path" => BuiltinCallable.Create(LythonKnownCallableSignatures.PkgutilExtendPath, ExtendPath),
                "resolve_name" => BuiltinCallable.Create(LythonKnownCallableSignatures.PkgutilResolveName, ResolveName),
                "get_importer" => BuiltinCallable.CreateUnsupported("pkgutil.get_importer"),
                "iter_importers" => BuiltinCallable.CreateUnsupported("pkgutil.iter_importers"),
                "iter_importer_modules" => BuiltinCallable.CreateUnsupported("pkgutil.iter_importer_modules"),
                "iter_zipimport_modules" => BuiltinCallable.CreateUnsupported("pkgutil.iter_zipimport_modules"),
                "get_data" => BuiltinCallable.CreateUnsupported("pkgutil.get_data"),
                "read_code" => BuiltinCallable.CreateUnsupported("pkgutil.read_code"),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static object ModuleInfo(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 3 ||
                !PyStringOps.TryAsString(arguments[1], out var name))
            {
                throw new LythonRuntimeException("TypeError", "pkgutil.ModuleInfo(module_finder, name, ispkg) expects finder, string name, and bool ispkg.", span);
            }

            return new PkgutilModuleInfoObject(arguments[0], name, IsTruthy(arguments[2]));
        }

        private static object IterModules(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var options = ParseDiscoveryArguments(arguments, "pkgutil.iter_modules", span, allowOnError: false);
            var modules = options.Path is null || options.Path is PyNone
                ? EnumerateDefaultModuleInfos(options.Prefix, recursive: false, onerror: null, context, span)
                : EnumerateExplicitPathModuleInfos(ParsePathEntries(options.Path, "pkgutil.iter_modules(..., path=...)", context, span), options.Prefix.AsString(), recursive: false, onerror: null, context, span);
            return new PyList(modules, context.MemoryGovernor, span);
        }

        private static object WalkPackages(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var options = ParseDiscoveryArguments(arguments, "pkgutil.walk_packages", span, allowOnError: true);
            var modules = options.Path is null || options.Path is PyNone
                ? EnumerateDefaultModuleInfos(options.Prefix, recursive: true, options.OnError, context, span)
                : EnumerateExplicitPathModuleInfos(ParsePathEntries(options.Path, "pkgutil.walk_packages(..., path=...)", context, span), options.Prefix.AsString(), recursive: true, options.OnError, context, span);
            return new PyList(modules, context.MemoryGovernor, span);
        }

        private static object FindLoader(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "pkgutil.find_loader(fullname) expects one argument.", span);
            }

            var fullname = RequireModuleName(arguments[0], "pkgutil.find_loader(fullname)", span);
            return TryCreateLoader(fullname, context, span, out var loader)
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

            return TryCreateLoader(fullname, context, span, out var loader)
                ? loader
                : PyNone.Instance;
        }

        private static object ExtendPath(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 2 || !PyStringOps.TryAsString(arguments[1], out _))
            {
                throw new LythonRuntimeException("TypeError", "pkgutil.extend_path(path, name) expects path and package name.", span);
            }

            if (arguments[0] is PyNone)
            {
                return new PyList([], context.MemoryGovernor, span);
            }

            if (PyStringOps.TryAsString(arguments[0], out _) || arguments[0] is PyBytes)
            {
                return arguments[0];
            }

            var values = new List<object>();
            try
            {
                foreach (var item in PyIteration.ToSequence(arguments[0], span))
                {
                    if (!TryAsPathText(item, out var path))
                    {
                        throw new LythonRuntimeException("TypeError", "pkgutil.extend_path(path, name) expects path entries to be strings or Path-like objects.", span);
                    }

                    values.Add(path);
                }
            }
            catch (LythonRuntimeException ex) when (ex.ExceptionType == "TypeError" && ex.Message == "Object is not iterable.")
            {
                return arguments[0];
            }

            return new PyList(values, context.MemoryGovernor, span);
        }

        private static object ResolveName(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "pkgutil.resolve_name(name) expects one argument.", span);
            }

            var name = RequireModuleName(arguments[0], "pkgutil.resolve_name(name)", span);
            if (name.Length == 0)
            {
                throw new LythonRuntimeException("ValueError", "pkgutil.resolve_name(name) expects a non-empty name.", span);
            }

            if (name.Contains(':', StringComparison.Ordinal))
            {
                var parts = name.Split(':', 2);
                if (parts[0].Length == 0 || parts[1].Length == 0)
                {
                    throw new LythonRuntimeException("ValueError", "pkgutil.resolve_name(name) expects 'module:object' or dotted names.", span);
                }

                var module = ResolveImportedModule(parts[0], context, span);
                return ResolveObjectPath(module, parts[1], span);
            }

            var segments = name.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0 || string.Join(".", segments) != name)
            {
                throw new LythonRuntimeException("ValueError", "pkgutil.resolve_name(name) expects a valid dotted name.", span);
            }

            for (var length = segments.Length; length >= 1; length--)
            {
                var moduleName = string.Join(".", segments.Take(length));
                if (!TryCanImportModule(moduleName, context, span))
                {
                    continue;
                }

                var module = ResolveImportedModule(moduleName, context, span);
                if (length == segments.Length)
                {
                    return module;
                }

                return ResolveObjectPath(module, string.Join(".", segments.Skip(length)), span);
            }

            throw RuntimeErrors.NoModuleNamed(segments[0], span);
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

            object? onerror = null;
            if (allowOnError && arguments.Length >= 3 && arguments[2] is not PyNone)
            {
                if (arguments[2] is not ICallable)
                {
                    throw new LythonRuntimeException("TypeError", $"{owner}(..., onerror=...) expects a callable or None.", span);
                }

                onerror = arguments[2];
            }

            return new DiscoveryOptions(arguments.Length == 0 ? null : arguments[0], prefix, onerror);
        }

        private static IEnumerable<object> EnumerateDefaultModuleInfos(
            PyString prefix,
            bool recursive,
            object? onerror,
            ExecutionContext context,
            LythonSourceSpan span)
        {
            _ = onerror;
            _ = span;
            var prefixText = prefix.AsString();
            var descriptors = CreateBuiltinDescriptors(context);
            string? activePackagePrefix = null;
            foreach (var descriptor in descriptors)
            {
                if (!descriptor.Name.Contains('.'))
                {
                    yield return descriptor.ToModuleInfo(prefixText + descriptor.Name);
                    activePackagePrefix = recursive && descriptor.IsPackage ? descriptor.Name + "." : null;
                    continue;
                }

                if (activePackagePrefix is not null && descriptor.Name.StartsWith(activePackagePrefix, StringComparison.Ordinal))
                {
                    yield return descriptor.ToModuleInfo(prefixText + descriptor.Name);
                }
            }

            foreach (var descriptor in EnumerateDiscoverableLocalModuleDescriptors(context, span))
            {
                yield return descriptor.ToModuleInfo(prefixText + descriptor.Name);
            }
        }

        private static IEnumerable<object> EnumerateExplicitPathModuleInfos(
            IReadOnlyList<string> paths,
            string prefix,
            bool recursive,
            object? onerror,
            ExecutionContext context,
            LythonSourceSpan span)
        {
            var yielded = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in paths)
            {
                foreach (var descriptor in DiscoverPathModules(path, prefix, recursive, onerror, context, span))
                {
                    if (yielded.Add(descriptor.Name))
                    {
                        yield return descriptor.ToModuleInfo(descriptor.Name);
                    }
                }
            }
        }

        private static IEnumerable<DiscoveredModule> DiscoverPathModules(string directory, string prefix, bool recursive, object? onerror, ExecutionContext context, LythonSourceSpan span)
            => DiscoverPathModules(directory, prefix, recursive, onerror, context, span, null);

        private static IEnumerable<DiscoveredModule> DiscoverPathModules(
            string directory,
            string prefix,
            bool recursive,
            object? onerror,
            ExecutionContext context,
            LythonSourceSpan span,
            string? errorName)
        {
            IReadOnlyList<string> entries;
            try
            {
                entries = context.HostListDir(directory, span);
            }
            catch (LythonRuntimeException) when (onerror is not null)
            {
                CallOnError(onerror, PyString.FromString(errorName ?? directory), span, context);
                yield break;
            }
            catch (LythonRuntimeException)
            {
                throw;
            }

            foreach (var entry in entries.Order(StringComparer.Ordinal))
            {
                if (entry.StartsWith(".", StringComparison.Ordinal))
                {
                    continue;
                }

                var child = PathOps.Join(directory, entry);
                if (entry.EndsWith(".py", StringComparison.Ordinal))
                {
                    var stem = entry[..^3];
                    if (stem == "__init__" || !IsSimpleModuleName(stem))
                    {
                        continue;
                    }

                    var moduleName = prefix + stem;
                    if (IsDiscoveredLocalModuleAllowed(moduleName, child, context))
                    {
                        yield return new DiscoveredModule(moduleName, IsPackage: false, child);
                    }

                    continue;
                }

                if (!IsSimpleModuleName(entry))
                {
                    continue;
                }

                var initPath = PathOps.Join(child, "__init__.py");
                if (!context.HostExists(initPath, span))
                {
                    continue;
                }

                var packageName = prefix + entry;
                if (!IsDiscoveredLocalModuleAllowed(packageName, initPath, context))
                {
                    continue;
                }

                yield return new DiscoveredModule(packageName, IsPackage: true, initPath);
                if (!recursive)
                {
                    continue;
                }

                var childPrefix = packageName + ".";
                foreach (var nested in DiscoverPathModules(child, childPrefix, recursive: true, onerror, context, span, packageName))
                {
                    yield return nested;
                }
            }
        }

        private static IReadOnlyList<string> ParsePathEntries(object path, string owner, ExecutionContext context, LythonSourceSpan span)
        {
            if (TryAsPathText(path, out var singlePath))
            {
                return [NormalizeDiscoveryPath(singlePath.AsString(), context)];
            }

            if (path is PyBytes)
            {
                throw new LythonRuntimeException("TypeError", $"{owner} expects a path string or iterable of path strings.", span);
            }

            var paths = new List<string>();
            try
            {
                foreach (var item in PyIteration.ToSequence(path, span))
                {
                    if (!TryAsPathText(item, out var itemPath))
                    {
                        throw new LythonRuntimeException("TypeError", $"{owner} expects a path string or iterable of path strings.", span);
                    }

                    paths.Add(NormalizeDiscoveryPath(itemPath.AsString(), context));
                }
            }
            catch (LythonRuntimeException ex) when (ex.ExceptionType == "TypeError" && ex.Message == "Object is not iterable.")
            {
                throw new LythonRuntimeException("TypeError", $"{owner} expects a path string or iterable of path strings.", span);
            }

            return paths;
        }

        private static string NormalizeDiscoveryPath(string path, ExecutionContext context)
            => PathOps.Normalize(path, context.Host.Cwd);

        private static bool TryAsPathText(object value, out PyString path)
        {
            if (PyStringOps.TryAsString(value, out path))
            {
                return true;
            }

            if (value is PyPath pyPath)
            {
                path = pyPath.Value;
                return true;
            }

            path = PyString.Empty;
            return false;
        }

        private static IEnumerable<DiscoveredModule> EnumerateDiscoverableLocalModuleDescriptors(ExecutionContext context, LythonSourceSpan span)
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
                if (name is null || !names.Add(name))
                {
                    continue;
                }

                if (TryGetLocalModulePath(name, context, span, out var path, out var isPackage))
                {
                    yield return new DiscoveredModule(name, isPackage, path);
                }
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

            if (normalized.EndsWith("/__init__.py", StringComparison.Ordinal))
            {
                normalized = normalized[..^"/__init__.py".Length];
            }
            else if (normalized.EndsWith(".py", StringComparison.Ordinal))
            {
                normalized = normalized[..^3];
            }

            var slash = normalized.LastIndexOf('/');
            var name = slash >= 0 ? normalized[(slash + 1)..] : normalized;
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

        private static bool TryCreateLoader(string fullname, ExecutionContext context, LythonSourceSpan span, out object loader)
        {
            var builtins = GetBuiltinDescriptorLookup(context);
            if (builtins.TryGetValue(fullname, out var builtin))
            {
                loader = new PkgutilLoaderObject(PyString.FromString(fullname), builtin.IsPackage, sourcePath: null);
                return true;
            }

            if (TryGetLocalModulePath(fullname, context, span, out var path, out var isPackage))
            {
                loader = new PkgutilLoaderObject(PyString.FromString(fullname), isPackage, path);
                return true;
            }

            loader = PyNone.Instance;
            return false;
        }

        private static bool TryCanImportModule(string moduleName, ExecutionContext context, LythonSourceSpan span)
        {
            if (IsDiscoverableBuiltinModuleName(moduleName, context))
            {
                return true;
            }

            return TryGetLocalModulePath(moduleName, context, span, out _, out _);
        }

        private static bool TryGetLocalModulePath(
            string moduleName,
            ExecutionContext context,
            LythonSourceSpan span,
            out string path,
            out bool isPackage)
        {
            return TryResolveLocalImportPath(moduleName, context, span, out path, out isPackage);
        }

        private static bool IsDiscoveredLocalModuleAllowed(string moduleName, string path, ExecutionContext context)
        {
            var importName = moduleName;
            if (importName.Contains('.'))
            {
                importName = importName[(importName.LastIndexOf('.') + 1)..];
            }

            var allowlistPath = ResolveLocalModuleAllowlistPath(path, context);
            return IsLocalModuleImportAllowed(importName, path, allowlistPath, context) ||
                IsLocalModuleImportAllowed(moduleName, path, allowlistPath, context);
        }

        private static IReadOnlyList<DiscoveredModule> CreateBuiltinDescriptors(ExecutionContext context)
            => context.Host.SubprocessRunner is null ? CapabilityFreeBuiltinDescriptors : BuiltinDescriptors;

        private static IReadOnlyDictionary<string, DiscoveredModule> GetBuiltinDescriptorLookup(ExecutionContext context)
            => context.Host.SubprocessRunner is null ? CapabilityFreeBuiltinDescriptorsByName : BuiltinDescriptorsByName;

        private static DiscoveredModule[] CreateBuiltinDescriptors(IReadOnlyList<string> names)
        {
            var descriptors = new DiscoveredModule[names.Count];
            for (var index = 0; index < names.Count; index++)
            {
                var name = names[index];
                descriptors[index] = new DiscoveredModule(
                    name,
                    IsPackage: index + 1 < names.Count && names[index + 1].StartsWith(name + ".", StringComparison.Ordinal),
                    SourcePath: null);
            }

            return descriptors;
        }

        private static object ResolveObjectPath(object current, string objectPath, LythonSourceSpan span)
        {
            foreach (var member in objectPath.Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!PyMemberAccess.TryResolve(current, member, out var next))
                {
                    throw new LythonRuntimeException("AttributeError", $"Object has no attribute '{member}'.", span);
                }

                current = next;
            }

            return current;
        }

        private static void CallOnError(object onerror, PyString name, LythonSourceSpan span, ExecutionContext context)
        {
            if (onerror is not ICallable callable)
            {
                return;
            }

            CallableInvocation.InvokeUnary(callable, name, span, context);
        }

        private readonly record struct DiscoveryOptions(object? Path, PyString Prefix, object? OnError);

        private readonly record struct DiscoveredModule(string Name, bool IsPackage, string? SourcePath)
        {
            public object ToModuleInfo(string visibleName)
                => new PkgutilModuleInfoObject(
                    PyNone.Instance,
                    PyString.FromString(visibleName),
                    IsPackage);
        }
    }

}

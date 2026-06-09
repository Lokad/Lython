using System.Numerics;
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
                "ModuleInfo" => new BuiltinCallable(LythonKnownCallableSignatures.PkgutilModuleInfo, ModuleInfo),
                "iter_modules" => new BuiltinCallable(LythonKnownCallableSignatures.PkgutilIterModules, IterModules),
                "walk_packages" => new BuiltinCallable(LythonKnownCallableSignatures.PkgutilWalkPackages, WalkPackages),
                "find_loader" => new BuiltinCallable(LythonKnownCallableSignatures.PkgutilFindLoader, FindLoader),
                "get_loader" => new BuiltinCallable(LythonKnownCallableSignatures.PkgutilGetLoader, GetLoader),
                "extend_path" => new BuiltinCallable(LythonKnownCallableSignatures.PkgutilExtendPath, ExtendPath),
                "resolve_name" => new BuiltinCallable(LythonKnownCallableSignatures.PkgutilResolveName, ResolveName),
                "get_importer" => UnsupportedPkgutilCallable("pkgutil.get_importer"),
                "iter_importers" => UnsupportedPkgutilCallable("pkgutil.iter_importers"),
                "iter_importer_modules" => UnsupportedPkgutilCallable("pkgutil.iter_importer_modules"),
                "iter_zipimport_modules" => UnsupportedPkgutilCallable("pkgutil.iter_zipimport_modules"),
                "get_data" => UnsupportedPkgutilCallable("pkgutil.get_data"),
                "read_code" => UnsupportedPkgutilCallable("pkgutil.read_code"),
                _ => null!,
            };

            return value is not null;
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

        private static BuiltinCallable UnsupportedPkgutilCallable(string qualifiedName)
            => new(
                qualifiedName,
                (arguments, span, context) =>
                {
                    _ = arguments;
                    _ = context;
                    throw new LythonRuntimeException("NotImplementedError", qualifiedName + " is unsupported by Lython.", span);
                });

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
            var topLevel = descriptors
                .Where(static descriptor => !descriptor.Name.Contains('.'))
                .OrderBy(static descriptor => descriptor.Name, StringComparer.Ordinal);

            foreach (var descriptor in topLevel)
            {
                yield return descriptor.ToModuleInfo(prefixText + descriptor.Name);
                if (!recursive || !descriptor.IsPackage)
                {
                    continue;
                }

                foreach (var child in descriptors
                    .Where(child => child.Name.StartsWith(descriptor.Name + ".", StringComparison.Ordinal))
                    .OrderBy(static child => child.Name, StringComparer.Ordinal))
                {
                    yield return child.ToModuleInfo(prefixText + child.Name);
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

        private static IEnumerable<DiscoveredModule> DiscoverPathModules(
            string directory,
            string prefix,
            bool recursive,
            object? onerror,
            ExecutionContext context,
            LythonSourceSpan span,
            string? errorName = null)
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
            catch (Exception ex)
            {
                if (onerror is not null)
                {
                    CallOnError(onerror, PyString.FromString(errorName ?? directory), span, context);
                    yield break;
                }

                throw new LythonRuntimeException("RuntimeError", "pkgutil explicit path discovery failed: " + ex.Message, span);
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
            var builtins = CreateBuiltinDescriptors(context);
            var builtin = builtins.FirstOrDefault(descriptor => string.Equals(descriptor.Name, fullname, StringComparison.Ordinal));
            if (builtin.Name is not null)
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
        {
            var names = EnumerateDiscoverableBuiltinModuleNames(context)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            var nameSet = names.ToHashSet(StringComparer.Ordinal);
            var descriptors = new List<DiscoveredModule>();
            foreach (var name in names)
            {
                descriptors.Add(new DiscoveredModule(
                    name,
                    IsPackage: nameSet.Any(candidate => candidate.StartsWith(name + ".", StringComparison.Ordinal)),
                    SourcePath: null));
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

            callable.Invoke([new CallArgumentValue(null, name)], span, context);
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

    internal sealed class PkgutilModuleInfoObject :
        IPySequenceValue,
        IPyIndexableValue,
        IPyIterableValue,
        IPyRenderableValue,
        IPyDynamicAttributes,
        IPyHashableValue,
        IEquatable<PkgutilModuleInfoObject>
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

        public int Count => 3;

        public int Length => 3;

        public object this[int index] => GetItem(index);

        public object GetItem(int index)
            => index switch
            {
                0 => ModuleFinder,
                1 => Name,
                2 => IsPackage,
                _ => throw new ArgumentOutOfRangeException(nameof(index))
            };

        public object CreateSlice(IEnumerable<object> items) => new PyTuple(items);

        public object GetIndex(int index) => GetItem(index);

        public object GetSlice(IEnumerable<int> indices)
            => new PyTuple(indices.Select(GetItem));

        public IEnumerator<object> GetEnumerator()
        {
            yield return ModuleFinder;
            yield return Name;
            yield return IsPackage;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public IEnumerable<object> Iterate() => this;

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "module_finder" => ModuleFinder,
                "name" => Name,
                "ispkg" => IsPackage,
                "_fields" => new PyTuple([
                    PyString.FromString("module_finder"),
                    PyString.FromString("name"),
                    PyString.FromString("ispkg")]),
                "_asdict" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "ModuleInfo._asdict() expects no arguments.", span);
                    }

                    var dict = new PyDict(context.MemoryGovernor, span);
                    dict.SetItem(PyString.FromString("module_finder"), ModuleFinder);
                    dict.SetItem(PyString.FromString("name"), Name);
                    dict.SetItem(PyString.FromString("ispkg"), IsPackage);
                    return dict;
                }, "ModuleInfo._asdict", []),
                "_replace" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length > 3)
                    {
                        throw new LythonRuntimeException("TypeError", "ModuleInfo._replace(module_finder, name, ispkg) expects zero to three field values.", span);
                    }

                    var moduleFinder = arguments.Length >= 1 ? arguments[0] : ModuleFinder;
                    var nameValue = arguments.Length >= 2 ? arguments[1] : Name;
                    var isPackageValue = arguments.Length >= 3 ? arguments[2] : IsPackage;
                    if (!PyStringOps.TryAsString(nameValue, out var replacementName))
                    {
                        throw new LythonRuntimeException("TypeError", "ModuleInfo._replace(..., name=...) expects a string.", span);
                    }

                    return new PkgutilModuleInfoObject(moduleFinder, replacementName, IsTruthy(isPackageValue));
                }, "ModuleInfo._replace", ["module_finder", "name", "ispkg"], requiredCount: 0),
                "count" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length != 1)
                    {
                        throw new LythonRuntimeException("TypeError", "ModuleInfo.count(value) expects one argument.", span);
                    }

                    var count = 0;
                    foreach (var item in this)
                    {
                        if (PyEquality.AreEqual(item, arguments[0]))
                        {
                            count++;
                        }
                    }

                    return new BigInteger(count);
                }, "ModuleInfo.count", ["value"]),
                "index" => new BoundCallable((arguments, span, _) =>
                {
                    if (arguments.Length is < 1 or > 3)
                    {
                        throw new LythonRuntimeException("TypeError", "ModuleInfo.index(value[, start[, stop]]) expects one to three arguments.", span);
                    }

                    var start = NormalizeModuleInfoSearchBound(arguments.Length >= 2 ? arguments[1] : null, 0, span);
                    var stop = NormalizeModuleInfoSearchBound(arguments.Length >= 3 ? arguments[2] : null, Count, span);
                    for (var i = start; i < stop; i++)
                    {
                        if (PyEquality.AreEqual(GetItem(i), arguments[0]))
                        {
                            return new BigInteger(i);
                        }
                    }

                    throw new LythonRuntimeException("ValueError", "ModuleInfo.index(value): value is not in tuple", span);
                }, "ModuleInfo.index", ["value", "start", "stop"], requiredCount: 1),
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

        public bool Equals(PkgutilModuleInfoObject? other)
            => other is not null &&
                PyEquality.AreEqual(ModuleFinder, other.ModuleFinder) &&
                Name.Equals(other.Name) &&
                IsPackage == other.IsPackage;

        public override bool Equals(object? obj) => obj is PkgutilModuleInfoObject other && Equals(other);

        public override int GetHashCode() => GetPyHashCode();

        public int GetPyHashCode()
        {
            var hash = new HashCode();
            if (ModuleFinder is not PyNone)
            {
                hash.Add(PyValueComparer.Instance.GetHashCode(ModuleFinder));
            }

            hash.Add(Name.GetPyHashCode());
            hash.Add(IsPackage);
            return hash.ToHashCode();
        }

        public PyString RenderPython(PyRenderingContext context)
            => PyString.FromString($"ModuleInfo(module_finder={PyRendering.ToReprPyString(ModuleFinder, context).AsString()}, name={PyRendering.ToReprPyString(Name, context).AsString()}, ispkg={(IsPackage ? "True" : "False")})");

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        private static int NormalizeModuleInfoSearchBound(object? value, int defaultValue, LythonSourceSpan span)
        {
            if (value is null)
            {
                return defaultValue;
            }

            if (value is not BigInteger integer)
            {
                throw new LythonRuntimeException("TypeError", "ModuleInfo.index(value[, start[, stop]]) expects integer start/stop bounds.", span);
            }

            if (integer < int.MinValue)
            {
                return 0;
            }

            if (integer > int.MaxValue)
            {
                return 3;
            }

            var index = (int)integer;
            if (index < 0)
            {
                index += 3;
            }

            return Math.Clamp(index, 0, 3);
        }
    }

    internal sealed class PkgutilLoaderObject : IPyRenderableValue, IPyDynamicAttributes
    {
        public PkgutilLoaderObject(PyString name, bool isPackage, string? sourcePath)
        {
            Name = name;
            IsPackage = isPackage;
            SourcePath = sourcePath;
        }

        public PyString Name { get; }

        public bool IsPackage { get; }

        public string? SourcePath { get; }

        public bool TryGetMember(string name, out object value)
        {
            value = name switch
            {
                "name" => Name,
                "fullname" => Name,
                "ispkg" => IsPackage,
                "is_package" => new BoundCallable((arguments, span, context) =>
                {
                    _ = context;
                    if (arguments.Length is > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "loader.is_package(fullname=None) expects zero or one argument.", span);
                    }

                    if (arguments.Length == 1 &&
                        arguments[0] is not PyNone &&
                        (!PyStringOps.TryAsString(arguments[0], out var fullname) || !fullname.Equals(Name)))
                    {
                        return false;
                    }

                    return IsPackage;
                }, "loader.is_package", ["fullname"], requiredCount: 0),
                "get_source" => new BoundCallable((arguments, span, context) =>
                {
                    if (arguments.Length is > 1)
                    {
                        throw new LythonRuntimeException("TypeError", "loader.get_source(fullname=None) expects zero or one argument.", span);
                    }

                    if (arguments.Length == 1 &&
                        arguments[0] is not PyNone &&
                        (!PyStringOps.TryAsString(arguments[0], out var fullname) || !fullname.Equals(Name)))
                    {
                        return PyNone.Instance;
                    }

                    return SourcePath is null
                        ? PyNone.Instance
                        : ReadGovernedHostText(SourcePath, context, span);
                }, "loader.get_source", ["fullname"], requiredCount: 0),
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

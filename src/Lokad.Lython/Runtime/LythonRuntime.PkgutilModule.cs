using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed class PkgutilModule : PyModule, IPyContextualDynamicAttributes
    {
        public static readonly PkgutilModule Instance = new();

        private PkgutilModule() : base("pkgutil") { }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "iter_modules" => BuiltinCallable.CreateWithPresence(LythonKnownCallableSignatures.PkgutilIterModules,
                    (a, s, c) => Discover(a, s, c, false), (a, s, c) => ValueTask.FromResult(Discover(a, s, c, false))),
                "walk_packages" => BuiltinCallable.CreateWithPresence(LythonKnownCallableSignatures.PkgutilWalkPackages,
                    (a, s, c) => Discover(a, s, c, true), (a, s, c) => ValueTask.FromResult(Discover(a, s, c, true))),
                "find_loader" => BuiltinCallable.Create(LythonKnownCallableSignatures.PkgutilFindLoader,
                    (a, s, c) => FindLoader(a, s, c, false).GetAwaiter().GetResult(), (a, s, c) => FindLoader(a, s, c, true)),
                "get_loader" => BuiltinCallable.Create(LythonKnownCallableSignatures.PkgutilGetLoader,
                    (a, s, c) => GetLoader(a, s, c, false).GetAwaiter().GetResult(), (a, s, c) => GetLoader(a, s, c, true)),
                "extend_path" => BuiltinCallable.Create(LythonKnownCallableSignatures.PkgutilExtendPath, ExtendPath),
                "resolve_name" => BuiltinCallable.Create(LythonKnownCallableSignatures.PkgutilResolveName,
                    (a, s, c) => ResolveName(a, s, c, false).GetAwaiter().GetResult(), (a, s, c) => ResolveName(a, s, c, true)),
                "get_importer" or "iter_importers" or "iter_importer_modules" or "iter_zipimport_modules" or "get_data" or "read_code"
                    => BuiltinCallable.CreateUnsupported("pkgutil." + name),
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        internal static void ChargePkgutilValue(MemoryGovernor governor, LythonSourceSpan? span)
        {
            governor.Reserve(64, span);
            governor.Commit(64);
        }

        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            if (name != "ModuleInfo") return TryGetCachedMember(name, out value);
            return ((PyMappingProxy)GetNamespace(context, span)).TryGetValue(PyString.FromString(name), out value);
        }

        internal static PyNamedTupleType CreateModuleInfoType(ExecutionContext context, LythonSourceSpan span)
            => new("ModuleInfo", ["module_finder", "name", "ispkg"], null, context.MemoryGovernor, span)
            { ModuleName = PyString.FromString("pkgutil") };

        internal static object MakeModuleInfo(PyString name, bool isPackage, ExecutionContext context, LythonSourceSpan span)
        {
            Instance.TryGetMember("ModuleInfo", context, span, out var type);
            return ((PyNamedTupleType)type!).CreateFromValues([PyNone.Instance, name, isPackage], context.MemoryGovernor, span);
        }

        private static object Discover(BoundCallArguments arguments, LythonSourceSpan span, ExecutionContext context, bool recursive)
        {
            using var shell = context.MemoryGovernor.ReserveTemporary(512, span);
            var result = new ModuleDiscoveryIterator(
                arguments.Assigned[0] ? arguments.Values[0] : PyNone.Instance,
                arguments.Assigned[1] ? arguments.Values[1] : PyString.Empty,
                recursive && arguments.Assigned[2] ? arguments.Values[2] : PyNone.Instance,
                recursive, GetGlobalContext(context), span);
            shell.Commit(512);
            return result;
        }

        private static async ValueTask<object> FindLoader(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var name = RequireModuleName(arguments[0], "pkgutil.find_loader(fullname)", context, span);
            return await FindLoaderByName(name, span, context, asynchronous).ConfigureAwait(false);
        }

        private static async ValueTask<object> FindLoaderByName(string name, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            if (name.StartsWith('.')) throw new LythonRuntimeException("ImportError", "relative module names are not supported", span);
            try
            {
                var spec = await TryCreateContainedModuleSpecCoreAsync(name, context, span, asynchronous).ConfigureAwait(false);
                return spec is null ? PyNone.Instance : await ReadWrapperAttributeAsync(spec, "loader", context, span, asynchronous).ConfigureAwait(false);
            }
            catch (LythonRuntimeException error) when (MatchesExceptionType(BuiltinException("ImportError"), error.Identity) ||
                error.ExceptionType is "AttributeError" or "TypeError" or "ValueError")
            {
                context.CheckExecution(span);
                throw new LythonRuntimeException("ImportError", "error while finding loader", span, error)
                { PythonCause = CreatePythonExceptionInstance(error), SuppressPythonContext = true };
            }
        }

        private static async ValueTask<object> GetLoader(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var target = arguments[0];
            if (target is PyString)
            {
                var name = RequireModuleName(target, "pkgutil.get_loader(module_or_name)", context, span);
                var cached = asynchronous
                    ? await context.State.ImportedModules.TryGetStringValueAsync(name, context, span).ConfigureAwait(false)
                    : context.State.ImportedModules.TryGetStringValue(name, context.MemoryGovernor, span, context, out var value)
                        ? (true, value) : (false, PyNone.Instance);
                if (!cached.Item1) return await FindLoaderByName(name, span, context, asynchronous).ConfigureAwait(false);
                target = cached.Item2;
            }
            if (target is PyNone) return PyNone.Instance;
            if (target is not PyModule) throw RuntimeErrors.Type("pkgutil.get_loader expects a module object or module name string", span);
            var loader = await TryReadWrapperAttributeAsync(target, "__loader__", context, span, asynchronous).ConfigureAwait(false);
            if (loader.Found && loader.Value is not PyNone) return loader.Value;
            var spec = await TryReadWrapperAttributeAsync(target, "__spec__", context, span, asynchronous).ConfigureAwait(false);
            if (!spec.Found || spec.Value is PyNone) return PyNone.Instance;
            var fullname = await ReadWrapperAttributeAsync(target, "__name__", context, span, asynchronous).ConfigureAwait(false);
            return await FindLoaderByName(RequireModuleName(fullname, "pkgutil.get_loader", context, span), span, context, asynchronous).ConfigureAwait(false);
        }

        private static object ExtendPath(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments[0] is not PyList list) return arguments[0];
            if (arguments[1] is not PyString) throw RuntimeErrors.Type("extend_path expects a string package name", span);
            // Contained discovery does not add ambient namespace-package paths.
            return new PyList(list, context.MemoryGovernor, span);
        }

        private static async ValueTask<object> ResolveName(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            if (arguments[0] is not PyString text) throw RuntimeErrors.Type("resolve_name expects a string", span);
            using var working = context.MemoryGovernor.ReserveTemporary(checked(256L + 64L * text.Utf8Bytes.Length), span);
            var name = text.AsString();
            var colon = name.IndexOf(':');
            var modulePart = colon < 0 ? name : name[..colon];
            var objectPart = colon < 0 ? null : name[(colon + 1)..];
            var segments = modulePart.Split('.');
            bool IsValidPart(string part) => PyStringOps.IsIdentifier(PyString.FromString(part), context, span);
            if (segments.Any(part => !IsValidPart(part)) ||
                objectPart is { Length: > 0 } && objectPart.Split('.').Any(part => !IsValidPart(part)))
                throw new LythonRuntimeException("ValueError", "invalid format for pkgutil.resolve_name", span);
            object current;
            var next = 1;
            if (colon >= 0)
            {
                current = asynchronous ? await ResolveImportedModuleHierarchyAsync(modulePart, context, span).ConfigureAwait(false)
                    : ResolveImportedModuleHierarchy(modulePart, context, span);
                segments = objectPart!.Length == 0 ? [] : objectPart.Split('.');
                next = 0;
            }
            else
            {
                var imported = segments[0];
                current = asynchronous ? await ResolveImportedModuleAsync(imported, context, span).ConfigureAwait(false)
                    : ResolveImportedModule(imported, context, span);
                for (; next < segments.Length; next++)
                {
                    context.CheckExecution(span);
                    var candidate = imported + "." + segments[next];
                    try
                    {
                        current = asynchronous ? await ResolveImportedModuleAsync(candidate, context, span).ConfigureAwait(false)
                            : ResolveImportedModule(candidate, context, span);
                        imported = candidate;
                    }
                    catch (LythonRuntimeException error) when (MatchesExceptionType(BuiltinException("ImportError"), error.Identity))
                    {
                        context.CheckExecution(span);
                        break;
                    }
                }
            }
            for (; next < segments.Length; next++)
                current = await ReadWrapperAttributeAsync(current, segments[next], context, span, asynchronous).ConfigureAwait(false);
            return current;
        }

        internal static string? TryGetLocalModuleName(string allowlistEntry)
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

        internal static bool IsSimpleModuleName(string name)
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

        private static string RequireModuleName(object value, string owner, ExecutionContext context, LythonSourceSpan span)
        {
            if (!PyStringOps.TryAsString(value, out var text))
            {
                throw new LythonRuntimeException("TypeError", $"{owner} expects a module name string.", span);
            }

            var bytes = checked(64L + 4L * text.Utf8Bytes.Length);
            context.MemoryGovernor.Reserve(bytes, span);
            context.MemoryGovernor.Commit(bytes); // escaped native import keys belong to this execution
            return text.AsString();
        }

        internal static bool IsDiscoveredLocalModuleAllowed(string moduleName, string path, ExecutionContext context)
        {
            if (IsDeniedBuiltinModule(moduleName, context)) return false;
            var importName = moduleName;
            if (importName.Contains('.'))
            {
                importName = importName[(importName.LastIndexOf('.') + 1)..];
            }

            var allowlistPath = ResolveLocalModuleAllowlistPath(path, context);
            return IsLocalModuleImportAllowed(importName, path, allowlistPath, context) ||
                IsLocalModuleImportAllowed(moduleName, path, allowlistPath, context);
        }

    }
}

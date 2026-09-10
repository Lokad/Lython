using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed class PkgutilModuleInfoObject :
        IPySequenceValue,
        IPyIndexableValue,
        IPyIterableValue,
        IPyRenderableValue,
        IPyDynamicAttributes,
        IPyHashableValue,
        IEquatable<PkgutilModuleInfoObject>
    {
        // Fixed labels are class-level constants like namedtuple _fields: shared
        // forever, so per-access reads cost nothing and alias stably.
        private static readonly PyString FieldsModuleFinder = PyString.FromString("module_finder");
        private static readonly PyString FieldsName = PyString.FromString("name");
        private static readonly PyString FieldsIsPackage = PyString.FromString("ispkg");
        private static readonly PyTuple FieldsTuple = PyTuple.FromOwnedArray([FieldsModuleFinder, FieldsName, FieldsIsPackage]);

        private readonly MemoryGovernor? _governor;
        private readonly LythonSourceSpan? _allocationSpan;

        public PkgutilModuleInfoObject(object moduleFinder, PyString name, bool isPackage, MemoryGovernor? governor = null, LythonSourceSpan? allocationSpan = null)
        {
            ModuleFinder = moduleFinder;
            Name = name;
            IsPackage = isPackage;
            _governor = governor;
            _allocationSpan = allocationSpan;
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

        public object CreateSlice(IEnumerable<object> items)
            => _governor is null ? new PyTuple(items) : new PyTuple(items, _governor, _allocationSpan);

        public object GetIndex(int index) => GetItem(index);

        public object GetSlice(IEnumerable<int> indices)
            => _governor is null ? new PyTuple(indices.Select(GetItem)) : new PyTuple(indices.Select(GetItem), _governor, _allocationSpan);

        public IEnumerator<object> GetEnumerator()
        {
            yield return ModuleFinder;
            yield return Name;
            yield return IsPackage;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public IEnumerable<object> Iterate() => this;

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "module_finder" => ModuleFinder,
                "name" => Name,
                "ispkg" => IsPackage,
                "_fields" => FieldsTuple,
                "_asdict" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 0)
                    {
                        throw new LythonRuntimeException("TypeError", "ModuleInfo._asdict() expects no arguments.", span);
                    }

                    var dict = new PyDict(context.MemoryGovernor, span);
                    dict.SetItem(FieldsModuleFinder, ModuleFinder);
                    dict.SetItem(FieldsName, Name);
                    dict.SetItem(FieldsIsPackage, IsPackage);
                    return dict;
                }, "ModuleInfo._asdict", []),
                "_replace" => BoundCallable.Create((arguments, span, context) =>
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

                    PkgutilModule.ChargePkgutilValue(context.MemoryGovernor, span);
                    return new PkgutilModuleInfoObject(moduleFinder, replacementName, IsTruthy(isPackageValue), context.MemoryGovernor, span);
                }, "ModuleInfo._replace", ["module_finder", "name", "ispkg"], requiredCount: 0),
                "count" => BoundCallable.Create((arguments, span, _) =>
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
                "index" => BoundCallable.Create((arguments, span, _) =>
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
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
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

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "name" => Name,
                "fullname" => Name,
                "ispkg" => IsPackage,
                "is_package" => BoundCallable.Create((arguments, span, context) =>
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
                "get_source" => BoundCallable.Create((arguments, span, context) =>
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
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
        public PyString RenderPython(PyRenderingContext context)
                    => PyString.FromString($"<lython loader for {PyRendering.ToReprPyString(Name, context).AsString()}>");

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }
}

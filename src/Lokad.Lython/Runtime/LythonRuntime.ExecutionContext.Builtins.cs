namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        // Plain functions retain no run state. Their __self__ comes from the
        // active caller; mutable types and their caches are constructed per run.
        private static readonly BuiltinCallable SharedLen = BuiltinCallable.Create("len", Len, LenAsync);
        private static readonly BuiltinCallable SharedAbs = BuiltinCallable.Create("abs", Abs, AbsAsync, ["x"]);
        private static readonly BuiltinCallable SharedSum = BuiltinCallable.CreateBorrowed(LythonCallableSignature.Create("sum", ["iterable", "start"], requiredCount: 1, maximumPositionalArgumentCount: 2, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1), Sum, SumAsync);

        // Signature interning may lock and package parameter arrays. Do it once
        // for immutable constructor metadata, independently of each fresh type.
        private static class BuiltinTypeSignatures
        {
            internal static readonly LythonCallableSignature Range = LythonCallableSignature.Create("range");
            internal static readonly LythonCallableSignature Enumerate = LythonCallableSignature.Create("enumerate", ["iterable", "start"], requiredCount: 1, maximumPositionalArgumentCount: 2, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
            internal static readonly LythonCallableSignature Reversed = LythonCallableSignature.Create("reversed", ["sequence"]);
            internal static readonly LythonCallableSignature Map = LythonCallableSignature.Create("map");
            internal static readonly LythonCallableSignature Filter = LythonCallableSignature.Create("filter", ["function", "iterable"]);
            internal static readonly LythonCallableSignature Slice = LythonCallableSignature.Create("slice");
            internal static readonly LythonCallableSignature Bool = LythonCallableSignature.Create("bool", ["value"], requiredCount: 0, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
            internal static readonly LythonCallableSignature Int = LythonCallableSignature.Create("int", ["x", "base"], requiredCount: 0, maximumPositionalArgumentCount: 2, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
            internal static readonly LythonCallableSignature Float = LythonCallableSignature.Create("float", ["value"], requiredCount: 0, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
            internal static readonly LythonCallableSignature Bytes = LythonCallableSignature.Create("bytes", ["source", "encoding", "errors"], requiredCount: 0, maximumPositionalArgumentCount: 3, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
            internal static readonly LythonCallableSignature Staticmethod = LythonCallableSignature.Create("staticmethod", ["func"], requiredCount: 1);
            internal static readonly LythonCallableSignature Classmethod = LythonCallableSignature.Create("classmethod", ["func"], requiredCount: 1);
            internal static readonly LythonCallableSignature Property = LythonCallableSignature.Create("property", ["fget", "fset", "fdel", "doc"], requiredCount: 0);
            internal static readonly LythonCallableSignature Super = LythonCallableSignature.Create("super");
            internal static readonly LythonCallableSignature List = LythonCallableSignature.Create("list", ["iterable"], requiredCount: 0, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
            internal static readonly LythonCallableSignature Tuple = LythonCallableSignature.Create("tuple", ["iterable"], requiredCount: 0, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
            internal static readonly LythonCallableSignature Set = LythonCallableSignature.Create("set", ["iterable"], requiredCount: 0, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
        }

        internal static bool IsLengthBuiltin(object value) => ReferenceEquals(value, SharedLen);
        internal static bool IsAbsoluteBuiltin(object value) => ReferenceEquals(value, SharedAbs);
        internal static bool IsSumBuiltin(object value) => ReferenceEquals(value, SharedSum);
        internal static bool IsRangeBuiltin(object value) => value is BuiltinCallable { Name: "range" };

        // Retain each run's connected object/metatype graph. Fixed functions
        // and immutable constructor signatures are shared above.
        // Fixed immutable definitions contain no execution or memory owner.
        // Mutable builtin namespaces and callable hierarchy caches stay per run.
        private static readonly (PyType Object, PyType Type) SharedCoreTypes = CreateCoreTypes();

        private static (PyType Object, PyType Type) CreateCoreTypes()
        {
            // Fixed entries: pre-size these per-run tables instead of growing
            // their storage on insertion. The builtin hint allows additions.
            var objectMembers = new Dictionary<string, object>(17, StringComparer.Ordinal)
            {
                ["__new__"] = new ObjectNewMethod(),
                ["__init__"] = new ObjectInitMethod(),
                ["__init_subclass__"] = new PyClassMethod(new ObjectInitSubclassMethod()),
                ["__getattribute__"] = new ObjectGetAttrMethod(),
                ["__setattr__"] = new ObjectSetAttrMethod(),
                ["__delattr__"] = new ObjectDelAttrMethod(),
                ["__format__"] = new ObjectFormatMethod(),
                ["__dir__"] = new ObjectDirMethod(),
                ["__eq__"] = new ObjectEqMethod(),
                ["__ne__"] = new ObjectNeMethod(),
                ["__lt__"] = new ObjectLtMethod(),
                ["__le__"] = new ObjectLeMethod(),
                ["__gt__"] = new ObjectGtMethod(),
                ["__ge__"] = new ObjectGeMethod(),
                ["__str__"] = new ObjectStrMethod(),
                ["__repr__"] = new ObjectReprMethod(),
                ["__hash__"] = new ObjectHashMethod()
            };
            var objectType = new PyType("object", Array.Empty<PyType>(), objectMembers);
            var typeMembers = new Dictionary<string, object>(StringComparer.Ordinal);
            var typeType = new PyType("type", [objectType], typeMembers);
            objectType.SetMetaType(typeType);
            typeType.SetMetaType(typeType);
            // The type metatype carries its own __new__ slot like CPython
            // (type.__new__ is not object.__new__); user classes keep sharing
            // the object slot through their MRO.
            typeMembers.Add("__new__", new TypeNewMethod(typeType, "type"));
            return (objectType, typeType);
        }

        internal sealed class BuiltinBindings
        {
            private static readonly object?[] SharedValues = CreateSharedValues();
            private object?[]? _runValues;

            public IEnumerable<string> Keys => ExecutionState.BuiltinNames;

            public object this[string name] => TryGetValue(name, out var value)
                ? value : throw new KeyNotFoundException(name);

            public bool TryGetValue(string name, [MaybeNullWhen(false)] out object value)
            {
                if (!ExecutionState.TryGetBuiltinSlot(name, out var index))
                {
                    value = null;
                    return false;
                }

                value = SharedValues[index] ?? GetRunValue(index, name);
                return true;
            }

            private object GetRunValue(int index, string name)
                => (_runValues ??= new object?[SharedValues.Length])[index] ??= CreateRunValue(name);

            private static object?[] CreateSharedValues()
            {
                var entries = new object?[ExecutionState.BuiltinNames.Count];
                entries[ExecutionState.GetBuiltinSlot("object")] = SharedCoreTypes.Object;
                entries[ExecutionState.GetBuiltinSlot("type")] = SharedCoreTypes.Type;
                entries[ExecutionState.GetBuiltinSlot("Ellipsis")] = PyEllipsis.Instance;
                entries[ExecutionState.GetBuiltinSlot("NotImplemented")] = PyNotImplemented.Instance;
                entries[ExecutionState.GetBuiltinSlot("open")] = new OpenCallable();
                entries[ExecutionState.GetBuiltinSlot("print")] = new PrintCallable();
                entries[ExecutionState.GetBuiltinSlot("input")] = BuiltinCallable.Create("input", Input, InputAsync, ["prompt"], requiredCount: 0);
                entries[ExecutionState.GetBuiltinSlot("repr")] = BuiltinCallable.Create("repr", Repr, ReprAsync, ["value"]);
                entries[ExecutionState.GetBuiltinSlot("ascii")] = BuiltinCallable.Create("ascii", Ascii, AsciiAsync, ["value"]);
                entries[ExecutionState.GetBuiltinSlot("format")] = BuiltinCallable.Create("format", Format, FormatAsync, ["value", "format_spec"], requiredCount: 1);
                entries[ExecutionState.GetBuiltinSlot("len")] = SharedLen;
                entries[ExecutionState.GetBuiltinSlot("sorted")] = BuiltinCallable.Create(LythonCallableSignature.Create("sorted", ["iterable", "key", "reverse"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1), Sorted, SortedAsync);
                entries[ExecutionState.GetBuiltinSlot("any")] = BuiltinCallable.Create("any", Any, AnyAsync);
                entries[ExecutionState.GetBuiltinSlot("all")] = BuiltinCallable.Create("all", All, AllAsync);
                entries[ExecutionState.GetBuiltinSlot("min")] = new MinMaxCallable(ExtremumOperation.Minimum);
                entries[ExecutionState.GetBuiltinSlot("max")] = new MinMaxCallable(ExtremumOperation.Maximum);
                entries[ExecutionState.GetBuiltinSlot("sum")] = SharedSum;
                entries[ExecutionState.GetBuiltinSlot("abs")] = SharedAbs;
                entries[ExecutionState.GetBuiltinSlot("pow")] = BuiltinCallable.Create("pow", Pow, ["base", "exp", "mod"], requiredCount: 2);
                entries[ExecutionState.GetBuiltinSlot("round")] = BuiltinCallable.Create("round", Round, ["number", "ndigits"], requiredCount: 1);
                entries[ExecutionState.GetBuiltinSlot("divmod")] = BuiltinCallable.Create("divmod", DivMod, DivModAsync, ["a", "b"]);
                entries[ExecutionState.GetBuiltinSlot("bin")] = BuiltinCallable.Create("bin", Bin, ["number"]);
                entries[ExecutionState.GetBuiltinSlot("oct")] = BuiltinCallable.Create("oct", Oct, ["number"]);
                entries[ExecutionState.GetBuiltinSlot("hex")] = BuiltinCallable.Create("hex", Hex, ["number"]);
                entries[ExecutionState.GetBuiltinSlot("chr")] = BuiltinCallable.Create("chr", Chr, ["i"]);
                entries[ExecutionState.GetBuiltinSlot("ord")] = BuiltinCallable.Create("ord", Ord, ["c"]);
                entries[ExecutionState.GetBuiltinSlot("iter")] = BuiltinCallable.Create("iter", Iter, IterAsync);
                entries[ExecutionState.GetBuiltinSlot("next")] = BuiltinCallable.Create(LythonCallableSignature.Create("next", ["iterator", "default"], requiredCount: 1, maximumPositionalArgumentCount: 2, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 2), Next, NextAsync);
                entries[ExecutionState.GetBuiltinSlot("isinstance")] = BuiltinCallable.Create("isinstance", IsInstance, ["value", "type"], requiredCount: 2);
                entries[ExecutionState.GetBuiltinSlot("issubclass")] = BuiltinCallable.Create("issubclass", IsSubclass, ["type", "base"], requiredCount: 2);
                entries[ExecutionState.GetBuiltinSlot("getattr")] = BuiltinCallable.Create("getattr", GetAttr, GetAttrAsync, ["object", "name", "default"], requiredCount: 2);
                entries[ExecutionState.GetBuiltinSlot("hasattr")] = BuiltinCallable.Create("hasattr", HasAttr, HasAttrAsync, ["object", "name"], requiredCount: 2);
                entries[ExecutionState.GetBuiltinSlot("setattr")] = BuiltinCallable.Create("setattr", SetAttr, ["object", "name", "value"], requiredCount: 3);
                entries[ExecutionState.GetBuiltinSlot("delattr")] = BuiltinCallable.Create("delattr", DelAttr, ["object", "name"], requiredCount: 2);
                entries[ExecutionState.GetBuiltinSlot("dir")] = BuiltinCallable.Create("dir", Dir, ["object"], requiredCount: 0);
                entries[ExecutionState.GetBuiltinSlot("vars")] = BuiltinCallable.Create("vars", Vars, VarsAsync, ["object"], requiredCount: 0);
                entries[ExecutionState.GetBuiltinSlot("globals")] = BuiltinCallable.Create("globals", Globals);
                entries[ExecutionState.GetBuiltinSlot("locals")] = BuiltinCallable.Create("locals", Locals);
                entries[ExecutionState.GetBuiltinSlot("callable")] = BuiltinCallable.Create("callable", Callable, ["object"]);
                entries[ExecutionState.GetBuiltinSlot("hash")] = BuiltinCallable.Create("hash", Hash, HashAsync, ["object"]);
                entries[ExecutionState.GetBuiltinSlot("id")] = BuiltinCallable.Create("id", Id);
                return entries;
            }

            private static object CreateRunValue(string name) => name switch
            {
                "str" => new StrCallable(),
                "range" => BuiltinCallable.Create(BuiltinTypeSignatures.Range, Range),
                "enumerate" => BuiltinCallable.Create(BuiltinTypeSignatures.Enumerate, Enumerate),
                "zip" => new ZipCallable(),
                "reversed" => BuiltinCallable.Create(BuiltinTypeSignatures.Reversed, Reversed),
                "map" => BuiltinCallable.Create(BuiltinTypeSignatures.Map, Map, MapAsync),
                "filter" => BuiltinCallable.Create(BuiltinTypeSignatures.Filter, Filter, FilterAsync),
                "slice" => BuiltinCallable.Create(BuiltinTypeSignatures.Slice, Slice),
                "bool" => BuiltinCallable.Create(BuiltinTypeSignatures.Bool, Bool, BoolAsync),
                "int" => BuiltinCallable.Create(BuiltinTypeSignatures.Int, Int, IntAsync),
                "complex" => new PyBuiltinRuntimeType("complex", ConstructComplex, null, ConstructComplexAsync),
                "float" => BuiltinCallable.Create(BuiltinTypeSignatures.Float, Float, FloatAsync),
                "bytes" => BuiltinCallable.Create(BuiltinTypeSignatures.Bytes, Bytes, BytesAsync),
                "staticmethod" => BuiltinCallable.Create(BuiltinTypeSignatures.Staticmethod, StaticMethod),
                "classmethod" => BuiltinCallable.Create(BuiltinTypeSignatures.Classmethod, ClassMethod),
                "property" => BuiltinCallable.Create(BuiltinTypeSignatures.Property, Property, PropertyAsync),
                "super" => BuiltinCallable.Create(BuiltinTypeSignatures.Super, Super),
                "list" => BuiltinCallable.Create(BuiltinTypeSignatures.List, List, ListAsync),
                "tuple" => BuiltinCallable.Create(BuiltinTypeSignatures.Tuple, Tuple, TupleAsync),
                "dict" => new DictCallable(),
                "set" => BuiltinCallable.Create(BuiltinTypeSignatures.Set, Set, SetAsync),
                "IOError" or "EnvironmentError" or "OSError" => new ExceptionTypeValue("OSError"),
                "BaseException" or "Exception" or "ArithmeticError" or "LookupError"
                    or "UnicodeError" or "Warning" or "FutureWarning" or "TypeError"
                    or "ValueError" or "KeyError" or "IndexError" or "RuntimeError"
                    or "EOFError" or "AssertionError" or "ImportError" or "ModuleNotFoundError"
                    or "NameError" or "UnboundLocalError" or "AttributeError" or "SyntaxError"
                    or "FileNotFoundError" or "FileExistsError" or "IsADirectoryError" or "NotADirectoryError"
                    or "PermissionError" or "BrokenPipeError" or "TimeoutError" or "StopIteration"
                    or "ZeroDivisionError" or "NotImplementedError" or "RecursionError" or "MemoryError"
                    or "UnicodeEncodeError" or "UnicodeDecodeError" or "UnicodeTranslateError" or "OverflowError"
                    or "SystemExit" or "GeneratorExit" or "KeyboardInterrupt" => new ExceptionTypeValue(name),
                _ => throw new InvalidOperationException("Missing builtin value for " + name),
            };
        }
    }
}

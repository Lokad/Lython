namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        // Plain functions retain no run state. Their __self__ comes from the
        // active caller; mutable types and their caches are constructed per run.
        private static readonly BuiltinCallable SharedLen = BuiltinCallable.Create("len", Len, LenAsync);
        private static readonly BuiltinCallable SharedAbs = BuiltinCallable.Create("abs", Abs, AbsAsync, ["x"]);
        private static readonly BuiltinCallable SharedSum = BuiltinCallable.Create(LythonCallableSignature.Create("sum", ["iterable", "start"], requiredCount: 1, maximumPositionalArgumentCount: 2, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1), Sum, SumAsync);

        private static readonly (string Name, object Value)[] SharedFunctions =
        [
            ("open", new OpenCallable()),
            ("print", new PrintCallable()),
            ("input", BuiltinCallable.Create("input", Input, InputAsync, ["prompt"], requiredCount: 0)),
            ("repr", BuiltinCallable.Create("repr", Repr, ReprAsync, ["value"])),
            ("ascii", BuiltinCallable.Create("ascii", Ascii, AsciiAsync, ["value"])),
            ("format", BuiltinCallable.Create("format", Format, FormatAsync, ["value", "format_spec"], requiredCount: 1)),
            ("len", SharedLen),
            ("sorted", BuiltinCallable.Create(LythonCallableSignature.Create("sorted", ["iterable", "key", "reverse"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1), Sorted, SortedAsync)),
            ("any", BuiltinCallable.Create("any", Any, AnyAsync)),
            ("all", BuiltinCallable.Create("all", All, AllAsync)),
            ("min", new MinMaxCallable(ExtremumOperation.Minimum)),
            ("max", new MinMaxCallable(ExtremumOperation.Maximum)),
            ("sum", SharedSum),
            ("abs", SharedAbs),
            ("pow", BuiltinCallable.Create("pow", Pow, ["base", "exp", "mod"], requiredCount: 2)),
            ("round", BuiltinCallable.Create("round", Round, ["number", "ndigits"], requiredCount: 1)),
            ("divmod", BuiltinCallable.Create("divmod", DivMod, DivModAsync, ["a", "b"])),
            ("bin", BuiltinCallable.Create("bin", Bin, ["number"])),
            ("oct", BuiltinCallable.Create("oct", Oct, ["number"])),
            ("hex", BuiltinCallable.Create("hex", Hex, ["number"])),
            ("chr", BuiltinCallable.Create("chr", Chr, ["i"])),
            ("ord", BuiltinCallable.Create("ord", Ord, ["c"])),
            ("iter", BuiltinCallable.Create("iter", Iter, IterAsync)),
            ("next", BuiltinCallable.Create(LythonCallableSignature.Create("next", ["iterator", "default"], requiredCount: 1, maximumPositionalArgumentCount: 2, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 2), Next, NextAsync)),
            ("isinstance", BuiltinCallable.Create("isinstance", IsInstance, ["value", "type"], requiredCount: 2)),
            ("issubclass", BuiltinCallable.Create("issubclass", IsSubclass, ["type", "base"], requiredCount: 2)),
            ("getattr", BuiltinCallable.Create("getattr", GetAttr, GetAttrAsync, ["object", "name", "default"], requiredCount: 2)),
            ("hasattr", BuiltinCallable.Create("hasattr", HasAttr, HasAttrAsync, ["object", "name"], requiredCount: 2)),
            ("setattr", BuiltinCallable.Create("setattr", SetAttr, ["object", "name", "value"], requiredCount: 3)),
            ("delattr", BuiltinCallable.Create("delattr", DelAttr, ["object", "name"], requiredCount: 2)),
            ("dir", BuiltinCallable.Create("dir", Dir, ["object"], requiredCount: 0)),
            ("vars", BuiltinCallable.Create("vars", Vars, VarsAsync, ["object"], requiredCount: 0)),
            ("globals", BuiltinCallable.Create("globals", Globals)),
            ("locals", BuiltinCallable.Create("locals", Locals)),
            ("callable", BuiltinCallable.Create("callable", Callable, ["object"])),
            ("hash", BuiltinCallable.Create("hash", Hash, HashAsync, ["object"])),
            ("id", BuiltinCallable.Create("id", Id)),
        ];

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

        private static Dictionary<string, object> CreateBuiltinVariables()
        {
            var (objectType, typeType) = SharedCoreTypes;
            var osErrorType = new ExceptionTypeValue("OSError");

            var builtins = new Dictionary<string, object>(128, StringComparer.Ordinal)
            {
                ["object"] = objectType,
                ["type"] = typeType,
                ["str"] = new StrCallable(),
                ["range"] = BuiltinCallable.Create(BuiltinTypeSignatures.Range, Range),
                ["enumerate"] = BuiltinCallable.Create(BuiltinTypeSignatures.Enumerate, Enumerate),
                ["zip"] = new ZipCallable(),
                ["reversed"] = BuiltinCallable.Create(BuiltinTypeSignatures.Reversed, Reversed),
                ["map"] = BuiltinCallable.Create(BuiltinTypeSignatures.Map, Map, MapAsync),
                ["filter"] = BuiltinCallable.Create(BuiltinTypeSignatures.Filter, Filter, FilterAsync),
                ["slice"] = BuiltinCallable.Create(BuiltinTypeSignatures.Slice, Slice),
                ["BaseException"] = new ExceptionTypeValue("BaseException"),
                ["Exception"] = new ExceptionTypeValue("Exception"),
                ["ArithmeticError"] = new ExceptionTypeValue("ArithmeticError"),
                ["LookupError"] = new ExceptionTypeValue("LookupError"),
                ["UnicodeError"] = new ExceptionTypeValue("UnicodeError"),
                ["Warning"] = new ExceptionTypeValue("Warning"),
                ["FutureWarning"] = new ExceptionTypeValue("FutureWarning"),
                ["TypeError"] = new ExceptionTypeValue("TypeError"),
                ["ValueError"] = new ExceptionTypeValue("ValueError"),
                ["KeyError"] = new ExceptionTypeValue("KeyError"),
                ["IndexError"] = new ExceptionTypeValue("IndexError"),
                ["RuntimeError"] = new ExceptionTypeValue("RuntimeError"),
                ["EOFError"] = new ExceptionTypeValue("EOFError"),
                ["AssertionError"] = new ExceptionTypeValue("AssertionError"),
                ["ImportError"] = new ExceptionTypeValue("ImportError"),
                ["ModuleNotFoundError"] = new ExceptionTypeValue("ModuleNotFoundError"),
                ["NameError"] = new ExceptionTypeValue("NameError"),
                ["UnboundLocalError"] = new ExceptionTypeValue("UnboundLocalError"),
                ["AttributeError"] = new ExceptionTypeValue("AttributeError"),
                ["SyntaxError"] = new ExceptionTypeValue("SyntaxError"),
                ["FileNotFoundError"] = new ExceptionTypeValue("FileNotFoundError"),
                ["FileExistsError"] = new ExceptionTypeValue("FileExistsError"),
                ["IsADirectoryError"] = new ExceptionTypeValue("IsADirectoryError"),
                ["NotADirectoryError"] = new ExceptionTypeValue("NotADirectoryError"),
                ["PermissionError"] = new ExceptionTypeValue("PermissionError"),
                ["BrokenPipeError"] = new ExceptionTypeValue("BrokenPipeError"),
                ["TimeoutError"] = new ExceptionTypeValue("TimeoutError"),
                ["IOError"] = osErrorType,
                ["EnvironmentError"] = osErrorType,
                ["OSError"] = osErrorType,
                ["StopIteration"] = new ExceptionTypeValue("StopIteration"),
                ["ZeroDivisionError"] = new ExceptionTypeValue("ZeroDivisionError"),
                ["NotImplementedError"] = new ExceptionTypeValue("NotImplementedError"),
                ["RecursionError"] = new ExceptionTypeValue("RecursionError"),
                ["MemoryError"] = new ExceptionTypeValue("MemoryError"),
                ["UnicodeEncodeError"] = new ExceptionTypeValue("UnicodeEncodeError"),
                ["UnicodeDecodeError"] = new ExceptionTypeValue("UnicodeDecodeError"),
                ["UnicodeTranslateError"] = new ExceptionTypeValue("UnicodeTranslateError"),
                ["OverflowError"] = new ExceptionTypeValue("OverflowError"),
                ["SystemExit"] = new ExceptionTypeValue("SystemExit"),
                ["GeneratorExit"] = new ExceptionTypeValue("GeneratorExit"),
                ["KeyboardInterrupt"] = new ExceptionTypeValue("KeyboardInterrupt"),
                ["bool"] = BuiltinCallable.Create(BuiltinTypeSignatures.Bool, Bool, BoolAsync),
                ["int"] = BuiltinCallable.Create(BuiltinTypeSignatures.Int, Int, IntAsync),
                ["complex"] = new PyBuiltinRuntimeType("complex", ConstructComplex, null, ConstructComplexAsync),
                ["float"] = BuiltinCallable.Create(BuiltinTypeSignatures.Float, Float, FloatAsync),
                ["bytes"] = BuiltinCallable.Create(BuiltinTypeSignatures.Bytes, Bytes, BytesAsync),
                ["staticmethod"] = BuiltinCallable.Create(BuiltinTypeSignatures.Staticmethod, StaticMethod),
                ["classmethod"] = BuiltinCallable.Create(BuiltinTypeSignatures.Classmethod, ClassMethod),
                ["property"] = BuiltinCallable.Create(BuiltinTypeSignatures.Property, Property, PropertyAsync),
                ["super"] = BuiltinCallable.Create(BuiltinTypeSignatures.Super, Super),
                ["list"] = BuiltinCallable.Create(BuiltinTypeSignatures.List, List, ListAsync),
                ["tuple"] = BuiltinCallable.Create(BuiltinTypeSignatures.Tuple, Tuple, TupleAsync),
                ["dict"] = new DictCallable(),
                ["set"] = BuiltinCallable.Create(BuiltinTypeSignatures.Set, Set, SetAsync),
                ["Ellipsis"] = PyEllipsis.Instance,
                ["NotImplemented"] = PyNotImplemented.Instance,
            };

            foreach (var (name, value) in SharedFunctions) builtins.Add(name, value);
            return builtins;
        }
    }
}

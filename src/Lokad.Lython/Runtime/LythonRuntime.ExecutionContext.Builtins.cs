namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        // Python types are mutable. Every root execution context therefore needs
        // its own connected object/type graph instead of sharing a cached table.
        private static Dictionary<string, object> CreateBuiltinVariables()
        {
            var objectMembers = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["__new__"] = new PyStaticMethod(new ObjectNewMethod()),
                ["__init__"] = new ObjectInitMethod(),
                ["__init_subclass__"] = new PyClassMethod(new ObjectInitSubclassMethod()),
                ["__getattribute__"] = new ObjectGetAttrMethod(),
                ["__setattr__"] = new ObjectSetAttrMethod(),
                ["__delattr__"] = new ObjectDelAttrMethod()
            };
            var objectType = new PyType("object", Array.Empty<PyType>(), objectMembers);
            var typeType = new PyType("type", [objectType], new Dictionary<string, object>(StringComparer.Ordinal));
            objectType.SetMetaType(typeType);
            typeType.SetMetaType(typeType);
            // The type metatype carries its own __new__ slot like CPython
            // (type.__new__ is not object.__new__); user classes keep sharing
            // the object slot through their MRO.
            typeType.TrySetMember("__new__", new TypeNewMethod(typeType, "type"));

            var osErrorType = new ExceptionTypeValue("OSError");

            var builtins = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["object"] = objectType,
                ["type"] = typeType,
                ["open"] = new OpenCallable(),
                ["print"] = new PrintCallable(),
                ["input"] = BuiltinCallable.Create("input", Input, InputAsync, ["prompt"], requiredCount: 0),
                ["str"] = BuiltinCallable.Create(LythonCallableSignature.Create("str", ["object", "encoding", "errors"], requiredCount: 0, maximumPositionalArgumentCount: 3, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1), Str),
                ["repr"] = BuiltinCallable.Create("repr", Repr, ["value"]),
                ["ascii"] = BuiltinCallable.Create("ascii", Ascii, ["value"]),
                ["format"] = BuiltinCallable.Create("format", Format, ["value", "format_spec"], requiredCount: 1),
                ["len"] = BuiltinCallable.Create("len", Len),
                ["sorted"] = BuiltinCallable.Create(LythonCallableSignature.Create("sorted", ["iterable", "key", "reverse"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1), Sorted, SortedAsync),
                ["any"] = BuiltinCallable.Create("any", Any, AnyAsync),
                ["all"] = BuiltinCallable.Create("all", All, AllAsync),
                ["min"] = new MinMaxCallable(ExtremumOperation.Minimum),
                ["max"] = new MinMaxCallable(ExtremumOperation.Maximum),
                ["sum"] = BuiltinCallable.Create(LythonCallableSignature.Create("sum", ["iterable", "start"], requiredCount: 1, maximumPositionalArgumentCount: 2, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1), Sum, SumAsync),
                ["abs"] = BuiltinCallable.Create("abs", Abs, ["x"]),
                ["pow"] = BuiltinCallable.Create("pow", Pow, ["base", "exp", "mod"], requiredCount: 2),
                ["round"] = BuiltinCallable.Create("round", Round, ["number", "ndigits"], requiredCount: 1),
                ["divmod"] = BuiltinCallable.Create("divmod", DivMod, ["a", "b"]),
                ["bin"] = BuiltinCallable.Create("bin", Bin, ["number"]),
                ["oct"] = BuiltinCallable.Create("oct", Oct, ["number"]),
                ["hex"] = BuiltinCallable.Create("hex", Hex, ["number"]),
                ["chr"] = BuiltinCallable.Create("chr", Chr, ["i"]),
                ["ord"] = BuiltinCallable.Create("ord", Ord, ["c"]),
                ["range"] = BuiltinCallable.Create("range", Range),
                ["enumerate"] = BuiltinCallable.Create(LythonCallableSignature.Create("enumerate", ["iterable", "start"], requiredCount: 1, maximumPositionalArgumentCount: 2, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1), Enumerate),
                ["zip"] = new ZipCallable(),
                ["iter"] = BuiltinCallable.Create("iter", Iter, IterAsync),
                ["next"] = BuiltinCallable.Create(LythonCallableSignature.Create("next", ["iterator", "default"], requiredCount: 1, maximumPositionalArgumentCount: 2, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 2), Next, NextAsync),
                ["reversed"] = BuiltinCallable.Create("reversed", Reversed, ["sequence"]),
                ["map"] = BuiltinCallable.Create("map", Map),
                ["filter"] = BuiltinCallable.Create("filter", Filter, ["function", "iterable"]),
                ["slice"] = BuiltinCallable.Create("slice", Slice),
                ["BaseException"] = new ExceptionTypeValue("BaseException"),
                ["Exception"] = new ExceptionTypeValue("Exception"),
                ["ArithmeticError"] = new ExceptionTypeValue("ArithmeticError"),
                ["LookupError"] = new ExceptionTypeValue("LookupError"),
                ["UnicodeError"] = new ExceptionTypeValue("UnicodeError"),
                ["Warning"] = new ExceptionTypeValue("Warning"),
                ["TypeError"] = new ExceptionTypeValue("TypeError"),
                ["ValueError"] = new ExceptionTypeValue("ValueError"),
                ["KeyError"] = new ExceptionTypeValue("KeyError"),
                ["IndexError"] = new ExceptionTypeValue("IndexError"),
                ["RuntimeError"] = new ExceptionTypeValue("RuntimeError"),
                ["AssertionError"] = new ExceptionTypeValue("AssertionError"),
                ["ImportError"] = new ExceptionTypeValue("ImportError"),
                ["ModuleNotFoundError"] = new ExceptionTypeValue("ModuleNotFoundError"),
                ["NameError"] = new ExceptionTypeValue("NameError"),
                ["AttributeError"] = new ExceptionTypeValue("AttributeError"),
                ["SyntaxError"] = new ExceptionTypeValue("SyntaxError"),
                ["FileNotFoundError"] = new ExceptionTypeValue("FileNotFoundError"),
                ["FileExistsError"] = new ExceptionTypeValue("FileExistsError"),
                ["IsADirectoryError"] = new ExceptionTypeValue("IsADirectoryError"),
                ["NotADirectoryError"] = new ExceptionTypeValue("NotADirectoryError"),
                ["PermissionError"] = new ExceptionTypeValue("PermissionError"),
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
                ["bool"] = BuiltinCallable.Create(LythonCallableSignature.Create("bool", ["value"], requiredCount: 0, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1), Bool),
                ["int"] = BuiltinCallable.Create(LythonCallableSignature.Create("int", ["x", "base"], requiredCount: 0, maximumPositionalArgumentCount: 2, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1), Int),
                ["float"] = BuiltinCallable.Create(LythonCallableSignature.Create("float", ["value"], requiredCount: 0, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1), Float),
                ["bytes"] = BuiltinCallable.Create(LythonCallableSignature.Create("bytes", ["source", "encoding", "errors"], requiredCount: 0, maximumPositionalArgumentCount: 3, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1), Bytes),
                ["staticmethod"] = BuiltinCallable.Create("staticmethod", StaticMethod, ["func"], requiredCount: 1),
                ["classmethod"] = BuiltinCallable.Create("classmethod", ClassMethod, ["func"], requiredCount: 1),
                ["property"] = BuiltinCallable.Create("property", Property, ["fget", "fset"], requiredCount: 0),
                ["super"] = BuiltinCallable.Create("super", Super),
                ["isinstance"] = BuiltinCallable.Create("isinstance", IsInstance, ["value", "type"], requiredCount: 2),
                ["issubclass"] = BuiltinCallable.Create("issubclass", IsSubclass, ["type", "base"], requiredCount: 2),
                ["getattr"] = BuiltinCallable.Create("getattr", GetAttr, ["object", "name", "default"], requiredCount: 2),
                ["hasattr"] = BuiltinCallable.Create("hasattr", HasAttr, ["object", "name"], requiredCount: 2),
                ["setattr"] = BuiltinCallable.Create("setattr", SetAttr, ["object", "name", "value"], requiredCount: 3),
                ["delattr"] = BuiltinCallable.Create("delattr", DelAttr, ["object", "name"], requiredCount: 2),
                ["dir"] = BuiltinCallable.Create("dir", Dir, ["object"], requiredCount: 0),
                ["vars"] = BuiltinCallable.Create("vars", Vars, ["object"], requiredCount: 0),
                ["callable"] = BuiltinCallable.Create("callable", Callable, ["object"]),
                ["hash"] = BuiltinCallable.Create("hash", Hash, ["object"]),
                ["list"] = BuiltinCallable.Create(LythonCallableSignature.Create("list", ["iterable"], requiredCount: 0, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1), List, ListAsync),
                ["tuple"] = BuiltinCallable.Create(LythonCallableSignature.Create("tuple", ["iterable"], requiredCount: 0, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1), Tuple, TupleAsync),
                ["dict"] = new DictCallable(),
                ["set"] = BuiltinCallable.Create(LythonCallableSignature.Create("set", ["iterable"], requiredCount: 0, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1), Set, SetAsync),
                ["Ellipsis"] = PyEllipsis.Instance,
                ["NotImplemented"] = PyNotImplemented.Instance,
            };

            return builtins;
        }
    }
}

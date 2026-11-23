namespace Lokad.Lython.Runtime;

internal static partial class LythonKnownCallableSignatures
{
    internal static class Sys
    {
        public static readonly LythonCallableSignature Exit = LythonCallableSignature.Create("sys.exit", ["code"], requiredCount: 0);
        public static readonly LythonCallableSignature GetDefaultEncoding = LythonCallableSignature.Create("sys.getdefaultencoding", []);
        public static readonly LythonCallableSignature ExcInfo = LythonCallableSignature.Create("sys.exc_info", []);
        public static readonly LythonCallableSignature GetSizeOf = LythonCallableSignature.Create("sys.getsizeof", ["object", "default"], requiredCount: 1);
        public static readonly LythonCallableSignature SetTrace = LythonCallableSignature.Create("sys.settrace", ["function"]);
        public static readonly LythonCallableSignature SetProfile = LythonCallableSignature.Create("sys.setprofile", ["function"]);
        public static readonly LythonCallableSignature SetRecursionLimit = LythonCallableSignature.Create("sys.setrecursionlimit", ["limit"]);
        public static readonly LythonCallableSignature AddAuditHook = LythonCallableSignature.Create("sys.addaudithook", ["hook"]);
        public static readonly LythonCallableSignature Audit = LythonCallableSignature.Create("sys.audit", ["event", "args"], requiredCount: 1, variadicParameters: LythonVariadicParameters.Keywords);
    }

    internal static class Pathlib
    {
        public static readonly LythonCallableSignature Path = LythonCallableSignature.Create("pathlib.Path", requiredCount: 0);
        public static readonly LythonCallableSignature PurePath = LythonCallableSignature.Create("pathlib.PurePath", requiredCount: 0);
        public static readonly LythonCallableSignature PurePosixPath = LythonCallableSignature.Create("pathlib.PurePosixPath", requiredCount: 0);
        public static readonly LythonCallableSignature PosixPath = LythonCallableSignature.Create("pathlib.PosixPath", requiredCount: 0);
        public static readonly LythonCallableSignature PureWindowsPath = LythonCallableSignature.Create("pathlib.PureWindowsPath", requiredCount: 0);
        public static readonly LythonCallableSignature WindowsPath = LythonCallableSignature.Create("pathlib.WindowsPath", requiredCount: 0);
    }

    internal static class Json
    {
        public static readonly LythonCallableSignature Load = LythonCallableSignature.Create("json.load", ["fp", "cls", "object_hook", "parse_float", "parse_int", "parse_constant", "object_pairs_hook", "strict"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.Keywords);
        public static readonly LythonCallableSignature Loads = LythonCallableSignature.Create("json.loads", ["s", "cls", "object_hook", "parse_float", "parse_int", "parse_constant", "object_pairs_hook", "strict"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.Keywords);
        public static readonly LythonCallableSignature Dump = LythonCallableSignature.Create("json.dump", ["obj", "fp", "skipkeys", "ensure_ascii", "check_circular", "allow_nan", "cls", "indent", "separators", "default", "sort_keys"], requiredCount: 2, maximumPositionalArgumentCount: 2, variadicParameters: LythonVariadicParameters.Keywords);
        public static readonly LythonCallableSignature Dumps = LythonCallableSignature.Create("json.dumps", ["obj", "skipkeys", "ensure_ascii", "check_circular", "allow_nan", "cls", "indent", "separators", "default", "sort_keys"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.Keywords);
        public static readonly LythonCallableSignature DecoderDecode = LythonCallableSignature.Create("json.JSONDecoder.decode", ["s"], requiredCount: 1, maximumPositionalArgumentCount: 1);
        public static readonly LythonCallableSignature DecoderRawDecode = LythonCallableSignature.Create("json.JSONDecoder.raw_decode", ["s", "idx"], requiredCount: 1, maximumPositionalArgumentCount: 2);
        public static readonly LythonCallableSignature EncoderEncode = LythonCallableSignature.Create("json.JSONEncoder.encode", ["o"], requiredCount: 1, maximumPositionalArgumentCount: 1);
        public static readonly LythonCallableSignature EncoderIterencode = LythonCallableSignature.Create("json.JSONEncoder.iterencode", ["o", "_one_shot"], requiredCount: 1, maximumPositionalArgumentCount: 2);
        public static readonly LythonCallableSignature EncoderDefault = LythonCallableSignature.Create("json.JSONEncoder.default", ["o"], requiredCount: 1, maximumPositionalArgumentCount: 1);
    }

    internal static class Csv
    {
        public static readonly LythonCallableSignature Reader = LythonCallableSignature.Create("csv.reader", ["csvfile", "dialect", "delimiter", "quotechar", "quoting", "doublequote", "escapechar", "skipinitialspace", "lineterminator", "strict"], requiredCount: 1, maximumPositionalArgumentCount: 2);
        public static readonly LythonCallableSignature Writer = LythonCallableSignature.Create("csv.writer", ["fileobj", "dialect", "delimiter", "quotechar", "quoting", "doublequote", "escapechar", "skipinitialspace", "lineterminator", "strict"], requiredCount: 0, maximumPositionalArgumentCount: 2);
        public static readonly LythonCallableSignature DictReader = LythonCallableSignature.Create("csv.DictReader", ["f", "fieldnames", "restkey", "restval", "dialect", "delimiter", "quotechar", "quoting", "doublequote", "escapechar", "skipinitialspace", "lineterminator", "strict"], requiredCount: 1, maximumPositionalArgumentCount: 5);
        public static readonly LythonCallableSignature DictWriter = LythonCallableSignature.Create("csv.DictWriter", ["f", "fieldnames", "restval", "extrasaction", "dialect", "delimiter", "quotechar", "quoting", "doublequote", "escapechar", "skipinitialspace", "lineterminator", "strict"], requiredCount: 2, maximumPositionalArgumentCount: 5);
    }

    internal static class Collections
    {
        public static readonly LythonCallableSignature DefaultDict = LythonCallableSignature.Create("collections.defaultdict", ["default_factory", "iterable"], requiredCount: 0, maximumPositionalArgumentCount: 2, variadicParameters: LythonVariadicParameters.Keywords);
        public static readonly LythonCallableSignature Counter = LythonCallableSignature.Create("collections.Counter", ["iterable"], requiredCount: 0, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.Keywords);
        public static readonly LythonCallableSignature Deque = LythonCallableSignature.Create("collections.deque", ["iterable", "maxlen"], requiredCount: 0);
        public static readonly LythonCallableSignature NamedTuple = LythonCallableSignature.Create("collections.namedtuple", ["typename", "field_names", "rename", "defaults", "module"], requiredCount: 2, maximumPositionalArgumentCount: 2);
        public static readonly LythonCallableSignature OrderedDict = LythonCallableSignature.Create("collections.OrderedDict", ["mapping"], requiredCount: 0, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.Keywords);
        public static readonly LythonCallableSignature ChainMap = LythonCallableSignature.Create("collections.ChainMap", requiredCount: 0);
        public static readonly LythonCallableSignature UserDict = LythonCallableSignature.Create("collections.UserDict", requiredCount: 0);
        public static readonly LythonCallableSignature UserList = LythonCallableSignature.Create("collections.UserList", requiredCount: 0);
        public static readonly LythonCallableSignature UserString = LythonCallableSignature.Create("collections.UserString", requiredCount: 0);
    }

    internal static class Copy
    {
        public static readonly LythonCallableSignature CopyCopy = LythonCallableSignature.Create("copy.copy", ["x"]);
        public static readonly LythonCallableSignature DeepCopy = LythonCallableSignature.Create("copy.deepcopy", ["x", "memo"], requiredCount: 1);
        public static readonly LythonCallableSignature Replace = LythonCallableSignature.Create("copy.replace", ["__object"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.Keywords);
    }

    internal static class Shutil
    {
        public static readonly LythonCallableSignature CopyFile = LythonCallableSignature.Create("shutil.copyfile", ["src", "dst", "follow_symlinks"], requiredCount: 2, maximumPositionalArgumentCount: 2);
        public static readonly LythonCallableSignature Copy = LythonCallableSignature.Create("shutil.copy", ["src", "dst", "follow_symlinks"], requiredCount: 2, maximumPositionalArgumentCount: 2);
        public static readonly LythonCallableSignature Copy2 = LythonCallableSignature.Create("shutil.copy2", ["src", "dst", "follow_symlinks"], requiredCount: 2, maximumPositionalArgumentCount: 2);
        public static readonly LythonCallableSignature Move = LythonCallableSignature.Create("shutil.move", ["src", "dst", "copy_function"], requiredCount: 2, maximumPositionalArgumentCount: 2);
        public static readonly LythonCallableSignature CopyFileObj = LythonCallableSignature.Create("shutil.copyfileobj", ["fsrc", "fdst", "length"], requiredCount: 2);
    }

    internal static class Functools
    {
        public static readonly LythonCallableSignature UpdateWrapper = LythonCallableSignature.Create("functools.update_wrapper", ["wrapper", "wrapped", "assigned", "updated"], requiredCount: 2);
        public static readonly LythonCallableSignature Wraps = LythonCallableSignature.Create("functools.wraps", ["wrapped", "assigned", "updated"], requiredCount: 1);
        public static readonly LythonCallableSignature TotalOrdering = LythonCallableSignature.Create("functools.total_ordering", ["cls"]);
        public static readonly LythonCallableSignature Reduce = LythonCallableSignature.Create("functools.reduce", ["function", "iterable", "initializer"], requiredCount: 2);
        public static readonly LythonCallableSignature Partial = LythonCallableSignature.Create("functools.partial", ["func"], requiredCount: 1, variadicParameters: LythonVariadicParameters.Keywords | LythonVariadicParameters.Positional);
        public static readonly LythonCallableSignature PartialMethod = LythonCallableSignature.Create("functools.partialmethod", ["func"], requiredCount: 1, variadicParameters: LythonVariadicParameters.Keywords | LythonVariadicParameters.Positional);
        public static readonly LythonCallableSignature CmpToKey = LythonCallableSignature.Create("functools.cmp_to_key", ["mycmp"]);
        public static readonly LythonCallableSignature LruCache = LythonCallableSignature.Create("functools.lru_cache", ["maxsize", "typed"], requiredCount: 0);
        public static readonly LythonCallableSignature Cache = LythonCallableSignature.Create("functools.cache", ["user_function"]);
        public static readonly LythonCallableSignature CachedProperty = LythonCallableSignature.Create("functools.cached_property", ["func"]);
        public static readonly LythonCallableSignature SingleDispatch = LythonCallableSignature.Create("functools.singledispatch", ["func"]);
        public static readonly LythonCallableSignature SingleDispatchMethod = LythonCallableSignature.Create("functools.singledispatchmethod", ["func"]);
        public static readonly LythonCallableSignature RecursiveRepr = LythonCallableSignature.Create("functools.recursive_repr", ["fillvalue"], requiredCount: 0);
    }

    internal static class Operator
    {
        public static readonly LythonCallableSignature Truth = LythonCallableSignature.Create("operator.truth", ["obj"]);
        public static readonly LythonCallableSignature Not = LythonCallableSignature.Create("operator.not_", ["obj"]);
        public static readonly LythonCallableSignature Is = LythonCallableSignature.Create("operator.is_", ["a", "b"]);
        public static readonly LythonCallableSignature IsNot = LythonCallableSignature.Create("operator.is_not", ["a", "b"]);
        public static readonly LythonCallableSignature Abs = LythonCallableSignature.Create("operator.abs", ["obj"]);
        public static readonly LythonCallableSignature Neg = LythonCallableSignature.Create("operator.neg", ["obj"]);
        public static readonly LythonCallableSignature Pos = LythonCallableSignature.Create("operator.pos", ["obj"]);
        public static readonly LythonCallableSignature Invert = LythonCallableSignature.Create("operator.invert", ["obj"]);
        public static readonly LythonCallableSignature Index = LythonCallableSignature.Create("operator.index", ["obj"]);
        public static readonly LythonCallableSignature Add = LythonCallableSignature.Create("operator.add", ["a", "b"]);
        public static readonly LythonCallableSignature Sub = LythonCallableSignature.Create("operator.sub", ["a", "b"]);
        public static readonly LythonCallableSignature Mul = LythonCallableSignature.Create("operator.mul", ["a", "b"]);
        public static readonly LythonCallableSignature TrueDiv = LythonCallableSignature.Create("operator.truediv", ["a", "b"]);
        public static readonly LythonCallableSignature FloorDiv = LythonCallableSignature.Create("operator.floordiv", ["a", "b"]);
        public static readonly LythonCallableSignature Mod = LythonCallableSignature.Create("operator.mod", ["a", "b"]);
        public static readonly LythonCallableSignature Pow = LythonCallableSignature.Create("operator.pow", ["a", "b"]);
        public static readonly LythonCallableSignature IMatMul = LythonCallableSignature.Create("operator.imatmul", ["a", "b"]);
        public static readonly LythonCallableSignature MatMul = LythonCallableSignature.Create("operator.matmul", ["a", "b"]);
        public static readonly LythonCallableSignature LShift = LythonCallableSignature.Create("operator.lshift", ["a", "b"]);
        public static readonly LythonCallableSignature RShift = LythonCallableSignature.Create("operator.rshift", ["a", "b"]);
        public static readonly LythonCallableSignature And = LythonCallableSignature.Create("operator.and_", ["a", "b"]);
        public static readonly LythonCallableSignature Or = LythonCallableSignature.Create("operator.or_", ["a", "b"]);
        public static readonly LythonCallableSignature Xor = LythonCallableSignature.Create("operator.xor", ["a", "b"]);
        public static readonly LythonCallableSignature Concat = LythonCallableSignature.Create("operator.concat", ["a", "b"]);
        public static readonly LythonCallableSignature Eq = LythonCallableSignature.Create("operator.eq", ["a", "b"]);
        public static readonly LythonCallableSignature Ne = LythonCallableSignature.Create("operator.ne", ["a", "b"]);
        public static readonly LythonCallableSignature Lt = LythonCallableSignature.Create("operator.lt", ["a", "b"]);
        public static readonly LythonCallableSignature Le = LythonCallableSignature.Create("operator.le", ["a", "b"]);
        public static readonly LythonCallableSignature Gt = LythonCallableSignature.Create("operator.gt", ["a", "b"]);
        public static readonly LythonCallableSignature Ge = LythonCallableSignature.Create("operator.ge", ["a", "b"]);
        public static readonly LythonCallableSignature GetItem = LythonCallableSignature.Create("operator.getitem", ["obj", "key"]);
        public static readonly LythonCallableSignature SetItem = LythonCallableSignature.Create("operator.setitem", ["obj", "key", "value"]);
        public static readonly LythonCallableSignature DelItem = LythonCallableSignature.Create("operator.delitem", ["obj", "key"]);
        public static readonly LythonCallableSignature Contains = LythonCallableSignature.Create("operator.contains", ["obj", "value"]);
        public static readonly LythonCallableSignature LengthHint = LythonCallableSignature.Create("operator.length_hint", ["obj", "default"], requiredCount: 1);
        public static readonly LythonCallableSignature CountOf = LythonCallableSignature.Create("operator.countOf", ["a", "b"]);
        public static readonly LythonCallableSignature IndexOf = LythonCallableSignature.Create("operator.indexOf", ["a", "b"]);
        public static readonly LythonCallableSignature IAdd = LythonCallableSignature.Create("operator.iadd", ["a", "b"]);
        public static readonly LythonCallableSignature ISub = LythonCallableSignature.Create("operator.isub", ["a", "b"]);
        public static readonly LythonCallableSignature IMul = LythonCallableSignature.Create("operator.imul", ["a", "b"]);
        public static readonly LythonCallableSignature ITrueDiv = LythonCallableSignature.Create("operator.itruediv", ["a", "b"]);
        public static readonly LythonCallableSignature IFloorDiv = LythonCallableSignature.Create("operator.ifloordiv", ["a", "b"]);
        public static readonly LythonCallableSignature IMod = LythonCallableSignature.Create("operator.imod", ["a", "b"]);
        public static readonly LythonCallableSignature IPow = LythonCallableSignature.Create("operator.ipow", ["a", "b"]);
        public static readonly LythonCallableSignature ILShift = LythonCallableSignature.Create("operator.ilshift", ["a", "b"]);
        public static readonly LythonCallableSignature IRShift = LythonCallableSignature.Create("operator.irshift", ["a", "b"]);
        public static readonly LythonCallableSignature IAnd = LythonCallableSignature.Create("operator.iand", ["a", "b"]);
        public static readonly LythonCallableSignature IOr = LythonCallableSignature.Create("operator.ior", ["a", "b"]);
        public static readonly LythonCallableSignature IXor = LythonCallableSignature.Create("operator.ixor", ["a", "b"]);
        public static readonly LythonCallableSignature IConcat = LythonCallableSignature.Create("operator.iconcat", ["a", "b"]);
        public static readonly LythonCallableSignature Call = LythonCallableSignature.Create("operator.call", ["obj"], requiredCount: 1, variadicParameters: LythonVariadicParameters.Keywords | LythonVariadicParameters.Positional);
        public static readonly LythonCallableSignature ItemGetter = LythonCallableSignature.Create("operator.itemgetter", requiredCount: 1);
        public static readonly LythonCallableSignature AttrGetter = LythonCallableSignature.Create("operator.attrgetter", requiredCount: 1);
        public static readonly LythonCallableSignature MethodCaller = LythonCallableSignature.Create("operator.methodcaller", ["name"], requiredCount: 1, variadicParameters: LythonVariadicParameters.Keywords | LythonVariadicParameters.Positional);
    }

    internal static class Decimal
    {
        public static readonly LythonCallableSignature Create = LythonCallableSignature.Create("decimal.Decimal", ["value", "context"], requiredCount: 0);
        public static readonly LythonCallableSignature Tuple = LythonCallableSignature.Create("decimal.DecimalTuple", ["sign", "digits", "exponent"]);
        public static readonly LythonCallableSignature Context = LythonCallableSignature.Create("decimal.Context", ["prec", "rounding", "Emin", "Emax", "capitals", "clamp", "flags", "traps"], requiredCount: 0);
        public static readonly LythonCallableSignature GetContext = LythonCallableSignature.Create("decimal.getcontext", []);
        public static readonly LythonCallableSignature SetContext = LythonCallableSignature.Create("decimal.setcontext", ["context"]);
        public static readonly LythonCallableSignature LocalContext = LythonCallableSignature.Create("decimal.localcontext", ["context"], requiredCount: 0);
    }

    internal static class DateTime
    {
        public static readonly LythonCallableSignature Timedelta = LythonCallableSignature.Create("datetime.timedelta", ["days", "seconds", "microseconds", "milliseconds", "minutes", "hours", "weeks"], requiredCount: 0);
        public static readonly LythonCallableSignature Date = LythonCallableSignature.Create("datetime.date", ["year", "month", "day"]);
        public static readonly LythonCallableSignature DateToday = LythonCallableSignature.Create("datetime.date.today", []);
        public static readonly LythonCallableSignature DateFromTimestamp = LythonCallableSignature.Create("datetime.date.fromtimestamp", ["timestamp"]);
        public static readonly LythonCallableSignature DateFromOrdinal = LythonCallableSignature.Create("datetime.date.fromordinal", ["ordinal"]);
        public static readonly LythonCallableSignature DateFromIsoFormat = LythonCallableSignature.Create("datetime.date.fromisoformat", ["date_string"]);
        public static readonly LythonCallableSignature DateFromIsoCalendar = LythonCallableSignature.Create("datetime.date.fromisocalendar", ["year", "week", "day"]);
        public static readonly LythonCallableSignature Time = LythonCallableSignature.Create("datetime.time", ["hour", "minute", "second", "microsecond", "tzinfo", "fold"], requiredCount: 0);
        public static readonly LythonCallableSignature TimeFromIsoFormat = LythonCallableSignature.Create("datetime.time.fromisoformat", ["time_string"]);
        public static readonly LythonCallableSignature DateTimeDateTime = LythonCallableSignature.Create("datetime.datetime", ["year", "month", "day", "hour", "minute", "second", "microsecond", "tzinfo", "fold"], requiredCount: 3);
        public static readonly LythonCallableSignature DateTimeToday = LythonCallableSignature.Create("datetime.datetime.today", []);
        public static readonly LythonCallableSignature DateTimeNow = LythonCallableSignature.Create("datetime.datetime.now", ["tz"], requiredCount: 0);
        public static readonly LythonCallableSignature DateTimeUtcNow = LythonCallableSignature.Create("datetime.datetime.utcnow", []);
        public static readonly LythonCallableSignature DateTimeFromTimestamp = LythonCallableSignature.Create("datetime.datetime.fromtimestamp", ["timestamp", "tz"], requiredCount: 1);
        public static readonly LythonCallableSignature DateTimeUtcFromTimestamp = LythonCallableSignature.Create("datetime.datetime.utcfromtimestamp", ["timestamp"]);
        public static readonly LythonCallableSignature DateTimeFromOrdinal = LythonCallableSignature.Create("datetime.datetime.fromordinal", ["ordinal"]);
        public static readonly LythonCallableSignature DateTimeCombine = LythonCallableSignature.Create("datetime.datetime.combine", ["date", "time", "tzinfo"], requiredCount: 2);
        public static readonly LythonCallableSignature DateTimeFromIsoFormat = LythonCallableSignature.Create("datetime.datetime.fromisoformat", ["date_string"]);
        public static readonly LythonCallableSignature DateTimeFromIsoCalendar = LythonCallableSignature.Create("datetime.datetime.fromisocalendar", ["year", "week", "day"]);
        public static readonly LythonCallableSignature DateTimeStrptime = LythonCallableSignature.Create("datetime.datetime.strptime", ["date_string", "format"]);
        public static readonly LythonCallableSignature Timezone = LythonCallableSignature.Create("datetime.timezone", ["offset", "name"], requiredCount: 1);
        public static readonly LythonCallableSignature TzInfo = LythonCallableSignature.Create("datetime.tzinfo", []);
    }
}

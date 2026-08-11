namespace Lokad.Lython.Runtime;

internal static partial class LythonKnownCallableSignatures
{
    public static readonly LythonCallableSignature SysExit = LythonCallableSignature.Create("sys.exit", ["code"], requiredCount: 0);
    public static readonly LythonCallableSignature SysGetDefaultEncoding = LythonCallableSignature.Create("sys.getdefaultencoding", []);
    public static readonly LythonCallableSignature SysExcInfo = LythonCallableSignature.Create("sys.exc_info", []);
    public static readonly LythonCallableSignature SysGetSizeOf = LythonCallableSignature.Create("sys.getsizeof", ["object", "default"], requiredCount: 1);
    public static readonly LythonCallableSignature SysSetTrace = LythonCallableSignature.Create("sys.settrace", ["function"]);
    public static readonly LythonCallableSignature SysSetProfile = LythonCallableSignature.Create("sys.setprofile", ["function"]);
    public static readonly LythonCallableSignature SysSetRecursionLimit = LythonCallableSignature.Create("sys.setrecursionlimit", ["limit"]);
    public static readonly LythonCallableSignature SysAddAuditHook = LythonCallableSignature.Create("sys.addaudithook", ["hook"]);
    public static readonly LythonCallableSignature SysAudit = LythonCallableSignature.Create("sys.audit", ["event", "args"], requiredCount: 1, variadicParameters: LythonVariadicParameters.Keywords);

    public static readonly LythonCallableSignature PathlibPath = LythonCallableSignature.Create("pathlib.Path", requiredCount: 0);
    public static readonly LythonCallableSignature PathlibPurePath = LythonCallableSignature.Create("pathlib.PurePath", requiredCount: 0);
    public static readonly LythonCallableSignature PathlibPurePosixPath = LythonCallableSignature.Create("pathlib.PurePosixPath", requiredCount: 0);
    public static readonly LythonCallableSignature PathlibPosixPath = LythonCallableSignature.Create("pathlib.PosixPath", requiredCount: 0);
    public static readonly LythonCallableSignature PathlibPureWindowsPath = LythonCallableSignature.Create("pathlib.PureWindowsPath", requiredCount: 0);
    public static readonly LythonCallableSignature PathlibWindowsPath = LythonCallableSignature.Create("pathlib.WindowsPath", requiredCount: 0);

    public static readonly LythonCallableSignature JsonLoad = LythonCallableSignature.Create("json.load", ["fp", "cls", "object_hook", "parse_float", "parse_int", "parse_constant", "object_pairs_hook"], requiredCount: 1, maximumPositionalArgumentCount: 1);
    public static readonly LythonCallableSignature JsonLoads = LythonCallableSignature.Create("json.loads", ["s", "cls", "object_hook", "parse_float", "parse_int", "parse_constant", "object_pairs_hook"], requiredCount: 1, maximumPositionalArgumentCount: 1);
    public static readonly LythonCallableSignature JsonDump = LythonCallableSignature.Create("json.dump", ["obj", "fp", "skipkeys", "ensure_ascii", "check_circular", "allow_nan", "cls", "indent", "separators", "default", "sort_keys"], requiredCount: 2, maximumPositionalArgumentCount: 2);
    public static readonly LythonCallableSignature JsonDumps = LythonCallableSignature.Create("json.dumps", ["obj", "skipkeys", "ensure_ascii", "check_circular", "allow_nan", "cls", "indent", "separators", "default", "sort_keys"], requiredCount: 1, maximumPositionalArgumentCount: 1);

    public static readonly LythonCallableSignature CsvReader = LythonCallableSignature.Create("csv.reader", ["csvfile", "dialect", "delimiter", "quotechar", "quoting", "doublequote", "escapechar", "skipinitialspace", "lineterminator", "strict"], requiredCount: 1, maximumPositionalArgumentCount: 2);
    public static readonly LythonCallableSignature CsvWriter = LythonCallableSignature.Create("csv.writer", ["fileobj", "dialect", "delimiter", "quotechar", "quoting", "doublequote", "escapechar", "skipinitialspace", "lineterminator", "strict"], requiredCount: 0, maximumPositionalArgumentCount: 2);
    public static readonly LythonCallableSignature CsvDictReader = LythonCallableSignature.Create("csv.DictReader", ["f", "fieldnames", "restkey", "restval", "dialect", "delimiter", "quotechar", "quoting", "doublequote", "escapechar", "skipinitialspace", "lineterminator", "strict"], requiredCount: 1, maximumPositionalArgumentCount: 5);
    public static readonly LythonCallableSignature CsvDictWriter = LythonCallableSignature.Create("csv.DictWriter", ["f", "fieldnames", "restval", "extrasaction", "dialect", "delimiter", "quotechar", "quoting", "doublequote", "escapechar", "skipinitialspace", "lineterminator", "strict"], requiredCount: 2, maximumPositionalArgumentCount: 5);

    public static readonly LythonCallableSignature CollectionsDefaultDict = LythonCallableSignature.Create("collections.defaultdict", ["default_factory", "iterable"], requiredCount: 0, maximumPositionalArgumentCount: 2, variadicParameters: LythonVariadicParameters.Keywords);
    public static readonly LythonCallableSignature CollectionsCounter = LythonCallableSignature.Create("collections.Counter", ["iterable"], requiredCount: 0, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.Keywords);
    public static readonly LythonCallableSignature CollectionsDeque = LythonCallableSignature.Create("collections.deque", ["iterable", "maxlen"], requiredCount: 0);
    public static readonly LythonCallableSignature CollectionsNamedTuple = LythonCallableSignature.Create("collections.namedtuple", ["typename", "field_names", "rename", "defaults", "module"], requiredCount: 2, maximumPositionalArgumentCount: 2);
    public static readonly LythonCallableSignature CollectionsOrderedDict = LythonCallableSignature.Create("collections.OrderedDict", ["mapping"], requiredCount: 0, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.Keywords);
    public static readonly LythonCallableSignature CollectionsChainMap = LythonCallableSignature.Create("collections.ChainMap", requiredCount: 0);
    public static readonly LythonCallableSignature CollectionsUserDict = LythonCallableSignature.Create("collections.UserDict", requiredCount: 0);
    public static readonly LythonCallableSignature CollectionsUserList = LythonCallableSignature.Create("collections.UserList", requiredCount: 0);
    public static readonly LythonCallableSignature CollectionsUserString = LythonCallableSignature.Create("collections.UserString", requiredCount: 0);

    public static readonly LythonCallableSignature CopyCopy = LythonCallableSignature.Create("copy.copy", ["x"]);
    public static readonly LythonCallableSignature CopyDeepCopy = LythonCallableSignature.Create("copy.deepcopy", ["x", "memo"], requiredCount: 1);
    public static readonly LythonCallableSignature CopyReplace = LythonCallableSignature.Create("copy.replace", ["__object"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.Keywords);

    public static readonly LythonCallableSignature ShutilCopyFile = LythonCallableSignature.Create("shutil.copyfile", ["src", "dst", "follow_symlinks"], requiredCount: 2, maximumPositionalArgumentCount: 2);
    public static readonly LythonCallableSignature ShutilCopy = LythonCallableSignature.Create("shutil.copy", ["src", "dst", "follow_symlinks"], requiredCount: 2, maximumPositionalArgumentCount: 2);
    public static readonly LythonCallableSignature ShutilCopy2 = LythonCallableSignature.Create("shutil.copy2", ["src", "dst", "follow_symlinks"], requiredCount: 2, maximumPositionalArgumentCount: 2);
    public static readonly LythonCallableSignature ShutilMove = LythonCallableSignature.Create("shutil.move", ["src", "dst", "copy_function"], requiredCount: 2, maximumPositionalArgumentCount: 2);
    public static readonly LythonCallableSignature ShutilCopyFileObj = LythonCallableSignature.Create("shutil.copyfileobj", ["fsrc", "fdst", "length"], requiredCount: 2);

    public static readonly LythonCallableSignature FunctoolsUpdateWrapper = LythonCallableSignature.Create("functools.update_wrapper", ["wrapper", "wrapped", "assigned", "updated"], requiredCount: 2);
    public static readonly LythonCallableSignature FunctoolsWraps = LythonCallableSignature.Create("functools.wraps", ["wrapped", "assigned", "updated"], requiredCount: 1);
    public static readonly LythonCallableSignature FunctoolsTotalOrdering = LythonCallableSignature.Create("functools.total_ordering", ["cls"]);
    public static readonly LythonCallableSignature FunctoolsReduce = LythonCallableSignature.Create("functools.reduce", ["function", "iterable", "initializer"], requiredCount: 2);
    public static readonly LythonCallableSignature FunctoolsPartial = LythonCallableSignature.Create("functools.partial", ["func"], requiredCount: 1, variadicParameters: LythonVariadicParameters.Keywords | LythonVariadicParameters.Positional);
    public static readonly LythonCallableSignature FunctoolsPartialMethod = LythonCallableSignature.Create("functools.partialmethod", ["func"], requiredCount: 1, variadicParameters: LythonVariadicParameters.Keywords | LythonVariadicParameters.Positional);
    public static readonly LythonCallableSignature FunctoolsCmpToKey = LythonCallableSignature.Create("functools.cmp_to_key", ["mycmp"]);
    public static readonly LythonCallableSignature FunctoolsLruCache = LythonCallableSignature.Create("functools.lru_cache", ["maxsize", "typed"], requiredCount: 0);
    public static readonly LythonCallableSignature FunctoolsCache = LythonCallableSignature.Create("functools.cache", ["user_function"]);
    public static readonly LythonCallableSignature FunctoolsCachedProperty = LythonCallableSignature.Create("functools.cached_property", ["func"]);
    public static readonly LythonCallableSignature FunctoolsSingleDispatch = LythonCallableSignature.Create("functools.singledispatch", ["func"]);
    public static readonly LythonCallableSignature FunctoolsSingleDispatchMethod = LythonCallableSignature.Create("functools.singledispatchmethod", ["func"]);
    public static readonly LythonCallableSignature FunctoolsRecursiveRepr = LythonCallableSignature.Create("functools.recursive_repr", ["fillvalue"], requiredCount: 0);

    public static readonly LythonCallableSignature OperatorTruth = LythonCallableSignature.Create("operator.truth", ["obj"]);
    public static readonly LythonCallableSignature OperatorNot = LythonCallableSignature.Create("operator.not_", ["obj"]);
    public static readonly LythonCallableSignature OperatorIs = LythonCallableSignature.Create("operator.is_", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorIsNot = LythonCallableSignature.Create("operator.is_not", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorAbs = LythonCallableSignature.Create("operator.abs", ["obj"]);
    public static readonly LythonCallableSignature OperatorNeg = LythonCallableSignature.Create("operator.neg", ["obj"]);
    public static readonly LythonCallableSignature OperatorPos = LythonCallableSignature.Create("operator.pos", ["obj"]);
    public static readonly LythonCallableSignature OperatorInvert = LythonCallableSignature.Create("operator.invert", ["obj"]);
    public static readonly LythonCallableSignature OperatorIndex = LythonCallableSignature.Create("operator.index", ["obj"]);
    public static readonly LythonCallableSignature OperatorAdd = LythonCallableSignature.Create("operator.add", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorSub = LythonCallableSignature.Create("operator.sub", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorMul = LythonCallableSignature.Create("operator.mul", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorTrueDiv = LythonCallableSignature.Create("operator.truediv", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorFloorDiv = LythonCallableSignature.Create("operator.floordiv", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorMod = LythonCallableSignature.Create("operator.mod", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorPow = LythonCallableSignature.Create("operator.pow", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorMatMul = LythonCallableSignature.Create("operator.matmul", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorLShift = LythonCallableSignature.Create("operator.lshift", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorRShift = LythonCallableSignature.Create("operator.rshift", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorAnd = LythonCallableSignature.Create("operator.and_", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorOr = LythonCallableSignature.Create("operator.or_", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorXor = LythonCallableSignature.Create("operator.xor", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorConcat = LythonCallableSignature.Create("operator.concat", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorEq = LythonCallableSignature.Create("operator.eq", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorNe = LythonCallableSignature.Create("operator.ne", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorLt = LythonCallableSignature.Create("operator.lt", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorLe = LythonCallableSignature.Create("operator.le", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorGt = LythonCallableSignature.Create("operator.gt", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorGe = LythonCallableSignature.Create("operator.ge", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorGetItem = LythonCallableSignature.Create("operator.getitem", ["obj", "key"]);
    public static readonly LythonCallableSignature OperatorSetItem = LythonCallableSignature.Create("operator.setitem", ["obj", "key", "value"]);
    public static readonly LythonCallableSignature OperatorDelItem = LythonCallableSignature.Create("operator.delitem", ["obj", "key"]);
    public static readonly LythonCallableSignature OperatorContains = LythonCallableSignature.Create("operator.contains", ["obj", "value"]);
    public static readonly LythonCallableSignature OperatorLengthHint = LythonCallableSignature.Create("operator.length_hint", ["obj", "default"], requiredCount: 1);
    public static readonly LythonCallableSignature OperatorCountOf = LythonCallableSignature.Create("operator.countOf", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorIndexOf = LythonCallableSignature.Create("operator.indexOf", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorIAdd = LythonCallableSignature.Create("operator.iadd", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorISub = LythonCallableSignature.Create("operator.isub", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorIMul = LythonCallableSignature.Create("operator.imul", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorITrueDiv = LythonCallableSignature.Create("operator.itruediv", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorIFloorDiv = LythonCallableSignature.Create("operator.ifloordiv", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorIMod = LythonCallableSignature.Create("operator.imod", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorIPow = LythonCallableSignature.Create("operator.ipow", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorILShift = LythonCallableSignature.Create("operator.ilshift", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorIRShift = LythonCallableSignature.Create("operator.irshift", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorIAnd = LythonCallableSignature.Create("operator.iand", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorIOr = LythonCallableSignature.Create("operator.ior", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorIXor = LythonCallableSignature.Create("operator.ixor", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorIConcat = LythonCallableSignature.Create("operator.iconcat", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorCall = LythonCallableSignature.Create("operator.call", ["obj"], requiredCount: 1, variadicParameters: LythonVariadicParameters.Keywords | LythonVariadicParameters.Positional);
    public static readonly LythonCallableSignature OperatorItemGetter = LythonCallableSignature.Create("operator.itemgetter", requiredCount: 1);
    public static readonly LythonCallableSignature OperatorAttrGetter = LythonCallableSignature.Create("operator.attrgetter", requiredCount: 1);
    public static readonly LythonCallableSignature OperatorMethodCaller = LythonCallableSignature.Create("operator.methodcaller", ["name"], requiredCount: 1, variadicParameters: LythonVariadicParameters.Keywords | LythonVariadicParameters.Positional);

    public static readonly LythonCallableSignature Decimal = LythonCallableSignature.Create("decimal.Decimal", ["value", "context"], requiredCount: 0);
    public static readonly LythonCallableSignature DecimalTuple = LythonCallableSignature.Create("decimal.DecimalTuple", ["sign", "digits", "exponent"]);
    public static readonly LythonCallableSignature DecimalContext = LythonCallableSignature.Create("decimal.Context", ["prec", "rounding", "Emin", "Emax", "capitals", "clamp", "flags", "traps"], requiredCount: 0);
    public static readonly LythonCallableSignature DecimalGetContext = LythonCallableSignature.Create("decimal.getcontext", []);
    public static readonly LythonCallableSignature DecimalSetContext = LythonCallableSignature.Create("decimal.setcontext", ["context"]);
    public static readonly LythonCallableSignature DecimalLocalContext = LythonCallableSignature.Create("decimal.localcontext", ["context"], requiredCount: 0);

    public static readonly LythonCallableSignature DateTimeTimedelta = LythonCallableSignature.Create("datetime.timedelta", ["days", "seconds", "microseconds", "milliseconds", "minutes", "hours", "weeks"], requiredCount: 0);
    public static readonly LythonCallableSignature DateTimeDate = LythonCallableSignature.Create("datetime.date", ["year", "month", "day"]);
    public static readonly LythonCallableSignature DateTimeDateToday = LythonCallableSignature.Create("datetime.date.today", []);
    public static readonly LythonCallableSignature DateTimeDateFromTimestamp = LythonCallableSignature.Create("datetime.date.fromtimestamp", ["timestamp"]);
    public static readonly LythonCallableSignature DateTimeDateFromOrdinal = LythonCallableSignature.Create("datetime.date.fromordinal", ["ordinal"]);
    public static readonly LythonCallableSignature DateTimeDateFromIsoFormat = LythonCallableSignature.Create("datetime.date.fromisoformat", ["date_string"]);
    public static readonly LythonCallableSignature DateTimeDateFromIsoCalendar = LythonCallableSignature.Create("datetime.date.fromisocalendar", ["year", "week", "day"]);
    public static readonly LythonCallableSignature DateTimeTime = LythonCallableSignature.Create("datetime.time", ["hour", "minute", "second", "microsecond", "tzinfo", "fold"], requiredCount: 0);
    public static readonly LythonCallableSignature DateTimeTimeFromIsoFormat = LythonCallableSignature.Create("datetime.time.fromisoformat", ["time_string"]);
    public static readonly LythonCallableSignature DateTimeDateTime = LythonCallableSignature.Create("datetime.datetime", ["year", "month", "day", "hour", "minute", "second", "microsecond", "tzinfo", "fold"], requiredCount: 3);
    public static readonly LythonCallableSignature DateTimeDateTimeToday = LythonCallableSignature.Create("datetime.datetime.today", []);
    public static readonly LythonCallableSignature DateTimeDateTimeNow = LythonCallableSignature.Create("datetime.datetime.now", ["tz"], requiredCount: 0);
    public static readonly LythonCallableSignature DateTimeDateTimeUtcNow = LythonCallableSignature.Create("datetime.datetime.utcnow", []);
    public static readonly LythonCallableSignature DateTimeDateTimeFromTimestamp = LythonCallableSignature.Create("datetime.datetime.fromtimestamp", ["timestamp", "tz"], requiredCount: 1);
    public static readonly LythonCallableSignature DateTimeDateTimeUtcFromTimestamp = LythonCallableSignature.Create("datetime.datetime.utcfromtimestamp", ["timestamp"]);
    public static readonly LythonCallableSignature DateTimeDateTimeFromOrdinal = LythonCallableSignature.Create("datetime.datetime.fromordinal", ["ordinal"]);
    public static readonly LythonCallableSignature DateTimeDateTimeCombine = LythonCallableSignature.Create("datetime.datetime.combine", ["date", "time", "tzinfo"], requiredCount: 2);
    public static readonly LythonCallableSignature DateTimeDateTimeFromIsoFormat = LythonCallableSignature.Create("datetime.datetime.fromisoformat", ["date_string"]);
    public static readonly LythonCallableSignature DateTimeDateTimeFromIsoCalendar = LythonCallableSignature.Create("datetime.datetime.fromisocalendar", ["year", "week", "day"]);
    public static readonly LythonCallableSignature DateTimeDateTimeStrptime = LythonCallableSignature.Create("datetime.datetime.strptime", ["date_string", "format"]);
    public static readonly LythonCallableSignature DateTimeTimezone = LythonCallableSignature.Create("datetime.timezone", ["offset", "name"], requiredCount: 1);
    public static readonly LythonCallableSignature DateTimeTzInfo = LythonCallableSignature.Create("datetime.tzinfo", []);

}

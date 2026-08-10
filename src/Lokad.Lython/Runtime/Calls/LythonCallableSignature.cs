using System.Collections.Concurrent;

namespace Lokad.Lython.Runtime;

[Flags]
internal enum LythonVariadicParameters
{
    None = 0,
    Keywords = 1,
    Positional = 2,
}

internal sealed class LythonCallableSignature
{
    private static readonly ConcurrentDictionary<string, SignatureBucket> SignaturesByName = new(StringComparer.Ordinal);

    private LythonCallableSignature(
        string name,
        string[]? parameterNames,
        int? requiredCount,
        int? maxPositionalCount,
        LythonVariadicParameters variadicParameters,
        int positionalOnlyCount)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("A callable signature requires a name.", nameof(name));
        }

        if (requiredCount < 0 ||
            maxPositionalCount < 0 ||
            positionalOnlyCount < 0 ||
            (parameterNames is not null && requiredCount > parameterNames.Length) ||
            (parameterNames is not null && maxPositionalCount > parameterNames.Length) ||
            positionalOnlyCount > (parameterNames?.Length ?? 0))
        {
            throw new ArgumentOutOfRangeException(nameof(requiredCount), $"Callable signature '{name}' has inconsistent parameter bounds.");
        }

        Name = name;
        ParameterNames = parameterNames is null ? null : [.. parameterNames];
        RequiredCount = requiredCount;
        MaxPositionalCount = maxPositionalCount;
        VariadicParameters = variadicParameters;
        PositionalOnlyCount = positionalOnlyCount;
        ParameterIndices = ParameterNames is null ? null : CreateParameterIndices(ParameterNames, name);
    }

    public string Name { get; }

    public string[]? ParameterNames { get; }

    public int? RequiredCount { get; }

    public int? MaxPositionalCount { get; }

    public LythonVariadicParameters VariadicParameters { get; }

    public int PositionalOnlyCount { get; }

    public IReadOnlyDictionary<string, int>? ParameterIndices { get; }

    public static LythonCallableSignature Create(string Name)
        => Create(Name, null, null, null, LythonVariadicParameters.None, 0);

    public static LythonCallableSignature Create(string Name, string[]? ParameterNames)
        => Create(Name, ParameterNames, null, null, LythonVariadicParameters.None, 0);

    public static LythonCallableSignature Create(string Name, int? RequiredCount)
        => Create(Name, null, RequiredCount, null, LythonVariadicParameters.None, 0);

    public static LythonCallableSignature Create(string Name, string[]? ParameterNames, int? RequiredCount)
        => Create(Name, ParameterNames, RequiredCount, null, LythonVariadicParameters.None, 0);

    public static LythonCallableSignature Create(string Name, string[]? ParameterNames, int? RequiredCount, int? MaxPositionalCount)
        => Create(Name, ParameterNames, RequiredCount, MaxPositionalCount, LythonVariadicParameters.None, 0);

    public static LythonCallableSignature Create(string Name, string[]? ParameterNames, int? RequiredCount, LythonVariadicParameters VariadicParameters)
        => Create(Name, ParameterNames, RequiredCount, null, VariadicParameters, 0);

    public static LythonCallableSignature Create(string Name, string[]? ParameterNames, int? RequiredCount, int? MaxPositionalCount, LythonVariadicParameters VariadicParameters)
        => Create(Name, ParameterNames, RequiredCount, MaxPositionalCount, VariadicParameters, 0);

    public static LythonCallableSignature Create(string Name, int? RequiredCount, LythonVariadicParameters VariadicParameters)
        => Create(Name, null, RequiredCount, null, VariadicParameters, 0);

    public static LythonCallableSignature Create(
        string Name,
        string[]? ParameterNames,
        int? RequiredCount,
        int? MaxPositionalCount,
        LythonVariadicParameters VariadicParameters,
        int PositionalOnlyCount)
    {
        var bucket = SignaturesByName.GetOrAdd(Name, static _ => new SignatureBucket());
        lock (bucket.Signatures)
        {
            foreach (var signature in bucket.Signatures)
            {
                if (signature.HasShape(ParameterNames, RequiredCount, MaxPositionalCount, VariadicParameters, PositionalOnlyCount))
                {
                    return signature;
                }
            }

            var created = new LythonCallableSignature(
                Name,
                ParameterNames,
                RequiredCount,
                MaxPositionalCount,
                VariadicParameters,
                PositionalOnlyCount);
            bucket.Signatures.Add(created);
            return created;
        }
    }

    public int MinimumArgumentCount => RequiredCount ?? ParameterNames?.Length ?? 0;

    public bool AllowsExtraKeywords => (VariadicParameters & LythonVariadicParameters.Keywords) != 0;

    public bool AllowsExtraPositional => (VariadicParameters & LythonVariadicParameters.Positional) != 0;

    public int? MaximumArgumentCount => AllowsExtraKeywords || AllowsExtraPositional
        ? null
        : ParameterNames?.Length;

    private static IReadOnlyDictionary<string, int> CreateParameterIndices(string[] parameterNames, string callableName)
    {
        var indices = new Dictionary<string, int>(parameterNames.Length, StringComparer.Ordinal);
        for (var index = 0; index < parameterNames.Length; index++)
        {
            if (!indices.TryAdd(parameterNames[index], index))
            {
                throw new ArgumentException($"Callable signature '{callableName}' contains duplicate parameter '{parameterNames[index]}'.", nameof(parameterNames));
            }
        }

        return indices;
    }

    private bool HasShape(
        string[]? parameterNames,
        int? requiredCount,
        int? maxPositionalCount,
        LythonVariadicParameters variadicParameters,
        int positionalOnlyCount)
        => RequiredCount == requiredCount &&
            MaxPositionalCount == maxPositionalCount &&
            VariadicParameters == variadicParameters &&
            PositionalOnlyCount == positionalOnlyCount &&
            ((ParameterNames is null && parameterNames is null) ||
             (ParameterNames is not null && parameterNames is not null && ParameterNames.SequenceEqual(parameterNames, StringComparer.Ordinal)));

    private sealed class SignatureBucket
    {
        public List<LythonCallableSignature> Signatures { get; } = [];
    }
}

internal static class LythonKnownCallableSignatures
{
    public static readonly LythonCallableSignature SysExit = LythonCallableSignature.Create("sys.exit", ["code"], RequiredCount: 0);
    public static readonly LythonCallableSignature SysGetDefaultEncoding = LythonCallableSignature.Create("sys.getdefaultencoding", []);
    public static readonly LythonCallableSignature SysExcInfo = LythonCallableSignature.Create("sys.exc_info", []);
    public static readonly LythonCallableSignature SysGetSizeOf = LythonCallableSignature.Create("sys.getsizeof", ["object", "default"], RequiredCount: 1);
    public static readonly LythonCallableSignature SysSetTrace = LythonCallableSignature.Create("sys.settrace", ["function"]);
    public static readonly LythonCallableSignature SysSetProfile = LythonCallableSignature.Create("sys.setprofile", ["function"]);
    public static readonly LythonCallableSignature SysSetRecursionLimit = LythonCallableSignature.Create("sys.setrecursionlimit", ["limit"]);
    public static readonly LythonCallableSignature SysAddAuditHook = LythonCallableSignature.Create("sys.addaudithook", ["hook"]);
    public static readonly LythonCallableSignature SysAudit = LythonCallableSignature.Create("sys.audit", ["event", "args"], RequiredCount: 1, VariadicParameters: LythonVariadicParameters.Keywords);

    public static readonly LythonCallableSignature PathlibPath = LythonCallableSignature.Create("pathlib.Path", RequiredCount: 0);
    public static readonly LythonCallableSignature PathlibPurePath = LythonCallableSignature.Create("pathlib.PurePath", RequiredCount: 0);
    public static readonly LythonCallableSignature PathlibPurePosixPath = LythonCallableSignature.Create("pathlib.PurePosixPath", RequiredCount: 0);
    public static readonly LythonCallableSignature PathlibPosixPath = LythonCallableSignature.Create("pathlib.PosixPath", RequiredCount: 0);
    public static readonly LythonCallableSignature PathlibPureWindowsPath = LythonCallableSignature.Create("pathlib.PureWindowsPath", RequiredCount: 0);
    public static readonly LythonCallableSignature PathlibWindowsPath = LythonCallableSignature.Create("pathlib.WindowsPath", RequiredCount: 0);

    public static readonly LythonCallableSignature JsonLoad = LythonCallableSignature.Create("json.load", ["fp", "cls", "object_hook", "parse_float", "parse_int", "parse_constant", "object_pairs_hook"], RequiredCount: 1, MaxPositionalCount: 1);
    public static readonly LythonCallableSignature JsonLoads = LythonCallableSignature.Create("json.loads", ["s", "cls", "object_hook", "parse_float", "parse_int", "parse_constant", "object_pairs_hook"], RequiredCount: 1, MaxPositionalCount: 1);
    public static readonly LythonCallableSignature JsonDump = LythonCallableSignature.Create("json.dump", ["obj", "fp", "skipkeys", "ensure_ascii", "check_circular", "allow_nan", "cls", "indent", "separators", "default", "sort_keys"], RequiredCount: 2, MaxPositionalCount: 2);
    public static readonly LythonCallableSignature JsonDumps = LythonCallableSignature.Create("json.dumps", ["obj", "skipkeys", "ensure_ascii", "check_circular", "allow_nan", "cls", "indent", "separators", "default", "sort_keys"], RequiredCount: 1, MaxPositionalCount: 1);

    public static readonly LythonCallableSignature CsvReader = LythonCallableSignature.Create("csv.reader", ["csvfile", "dialect", "delimiter", "quotechar", "quoting", "doublequote", "escapechar", "skipinitialspace", "lineterminator", "strict"], RequiredCount: 1, MaxPositionalCount: 2);
    public static readonly LythonCallableSignature CsvWriter = LythonCallableSignature.Create("csv.writer", ["fileobj", "dialect", "delimiter", "quotechar", "quoting", "doublequote", "escapechar", "skipinitialspace", "lineterminator", "strict"], RequiredCount: 0, MaxPositionalCount: 2);
    public static readonly LythonCallableSignature CsvDictReader = LythonCallableSignature.Create("csv.DictReader", ["f", "fieldnames", "restkey", "restval", "dialect", "delimiter", "quotechar", "quoting", "doublequote", "escapechar", "skipinitialspace", "lineterminator", "strict"], RequiredCount: 1, MaxPositionalCount: 5);
    public static readonly LythonCallableSignature CsvDictWriter = LythonCallableSignature.Create("csv.DictWriter", ["f", "fieldnames", "restval", "extrasaction", "dialect", "delimiter", "quotechar", "quoting", "doublequote", "escapechar", "skipinitialspace", "lineterminator", "strict"], RequiredCount: 2, MaxPositionalCount: 5);

    public static readonly LythonCallableSignature CollectionsDefaultDict = LythonCallableSignature.Create("collections.defaultdict", ["default_factory", "iterable"], RequiredCount: 0, MaxPositionalCount: 2, VariadicParameters: LythonVariadicParameters.Keywords);
    public static readonly LythonCallableSignature CollectionsCounter = LythonCallableSignature.Create("collections.Counter", ["iterable"], RequiredCount: 0, MaxPositionalCount: 1, VariadicParameters: LythonVariadicParameters.Keywords);
    public static readonly LythonCallableSignature CollectionsDeque = LythonCallableSignature.Create("collections.deque", ["iterable", "maxlen"], RequiredCount: 0);
    public static readonly LythonCallableSignature CollectionsNamedTuple = LythonCallableSignature.Create("collections.namedtuple", ["typename", "field_names", "rename", "defaults", "module"], RequiredCount: 2, MaxPositionalCount: 2);
    public static readonly LythonCallableSignature CollectionsOrderedDict = LythonCallableSignature.Create("collections.OrderedDict", ["mapping"], RequiredCount: 0, MaxPositionalCount: 1, VariadicParameters: LythonVariadicParameters.Keywords);
    public static readonly LythonCallableSignature CollectionsChainMap = LythonCallableSignature.Create("collections.ChainMap", RequiredCount: 0);
    public static readonly LythonCallableSignature CollectionsUserDict = LythonCallableSignature.Create("collections.UserDict", RequiredCount: 0);
    public static readonly LythonCallableSignature CollectionsUserList = LythonCallableSignature.Create("collections.UserList", RequiredCount: 0);
    public static readonly LythonCallableSignature CollectionsUserString = LythonCallableSignature.Create("collections.UserString", RequiredCount: 0);

    public static readonly LythonCallableSignature CopyCopy = LythonCallableSignature.Create("copy.copy", ["x"]);
    public static readonly LythonCallableSignature CopyDeepCopy = LythonCallableSignature.Create("copy.deepcopy", ["x", "memo"], RequiredCount: 1);
    public static readonly LythonCallableSignature CopyReplace = LythonCallableSignature.Create("copy.replace", ["__object"], RequiredCount: 1, MaxPositionalCount: 1, VariadicParameters: LythonVariadicParameters.Keywords);

    public static readonly LythonCallableSignature ShutilCopyFile = LythonCallableSignature.Create("shutil.copyfile", ["src", "dst", "follow_symlinks"], RequiredCount: 2, MaxPositionalCount: 2);
    public static readonly LythonCallableSignature ShutilCopy = LythonCallableSignature.Create("shutil.copy", ["src", "dst", "follow_symlinks"], RequiredCount: 2, MaxPositionalCount: 2);
    public static readonly LythonCallableSignature ShutilCopy2 = LythonCallableSignature.Create("shutil.copy2", ["src", "dst", "follow_symlinks"], RequiredCount: 2, MaxPositionalCount: 2);
    public static readonly LythonCallableSignature ShutilMove = LythonCallableSignature.Create("shutil.move", ["src", "dst", "copy_function"], RequiredCount: 2, MaxPositionalCount: 2);
    public static readonly LythonCallableSignature ShutilCopyFileObj = LythonCallableSignature.Create("shutil.copyfileobj", ["fsrc", "fdst", "length"], RequiredCount: 2);

    public static readonly LythonCallableSignature FunctoolsUpdateWrapper = LythonCallableSignature.Create("functools.update_wrapper", ["wrapper", "wrapped", "assigned", "updated"], RequiredCount: 2);
    public static readonly LythonCallableSignature FunctoolsWraps = LythonCallableSignature.Create("functools.wraps", ["wrapped", "assigned", "updated"], RequiredCount: 1);
    public static readonly LythonCallableSignature FunctoolsTotalOrdering = LythonCallableSignature.Create("functools.total_ordering", ["cls"]);
    public static readonly LythonCallableSignature FunctoolsReduce = LythonCallableSignature.Create("functools.reduce", ["function", "iterable", "initializer"], RequiredCount: 2);
    public static readonly LythonCallableSignature FunctoolsPartial = LythonCallableSignature.Create("functools.partial", ["func"], RequiredCount: 1, VariadicParameters: LythonVariadicParameters.Keywords | LythonVariadicParameters.Positional);
    public static readonly LythonCallableSignature FunctoolsPartialMethod = LythonCallableSignature.Create("functools.partialmethod", ["func"], RequiredCount: 1, VariadicParameters: LythonVariadicParameters.Keywords | LythonVariadicParameters.Positional);
    public static readonly LythonCallableSignature FunctoolsCmpToKey = LythonCallableSignature.Create("functools.cmp_to_key", ["mycmp"]);
    public static readonly LythonCallableSignature FunctoolsLruCache = LythonCallableSignature.Create("functools.lru_cache", ["maxsize", "typed"], RequiredCount: 0);
    public static readonly LythonCallableSignature FunctoolsCache = LythonCallableSignature.Create("functools.cache", ["user_function"]);
    public static readonly LythonCallableSignature FunctoolsCachedProperty = LythonCallableSignature.Create("functools.cached_property", ["func"]);
    public static readonly LythonCallableSignature FunctoolsSingleDispatch = LythonCallableSignature.Create("functools.singledispatch", ["func"]);
    public static readonly LythonCallableSignature FunctoolsSingleDispatchMethod = LythonCallableSignature.Create("functools.singledispatchmethod", ["func"]);
    public static readonly LythonCallableSignature FunctoolsRecursiveRepr = LythonCallableSignature.Create("functools.recursive_repr", ["fillvalue"], RequiredCount: 0);

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
    public static readonly LythonCallableSignature OperatorLengthHint = LythonCallableSignature.Create("operator.length_hint", ["obj", "default"], RequiredCount: 1);
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
    public static readonly LythonCallableSignature OperatorCall = LythonCallableSignature.Create("operator.call", ["obj"], RequiredCount: 1, VariadicParameters: LythonVariadicParameters.Keywords | LythonVariadicParameters.Positional);
    public static readonly LythonCallableSignature OperatorItemGetter = LythonCallableSignature.Create("operator.itemgetter", RequiredCount: 1);
    public static readonly LythonCallableSignature OperatorAttrGetter = LythonCallableSignature.Create("operator.attrgetter", RequiredCount: 1);
    public static readonly LythonCallableSignature OperatorMethodCaller = LythonCallableSignature.Create("operator.methodcaller", ["name"], RequiredCount: 1, VariadicParameters: LythonVariadicParameters.Keywords | LythonVariadicParameters.Positional);

    public static readonly LythonCallableSignature Decimal = LythonCallableSignature.Create("decimal.Decimal", ["value", "context"], RequiredCount: 0);
    public static readonly LythonCallableSignature DecimalTuple = LythonCallableSignature.Create("decimal.DecimalTuple", ["sign", "digits", "exponent"]);
    public static readonly LythonCallableSignature DecimalContext = LythonCallableSignature.Create("decimal.Context", ["prec", "rounding", "Emin", "Emax", "capitals", "clamp", "flags", "traps"], RequiredCount: 0);
    public static readonly LythonCallableSignature DecimalGetContext = LythonCallableSignature.Create("decimal.getcontext", []);
    public static readonly LythonCallableSignature DecimalSetContext = LythonCallableSignature.Create("decimal.setcontext", ["context"]);
    public static readonly LythonCallableSignature DecimalLocalContext = LythonCallableSignature.Create("decimal.localcontext", ["context"], RequiredCount: 0);

    public static readonly LythonCallableSignature DateTimeTimedelta = LythonCallableSignature.Create("datetime.timedelta", ["days", "seconds", "microseconds", "milliseconds", "minutes", "hours", "weeks"], RequiredCount: 0);
    public static readonly LythonCallableSignature DateTimeDate = LythonCallableSignature.Create("datetime.date", ["year", "month", "day"]);
    public static readonly LythonCallableSignature DateTimeDateToday = LythonCallableSignature.Create("datetime.date.today", []);
    public static readonly LythonCallableSignature DateTimeDateFromTimestamp = LythonCallableSignature.Create("datetime.date.fromtimestamp", ["timestamp"]);
    public static readonly LythonCallableSignature DateTimeDateFromOrdinal = LythonCallableSignature.Create("datetime.date.fromordinal", ["ordinal"]);
    public static readonly LythonCallableSignature DateTimeDateFromIsoFormat = LythonCallableSignature.Create("datetime.date.fromisoformat", ["date_string"]);
    public static readonly LythonCallableSignature DateTimeDateFromIsoCalendar = LythonCallableSignature.Create("datetime.date.fromisocalendar", ["year", "week", "day"]);
    public static readonly LythonCallableSignature DateTimeTime = LythonCallableSignature.Create("datetime.time", ["hour", "minute", "second", "microsecond", "tzinfo", "fold"], RequiredCount: 0);
    public static readonly LythonCallableSignature DateTimeTimeFromIsoFormat = LythonCallableSignature.Create("datetime.time.fromisoformat", ["time_string"]);
    public static readonly LythonCallableSignature DateTimeDateTime = LythonCallableSignature.Create("datetime.datetime", ["year", "month", "day", "hour", "minute", "second", "microsecond", "tzinfo", "fold"], RequiredCount: 3);
    public static readonly LythonCallableSignature DateTimeDateTimeToday = LythonCallableSignature.Create("datetime.datetime.today", []);
    public static readonly LythonCallableSignature DateTimeDateTimeNow = LythonCallableSignature.Create("datetime.datetime.now", ["tz"], RequiredCount: 0);
    public static readonly LythonCallableSignature DateTimeDateTimeUtcNow = LythonCallableSignature.Create("datetime.datetime.utcnow", []);
    public static readonly LythonCallableSignature DateTimeDateTimeFromTimestamp = LythonCallableSignature.Create("datetime.datetime.fromtimestamp", ["timestamp", "tz"], RequiredCount: 1);
    public static readonly LythonCallableSignature DateTimeDateTimeUtcFromTimestamp = LythonCallableSignature.Create("datetime.datetime.utcfromtimestamp", ["timestamp"]);
    public static readonly LythonCallableSignature DateTimeDateTimeFromOrdinal = LythonCallableSignature.Create("datetime.datetime.fromordinal", ["ordinal"]);
    public static readonly LythonCallableSignature DateTimeDateTimeCombine = LythonCallableSignature.Create("datetime.datetime.combine", ["date", "time", "tzinfo"], RequiredCount: 2);
    public static readonly LythonCallableSignature DateTimeDateTimeFromIsoFormat = LythonCallableSignature.Create("datetime.datetime.fromisoformat", ["date_string"]);
    public static readonly LythonCallableSignature DateTimeDateTimeFromIsoCalendar = LythonCallableSignature.Create("datetime.datetime.fromisocalendar", ["year", "week", "day"]);
    public static readonly LythonCallableSignature DateTimeDateTimeStrptime = LythonCallableSignature.Create("datetime.datetime.strptime", ["date_string", "format"]);
    public static readonly LythonCallableSignature DateTimeTimezone = LythonCallableSignature.Create("datetime.timezone", ["offset", "name"], RequiredCount: 1);
    public static readonly LythonCallableSignature DateTimeTzInfo = LythonCallableSignature.Create("datetime.tzinfo", []);

    public static readonly LythonCallableSignature StatisticsMean = LythonCallableSignature.Create("statistics.mean", ["data"]);
    public static readonly LythonCallableSignature StatisticsFMean = LythonCallableSignature.Create("statistics.fmean", ["data", "weights"], RequiredCount: 1);
    public static readonly LythonCallableSignature StatisticsGeometricMean = LythonCallableSignature.Create("statistics.geometric_mean", ["data"]);
    public static readonly LythonCallableSignature StatisticsHarmonicMean = LythonCallableSignature.Create("statistics.harmonic_mean", ["data", "weights"], RequiredCount: 1);
    public static readonly LythonCallableSignature StatisticsMedian = LythonCallableSignature.Create("statistics.median", ["data"]);
    public static readonly LythonCallableSignature StatisticsMedianLow = LythonCallableSignature.Create("statistics.median_low", ["data"]);
    public static readonly LythonCallableSignature StatisticsMedianHigh = LythonCallableSignature.Create("statistics.median_high", ["data"]);
    public static readonly LythonCallableSignature StatisticsMedianGrouped = LythonCallableSignature.Create("statistics.median_grouped", ["data", "interval"], RequiredCount: 1);
    public static readonly LythonCallableSignature StatisticsMode = LythonCallableSignature.Create("statistics.mode", ["data"]);
    public static readonly LythonCallableSignature StatisticsMultiMode = LythonCallableSignature.Create("statistics.multimode", ["data"]);
    public static readonly LythonCallableSignature StatisticsPStdev = LythonCallableSignature.Create("statistics.pstdev", ["data", "mu"], RequiredCount: 1);
    public static readonly LythonCallableSignature StatisticsStdev = LythonCallableSignature.Create("statistics.stdev", ["data", "xbar"], RequiredCount: 1);
    public static readonly LythonCallableSignature StatisticsPVariance = LythonCallableSignature.Create("statistics.pvariance", ["data", "mu"], RequiredCount: 1);
    public static readonly LythonCallableSignature StatisticsVariance = LythonCallableSignature.Create("statistics.variance", ["data", "xbar"], RequiredCount: 1);
    public static readonly LythonCallableSignature StatisticsQuantiles = LythonCallableSignature.Create("statistics.quantiles", ["data", "n", "method"], RequiredCount: 1, MaxPositionalCount: 1);
    public static readonly LythonCallableSignature StatisticsCovariance = LythonCallableSignature.Create("statistics.covariance", ["x", "y"]);
    public static readonly LythonCallableSignature StatisticsCorrelation = LythonCallableSignature.Create("statistics.correlation", ["x", "y"]);
    public static readonly LythonCallableSignature StatisticsLinearRegression = LythonCallableSignature.Create("statistics.linear_regression", ["x", "y", "proportional"], RequiredCount: 2, MaxPositionalCount: 2);
    public static readonly LythonCallableSignature StatisticsLinearRegressionResult = LythonCallableSignature.Create("statistics.LinearRegression", ["slope", "intercept"]);
    public static readonly LythonCallableSignature StatisticsNormalDist = LythonCallableSignature.Create("statistics.NormalDist", ["mu", "sigma"], RequiredCount: 0);
    public static readonly LythonCallableSignature StatisticsNormalDistFromSamples = LythonCallableSignature.Create("statistics.NormalDist.from_samples", ["data"]);
    public static readonly LythonCallableSignature StatisticsKde = LythonCallableSignature.Create("statistics.kde", RequiredCount: 0);
    public static readonly LythonCallableSignature StatisticsKdeRandom = LythonCallableSignature.Create("statistics.kde_random", RequiredCount: 0);

    public static readonly LythonCallableSignature RandomClass = LythonCallableSignature.Create("random.Random", ["a"], RequiredCount: 0);
    public static readonly LythonCallableSignature RandomSystemRandom = LythonCallableSignature.Create("random.SystemRandom", ["a"], RequiredCount: 0);
    public static readonly LythonCallableSignature RandomSeed = LythonCallableSignature.Create("random.seed", ["a", "version"], RequiredCount: 0);
    public static readonly LythonCallableSignature RandomRandom = LythonCallableSignature.Create("random.random", []);
    public static readonly LythonCallableSignature RandomGetState = LythonCallableSignature.Create("random.getstate", []);
    public static readonly LythonCallableSignature RandomSetState = LythonCallableSignature.Create("random.setstate", ["state"]);
    public static readonly LythonCallableSignature RandomRandRange = LythonCallableSignature.Create("random.randrange", ["start", "stop", "step"], RequiredCount: 0);
    public static readonly LythonCallableSignature RandomRandInt = LythonCallableSignature.Create("random.randint", ["a", "b"]);
    public static readonly LythonCallableSignature RandomChoice = LythonCallableSignature.Create("random.choice", ["seq"]);
    public static readonly LythonCallableSignature RandomChoices = LythonCallableSignature.Create("random.choices", ["population", "weights", "cum_weights", "k"], RequiredCount: 1);
    public static readonly LythonCallableSignature RandomShuffle = LythonCallableSignature.Create("random.shuffle", ["x"]);
    public static readonly LythonCallableSignature RandomSample = LythonCallableSignature.Create("random.sample", ["population", "k", "counts"], RequiredCount: 2, MaxPositionalCount: 2);
    public static readonly LythonCallableSignature RandomGetRandBits = LythonCallableSignature.Create("random.getrandbits", ["k"]);
    public static readonly LythonCallableSignature RandomRandBytes = LythonCallableSignature.Create("random.randbytes", ["n"]);
    public static readonly LythonCallableSignature RandomUniform = LythonCallableSignature.Create("random.uniform", ["a", "b"]);
    public static readonly LythonCallableSignature RandomTriangular = LythonCallableSignature.Create("random.triangular", ["low", "high", "mode"], RequiredCount: 0);
    public static readonly LythonCallableSignature RandomBetaVariate = LythonCallableSignature.Create("random.betavariate", ["alpha", "beta"]);
    public static readonly LythonCallableSignature RandomExpVariate = LythonCallableSignature.Create("random.expovariate", ["lambd"], RequiredCount: 0);
    public static readonly LythonCallableSignature RandomGammaVariate = LythonCallableSignature.Create("random.gammavariate", ["alpha", "beta"]);
    public static readonly LythonCallableSignature RandomGauss = LythonCallableSignature.Create("random.gauss", ["mu", "sigma"], RequiredCount: 0);
    public static readonly LythonCallableSignature RandomNormalVariate = LythonCallableSignature.Create("random.normalvariate", ["mu", "sigma"], RequiredCount: 0);
    public static readonly LythonCallableSignature RandomLogNormVariate = LythonCallableSignature.Create("random.lognormvariate", ["mu", "sigma"]);
    public static readonly LythonCallableSignature RandomParetoVariate = LythonCallableSignature.Create("random.paretovariate", ["alpha"]);
    public static readonly LythonCallableSignature RandomVonMisesVariate = LythonCallableSignature.Create("random.vonmisesvariate", ["mu", "kappa"]);
    public static readonly LythonCallableSignature RandomWeibullVariate = LythonCallableSignature.Create("random.weibullvariate", ["alpha", "beta"]);

    public static readonly LythonCallableSignature MathSqrt = LythonCallableSignature.Create("math.sqrt", ["x"]);
    public static readonly LythonCallableSignature MathExp = LythonCallableSignature.Create("math.exp", ["x"]);
    public static readonly LythonCallableSignature MathLog = LythonCallableSignature.Create("math.log", ["x", "base"], RequiredCount: 1);
    public static readonly LythonCallableSignature MathLog10 = LythonCallableSignature.Create("math.log10", ["x"]);
    public static readonly LythonCallableSignature MathLog2 = LythonCallableSignature.Create("math.log2", ["x"]);
    public static readonly LythonCallableSignature MathSin = LythonCallableSignature.Create("math.sin", ["x"]);
    public static readonly LythonCallableSignature MathCos = LythonCallableSignature.Create("math.cos", ["x"]);
    public static readonly LythonCallableSignature MathTan = LythonCallableSignature.Create("math.tan", ["x"]);
    public static readonly LythonCallableSignature MathAsin = LythonCallableSignature.Create("math.asin", ["x"]);
    public static readonly LythonCallableSignature MathAcos = LythonCallableSignature.Create("math.acos", ["x"]);
    public static readonly LythonCallableSignature MathAtan = LythonCallableSignature.Create("math.atan", ["x"]);
    public static readonly LythonCallableSignature MathAtan2 = LythonCallableSignature.Create("math.atan2", ["y", "x"]);
    public static readonly LythonCallableSignature MathSinh = LythonCallableSignature.Create("math.sinh", ["x"]);
    public static readonly LythonCallableSignature MathCosh = LythonCallableSignature.Create("math.cosh", ["x"]);
    public static readonly LythonCallableSignature MathTanh = LythonCallableSignature.Create("math.tanh", ["x"]);
    public static readonly LythonCallableSignature MathFloor = LythonCallableSignature.Create("math.floor", ["x"]);
    public static readonly LythonCallableSignature MathCeil = LythonCallableSignature.Create("math.ceil", ["x"]);
    public static readonly LythonCallableSignature MathFabs = LythonCallableSignature.Create("math.fabs", ["x"]);
    public static readonly LythonCallableSignature MathTrunc = LythonCallableSignature.Create("math.trunc", ["x"]);
    public static readonly LythonCallableSignature MathDegrees = LythonCallableSignature.Create("math.degrees", ["x"]);
    public static readonly LythonCallableSignature MathRadians = LythonCallableSignature.Create("math.radians", ["x"]);
    public static readonly LythonCallableSignature MathIsFinite = LythonCallableSignature.Create("math.isfinite", ["x"]);
    public static readonly LythonCallableSignature MathIsInf = LythonCallableSignature.Create("math.isinf", ["x"]);
    public static readonly LythonCallableSignature MathIsNaN = LythonCallableSignature.Create("math.isnan", ["x"]);
    public static readonly LythonCallableSignature MathPow = LythonCallableSignature.Create("math.pow", ["x", "y"]);
    public static readonly LythonCallableSignature MathHypot = LythonCallableSignature.Create("math.hypot", RequiredCount: 0);
    public static readonly LythonCallableSignature MathFmod = LythonCallableSignature.Create("math.fmod", ["x", "y"]);
    public static readonly LythonCallableSignature MathCopySign = LythonCallableSignature.Create("math.copysign", ["x", "y"]);
    public static readonly LythonCallableSignature MathIsClose = LythonCallableSignature.Create("math.isclose", ["a", "b", "rel_tol", "abs_tol"], RequiredCount: 2);
    public static readonly LythonCallableSignature MathProd = LythonCallableSignature.Create("math.prod", ["iterable", "start"], RequiredCount: 1, MaxPositionalCount: 1);
    public static readonly LythonCallableSignature MathFsum = LythonCallableSignature.Create("math.fsum", ["iterable"]);
    public static readonly LythonCallableSignature MathFactorial = LythonCallableSignature.Create("math.factorial", ["n"]);
    public static readonly LythonCallableSignature MathGcd = LythonCallableSignature.Create("math.gcd", RequiredCount: 0);
    public static readonly LythonCallableSignature MathLcm = LythonCallableSignature.Create("math.lcm", RequiredCount: 0);
    public static readonly LythonCallableSignature MathComb = LythonCallableSignature.Create("math.comb", ["n", "k"]);
    public static readonly LythonCallableSignature MathPerm = LythonCallableSignature.Create("math.perm", ["n", "k"], RequiredCount: 1);
    public static readonly LythonCallableSignature MathIsqrt = LythonCallableSignature.Create("math.isqrt", ["n"]);
    public static readonly LythonCallableSignature MathDist = LythonCallableSignature.Create("math.dist", ["p", "q"]);
    public static readonly LythonCallableSignature MathFrexp = LythonCallableSignature.Create("math.frexp", ["x"]);
    public static readonly LythonCallableSignature MathLdexp = LythonCallableSignature.Create("math.ldexp", ["x", "i"]);
    public static readonly LythonCallableSignature MathModf = LythonCallableSignature.Create("math.modf", ["x"]);
    public static readonly LythonCallableSignature MathRemainder = LythonCallableSignature.Create("math.remainder", ["x", "y"]);
    public static readonly LythonCallableSignature MathNextAfter = LythonCallableSignature.Create("math.nextafter", ["x", "y", "steps"], RequiredCount: 2, MaxPositionalCount: 2);
    public static readonly LythonCallableSignature MathUlp = LythonCallableSignature.Create("math.ulp", ["x"]);
    public static readonly LythonCallableSignature MathExp2 = LythonCallableSignature.Create("math.exp2", ["x"]);
    public static readonly LythonCallableSignature MathExpm1 = LythonCallableSignature.Create("math.expm1", ["x"]);
    public static readonly LythonCallableSignature MathLog1p = LythonCallableSignature.Create("math.log1p", ["x"]);
    public static readonly LythonCallableSignature MathCbrt = LythonCallableSignature.Create("math.cbrt", ["x"]);
    public static readonly LythonCallableSignature MathErf = LythonCallableSignature.Create("math.erf", ["x"]);
    public static readonly LythonCallableSignature MathErfc = LythonCallableSignature.Create("math.erfc", ["x"]);
    public static readonly LythonCallableSignature MathGamma = LythonCallableSignature.Create("math.gamma", ["x"]);
    public static readonly LythonCallableSignature MathLgamma = LythonCallableSignature.Create("math.lgamma", ["x"]);
    public static readonly LythonCallableSignature MathFma = LythonCallableSignature.Create("math.fma", ["x", "y", "z"], RequiredCount: null, MaxPositionalCount: 3, VariadicParameters: LythonVariadicParameters.None, PositionalOnlyCount: 0);
    public static readonly LythonCallableSignature MathSumProd = LythonCallableSignature.Create("math.sumprod", ["p", "q"], RequiredCount: null, MaxPositionalCount: 2, VariadicParameters: LythonVariadicParameters.None, PositionalOnlyCount: 0);

    public static readonly LythonCallableSignature OpenPyxlWorkbook = LythonCallableSignature.Create("openpyxl.Workbook", ["write_only", "iso_dates"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlLoadWorkbook = LythonCallableSignature.Create("openpyxl.load_workbook", ["filename", "read_only", "keep_vba", "data_only", "keep_links", "rich_text"], RequiredCount: 1);
    public static readonly LythonCallableSignature OpenPyxlGetColumnLetter = LythonCallableSignature.Create("openpyxl.utils.get_column_letter", ["col_idx"]);
    public static readonly LythonCallableSignature OpenPyxlColumnIndexFromString = LythonCallableSignature.Create("openpyxl.utils.column_index_from_string", ["col"]);
    public static readonly LythonCallableSignature OpenPyxlCoordinateFromString = LythonCallableSignature.Create("openpyxl.utils.coordinate_from_string", ["coord_string"]);
    public static readonly LythonCallableSignature OpenPyxlCoordinateToTuple = LythonCallableSignature.Create("openpyxl.utils.coordinate_to_tuple", ["coordinate"]);
    public static readonly LythonCallableSignature OpenPyxlRangeBoundaries = LythonCallableSignature.Create("openpyxl.utils.range_boundaries", ["range_string"]);
    public static readonly LythonCallableSignature OpenPyxlGetColumnInterval = LythonCallableSignature.Create("openpyxl.utils.get_column_interval", ["start", "end"]);
    public static readonly LythonCallableSignature OpenPyxlAbsoluteCoordinate = LythonCallableSignature.Create("openpyxl.utils.absolute_coordinate", ["coord_string"]);
    public static readonly LythonCallableSignature OpenPyxlQuoteSheetName = LythonCallableSignature.Create("openpyxl.utils.quote_sheetname", ["sheetname"]);
    public static readonly LythonCallableSignature OpenPyxlRowsFromRange = LythonCallableSignature.Create("openpyxl.utils.rows_from_range", ["range_string"]);
    public static readonly LythonCallableSignature OpenPyxlColsFromRange = LythonCallableSignature.Create("openpyxl.utils.cols_from_range", ["range_string"]);
    public static readonly LythonCallableSignature OpenPyxlComment = LythonCallableSignature.Create("openpyxl.comments.Comment", ["text", "author"]);
    public static readonly LythonCallableSignature OpenPyxlFont = LythonCallableSignature.Create("openpyxl.styles.Font", ["name", "sz", "bold", "italic", "color", "underline", "b", "i", "size", "u", "strike", "strikethrough"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlPatternFill = LythonCallableSignature.Create("openpyxl.styles.PatternFill", ["fill_type", "start_color", "end_color", "fgColor", "bgColor", "patternType"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlBorder = LythonCallableSignature.Create("openpyxl.styles.Border", ["left", "right", "top", "bottom"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlSide = LythonCallableSignature.Create("openpyxl.styles.Side", ["style", "color", "border_style"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlAlignment = LythonCallableSignature.Create("openpyxl.styles.Alignment", ["horizontal", "vertical", "wrap_text", "text_rotation", "wrapText", "textRotation", "shrinkToFit", "shrink_to_fit"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlProtection = LythonCallableSignature.Create("openpyxl.styles.Protection", ["locked", "hidden"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlNamedStyle = LythonCallableSignature.Create("openpyxl.styles.NamedStyle", ["name", "font", "fill", "border", "alignment", "number_format", "protection"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlColor = LythonCallableSignature.Create("openpyxl.styles.colors.Color", ["rgb", "indexed", "auto", "theme", "tint", "type"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlTable = LythonCallableSignature.Create("openpyxl.worksheet.table.Table", ["displayName", "ref"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlTableStyleInfo = LythonCallableSignature.Create("openpyxl.worksheet.table.TableStyleInfo", ["name", "showFirstColumn", "showLastColumn", "showRowStripes", "showColumnStripes"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlDataValidation = LythonCallableSignature.Create("openpyxl.worksheet.datavalidation.DataValidation", ["type", "formula1", "formula2", "allow_blank", "showErrorMessage", "showInputMessage", "operator", "errorTitle", "error", "promptTitle", "prompt"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlBarChart = LythonCallableSignature.Create("openpyxl.chart.BarChart", []);
    public static readonly LythonCallableSignature OpenPyxlLineChart = LythonCallableSignature.Create("openpyxl.chart.LineChart", []);
    public static readonly LythonCallableSignature OpenPyxlPieChart = LythonCallableSignature.Create("openpyxl.chart.PieChart", []);
    public static readonly LythonCallableSignature OpenPyxlScatterChart = LythonCallableSignature.Create("openpyxl.chart.ScatterChart", []);
    public static readonly LythonCallableSignature OpenPyxlChartReference = LythonCallableSignature.Create("openpyxl.chart.Reference", ["worksheet", "min_col", "min_row", "max_col", "max_row", "range_string"], RequiredCount: 1);
    public static readonly LythonCallableSignature OpenPyxlChartSeries = LythonCallableSignature.Create("openpyxl.chart.Series", ["values", "xvalues", "title"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlDrawingImage = LythonCallableSignature.Create("openpyxl.drawing.image.Image", ["img"]);

    public static readonly LythonCallableSignature ReCompile = LythonCallableSignature.Create("re.compile", ["pattern", "flags"], RequiredCount: 1);
    public static readonly LythonCallableSignature ReSearch = LythonCallableSignature.Create("re.search", ["pattern", "string", "flags", "pos", "endpos"], RequiredCount: 2);
    public static readonly LythonCallableSignature ReMatch = LythonCallableSignature.Create("re.match", ["pattern", "string", "flags", "pos", "endpos"], RequiredCount: 2);
    public static readonly LythonCallableSignature ReFullMatch = LythonCallableSignature.Create("re.fullmatch", ["pattern", "string", "flags", "pos", "endpos"], RequiredCount: 2);
    public static readonly LythonCallableSignature ReFindAll = LythonCallableSignature.Create("re.findall", ["pattern", "string", "flags", "pos", "endpos"], RequiredCount: 2);
    public static readonly LythonCallableSignature ReFindIter = LythonCallableSignature.Create("re.finditer", ["pattern", "string", "flags", "pos", "endpos"], RequiredCount: 2);
    public static readonly LythonCallableSignature ReSub = LythonCallableSignature.Create("re.sub", ["pattern", "repl", "string", "count", "flags", "pos", "endpos"], RequiredCount: 3);
    public static readonly LythonCallableSignature ReSubn = LythonCallableSignature.Create("re.subn", ["pattern", "repl", "string", "count", "flags", "pos", "endpos"], RequiredCount: 3);
    public static readonly LythonCallableSignature ReSplit = LythonCallableSignature.Create("re.split", ["pattern", "string", "maxsplit", "flags", "pos", "endpos"], RequiredCount: 2);
    public static readonly LythonCallableSignature ReEscape = LythonCallableSignature.Create("re.escape", ["string"]);
    public static readonly LythonCallableSignature RePurge = LythonCallableSignature.Create("re.purge", []);

    public static readonly LythonCallableSignature ArgparseArgumentParser = LythonCallableSignature.Create(
        "argparse.ArgumentParser",
        [
            "prog",
            "usage",
            "description",
            "epilog",
            "formatter_class",
            "add_help",
            "allow_abbrev",
            "exit_on_error",
            "fromfile_prefix_chars",
            "parents",
            "conflict_handler",
            "prefix_chars",
            "argument_default",
        ],
        RequiredCount: 0,
        MaxPositionalCount: 8);
    public static readonly LythonCallableSignature ArgparseNamespace = LythonCallableSignature.Create("argparse.Namespace", [], RequiredCount: 0, MaxPositionalCount: 0, VariadicParameters: LythonVariadicParameters.Keywords);
    public static readonly LythonCallableSignature ArgparseFileType = LythonCallableSignature.Create("argparse.FileType", ["mode", "bufsize", "encoding", "errors"], RequiredCount: 0);

    public static readonly LythonCallableSignature DataclassesDataclass = LythonCallableSignature.Create("dataclasses.dataclass", ["cls", "init", "repr", "eq", "order", "unsafe_hash", "frozen", "match_args", "kw_only", "slots", "weakref_slot"], RequiredCount: 0, MaxPositionalCount: 1);
    public static readonly LythonCallableSignature DataclassesField = LythonCallableSignature.Create("dataclasses.field", ["default", "default_factory", "init", "repr", "hash", "compare", "metadata", "kw_only"], RequiredCount: 0, MaxPositionalCount: 0);
    public static readonly LythonCallableSignature DataclassesMakeDataclass = LythonCallableSignature.Create("dataclasses.make_dataclass", ["cls_name", "fields", "bases", "namespace", "init", "repr", "eq", "order", "unsafe_hash", "frozen", "match_args", "kw_only", "slots", "weakref_slot"], RequiredCount: 2, MaxPositionalCount: 2);
    public static readonly LythonCallableSignature DataclassesIsDataclass = LythonCallableSignature.Create("dataclasses.is_dataclass", ["value"]);
    public static readonly LythonCallableSignature DataclassesFields = LythonCallableSignature.Create("dataclasses.fields", ["class_or_instance"]);
    public static readonly LythonCallableSignature DataclassesAsDict = LythonCallableSignature.Create("dataclasses.asdict", ["obj", "dict_factory"], RequiredCount: 1);
    public static readonly LythonCallableSignature DataclassesAsTuple = LythonCallableSignature.Create("dataclasses.astuple", ["obj", "tuple_factory"], RequiredCount: 1);
    public static readonly LythonCallableSignature DataclassesReplace = LythonCallableSignature.Create("dataclasses.replace", ["obj"], RequiredCount: 1, MaxPositionalCount: 1, VariadicParameters: LythonVariadicParameters.Keywords);

    public static readonly LythonCallableSignature TypingTypeVar = LythonCallableSignature.Create("typing.TypeVar", ["name"], RequiredCount: 1, VariadicParameters: LythonVariadicParameters.Keywords);
    public static readonly LythonCallableSignature TypingNewType = LythonCallableSignature.Create("typing.NewType", ["name", "tp"]);
    public static readonly LythonCallableSignature TypingCast = LythonCallableSignature.Create("typing.cast", ["typ", "val"]);
    public static readonly LythonCallableSignature TypingGetOrigin = LythonCallableSignature.Create("typing.get_origin", ["tp"]);
    public static readonly LythonCallableSignature TypingGetArgs = LythonCallableSignature.Create("typing.get_args", ["tp"]);
    public static readonly LythonCallableSignature TypingNamedTuple = LythonCallableSignature.Create("typing.NamedTuple", ["typename", "fields"], RequiredCount: 1, VariadicParameters: LythonVariadicParameters.Keywords);
    public static readonly LythonCallableSignature TypingTypedDict = LythonCallableSignature.Create("typing.TypedDict", ["typename", "fields"], RequiredCount: 1, VariadicParameters: LythonVariadicParameters.Keywords);

    private static readonly string[] SubprocessParameters = ["args", "input", "cwd", "timeout", "check", "capture_output", "stdin", "stdout", "stderr", "shell", "text", "encoding", "errors", "env", "universal_newlines"];
    private static readonly string[] SubprocessPopenParameters = ["args", "bufsize", "executable", "stdin", "stdout", "stderr", "preexec_fn", "close_fds", "shell", "cwd", "env", "universal_newlines", "startupinfo", "creationflags", "restore_signals", "start_new_session", "pass_fds", "user", "group", "extra_groups", "encoding", "errors", "text", "umask", "pipesize", "process_group"];

    public static readonly LythonCallableSignature SubprocessRun = LythonCallableSignature.Create("subprocess.run", SubprocessParameters, RequiredCount: 1, MaxPositionalCount: 6);
    public static readonly LythonCallableSignature SubprocessCall = LythonCallableSignature.Create("subprocess.call", SubprocessParameters, RequiredCount: 1, MaxPositionalCount: 6);
    public static readonly LythonCallableSignature SubprocessCheckCall = LythonCallableSignature.Create("subprocess.check_call", SubprocessParameters, RequiredCount: 1, MaxPositionalCount: 6);
    public static readonly LythonCallableSignature SubprocessCheckOutput = LythonCallableSignature.Create("subprocess.check_output", SubprocessParameters, RequiredCount: 1, MaxPositionalCount: 6);
    public static readonly LythonCallableSignature SubprocessPopen = LythonCallableSignature.Create("subprocess.Popen", SubprocessPopenParameters, RequiredCount: 1, MaxPositionalCount: 17);
    public static readonly LythonCallableSignature SubprocessCompletedProcess = LythonCallableSignature.Create("subprocess.CompletedProcess", ["args", "returncode", "stdout", "stderr"], RequiredCount: 2);
    public static readonly LythonCallableSignature SubprocessTimeoutExpired = LythonCallableSignature.Create("subprocess.TimeoutExpired", ["cmd", "timeout", "output", "stderr"], RequiredCount: 2);
    public static readonly LythonCallableSignature SubprocessList2Cmdline = LythonCallableSignature.Create("subprocess.list2cmdline", ["seq"]);
    public static readonly LythonCallableSignature SubprocessUnsupported = LythonCallableSignature.Create("subprocess.unsupported", RequiredCount: 0, VariadicParameters: LythonVariadicParameters.Keywords | LythonVariadicParameters.Positional);

    public static readonly LythonCallableSignature OsListDir = LythonCallableSignature.Create("os.listdir", ["path"], RequiredCount: 0);
    public static readonly LythonCallableSignature OsWalk = LythonCallableSignature.Create("os.walk", ["top", "topdown", "onerror", "followlinks"], RequiredCount: 0);
    public static readonly LythonCallableSignature OsGetCwd = LythonCallableSignature.Create("os.getcwd", []);
    public static readonly LythonCallableSignature OsFspath = LythonCallableSignature.Create("os.fspath", ["path"]);
    public static readonly LythonCallableSignature OsFsEncode = LythonCallableSignature.Create("os.fsencode", ["filename"]);
    public static readonly LythonCallableSignature OsFsDecode = LythonCallableSignature.Create("os.fsdecode", ["filename"]);
    public static readonly LythonCallableSignature OsGetEnv = LythonCallableSignature.Create("os.getenv", ["key", "default"], RequiredCount: 1);
    public static readonly LythonCallableSignature OsPutEnv = LythonCallableSignature.Create("os.putenv", ["key", "value"]);
    public static readonly LythonCallableSignature OsUnsetEnv = LythonCallableSignature.Create("os.unsetenv", ["key"]);
    public static readonly LythonCallableSignature OsGetExecPath = LythonCallableSignature.Create("os.get_exec_path", ["env"], RequiredCount: 0);
    public static readonly LythonCallableSignature OsStat = LythonCallableSignature.Create("os.stat", ["path"]);
    public static readonly LythonCallableSignature OsLstat = LythonCallableSignature.Create("os.lstat", ["path"]);
    public static readonly LythonCallableSignature OsScandir = LythonCallableSignature.Create("os.scandir", ["path"], RequiredCount: 0);
    public static readonly LythonCallableSignature OsMkdir = LythonCallableSignature.Create("os.mkdir", ["path"]);
    public static readonly LythonCallableSignature OsMakedirs = LythonCallableSignature.Create("os.makedirs", ["path", "exist_ok"], RequiredCount: 1);
    public static readonly LythonCallableSignature OsRemove = LythonCallableSignature.Create("os.remove", ["path"]);
    public static readonly LythonCallableSignature OsUnlink = LythonCallableSignature.Create("os.unlink", ["path"]);
    public static readonly LythonCallableSignature OsRename = LythonCallableSignature.Create("os.rename", ["src", "dst"]);
    public static readonly LythonCallableSignature OsReplace = LythonCallableSignature.Create("os.replace", ["src", "dst"]);
    public static readonly LythonCallableSignature OsRmdir = LythonCallableSignature.Create("os.rmdir", ["path"]);
    public static readonly LythonCallableSignature OsRemovedirs = LythonCallableSignature.Create("os.removedirs", ["path"]);

    public static readonly LythonCallableSignature OsPathJoin = LythonCallableSignature.Create("os.path.join", RequiredCount: 1);
    public static readonly LythonCallableSignature OsPathSplit = LythonCallableSignature.Create("os.path.split", ["path"]);
    public static readonly LythonCallableSignature OsPathSplitExt = LythonCallableSignature.Create("os.path.splitext", ["path"]);
    public static readonly LythonCallableSignature OsPathBasename = LythonCallableSignature.Create("os.path.basename", ["path"]);
    public static readonly LythonCallableSignature OsPathDirname = LythonCallableSignature.Create("os.path.dirname", ["path"]);
    public static readonly LythonCallableSignature OsPathIsAbs = LythonCallableSignature.Create("os.path.isabs", ["path"]);
    public static readonly LythonCallableSignature OsPathNormPath = LythonCallableSignature.Create("os.path.normpath", ["path"]);
    public static readonly LythonCallableSignature OsPathNormCase = LythonCallableSignature.Create("os.path.normcase", ["path"]);
    public static readonly LythonCallableSignature OsPathAbsPath = LythonCallableSignature.Create("os.path.abspath", ["path"]);
    public static readonly LythonCallableSignature OsPathRelPath = LythonCallableSignature.Create("os.path.relpath", ["path", "start"], RequiredCount: 1);
    public static readonly LythonCallableSignature OsPathCommonPath = LythonCallableSignature.Create("os.path.commonpath", ["paths"]);
    public static readonly LythonCallableSignature OsPathCommonPrefix = LythonCallableSignature.Create("os.path.commonprefix", ["list"]);
    public static readonly LythonCallableSignature OsPathExists = LythonCallableSignature.Create("os.path.exists", ["path"]);
    public static readonly LythonCallableSignature OsPathLexists = LythonCallableSignature.Create("os.path.lexists", ["path"]);
    public static readonly LythonCallableSignature OsPathIsFile = LythonCallableSignature.Create("os.path.isfile", ["path"]);
    public static readonly LythonCallableSignature OsPathIsDir = LythonCallableSignature.Create("os.path.isdir", ["path"]);
    public static readonly LythonCallableSignature OsPathGetSize = LythonCallableSignature.Create("os.path.getsize", ["path"]);
    public static readonly LythonCallableSignature OsPathGetMTime = LythonCallableSignature.Create("os.path.getmtime", ["path"]);
    public static readonly LythonCallableSignature OsPathGetATime = LythonCallableSignature.Create("os.path.getatime", ["path"]);
    public static readonly LythonCallableSignature OsPathGetCTime = LythonCallableSignature.Create("os.path.getctime", ["path"]);
    public static readonly LythonCallableSignature OsPathSameFile = LythonCallableSignature.Create("os.path.samefile", ["path1", "path2"]);
    public static readonly LythonCallableSignature OsPathRealPath = LythonCallableSignature.Create("os.path.realpath", ["path"]);
    public static readonly LythonCallableSignature OsPathExpandVars = LythonCallableSignature.Create("os.path.expandvars", ["path"]);
    public static readonly LythonCallableSignature OsPathSplitDrive = LythonCallableSignature.Create("os.path.splitdrive", ["path"]);
    public static readonly LythonCallableSignature OsPathSplitRoot = LythonCallableSignature.Create("os.path.splitroot", ["path"]);
    public static readonly LythonCallableSignature OsPathIsMount = LythonCallableSignature.Create("os.path.ismount", ["path"]);

    public static readonly LythonCallableSignature Glob = LythonCallableSignature.Create("glob.glob", ["pathname", "root_dir", "dir_fd", "recursive", "include_hidden"], RequiredCount: 1, MaxPositionalCount: 1);
    public static readonly LythonCallableSignature IGlob = LythonCallableSignature.Create("glob.iglob", ["pathname", "root_dir", "dir_fd", "recursive", "include_hidden"], RequiredCount: 1, MaxPositionalCount: 1);
    public static readonly LythonCallableSignature GlobEscape = LythonCallableSignature.Create("glob.escape", ["pathname"]);
    public static readonly LythonCallableSignature GlobHasMagic = LythonCallableSignature.Create("glob.has_magic", ["s"]);
    public static readonly LythonCallableSignature GlobTranslate = LythonCallableSignature.Create("glob.translate", ["pathname", "recursive", "include_hidden", "seps"], RequiredCount: 1, MaxPositionalCount: 1);
    public static readonly LythonCallableSignature Glob0 = LythonCallableSignature.Create("glob.glob0", ["dirname", "basename", "dir_fd", "dironly", "include_hidden"], RequiredCount: 2);
    public static readonly LythonCallableSignature Glob1 = LythonCallableSignature.Create("glob.glob1", ["dirname", "pattern"], RequiredCount: 2);

    public static readonly LythonCallableSignature FnMatch = LythonCallableSignature.Create("fnmatch.fnmatch", ["name", "pattern"]);
    public static readonly LythonCallableSignature FnMatchCase = LythonCallableSignature.Create("fnmatch.fnmatchcase", ["name", "pattern"]);
    public static readonly LythonCallableSignature FnMatchFilter = LythonCallableSignature.Create("fnmatch.filter", ["names", "pattern"]);
    public static readonly LythonCallableSignature FnMatchTranslate = LythonCallableSignature.Create("fnmatch.translate", ["pattern"]);

    public static readonly LythonCallableSignature DifflibIsLineJunk = LythonCallableSignature.Create("difflib.IS_LINE_JUNK", ["line"]);
    public static readonly LythonCallableSignature DifflibIsCharacterJunk = LythonCallableSignature.Create("difflib.IS_CHARACTER_JUNK", ["ch"]);
    public static readonly LythonCallableSignature DifflibUnifiedDiff = LythonCallableSignature.Create("difflib.unified_diff", ["a", "b", "fromfile", "tofile", "fromfiledate", "tofiledate", "n", "lineterm"], RequiredCount: 2);
    public static readonly LythonCallableSignature DifflibContextDiff = LythonCallableSignature.Create("difflib.context_diff", ["a", "b", "fromfile", "tofile", "fromfiledate", "tofiledate", "n", "lineterm"], RequiredCount: 2);
    public static readonly LythonCallableSignature DifflibNdiff = LythonCallableSignature.Create("difflib.ndiff", ["a", "b", "linejunk", "charjunk"], RequiredCount: 2);
    public static readonly LythonCallableSignature DifflibRestore = LythonCallableSignature.Create("difflib.restore", ["delta", "which"]);
    public static readonly LythonCallableSignature DifflibGetCloseMatches = LythonCallableSignature.Create("difflib.get_close_matches", ["word", "possibilities", "n", "cutoff"], RequiredCount: 2);
    public static readonly LythonCallableSignature DifflibDiffBytes = LythonCallableSignature.Create("difflib.diff_bytes", ["dfunc", "a", "b", "fromfile", "tofile", "fromfiledate", "tofiledate", "n", "lineterm"], RequiredCount: 3);
    public static readonly LythonCallableSignature DifflibDiffer = LythonCallableSignature.Create("difflib.Differ", ["linejunk", "charjunk"], RequiredCount: 0);
    public static readonly LythonCallableSignature DifflibHtmlDiff = LythonCallableSignature.Create("difflib.HtmlDiff", ["tabsize", "wrapcolumn", "linejunk", "charjunk"], RequiredCount: 0);
    public static readonly LythonCallableSignature DifflibSequenceMatcher = LythonCallableSignature.Create("difflib.SequenceMatcher", ["isjunk", "a", "b", "autojunk"], RequiredCount: 0);

    public static readonly LythonCallableSignature PkgutilModuleInfo = LythonCallableSignature.Create("pkgutil.ModuleInfo", ["module_finder", "name", "ispkg"]);
    public static readonly LythonCallableSignature PkgutilIterModules = LythonCallableSignature.Create("pkgutil.iter_modules", ["path", "prefix"], RequiredCount: 0);
    public static readonly LythonCallableSignature PkgutilWalkPackages = LythonCallableSignature.Create("pkgutil.walk_packages", ["path", "prefix", "onerror"], RequiredCount: 0);
    public static readonly LythonCallableSignature PkgutilFindLoader = LythonCallableSignature.Create("pkgutil.find_loader", ["fullname"]);
    public static readonly LythonCallableSignature PkgutilGetLoader = LythonCallableSignature.Create("pkgutil.get_loader", ["module_or_name"]);
    public static readonly LythonCallableSignature PkgutilExtendPath = LythonCallableSignature.Create("pkgutil.extend_path", ["path", "name"]);
    public static readonly LythonCallableSignature PkgutilResolveName = LythonCallableSignature.Create("pkgutil.resolve_name", ["name"]);
    public static readonly LythonCallableSignature PkgutilGetImporter = LythonCallableSignature.Create("pkgutil.get_importer", ["path_item"]);
    public static readonly LythonCallableSignature PkgutilIterImporters = LythonCallableSignature.Create("pkgutil.iter_importers", ["fullname"], RequiredCount: 0);
    public static readonly LythonCallableSignature PkgutilIterImporterModules = LythonCallableSignature.Create("pkgutil.iter_importer_modules", ["importer", "prefix"], RequiredCount: 1);
    public static readonly LythonCallableSignature PkgutilIterZipimportModules = LythonCallableSignature.Create("pkgutil.iter_zipimport_modules", ["importer", "prefix"], RequiredCount: 1);
    public static readonly LythonCallableSignature PkgutilGetData = LythonCallableSignature.Create("pkgutil.get_data", ["package", "resource"]);
    public static readonly LythonCallableSignature PkgutilReadCode = LythonCallableSignature.Create("pkgutil.read_code", ["stream"]);

    public static readonly LythonCallableSignature ImportlibImportModule = LythonCallableSignature.Create("importlib.import_module", ["name", "package"], RequiredCount: 1);
    public static readonly LythonCallableSignature ImportlibInvalidateCaches = LythonCallableSignature.Create("importlib.invalidate_caches", []);
    public static readonly LythonCallableSignature ImportlibUtilFindSpec = LythonCallableSignature.Create("importlib.util.find_spec", ["name", "package"], RequiredCount: 1);
    public static readonly LythonCallableSignature ImportlibUtilResolveName = LythonCallableSignature.Create("importlib.util.resolve_name", ["name", "package"]);
    public static readonly LythonCallableSignature ImportlibUtilModuleFromSpec = LythonCallableSignature.Create("importlib.util.module_from_spec", ["spec"]);
    public static readonly LythonCallableSignature ImportlibUtilSpecFromFileLocation = LythonCallableSignature.Create("importlib.util.spec_from_file_location", ["name", "location", "loader", "submodule_search_locations"], RequiredCount: 2, MaxPositionalCount: 2);
    public static readonly LythonCallableSignature ImportlibUtilSpecFromLoader = LythonCallableSignature.Create("importlib.util.spec_from_loader", ["name", "loader", "origin", "is_package"], RequiredCount: 2, MaxPositionalCount: 2);

    public static readonly LythonCallableSignature FilecmpCmp = LythonCallableSignature.Create("filecmp.cmp", ["f1", "f2", "shallow"], RequiredCount: 2);
    public static readonly LythonCallableSignature FilecmpClearCache = LythonCallableSignature.Create("filecmp.clear_cache", []);
    public static readonly LythonCallableSignature FilecmpDircmp = LythonCallableSignature.Create("filecmp.dircmp", ["a", "b", "ignore", "hide"], RequiredCount: 2);

    public static readonly LythonCallableSignature HashlibMd5 = LythonCallableSignature.Create("hashlib.md5", ["string", "usedforsecurity"], RequiredCount: 0, MaxPositionalCount: 1);
    public static readonly LythonCallableSignature HashlibSha1 = LythonCallableSignature.Create("hashlib.sha1", ["string", "usedforsecurity"], RequiredCount: 0, MaxPositionalCount: 1);
    public static readonly LythonCallableSignature HashlibSha256 = LythonCallableSignature.Create("hashlib.sha256", ["string", "usedforsecurity"], RequiredCount: 0, MaxPositionalCount: 1);
    public static readonly LythonCallableSignature HashlibSha384 = LythonCallableSignature.Create("hashlib.sha384", ["string", "usedforsecurity"], RequiredCount: 0, MaxPositionalCount: 1);
    public static readonly LythonCallableSignature HashlibSha512 = LythonCallableSignature.Create("hashlib.sha512", ["string", "usedforsecurity"], RequiredCount: 0, MaxPositionalCount: 1);
    public static readonly LythonCallableSignature HashlibNew = LythonCallableSignature.Create("hashlib.new", ["name", "data", "usedforsecurity"], RequiredCount: 1, MaxPositionalCount: 2);
    public static readonly LythonCallableSignature HashlibFileDigest = LythonCallableSignature.Create("hashlib.file_digest", ["fileobj", "digest", "_bufsize"], RequiredCount: 2, MaxPositionalCount: 2);

    public static readonly LythonCallableSignature GzipCompress = LythonCallableSignature.Create("gzip.compress", ["data", "compresslevel", "mtime"], RequiredCount: 1, MaxPositionalCount: 2);
    public static readonly LythonCallableSignature GzipDecompress = LythonCallableSignature.Create("gzip.decompress", ["data"]);
    public static readonly LythonCallableSignature GzipOpen = LythonCallableSignature.Create("gzip.open", ["filename", "mode", "compresslevel", "encoding", "errors", "newline"], RequiredCount: 1);

    public static readonly LythonCallableSignature ShlexQuote = LythonCallableSignature.Create("shlex.quote", ["s"]);
    public static readonly LythonCallableSignature ShlexJoin = LythonCallableSignature.Create("shlex.join", ["split_command"]);
    public static readonly LythonCallableSignature ShlexSplit = LythonCallableSignature.Create("shlex.split", ["s", "comments", "posix"], RequiredCount: 1);
    public static readonly LythonCallableSignature ShlexClass = LythonCallableSignature.Create("shlex.shlex", ["instream", "infile", "posix", "punctuation_chars"], RequiredCount: 0);

    public static readonly LythonCallableSignature TimeTime = LythonCallableSignature.Create("time.time", []);
    public static readonly LythonCallableSignature TimeTimeNs = LythonCallableSignature.Create("time.time_ns", []);
    public static readonly LythonCallableSignature TimeMonotonic = LythonCallableSignature.Create("time.monotonic", []);
    public static readonly LythonCallableSignature TimeMonotonicNs = LythonCallableSignature.Create("time.monotonic_ns", []);
    public static readonly LythonCallableSignature TimePerfCounter = LythonCallableSignature.Create("time.perf_counter", []);
    public static readonly LythonCallableSignature TimePerfCounterNs = LythonCallableSignature.Create("time.perf_counter_ns", []);
    public static readonly LythonCallableSignature TimeSleep = LythonCallableSignature.Create("time.sleep", ["secs"], RequiredCount: null, MaxPositionalCount: null, VariadicParameters: LythonVariadicParameters.None, PositionalOnlyCount: 1);
    public static readonly LythonCallableSignature TimeGetClockInfo = LythonCallableSignature.Create("time.get_clock_info", ["name"], RequiredCount: null, MaxPositionalCount: null, VariadicParameters: LythonVariadicParameters.None, PositionalOnlyCount: 1);
    public static readonly LythonCallableSignature TimeProcessTime = LythonCallableSignature.Create("time.process_time", []);
    public static readonly LythonCallableSignature TimeProcessTimeNs = LythonCallableSignature.Create("time.process_time_ns", []);
    public static readonly LythonCallableSignature TimeThreadTime = LythonCallableSignature.Create("time.thread_time", []);
    public static readonly LythonCallableSignature TimeThreadTimeNs = LythonCallableSignature.Create("time.thread_time_ns", []);
    public static readonly LythonCallableSignature TimeClockGetTime = LythonCallableSignature.Create("time.clock_gettime", ["clk_id"], RequiredCount: null, MaxPositionalCount: null, VariadicParameters: LythonVariadicParameters.None, PositionalOnlyCount: 1);
    public static readonly LythonCallableSignature TimeClockGetTimeNs = LythonCallableSignature.Create("time.clock_gettime_ns", ["clk_id"], RequiredCount: null, MaxPositionalCount: null, VariadicParameters: LythonVariadicParameters.None, PositionalOnlyCount: 1);
    public static readonly LythonCallableSignature TimeClockGetRes = LythonCallableSignature.Create("time.clock_getres", ["clk_id"], RequiredCount: null, MaxPositionalCount: null, VariadicParameters: LythonVariadicParameters.None, PositionalOnlyCount: 1);
    public static readonly LythonCallableSignature TimeClockSetTime = LythonCallableSignature.Create("time.clock_settime", ["clk_id", "time"], RequiredCount: null, MaxPositionalCount: null, VariadicParameters: LythonVariadicParameters.None, PositionalOnlyCount: 2);
    public static readonly LythonCallableSignature TimeClockSetTimeNs = LythonCallableSignature.Create("time.clock_settime_ns", ["clk_id", "time_ns"], RequiredCount: null, MaxPositionalCount: null, VariadicParameters: LythonVariadicParameters.None, PositionalOnlyCount: 2);
    public static readonly LythonCallableSignature TimePthreadGetCpuClockId = LythonCallableSignature.Create("time.pthread_getcpuclockid", ["thread_id"], RequiredCount: null, MaxPositionalCount: null, VariadicParameters: LythonVariadicParameters.None, PositionalOnlyCount: 1);
    public static readonly LythonCallableSignature TimeGmtime = LythonCallableSignature.Create("time.gmtime", ["secs"], RequiredCount: 0);
    public static readonly LythonCallableSignature TimeLocaltime = LythonCallableSignature.Create("time.localtime", ["secs"], RequiredCount: 0);
    public static readonly LythonCallableSignature TimeCtime = LythonCallableSignature.Create("time.ctime", ["secs"], RequiredCount: 0);
    public static readonly LythonCallableSignature TimeMktime = LythonCallableSignature.Create("time.mktime", ["t"]);
    public static readonly LythonCallableSignature TimeAsctime = LythonCallableSignature.Create("time.asctime", ["t"], RequiredCount: 0);
    public static readonly LythonCallableSignature TimeStrftime = LythonCallableSignature.Create("time.strftime", ["format", "t"], RequiredCount: 1);
    public static readonly LythonCallableSignature TimeStrptime = LythonCallableSignature.Create("time.strptime", ["string", "format"], RequiredCount: 1);
    public static readonly LythonCallableSignature TimeStructTime = LythonCallableSignature.Create("time.struct_time", ["sequence"], RequiredCount: null, MaxPositionalCount: null, VariadicParameters: LythonVariadicParameters.None, PositionalOnlyCount: 1);
    public static readonly LythonCallableSignature TimeTzset = LythonCallableSignature.Create("time.tzset", []);

    public static readonly LythonCallableSignature ItertoolsChain = LythonCallableSignature.Create("itertools.chain", RequiredCount: 0);
    public static readonly LythonCallableSignature ItertoolsCount = LythonCallableSignature.Create("itertools.count", ["start", "step"], RequiredCount: 0);
    public static readonly LythonCallableSignature ItertoolsRepeat = LythonCallableSignature.Create("itertools.repeat", ["object", "times"], RequiredCount: 1);
    public static readonly LythonCallableSignature ItertoolsCycle = LythonCallableSignature.Create("itertools.cycle", ["iterable"]);
    public static readonly LythonCallableSignature ItertoolsIslice = LythonCallableSignature.Create("itertools.islice", ["iterable", "start", "stop", "step"], RequiredCount: 2);
    public static readonly LythonCallableSignature ItertoolsCombinations = LythonCallableSignature.Create("itertools.combinations", ["iterable", "r"]);
    public static readonly LythonCallableSignature ItertoolsCombinationsWithReplacement = LythonCallableSignature.Create("itertools.combinations_with_replacement", ["iterable", "r"]);
    public static readonly LythonCallableSignature ItertoolsPermutations = LythonCallableSignature.Create("itertools.permutations", ["iterable", "r"], RequiredCount: 1);
    public static readonly LythonCallableSignature ItertoolsAccumulate = LythonCallableSignature.Create("itertools.accumulate", ["iterable", "func", "initial"], RequiredCount: 1, MaxPositionalCount: 2);
    public static readonly LythonCallableSignature ItertoolsCompress = LythonCallableSignature.Create("itertools.compress", ["data", "selectors"]);
    public static readonly LythonCallableSignature ItertoolsFilterFalse = LythonCallableSignature.Create("itertools.filterfalse", ["function", "iterable"]);
    public static readonly LythonCallableSignature ItertoolsDropWhile = LythonCallableSignature.Create("itertools.dropwhile", ["predicate", "iterable"]);
    public static readonly LythonCallableSignature ItertoolsTakeWhile = LythonCallableSignature.Create("itertools.takewhile", ["predicate", "iterable"]);
    public static readonly LythonCallableSignature ItertoolsStarmap = LythonCallableSignature.Create("itertools.starmap", ["function", "iterable"]);
    public static readonly LythonCallableSignature ItertoolsPairwise = LythonCallableSignature.Create("itertools.pairwise", ["iterable"]);
    public static readonly LythonCallableSignature ItertoolsGroupBy = LythonCallableSignature.Create("itertools.groupby", ["iterable", "key"], RequiredCount: 1);
    public static readonly LythonCallableSignature ItertoolsTee = LythonCallableSignature.Create("itertools.tee", ["iterable", "n"], RequiredCount: 1);
    public static readonly LythonCallableSignature ItertoolsBatched = LythonCallableSignature.Create("itertools.batched", ["iterable", "n", "strict"], RequiredCount: 2, MaxPositionalCount: 2);
}

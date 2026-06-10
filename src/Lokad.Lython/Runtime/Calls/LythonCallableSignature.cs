namespace Lokad.Lython.Runtime;

internal readonly record struct LythonCallableSignature(
    string Name,
    string[]? ParameterNames = null,
    int? RequiredCount = null,
    int? MaxPositionalCount = null,
    bool AllowsExtraKeywords = false,
    bool AllowsExtraPositional = false)
{
    public int MinimumArgumentCount => RequiredCount ?? ParameterNames?.Length ?? 0;

    public int? MaximumArgumentCount => AllowsExtraKeywords || AllowsExtraPositional
        ? null
        : ParameterNames?.Length;
}

internal static class LythonKnownCallableSignatures
{
    public static readonly LythonCallableSignature SysExit = new("sys.exit", ["code"], RequiredCount: 0);
    public static readonly LythonCallableSignature SysGetDefaultEncoding = new("sys.getdefaultencoding", []);
    public static readonly LythonCallableSignature SysExcInfo = new("sys.exc_info", []);
    public static readonly LythonCallableSignature SysGetSizeOf = new("sys.getsizeof", ["object", "default"], RequiredCount: 1);
    public static readonly LythonCallableSignature SysSetTrace = new("sys.settrace", ["function"]);
    public static readonly LythonCallableSignature SysSetProfile = new("sys.setprofile", ["function"]);
    public static readonly LythonCallableSignature SysSetRecursionLimit = new("sys.setrecursionlimit", ["limit"]);
    public static readonly LythonCallableSignature SysAddAuditHook = new("sys.addaudithook", ["hook"]);
    public static readonly LythonCallableSignature SysAudit = new("sys.audit", ["event", "args"], RequiredCount: 1, AllowsExtraKeywords: true);

    public static readonly LythonCallableSignature PathlibPath = new("pathlib.Path", RequiredCount: 0);
    public static readonly LythonCallableSignature PathlibPurePath = new("pathlib.PurePath", RequiredCount: 0);
    public static readonly LythonCallableSignature PathlibPurePosixPath = new("pathlib.PurePosixPath", RequiredCount: 0);
    public static readonly LythonCallableSignature PathlibPosixPath = new("pathlib.PosixPath", RequiredCount: 0);
    public static readonly LythonCallableSignature PathlibPureWindowsPath = new("pathlib.PureWindowsPath", RequiredCount: 0);
    public static readonly LythonCallableSignature PathlibWindowsPath = new("pathlib.WindowsPath", RequiredCount: 0);

    public static readonly LythonCallableSignature JsonLoad = new("json.load", ["fp", "cls", "object_hook", "parse_float", "parse_int", "parse_constant", "object_pairs_hook"], RequiredCount: 1, MaxPositionalCount: 1);
    public static readonly LythonCallableSignature JsonLoads = new("json.loads", ["s", "cls", "object_hook", "parse_float", "parse_int", "parse_constant", "object_pairs_hook"], RequiredCount: 1, MaxPositionalCount: 1);
    public static readonly LythonCallableSignature JsonDump = new("json.dump", ["obj", "fp", "skipkeys", "ensure_ascii", "check_circular", "allow_nan", "cls", "indent", "separators", "default", "sort_keys"], RequiredCount: 2, MaxPositionalCount: 2);
    public static readonly LythonCallableSignature JsonDumps = new("json.dumps", ["obj", "skipkeys", "ensure_ascii", "check_circular", "allow_nan", "cls", "indent", "separators", "default", "sort_keys"], RequiredCount: 1, MaxPositionalCount: 1);

    public static readonly LythonCallableSignature CsvReader = new("csv.reader", ["csvfile", "dialect", "delimiter", "quotechar", "quoting", "doublequote", "escapechar", "skipinitialspace", "lineterminator", "strict"], RequiredCount: 1, MaxPositionalCount: 2);
    public static readonly LythonCallableSignature CsvWriter = new("csv.writer", ["fileobj", "dialect", "delimiter", "quotechar", "quoting", "doublequote", "escapechar", "skipinitialspace", "lineterminator", "strict"], RequiredCount: 0, MaxPositionalCount: 2);
    public static readonly LythonCallableSignature CsvDictReader = new("csv.DictReader", ["f", "fieldnames", "restkey", "restval", "dialect", "delimiter", "quotechar", "quoting", "doublequote", "escapechar", "skipinitialspace", "lineterminator", "strict"], RequiredCount: 1, MaxPositionalCount: 5);
    public static readonly LythonCallableSignature CsvDictWriter = new("csv.DictWriter", ["f", "fieldnames", "restval", "extrasaction", "dialect", "delimiter", "quotechar", "quoting", "doublequote", "escapechar", "skipinitialspace", "lineterminator", "strict"], RequiredCount: 2, MaxPositionalCount: 5);

    public static readonly LythonCallableSignature CollectionsDefaultDict = new("collections.defaultdict", ["default_factory", "iterable"], RequiredCount: 0, MaxPositionalCount: 2, AllowsExtraKeywords: true);
    public static readonly LythonCallableSignature CollectionsCounter = new("collections.Counter", ["iterable"], RequiredCount: 0, MaxPositionalCount: 1, AllowsExtraKeywords: true);
    public static readonly LythonCallableSignature CollectionsDeque = new("collections.deque", ["iterable", "maxlen"], RequiredCount: 0);
    public static readonly LythonCallableSignature CollectionsNamedTuple = new("collections.namedtuple", ["typename", "field_names", "rename", "defaults", "module"], RequiredCount: 2, MaxPositionalCount: 2);
    public static readonly LythonCallableSignature CollectionsOrderedDict = new("collections.OrderedDict", ["mapping"], RequiredCount: 0, MaxPositionalCount: 1, AllowsExtraKeywords: true);
    public static readonly LythonCallableSignature CollectionsChainMap = new("collections.ChainMap", RequiredCount: 0);
    public static readonly LythonCallableSignature CollectionsUserDict = new("collections.UserDict", RequiredCount: 0);
    public static readonly LythonCallableSignature CollectionsUserList = new("collections.UserList", RequiredCount: 0);
    public static readonly LythonCallableSignature CollectionsUserString = new("collections.UserString", RequiredCount: 0);

    public static readonly LythonCallableSignature CopyCopy = new("copy.copy", ["x"]);
    public static readonly LythonCallableSignature CopyDeepCopy = new("copy.deepcopy", ["x", "memo"], RequiredCount: 1);
    public static readonly LythonCallableSignature CopyReplace = new("copy.replace", ["__object"], RequiredCount: 1, MaxPositionalCount: 1, AllowsExtraKeywords: true);

    public static readonly LythonCallableSignature FunctoolsUpdateWrapper = new("functools.update_wrapper", ["wrapper", "wrapped", "assigned", "updated"], RequiredCount: 2);
    public static readonly LythonCallableSignature FunctoolsWraps = new("functools.wraps", ["wrapped", "assigned", "updated"], RequiredCount: 1);
    public static readonly LythonCallableSignature FunctoolsTotalOrdering = new("functools.total_ordering", ["cls"]);
    public static readonly LythonCallableSignature FunctoolsReduce = new("functools.reduce", ["function", "iterable", "initializer"], RequiredCount: 2);
    public static readonly LythonCallableSignature FunctoolsPartial = new("functools.partial", ["func"], RequiredCount: 1, AllowsExtraKeywords: true, AllowsExtraPositional: true);
    public static readonly LythonCallableSignature FunctoolsPartialMethod = new("functools.partialmethod", ["func"], RequiredCount: 1, AllowsExtraKeywords: true, AllowsExtraPositional: true);
    public static readonly LythonCallableSignature FunctoolsCmpToKey = new("functools.cmp_to_key", ["mycmp"]);
    public static readonly LythonCallableSignature FunctoolsLruCache = new("functools.lru_cache", ["maxsize", "typed"], RequiredCount: 0);
    public static readonly LythonCallableSignature FunctoolsCache = new("functools.cache", ["user_function"]);
    public static readonly LythonCallableSignature FunctoolsCachedProperty = new("functools.cached_property", ["func"]);
    public static readonly LythonCallableSignature FunctoolsSingleDispatch = new("functools.singledispatch", ["func"]);
    public static readonly LythonCallableSignature FunctoolsSingleDispatchMethod = new("functools.singledispatchmethod", ["func"]);
    public static readonly LythonCallableSignature FunctoolsRecursiveRepr = new("functools.recursive_repr", ["fillvalue"], RequiredCount: 0);

    public static readonly LythonCallableSignature OperatorTruth = new("operator.truth", ["obj"]);
    public static readonly LythonCallableSignature OperatorNot = new("operator.not_", ["obj"]);
    public static readonly LythonCallableSignature OperatorIs = new("operator.is_", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorIsNot = new("operator.is_not", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorAbs = new("operator.abs", ["obj"]);
    public static readonly LythonCallableSignature OperatorNeg = new("operator.neg", ["obj"]);
    public static readonly LythonCallableSignature OperatorPos = new("operator.pos", ["obj"]);
    public static readonly LythonCallableSignature OperatorInvert = new("operator.invert", ["obj"]);
    public static readonly LythonCallableSignature OperatorIndex = new("operator.index", ["obj"]);
    public static readonly LythonCallableSignature OperatorAdd = new("operator.add", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorSub = new("operator.sub", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorMul = new("operator.mul", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorTrueDiv = new("operator.truediv", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorFloorDiv = new("operator.floordiv", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorMod = new("operator.mod", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorPow = new("operator.pow", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorMatMul = new("operator.matmul", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorLShift = new("operator.lshift", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorRShift = new("operator.rshift", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorAnd = new("operator.and_", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorOr = new("operator.or_", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorXor = new("operator.xor", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorConcat = new("operator.concat", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorEq = new("operator.eq", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorNe = new("operator.ne", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorLt = new("operator.lt", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorLe = new("operator.le", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorGt = new("operator.gt", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorGe = new("operator.ge", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorGetItem = new("operator.getitem", ["obj", "key"]);
    public static readonly LythonCallableSignature OperatorSetItem = new("operator.setitem", ["obj", "key", "value"]);
    public static readonly LythonCallableSignature OperatorDelItem = new("operator.delitem", ["obj", "key"]);
    public static readonly LythonCallableSignature OperatorContains = new("operator.contains", ["obj", "value"]);
    public static readonly LythonCallableSignature OperatorLengthHint = new("operator.length_hint", ["obj", "default"], RequiredCount: 1);
    public static readonly LythonCallableSignature OperatorCountOf = new("operator.countOf", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorIndexOf = new("operator.indexOf", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorIAdd = new("operator.iadd", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorISub = new("operator.isub", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorIMul = new("operator.imul", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorITrueDiv = new("operator.itruediv", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorIFloorDiv = new("operator.ifloordiv", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorIMod = new("operator.imod", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorIPow = new("operator.ipow", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorILShift = new("operator.ilshift", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorIRShift = new("operator.irshift", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorIAnd = new("operator.iand", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorIOr = new("operator.ior", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorIXor = new("operator.ixor", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorIConcat = new("operator.iconcat", ["a", "b"]);
    public static readonly LythonCallableSignature OperatorCall = new("operator.call", ["obj"], RequiredCount: 1, AllowsExtraKeywords: true, AllowsExtraPositional: true);
    public static readonly LythonCallableSignature OperatorItemGetter = new("operator.itemgetter", RequiredCount: 1);
    public static readonly LythonCallableSignature OperatorAttrGetter = new("operator.attrgetter", RequiredCount: 1);
    public static readonly LythonCallableSignature OperatorMethodCaller = new("operator.methodcaller", ["name"], RequiredCount: 1, AllowsExtraKeywords: true, AllowsExtraPositional: true);

    public static readonly LythonCallableSignature Decimal = new("decimal.Decimal", ["value", "context"], RequiredCount: 0);
    public static readonly LythonCallableSignature DecimalTuple = new("decimal.DecimalTuple", ["sign", "digits", "exponent"]);
    public static readonly LythonCallableSignature DecimalContext = new("decimal.Context", ["prec", "rounding", "Emin", "Emax", "capitals", "clamp", "flags", "traps"], RequiredCount: 0);
    public static readonly LythonCallableSignature DecimalGetContext = new("decimal.getcontext", []);
    public static readonly LythonCallableSignature DecimalSetContext = new("decimal.setcontext", ["context"]);
    public static readonly LythonCallableSignature DecimalLocalContext = new("decimal.localcontext", ["context"], RequiredCount: 0);

    public static readonly LythonCallableSignature DateTimeTimedelta = new("datetime.timedelta", ["days", "seconds", "microseconds", "milliseconds", "minutes", "hours", "weeks"], RequiredCount: 0);
    public static readonly LythonCallableSignature DateTimeDate = new("datetime.date", ["year", "month", "day"]);
    public static readonly LythonCallableSignature DateTimeDateToday = new("datetime.date.today", []);
    public static readonly LythonCallableSignature DateTimeDateFromTimestamp = new("datetime.date.fromtimestamp", ["timestamp"]);
    public static readonly LythonCallableSignature DateTimeDateFromOrdinal = new("datetime.date.fromordinal", ["ordinal"]);
    public static readonly LythonCallableSignature DateTimeDateFromIsoFormat = new("datetime.date.fromisoformat", ["date_string"]);
    public static readonly LythonCallableSignature DateTimeDateFromIsoCalendar = new("datetime.date.fromisocalendar", ["year", "week", "day"]);
    public static readonly LythonCallableSignature DateTimeTime = new("datetime.time", ["hour", "minute", "second", "microsecond", "tzinfo", "fold"], RequiredCount: 0);
    public static readonly LythonCallableSignature DateTimeTimeFromIsoFormat = new("datetime.time.fromisoformat", ["time_string"]);
    public static readonly LythonCallableSignature DateTimeDateTime = new("datetime.datetime", ["year", "month", "day", "hour", "minute", "second", "microsecond", "tzinfo", "fold"], RequiredCount: 3);
    public static readonly LythonCallableSignature DateTimeDateTimeToday = new("datetime.datetime.today", []);
    public static readonly LythonCallableSignature DateTimeDateTimeNow = new("datetime.datetime.now", ["tz"], RequiredCount: 0);
    public static readonly LythonCallableSignature DateTimeDateTimeUtcNow = new("datetime.datetime.utcnow", []);
    public static readonly LythonCallableSignature DateTimeDateTimeFromTimestamp = new("datetime.datetime.fromtimestamp", ["timestamp", "tz"], RequiredCount: 1);
    public static readonly LythonCallableSignature DateTimeDateTimeUtcFromTimestamp = new("datetime.datetime.utcfromtimestamp", ["timestamp"]);
    public static readonly LythonCallableSignature DateTimeDateTimeFromOrdinal = new("datetime.datetime.fromordinal", ["ordinal"]);
    public static readonly LythonCallableSignature DateTimeDateTimeCombine = new("datetime.datetime.combine", ["date", "time", "tzinfo"], RequiredCount: 2);
    public static readonly LythonCallableSignature DateTimeDateTimeFromIsoFormat = new("datetime.datetime.fromisoformat", ["date_string"]);
    public static readonly LythonCallableSignature DateTimeDateTimeFromIsoCalendar = new("datetime.datetime.fromisocalendar", ["year", "week", "day"]);
    public static readonly LythonCallableSignature DateTimeDateTimeStrptime = new("datetime.datetime.strptime", ["date_string", "format"]);
    public static readonly LythonCallableSignature DateTimeTimezone = new("datetime.timezone", ["offset", "name"], RequiredCount: 1);
    public static readonly LythonCallableSignature DateTimeTzInfo = new("datetime.tzinfo", []);

    public static readonly LythonCallableSignature StatisticsMean = new("statistics.mean", ["data"]);
    public static readonly LythonCallableSignature StatisticsFMean = new("statistics.fmean", ["data"]);
    public static readonly LythonCallableSignature StatisticsGeometricMean = new("statistics.geometric_mean", ["data"]);
    public static readonly LythonCallableSignature StatisticsHarmonicMean = new("statistics.harmonic_mean", ["data", "weights"], RequiredCount: 1);
    public static readonly LythonCallableSignature StatisticsMedian = new("statistics.median", ["data"]);
    public static readonly LythonCallableSignature StatisticsMedianLow = new("statistics.median_low", ["data"]);
    public static readonly LythonCallableSignature StatisticsMedianHigh = new("statistics.median_high", ["data"]);
    public static readonly LythonCallableSignature StatisticsMedianGrouped = new("statistics.median_grouped", ["data", "interval"], RequiredCount: 1);
    public static readonly LythonCallableSignature StatisticsMode = new("statistics.mode", ["data"]);
    public static readonly LythonCallableSignature StatisticsMultiMode = new("statistics.multimode", ["data"]);
    public static readonly LythonCallableSignature StatisticsPStdev = new("statistics.pstdev", ["data"]);
    public static readonly LythonCallableSignature StatisticsStdev = new("statistics.stdev", ["data"]);
    public static readonly LythonCallableSignature StatisticsPVariance = new("statistics.pvariance", ["data"]);
    public static readonly LythonCallableSignature StatisticsVariance = new("statistics.variance", ["data"]);
    public static readonly LythonCallableSignature StatisticsQuantiles = new("statistics.quantiles", ["data", "n", "method"], RequiredCount: 1, MaxPositionalCount: 1);
    public static readonly LythonCallableSignature StatisticsCovariance = new("statistics.covariance", ["x", "y"]);
    public static readonly LythonCallableSignature StatisticsCorrelation = new("statistics.correlation", ["x", "y"]);
    public static readonly LythonCallableSignature StatisticsLinearRegression = new("statistics.linear_regression", ["x", "y", "proportional"], RequiredCount: 2, MaxPositionalCount: 2);
    public static readonly LythonCallableSignature StatisticsLinearRegressionResult = new("statistics.LinearRegression", ["slope", "intercept"]);
    public static readonly LythonCallableSignature StatisticsNormalDist = new("statistics.NormalDist", ["mu", "sigma"], RequiredCount: 0);
    public static readonly LythonCallableSignature StatisticsNormalDistFromSamples = new("statistics.NormalDist.from_samples", ["data"]);
    public static readonly LythonCallableSignature StatisticsKde = new("statistics.kde", RequiredCount: 0);
    public static readonly LythonCallableSignature StatisticsKdeRandom = new("statistics.kde_random", RequiredCount: 0);

    public static readonly LythonCallableSignature RandomClass = new("random.Random", ["a"], RequiredCount: 0);
    public static readonly LythonCallableSignature RandomSystemRandom = new("random.SystemRandom", ["a"], RequiredCount: 0);
    public static readonly LythonCallableSignature RandomSeed = new("random.seed", ["a", "version"], RequiredCount: 0);
    public static readonly LythonCallableSignature RandomRandom = new("random.random", []);
    public static readonly LythonCallableSignature RandomGetState = new("random.getstate", []);
    public static readonly LythonCallableSignature RandomSetState = new("random.setstate", ["state"]);
    public static readonly LythonCallableSignature RandomRandRange = new("random.randrange", ["start", "stop", "step"], RequiredCount: 0);
    public static readonly LythonCallableSignature RandomRandInt = new("random.randint", ["a", "b"]);
    public static readonly LythonCallableSignature RandomChoice = new("random.choice", ["seq"]);
    public static readonly LythonCallableSignature RandomChoices = new("random.choices", ["population", "weights", "cum_weights", "k"], RequiredCount: 1);
    public static readonly LythonCallableSignature RandomShuffle = new("random.shuffle", ["x"]);
    public static readonly LythonCallableSignature RandomSample = new("random.sample", ["population", "k", "counts"], RequiredCount: 2, MaxPositionalCount: 2);
    public static readonly LythonCallableSignature RandomGetRandBits = new("random.getrandbits", ["k"]);
    public static readonly LythonCallableSignature RandomRandBytes = new("random.randbytes", ["n"]);
    public static readonly LythonCallableSignature RandomUniform = new("random.uniform", ["a", "b"]);
    public static readonly LythonCallableSignature RandomTriangular = new("random.triangular", ["low", "high", "mode"], RequiredCount: 0);
    public static readonly LythonCallableSignature RandomBetaVariate = new("random.betavariate", ["alpha", "beta"]);
    public static readonly LythonCallableSignature RandomExpVariate = new("random.expovariate", ["lambd"], RequiredCount: 0);
    public static readonly LythonCallableSignature RandomGammaVariate = new("random.gammavariate", ["alpha", "beta"]);
    public static readonly LythonCallableSignature RandomGauss = new("random.gauss", ["mu", "sigma"], RequiredCount: 0);
    public static readonly LythonCallableSignature RandomNormalVariate = new("random.normalvariate", ["mu", "sigma"], RequiredCount: 0);
    public static readonly LythonCallableSignature RandomLogNormVariate = new("random.lognormvariate", ["mu", "sigma"]);
    public static readonly LythonCallableSignature RandomParetoVariate = new("random.paretovariate", ["alpha"]);
    public static readonly LythonCallableSignature RandomVonMisesVariate = new("random.vonmisesvariate", ["mu", "kappa"]);
    public static readonly LythonCallableSignature RandomWeibullVariate = new("random.weibullvariate", ["alpha", "beta"]);

    public static readonly LythonCallableSignature MathSqrt = new("math.sqrt", ["x"]);
    public static readonly LythonCallableSignature MathExp = new("math.exp", ["x"]);
    public static readonly LythonCallableSignature MathLog = new("math.log", ["x", "base"], RequiredCount: 1);
    public static readonly LythonCallableSignature MathLog10 = new("math.log10", ["x"]);
    public static readonly LythonCallableSignature MathLog2 = new("math.log2", ["x"]);
    public static readonly LythonCallableSignature MathSin = new("math.sin", ["x"]);
    public static readonly LythonCallableSignature MathCos = new("math.cos", ["x"]);
    public static readonly LythonCallableSignature MathTan = new("math.tan", ["x"]);
    public static readonly LythonCallableSignature MathAsin = new("math.asin", ["x"]);
    public static readonly LythonCallableSignature MathAcos = new("math.acos", ["x"]);
    public static readonly LythonCallableSignature MathAtan = new("math.atan", ["x"]);
    public static readonly LythonCallableSignature MathAtan2 = new("math.atan2", ["y", "x"]);
    public static readonly LythonCallableSignature MathSinh = new("math.sinh", ["x"]);
    public static readonly LythonCallableSignature MathCosh = new("math.cosh", ["x"]);
    public static readonly LythonCallableSignature MathTanh = new("math.tanh", ["x"]);
    public static readonly LythonCallableSignature MathFloor = new("math.floor", ["x"]);
    public static readonly LythonCallableSignature MathCeil = new("math.ceil", ["x"]);
    public static readonly LythonCallableSignature MathFabs = new("math.fabs", ["x"]);
    public static readonly LythonCallableSignature MathTrunc = new("math.trunc", ["x"]);
    public static readonly LythonCallableSignature MathDegrees = new("math.degrees", ["x"]);
    public static readonly LythonCallableSignature MathRadians = new("math.radians", ["x"]);
    public static readonly LythonCallableSignature MathIsFinite = new("math.isfinite", ["x"]);
    public static readonly LythonCallableSignature MathIsInf = new("math.isinf", ["x"]);
    public static readonly LythonCallableSignature MathIsNaN = new("math.isnan", ["x"]);
    public static readonly LythonCallableSignature MathPow = new("math.pow", ["x", "y"]);
    public static readonly LythonCallableSignature MathHypot = new("math.hypot", RequiredCount: 0);
    public static readonly LythonCallableSignature MathFmod = new("math.fmod", ["x", "y"]);
    public static readonly LythonCallableSignature MathCopySign = new("math.copysign", ["x", "y"]);
    public static readonly LythonCallableSignature MathIsClose = new("math.isclose", ["a", "b", "rel_tol", "abs_tol"], RequiredCount: 2);
    public static readonly LythonCallableSignature MathProd = new("math.prod", ["iterable", "start"], RequiredCount: 1, MaxPositionalCount: 1);
    public static readonly LythonCallableSignature MathFsum = new("math.fsum", ["iterable"]);
    public static readonly LythonCallableSignature MathFactorial = new("math.factorial", ["n"]);
    public static readonly LythonCallableSignature MathGcd = new("math.gcd", RequiredCount: 0);
    public static readonly LythonCallableSignature MathLcm = new("math.lcm", RequiredCount: 0);
    public static readonly LythonCallableSignature MathComb = new("math.comb", ["n", "k"]);
    public static readonly LythonCallableSignature MathPerm = new("math.perm", ["n", "k"], RequiredCount: 1);
    public static readonly LythonCallableSignature MathIsqrt = new("math.isqrt", ["n"]);
    public static readonly LythonCallableSignature MathDist = new("math.dist", ["p", "q"]);
    public static readonly LythonCallableSignature MathFrexp = new("math.frexp", ["x"]);
    public static readonly LythonCallableSignature MathLdexp = new("math.ldexp", ["x", "i"]);
    public static readonly LythonCallableSignature MathModf = new("math.modf", ["x"]);
    public static readonly LythonCallableSignature MathRemainder = new("math.remainder", ["x", "y"]);
    public static readonly LythonCallableSignature MathNextAfter = new("math.nextafter", ["x", "y", "steps"], RequiredCount: 2, MaxPositionalCount: 2);
    public static readonly LythonCallableSignature MathUlp = new("math.ulp", ["x"]);
    public static readonly LythonCallableSignature MathExp2 = new("math.exp2", ["x"]);
    public static readonly LythonCallableSignature MathExpm1 = new("math.expm1", ["x"]);
    public static readonly LythonCallableSignature MathLog1p = new("math.log1p", ["x"]);
    public static readonly LythonCallableSignature MathCbrt = new("math.cbrt", ["x"]);
    public static readonly LythonCallableSignature MathErf = new("math.erf", ["x"]);
    public static readonly LythonCallableSignature MathErfc = new("math.erfc", ["x"]);
    public static readonly LythonCallableSignature MathGamma = new("math.gamma", ["x"]);
    public static readonly LythonCallableSignature MathLgamma = new("math.lgamma", ["x"]);
    public static readonly LythonCallableSignature MathFma = new("math.fma", ["x", "y", "z"], MaxPositionalCount: 3);
    public static readonly LythonCallableSignature MathSumProd = new("math.sumprod", ["p", "q"], MaxPositionalCount: 2);

    public static readonly LythonCallableSignature OpenPyxlWorkbook = new("openpyxl.Workbook", ["write_only", "iso_dates"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlLoadWorkbook = new("openpyxl.load_workbook", ["filename", "read_only", "keep_vba", "data_only", "keep_links", "rich_text"], RequiredCount: 1);
    public static readonly LythonCallableSignature OpenPyxlGetColumnLetter = new("openpyxl.utils.get_column_letter", ["col_idx"]);
    public static readonly LythonCallableSignature OpenPyxlColumnIndexFromString = new("openpyxl.utils.column_index_from_string", ["col"]);
    public static readonly LythonCallableSignature OpenPyxlCoordinateFromString = new("openpyxl.utils.coordinate_from_string", ["coord_string"]);
    public static readonly LythonCallableSignature OpenPyxlCoordinateToTuple = new("openpyxl.utils.coordinate_to_tuple", ["coordinate"]);
    public static readonly LythonCallableSignature OpenPyxlRangeBoundaries = new("openpyxl.utils.range_boundaries", ["range_string"]);
    public static readonly LythonCallableSignature OpenPyxlGetColumnInterval = new("openpyxl.utils.get_column_interval", ["start", "end"]);
    public static readonly LythonCallableSignature OpenPyxlAbsoluteCoordinate = new("openpyxl.utils.absolute_coordinate", ["coord_string"]);
    public static readonly LythonCallableSignature OpenPyxlQuoteSheetName = new("openpyxl.utils.quote_sheetname", ["sheetname"]);
    public static readonly LythonCallableSignature OpenPyxlRowsFromRange = new("openpyxl.utils.rows_from_range", ["range_string"]);
    public static readonly LythonCallableSignature OpenPyxlColsFromRange = new("openpyxl.utils.cols_from_range", ["range_string"]);
    public static readonly LythonCallableSignature OpenPyxlComment = new("openpyxl.comments.Comment", ["text", "author"]);
    public static readonly LythonCallableSignature OpenPyxlFont = new("openpyxl.styles.Font", ["name", "sz", "bold", "italic", "color", "underline", "b", "i"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlPatternFill = new("openpyxl.styles.PatternFill", ["fill_type", "start_color", "end_color", "fgColor", "bgColor", "patternType"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlBorder = new("openpyxl.styles.Border", ["left", "right", "top", "bottom"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlSide = new("openpyxl.styles.Side", ["style", "color", "border_style"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlAlignment = new("openpyxl.styles.Alignment", ["horizontal", "vertical", "wrap_text", "text_rotation"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlProtection = new("openpyxl.styles.Protection", ["locked", "hidden"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlNamedStyle = new("openpyxl.styles.NamedStyle", ["name", "font", "fill", "border", "alignment", "number_format", "protection"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlColor = new("openpyxl.styles.colors.Color", ["rgb", "indexed", "auto", "theme", "tint", "type"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlTable = new("openpyxl.worksheet.table.Table", ["displayName", "ref"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlTableStyleInfo = new("openpyxl.worksheet.table.TableStyleInfo", ["name", "showFirstColumn", "showLastColumn", "showRowStripes", "showColumnStripes"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlDataValidation = new("openpyxl.worksheet.datavalidation.DataValidation", ["type", "formula1", "formula2", "allow_blank", "showErrorMessage", "showInputMessage", "operator", "errorTitle", "error", "promptTitle", "prompt"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlBarChart = new("openpyxl.chart.BarChart", []);
    public static readonly LythonCallableSignature OpenPyxlLineChart = new("openpyxl.chart.LineChart", []);
    public static readonly LythonCallableSignature OpenPyxlPieChart = new("openpyxl.chart.PieChart", []);
    public static readonly LythonCallableSignature OpenPyxlScatterChart = new("openpyxl.chart.ScatterChart", []);
    public static readonly LythonCallableSignature OpenPyxlChartReference = new("openpyxl.chart.Reference", ["worksheet", "min_col", "min_row", "max_col", "max_row", "range_string"], RequiredCount: 1);
    public static readonly LythonCallableSignature OpenPyxlChartSeries = new("openpyxl.chart.Series", ["values", "xvalues", "title"], RequiredCount: 0);
    public static readonly LythonCallableSignature OpenPyxlDrawingImage = new("openpyxl.drawing.image.Image", ["img"]);

    public static readonly LythonCallableSignature ReCompile = new("re.compile", ["pattern", "flags"], RequiredCount: 1);
    public static readonly LythonCallableSignature ReSearch = new("re.search", ["pattern", "string", "flags", "pos", "endpos"], RequiredCount: 2);
    public static readonly LythonCallableSignature ReMatch = new("re.match", ["pattern", "string", "flags", "pos", "endpos"], RequiredCount: 2);
    public static readonly LythonCallableSignature ReFullMatch = new("re.fullmatch", ["pattern", "string", "flags", "pos", "endpos"], RequiredCount: 2);
    public static readonly LythonCallableSignature ReFindAll = new("re.findall", ["pattern", "string", "flags", "pos", "endpos"], RequiredCount: 2);
    public static readonly LythonCallableSignature ReFindIter = new("re.finditer", ["pattern", "string", "flags", "pos", "endpos"], RequiredCount: 2);
    public static readonly LythonCallableSignature ReSub = new("re.sub", ["pattern", "repl", "string", "count", "flags", "pos", "endpos"], RequiredCount: 3);
    public static readonly LythonCallableSignature ReSubn = new("re.subn", ["pattern", "repl", "string", "count", "flags", "pos", "endpos"], RequiredCount: 3);
    public static readonly LythonCallableSignature ReSplit = new("re.split", ["pattern", "string", "maxsplit", "flags", "pos", "endpos"], RequiredCount: 2);
    public static readonly LythonCallableSignature ReEscape = new("re.escape", ["string"]);
    public static readonly LythonCallableSignature RePurge = new("re.purge", []);

    public static readonly LythonCallableSignature ArgparseArgumentParser = new(
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
    public static readonly LythonCallableSignature ArgparseNamespace = new("argparse.Namespace", [], RequiredCount: 0, MaxPositionalCount: 0, AllowsExtraKeywords: true);
    public static readonly LythonCallableSignature ArgparseFileType = new("argparse.FileType", ["mode", "bufsize", "encoding", "errors"], RequiredCount: 0);

    public static readonly LythonCallableSignature DataclassesDataclass = new("dataclasses.dataclass", ["cls", "init", "repr", "eq", "order", "unsafe_hash", "frozen", "match_args", "kw_only", "slots", "weakref_slot"], RequiredCount: 0, MaxPositionalCount: 1);
    public static readonly LythonCallableSignature DataclassesField = new("dataclasses.field", ["default", "default_factory", "init", "repr", "hash", "compare", "metadata", "kw_only"], RequiredCount: 0, MaxPositionalCount: 0);
    public static readonly LythonCallableSignature DataclassesMakeDataclass = new("dataclasses.make_dataclass", ["cls_name", "fields", "bases", "namespace", "init", "repr", "eq", "order", "unsafe_hash", "frozen", "match_args", "kw_only", "slots", "weakref_slot"], RequiredCount: 2, MaxPositionalCount: 2);
    public static readonly LythonCallableSignature DataclassesIsDataclass = new("dataclasses.is_dataclass", ["value"]);
    public static readonly LythonCallableSignature DataclassesFields = new("dataclasses.fields", ["class_or_instance"]);
    public static readonly LythonCallableSignature DataclassesAsDict = new("dataclasses.asdict", ["obj", "dict_factory"], RequiredCount: 1);
    public static readonly LythonCallableSignature DataclassesAsTuple = new("dataclasses.astuple", ["obj", "tuple_factory"], RequiredCount: 1);
    public static readonly LythonCallableSignature DataclassesReplace = new("dataclasses.replace", ["obj"], RequiredCount: 1, MaxPositionalCount: 1, AllowsExtraKeywords: true);

    public static readonly LythonCallableSignature TypingTypeVar = new("typing.TypeVar", ["name"], RequiredCount: 1, AllowsExtraKeywords: true);
    public static readonly LythonCallableSignature TypingNewType = new("typing.NewType", ["name", "tp"]);
    public static readonly LythonCallableSignature TypingCast = new("typing.cast", ["typ", "val"]);
    public static readonly LythonCallableSignature TypingGetOrigin = new("typing.get_origin", ["tp"]);
    public static readonly LythonCallableSignature TypingGetArgs = new("typing.get_args", ["tp"]);
    public static readonly LythonCallableSignature TypingNamedTuple = new("typing.NamedTuple", ["typename", "fields"], RequiredCount: 1, AllowsExtraKeywords: true);
    public static readonly LythonCallableSignature TypingTypedDict = new("typing.TypedDict", ["typename", "fields"], RequiredCount: 1, AllowsExtraKeywords: true);

    private static readonly string[] SubprocessParameters = ["args", "input", "cwd", "timeout", "check", "capture_output", "stdin", "stdout", "stderr", "shell", "text", "encoding", "errors", "env", "universal_newlines"];

    public static readonly LythonCallableSignature SubprocessRun = new("subprocess.run", SubprocessParameters, RequiredCount: 1, MaxPositionalCount: 6);
    public static readonly LythonCallableSignature SubprocessCall = new("subprocess.call", SubprocessParameters, RequiredCount: 1, MaxPositionalCount: 6);
    public static readonly LythonCallableSignature SubprocessCheckCall = new("subprocess.check_call", SubprocessParameters, RequiredCount: 1, MaxPositionalCount: 6);
    public static readonly LythonCallableSignature SubprocessCheckOutput = new("subprocess.check_output", SubprocessParameters, RequiredCount: 1, MaxPositionalCount: 6);
    public static readonly LythonCallableSignature SubprocessCompletedProcess = new("subprocess.CompletedProcess", ["args", "returncode", "stdout", "stderr"], RequiredCount: 2);
    public static readonly LythonCallableSignature SubprocessList2Cmdline = new("subprocess.list2cmdline", ["seq"]);
    public static readonly LythonCallableSignature SubprocessUnsupported = new("subprocess.unsupported", RequiredCount: 0, AllowsExtraKeywords: true, AllowsExtraPositional: true);

    public static readonly LythonCallableSignature OsListDir = new("os.listdir", ["path"], RequiredCount: 0);
    public static readonly LythonCallableSignature OsWalk = new("os.walk", ["top", "topdown", "onerror", "followlinks"], RequiredCount: 0);
    public static readonly LythonCallableSignature OsGetCwd = new("os.getcwd", []);
    public static readonly LythonCallableSignature OsFspath = new("os.fspath", ["path"]);
    public static readonly LythonCallableSignature OsFsEncode = new("os.fsencode", ["filename"]);
    public static readonly LythonCallableSignature OsFsDecode = new("os.fsdecode", ["filename"]);
    public static readonly LythonCallableSignature OsGetEnv = new("os.getenv", ["key", "default"], RequiredCount: 1);
    public static readonly LythonCallableSignature OsPutEnv = new("os.putenv", ["key", "value"]);
    public static readonly LythonCallableSignature OsUnsetEnv = new("os.unsetenv", ["key"]);
    public static readonly LythonCallableSignature OsGetExecPath = new("os.get_exec_path", ["env"], RequiredCount: 0);
    public static readonly LythonCallableSignature OsStat = new("os.stat", ["path"]);
    public static readonly LythonCallableSignature OsLstat = new("os.lstat", ["path"]);
    public static readonly LythonCallableSignature OsScandir = new("os.scandir", ["path"], RequiredCount: 0);
    public static readonly LythonCallableSignature OsMkdir = new("os.mkdir", ["path"]);
    public static readonly LythonCallableSignature OsMakedirs = new("os.makedirs", ["path", "exist_ok"], RequiredCount: 1);
    public static readonly LythonCallableSignature OsRemove = new("os.remove", ["path"]);
    public static readonly LythonCallableSignature OsUnlink = new("os.unlink", ["path"]);
    public static readonly LythonCallableSignature OsRename = new("os.rename", ["src", "dst"]);
    public static readonly LythonCallableSignature OsReplace = new("os.replace", ["src", "dst"]);
    public static readonly LythonCallableSignature OsRmdir = new("os.rmdir", ["path"]);
    public static readonly LythonCallableSignature OsRemovedirs = new("os.removedirs", ["path"]);

    public static readonly LythonCallableSignature OsPathJoin = new("os.path.join", RequiredCount: 1);
    public static readonly LythonCallableSignature OsPathSplit = new("os.path.split", ["path"]);
    public static readonly LythonCallableSignature OsPathSplitExt = new("os.path.splitext", ["path"]);
    public static readonly LythonCallableSignature OsPathBasename = new("os.path.basename", ["path"]);
    public static readonly LythonCallableSignature OsPathDirname = new("os.path.dirname", ["path"]);
    public static readonly LythonCallableSignature OsPathIsAbs = new("os.path.isabs", ["path"]);
    public static readonly LythonCallableSignature OsPathNormPath = new("os.path.normpath", ["path"]);
    public static readonly LythonCallableSignature OsPathNormCase = new("os.path.normcase", ["path"]);
    public static readonly LythonCallableSignature OsPathAbsPath = new("os.path.abspath", ["path"]);
    public static readonly LythonCallableSignature OsPathRelPath = new("os.path.relpath", ["path", "start"], RequiredCount: 1);
    public static readonly LythonCallableSignature OsPathCommonPath = new("os.path.commonpath", ["paths"]);
    public static readonly LythonCallableSignature OsPathCommonPrefix = new("os.path.commonprefix", ["list"]);
    public static readonly LythonCallableSignature OsPathExists = new("os.path.exists", ["path"]);
    public static readonly LythonCallableSignature OsPathLexists = new("os.path.lexists", ["path"]);
    public static readonly LythonCallableSignature OsPathIsFile = new("os.path.isfile", ["path"]);
    public static readonly LythonCallableSignature OsPathIsDir = new("os.path.isdir", ["path"]);
    public static readonly LythonCallableSignature OsPathGetSize = new("os.path.getsize", ["path"]);
    public static readonly LythonCallableSignature OsPathGetMTime = new("os.path.getmtime", ["path"]);
    public static readonly LythonCallableSignature OsPathGetATime = new("os.path.getatime", ["path"]);
    public static readonly LythonCallableSignature OsPathGetCTime = new("os.path.getctime", ["path"]);
    public static readonly LythonCallableSignature OsPathSameFile = new("os.path.samefile", ["path1", "path2"]);
    public static readonly LythonCallableSignature OsPathRealPath = new("os.path.realpath", ["path"]);
    public static readonly LythonCallableSignature OsPathExpandVars = new("os.path.expandvars", ["path"]);
    public static readonly LythonCallableSignature OsPathSplitDrive = new("os.path.splitdrive", ["path"]);
    public static readonly LythonCallableSignature OsPathSplitRoot = new("os.path.splitroot", ["path"]);
    public static readonly LythonCallableSignature OsPathIsMount = new("os.path.ismount", ["path"]);

    public static readonly LythonCallableSignature Glob = new("glob.glob", ["pathname", "root_dir", "dir_fd", "recursive", "include_hidden"], RequiredCount: 1, MaxPositionalCount: 1);
    public static readonly LythonCallableSignature IGlob = new("glob.iglob", ["pathname", "root_dir", "dir_fd", "recursive", "include_hidden"], RequiredCount: 1, MaxPositionalCount: 1);
    public static readonly LythonCallableSignature GlobEscape = new("glob.escape", ["pathname"]);
    public static readonly LythonCallableSignature GlobHasMagic = new("glob.has_magic", ["s"]);
    public static readonly LythonCallableSignature GlobTranslate = new("glob.translate", ["pathname", "recursive", "include_hidden", "seps"], RequiredCount: 1, MaxPositionalCount: 1);
    public static readonly LythonCallableSignature Glob0 = new("glob.glob0", ["dirname", "basename", "dir_fd", "dironly", "include_hidden"], RequiredCount: 2);
    public static readonly LythonCallableSignature Glob1 = new("glob.glob1", ["dirname", "pattern"], RequiredCount: 2);

    public static readonly LythonCallableSignature FnMatch = new("fnmatch.fnmatch", ["name", "pattern"]);
    public static readonly LythonCallableSignature FnMatchCase = new("fnmatch.fnmatchcase", ["name", "pattern"]);
    public static readonly LythonCallableSignature FnMatchFilter = new("fnmatch.filter", ["names", "pattern"]);
    public static readonly LythonCallableSignature FnMatchTranslate = new("fnmatch.translate", ["pattern"]);

    public static readonly LythonCallableSignature DifflibIsLineJunk = new("difflib.IS_LINE_JUNK", ["line"]);
    public static readonly LythonCallableSignature DifflibIsCharacterJunk = new("difflib.IS_CHARACTER_JUNK", ["ch"]);
    public static readonly LythonCallableSignature DifflibUnifiedDiff = new("difflib.unified_diff", ["a", "b", "fromfile", "tofile", "fromfiledate", "tofiledate", "n", "lineterm"], RequiredCount: 2);
    public static readonly LythonCallableSignature DifflibContextDiff = new("difflib.context_diff", ["a", "b", "fromfile", "tofile", "fromfiledate", "tofiledate", "n", "lineterm"], RequiredCount: 2);
    public static readonly LythonCallableSignature DifflibNdiff = new("difflib.ndiff", ["a", "b", "linejunk", "charjunk"], RequiredCount: 2);
    public static readonly LythonCallableSignature DifflibRestore = new("difflib.restore", ["delta", "which"]);
    public static readonly LythonCallableSignature DifflibGetCloseMatches = new("difflib.get_close_matches", ["word", "possibilities", "n", "cutoff"], RequiredCount: 2);
    public static readonly LythonCallableSignature DifflibDiffBytes = new("difflib.diff_bytes", ["dfunc", "a", "b", "fromfile", "tofile", "fromfiledate", "tofiledate", "n", "lineterm"], RequiredCount: 3);
    public static readonly LythonCallableSignature DifflibDiffer = new("difflib.Differ", ["linejunk", "charjunk"], RequiredCount: 0);
    public static readonly LythonCallableSignature DifflibHtmlDiff = new("difflib.HtmlDiff", ["tabsize", "wrapcolumn", "linejunk", "charjunk"], RequiredCount: 0);
    public static readonly LythonCallableSignature DifflibSequenceMatcher = new("difflib.SequenceMatcher", ["isjunk", "a", "b", "autojunk"], RequiredCount: 0);

    public static readonly LythonCallableSignature PkgutilModuleInfo = new("pkgutil.ModuleInfo", ["module_finder", "name", "ispkg"]);
    public static readonly LythonCallableSignature PkgutilIterModules = new("pkgutil.iter_modules", ["path", "prefix"], RequiredCount: 0);
    public static readonly LythonCallableSignature PkgutilWalkPackages = new("pkgutil.walk_packages", ["path", "prefix", "onerror"], RequiredCount: 0);
    public static readonly LythonCallableSignature PkgutilFindLoader = new("pkgutil.find_loader", ["fullname"]);
    public static readonly LythonCallableSignature PkgutilGetLoader = new("pkgutil.get_loader", ["module_or_name"]);
    public static readonly LythonCallableSignature PkgutilExtendPath = new("pkgutil.extend_path", ["path", "name"]);
    public static readonly LythonCallableSignature PkgutilResolveName = new("pkgutil.resolve_name", ["name"]);
    public static readonly LythonCallableSignature PkgutilGetImporter = new("pkgutil.get_importer", ["path_item"]);
    public static readonly LythonCallableSignature PkgutilIterImporters = new("pkgutil.iter_importers", ["fullname"], RequiredCount: 0);
    public static readonly LythonCallableSignature PkgutilIterImporterModules = new("pkgutil.iter_importer_modules", ["importer", "prefix"], RequiredCount: 1);
    public static readonly LythonCallableSignature PkgutilIterZipimportModules = new("pkgutil.iter_zipimport_modules", ["importer", "prefix"], RequiredCount: 1);
    public static readonly LythonCallableSignature PkgutilGetData = new("pkgutil.get_data", ["package", "resource"]);
    public static readonly LythonCallableSignature PkgutilReadCode = new("pkgutil.read_code", ["stream"]);

    public static readonly LythonCallableSignature ItertoolsChain = new("itertools.chain", RequiredCount: 0);
    public static readonly LythonCallableSignature ItertoolsCount = new("itertools.count", ["start", "step"], RequiredCount: 0);
    public static readonly LythonCallableSignature ItertoolsRepeat = new("itertools.repeat", ["object", "times"], RequiredCount: 1);
    public static readonly LythonCallableSignature ItertoolsCycle = new("itertools.cycle", ["iterable"]);
    public static readonly LythonCallableSignature ItertoolsIslice = new("itertools.islice", ["iterable", "start", "stop", "step"], RequiredCount: 2);
    public static readonly LythonCallableSignature ItertoolsCombinations = new("itertools.combinations", ["iterable", "r"]);
    public static readonly LythonCallableSignature ItertoolsCombinationsWithReplacement = new("itertools.combinations_with_replacement", ["iterable", "r"]);
    public static readonly LythonCallableSignature ItertoolsPermutations = new("itertools.permutations", ["iterable", "r"], RequiredCount: 1);
    public static readonly LythonCallableSignature ItertoolsAccumulate = new("itertools.accumulate", ["iterable", "func", "initial"], RequiredCount: 1, MaxPositionalCount: 2);
    public static readonly LythonCallableSignature ItertoolsCompress = new("itertools.compress", ["data", "selectors"]);
    public static readonly LythonCallableSignature ItertoolsFilterFalse = new("itertools.filterfalse", ["function", "iterable"]);
    public static readonly LythonCallableSignature ItertoolsDropWhile = new("itertools.dropwhile", ["predicate", "iterable"]);
    public static readonly LythonCallableSignature ItertoolsTakeWhile = new("itertools.takewhile", ["predicate", "iterable"]);
    public static readonly LythonCallableSignature ItertoolsStarmap = new("itertools.starmap", ["function", "iterable"]);
    public static readonly LythonCallableSignature ItertoolsPairwise = new("itertools.pairwise", ["iterable"]);
    public static readonly LythonCallableSignature ItertoolsGroupBy = new("itertools.groupby", ["iterable", "key"], RequiredCount: 1);
    public static readonly LythonCallableSignature ItertoolsTee = new("itertools.tee", ["iterable", "n"], RequiredCount: 1);
    public static readonly LythonCallableSignature ItertoolsBatched = new("itertools.batched", ["iterable", "n", "strict"], RequiredCount: 2, MaxPositionalCount: 2);
}

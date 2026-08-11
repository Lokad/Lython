namespace Lokad.Lython.Runtime;

internal static partial class LythonKnownCallableSignatures
{
    public static readonly LythonCallableSignature OsListDir = LythonCallableSignature.Create("os.listdir", ["path"], requiredCount: 0);
    public static readonly LythonCallableSignature OsWalk = LythonCallableSignature.Create("os.walk", ["top", "topdown", "onerror", "followlinks"], requiredCount: 0);
    public static readonly LythonCallableSignature OsGetCwd = LythonCallableSignature.Create("os.getcwd", []);
    public static readonly LythonCallableSignature OsFspath = LythonCallableSignature.Create("os.fspath", ["path"]);
    public static readonly LythonCallableSignature OsFsEncode = LythonCallableSignature.Create("os.fsencode", ["filename"]);
    public static readonly LythonCallableSignature OsFsDecode = LythonCallableSignature.Create("os.fsdecode", ["filename"]);
    public static readonly LythonCallableSignature OsGetEnv = LythonCallableSignature.Create("os.getenv", ["key", "default"], requiredCount: 1);
    public static readonly LythonCallableSignature OsPutEnv = LythonCallableSignature.Create("os.putenv", ["key", "value"]);
    public static readonly LythonCallableSignature OsUnsetEnv = LythonCallableSignature.Create("os.unsetenv", ["key"]);
    public static readonly LythonCallableSignature OsGetExecPath = LythonCallableSignature.Create("os.get_exec_path", ["env"], requiredCount: 0);
    public static readonly LythonCallableSignature OsStat = LythonCallableSignature.Create("os.stat", ["path"]);
    public static readonly LythonCallableSignature OsLstat = LythonCallableSignature.Create("os.lstat", ["path"]);
    public static readonly LythonCallableSignature OsScandir = LythonCallableSignature.Create("os.scandir", ["path"], requiredCount: 0);
    public static readonly LythonCallableSignature OsMkdir = LythonCallableSignature.Create("os.mkdir", ["path"]);
    public static readonly LythonCallableSignature OsMakedirs = LythonCallableSignature.Create("os.makedirs", ["path", "exist_ok"], requiredCount: 1);
    public static readonly LythonCallableSignature OsRemove = LythonCallableSignature.Create("os.remove", ["path"]);
    public static readonly LythonCallableSignature OsUnlink = LythonCallableSignature.Create("os.unlink", ["path"]);
    public static readonly LythonCallableSignature OsRename = LythonCallableSignature.Create("os.rename", ["src", "dst"]);
    public static readonly LythonCallableSignature OsReplace = LythonCallableSignature.Create("os.replace", ["src", "dst"]);
    public static readonly LythonCallableSignature OsRmdir = LythonCallableSignature.Create("os.rmdir", ["path"]);
    public static readonly LythonCallableSignature OsRemovedirs = LythonCallableSignature.Create("os.removedirs", ["path"]);

    public static readonly LythonCallableSignature OsPathJoin = LythonCallableSignature.Create("os.path.join", requiredCount: 1);
    public static readonly LythonCallableSignature OsPathSplit = LythonCallableSignature.Create("os.path.split", ["path"]);
    public static readonly LythonCallableSignature OsPathSplitExt = LythonCallableSignature.Create("os.path.splitext", ["path"]);
    public static readonly LythonCallableSignature OsPathBasename = LythonCallableSignature.Create("os.path.basename", ["path"]);
    public static readonly LythonCallableSignature OsPathDirname = LythonCallableSignature.Create("os.path.dirname", ["path"]);
    public static readonly LythonCallableSignature OsPathIsAbs = LythonCallableSignature.Create("os.path.isabs", ["path"]);
    public static readonly LythonCallableSignature OsPathNormPath = LythonCallableSignature.Create("os.path.normpath", ["path"]);
    public static readonly LythonCallableSignature OsPathNormCase = LythonCallableSignature.Create("os.path.normcase", ["path"]);
    public static readonly LythonCallableSignature OsPathAbsPath = LythonCallableSignature.Create("os.path.abspath", ["path"]);
    public static readonly LythonCallableSignature OsPathRelPath = LythonCallableSignature.Create("os.path.relpath", ["path", "start"], requiredCount: 1);
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

    public static readonly LythonCallableSignature Glob = LythonCallableSignature.Create("glob.glob", ["pathname", "root_dir", "dir_fd", "recursive", "include_hidden"], requiredCount: 1, maximumPositionalArgumentCount: 1);
    public static readonly LythonCallableSignature IGlob = LythonCallableSignature.Create("glob.iglob", ["pathname", "root_dir", "dir_fd", "recursive", "include_hidden"], requiredCount: 1, maximumPositionalArgumentCount: 1);
    public static readonly LythonCallableSignature GlobEscape = LythonCallableSignature.Create("glob.escape", ["pathname"]);
    public static readonly LythonCallableSignature GlobHasMagic = LythonCallableSignature.Create("glob.has_magic", ["s"]);
    public static readonly LythonCallableSignature GlobTranslate = LythonCallableSignature.Create("glob.translate", ["pathname", "recursive", "include_hidden", "seps"], requiredCount: 1, maximumPositionalArgumentCount: 1);
    public static readonly LythonCallableSignature Glob0 = LythonCallableSignature.Create("glob.glob0", ["dirname", "basename", "dir_fd", "dironly", "include_hidden"], requiredCount: 2);
    public static readonly LythonCallableSignature Glob1 = LythonCallableSignature.Create("glob.glob1", ["dirname", "pattern"], requiredCount: 2);

    public static readonly LythonCallableSignature FnMatch = LythonCallableSignature.Create("fnmatch.fnmatch", ["name", "pattern"]);
    public static readonly LythonCallableSignature FnMatchCase = LythonCallableSignature.Create("fnmatch.fnmatchcase", ["name", "pattern"]);
    public static readonly LythonCallableSignature FnMatchFilter = LythonCallableSignature.Create("fnmatch.filter", ["names", "pattern"]);
    public static readonly LythonCallableSignature FnMatchTranslate = LythonCallableSignature.Create("fnmatch.translate", ["pattern"]);

    public static readonly LythonCallableSignature DifflibIsLineJunk = LythonCallableSignature.Create("difflib.IS_LINE_JUNK", ["line"]);
    public static readonly LythonCallableSignature DifflibIsCharacterJunk = LythonCallableSignature.Create("difflib.IS_CHARACTER_JUNK", ["ch"]);
    public static readonly LythonCallableSignature DifflibUnifiedDiff = LythonCallableSignature.Create("difflib.unified_diff", ["a", "b", "fromfile", "tofile", "fromfiledate", "tofiledate", "n", "lineterm"], requiredCount: 2);
    public static readonly LythonCallableSignature DifflibContextDiff = LythonCallableSignature.Create("difflib.context_diff", ["a", "b", "fromfile", "tofile", "fromfiledate", "tofiledate", "n", "lineterm"], requiredCount: 2);
    public static readonly LythonCallableSignature DifflibNdiff = LythonCallableSignature.Create("difflib.ndiff", ["a", "b", "linejunk", "charjunk"], requiredCount: 2);
    public static readonly LythonCallableSignature DifflibRestore = LythonCallableSignature.Create("difflib.restore", ["delta", "which"]);
    public static readonly LythonCallableSignature DifflibGetCloseMatches = LythonCallableSignature.Create("difflib.get_close_matches", ["word", "possibilities", "n", "cutoff"], requiredCount: 2);
    public static readonly LythonCallableSignature DifflibDiffBytes = LythonCallableSignature.Create("difflib.diff_bytes", ["dfunc", "a", "b", "fromfile", "tofile", "fromfiledate", "tofiledate", "n", "lineterm"], requiredCount: 3);
    public static readonly LythonCallableSignature DifflibDiffer = LythonCallableSignature.Create("difflib.Differ", ["linejunk", "charjunk"], requiredCount: 0);
    public static readonly LythonCallableSignature DifflibHtmlDiff = LythonCallableSignature.Create("difflib.HtmlDiff", ["tabsize", "wrapcolumn", "linejunk", "charjunk"], requiredCount: 0);
    public static readonly LythonCallableSignature DifflibSequenceMatcher = LythonCallableSignature.Create("difflib.SequenceMatcher", ["isjunk", "a", "b", "autojunk"], requiredCount: 0);

    public static readonly LythonCallableSignature PkgutilModuleInfo = LythonCallableSignature.Create("pkgutil.ModuleInfo", ["module_finder", "name", "ispkg"]);
    public static readonly LythonCallableSignature PkgutilIterModules = LythonCallableSignature.Create("pkgutil.iter_modules", ["path", "prefix"], requiredCount: 0);
    public static readonly LythonCallableSignature PkgutilWalkPackages = LythonCallableSignature.Create("pkgutil.walk_packages", ["path", "prefix", "onerror"], requiredCount: 0);
    public static readonly LythonCallableSignature PkgutilFindLoader = LythonCallableSignature.Create("pkgutil.find_loader", ["fullname"]);
    public static readonly LythonCallableSignature PkgutilGetLoader = LythonCallableSignature.Create("pkgutil.get_loader", ["module_or_name"]);
    public static readonly LythonCallableSignature PkgutilExtendPath = LythonCallableSignature.Create("pkgutil.extend_path", ["path", "name"]);
    public static readonly LythonCallableSignature PkgutilResolveName = LythonCallableSignature.Create("pkgutil.resolve_name", ["name"]);
    public static readonly LythonCallableSignature PkgutilGetImporter = LythonCallableSignature.Create("pkgutil.get_importer", ["path_item"]);
    public static readonly LythonCallableSignature PkgutilIterImporters = LythonCallableSignature.Create("pkgutil.iter_importers", ["fullname"], requiredCount: 0);
    public static readonly LythonCallableSignature PkgutilIterImporterModules = LythonCallableSignature.Create("pkgutil.iter_importer_modules", ["importer", "prefix"], requiredCount: 1);
    public static readonly LythonCallableSignature PkgutilIterZipimportModules = LythonCallableSignature.Create("pkgutil.iter_zipimport_modules", ["importer", "prefix"], requiredCount: 1);
    public static readonly LythonCallableSignature PkgutilGetData = LythonCallableSignature.Create("pkgutil.get_data", ["package", "resource"]);
    public static readonly LythonCallableSignature PkgutilReadCode = LythonCallableSignature.Create("pkgutil.read_code", ["stream"]);

    public static readonly LythonCallableSignature ImportlibImportModule = LythonCallableSignature.Create("importlib.import_module", ["name", "package"], requiredCount: 1);
    public static readonly LythonCallableSignature ImportlibInvalidateCaches = LythonCallableSignature.Create("importlib.invalidate_caches", []);
    public static readonly LythonCallableSignature ImportlibUtilFindSpec = LythonCallableSignature.Create("importlib.util.find_spec", ["name", "package"], requiredCount: 1);
    public static readonly LythonCallableSignature ImportlibUtilResolveName = LythonCallableSignature.Create("importlib.util.resolve_name", ["name", "package"]);
    public static readonly LythonCallableSignature ImportlibUtilModuleFromSpec = LythonCallableSignature.Create("importlib.util.module_from_spec", ["spec"]);
    public static readonly LythonCallableSignature ImportlibUtilSpecFromFileLocation = LythonCallableSignature.Create("importlib.util.spec_from_file_location", ["name", "location", "loader", "submodule_search_locations"], requiredCount: 2, maximumPositionalArgumentCount: 2);
    public static readonly LythonCallableSignature ImportlibUtilSpecFromLoader = LythonCallableSignature.Create("importlib.util.spec_from_loader", ["name", "loader", "origin", "is_package"], requiredCount: 2, maximumPositionalArgumentCount: 2);

    public static readonly LythonCallableSignature FilecmpCmp = LythonCallableSignature.Create("filecmp.cmp", ["f1", "f2", "shallow"], requiredCount: 2);
    public static readonly LythonCallableSignature FilecmpClearCache = LythonCallableSignature.Create("filecmp.clear_cache", []);
    public static readonly LythonCallableSignature FilecmpDircmp = LythonCallableSignature.Create("filecmp.dircmp", ["a", "b", "ignore", "hide"], requiredCount: 2);

    public static readonly LythonCallableSignature HashlibMd5 = LythonCallableSignature.Create("hashlib.md5", ["string", "usedforsecurity"], requiredCount: 0, maximumPositionalArgumentCount: 1);
    public static readonly LythonCallableSignature HashlibSha1 = LythonCallableSignature.Create("hashlib.sha1", ["string", "usedforsecurity"], requiredCount: 0, maximumPositionalArgumentCount: 1);
    public static readonly LythonCallableSignature HashlibSha256 = LythonCallableSignature.Create("hashlib.sha256", ["string", "usedforsecurity"], requiredCount: 0, maximumPositionalArgumentCount: 1);
    public static readonly LythonCallableSignature HashlibSha384 = LythonCallableSignature.Create("hashlib.sha384", ["string", "usedforsecurity"], requiredCount: 0, maximumPositionalArgumentCount: 1);
    public static readonly LythonCallableSignature HashlibSha512 = LythonCallableSignature.Create("hashlib.sha512", ["string", "usedforsecurity"], requiredCount: 0, maximumPositionalArgumentCount: 1);
    public static readonly LythonCallableSignature HashlibNew = LythonCallableSignature.Create("hashlib.new", ["name", "data", "usedforsecurity"], requiredCount: 1, maximumPositionalArgumentCount: 2);
    public static readonly LythonCallableSignature HashlibFileDigest = LythonCallableSignature.Create("hashlib.file_digest", ["fileobj", "digest", "_bufsize"], requiredCount: 2, maximumPositionalArgumentCount: 2);

    public static readonly LythonCallableSignature GzipCompress = LythonCallableSignature.Create("gzip.compress", ["data", "compresslevel", "mtime"], requiredCount: 1, maximumPositionalArgumentCount: 2);
    public static readonly LythonCallableSignature GzipDecompress = LythonCallableSignature.Create("gzip.decompress", ["data"]);
    public static readonly LythonCallableSignature GzipOpen = LythonCallableSignature.Create("gzip.open", ["filename", "mode", "compresslevel", "encoding", "errors", "newline"], requiredCount: 1);

    public static readonly LythonCallableSignature ShlexQuote = LythonCallableSignature.Create("shlex.quote", ["s"]);
    public static readonly LythonCallableSignature ShlexJoin = LythonCallableSignature.Create("shlex.join", ["split_command"]);
    public static readonly LythonCallableSignature ShlexSplit = LythonCallableSignature.Create("shlex.split", ["s", "comments", "posix"], requiredCount: 1);
    public static readonly LythonCallableSignature ShlexClass = LythonCallableSignature.Create("shlex.shlex", ["instream", "infile", "posix", "punctuation_chars"], requiredCount: 0);

    public static readonly LythonCallableSignature TimeTime = LythonCallableSignature.Create("time.time", []);
    public static readonly LythonCallableSignature TimeTimeNs = LythonCallableSignature.Create("time.time_ns", []);
    public static readonly LythonCallableSignature TimeMonotonic = LythonCallableSignature.Create("time.monotonic", []);
    public static readonly LythonCallableSignature TimeMonotonicNs = LythonCallableSignature.Create("time.monotonic_ns", []);
    public static readonly LythonCallableSignature TimePerfCounter = LythonCallableSignature.Create("time.perf_counter", []);
    public static readonly LythonCallableSignature TimePerfCounterNs = LythonCallableSignature.Create("time.perf_counter_ns", []);
    public static readonly LythonCallableSignature TimeSleep = LythonCallableSignature.Create("time.sleep", ["secs"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
    public static readonly LythonCallableSignature TimeGetClockInfo = LythonCallableSignature.Create("time.get_clock_info", ["name"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
    public static readonly LythonCallableSignature TimeProcessTime = LythonCallableSignature.Create("time.process_time", []);
    public static readonly LythonCallableSignature TimeProcessTimeNs = LythonCallableSignature.Create("time.process_time_ns", []);
    public static readonly LythonCallableSignature TimeThreadTime = LythonCallableSignature.Create("time.thread_time", []);
    public static readonly LythonCallableSignature TimeThreadTimeNs = LythonCallableSignature.Create("time.thread_time_ns", []);
    public static readonly LythonCallableSignature TimeClockGetTime = LythonCallableSignature.Create("time.clock_gettime", ["clk_id"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
    public static readonly LythonCallableSignature TimeClockGetTimeNs = LythonCallableSignature.Create("time.clock_gettime_ns", ["clk_id"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
    public static readonly LythonCallableSignature TimeClockGetRes = LythonCallableSignature.Create("time.clock_getres", ["clk_id"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
    public static readonly LythonCallableSignature TimeClockSetTime = LythonCallableSignature.Create("time.clock_settime", ["clk_id", "time"], requiredCount: 2, maximumPositionalArgumentCount: 2, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 2);
    public static readonly LythonCallableSignature TimeClockSetTimeNs = LythonCallableSignature.Create("time.clock_settime_ns", ["clk_id", "time_ns"], requiredCount: 2, maximumPositionalArgumentCount: 2, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 2);
    public static readonly LythonCallableSignature TimePthreadGetCpuClockId = LythonCallableSignature.Create("time.pthread_getcpuclockid", ["thread_id"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
    public static readonly LythonCallableSignature TimeGmtime = LythonCallableSignature.Create("time.gmtime", ["secs"], requiredCount: 0);
    public static readonly LythonCallableSignature TimeLocaltime = LythonCallableSignature.Create("time.localtime", ["secs"], requiredCount: 0);
    public static readonly LythonCallableSignature TimeCtime = LythonCallableSignature.Create("time.ctime", ["secs"], requiredCount: 0);
    public static readonly LythonCallableSignature TimeMktime = LythonCallableSignature.Create("time.mktime", ["t"]);
    public static readonly LythonCallableSignature TimeAsctime = LythonCallableSignature.Create("time.asctime", ["t"], requiredCount: 0);
    public static readonly LythonCallableSignature TimeStrftime = LythonCallableSignature.Create("time.strftime", ["format", "t"], requiredCount: 1);
    public static readonly LythonCallableSignature TimeStrptime = LythonCallableSignature.Create("time.strptime", ["string", "format"], requiredCount: 1);
    public static readonly LythonCallableSignature TimeStructTime = LythonCallableSignature.Create("time.struct_time", ["sequence"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
    public static readonly LythonCallableSignature TimeTzset = LythonCallableSignature.Create("time.tzset", []);

    public static readonly LythonCallableSignature ItertoolsChain = LythonCallableSignature.Create("itertools.chain", requiredCount: 0);
    public static readonly LythonCallableSignature ItertoolsCount = LythonCallableSignature.Create("itertools.count", ["start", "step"], requiredCount: 0);
    public static readonly LythonCallableSignature ItertoolsRepeat = LythonCallableSignature.Create("itertools.repeat", ["object", "times"], requiredCount: 1);
    public static readonly LythonCallableSignature ItertoolsCycle = LythonCallableSignature.Create("itertools.cycle", ["iterable"]);
    public static readonly LythonCallableSignature ItertoolsIslice = LythonCallableSignature.Create("itertools.islice", ["iterable", "start", "stop", "step"], requiredCount: 2);
    public static readonly LythonCallableSignature ItertoolsCombinations = LythonCallableSignature.Create("itertools.combinations", ["iterable", "r"]);
    public static readonly LythonCallableSignature ItertoolsCombinationsWithReplacement = LythonCallableSignature.Create("itertools.combinations_with_replacement", ["iterable", "r"]);
    public static readonly LythonCallableSignature ItertoolsPermutations = LythonCallableSignature.Create("itertools.permutations", ["iterable", "r"], requiredCount: 1);
    public static readonly LythonCallableSignature ItertoolsAccumulate = LythonCallableSignature.Create("itertools.accumulate", ["iterable", "func", "initial"], requiredCount: 1, maximumPositionalArgumentCount: 2);
    public static readonly LythonCallableSignature ItertoolsCompress = LythonCallableSignature.Create("itertools.compress", ["data", "selectors"]);
    public static readonly LythonCallableSignature ItertoolsFilterFalse = LythonCallableSignature.Create("itertools.filterfalse", ["function", "iterable"]);
    public static readonly LythonCallableSignature ItertoolsDropWhile = LythonCallableSignature.Create("itertools.dropwhile", ["predicate", "iterable"]);
    public static readonly LythonCallableSignature ItertoolsTakeWhile = LythonCallableSignature.Create("itertools.takewhile", ["predicate", "iterable"]);
    public static readonly LythonCallableSignature ItertoolsStarmap = LythonCallableSignature.Create("itertools.starmap", ["function", "iterable"]);
    public static readonly LythonCallableSignature ItertoolsPairwise = LythonCallableSignature.Create("itertools.pairwise", ["iterable"]);
    public static readonly LythonCallableSignature ItertoolsGroupBy = LythonCallableSignature.Create("itertools.groupby", ["iterable", "key"], requiredCount: 1);
    public static readonly LythonCallableSignature ItertoolsTee = LythonCallableSignature.Create("itertools.tee", ["iterable", "n"], requiredCount: 1);
    public static readonly LythonCallableSignature ItertoolsBatched = LythonCallableSignature.Create("itertools.batched", ["iterable", "n", "strict"], requiredCount: 2, maximumPositionalArgumentCount: 2);
}

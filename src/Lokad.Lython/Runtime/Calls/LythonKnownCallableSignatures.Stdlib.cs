namespace Lokad.Lython.Runtime;

internal static partial class LythonKnownCallableSignatures
{
    internal static class Os
    {
        public static readonly LythonCallableSignature ListDir = LythonCallableSignature.Create("os.listdir", ["path"], requiredCount: 0);
        public static readonly LythonCallableSignature Walk = LythonCallableSignature.Create("os.walk", ["top", "topdown", "onerror", "followlinks"], requiredCount: 0);
        public static readonly LythonCallableSignature GetCwd = LythonCallableSignature.Create("os.getcwd", []);
        public static readonly LythonCallableSignature Fspath = LythonCallableSignature.Create("os.fspath", ["path"]);
        public static readonly LythonCallableSignature FsEncode = LythonCallableSignature.Create("os.fsencode", ["filename"]);
        public static readonly LythonCallableSignature FsDecode = LythonCallableSignature.Create("os.fsdecode", ["filename"]);
        public static readonly LythonCallableSignature GetEnv = LythonCallableSignature.Create("os.getenv", ["key", "default"], requiredCount: 1);
        public static readonly LythonCallableSignature PutEnv = LythonCallableSignature.Create("os.putenv", ["key", "value"]);
        public static readonly LythonCallableSignature UnsetEnv = LythonCallableSignature.Create("os.unsetenv", ["key"]);
        public static readonly LythonCallableSignature GetExecPath = LythonCallableSignature.Create("os.get_exec_path", ["env"], requiredCount: 0);
        public static readonly LythonCallableSignature Stat = LythonCallableSignature.Create("os.stat", ["path"]);
        public static readonly LythonCallableSignature Lstat = LythonCallableSignature.Create("os.lstat", ["path"]);
        public static readonly LythonCallableSignature Scandir = LythonCallableSignature.Create("os.scandir", ["path"], requiredCount: 0);
        public static readonly LythonCallableSignature Mkdir = LythonCallableSignature.Create("os.mkdir", ["path"]);
        public static readonly LythonCallableSignature Makedirs = LythonCallableSignature.Create("os.makedirs", ["path", "exist_ok"], requiredCount: 1);
        public static readonly LythonCallableSignature Remove = LythonCallableSignature.Create("os.remove", ["path"]);
        public static readonly LythonCallableSignature Unlink = LythonCallableSignature.Create("os.unlink", ["path"]);
        public static readonly LythonCallableSignature Rename = LythonCallableSignature.Create("os.rename", ["src", "dst"]);
        public static readonly LythonCallableSignature Replace = LythonCallableSignature.Create("os.replace", ["src", "dst"]);
        public static readonly LythonCallableSignature Rmdir = LythonCallableSignature.Create("os.rmdir", ["path"]);
        public static readonly LythonCallableSignature Removedirs = LythonCallableSignature.Create("os.removedirs", ["path"]);
        public static readonly LythonCallableSignature PathJoin = LythonCallableSignature.Create("os.path.join", requiredCount: 1);
        public static readonly LythonCallableSignature PathSplit = LythonCallableSignature.Create("os.path.split", ["path"]);
        public static readonly LythonCallableSignature PathSplitExt = LythonCallableSignature.Create("os.path.splitext", ["path"]);
        public static readonly LythonCallableSignature PathBasename = LythonCallableSignature.Create("os.path.basename", ["path"]);
        public static readonly LythonCallableSignature PathDirname = LythonCallableSignature.Create("os.path.dirname", ["path"]);
        public static readonly LythonCallableSignature PathIsAbs = LythonCallableSignature.Create("os.path.isabs", ["path"]);
        public static readonly LythonCallableSignature PathNormPath = LythonCallableSignature.Create("os.path.normpath", ["path"]);
        public static readonly LythonCallableSignature PathNormCase = LythonCallableSignature.Create("os.path.normcase", ["path"]);
        public static readonly LythonCallableSignature PathAbsPath = LythonCallableSignature.Create("os.path.abspath", ["path"]);
        public static readonly LythonCallableSignature PathRelPath = LythonCallableSignature.Create("os.path.relpath", ["path", "start"], requiredCount: 1);
        public static readonly LythonCallableSignature PathCommonPath = LythonCallableSignature.Create("os.path.commonpath", ["paths"]);
        public static readonly LythonCallableSignature PathCommonPrefix = LythonCallableSignature.Create("os.path.commonprefix", ["list"]);
        public static readonly LythonCallableSignature PathExists = LythonCallableSignature.Create("os.path.exists", ["path"]);
        public static readonly LythonCallableSignature PathLexists = LythonCallableSignature.Create("os.path.lexists", ["path"]);
        public static readonly LythonCallableSignature PathIsFile = LythonCallableSignature.Create("os.path.isfile", ["path"]);
        public static readonly LythonCallableSignature PathIsDir = LythonCallableSignature.Create("os.path.isdir", ["path"]);
        public static readonly LythonCallableSignature PathGetSize = LythonCallableSignature.Create("os.path.getsize", ["path"]);
        public static readonly LythonCallableSignature PathGetMTime = LythonCallableSignature.Create("os.path.getmtime", ["path"]);
        public static readonly LythonCallableSignature PathGetATime = LythonCallableSignature.Create("os.path.getatime", ["path"]);
        public static readonly LythonCallableSignature PathGetCTime = LythonCallableSignature.Create("os.path.getctime", ["path"]);
        public static readonly LythonCallableSignature PathSameFile = LythonCallableSignature.Create("os.path.samefile", ["path1", "path2"]);
        public static readonly LythonCallableSignature PathRealPath = LythonCallableSignature.Create("os.path.realpath", ["path"]);
        public static readonly LythonCallableSignature PathExpandVars = LythonCallableSignature.Create("os.path.expandvars", ["path"]);
        public static readonly LythonCallableSignature PathSplitDrive = LythonCallableSignature.Create("os.path.splitdrive", ["path"]);
        public static readonly LythonCallableSignature PathSplitRoot = LythonCallableSignature.Create("os.path.splitroot", ["path"]);
        public static readonly LythonCallableSignature PathIsMount = LythonCallableSignature.Create("os.path.ismount", ["path"]);
    }

    internal static class Glob
    {
        public static readonly LythonCallableSignature Find = LythonCallableSignature.Create("glob.glob", ["pathname", "root_dir", "dir_fd", "recursive", "include_hidden"], requiredCount: 1, maximumPositionalArgumentCount: 1);
        public static readonly LythonCallableSignature IGlob = LythonCallableSignature.Create("glob.iglob", ["pathname", "root_dir", "dir_fd", "recursive", "include_hidden"], requiredCount: 1, maximumPositionalArgumentCount: 1);
        public static readonly LythonCallableSignature Escape = LythonCallableSignature.Create("glob.escape", ["pathname"]);
        public static readonly LythonCallableSignature HasMagic = LythonCallableSignature.Create("glob.has_magic", ["s"]);
        public static readonly LythonCallableSignature Translate = LythonCallableSignature.Create("glob.translate", ["pathname", "recursive", "include_hidden", "seps"], requiredCount: 1, maximumPositionalArgumentCount: 1);
        public static readonly LythonCallableSignature Glob0 = LythonCallableSignature.Create("glob.glob0", ["dirname", "basename", "dir_fd", "dironly", "include_hidden"], requiredCount: 2);
        public static readonly LythonCallableSignature Glob1 = LythonCallableSignature.Create("glob.glob1", ["dirname", "pattern"], requiredCount: 2);
    }

    internal static class Fnmatch
    {
        public static readonly LythonCallableSignature FnMatch = LythonCallableSignature.Create("fnmatch.fnmatch", ["name", "pattern"]);
        public static readonly LythonCallableSignature FnMatchCase = LythonCallableSignature.Create("fnmatch.fnmatchcase", ["name", "pattern"]);
        public static readonly LythonCallableSignature FnMatchFilter = LythonCallableSignature.Create("fnmatch.filter", ["names", "pattern"]);
        public static readonly LythonCallableSignature FnMatchTranslate = LythonCallableSignature.Create("fnmatch.translate", ["pattern"]);
    }

    internal static class Textwrap
    {
        public static readonly LythonCallableSignature Dedent = LythonCallableSignature.Create("textwrap.dedent", ["text"]);
        public static readonly LythonCallableSignature Indent = LythonCallableSignature.Create("textwrap.indent", ["text", "prefix", "predicate"], requiredCount: 2);
    }

    internal static class Html
    {
        public static readonly LythonCallableSignature Escape = LythonCallableSignature.Create("html.escape", ["s", "quote"], requiredCount: 1);
        public static readonly LythonCallableSignature Unescape = LythonCallableSignature.Create("html.unescape", ["s"]);
    }

    internal static class UrlLib
    {
        public static readonly LythonCallableSignature UrlSplit = LythonCallableSignature.Create("urllib.parse.urlsplit", ["url", "scheme", "allow_fragments"], requiredCount: 1);
        public static readonly LythonCallableSignature UrlParse = LythonCallableSignature.Create("urllib.parse.urlparse", ["url", "scheme", "allow_fragments"], requiredCount: 1);
        public static readonly LythonCallableSignature UrlUnsplit = LythonCallableSignature.Create("urllib.parse.urlunsplit", ["components"]);
        public static readonly LythonCallableSignature UrlUnparse = LythonCallableSignature.Create("urllib.parse.urlunparse", ["components"]);
        public static readonly LythonCallableSignature UrlQuote = LythonCallableSignature.Create("urllib.parse.quote", ["string", "safe", "encoding", "errors"], requiredCount: 1);
        public static readonly LythonCallableSignature UrlQuotePlus = LythonCallableSignature.Create("urllib.parse.quote_plus", ["string", "safe", "encoding", "errors"], requiredCount: 1);
        public static readonly LythonCallableSignature UrlQuoteFromBytes = LythonCallableSignature.Create("urllib.parse.quote_from_bytes", ["bs", "safe"], requiredCount: 1);
        public static readonly LythonCallableSignature UrlUnquote = LythonCallableSignature.Create("urllib.parse.unquote", ["string", "encoding", "errors"], requiredCount: 1);
        public static readonly LythonCallableSignature UrlUnquotePlus = LythonCallableSignature.Create("urllib.parse.unquote_plus", ["string", "encoding", "errors"], requiredCount: 1);
        public static readonly LythonCallableSignature UrlUnquoteToBytes = LythonCallableSignature.Create("urllib.parse.unquote_to_bytes", ["string"]);
        public static readonly LythonCallableSignature UrlEncode = LythonCallableSignature.Create("urllib.parse.urlencode", ["query", "doseq", "safe", "encoding", "errors", "quote_via"], requiredCount: 1);
        public static readonly LythonCallableSignature Qs = LythonCallableSignature.Create("urllib.parse.parse_qs", ["qs", "keep_blank_values", "strict_parsing", "encoding", "errors", "max_num_fields", "separator"], requiredCount: 1);
        public static readonly LythonCallableSignature Qsl = LythonCallableSignature.Create("urllib.parse.parse_qsl", ["qs", "keep_blank_values", "strict_parsing", "encoding", "errors", "max_num_fields", "separator"], requiredCount: 1);
    }

    internal static class Difflib
    {
        public static readonly LythonCallableSignature IsLineJunk = LythonCallableSignature.Create("difflib.IS_LINE_JUNK", ["line"]);
        public static readonly LythonCallableSignature IsCharacterJunk = LythonCallableSignature.Create("difflib.IS_CHARACTER_JUNK", ["ch"]);
        public static readonly LythonCallableSignature UnifiedDiff = LythonCallableSignature.Create("difflib.unified_diff", ["a", "b", "fromfile", "tofile", "fromfiledate", "tofiledate", "n", "lineterm"], requiredCount: 2);
        public static readonly LythonCallableSignature ContextDiff = LythonCallableSignature.Create("difflib.context_diff", ["a", "b", "fromfile", "tofile", "fromfiledate", "tofiledate", "n", "lineterm"], requiredCount: 2);
        public static readonly LythonCallableSignature Ndiff = LythonCallableSignature.Create("difflib.ndiff", ["a", "b", "linejunk", "charjunk"], requiredCount: 2);
        public static readonly LythonCallableSignature Restore = LythonCallableSignature.Create("difflib.restore", ["delta", "which"]);
        public static readonly LythonCallableSignature GetCloseMatches = LythonCallableSignature.Create("difflib.get_close_matches", ["word", "possibilities", "n", "cutoff"], requiredCount: 2);
        public static readonly LythonCallableSignature DiffBytes = LythonCallableSignature.Create("difflib.diff_bytes", ["dfunc", "a", "b", "fromfile", "tofile", "fromfiledate", "tofiledate", "n", "lineterm"], requiredCount: 3);
        public static readonly LythonCallableSignature Differ = LythonCallableSignature.Create("difflib.Differ", ["linejunk", "charjunk"], requiredCount: 0);
        public static readonly LythonCallableSignature HtmlDiff = LythonCallableSignature.Create("difflib.HtmlDiff", ["tabsize", "wrapcolumn", "linejunk", "charjunk"], requiredCount: 0);
        public static readonly LythonCallableSignature Match = LythonCallableSignature.Create("difflib.Match", ["a", "b", "size"]);
        public static readonly LythonCallableSignature SequenceMatcher = LythonCallableSignature.Create("difflib.SequenceMatcher", ["isjunk", "a", "b", "autojunk"], requiredCount: 0);
    }

    internal static class Pkgutil
    {
        public static readonly LythonCallableSignature ModuleInfo = LythonCallableSignature.Create("pkgutil.ModuleInfo", ["module_finder", "name", "ispkg"]);
        public static readonly LythonCallableSignature IterModules = LythonCallableSignature.Create("pkgutil.iter_modules", ["path", "prefix"], requiredCount: 0);
        public static readonly LythonCallableSignature WalkPackages = LythonCallableSignature.Create("pkgutil.walk_packages", ["path", "prefix", "onerror"], requiredCount: 0);
        public static readonly LythonCallableSignature FindLoader = LythonCallableSignature.Create("pkgutil.find_loader", ["fullname"]);
        public static readonly LythonCallableSignature GetLoader = LythonCallableSignature.Create("pkgutil.get_loader", ["module_or_name"]);
        public static readonly LythonCallableSignature ExtendPath = LythonCallableSignature.Create("pkgutil.extend_path", ["path", "name"]);
        public static readonly LythonCallableSignature ResolveName = LythonCallableSignature.Create("pkgutil.resolve_name", ["name"]);
        public static readonly LythonCallableSignature GetImporter = LythonCallableSignature.Create("pkgutil.get_importer", ["path_item"]);
        public static readonly LythonCallableSignature IterImporters = LythonCallableSignature.Create("pkgutil.iter_importers", ["fullname"], requiredCount: 0);
        public static readonly LythonCallableSignature IterImporterModules = LythonCallableSignature.Create("pkgutil.iter_importer_modules", ["importer", "prefix"], requiredCount: 1);
        public static readonly LythonCallableSignature IterZipimportModules = LythonCallableSignature.Create("pkgutil.iter_zipimport_modules", ["importer", "prefix"], requiredCount: 1);
        public static readonly LythonCallableSignature GetData = LythonCallableSignature.Create("pkgutil.get_data", ["package", "resource"]);
        public static readonly LythonCallableSignature ReadCode = LythonCallableSignature.Create("pkgutil.read_code", ["stream"]);
    }

    internal static class Importlib
    {
        public static readonly LythonCallableSignature ImportModule = LythonCallableSignature.Create("importlib.import_module", ["name", "package"], requiredCount: 1);
        public static readonly LythonCallableSignature InvalidateCaches = LythonCallableSignature.Create("importlib.invalidate_caches", []);
        public static readonly LythonCallableSignature UtilFindSpec = LythonCallableSignature.Create("importlib.util.find_spec", ["name", "package"], requiredCount: 1);
        public static readonly LythonCallableSignature UtilResolveName = LythonCallableSignature.Create("importlib.util.resolve_name", ["name", "package"]);
        public static readonly LythonCallableSignature UtilModuleFromSpec = LythonCallableSignature.Create("importlib.util.module_from_spec", ["spec"]);
        public static readonly LythonCallableSignature UtilSpecFromFileLocation = LythonCallableSignature.Create("importlib.util.spec_from_file_location", ["name", "location", "loader", "submodule_search_locations"], requiredCount: 2, maximumPositionalArgumentCount: 2);
        public static readonly LythonCallableSignature UtilSpecFromLoader = LythonCallableSignature.Create("importlib.util.spec_from_loader", ["name", "loader", "origin", "is_package"], requiredCount: 2, maximumPositionalArgumentCount: 2);
    }

    internal static class Filecmp
    {
        public static readonly LythonCallableSignature Cmp = LythonCallableSignature.Create("filecmp.cmp", ["f1", "f2", "shallow"], requiredCount: 2);
        public static readonly LythonCallableSignature ClearCache = LythonCallableSignature.Create("filecmp.clear_cache", []);
        public static readonly LythonCallableSignature Dircmp = LythonCallableSignature.Create("filecmp.dircmp", ["a", "b", "ignore", "hide"], requiredCount: 2);
    }

    internal static class Hashlib
    {
        public static readonly LythonCallableSignature Md5 = LythonCallableSignature.Create("hashlib.md5", ["string", "usedforsecurity"], requiredCount: 0, maximumPositionalArgumentCount: 1);
        public static readonly LythonCallableSignature Sha1 = LythonCallableSignature.Create("hashlib.sha1", ["string", "usedforsecurity"], requiredCount: 0, maximumPositionalArgumentCount: 1);
        public static readonly LythonCallableSignature Sha256 = LythonCallableSignature.Create("hashlib.sha256", ["string", "usedforsecurity"], requiredCount: 0, maximumPositionalArgumentCount: 1);
        public static readonly LythonCallableSignature Sha384 = LythonCallableSignature.Create("hashlib.sha384", ["string", "usedforsecurity"], requiredCount: 0, maximumPositionalArgumentCount: 1);
        public static readonly LythonCallableSignature Sha512 = LythonCallableSignature.Create("hashlib.sha512", ["string", "usedforsecurity"], requiredCount: 0, maximumPositionalArgumentCount: 1);
        public static readonly LythonCallableSignature New = LythonCallableSignature.Create("hashlib.new", ["name", "data", "usedforsecurity"], requiredCount: 1, maximumPositionalArgumentCount: 2);
        public static readonly LythonCallableSignature FileDigest = LythonCallableSignature.Create("hashlib.file_digest", ["fileobj", "digest", "_bufsize"], requiredCount: 2, maximumPositionalArgumentCount: 2);
    }

    internal static class Gzip
    {
        public static readonly LythonCallableSignature Compress = LythonCallableSignature.Create("gzip.compress", ["data", "compresslevel", "mtime"], requiredCount: 1, maximumPositionalArgumentCount: 2);
        public static readonly LythonCallableSignature Decompress = LythonCallableSignature.Create("gzip.decompress", ["data"]);
        public static readonly LythonCallableSignature Open = LythonCallableSignature.Create("gzip.open", ["filename", "mode", "compresslevel", "encoding", "errors", "newline"], requiredCount: 1);
        public static readonly LythonCallableSignature File = LythonCallableSignature.Create("gzip.GzipFile", ["filename", "mode", "compresslevel", "fileobj", "mtime"], requiredCount: 0);
    }

    internal static class Struct
    {
        public static readonly LythonCallableSignature Pack = LythonCallableSignature.Create("struct.pack",
            ["format"], 1, ArgumentCountLimit.Unbounded, LythonVariadicParameters.Positional, 1);
        public static readonly LythonCallableSignature Unpack = LythonCallableSignature.Create("struct.unpack",
            ["format", "buffer"], 2, 2, LythonVariadicParameters.None, 2);
        public static readonly LythonCallableSignature Calcsize = LythonCallableSignature.Create("struct.calcsize",
            ["format"], 1, 1, LythonVariadicParameters.None, 1);
        public static readonly LythonCallableSignature IterUnpack = LythonCallableSignature.Create("struct.iter_unpack",
            ["format", "buffer"], 2, 2, LythonVariadicParameters.None, 2);
    }

    internal static class Zipfile
    {
        public static readonly LythonCallableSignature ZipFile = LythonCallableSignature.Create("zipfile.ZipFile", ["file", "mode", "compression", "allowZip64", "compresslevel", "strict_timestamps", "metadata_encoding"], requiredCount: 1);
        public static readonly LythonCallableSignature ZipIsZipFile = LythonCallableSignature.Create("zipfile.is_zipfile", ["filename"], requiredCount: 1);
        public static readonly LythonCallableSignature ZipInfo = LythonCallableSignature.Create("zipfile.ZipInfo", ["filename", "date_time"], requiredCount: 0);
    }

    internal static class Shlex
    {
        public static readonly LythonCallableSignature Quote = LythonCallableSignature.Create("shlex.quote", ["s"]);
        public static readonly LythonCallableSignature Join = LythonCallableSignature.Create("shlex.join", ["split_command"]);
        public static readonly LythonCallableSignature Split = LythonCallableSignature.Create("shlex.split", ["s", "comments", "posix"], requiredCount: 1);
        public static readonly LythonCallableSignature Class = LythonCallableSignature.Create("shlex.shlex", ["instream", "infile", "posix", "punctuation_chars"], requiredCount: 0);
    }

    internal static class Time
    {
        public static readonly LythonCallableSignature TimeTime = LythonCallableSignature.Create("time.time", []);
        public static readonly LythonCallableSignature TimeNs = LythonCallableSignature.Create("time.time_ns", []);
        public static readonly LythonCallableSignature Monotonic = LythonCallableSignature.Create("time.monotonic", []);
        public static readonly LythonCallableSignature MonotonicNs = LythonCallableSignature.Create("time.monotonic_ns", []);
        public static readonly LythonCallableSignature PerfCounter = LythonCallableSignature.Create("time.perf_counter", []);
        public static readonly LythonCallableSignature PerfCounterNs = LythonCallableSignature.Create("time.perf_counter_ns", []);
        public static readonly LythonCallableSignature Sleep = LythonCallableSignature.Create("time.sleep", ["secs"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
        public static readonly LythonCallableSignature GetClockInfo = LythonCallableSignature.Create("time.get_clock_info", ["name"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
        public static readonly LythonCallableSignature ProcessTime = LythonCallableSignature.Create("time.process_time", []);
        public static readonly LythonCallableSignature ProcessTimeNs = LythonCallableSignature.Create("time.process_time_ns", []);
        public static readonly LythonCallableSignature ThreadTime = LythonCallableSignature.Create("time.thread_time", []);
        public static readonly LythonCallableSignature ThreadTimeNs = LythonCallableSignature.Create("time.thread_time_ns", []);
        public static readonly LythonCallableSignature ClockGetTime = LythonCallableSignature.Create("time.clock_gettime", ["clk_id"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
        public static readonly LythonCallableSignature ClockGetTimeNs = LythonCallableSignature.Create("time.clock_gettime_ns", ["clk_id"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
        public static readonly LythonCallableSignature ClockGetRes = LythonCallableSignature.Create("time.clock_getres", ["clk_id"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
        public static readonly LythonCallableSignature ClockSetTime = LythonCallableSignature.Create("time.clock_settime", ["clk_id", "time"], requiredCount: 2, maximumPositionalArgumentCount: 2, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 2);
        public static readonly LythonCallableSignature ClockSetTimeNs = LythonCallableSignature.Create("time.clock_settime_ns", ["clk_id", "time_ns"], requiredCount: 2, maximumPositionalArgumentCount: 2, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 2);
        public static readonly LythonCallableSignature PthreadGetCpuClockId = LythonCallableSignature.Create("time.pthread_getcpuclockid", ["thread_id"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
        public static readonly LythonCallableSignature Gmtime = LythonCallableSignature.Create("time.gmtime", ["secs"], requiredCount: 0);
        public static readonly LythonCallableSignature Localtime = LythonCallableSignature.Create("time.localtime", ["secs"], requiredCount: 0);
        public static readonly LythonCallableSignature Ctime = LythonCallableSignature.Create("time.ctime", ["secs"], requiredCount: 0);
        public static readonly LythonCallableSignature Mktime = LythonCallableSignature.Create("time.mktime", ["t"]);
        public static readonly LythonCallableSignature Asctime = LythonCallableSignature.Create("time.asctime", ["t"], requiredCount: 0);
        public static readonly LythonCallableSignature Strftime = LythonCallableSignature.Create("time.strftime", ["format", "t"], requiredCount: 1);
        public static readonly LythonCallableSignature Strptime = LythonCallableSignature.Create("time.strptime", ["string", "format"], requiredCount: 1);
        public static readonly LythonCallableSignature StructTime = LythonCallableSignature.Create("time.struct_time", ["sequence"], requiredCount: 1, maximumPositionalArgumentCount: 1, variadicParameters: LythonVariadicParameters.None, positionalOnlyCount: 1);
        public static readonly LythonCallableSignature Tzset = LythonCallableSignature.Create("time.tzset", []);
    }

    internal static class Itertools
    {
        public static readonly LythonCallableSignature Chain = LythonCallableSignature.Create("itertools.chain", requiredCount: 0);
        public static readonly LythonCallableSignature Count = LythonCallableSignature.Create("itertools.count", ["start", "step"], requiredCount: 0);
        public static readonly LythonCallableSignature Repeat = LythonCallableSignature.Create("itertools.repeat", ["object", "times"], requiredCount: 1);
        public static readonly LythonCallableSignature Cycle = LythonCallableSignature.Create("itertools.cycle", ["iterable"]);
        public static readonly LythonCallableSignature Islice = LythonCallableSignature.Create("itertools.islice", ["iterable", "start", "stop", "step"], requiredCount: 2);
        public static readonly LythonCallableSignature Combinations = LythonCallableSignature.Create("itertools.combinations", ["iterable", "r"]);
        public static readonly LythonCallableSignature CombinationsWithReplacement = LythonCallableSignature.Create("itertools.combinations_with_replacement", ["iterable", "r"]);
        public static readonly LythonCallableSignature Permutations = LythonCallableSignature.Create("itertools.permutations", ["iterable", "r"], requiredCount: 1);
        public static readonly LythonCallableSignature Accumulate = LythonCallableSignature.Create("itertools.accumulate", ["iterable", "func", "initial"], requiredCount: 1, maximumPositionalArgumentCount: 2);
        public static readonly LythonCallableSignature Compress = LythonCallableSignature.Create("itertools.compress", ["data", "selectors"]);
        public static readonly LythonCallableSignature FilterFalse = LythonCallableSignature.Create("itertools.filterfalse", ["function", "iterable"]);
        public static readonly LythonCallableSignature DropWhile = LythonCallableSignature.Create("itertools.dropwhile", ["predicate", "iterable"]);
        public static readonly LythonCallableSignature TakeWhile = LythonCallableSignature.Create("itertools.takewhile", ["predicate", "iterable"]);
        public static readonly LythonCallableSignature Starmap = LythonCallableSignature.Create("itertools.starmap", ["function", "iterable"]);
        public static readonly LythonCallableSignature Pairwise = LythonCallableSignature.Create("itertools.pairwise", ["iterable"]);
        public static readonly LythonCallableSignature GroupBy = LythonCallableSignature.Create("itertools.groupby", ["iterable", "key"], requiredCount: 1);
        public static readonly LythonCallableSignature Tee = LythonCallableSignature.Create("itertools.tee", ["iterable", "n"], requiredCount: 1);
        public static readonly LythonCallableSignature Batched = LythonCallableSignature.Create("itertools.batched", ["iterable", "n", "strict"], requiredCount: 2, maximumPositionalArgumentCount: 2);
    }
}

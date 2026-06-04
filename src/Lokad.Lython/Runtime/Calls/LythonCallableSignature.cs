namespace Lokad.Lython.Runtime;

internal readonly record struct LythonCallableSignature(
    string Name,
    string[]? ParameterNames = null,
    int? RequiredCount = null,
    int? MaxPositionalCount = null,
    bool AllowsExtraKeywords = false)
{
    public int MinimumArgumentCount => RequiredCount ?? ParameterNames?.Length ?? 0;

    public int? MaximumArgumentCount => AllowsExtraKeywords
        ? null
        : ParameterNames?.Length;
}

internal static class LythonKnownCallableSignatures
{
    public static readonly LythonCallableSignature SysExit = new("sys.exit", ["code"], RequiredCount: 0);

    public static readonly LythonCallableSignature PathlibPath = new("pathlib.Path", RequiredCount: 1);

    public static readonly LythonCallableSignature JsonLoads = new("json.loads", ["s"]);
    public static readonly LythonCallableSignature JsonDumps = new("json.dumps", ["obj"]);

    public static readonly LythonCallableSignature CsvReader = new("csv.reader", ["lines", "delimiter"], RequiredCount: 1);
    public static readonly LythonCallableSignature CsvWriter = new("csv.writer", ["delimiter"], RequiredCount: 0);

    public static readonly LythonCallableSignature ReCompile = new("re.compile", ["pattern", "flags"], RequiredCount: 1);
    public static readonly LythonCallableSignature ReSearch = new("re.search", ["pattern", "string", "flags"], RequiredCount: 2);
    public static readonly LythonCallableSignature ReMatch = new("re.match", ["pattern", "string", "flags"], RequiredCount: 2);
    public static readonly LythonCallableSignature ReFullMatch = new("re.fullmatch", ["pattern", "string", "flags"], RequiredCount: 2);
    public static readonly LythonCallableSignature ReFindAll = new("re.findall", ["pattern", "string", "flags"], RequiredCount: 2);
    public static readonly LythonCallableSignature ReFindIter = new("re.finditer", ["pattern", "string", "flags"], RequiredCount: 2);
    public static readonly LythonCallableSignature ReSub = new("re.sub", ["pattern", "repl", "string", "count", "flags"], RequiredCount: 3);
    public static readonly LythonCallableSignature ReSubn = new("re.subn", ["pattern", "repl", "string", "count", "flags"], RequiredCount: 3);
    public static readonly LythonCallableSignature ReSplit = new("re.split", ["pattern", "string", "maxsplit", "flags"], RequiredCount: 2);
    public static readonly LythonCallableSignature ReEscape = new("re.escape", ["string"]);

    public static readonly LythonCallableSignature ArgparseArgumentParser = new("argparse.ArgumentParser", ["description"], RequiredCount: 0);

    public static readonly LythonCallableSignature DataclassesIsDataclass = new("dataclasses.is_dataclass", ["value"]);
    public static readonly LythonCallableSignature DataclassesFields = new("dataclasses.fields", ["class_or_instance"]);
    public static readonly LythonCallableSignature DataclassesAsDict = new("dataclasses.asdict", ["obj", "dict_factory"], RequiredCount: 1);
    public static readonly LythonCallableSignature DataclassesAsTuple = new("dataclasses.astuple", ["obj", "tuple_factory"], RequiredCount: 1);
    public static readonly LythonCallableSignature DataclassesReplace = new("dataclasses.replace", ["obj"], RequiredCount: 1, MaxPositionalCount: 1, AllowsExtraKeywords: true);

    public static readonly LythonCallableSignature SubprocessRun = new("subprocess.run", ["args", "input", "cwd", "timeout", "check", "capture_output"], RequiredCount: 1);

    public static readonly LythonCallableSignature OsListDir = new("os.listdir", ["path"], RequiredCount: 0);
    public static readonly LythonCallableSignature OsWalk = new("os.walk", ["top", "topdown", "onerror", "followlinks"], RequiredCount: 0);
    public static readonly LythonCallableSignature OsGetCwd = new("os.getcwd", []);
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
    public static readonly LythonCallableSignature OsPathAbsPath = new("os.path.abspath", ["path"]);
    public static readonly LythonCallableSignature OsPathRelPath = new("os.path.relpath", ["path", "start"], RequiredCount: 1);
    public static readonly LythonCallableSignature OsPathCommonPath = new("os.path.commonpath", ["paths"]);
    public static readonly LythonCallableSignature OsPathExists = new("os.path.exists", ["path"]);
    public static readonly LythonCallableSignature OsPathIsFile = new("os.path.isfile", ["path"]);
    public static readonly LythonCallableSignature OsPathIsDir = new("os.path.isdir", ["path"]);

    public static readonly LythonCallableSignature Glob = new("glob.glob", ["pathname", "recursive"], RequiredCount: 1);
    public static readonly LythonCallableSignature IGlob = new("glob.iglob", ["pathname", "recursive"], RequiredCount: 1);
    public static readonly LythonCallableSignature GlobEscape = new("glob.escape", ["pathname"]);

    public static readonly LythonCallableSignature FnMatch = new("fnmatch.fnmatch", ["name", "pattern"]);
    public static readonly LythonCallableSignature FnMatchFilter = new("fnmatch.filter", ["names", "pattern"]);

    public static readonly LythonCallableSignature DifflibUnifiedDiff = new("difflib.unified_diff", ["a", "b", "fromfile", "tofile", "fromfiledate", "tofiledate", "n", "lineterm"], RequiredCount: 2);
    public static readonly LythonCallableSignature DifflibContextDiff = new("difflib.context_diff", ["a", "b", "fromfile", "tofile", "fromfiledate", "tofiledate", "n", "lineterm"], RequiredCount: 2);
    public static readonly LythonCallableSignature DifflibNdiff = new("difflib.ndiff", ["a", "b"], RequiredCount: 2);
    public static readonly LythonCallableSignature DifflibRestore = new("difflib.restore", ["delta", "which"]);
    public static readonly LythonCallableSignature DifflibGetCloseMatches = new("difflib.get_close_matches", ["word", "possibilities", "n", "cutoff"], RequiredCount: 2);
    public static readonly LythonCallableSignature DifflibSequenceMatcher = new("difflib.SequenceMatcher", ["isjunk", "a", "b", "autojunk"], RequiredCount: 0);

    public static readonly LythonCallableSignature PkgutilIterModules = new("pkgutil.iter_modules", ["path", "prefix"], RequiredCount: 0);
    public static readonly LythonCallableSignature PkgutilWalkPackages = new("pkgutil.walk_packages", ["path", "prefix", "onerror"], RequiredCount: 0);
    public static readonly LythonCallableSignature PkgutilFindLoader = new("pkgutil.find_loader", ["fullname"]);
    public static readonly LythonCallableSignature PkgutilGetLoader = new("pkgutil.get_loader", ["module_or_name"]);
}

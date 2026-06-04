using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

internal static partial class StaticContracts
{
    private static readonly StaticKnownCallContract[] KnownCallContracts =
    [
        new(LythonKnownCallableSignatures.SysExit, "LA3151", "sys.exit([code]) expects zero or one argument.", StaticReturnShape.None),
        new(LythonKnownCallableSignatures.PathlibPath, "LA3151", "pathlib.Path(path[, ...]) expects one or more arguments.", StaticReturnShape.Path),
        new(LythonKnownCallableSignatures.JsonLoads, "LA3151", "json.loads(s) expects one argument."),
        new(LythonKnownCallableSignatures.JsonDumps, "LA3151", "json.dumps(obj) expects one argument.", StaticReturnShape.String),
        new(LythonKnownCallableSignatures.CsvReader, "LA3151", "csv.reader(lines[, delimiter]) expects one or two arguments.", StaticReturnShape.ListOfListOfString),
        new(LythonKnownCallableSignatures.CsvWriter, "LA3151", "csv.writer([delimiter]) expects zero or one argument.", StaticReturnShape.CsvWriter),
        new(LythonKnownCallableSignatures.ReCompile, "LA3151", "re.compile(pattern[, flags]) expects one or two arguments.", StaticReturnShape.RegexPattern),
        new(LythonKnownCallableSignatures.ReSearch, "LA3151", "re.search(pattern, string[, flags]) expects two or three arguments.", StaticReturnShape.MaybeRegexMatch),
        new(LythonKnownCallableSignatures.ReMatch, "LA3151", "re.match(pattern, string[, flags]) expects two or three arguments.", StaticReturnShape.MaybeRegexMatch),
        new(LythonKnownCallableSignatures.ReFullMatch, "LA3151", "re.fullmatch(pattern, string[, flags]) expects two or three arguments.", StaticReturnShape.MaybeRegexMatch),
        new(LythonKnownCallableSignatures.ReFindAll, "LA3151", "re.findall(pattern, string[, flags]) expects two or three arguments.", StaticReturnShape.ListOfUnknown),
        new(LythonKnownCallableSignatures.ReFindIter, "LA3151", "re.finditer(pattern, string[, flags]) expects two or three arguments."),
        new(LythonKnownCallableSignatures.ReSub, "LA3151", "re.sub(pattern, repl, string[, count][, flags]) expects three to five arguments.", StaticReturnShape.String),
        new(LythonKnownCallableSignatures.ReSubn, "LA3151", "re.subn(pattern, repl, string[, count][, flags]) expects three to five arguments."),
        new(LythonKnownCallableSignatures.ReSplit, "LA3151", "re.split(pattern, string[, maxsplit][, flags]) expects two to four arguments.", StaticReturnShape.ListOfString),
        new(LythonKnownCallableSignatures.ReEscape, "LA3151", "re.escape(string) expects one argument.", StaticReturnShape.String),
        new(LythonKnownCallableSignatures.ArgparseArgumentParser, "LA3151", "argparse.ArgumentParser([description]) expects zero or one argument.", StaticReturnShape.ArgparseParser),
        new(LythonKnownCallableSignatures.DataclassesIsDataclass, "LA3151", "dataclasses.is_dataclass(value) expects one argument."),
        new(LythonKnownCallableSignatures.DataclassesFields, "LA3151", "dataclasses.fields(class_or_instance) expects one argument."),
        new(LythonKnownCallableSignatures.DataclassesAsDict, "LA3151", "dataclasses.asdict(obj[, dict_factory]) expects one or two arguments."),
        new(LythonKnownCallableSignatures.DataclassesAsTuple, "LA3151", "dataclasses.astuple(obj[, tuple_factory]) expects one or two arguments."),
        new(LythonKnownCallableSignatures.DataclassesReplace, "LA3151", "dataclasses.replace(obj, **changes) expects one positional object plus keyword field changes."),
        new(LythonKnownCallableSignatures.SubprocessRun, "LA3151", "subprocess.run(args[, input][, cwd][, timeout][, check][, capture_output]) expects one to six arguments.", StaticReturnShape.SubprocessCompletedProcess),
        new(LythonKnownCallableSignatures.OsListDir, "LA3151", "os.listdir([path]) expects zero or one argument.", StaticReturnShape.ListOfString),
        new(LythonKnownCallableSignatures.OsWalk, "LA3151", "os.walk([top][, topdown][, onerror][, followlinks]) expects zero to four arguments."),
        new(LythonKnownCallableSignatures.OsGetCwd, "LA3151", "os.getcwd() expects no arguments.", StaticReturnShape.String),
        new(LythonKnownCallableSignatures.OsMkdir, "LA3151", "os.mkdir(path) expects one argument.", StaticReturnShape.None),
        new(LythonKnownCallableSignatures.OsMakedirs, "LA3151", "os.makedirs(path[, exist_ok]) expects one or two arguments.", StaticReturnShape.None),
        new(LythonKnownCallableSignatures.OsRemove, "LA3151", "os.remove(path) expects one argument.", StaticReturnShape.None),
        new(LythonKnownCallableSignatures.OsUnlink, "LA3151", "os.unlink(path) expects one argument.", StaticReturnShape.None),
        new(LythonKnownCallableSignatures.OsRename, "LA3151", "os.rename(src, dst) expects two arguments.", StaticReturnShape.None),
        new(LythonKnownCallableSignatures.OsReplace, "LA3151", "os.replace(src, dst) expects two arguments.", StaticReturnShape.None),
        new(LythonKnownCallableSignatures.OsRmdir, "LA3151", "os.rmdir(path) expects one argument.", StaticReturnShape.None),
        new(LythonKnownCallableSignatures.OsRemovedirs, "LA3151", "os.removedirs(path) expects one argument.", StaticReturnShape.None),
        new(LythonKnownCallableSignatures.OsPathJoin, "LA3151", "os.path.join(path, *paths) expects one or more arguments.", StaticReturnShape.String),
        new(LythonKnownCallableSignatures.OsPathSplit, "LA3151", "os.path.split(path) expects one argument."),
        new(LythonKnownCallableSignatures.OsPathSplitExt, "LA3151", "os.path.splitext(path) expects one argument."),
        new(LythonKnownCallableSignatures.OsPathBasename, "LA3151", "os.path.basename(path) expects one argument.", StaticReturnShape.String),
        new(LythonKnownCallableSignatures.OsPathDirname, "LA3151", "os.path.dirname(path) expects one argument.", StaticReturnShape.String),
        new(LythonKnownCallableSignatures.OsPathIsAbs, "LA3151", "os.path.isabs(path) expects one argument.", StaticReturnShape.Boolean),
        new(LythonKnownCallableSignatures.OsPathNormPath, "LA3151", "os.path.normpath(path) expects one argument.", StaticReturnShape.String),
        new(LythonKnownCallableSignatures.OsPathAbsPath, "LA3151", "os.path.abspath(path) expects one argument.", StaticReturnShape.String),
        new(LythonKnownCallableSignatures.OsPathRelPath, "LA3151", "os.path.relpath(path[, start]) expects one or two arguments.", StaticReturnShape.String),
        new(LythonKnownCallableSignatures.OsPathCommonPath, "LA3151", "os.path.commonpath(paths) expects one argument.", StaticReturnShape.String),
        new(LythonKnownCallableSignatures.OsPathExists, "LA3151", "os.path.exists(path) expects one argument.", StaticReturnShape.Boolean),
        new(LythonKnownCallableSignatures.OsPathIsFile, "LA3151", "os.path.isfile(path) expects one argument.", StaticReturnShape.Boolean),
        new(LythonKnownCallableSignatures.OsPathIsDir, "LA3151", "os.path.isdir(path) expects one argument.", StaticReturnShape.Boolean),
        new(LythonKnownCallableSignatures.Glob, "LA3151", "glob.glob(pathname[, recursive]) expects one or two arguments.", StaticReturnShape.ListOfPath),
        new(LythonKnownCallableSignatures.IGlob, "LA3151", "glob.iglob(pathname[, recursive]) expects one or two arguments.", StaticReturnShape.ListOfPath),
        new(LythonKnownCallableSignatures.GlobEscape, "LA3151", "glob.escape(pathname) expects one argument.", StaticReturnShape.String),
        new(LythonKnownCallableSignatures.FnMatch, "LA3151", "fnmatch.fnmatch(name, pattern) expects two arguments.", StaticReturnShape.Boolean),
        new(LythonKnownCallableSignatures.FnMatchFilter, "LA3151", "fnmatch.filter(names, pattern) expects two arguments.", StaticReturnShape.ListOfString),
        new(LythonKnownCallableSignatures.DifflibUnifiedDiff, "LA3151", "difflib.unified_diff(a, b[, fromfile][, tofile][, fromfiledate][, tofiledate][, n][, lineterm]) expects two to eight arguments.", StaticReturnShape.ListOfString),
        new(LythonKnownCallableSignatures.DifflibContextDiff, "LA3151", "difflib.context_diff(a, b[, fromfile][, tofile][, fromfiledate][, tofiledate][, n][, lineterm]) expects two to eight arguments.", StaticReturnShape.ListOfString),
        new(LythonKnownCallableSignatures.DifflibNdiff, "LA3151", "difflib.ndiff(a, b) expects two arguments.", StaticReturnShape.ListOfString),
        new(LythonKnownCallableSignatures.DifflibRestore, "LA3151", "difflib.restore(delta, which) expects two arguments.", StaticReturnShape.ListOfString),
        new(LythonKnownCallableSignatures.DifflibGetCloseMatches, "LA3151", "difflib.get_close_matches(word, possibilities[, n][, cutoff]) expects two to four arguments.", StaticReturnShape.ListOfString),
        new(LythonKnownCallableSignatures.DifflibSequenceMatcher, "LA3151", "difflib.SequenceMatcher([isjunk][, a][, b][, autojunk]) expects zero to four arguments."),
    ];

    public static bool TryGetKnownCallContract(string targetName, out StaticKnownCallContract contract)
    {
        foreach (var candidate in KnownCallContracts)
        {
            if (string.Equals(candidate.TargetName, targetName, StringComparison.Ordinal))
            {
                contract = candidate;
                return true;
            }
        }

        contract = default;
        return false;
    }

    public static bool TryGetKnownCallReturn(string targetName, LythonSourceSpan span, out AbstractValue value)
    {
        if (TryGetKnownCallContract(targetName, out var contract) &&
            contract.ReturnShape != StaticReturnShape.Unknown)
        {
            value = CreateReturnValue(contract.ReturnShape, span);
            return true;
        }

        value = default;
        return false;
    }
}

using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

internal static partial class StaticContracts
{
    public static bool TryGetKnownCallContract(string targetName, out StaticKnownCallContract contract)
    {
        var index = KnownCallModule(targetName) switch
        {
            "argparse" => ArgparseCalls.ByName,
            "collections" => CollectionsCalls.ByName,
            "copy" => CopyCalls.ByName,
            "csv" => CsvCalls.ByName,
            "dataclasses" => DataclassesCalls.ByName,
            "datetime" => DateTimeCalls.ByName,
            "decimal" => DecimalCalls.ByName,
            "difflib" => DifflibCalls.ByName,
            "filecmp" => FilecmpCalls.ByName,
            "fnmatch" => FnmatchCalls.ByName,
            "functools" => FunctoolsCalls.ByName,
            "glob" => GlobCalls.ByName,
            "gzip" => GzipCalls.ByName,
            "hashlib" => HashlibCalls.ByName,
            "html" => HtmlCalls.ByName,
            "importlib" => ImportlibCalls.ByName,
            "io" => IoCalls.ByName,
            "itertools" => ItertoolsCalls.ByName,
            "json" => JsonCalls.ByName,
            "math" => MathCalls.ByName,
            "openpyxl" => OpenPyxlCalls.ByName,
            "operator" => OperatorCalls.ByName,
            "os" => OsCalls.ByName,
            "pathlib" => PathlibCalls.ByName,
            "pkgutil" => PkgutilCalls.ByName,
            "random" => RandomCalls.ByName,
            "re" => RegexCalls.ByName,
            "shlex" => ShlexCalls.ByName,
            "shutil" => ShutilCalls.ByName,
            "statistics" => StatisticsCalls.ByName,
            "struct" => StructCalls.ByName,
            "subprocess" => SubprocessCalls.ByName,
            "sys" => SysCalls.ByName,
            "textwrap" => TextwrapCalls.ByName,
            "time" => TimeCalls.ByName,
            "typing" => TypingCalls.ByName,
            "urllib" => UrlLibCalls.ByName,
            "xml" => ElementTreeCalls.ByName,
            "zipfile" => ZipfileCalls.ByName,
            "zlib" => ZlibCalls.ByName,
            _ => null,
        };

        if (index is not null)
            return index.TryGetValue(targetName, out contract);
        contract = default;
        return false;
    }

    internal static ReadOnlySpan<char> KnownCallModule(string targetName)
    {
        // Aliases use their canonical target, including nested modules such as os.path.
        var separator = targetName.IndexOf('.');
        return separator > 0 ? targetName.AsSpan(0, separator) : default;
    }

    public static bool TryGetKnownCallReturn(StaticKnownCallContract contract, LythonSourceSpan span, out AbstractValue value)
    {
        if (contract.ReturnShape != StaticReturnShape.Unknown)
        {
            value = CreateReturnValue(contract.ReturnShape, span);
            return true;
        }

        value = default;
        return false;
    }

    private static Dictionary<string, StaticKnownCallContract> Index(StaticKnownCallContract[] declarations)
        => declarations.ToDictionary(static declaration => declaration.TargetName, StringComparer.Ordinal);

    private static class ArgparseCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Argparse.ArgumentParser, "LA3151", "argparse.ArgumentParser([prog][, usage][, description][, epilog][, formatter_class][, add_help][, allow_abbrev][, exit_on_error]) expects supported parser options.", StaticReturnShape.ArgparseParser),
            new(LythonKnownCallableSignatures.Argparse.Namespace, "LA3151", "argparse.Namespace(**kwargs) expects keyword arguments only.", StaticReturnShape.ArgparseNamespace),
            new(LythonKnownCallableSignatures.Argparse.FileType, "LA3151", "argparse.FileType([mode][, bufsize][, encoding][, errors]) expects supported text-mode options."),
        ]);
    }

    private static class CollectionsCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Collections.DefaultDict, "LA3151", "collections.defaultdict([default_factory][, iterable], **kwargs) expects supported arguments.", StaticReturnShape.CollectionsDefaultDict),
            new(LythonKnownCallableSignatures.Collections.Counter, "LA3151", "collections.Counter([iterable], **kwargs) expects supported arguments.", StaticReturnShape.CollectionsCounter),
            new(LythonKnownCallableSignatures.Collections.Deque, "LA3151", "collections.deque([iterable][, maxlen]) expects supported arguments.", StaticReturnShape.CollectionsDeque),
            new(LythonKnownCallableSignatures.Collections.NamedTuple, "LA3151", "collections.namedtuple(typename, field_names[, rename][, defaults][, module]) expects supported arguments."),
            // OrderedDict results stay unchecked like dict() calls: modeling them as
            // a precisely-empty dict wrongly rejects literal subscripts of stored keys.
            new(LythonKnownCallableSignatures.Collections.OrderedDict, "LA3151", "collections.OrderedDict([mapping], **kwargs) expects supported arguments.", StaticReturnShape.Unknown),
            new(LythonKnownCallableSignatures.Collections.ChainMap, "LA3151", "collections.ChainMap(*maps) expects positional mapping arguments.", StaticReturnShape.CollectionsChainMap),
            new(LythonKnownCallableSignatures.Collections.UserDict, "LA3151", "collections.UserDict is explicitly unsupported by Lython."),
            new(LythonKnownCallableSignatures.Collections.UserList, "LA3151", "collections.UserList is explicitly unsupported by Lython."),
            new(LythonKnownCallableSignatures.Collections.UserString, "LA3151", "collections.UserString is explicitly unsupported by Lython."),
        ]);
    }

    private static class CopyCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Copy.CopyCopy, "LA3151", "copy.copy(x) expects one argument."),
            new(LythonKnownCallableSignatures.Copy.DeepCopy, "LA3151", "copy.deepcopy(x, memo=None) expects one argument plus an optional memo dict."),
            new(LythonKnownCallableSignatures.Copy.Replace, "LA3151", "copy.replace(obj, /, **changes) expects one positional object plus keyword changes."),
        ]);
    }

    private static class CsvCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Csv.Reader, "LA3151", "csv.reader(csvfile[, dialect][, ...]) expects one argument and supported CSV options.", StaticReturnShape.CsvReader),
            new(LythonKnownCallableSignatures.Csv.Writer, "LA3151", "csv.writer([fileobj][, dialect][, ...]) expects a text file handle or no file object.", StaticReturnShape.CsvWriter),
            new(LythonKnownCallableSignatures.Csv.DictReader, "LA3151", "csv.DictReader(f[, fieldnames][, restkey][, restval][, ...]) expects supported CSV options.", StaticReturnShape.CsvDictReader),
            new(LythonKnownCallableSignatures.Csv.DictWriter, "LA3151", "csv.DictWriter(f, fieldnames, ...) expects a text file handle and field names.", StaticReturnShape.CsvDictWriter),
        ]);
    }

    private static class DataclassesCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Dataclasses.Dataclass, "LA3151", "dataclasses.dataclass([cls], ...) expects at most one positional class plus supported boolean options."),
            new(LythonKnownCallableSignatures.Dataclasses.Field, "LA3151", "dataclasses.field(...) expects keyword-only field options."),
            new(LythonKnownCallableSignatures.Dataclasses.MakeDataclass, "LA3151", "dataclasses.make_dataclass(cls_name, fields, ...) expects a class name and field list."),
            new(LythonKnownCallableSignatures.Dataclasses.IsDataclass, "LA3151", "dataclasses.is_dataclass(value) expects one argument."),
            new(LythonKnownCallableSignatures.Dataclasses.Fields, "LA3151", "dataclasses.fields(class_or_instance) expects one argument."),
            new(LythonKnownCallableSignatures.Dataclasses.AsDict, "LA3151", "dataclasses.asdict(obj[, dict_factory]) expects one or two arguments."),
            new(LythonKnownCallableSignatures.Dataclasses.AsTuple, "LA3151", "dataclasses.astuple(obj[, tuple_factory]) expects one or two arguments."),
            new(LythonKnownCallableSignatures.Dataclasses.Replace, "LA3151", "dataclasses.replace(obj, **changes) expects one positional object plus keyword field changes."),
        ]);
    }

    private static class DateTimeCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.DateTime.Timedelta, "LA3151", "datetime.timedelta(...) expects supported duration fields.", StaticReturnShape.DateTimeTimedelta),
            new(LythonKnownCallableSignatures.DateTime.Date, "LA3151", "datetime.date(year, month, day) expects three arguments.", StaticReturnShape.DateTimeDate),
            new(LythonKnownCallableSignatures.DateTime.DateToday, "LA3151", "datetime.date.today() expects no arguments.", StaticReturnShape.DateTimeDate),
            new(LythonKnownCallableSignatures.DateTime.DateFromTimestamp, "LA3151", "datetime.date.fromtimestamp(timestamp) expects one argument.", StaticReturnShape.DateTimeDate),
            new(LythonKnownCallableSignatures.DateTime.DateFromOrdinal, "LA3151", "datetime.date.fromordinal(ordinal) expects one argument.", StaticReturnShape.DateTimeDate),
            new(LythonKnownCallableSignatures.DateTime.DateFromIsoFormat, "LA3151", "datetime.date.fromisoformat(date_string) expects one argument.", StaticReturnShape.DateTimeDate),
            new(LythonKnownCallableSignatures.DateTime.DateFromIsoCalendar, "LA3151", "datetime.date.fromisocalendar(year, week, day) expects three arguments.", StaticReturnShape.DateTimeDate),
            new(LythonKnownCallableSignatures.DateTime.Time, "LA3151", "datetime.time([hour][, minute][, second][, microsecond][, tzinfo][, fold]) expects supported arguments.", StaticReturnShape.DateTimeTime),
            new(LythonKnownCallableSignatures.DateTime.TimeFromIsoFormat, "LA3151", "datetime.time.fromisoformat(time_string) expects one argument.", StaticReturnShape.DateTimeTime),
            new(LythonKnownCallableSignatures.DateTime.DateTimeDateTime, "LA3151", "datetime.datetime(year, month, day[, ...]) expects at least year, month, and day.", StaticReturnShape.DateTimeDateTime),
            new(LythonKnownCallableSignatures.DateTime.DateTimeToday, "LA3151", "datetime.datetime.today() expects no arguments.", StaticReturnShape.DateTimeDateTime),
            new(LythonKnownCallableSignatures.DateTime.DateTimeNow, "LA3151", "datetime.datetime.now([tz]) expects zero or one argument.", StaticReturnShape.DateTimeDateTime),
            new(LythonKnownCallableSignatures.DateTime.DateTimeUtcNow, "LA3151", "datetime.datetime.utcnow() expects no arguments.", StaticReturnShape.DateTimeDateTime),
            new(LythonKnownCallableSignatures.DateTime.DateTimeFromTimestamp, "LA3151", "datetime.datetime.fromtimestamp(timestamp[, tz]) expects one or two arguments.", StaticReturnShape.DateTimeDateTime),
            new(LythonKnownCallableSignatures.DateTime.DateTimeUtcFromTimestamp, "LA3151", "datetime.datetime.utcfromtimestamp(timestamp) expects one argument.", StaticReturnShape.DateTimeDateTime),
            new(LythonKnownCallableSignatures.DateTime.DateTimeFromOrdinal, "LA3151", "datetime.datetime.fromordinal(ordinal) expects one argument.", StaticReturnShape.DateTimeDateTime),
            new(LythonKnownCallableSignatures.DateTime.DateTimeCombine, "LA3151", "datetime.datetime.combine(date, time[, tzinfo]) expects two or three arguments.", StaticReturnShape.DateTimeDateTime),
            new(LythonKnownCallableSignatures.DateTime.DateTimeFromIsoFormat, "LA3151", "datetime.datetime.fromisoformat(date_string) expects one argument.", StaticReturnShape.DateTimeDateTime),
            new(LythonKnownCallableSignatures.DateTime.DateTimeFromIsoCalendar, "LA3151", "datetime.datetime.fromisocalendar(year, week, day) expects three arguments.", StaticReturnShape.DateTimeDateTime),
            new(LythonKnownCallableSignatures.DateTime.DateTimeStrptime, "LA3151", "datetime.datetime.strptime(date_string, format) expects two arguments.", StaticReturnShape.DateTimeDateTime),
            new(LythonKnownCallableSignatures.DateTime.Timezone, "LA3151", "datetime.timezone(offset[, name]) expects a timedelta offset and optional name.", StaticReturnShape.DateTimeTimezone),
            new(LythonKnownCallableSignatures.DateTime.TzInfo, "LA3151", "datetime.tzinfo() is abstract; use datetime.timezone(...)."),
        ]);
    }

    private static class DecimalCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Decimal.Create, "LA3151", "decimal.Decimal([value][, context]) expects a supported fixed-precision decimal value.", StaticReturnShape.Decimal),
            new(LythonKnownCallableSignatures.Decimal.Tuple, "LA3151", "decimal.DecimalTuple(sign, digits, exponent) expects three arguments.", StaticReturnShape.DecimalTuple),
            new(LythonKnownCallableSignatures.Decimal.Context, "LA3151", "decimal.Context(...) expects supported context options.", StaticReturnShape.DecimalContext),
            new(LythonKnownCallableSignatures.Decimal.GetContext, "LA3151", "decimal.getcontext() expects no arguments.", StaticReturnShape.DecimalContext),
            new(LythonKnownCallableSignatures.Decimal.SetContext, "LA3151", "decimal.setcontext(context) expects one argument.", StaticReturnShape.None),
            new(LythonKnownCallableSignatures.Decimal.LocalContext, "LA3151", "decimal.localcontext([context]) expects zero or one argument."),
        ]);
    }

    private static class DifflibCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Difflib.IsLineJunk, "LA3151", "difflib.IS_LINE_JUNK(line) expects one argument.", StaticReturnShape.Boolean),
            new(LythonKnownCallableSignatures.Difflib.IsCharacterJunk, "LA3151", "difflib.IS_CHARACTER_JUNK(ch) expects one argument.", StaticReturnShape.Boolean),
            new(LythonKnownCallableSignatures.Difflib.UnifiedDiff, "LA3151", "difflib.unified_diff(a, b[, fromfile][, tofile][, fromfiledate][, tofiledate][, n][, lineterm]) expects two to eight arguments.", StaticReturnShape.Unknown),
            new(LythonKnownCallableSignatures.Difflib.ContextDiff, "LA3151", "difflib.context_diff(a, b[, fromfile][, tofile][, fromfiledate][, tofiledate][, n][, lineterm]) expects two to eight arguments.", StaticReturnShape.Unknown),
            new(LythonKnownCallableSignatures.Difflib.Ndiff, "LA3151", "difflib.ndiff(a, b[, linejunk][, charjunk]) expects two to four arguments.", StaticReturnShape.ListOfString),
            new(LythonKnownCallableSignatures.Difflib.Restore, "LA3151", "difflib.restore(delta, which) expects two arguments.", StaticReturnShape.Unknown),
            new(LythonKnownCallableSignatures.Difflib.GetCloseMatches, "LA3151", "difflib.get_close_matches(word, possibilities[, n][, cutoff]) expects two to four arguments.", StaticReturnShape.ListOfString),
            new(LythonKnownCallableSignatures.Difflib.DiffBytes, "LA3151", "difflib.diff_bytes(dfunc, a, b[, fromfile][, tofile][, fromfiledate][, tofiledate][, n][, lineterm]) expects three to nine arguments.", StaticReturnShape.ListOfBytes),
            new(LythonKnownCallableSignatures.Difflib.Differ, "LA3151", "difflib.Differ([linejunk][, charjunk]) expects zero to two arguments.", StaticReturnShape.DifflibDiffer),
            new(LythonKnownCallableSignatures.Difflib.HtmlDiff, "LA3151", "difflib.HtmlDiff([tabsize][, wrapcolumn][, linejunk][, charjunk]) expects zero to four arguments.", StaticReturnShape.DifflibHtmlDiff),
            new(LythonKnownCallableSignatures.Difflib.Match, "LA3151", "difflib.Match(a, b, size) expects three arguments.", StaticReturnShape.Unknown),
            new(LythonKnownCallableSignatures.Difflib.SequenceMatcher, "LA3151", "difflib.SequenceMatcher([isjunk][, a][, b][, autojunk]) expects zero to four arguments.", StaticReturnShape.DifflibSequenceMatcher),
        ]);
    }

    private static class FilecmpCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Filecmp.Cmp, "LA3151", "filecmp.cmp(f1, f2[, shallow]) expects two or three arguments.", StaticReturnShape.Boolean),
            new(LythonKnownCallableSignatures.Filecmp.ClearCache, "LA3151", "filecmp.clear_cache() expects no arguments.", StaticReturnShape.None),
            new(LythonKnownCallableSignatures.Filecmp.Dircmp, "LA3151", "filecmp.dircmp(a, b[, ignore][, hide]) is explicitly unsupported by Lython."),
        ]);
    }

    private static class FnmatchCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Fnmatch.FnMatch, "LA3151", "fnmatch.fnmatch(name, pattern) expects two arguments.", StaticReturnShape.Boolean),
            new(LythonKnownCallableSignatures.Fnmatch.FnMatchCase, "LA3151", "fnmatch.fnmatchcase(name, pattern) expects two arguments.", StaticReturnShape.Boolean),
            new(LythonKnownCallableSignatures.Fnmatch.FnMatchFilter, "LA3151", "fnmatch.filter(names, pattern) expects two arguments.", StaticReturnShape.ListOfString),
            new(LythonKnownCallableSignatures.Fnmatch.FnMatchTranslate, "LA3151", "fnmatch.translate(pattern) expects one argument.", StaticReturnShape.String),
        ]);
    }

    private static class FunctoolsCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Functools.UpdateWrapper, "LA3151", "functools.update_wrapper(wrapper, wrapped[, assigned][, updated]) expects two to four arguments."),
            new(LythonKnownCallableSignatures.Functools.Wraps, "LA3151", "functools.wraps(wrapped[, assigned][, updated]) expects one to three arguments."),
            new(LythonKnownCallableSignatures.Functools.TotalOrdering, "LA3151", "functools.total_ordering(cls) expects one class argument."),
            new(LythonKnownCallableSignatures.Functools.Reduce, "LA3151", "functools.reduce(function, iterable[, initializer]) expects two or three arguments."),
            new(LythonKnownCallableSignatures.Functools.Partial, "LA3151", "functools.partial(func, ...) expects the first argument to be callable."),
            new(LythonKnownCallableSignatures.Functools.PartialMethod, "LA3151", "functools.partialmethod(func, ...) expects the first argument to be callable."),
            new(LythonKnownCallableSignatures.Functools.CmpToKey, "LA3151", "functools.cmp_to_key(mycmp) expects one callable argument."),
            new(LythonKnownCallableSignatures.Functools.LruCache, "LA3151", "functools.lru_cache(maxsize=128, typed=False) expects supported cache options."),
            new(LythonKnownCallableSignatures.Functools.Cache, "LA3151", "functools.cache(user_function) expects one callable argument."),
            new(LythonKnownCallableSignatures.Functools.CachedProperty, "LA3151", "functools.cached_property(func) expects one callable argument."),
            new(LythonKnownCallableSignatures.Functools.SingleDispatch, "LA3151", "functools.singledispatch(func) expects one callable argument."),
            new(LythonKnownCallableSignatures.Functools.SingleDispatchMethod, "LA3151", "functools.singledispatchmethod(func) expects one callable argument."),
            new(LythonKnownCallableSignatures.Functools.RecursiveRepr, "LA3151", "functools.recursive_repr(fillvalue='...') expects zero or one argument."),
        ]);
    }

    private static class GlobCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Glob.Find, "LA3151", "glob.glob(pathname, *, root_dir=None, dir_fd=None, recursive=False, include_hidden=False) expects one path-like argument plus supported keyword options.", StaticReturnShape.ListOfString),
            new(LythonKnownCallableSignatures.Glob.IGlob, "LA3151", "glob.iglob(pathname, *, root_dir=None, dir_fd=None, recursive=False, include_hidden=False) expects one path-like argument plus supported keyword options."),
            new(LythonKnownCallableSignatures.Glob.Escape, "LA3151", "glob.escape(pathname) expects one argument.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.Glob.HasMagic, "LA3151", "glob.has_magic(s) expects one argument.", StaticReturnShape.Boolean),
            new(LythonKnownCallableSignatures.Glob.Translate, "LA3151", "glob.translate(pathname, *, recursive=False, include_hidden=False, seps=None) expects one path-like argument plus supported keyword options.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.Glob.Glob0, "LA3151", "glob.glob0(...) is not supported by Lython; use glob.glob()."),
            new(LythonKnownCallableSignatures.Glob.Glob1, "LA3151", "glob.glob1(...) is not supported by Lython; use glob.glob()."),
        ]);
    }

    private static class GzipCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Gzip.Compress, "LA3151", "gzip.compress(data[, compresslevel], *, mtime=...) expects one or two positional arguments."),
            new(LythonKnownCallableSignatures.Gzip.Decompress, "LA3151", "gzip.decompress(data) expects one argument."),
            new(LythonKnownCallableSignatures.Gzip.Open, "LA3151", "gzip.open(filename[, mode[, compresslevel[, encoding[, errors[, newline]]]]]) expects one to six arguments."),
            new(LythonKnownCallableSignatures.Gzip.File, "LA3151", "gzip.GzipFile(filename=None, mode=None, compresslevel=9, fileobj=None, mtime=None) expects at most five arguments."),
        ]);
    }

    private static class HashlibCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Hashlib.Md5, "LA3151", "hashlib.md5([data], *, usedforsecurity=True) expects at most one positional argument."),
            new(LythonKnownCallableSignatures.Hashlib.Sha1, "LA3151", "hashlib.sha1([data], *, usedforsecurity=True) expects at most one positional argument."),
            new(LythonKnownCallableSignatures.Hashlib.Sha256, "LA3151", "hashlib.sha256([data], *, usedforsecurity=True) expects at most one positional argument."),
            new(LythonKnownCallableSignatures.Hashlib.Sha384, "LA3151", "hashlib.sha384([data], *, usedforsecurity=True) expects at most one positional argument."),
            new(LythonKnownCallableSignatures.Hashlib.Sha512, "LA3151", "hashlib.sha512([data], *, usedforsecurity=True) expects at most one positional argument."),
            new(LythonKnownCallableSignatures.Hashlib.New, "LA3151", "hashlib.new(name[, data], *, usedforsecurity=True) expects one or two positional arguments."),
            new(LythonKnownCallableSignatures.Hashlib.FileDigest, "LA3151", "hashlib.file_digest(fileobj, digest, *, _bufsize=...) expects two positional arguments."),
        ]);
    }

    private static class HtmlCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Html.Escape, "LA3151", "html.escape(s, quote=True) expects one to two arguments.", StaticReturnShape.Unknown),
            new(LythonKnownCallableSignatures.Html.Unescape, "LA3151", "html.unescape(s) expects one argument.", StaticReturnShape.Unknown),
        ]);
    }

    private static class ImportlibCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Importlib.ImportModule, "LA3151", "importlib.import_module(name[, package]) expects one or two arguments."),
            new(LythonKnownCallableSignatures.Importlib.InvalidateCaches, "LA3151", "importlib.invalidate_caches() expects no arguments.", StaticReturnShape.None),
            new(LythonKnownCallableSignatures.Importlib.UtilFindSpec, "LA3151", "importlib.util.find_spec(name[, package]) expects one or two arguments."),
            new(LythonKnownCallableSignatures.Importlib.UtilResolveName, "LA3151", "importlib.util.resolve_name(name, package) expects two arguments.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.Importlib.UtilModuleFromSpec, "LA3151", "importlib.util.module_from_spec(spec) expects one argument."),
            new(LythonKnownCallableSignatures.Importlib.UtilSpecFromFileLocation, "LA3151", "importlib.util.spec_from_file_location(name, location, *, loader=None, submodule_search_locations=None) expects two positional arguments."),
            new(LythonKnownCallableSignatures.Importlib.UtilSpecFromLoader, "LA3151", "importlib.util.spec_from_loader(name, loader, *, origin=None, is_package=None) expects two positional arguments."),
        ]);
    }

    private static class IoCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Io.BytesIO, "LA3158", "io.BytesIO([initial_bytes]) expects bytes or None.", StaticReturnShape.BytesIO),
            new(LythonKnownCallableSignatures.Io.StringIO, "LA3158", "io.StringIO([initial_value[, newline]]) expects text or None and a supported newline policy.", StaticReturnShape.StringIO),
        ]);
    }

    private static class ItertoolsCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Itertools.Chain, "LA3151", "itertools.chain(*iterables) expects positional iterable arguments."),
            new(LythonKnownCallableSignatures.Itertools.Count, "LA3151", "itertools.count([start][, step]) expects zero to two arguments."),
            new(LythonKnownCallableSignatures.Itertools.Repeat, "LA3151", "itertools.repeat(object[, times]) expects one or two arguments."),
            new(LythonKnownCallableSignatures.Itertools.Cycle, "LA3151", "itertools.cycle(iterable) expects one argument."),
            new(LythonKnownCallableSignatures.Itertools.Islice, "LA3151", "itertools.islice(iterable, stop) or islice(iterable, start, stop[, step]) expects two to four arguments."),
            new(LythonKnownCallableSignatures.Itertools.Combinations, "LA3151", "itertools.combinations(iterable, r) expects two arguments."),
            new(LythonKnownCallableSignatures.Itertools.CombinationsWithReplacement, "LA3151", "itertools.combinations_with_replacement(iterable, r) expects two arguments."),
            new(LythonKnownCallableSignatures.Itertools.Permutations, "LA3151", "itertools.permutations(iterable[, r]) expects one or two arguments."),
            new(LythonKnownCallableSignatures.Itertools.Accumulate, "LA3151", "itertools.accumulate(iterable[, func], *, initial=None) expects supported arguments."),
            new(LythonKnownCallableSignatures.Itertools.Compress, "LA3151", "itertools.compress(data, selectors) expects two arguments."),
            new(LythonKnownCallableSignatures.Itertools.FilterFalse, "LA3151", "itertools.filterfalse(function, iterable) expects two arguments."),
            new(LythonKnownCallableSignatures.Itertools.DropWhile, "LA3151", "itertools.dropwhile(predicate, iterable) expects two arguments."),
            new(LythonKnownCallableSignatures.Itertools.TakeWhile, "LA3151", "itertools.takewhile(predicate, iterable) expects two arguments."),
            new(LythonKnownCallableSignatures.Itertools.Starmap, "LA3151", "itertools.starmap(function, iterable) expects two arguments."),
            new(LythonKnownCallableSignatures.Itertools.Pairwise, "LA3151", "itertools.pairwise(iterable) expects one argument."),
            new(LythonKnownCallableSignatures.Itertools.GroupBy, "LA3151", "itertools.groupby(iterable[, key]) expects one or two arguments."),
            new(LythonKnownCallableSignatures.Itertools.Tee, "LA3151", "itertools.tee(iterable[, n]) expects one or two arguments."),
            new(LythonKnownCallableSignatures.Itertools.Batched, "LA3151", "itertools.batched(iterable, n, *, strict=False) expects supported arguments."),
        ]);
    }

    private static class JsonCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Json.Load, "LA3151", "json.load(fp, *, ...) expects one text stream with read().", StaticReturnShape.Unknown),
            new(LythonKnownCallableSignatures.Json.Loads, "LA3151", "json.loads(s, *, ...) expects one str or bytes argument plus supported keyword options."),
            new(LythonKnownCallableSignatures.Json.Dump, "LA3151", "json.dump(obj, fp, *, ...) expects an object and text stream with write().", StaticReturnShape.None),
            new(LythonKnownCallableSignatures.Json.Dumps, "LA3151", "json.dumps(obj, *, ...) expects one object plus supported keyword options.", StaticReturnShape.String),
        ]);
    }

    private static class MathCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Math.Sqrt, "LA3151", "math.sqrt(x) expects one numeric argument.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Exp, "LA3151", "math.exp(x) expects one numeric argument.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Log, "LA3151", "math.log(x[, base]) expects one or two numeric arguments.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Log10, "LA3151", "math.log10(x) expects one numeric argument.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Log2, "LA3151", "math.log2(x) expects one numeric argument.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Sin, "LA3151", "math.sin(x) expects one numeric argument.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Cos, "LA3151", "math.cos(x) expects one numeric argument.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Tan, "LA3151", "math.tan(x) expects one numeric argument.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Asin, "LA3151", "math.asin(x) expects one numeric argument.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Acos, "LA3151", "math.acos(x) expects one numeric argument.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Atan, "LA3151", "math.atan(x) expects one numeric argument.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Atan2, "LA3151", "math.atan2(y, x) expects two numeric arguments.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Sinh, "LA3151", "math.sinh(x) expects one numeric argument.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Cosh, "LA3151", "math.cosh(x) expects one numeric argument.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Tanh, "LA3151", "math.tanh(x) expects one numeric argument.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Floor, "LA3151", "math.floor(x) expects one numeric argument.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.Math.Ceil, "LA3151", "math.ceil(x) expects one numeric argument.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.Math.Fabs, "LA3151", "math.fabs(x) expects one numeric argument.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Trunc, "LA3151", "math.trunc(x) expects one numeric argument.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.Math.Degrees, "LA3151", "math.degrees(x) expects one numeric argument.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Radians, "LA3151", "math.radians(x) expects one numeric argument.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.IsFinite, "LA3151", "math.isfinite(x) expects one numeric argument.", StaticReturnShape.Boolean),
            new(LythonKnownCallableSignatures.Math.IsInf, "LA3151", "math.isinf(x) expects one numeric argument.", StaticReturnShape.Boolean),
            new(LythonKnownCallableSignatures.Math.IsNaN, "LA3151", "math.isnan(x) expects one numeric argument.", StaticReturnShape.Boolean),
            new(LythonKnownCallableSignatures.Math.Pow, "LA3151", "math.pow(x, y) expects two numeric arguments.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Hypot, "LA3151", "math.hypot(*coordinates) expects numeric positional arguments.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Fmod, "LA3151", "math.fmod(x, y) expects two numeric arguments.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.CopySign, "LA3151", "math.copysign(x, y) expects two numeric arguments.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.IsClose, "LA3151", "math.isclose(a, b[, rel_tol][, abs_tol]) expects two to four numeric arguments.", StaticReturnShape.Boolean),
            new(LythonKnownCallableSignatures.Math.Prod, "LA3151", "math.prod(iterable, *, start=1) expects one iterable plus optional keyword start."),
            new(LythonKnownCallableSignatures.Math.Fsum, "LA3151", "math.fsum(iterable) expects one iterable.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Factorial, "LA3151", "math.factorial(n) expects one integer argument.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.Math.Gcd, "LA3151", "math.gcd(*integers) expects integer positional arguments.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.Math.Lcm, "LA3151", "math.lcm(*integers) expects integer positional arguments.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.Math.Comb, "LA3151", "math.comb(n, k) expects two integer arguments.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.Math.Perm, "LA3151", "math.perm(n[, k]) expects one or two integer arguments.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.Math.Isqrt, "LA3151", "math.isqrt(n) expects one integer argument.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.Math.Dist, "LA3151", "math.dist(p, q) expects two iterable points.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Frexp, "LA3151", "math.frexp(x) expects one numeric argument.", StaticReturnShape.TupleFloatInteger),
            new(LythonKnownCallableSignatures.Math.Ldexp, "LA3151", "math.ldexp(x, i) expects a numeric value and integer exponent.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Modf, "LA3151", "math.modf(x) expects one numeric argument.", StaticReturnShape.TupleFloatFloat),
            new(LythonKnownCallableSignatures.Math.Remainder, "LA3151", "math.remainder(x, y) expects two numeric arguments.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.NextAfter, "LA3151", "math.nextafter(x, y, *, steps=None) expects two numeric arguments and optional integer steps.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Ulp, "LA3151", "math.ulp(x) expects one numeric argument.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Exp2, "LA3151", "math.exp2(x) expects one numeric argument.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Expm1, "LA3151", "math.expm1(x) expects one numeric argument.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Log1p, "LA3151", "math.log1p(x) expects one numeric argument.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Cbrt, "LA3151", "math.cbrt(x) expects one numeric argument.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Erf, "LA3151", "math.erf(x) expects one numeric argument.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Erfc, "LA3151", "math.erfc(x) expects one numeric argument.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Gamma, "LA3151", "math.gamma(x) expects one numeric argument.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Lgamma, "LA3151", "math.lgamma(x) expects one numeric argument.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.Fma, "LA3151", "math.fma(x, y, z) expects three numeric arguments.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Math.SumProd, "LA3151", "math.sumprod(p, q) expects two iterables."),
        ]);
    }

    private static class OpenPyxlCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.OpenPyxl.Workbook, "LA3151", "openpyxl.Workbook([write_only][, iso_dates]) expects zero to two arguments.", StaticReturnShape.OpenPyxlWorkbook),
            new(LythonKnownCallableSignatures.OpenPyxl.LoadWorkbook, "LA3151", "openpyxl.load_workbook(filename[, read_only][, keep_vba][, data_only][, keep_links][, rich_text]) expects one to six arguments.", StaticReturnShape.OpenPyxlWorkbook),
            new(LythonKnownCallableSignatures.OpenPyxl.GetColumnLetter, "LA3151", "openpyxl.utils.get_column_letter(col_idx) expects one argument.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.OpenPyxl.ColumnIndexFromString, "LA3151", "openpyxl.utils.column_index_from_string(col) expects one argument.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.OpenPyxl.CoordinateFromString, "LA3151", "openpyxl.utils.coordinate_from_string(coord_string) expects one argument."),
            new(LythonKnownCallableSignatures.OpenPyxl.CoordinateToTuple, "LA3151", "openpyxl.utils.coordinate_to_tuple(coordinate) expects one argument."),
            new(LythonKnownCallableSignatures.OpenPyxl.RangeBoundaries, "LA3151", "openpyxl.utils.range_boundaries(range_string) expects one argument."),
            new(LythonKnownCallableSignatures.OpenPyxl.GetColumnInterval, "LA3151", "openpyxl.utils.get_column_interval(start, end) expects two arguments.", StaticReturnShape.ListOfString),
            new(LythonKnownCallableSignatures.OpenPyxl.AbsoluteCoordinate, "LA3151", "openpyxl.utils.absolute_coordinate(coord_string) expects one argument.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.OpenPyxl.QuoteSheetName, "LA3151", "openpyxl.utils.quote_sheetname(sheetname) expects one argument.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.OpenPyxl.RowsFromRange, "LA3151", "openpyxl.utils.rows_from_range(range_string) expects one argument.", StaticReturnShape.ListOfUnknown),
            new(LythonKnownCallableSignatures.OpenPyxl.ColsFromRange, "LA3151", "openpyxl.utils.cols_from_range(range_string) expects one argument.", StaticReturnShape.ListOfUnknown),
            new(LythonKnownCallableSignatures.OpenPyxl.Comment, "LA3151", "openpyxl.comments.Comment(text, author) expects two arguments.", StaticReturnShape.OpenPyxlComment),
            new(LythonKnownCallableSignatures.OpenPyxl.Font, "LA3151", "openpyxl.styles.Font(...) received unsupported arguments.", StaticReturnShape.OpenPyxlFont),
            new(LythonKnownCallableSignatures.OpenPyxl.PatternFill, "LA3151", "openpyxl.styles.PatternFill(...) received unsupported arguments.", StaticReturnShape.OpenPyxlPatternFill),
            new(LythonKnownCallableSignatures.OpenPyxl.Border, "LA3151", "openpyxl.styles.Border(...) received unsupported arguments.", StaticReturnShape.OpenPyxlBorder),
            new(LythonKnownCallableSignatures.OpenPyxl.Side, "LA3151", "openpyxl.styles.Side(...) received unsupported arguments.", StaticReturnShape.OpenPyxlSide),
            new(LythonKnownCallableSignatures.OpenPyxl.Alignment, "LA3151", "openpyxl.styles.Alignment(...) received unsupported arguments.", StaticReturnShape.OpenPyxlAlignment),
            new(LythonKnownCallableSignatures.OpenPyxl.Protection, "LA3151", "openpyxl.styles.Protection(...) received unsupported arguments.", StaticReturnShape.OpenPyxlProtection),
            new(LythonKnownCallableSignatures.OpenPyxl.NamedStyle, "LA3151", "openpyxl.styles.NamedStyle(...) received unsupported arguments.", StaticReturnShape.OpenPyxlNamedStyle),
            new(LythonKnownCallableSignatures.OpenPyxl.Color, "LA3151", "openpyxl.styles.colors.Color(...) received unsupported arguments.", StaticReturnShape.OpenPyxlColor),
            new(LythonKnownCallableSignatures.OpenPyxl.Table, "LA3151", "openpyxl.worksheet.table.Table([displayName][, ref]) expects zero to two arguments.", StaticReturnShape.OpenPyxlTable),
            new(LythonKnownCallableSignatures.OpenPyxl.TableStyleInfo, "LA3151", "openpyxl.worksheet.table.TableStyleInfo(...) received unsupported arguments.", StaticReturnShape.OpenPyxlTableStyleInfo),
            new(LythonKnownCallableSignatures.OpenPyxl.DataValidation, "LA3151", "openpyxl.worksheet.datavalidation.DataValidation(...) received unsupported arguments.", StaticReturnShape.OpenPyxlDataValidation),
            new(LythonKnownCallableSignatures.OpenPyxl.BarChart, "LA3151", "openpyxl.chart.BarChart() expects no arguments.", StaticReturnShape.OpenPyxlChart),
            new(LythonKnownCallableSignatures.OpenPyxl.LineChart, "LA3151", "openpyxl.chart.LineChart() expects no arguments.", StaticReturnShape.OpenPyxlChart),
            new(LythonKnownCallableSignatures.OpenPyxl.PieChart, "LA3151", "openpyxl.chart.PieChart() expects no arguments.", StaticReturnShape.OpenPyxlChart),
            new(LythonKnownCallableSignatures.OpenPyxl.ScatterChart, "LA3151", "openpyxl.chart.ScatterChart() expects no arguments.", StaticReturnShape.OpenPyxlChart),
            new(LythonKnownCallableSignatures.OpenPyxl.ChartReference, "LA3151", "openpyxl.chart.Reference(worksheet[, min_col][, min_row][, max_col][, max_row][, range_string]) expects one to six arguments."),
            new(LythonKnownCallableSignatures.OpenPyxl.ChartSeries, "LA3151", "openpyxl.chart.Series([values][, xvalues][, title]) expects zero to three arguments."),
            new(LythonKnownCallableSignatures.OpenPyxl.DrawingImage, "LA3151", "openpyxl.drawing.image.Image(img) expects one argument.", StaticReturnShape.OpenPyxlImage),
        ]);
    }

    private static class OperatorCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Operator.Truth, "LA3151", "operator.truth(obj) expects one argument.", StaticReturnShape.Boolean),
            new(LythonKnownCallableSignatures.Operator.Not, "LA3151", "operator.not_(obj) expects one argument.", StaticReturnShape.Boolean),
            new(LythonKnownCallableSignatures.Operator.Is, "LA3151", "operator.is_(a, b) expects two arguments.", StaticReturnShape.Boolean),
            new(LythonKnownCallableSignatures.Operator.IsNot, "LA3151", "operator.is_not(a, b) expects two arguments.", StaticReturnShape.Boolean),
            new(LythonKnownCallableSignatures.Operator.Abs, "LA3151", "operator.abs(obj) expects one numeric argument."),
            new(LythonKnownCallableSignatures.Operator.Neg, "LA3151", "operator.neg(obj) expects one numeric argument."),
            new(LythonKnownCallableSignatures.Operator.Pos, "LA3151", "operator.pos(obj) expects one numeric argument."),
            new(LythonKnownCallableSignatures.Operator.Invert, "LA3151", "operator.invert(obj) expects one integer argument.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.Operator.Index, "LA3151", "operator.index(obj) expects one integer-compatible argument.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.Operator.Add, "LA3151", "operator.add(a, b) expects two arguments."),
            new(LythonKnownCallableSignatures.Operator.Sub, "LA3151", "operator.sub(a, b) expects two arguments."),
            new(LythonKnownCallableSignatures.Operator.Mul, "LA3151", "operator.mul(a, b) expects two arguments."),
            new(LythonKnownCallableSignatures.Operator.TrueDiv, "LA3151", "operator.truediv(a, b) expects two arguments."),
            new(LythonKnownCallableSignatures.Operator.FloorDiv, "LA3151", "operator.floordiv(a, b) expects two arguments."),
            new(LythonKnownCallableSignatures.Operator.Mod, "LA3151", "operator.mod(a, b) expects two arguments."),
            new(LythonKnownCallableSignatures.Operator.Pow, "LA3151", "operator.pow(a, b) expects two arguments."),
            new(LythonKnownCallableSignatures.Operator.MatMul, "LA3151", "operator.matmul(a, b) is explicitly unsupported because Lython has no matrix multiplication operator."),
            new(LythonKnownCallableSignatures.Operator.LShift, "LA3151", "operator.lshift(a, b) expects two integer arguments.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.Operator.RShift, "LA3151", "operator.rshift(a, b) expects two integer arguments.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.Operator.And, "LA3151", "operator.and_(a, b) expects two compatible integer or set-like arguments."),
            new(LythonKnownCallableSignatures.Operator.Or, "LA3151", "operator.or_(a, b) expects two compatible integer or set-like arguments."),
            new(LythonKnownCallableSignatures.Operator.Xor, "LA3151", "operator.xor(a, b) expects two compatible integer or set-like arguments."),
            new(LythonKnownCallableSignatures.Operator.Concat, "LA3151", "operator.concat(a, b) expects two compatible sequence arguments."),
            new(LythonKnownCallableSignatures.Operator.Eq, "LA3151", "operator.eq(a, b) expects two arguments.", StaticReturnShape.Unknown),
            new(LythonKnownCallableSignatures.Operator.Ne, "LA3151", "operator.ne(a, b) expects two arguments.", StaticReturnShape.Unknown),
            new(LythonKnownCallableSignatures.Operator.Lt, "LA3151", "operator.lt(a, b) expects two comparable arguments.", StaticReturnShape.Unknown),
            new(LythonKnownCallableSignatures.Operator.Le, "LA3151", "operator.le(a, b) expects two comparable arguments.", StaticReturnShape.Unknown),
            new(LythonKnownCallableSignatures.Operator.Gt, "LA3151", "operator.gt(a, b) expects two comparable arguments.", StaticReturnShape.Unknown),
            new(LythonKnownCallableSignatures.Operator.Ge, "LA3151", "operator.ge(a, b) expects two comparable arguments.", StaticReturnShape.Unknown),
            new(LythonKnownCallableSignatures.Operator.GetItem, "LA3151", "operator.getitem(obj, key) expects two arguments."),
            new(LythonKnownCallableSignatures.Operator.SetItem, "LA3151", "operator.setitem(obj, key, value) expects three arguments.", StaticReturnShape.None),
            new(LythonKnownCallableSignatures.Operator.DelItem, "LA3151", "operator.delitem(obj, key) expects two arguments.", StaticReturnShape.None),
            new(LythonKnownCallableSignatures.Operator.Contains, "LA3151", "operator.contains(obj, value) expects two arguments.", StaticReturnShape.Boolean),
            new(LythonKnownCallableSignatures.Operator.LengthHint, "LA3151", "operator.length_hint(obj, default=0) expects one or two arguments.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.Operator.CountOf, "LA3151", "operator.countOf(a, b) expects two arguments.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.Operator.IndexOf, "LA3151", "operator.indexOf(a, b) expects two arguments.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.Operator.IAdd, "LA3151", "operator.iadd(a, b) expects two arguments."),
            new(LythonKnownCallableSignatures.Operator.ISub, "LA3151", "operator.isub(a, b) expects two arguments."),
            new(LythonKnownCallableSignatures.Operator.IMul, "LA3151", "operator.imul(a, b) expects two arguments."),
            new(LythonKnownCallableSignatures.Operator.ITrueDiv, "LA3151", "operator.itruediv(a, b) expects two arguments."),
            new(LythonKnownCallableSignatures.Operator.IFloorDiv, "LA3151", "operator.ifloordiv(a, b) expects two arguments."),
            new(LythonKnownCallableSignatures.Operator.IMod, "LA3151", "operator.imod(a, b) expects two arguments."),
            new(LythonKnownCallableSignatures.Operator.IPow, "LA3151", "operator.ipow(a, b) expects two arguments."),
            new(LythonKnownCallableSignatures.Operator.ILShift, "LA3151", "operator.ilshift(a, b) expects two arguments."),
            new(LythonKnownCallableSignatures.Operator.IRShift, "LA3151", "operator.irshift(a, b) expects two arguments."),
            new(LythonKnownCallableSignatures.Operator.IAnd, "LA3151", "operator.iand(a, b) expects two arguments."),
            new(LythonKnownCallableSignatures.Operator.IOr, "LA3151", "operator.ior(a, b) expects two arguments."),
            new(LythonKnownCallableSignatures.Operator.IXor, "LA3151", "operator.ixor(a, b) expects two arguments."),
            new(LythonKnownCallableSignatures.Operator.IConcat, "LA3151", "operator.iconcat(a, b) expects two sequence-compatible arguments."),
            new(LythonKnownCallableSignatures.Operator.Call, "LA3151", "operator.call(obj, /, *args, **kwargs) expects at least one callable argument."),
            new(LythonKnownCallableSignatures.Operator.ItemGetter, "LA3151", "operator.itemgetter(item, ...) expects one or more positional item keys."),
            new(LythonKnownCallableSignatures.Operator.AttrGetter, "LA3151", "operator.attrgetter(attr, ...) expects one or more positional string attributes."),
            new(LythonKnownCallableSignatures.Operator.MethodCaller, "LA3151", "operator.methodcaller(name, ...) expects a method-name string and optional call arguments."),
        ]);
    }

    private static class OsCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Os.ListDir, "LA3151", "os.listdir([path]) expects zero or one argument.", StaticReturnShape.ListOfString),
            new(LythonKnownCallableSignatures.Os.Walk, "LA3151", "os.walk([top][, topdown][, onerror][, followlinks]) expects zero to four arguments."),
            new(LythonKnownCallableSignatures.Os.GetCwd, "LA3151", "os.getcwd() expects no arguments.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.Os.Fspath, "LA3151", "os.fspath(path) expects one path-like argument.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.Os.FsEncode, "LA3151", "os.fsencode(filename) expects one path-like argument."),
            new(LythonKnownCallableSignatures.Os.FsDecode, "LA3151", "os.fsdecode(filename) expects one path-like argument.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.Os.GetEnv, "LA3151", "os.getenv(key[, default]) expects one or two arguments."),
            new(LythonKnownCallableSignatures.Os.PutEnv, "LA3151", "os.putenv(key, value) expects two arguments.", StaticReturnShape.None),
            new(LythonKnownCallableSignatures.Os.UnsetEnv, "LA3151", "os.unsetenv(key) expects one argument.", StaticReturnShape.None),
            new(LythonKnownCallableSignatures.Os.GetExecPath, "LA3151", "os.get_exec_path([env]) expects zero or one argument.", StaticReturnShape.ListOfString),
            new(LythonKnownCallableSignatures.Os.Stat, "LA3151", "os.stat(path) expects one path-like argument."),
            new(LythonKnownCallableSignatures.Os.Lstat, "LA3151", "os.lstat(path) expects one path-like argument."),
            new(LythonKnownCallableSignatures.Os.Scandir, "LA3151", "os.scandir([path]) expects zero or one path-like argument."),
            new(LythonKnownCallableSignatures.Os.Mkdir, "LA3151", "os.mkdir(path) expects one argument.", StaticReturnShape.None),
            new(LythonKnownCallableSignatures.Os.Makedirs, "LA3151", "os.makedirs(path[, exist_ok]) expects one or two arguments.", StaticReturnShape.None),
            new(LythonKnownCallableSignatures.Os.Remove, "LA3151", "os.remove(path) expects one argument.", StaticReturnShape.None),
            new(LythonKnownCallableSignatures.Os.Unlink, "LA3151", "os.unlink(path) expects one argument.", StaticReturnShape.None),
            new(LythonKnownCallableSignatures.Os.Rename, "LA3151", "os.rename(src, dst) expects two arguments.", StaticReturnShape.None),
            new(LythonKnownCallableSignatures.Os.Replace, "LA3151", "os.replace(src, dst) expects two arguments.", StaticReturnShape.None),
            new(LythonKnownCallableSignatures.Os.Rmdir, "LA3151", "os.rmdir(path) expects one argument.", StaticReturnShape.None),
            new(LythonKnownCallableSignatures.Os.Removedirs, "LA3151", "os.removedirs(path) expects one argument.", StaticReturnShape.None),
            new(LythonKnownCallableSignatures.Os.PathJoin, "LA3151", "os.path.join(path, *paths) expects one or more arguments.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.Os.PathSplit, "LA3151", "os.path.split(path) expects one argument."),
            new(LythonKnownCallableSignatures.Os.PathSplitExt, "LA3151", "os.path.splitext(path) expects one argument."),
            new(LythonKnownCallableSignatures.Os.PathBasename, "LA3151", "os.path.basename(path) expects one argument.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.Os.PathDirname, "LA3151", "os.path.dirname(path) expects one argument.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.Os.PathIsAbs, "LA3151", "os.path.isabs(path) expects one argument.", StaticReturnShape.Boolean),
            new(LythonKnownCallableSignatures.Os.PathNormPath, "LA3151", "os.path.normpath(path) expects one argument.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.Os.PathNormCase, "LA3151", "os.path.normcase(path) expects one argument.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.Os.PathAbsPath, "LA3151", "os.path.abspath(path) expects one argument.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.Os.PathRelPath, "LA3151", "os.path.relpath(path[, start]) expects one or two arguments.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.Os.PathCommonPath, "LA3151", "os.path.commonpath(paths) expects one argument.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.Os.PathCommonPrefix, "LA3151", "os.path.commonprefix(list) expects one argument.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.Os.PathExists, "LA3151", "os.path.exists(path) expects one argument.", StaticReturnShape.Boolean),
            new(LythonKnownCallableSignatures.Os.PathLexists, "LA3151", "os.path.lexists(path) expects one argument.", StaticReturnShape.Boolean),
            new(LythonKnownCallableSignatures.Os.PathIsFile, "LA3151", "os.path.isfile(path) expects one argument.", StaticReturnShape.Boolean),
            new(LythonKnownCallableSignatures.Os.PathIsDir, "LA3151", "os.path.isdir(path) expects one argument.", StaticReturnShape.Boolean),
            new(LythonKnownCallableSignatures.Os.PathGetSize, "LA3151", "os.path.getsize(path) expects one argument.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.Os.PathGetMTime, "LA3151", "os.path.getmtime(path) expects one argument.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Os.PathGetATime, "LA3151", "os.path.getatime(path) expects one argument.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Os.PathGetCTime, "LA3151", "os.path.getctime(path) expects one argument.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Os.PathSameFile, "LA3151", "os.path.samefile(path1, path2) expects two arguments.", StaticReturnShape.Boolean),
            new(LythonKnownCallableSignatures.Os.PathRealPath, "LA3151", "os.path.realpath(path) expects one argument.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.Os.PathExpandVars, "LA3151", "os.path.expandvars(path) expects one argument.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.Os.PathSplitDrive, "LA3151", "os.path.splitdrive(path) expects one argument."),
            new(LythonKnownCallableSignatures.Os.PathSplitRoot, "LA3151", "os.path.splitroot(path) expects one argument."),
            new(LythonKnownCallableSignatures.Os.PathIsMount, "LA3151", "os.path.ismount(path) expects one argument.", StaticReturnShape.Boolean),
        ]);
    }

    private static class PathlibCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Pathlib.Path, "LA3151", "pathlib.Path([path][, ...]) expects string or Path path arguments.", StaticReturnShape.Path),
            new(LythonKnownCallableSignatures.Pathlib.PurePath, "LA3151", "pathlib.PurePath([path][, ...]) expects string or Path path arguments.", StaticReturnShape.Path),
            new(LythonKnownCallableSignatures.Pathlib.PurePosixPath, "LA3151", "pathlib.PurePosixPath([path][, ...]) expects string or Path path arguments.", StaticReturnShape.Path),
            new(LythonKnownCallableSignatures.Pathlib.PosixPath, "LA3151", "pathlib.PosixPath([path][, ...]) expects string or Path path arguments.", StaticReturnShape.Path),
            new(LythonKnownCallableSignatures.Pathlib.PureWindowsPath, "LA3151", "pathlib.PureWindowsPath(...) is not supported by Lython's normalized POSIX-like path model."),
            new(LythonKnownCallableSignatures.Pathlib.WindowsPath, "LA3151", "pathlib.WindowsPath(...) is not supported by Lython's normalized POSIX-like path model."),
        ]);
    }

    private static class PkgutilCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Pkgutil.ModuleInfo, "LA3151", "pkgutil.ModuleInfo(module_finder, name, ispkg) expects three arguments.", StaticReturnShape.PkgutilModuleInfo),
            new(LythonKnownCallableSignatures.Pkgutil.IterModules, "LA3151", "pkgutil.iter_modules([path][, prefix]) expects zero to two arguments."),
            new(LythonKnownCallableSignatures.Pkgutil.WalkPackages, "LA3151", "pkgutil.walk_packages([path][, prefix][, onerror]) expects zero to three arguments."),
            new(LythonKnownCallableSignatures.Pkgutil.FindLoader, "LA3151", "pkgutil.find_loader(fullname) expects one argument.", StaticReturnShape.PkgutilLoader),
            new(LythonKnownCallableSignatures.Pkgutil.GetLoader, "LA3151", "pkgutil.get_loader(module_or_name) expects one argument.", StaticReturnShape.PkgutilLoader),
            new(LythonKnownCallableSignatures.Pkgutil.ExtendPath, "LA3151", "pkgutil.extend_path(path, name) expects two arguments."),
            new(LythonKnownCallableSignatures.Pkgutil.ResolveName, "LA3151", "pkgutil.resolve_name(name) expects one argument."),
            new(LythonKnownCallableSignatures.Pkgutil.GetImporter, "LA3151", "pkgutil.get_importer(path_item) expects one argument."),
            new(LythonKnownCallableSignatures.Pkgutil.IterImporters, "LA3151", "pkgutil.iter_importers([fullname]) expects zero or one argument.", StaticReturnShape.ListOfUnknown),
            new(LythonKnownCallableSignatures.Pkgutil.IterImporterModules, "LA3151", "pkgutil.iter_importer_modules(importer[, prefix]) expects one or two arguments."),
            new(LythonKnownCallableSignatures.Pkgutil.IterZipimportModules, "LA3151", "pkgutil.iter_zipimport_modules(importer[, prefix]) expects one or two arguments."),
            new(LythonKnownCallableSignatures.Pkgutil.GetData, "LA3151", "pkgutil.get_data(package, resource) expects two arguments."),
            new(LythonKnownCallableSignatures.Pkgutil.ReadCode, "LA3151", "pkgutil.read_code(stream) expects one argument."),
        ]);
    }

    private static class RandomCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Random.Class, "LA3151", "random.Random([a]) expects an optional seed.", StaticReturnShape.Random),
            new(LythonKnownCallableSignatures.Random.SystemRandom, "LA3151", "random.SystemRandom(...) is explicitly unsupported by Lython."),
            new(LythonKnownCallableSignatures.Random.Seed, "LA3151", "random.seed(a=None, version=2) expects supported seed arguments.", StaticReturnShape.None),
            new(LythonKnownCallableSignatures.Random.RandomRandom, "LA3151", "random.random() expects no arguments.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Random.GetState, "LA3151", "random.getstate() expects no arguments."),
            new(LythonKnownCallableSignatures.Random.SetState, "LA3151", "random.setstate(state) expects one state object.", StaticReturnShape.None),
            new(LythonKnownCallableSignatures.Random.RandRange, "LA3151", "random.randrange(start, stop=None, step=1) expects one to three integer arguments.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.Random.RandInt, "LA3151", "random.randint(a, b) expects two integer arguments.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.Random.Choice, "LA3151", "random.choice(seq) expects one sequence.", StaticReturnShape.Unknown),
            new(LythonKnownCallableSignatures.Random.Choices, "LA3151", "random.choices(population, weights=None, *, cum_weights=None, k=1) expects supported arguments.", StaticReturnShape.ListOfUnknown),
            new(LythonKnownCallableSignatures.Random.Shuffle, "LA3151", "random.shuffle(x) expects one mutable sequence.", StaticReturnShape.None),
            new(LythonKnownCallableSignatures.Random.Sample, "LA3151", "random.sample(population, k, *, counts=None) expects two arguments plus optional counts.", StaticReturnShape.ListOfUnknown),
            new(LythonKnownCallableSignatures.Random.GetRandBits, "LA3151", "random.getrandbits(k) expects one integer argument.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.Random.RandBytes, "LA3151", "random.randbytes(n) expects one integer argument.", StaticReturnShape.Bytes),
            new(LythonKnownCallableSignatures.Random.Uniform, "LA3151", "random.uniform(a, b) expects two real arguments.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Random.Triangular, "LA3151", "random.triangular(low=0.0, high=1.0, mode=None) expects zero to three real arguments.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Random.BetaVariate, "LA3151", "random.betavariate(alpha, beta) expects two positive real arguments.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Random.ExpVariate, "LA3151", "random.expovariate(lambd=1.0) expects zero or one real argument.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Random.GammaVariate, "LA3151", "random.gammavariate(alpha, beta) expects two positive real arguments.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Random.Gauss, "LA3151", "random.gauss(mu=0.0, sigma=1.0) expects zero to two real arguments.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Random.NormalVariate, "LA3151", "random.normalvariate(mu=0.0, sigma=1.0) expects zero to two real arguments.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Random.LogNormVariate, "LA3151", "random.lognormvariate(mu, sigma) expects two real arguments.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Random.ParetoVariate, "LA3151", "random.paretovariate(alpha) expects one positive real argument.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Random.VonMisesVariate, "LA3151", "random.vonmisesvariate(mu, kappa) expects two real arguments.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Random.WeibullVariate, "LA3151", "random.weibullvariate(alpha, beta) expects two positive real arguments.", StaticReturnShape.Float),
        ]);
    }

    private static class RegexCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Regex.Compile, "LA3151", "re.compile(pattern[, flags]) expects one or two arguments.", StaticReturnShape.RegexPattern),
            new(LythonKnownCallableSignatures.Regex.Search, "LA3151", "re.search(pattern, string[, flags][, pos][, endpos]) expects two to five arguments.", StaticReturnShape.MaybeRegexMatch),
            new(LythonKnownCallableSignatures.Regex.Match, "LA3151", "re.match(pattern, string[, flags][, pos][, endpos]) expects two to five arguments.", StaticReturnShape.MaybeRegexMatch),
            new(LythonKnownCallableSignatures.Regex.FullMatch, "LA3151", "re.fullmatch(pattern, string[, flags][, pos][, endpos]) expects two to five arguments.", StaticReturnShape.MaybeRegexMatch),
            new(LythonKnownCallableSignatures.Regex.FindAll, "LA3151", "re.findall(pattern, string[, flags][, pos][, endpos]) expects two to five arguments.", StaticReturnShape.ListOfUnknown),
            new(LythonKnownCallableSignatures.Regex.FindIter, "LA3151", "re.finditer(pattern, string[, flags][, pos][, endpos]) expects two to five arguments."),
            new(LythonKnownCallableSignatures.Regex.Sub, "LA3151", "re.sub(pattern, repl, string[, count][, flags][, pos][, endpos]) expects three to seven arguments.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.Regex.Subn, "LA3151", "re.subn(pattern, repl, string[, count][, flags][, pos][, endpos]) expects three to seven arguments."),
            new(LythonKnownCallableSignatures.Regex.Split, "LA3151", "re.split(pattern, string[, maxsplit][, flags][, pos][, endpos]) expects two to six arguments.", StaticReturnShape.ListOfString),
            new(LythonKnownCallableSignatures.Regex.Escape, "LA3151", "re.escape(string) expects one argument.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.Regex.Purge, "LA3151", "re.purge() expects no arguments."),
        ]);
    }

    private static class ShlexCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Shlex.Quote, "LA3151", "shlex.quote(s) expects one string argument.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.Shlex.Join, "LA3151", "shlex.join(split_command) expects one iterable of strings.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.Shlex.Split, "LA3151", "shlex.split(s[, comments[, posix]]) expects one to three arguments.", StaticReturnShape.ListOfString),
            new(LythonKnownCallableSignatures.Shlex.Class, "LA3151", "shlex.shlex([instream[, infile[, posix[, punctuation_chars]]]]) expects zero to four arguments."),
        ]);
    }

    private static class ShutilCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Shutil.CopyFile, "LA3151", "shutil.copyfile(src, dst, *, follow_symlinks=True) expects two path-like arguments plus supported keyword options."),
            new(LythonKnownCallableSignatures.Shutil.Copy, "LA3151", "shutil.copy(src, dst, *, follow_symlinks=True) expects two path-like arguments plus supported keyword options."),
            new(LythonKnownCallableSignatures.Shutil.Copy2, "LA3151", "shutil.copy2(src, dst, *, follow_symlinks=True) is not supported by Lython because metadata preservation is outside the host contract."),
            new(LythonKnownCallableSignatures.Shutil.Move, "LA3151", "shutil.move(src, dst, copy_function=copy) expects two path-like arguments; custom copy_function is not supported."),
            new(LythonKnownCallableSignatures.Shutil.CopyFileObj, "LA3151", "shutil.copyfileobj(fsrc, fdst, length=0) expects source and destination text file-like objects.", StaticReturnShape.None),
        ]);
    }

    private static class StatisticsCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Statistics.Mean, "LA3151", "statistics.mean(data) expects one iterable.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Statistics.FMean, "LA3151", "statistics.fmean(data[, weights]) expects iterable data and optional weights.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Statistics.GeometricMean, "LA3151", "statistics.geometric_mean(data) expects one iterable.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Statistics.HarmonicMean, "LA3151", "statistics.harmonic_mean(data, *, weights=None) expects one iterable plus optional weights.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Statistics.Median, "LA3151", "statistics.median(data) expects one iterable.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Statistics.MedianLow, "LA3151", "statistics.median_low(data) expects one iterable."),
            new(LythonKnownCallableSignatures.Statistics.MedianHigh, "LA3151", "statistics.median_high(data) expects one iterable."),
            new(LythonKnownCallableSignatures.Statistics.MedianGrouped, "LA3151", "statistics.median_grouped(data[, interval]) expects one iterable and optional interval.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Statistics.Mode, "LA3151", "statistics.mode(data) expects one iterable."),
            new(LythonKnownCallableSignatures.Statistics.MultiMode, "LA3151", "statistics.multimode(data) expects one iterable.", StaticReturnShape.ListOfUnknown),
            new(LythonKnownCallableSignatures.Statistics.PStdev, "LA3151", "statistics.pstdev(data[, mu]) expects iterable data and an optional center.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Statistics.Stdev, "LA3151", "statistics.stdev(data[, xbar]) expects iterable data and an optional center.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Statistics.PVariance, "LA3151", "statistics.pvariance(data[, mu]) expects iterable data and an optional center.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Statistics.Variance, "LA3151", "statistics.variance(data[, xbar]) expects iterable data and an optional center.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Statistics.Quantiles, "LA3151", "statistics.quantiles(data, *, n=4, method='exclusive') expects one iterable plus optional keyword parameters.", StaticReturnShape.ListOfFloat),
            new(LythonKnownCallableSignatures.Statistics.Covariance, "LA3151", "statistics.covariance(x, y) expects two iterables.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Statistics.Correlation, "LA3151", "statistics.correlation(x, y) expects two iterables.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Statistics.LinearRegression, "LA3151", "statistics.linear_regression(x, y, *, proportional=False) expects two iterables."),
            new(LythonKnownCallableSignatures.Statistics.LinearRegressionResult, "LA3151", "statistics.LinearRegression(slope, intercept) expects two fields."),
            new(LythonKnownCallableSignatures.Statistics.NormalDist, "LA3151", "statistics.NormalDist([mu][, sigma]) expects zero to two real numbers.", StaticReturnShape.StatisticsNormalDist),
            new(LythonKnownCallableSignatures.Statistics.NormalDistFromSamples, "LA3151", "statistics.NormalDist.from_samples(data) expects one iterable.", StaticReturnShape.StatisticsNormalDist),
            new(LythonKnownCallableSignatures.Statistics.Kde, "LA3151", "statistics.kde(...) is explicitly unsupported by Lython."),
            new(LythonKnownCallableSignatures.Statistics.KdeRandom, "LA3151", "statistics.kde_random(...) is explicitly unsupported by Lython."),
        ]);
    }

    private static class StructCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Struct.Pack, "LA3151", "struct.pack(format, /, *values) expects a format.", StaticReturnShape.Bytes),
            new(LythonKnownCallableSignatures.Struct.Unpack, "LA3151", "struct.unpack(format, buffer, /) expects two arguments."),
            new(LythonKnownCallableSignatures.Struct.Calcsize, "LA3151", "struct.calcsize(format, /) expects one argument.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.Struct.IterUnpack, "LA3151", "struct.iter_unpack(format, buffer, /) expects two arguments."),
        ]);
    }

    private static class SubprocessCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Subprocess.Run, "LA3151", "subprocess.run(args, ...) expects one command and supported subprocess options.", StaticReturnShape.SubprocessCompletedProcess),
            new(LythonKnownCallableSignatures.Subprocess.Call, "LA3151", "subprocess.call(args, ...) expects one command and supported subprocess options.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.Subprocess.CheckCall, "LA3151", "subprocess.check_call(args, ...) expects one command and supported subprocess options.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.Subprocess.CheckOutput, "LA3151", "subprocess.check_output(args, ...) expects one command and supported subprocess options."),
            new(LythonKnownCallableSignatures.Subprocess.Popen, "LA3151", "subprocess.Popen(args, ...) expects one command and supported buffered-process options.", StaticReturnShape.SubprocessPopen),
            new(LythonKnownCallableSignatures.Subprocess.CompletedProcess, "LA3151", "subprocess.CompletedProcess(args, returncode, stdout=None, stderr=None) expects args and returncode.", StaticReturnShape.SubprocessCompletedProcess),
            new(LythonKnownCallableSignatures.Subprocess.TimeoutExpired, "LA3151", "subprocess.TimeoutExpired(cmd, timeout, output=None, stderr=None) expects cmd and timeout."),
            new(LythonKnownCallableSignatures.Subprocess.List2Cmdline, "LA3151", "subprocess.list2cmdline(seq) expects one iterable of strings.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.Subprocess.Unsupported, "LA3151", "This subprocess helper is not supported by Lython."),
        ]);
    }

    private static class SysCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Sys.Exit, "LA3151", "sys.exit([code]) expects zero or one argument.", StaticReturnShape.None),
            new(LythonKnownCallableSignatures.Sys.GetDefaultEncoding, "LA3151", "sys.getdefaultencoding() expects no arguments.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.Sys.ExcInfo, "LA3151", "sys.exc_info() expects no arguments."),
            new(LythonKnownCallableSignatures.Sys.GetSizeOf, "LA3151", "sys.getsizeof(object[, default]) expects one or two arguments.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.Sys.SetTrace, "LA3151", "sys.settrace(function) expects one argument.", StaticReturnShape.None),
            new(LythonKnownCallableSignatures.Sys.SetProfile, "LA3151", "sys.setprofile(function) expects one argument.", StaticReturnShape.None),
            new(LythonKnownCallableSignatures.Sys.SetRecursionLimit, "LA3151", "sys.setrecursionlimit(limit) expects one argument.", StaticReturnShape.None),
            new(LythonKnownCallableSignatures.Sys.AddAuditHook, "LA3151", "sys.addaudithook(hook) expects one argument.", StaticReturnShape.None),
            new(LythonKnownCallableSignatures.Sys.Audit, "LA3151", "sys.audit(event[, args]) expects one or two arguments.", StaticReturnShape.None),
        ]);
    }

    private static class TextwrapCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Textwrap.Dedent, "LA3151", "textwrap.dedent(text) expects one argument.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.Textwrap.Indent, "LA3151", "textwrap.indent(text, prefix, predicate=None) expects two to three arguments.", StaticReturnShape.String),
        ]);
    }

    private static class TimeCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Time.TimeTime, "LA3151", "time.time() expects no arguments.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Time.TimeNs, "LA3151", "time.time_ns() expects no arguments.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.Time.Monotonic, "LA3151", "time.monotonic() expects no arguments.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Time.MonotonicNs, "LA3151", "time.monotonic_ns() expects no arguments.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.Time.PerfCounter, "LA3151", "time.perf_counter() expects no arguments.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Time.PerfCounterNs, "LA3151", "time.perf_counter_ns() expects no arguments.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.Time.Sleep, "LA3151", "time.sleep(secs) expects one real-number argument.", StaticReturnShape.None),
            new(LythonKnownCallableSignatures.Time.GetClockInfo, "LA3151", "time.get_clock_info(name) expects one string argument."),
            new(LythonKnownCallableSignatures.Time.ProcessTime, "LA3151", "time.process_time() is explicitly unsupported by Lython.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Time.ProcessTimeNs, "LA3151", "time.process_time_ns() is explicitly unsupported by Lython.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.Time.ThreadTime, "LA3151", "time.thread_time() is explicitly unsupported by Lython.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Time.ThreadTimeNs, "LA3151", "time.thread_time_ns() is explicitly unsupported by Lython.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.Time.ClockGetTime, "LA3151", "time.clock_gettime(clk_id) is explicitly unsupported by Lython.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Time.ClockGetTimeNs, "LA3151", "time.clock_gettime_ns(clk_id) is explicitly unsupported by Lython.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.Time.ClockGetRes, "LA3151", "time.clock_getres(clk_id) is explicitly unsupported by Lython.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Time.ClockSetTime, "LA3151", "time.clock_settime(clk_id, time) is explicitly unsupported by Lython.", StaticReturnShape.None),
            new(LythonKnownCallableSignatures.Time.ClockSetTimeNs, "LA3151", "time.clock_settime_ns(clk_id, time_ns) is explicitly unsupported by Lython.", StaticReturnShape.None),
            new(LythonKnownCallableSignatures.Time.PthreadGetCpuClockId, "LA3151", "time.pthread_getcpuclockid(thread_id) is explicitly unsupported by Lython.", StaticReturnShape.Integer),
            new(LythonKnownCallableSignatures.Time.Gmtime, "LA3151", "time.gmtime([secs]) expects zero or one argument."),
            new(LythonKnownCallableSignatures.Time.Localtime, "LA3151", "time.localtime([secs]) expects zero or one argument."),
            new(LythonKnownCallableSignatures.Time.Ctime, "LA3151", "time.ctime([secs]) expects zero or one argument.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.Time.Mktime, "LA3151", "time.mktime(t) expects one time tuple.", StaticReturnShape.Float),
            new(LythonKnownCallableSignatures.Time.Asctime, "LA3151", "time.asctime([t]) expects zero or one time tuple.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.Time.Strftime, "LA3151", "time.strftime(format[, t]) expects one or two arguments.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.Time.Strptime, "LA3151", "time.strptime(string[, format]) expects one or two string arguments."),
            new(LythonKnownCallableSignatures.Time.StructTime, "LA3151", "time.struct_time(sequence, /) expects one positional sequence."),
            new(LythonKnownCallableSignatures.Time.Tzset, "LA3151", "time.tzset() is explicitly unsupported by Lython."),
        ]);
    }

    private static class TypingCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Typing.TypeVar, "LA3151", "typing.TypeVar(name, ...) expects at least one argument."),
            new(LythonKnownCallableSignatures.Typing.NewType, "LA3151", "typing.NewType(name, tp) expects two arguments."),
            new(LythonKnownCallableSignatures.Typing.Cast, "LA3151", "typing.cast(typ, val) expects two arguments."),
            new(LythonKnownCallableSignatures.Typing.GetOrigin, "LA3151", "typing.get_origin(tp) expects one argument."),
            new(LythonKnownCallableSignatures.Typing.GetArgs, "LA3151", "typing.get_args(tp) expects one argument."),
            new(LythonKnownCallableSignatures.Typing.NamedTuple, "LA3151", "typing.NamedTuple(typename[, fields]) expects at least one argument."),
            new(LythonKnownCallableSignatures.Typing.TypedDict, "LA3151", "typing.TypedDict(typename[, fields]) expects at least one argument."),
        ]);
    }

    private static class UrlLibCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.UrlLib.UrlSplit, "LA3151", "urllib.parse.urlsplit(url, scheme='', allow_fragments=True) expects one to three arguments.", StaticReturnShape.Unknown),
            new(LythonKnownCallableSignatures.UrlLib.UrlParse, "LA3151", "urllib.parse.urlparse(url, scheme='', allow_fragments=True) expects one to three arguments.", StaticReturnShape.Unknown),
            new(LythonKnownCallableSignatures.UrlLib.UrlUnsplit, "LA3151", "urllib.parse.urlunsplit(components) expects one argument.", StaticReturnShape.Unknown),
            new(LythonKnownCallableSignatures.UrlLib.UrlUnparse, "LA3151", "urllib.parse.urlunparse(components) expects one argument.", StaticReturnShape.Unknown),
            new(LythonKnownCallableSignatures.UrlLib.UrlQuote, "LA3151", "urllib.parse.quote(string, safe='/', encoding=None, errors=None) expects one to four arguments.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.UrlLib.UrlQuotePlus, "LA3151", "urllib.parse.quote_plus(string, safe='', encoding=None, errors=None) expects one to four arguments.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.UrlLib.UrlQuoteFromBytes, "LA3151", "urllib.parse.quote_from_bytes(bs, safe='/') expects one to two arguments.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.UrlLib.UrlUnquote, "LA3151", "urllib.parse.unquote(string, encoding='utf-8', errors='replace') expects one to three arguments.", StaticReturnShape.Unknown),
            new(LythonKnownCallableSignatures.UrlLib.UrlUnquotePlus, "LA3151", "urllib.parse.unquote_plus(string, encoding='utf-8', errors='replace') expects one to three arguments.", StaticReturnShape.Unknown),
            new(LythonKnownCallableSignatures.UrlLib.UrlUnquoteToBytes, "LA3151", "urllib.parse.unquote_to_bytes(string) expects one argument.", StaticReturnShape.Bytes),
            new(LythonKnownCallableSignatures.UrlLib.UrlEncode, "LA3151", "urllib.parse.urlencode(query, doseq=False, safe='', encoding=None, errors=None, quote_via=quote_plus) expects one to six arguments.", StaticReturnShape.String),
            new(LythonKnownCallableSignatures.UrlLib.Qs, "LA3151", "urllib.parse.parse_qs(qs, keep_blank_values=False, strict_parsing=False, encoding='utf-8', errors='replace', max_num_fields=None, separator='&') expects one to seven arguments.", StaticReturnShape.Unknown),
            new(LythonKnownCallableSignatures.UrlLib.Qsl, "LA3151", "urllib.parse.parse_qsl(qs, keep_blank_values=False, strict_parsing=False, encoding='utf-8', errors='replace', max_num_fields=None, separator='&') expects one to seven arguments.", StaticReturnShape.ListOfUnknown),
        ]);
    }

    private static class ElementTreeCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.ElementTree.FromString, "LA3151", "xml.etree.ElementTree.fromstring(text, parser=None) expects one or two arguments."),
        ]);
    }

    private static class ZipfileCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Zipfile.ZipFile, "LA3151", "zipfile.ZipFile(file[, mode[, compression[, allowZip64[, compresslevel[, strict_timestamps[, metadata_encoding]]]]]]) expects one to seven arguments."),
            new(LythonKnownCallableSignatures.Zipfile.ZipIsZipFile, "LA3151", "zipfile.is_zipfile(filename) expects one argument."),
            new(LythonKnownCallableSignatures.Zipfile.ZipInfo, "LA3151", "zipfile.ZipInfo([filename[, date_time]]) expects zero to two arguments."),
        ]);
    }

    private static class ZlibCalls
    {
        internal static readonly Dictionary<string, StaticKnownCallContract> ByName = Index(
        [
            new(LythonKnownCallableSignatures.Zlib.Compress, "LA3151", "zlib.compress(data, /, level=-1, wbits=15) expects one to three arguments.", StaticReturnShape.Bytes),
            new(LythonKnownCallableSignatures.Zlib.Decompress, "LA3151", "zlib.decompress(data, /, wbits=15, bufsize=16384) expects one to three arguments.", StaticReturnShape.Bytes),
        ]);
    }
}

using Lokad.Lython.Runtime;

namespace Lokad.Lython.Frontend;

internal static partial class StaticContracts
{
    private static class StringMemberCatalog
    {
        internal static readonly Dictionary<string, (StaticCallableContract? Call, StaticReturnShape? Return)> ByName = Create();

        private static Dictionary<string, (StaticCallableContract? Call, StaticReturnShape? Return)> Create()
        {
            var entries = new Dictionary<string, (StaticCallableContract?, StaticReturnShape?)>(61, StringComparer.Ordinal);
            Add("lower", 0, 0, "str.lower() expects no arguments.", StaticReturnShape.String);
            Add("__iter__", 0, 0, "str.__iter__() expects no arguments.", null);
            Add("__len__", 0, 0, "str.__len__() expects no arguments.", null);
            Add("__contains__", 1, 1, "str.__contains__(item) expects one argument.", null, "item");
            Add("__getitem__", 1, 1, "str.__getitem__(index) expects one argument.", null, "index");
            Add("__add__", 1, 1, "str.__add__(value) expects one argument.", null, "value");
            Add("__mul__", 1, 1, "str.__mul__(value) expects one argument.", null, "value");
            Add("__rmul__", 1, 1, "str.__rmul__(value) expects one argument.", null, "value");
            Add("__eq__", 1, 1, "str.__eq__(value) expects one argument.", null, "value");
            Add("__ne__", 1, 1, "str.__ne__(value) expects one argument.", null, "value");
            Add("__lt__", 1, 1, "str.__lt__(value) expects one argument.", null, "value");
            Add("__le__", 1, 1, "str.__le__(value) expects one argument.", null, "value");
            Add("__gt__", 1, 1, "str.__gt__(value) expects one argument.", null, "value");
            Add("__ge__", 1, 1, "str.__ge__(value) expects one argument.", null, "value");
            Add("__hash__", 0, 0, "str.__hash__() expects no arguments.", null);
            Add("capitalize", 0, 0, "str.capitalize() expects no arguments.", StaticReturnShape.String);
            Add("islower", 0, 0, "str.islower() expects no arguments.", StaticReturnShape.Boolean);
            Add("upper", 0, 0, "str.upper() expects no arguments.", StaticReturnShape.String);
            Add("swapcase", 0, 0, "str.swapcase() expects no arguments.", StaticReturnShape.String);
            Add("title", 0, 0, "str.title() expects no arguments.", StaticReturnShape.String);
            Add("casefold", 0, 0, "str.casefold() expects no arguments.", StaticReturnShape.String);
            Add("isupper", 0, 0, "str.isupper() expects no arguments.", StaticReturnShape.Boolean);
            Add("isalpha", 0, 0, "str.isalpha() expects no arguments.", StaticReturnShape.Boolean);
            Add("isdigit", 0, 0, "str.isdigit() expects no arguments.", StaticReturnShape.Boolean);
            Add("isalnum", 0, 0, "str.isalnum() expects no arguments.", StaticReturnShape.Boolean);
            Add("isspace", 0, 0, "str.isspace() expects no arguments.", StaticReturnShape.Boolean);
            Add("strip", 0, 1, "str.strip([chars]) expects zero or one argument.", StaticReturnShape.String, "chars");
            Add("lstrip", 0, 1, "str.lstrip([chars]) expects zero or one argument.", StaticReturnShape.String, "chars");
            Add("rstrip", 0, 1, "str.rstrip([chars]) expects zero or one argument.", StaticReturnShape.String, "chars");
            Add("center", 1, 2, "str.center(width[, fillchar]) expects one or two arguments.", StaticReturnShape.String, "width", "fillchar");
            Add("ljust", 1, 2, "str.ljust(width[, fillchar]) expects one or two arguments.", StaticReturnShape.String, "width", "fillchar");
            Add("rjust", 1, 2, "str.rjust(width[, fillchar]) expects one or two arguments.", StaticReturnShape.String, "width", "fillchar");
            Add("zfill", 1, 1, "str.zfill(width) expects one argument.", StaticReturnShape.String, "width");
            Add("expandtabs", 0, 1, "str.expandtabs([tabsize]) expects zero or one argument.", StaticReturnShape.String, "tabsize");
            Add("replace", 2, 3, "str.replace(old, new[, count]) expects two or three arguments.", StaticReturnShape.String, "old", "new", "count");
            Add("removeprefix", 1, 1, "str.removeprefix(prefix) expects one argument.", StaticReturnShape.String, "prefix");
            Add("removesuffix", 1, 1, "str.removesuffix(suffix) expects one argument.", StaticReturnShape.String, "suffix");
            Add("split", 0, 2, "str.split([sep[, maxsplit]]) expects zero to two arguments.", StaticReturnShape.ListOfString, "sep", "maxsplit");
            Add("rsplit", 0, 2, "str.rsplit([sep[, maxsplit]]) expects zero to two arguments.", StaticReturnShape.ListOfString, "sep", "maxsplit");
            Add("splitlines", 0, 1, "str.splitlines([keepends]) expects zero or one argument.", StaticReturnShape.ListOfString, "keepends");
            Add("startswith", 1, 3, "str.startswith(prefix[, start[, end]]) expects one to three arguments.", StaticReturnShape.Boolean, "prefix", "start", "end");
            Add("endswith", 1, 3, "str.endswith(suffix[, start[, end]]) expects one to three arguments.", StaticReturnShape.Boolean, "suffix", "start", "end");
            Add("find", 1, 3, "str.find(sub[, start[, end]]) expects one to three arguments.", StaticReturnShape.Integer, "sub", "start", "end");
            Add("index", 1, 3, "str.index(sub[, start[, end]]) expects one to three arguments.", StaticReturnShape.Integer, "sub", "start", "end");
            Add("rfind", 1, 3, "str.rfind(sub[, start[, end]]) expects one to three arguments.", StaticReturnShape.Integer, "sub", "start", "end");
            Add("rindex", 1, 3, "str.rindex(sub[, start[, end]]) expects one to three arguments.", StaticReturnShape.Integer, "sub", "start", "end");
            Add("count", 1, 3, "str.count(sub[, start[, end]]) expects one to three arguments.", StaticReturnShape.Integer, "sub", "start", "end");
            Add("partition", 1, 1, "str.partition(sep) expects one argument.", null, "sep");
            Add("rpartition", 1, 1, "str.rpartition(sep) expects one argument.", null, "sep");
            Add("join", 1, 1, "str.join(iterable) expects one argument.", StaticReturnShape.String, "iterable");
            Add("format_map", 1, 1, "str.format_map(mapping) expects one argument.", StaticReturnShape.String, "mapping");
            Add("isascii", 0, 0, "str.isascii() expects no arguments.", StaticReturnShape.Boolean);
            Add("isdecimal", 0, 0, "str.isdecimal() expects no arguments.", StaticReturnShape.Boolean);
            Add("isidentifier", 0, 0, "str.isidentifier() expects no arguments.", StaticReturnShape.Boolean);
            Add("isnumeric", 0, 0, "str.isnumeric() expects no arguments.", StaticReturnShape.Boolean);
            Add("isprintable", 0, 0, "str.isprintable() expects no arguments.", StaticReturnShape.Boolean);
            Add("istitle", 0, 0, "str.istitle() expects no arguments.", StaticReturnShape.Boolean);
            Add("translate", 1, 1, "str.translate(table) expects one argument.", null, "table");
            Add("maketrans", 1, 3, "str.maketrans(x[, y[, z]]) expects one to three arguments.", null);
            Add("encode", 0, 2, "str.encode([encoding][, errors]) expects zero to two arguments.", StaticReturnShape.Bytes, "encoding", "errors");
            entries.Add("format", (null, StaticReturnShape.String));
            return entries;

            void Add(string name, int minimum, ArgumentCountLimit maximum, string message, StaticReturnShape? returns, params string[] parameters)
            {
                var call = parameters.Length == 0
                    ? new StaticCallableContract(AbstractValueKind.String, name, minimum, maximum, "LA3147", message)
                    : new StaticCallableContract(AbstractValueKind.String, name, minimum, maximum, "LA3147", message, parameterNames: parameters);
                entries.Add(name, (call, returns));
            }
        }
    }

    private static bool TryGetStringCallableContract(AbstractValueKind kind, string name, out StaticCallableContract contract)
    {
        if (StringMemberCatalog.ByName.TryGetValue(name, out var member) && member.Call is { } call)
        {
            contract = kind == AbstractValueKind.StringType ? call with { ReceiverKind = kind } : call;
            return true;
        }

        contract = default;
        return false;
    }
}

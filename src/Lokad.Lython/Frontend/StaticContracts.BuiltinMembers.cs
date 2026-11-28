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
                entries.Add(name, (CreateBuiltinMemberCall(AbstractValueKind.String, name, minimum, maximum,
                    "LA3147", message, StaticMutationKind.None, parameters), returns));
            }
        }
    }

    private static class BytesMemberCatalog
    {
        internal static readonly Dictionary<string, (StaticCallableContract? Call, StaticReturnShape? Return)> ByName = Create();

        private static Dictionary<string, (StaticCallableContract? Call, StaticReturnShape? Return)> Create()
        {
            var entries = new Dictionary<string, (StaticCallableContract?, StaticReturnShape?)>(56, StringComparer.Ordinal);
            Add("decode", 0, 2, "LA3147", "bytes.decode([encoding][, errors]) expects zero to two arguments.", StaticReturnShape.String, "encoding", "errors");
            Add("fromhex", 1, 1, "LA3166", "bytes.fromhex(string) expects one argument.", null);
            Add("maketrans", 2, 2, "LA3172", "bytes.maketrans(from, to) expects two arguments.", null, "from", "to");
            Add("translate", 1, 2, "LA3172", "bytes.translate(table[, delete]) expects one or two arguments.", null, "table", "delete");
            Add("count", 1, 3, "LA3174", "bytes.count(sub[, start[, end]]) expects one to three arguments.", StaticReturnShape.Integer, "sub", "start", "end");
            Add("find", 1, 3, "LA3174", "bytes.find(sub[, start[, end]]) expects one to three arguments.", StaticReturnShape.Integer, "sub", "start", "end");
            Add("index", 1, 3, "LA3174", "bytes.index(sub[, start[, end]]) expects one to three arguments.", StaticReturnShape.Integer, "sub", "start", "end");
            Add("rfind", 1, 3, "LA3174", "bytes.rfind(sub[, start[, end]]) expects one to three arguments.", StaticReturnShape.Integer, "sub", "start", "end");
            Add("rindex", 1, 3, "LA3174", "bytes.rindex(sub[, start[, end]]) expects one to three arguments.", StaticReturnShape.Integer, "sub", "start", "end");
            Add("hex", 0, 2, "LA3173", "bytes.hex([sep[, bytes_per_sep]]) expects zero to two arguments.", StaticReturnShape.String, "sep", "bytes_per_sep");
            Add("startswith", 1, 3, "LA3175", "bytes.startswith(prefix[, start[, end]]) expects one to three arguments.", StaticReturnShape.Boolean, "prefix", "start", "end");
            Add("endswith", 1, 3, "LA3175", "bytes.endswith(suffix[, start[, end]]) expects one to three arguments.", StaticReturnShape.Boolean, "suffix", "start", "end");
            Add("replace", 2, 3, "LA3176", "bytes.replace(old, new[, count]) expects two or three arguments.", StaticReturnShape.Bytes, "old", "new", "count");
            Add("removeprefix", 1, 1, "LA3178", "bytes.removeprefix(prefix) expects one argument.", StaticReturnShape.Bytes, "prefix");
            Add("removesuffix", 1, 1, "LA3178", "bytes.removesuffix(suffix) expects one argument.", StaticReturnShape.Bytes, "suffix");
            Add("isalnum", 0, 0, "LA3177", "bytes.isalnum() expects no arguments.", StaticReturnShape.Boolean);
            Add("isalpha", 0, 0, "LA3177", "bytes.isalpha() expects no arguments.", StaticReturnShape.Boolean);
            Add("isascii", 0, 0, "LA3177", "bytes.isascii() expects no arguments.", StaticReturnShape.Boolean);
            Add("isdigit", 0, 0, "LA3177", "bytes.isdigit() expects no arguments.", StaticReturnShape.Boolean);
            Add("islower", 0, 0, "LA3177", "bytes.islower() expects no arguments.", StaticReturnShape.Boolean);
            Add("isspace", 0, 0, "LA3177", "bytes.isspace() expects no arguments.", StaticReturnShape.Boolean);
            Add("istitle", 0, 0, "LA3177", "bytes.istitle() expects no arguments.", StaticReturnShape.Boolean);
            Add("isupper", 0, 0, "LA3177", "bytes.isupper() expects no arguments.", StaticReturnShape.Boolean);
            Add("expandtabs", 0, 1, "LA3185", "bytes.expandtabs([tabsize]) expects zero or one argument.", StaticReturnShape.Bytes, "tabsize");
            Add("center", 1, 2, "LA3184", "bytes.center(width[, fillchar]) expects one or two arguments.", StaticReturnShape.Bytes, "width", "fillchar");
            Add("ljust", 1, 2, "LA3184", "bytes.ljust(width[, fillchar]) expects one or two arguments.", StaticReturnShape.Bytes, "width", "fillchar");
            Add("rjust", 1, 2, "LA3184", "bytes.rjust(width[, fillchar]) expects one or two arguments.", StaticReturnShape.Bytes, "width", "fillchar");
            Add("zfill", 1, 1, "LA3184", "bytes.zfill(width) expects one argument.", StaticReturnShape.Bytes, "width");
            Add("join", 1, 1, "LA3183", "bytes.join(iterable) expects one argument.", StaticReturnShape.Bytes, "iterable");
            Add("partition", 1, 1, "LA3182", "bytes.partition(sep) expects one argument.", null, "sep");
            Add("rpartition", 1, 1, "LA3182", "bytes.rpartition(sep) expects one argument.", null, "sep");
            Add("split", 0, 2, "LA3181", "bytes.split([sep[, maxsplit]]) expects zero to two arguments.", StaticReturnShape.ListOfBytes, "sep", "maxsplit");
            Add("rsplit", 0, 2, "LA3181", "bytes.rsplit([sep[, maxsplit]]) expects zero to two arguments.", StaticReturnShape.ListOfBytes, "sep", "maxsplit");
            Add("splitlines", 0, 1, "LA3181", "bytes.splitlines([keepends]) expects zero or one argument.", StaticReturnShape.ListOfBytes, "keepends");
            Add("strip", 0, 1, "LA3180", "bytes.strip([chars]) expects zero or one argument.", StaticReturnShape.Bytes, "chars");
            Add("lstrip", 0, 1, "LA3180", "bytes.lstrip([chars]) expects zero or one argument.", StaticReturnShape.Bytes, "chars");
            Add("rstrip", 0, 1, "LA3180", "bytes.rstrip([chars]) expects zero or one argument.", StaticReturnShape.Bytes, "chars");
            Add("capitalize", 0, 0, "LA3179", "bytes.capitalize() expects no arguments.", StaticReturnShape.Bytes);
            Add("lower", 0, 0, "LA3179", "bytes.lower() expects no arguments.", StaticReturnShape.Bytes);
            Add("swapcase", 0, 0, "LA3179", "bytes.swapcase() expects no arguments.", StaticReturnShape.Bytes);
            Add("title", 0, 0, "LA3179", "bytes.title() expects no arguments.", StaticReturnShape.Bytes);
            Add("upper", 0, 0, "LA3179", "bytes.upper() expects no arguments.", StaticReturnShape.Bytes);
            Add("__iter__", 0, 0, "LA3179", "bytes.__iter__() expects no arguments.", null);
            Add("__len__", 0, 0, "LA3179", "bytes.__len__() expects no arguments.", StaticReturnShape.Integer);
            Add("__contains__", 1, 1, "LA3179", "bytes.__contains__(item) expects one argument.", null, "item");
            Add("__getitem__", 1, 1, "LA3179", "bytes.__getitem__(index) expects one argument.", null, "index");
            Add("__add__", 1, 1, "LA3179", "bytes.__add__(value) expects one argument.", null, "value");
            Add("__mul__", 1, 1, "LA3179", "bytes.__mul__(value) expects one argument.", null, "value");
            Add("__rmul__", 1, 1, "LA3179", "bytes.__rmul__(value) expects one argument.", null, "value");
            Add("__eq__", 1, 1, "LA3179", "bytes.__eq__(value) expects one argument.", null, "value");
            Add("__ne__", 1, 1, "LA3179", "bytes.__ne__(value) expects one argument.", null, "value");
            Add("__lt__", 1, 1, "LA3179", "bytes.__lt__(value) expects one argument.", null, "value");
            Add("__le__", 1, 1, "LA3179", "bytes.__le__(value) expects one argument.", null, "value");
            Add("__gt__", 1, 1, "LA3179", "bytes.__gt__(value) expects one argument.", null, "value");
            Add("__ge__", 1, 1, "LA3179", "bytes.__ge__(value) expects one argument.", null, "value");
            Add("__hash__", 0, 0, "LA3179", "bytes.__hash__() expects no arguments.", null);
            return entries;

            void Add(string name, int minimum, ArgumentCountLimit maximum, string code, string message,
                StaticReturnShape? returns, params string[] parameters)
            {
                entries.Add(name, (CreateBuiltinMemberCall(AbstractValueKind.Bytes, name, minimum, maximum,
                    code, message, StaticMutationKind.None, parameters), returns));
            }
        }
    }

    private static class ListMemberCatalog
    {
        internal static readonly Dictionary<string, (StaticCallableContract? Call, StaticReturnShape? Return)> ByName = Create();

        private static Dictionary<string, (StaticCallableContract? Call, StaticReturnShape? Return)> Create()
        {
            var entries = new Dictionary<string, (StaticCallableContract?, StaticReturnShape?)>(27, StringComparer.Ordinal);
            Add("append", 1, 1, "LA3121", "list.append(value) expects one argument.", StaticReturnShape.None, StaticMutationKind.MutatesReceiver, "value");
            Add("extend", 1, 1, "LA3122", "list.extend(iterable) expects one argument.", StaticReturnShape.None, StaticMutationKind.MutatesReceiver, "iterable");
            Add("index", 1, 3, "LA3123", "list.index(value[, start[, stop]]) expects one to three arguments.", StaticReturnShape.Integer, StaticMutationKind.None, "value", "start", "stop");
            Add("count", 1, 1, "LA3123", "list.count(value) expects one argument.", StaticReturnShape.Integer, StaticMutationKind.None, "value");
            Add("insert", 2, 2, "LA3123", "list.insert(index, value) expects two arguments.", StaticReturnShape.None, StaticMutationKind.MutatesReceiver, "index", "value");
            Add("remove", 1, 1, "LA3123", "list.remove(value) expects one argument.", StaticReturnShape.None, StaticMutationKind.MutatesReceiver, "value");
            Add("pop", 0, 1, "LA3123", "list.pop([index]) expects zero or one argument.", StaticReturnShape.ListElement, StaticMutationKind.MutatesReceiver, "index");
            Add("reverse", 0, 0, "LA3123", "list.reverse() expects no arguments.", StaticReturnShape.None, StaticMutationKind.MutatesReceiver);
            Add("sort", 0, 2, "LA3123", "list.sort(*, key=None, reverse=False) expects optional key/reverse keyword arguments.", StaticReturnShape.None, StaticMutationKind.MutatesReceiver, "key", "reverse");
            Add("copy", 0, 0, "LA3124", "list.copy() expects no arguments.", StaticReturnShape.ListSame, StaticMutationKind.None);
            Add("__iter__", 0, 0, "LA3124", "list.__iter__() expects no arguments.", null, StaticMutationKind.None);
            Add("__len__", 0, 0, "LA3124", "list.__len__() expects no arguments.", StaticReturnShape.Integer, StaticMutationKind.None);
            Add("__contains__", 1, 1, "LA3124", "list.__contains__(item) expects one argument.", null, StaticMutationKind.None, "item");
            Add("__getitem__", 1, 1, "LA3124", "list.__getitem__(index) expects one argument.", null, StaticMutationKind.None, "index");
            Add("__add__", 1, 1, "LA3124", "list.__add__(value) expects one argument.", null, StaticMutationKind.None, "value");
            Add("__mul__", 1, 1, "LA3124", "list.__mul__(value) expects one argument.", null, StaticMutationKind.None, "value");
            Add("__rmul__", 1, 1, "LA3124", "list.__rmul__(value) expects one argument.", null, StaticMutationKind.None, "value");
            Add("__reversed__", 0, 0, "LA3124", "list.__reversed__() expects no arguments.", null, StaticMutationKind.None);
            Add("__eq__", 1, 1, "LA3124", "list.__eq__(value) expects one argument.", null, StaticMutationKind.None, "value");
            Add("__ne__", 1, 1, "LA3124", "list.__ne__(value) expects one argument.", null, StaticMutationKind.None, "value");
            Add("__lt__", 1, 1, "LA3124", "list.__lt__(value) expects one argument.", null, StaticMutationKind.None, "value");
            Add("__le__", 1, 1, "LA3124", "list.__le__(value) expects one argument.", null, StaticMutationKind.None, "value");
            Add("__gt__", 1, 1, "LA3124", "list.__gt__(value) expects one argument.", null, StaticMutationKind.None, "value");
            Add("__ge__", 1, 1, "LA3124", "list.__ge__(value) expects one argument.", null, StaticMutationKind.None, "value");
            Add("__setitem__", 2, 2, "LA3124", "list.__setitem__(index, value) expects two arguments.", StaticReturnShape.None, StaticMutationKind.MutatesReceiver, "index", "value");
            Add("__delitem__", 1, 1, "LA3124", "list.__delitem__(index) expects one argument.", StaticReturnShape.None, StaticMutationKind.MutatesReceiver, "index");
            Add("clear", 0, 0, "LA3125", "list.clear() expects no arguments.", StaticReturnShape.None, StaticMutationKind.MutatesReceiver);
            return entries;

            void Add(string name, int minimum, ArgumentCountLimit maximum, string code, string message,
                StaticReturnShape? returns, StaticMutationKind mutation, params string[] parameters)
            {
                entries.Add(name, (CreateBuiltinMemberCall(AbstractValueKind.List, name, minimum, maximum,
                    code, message, mutation, parameters), returns));
            }
        }
    }

    private static Dictionary<string, (StaticCallableContract? Call, StaticReturnShape? Return)>? GetBuiltinMemberCatalog(AbstractValueKind kind)
        => kind switch
        {
            AbstractValueKind.String or AbstractValueKind.StringType => StringMemberCatalog.ByName,
            AbstractValueKind.Bytes or AbstractValueKind.BytesType => BytesMemberCatalog.ByName,
            AbstractValueKind.List or AbstractValueKind.ListType => ListMemberCatalog.ByName,
            _ => null,
        };

    private static StaticCallableContract CreateBuiltinMemberCall(AbstractValueKind kind, string name, int minimum,
        ArgumentCountLimit maximum, string code, string message, StaticMutationKind mutation, string[] parameters)
        => parameters.Length == 0
            ? new StaticCallableContract(kind, name, minimum, maximum, code, message, mutation)
            : new StaticCallableContract(kind, name, minimum, maximum, code, message, mutation, parameters);
}

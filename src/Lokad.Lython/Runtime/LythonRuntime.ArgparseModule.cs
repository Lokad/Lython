using System.Globalization;
using System.Numerics;
using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static PyString RequireArgparseStringValue(object value, string name, string owner, LythonSourceSpan span)
    {
        if (!PyStringOps.TryAsString(value, out var text))
        {
            throw new LythonRuntimeException("TypeError", $"{owner}(..., {name}=...) expects a string.", span);
        }

        return text;
    }

    private static string? OptionalArgparseString(object value, string name, string owner, LythonSourceSpan span)
    {
        if (ReferenceEquals(value, PyNone.Instance))
        {
            return null;
        }

        return RequireArgparseStringValue(value, name, owner, span).AsString();
    }

    private static bool RequireArgparseBool(object value, string name, string owner, LythonSourceSpan span)
    {
        if (value is bool boolean)
        {
            return boolean;
        }

        throw new LythonRuntimeException("TypeError", $"{owner}(..., {name}=...) expects a bool.", span);
    }

    private sealed class ArgparseModule : PyModule
    {
        public static readonly ArgparseModule Instance = new();

        private static readonly IReadOnlyDictionary<string, ArgparseFormatterClass> FormatterClasses =
            new Dictionary<string, ArgparseFormatterClass>(StringComparer.Ordinal)
            {
                ["HelpFormatter"] = new("HelpFormatter"),
                ["RawDescriptionHelpFormatter"] = new("RawDescriptionHelpFormatter"),
                ["RawTextHelpFormatter"] = new("RawTextHelpFormatter"),
                ["ArgumentDefaultsHelpFormatter"] = new("ArgumentDefaultsHelpFormatter"),
            };

        private ArgparseModule() : base("argparse")
        {
        }

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "ArgumentParser" => new ArgumentParserFactory(),
                "Namespace" => new ArgparseNamespaceFactory(),
                "FileType" => new BuiltinCallable(LythonKnownCallableSignatures.ArgparseFileType, CreateFileType),
                "ArgumentError" => new ExceptionTypeValue("ArgumentError"),
                "ArgumentTypeError" => new ExceptionTypeValue("ArgumentTypeError"),
                "SUPPRESS" => ArgparseSuppressValue.Instance,
                "OPTIONAL" => PyString.FromString("?"),
                "ZERO_OR_MORE" => PyString.FromString("*"),
                "ONE_OR_MORE" => PyString.FromString("+"),
                "PARSER" => PyString.FromString("A..."),
                "REMAINDER" => PyString.FromString("..."),
                "HelpFormatter" => FormatterClasses["HelpFormatter"],
                "RawDescriptionHelpFormatter" => FormatterClasses["RawDescriptionHelpFormatter"],
                "RawTextHelpFormatter" => FormatterClasses["RawTextHelpFormatter"],
                "ArgumentDefaultsHelpFormatter" => FormatterClasses["ArgumentDefaultsHelpFormatter"],
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static object CreateParser(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var options = new ArgparseParserOptions(DefaultProgramName(context));
            var positionalIndex = 0;
            var assigned = new HashSet<string>(StringComparer.Ordinal);

            foreach (var argument in arguments)
            {
                if (argument.Name is null)
                {
                    if (positionalIndex >= ArgparseParserOptions.SupportedConstructorParameters.Length)
                    {
                        throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser(...) received too many positional arguments.", span);
                    }

                    AssignParserOption(options, ArgparseParserOptions.SupportedConstructorParameters[positionalIndex++], argument.Value, assigned, span);
                    continue;
                }

                if (argument.Name is "fromfile_prefix_chars" or "parents" or "conflict_handler" or "prefix_chars" or "argument_default")
                {
                    throw new LythonRuntimeException(
                        "NotImplementedError",
                        $"argparse.ArgumentParser(..., {argument.Name}=...) is not supported by Lython.",
                        span);
                }

                if (!ArgparseParserOptions.SupportedConstructorParameters.Contains(argument.Name, StringComparer.Ordinal))
                {
                    throw new LythonRuntimeException("TypeError", $"argparse.ArgumentParser(...) got an unexpected keyword argument '{argument.Name}'.", span);
                }

                AssignParserOption(options, argument.Name, argument.Value, assigned, span);
            }

            return new ArgumentParserObject(options);
        }

        private static void AssignParserOption(
            ArgparseParserOptions options,
            string name,
            object value,
            HashSet<string> assigned,
            LythonSourceSpan span)
        {
            if (!assigned.Add(name))
            {
                throw new LythonRuntimeException("TypeError", $"argparse.ArgumentParser(...) got multiple values for argument '{name}'.", span);
            }

            switch (name)
            {
                case "prog":
                    options.Prog = OptionalArgparseString(value, "prog", "argparse.ArgumentParser", span) ?? options.Prog;
                    break;
                case "usage":
                    options.Usage = OptionalArgparseString(value, "usage", "argparse.ArgumentParser", span);
                    break;
                case "description":
                    options.Description = OptionalArgparseString(value, "description", "argparse.ArgumentParser", span);
                    break;
                case "epilog":
                    options.Epilog = OptionalArgparseString(value, "epilog", "argparse.ArgumentParser", span);
                    break;
                case "formatter_class":
                    options.FormatterClass = ReferenceEquals(value, PyNone.Instance) ? null : value;
                    break;
                case "add_help":
                    options.AddHelp = RequireArgparseBool(value, "add_help", "argparse.ArgumentParser", span);
                    break;
                case "allow_abbrev":
                    options.AllowAbbrev = RequireArgparseBool(value, "allow_abbrev", "argparse.ArgumentParser", span);
                    break;
                case "exit_on_error":
                    options.ExitOnError = RequireArgparseBool(value, "exit_on_error", "argparse.ArgumentParser", span);
                    break;
            }
        }

        private static object CreateFileType(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            var mode = arguments.Length >= 1 && !ReferenceEquals(arguments[0], PyNone.Instance)
                ? RequireArgparseStringValue(arguments[0], "mode", "argparse.FileType", span).AsString()
                : "r";
            if (mode.Contains('b'))
            {
                throw new LythonRuntimeException("ValueError", "argparse.FileType only supports host-mediated text modes.", span);
            }

            mode = ParseTextOpenMode(PyString.FromString(mode), "argparse.FileType", span);
            if (arguments.Length >= 2)
            {
                ValidateTextBuffering(arguments[1], "argparse.FileType", span);
            }

            if (mode is not ("r" or "w" or "a"))
            {
                throw new LythonRuntimeException("ValueError", "argparse.FileType only supports modes 'r', 'w', and 'a'.", span);
            }

            var encodingMode = arguments.Length >= 3
                ? ParseTextEncoding(arguments[2], "argparse.FileType", span)
                : TextEncodingMode.Utf8;
            var errorsMode = arguments.Length >= 4
                ? ParseTextErrors(arguments[3], "argparse.FileType", span)
                : TextErrorMode.Strict;
            var encoding = arguments.Length >= 3
                ? encodingMode switch
                {
                    TextEncodingMode.Utf8Bom => "utf-8-sig",
                    TextEncodingMode.Latin1 => "latin-1",
                    _ => "utf-8"
                }
                : null;
            var errors = arguments.Length >= 4
                ? errorsMode switch
                {
                    TextErrorMode.Ignore => "ignore",
                    TextErrorMode.Replace => "replace",
                    TextErrorMode.BackslashReplace => "backslashreplace",
                    _ => "strict"
                }
                : null;
            return new ArgparseFileTypeObject(mode, encoding, errors);
        }

        private static string DefaultProgramName(ExecutionContext context)
        {
            if (context.SourcePath is not null)
            {
                var normalized = context.SourcePath.Replace('\\', '/');
                var slash = normalized.LastIndexOf('/');
                return slash >= 0 ? normalized[(slash + 1)..] : normalized;
            }

            return "lython";
        }

        private sealed class ArgumentParserFactory : ICallable
        {
            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecutionBudget(span);
                return CreateParser(arguments, span, context);
            }
        }

        private sealed class ArgparseNamespaceFactory : ICallable
        {
            public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            {
                context.CheckExecutionBudget(span);
                var members = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (var argument in arguments)
                {
                    if (argument.Name is null)
                    {
                        throw new LythonRuntimeException("TypeError", "argparse.Namespace(...) accepts keyword arguments only.", span);
                    }

                    members[argument.Name] = argument.Value;
                }

                return new ArgparseNamespaceObject(members);
            }
        }
    }

    internal sealed class ArgparseParserOptions
    {
        public static readonly string[] SupportedConstructorParameters =
        [
            "prog",
            "usage",
            "description",
            "epilog",
            "formatter_class",
            "add_help",
            "allow_abbrev",
            "exit_on_error",
        ];

        public ArgparseParserOptions(string prog)
        {
            Prog = prog;
        }

        public string Prog { get; set; }

        public string? Usage { get; set; }

        public string? Description { get; set; }

        public string? Epilog { get; set; }

        public object? FormatterClass { get; set; }

        public bool AddHelp { get; set; } = true;

        public bool AllowAbbrev { get; set; } = true;

        public bool ExitOnError { get; set; } = true;
    }

    internal sealed partial class ArgumentParserObject
    {
        private readonly List<ArgumentSpec> _arguments = [];
        private readonly Dictionary<string, ArgumentSpec> _optionalArgumentsByName = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ArgumentSpec> _argumentsByDestination = new(StringComparer.Ordinal);
        private readonly List<ArgparseMutuallyExclusiveGroupObject> _groups = [];
        private readonly Dictionary<string, object> _defaults = new(StringComparer.Ordinal);
        private readonly ArgparseParserOptions _options;
        private int _nextGroupId;

        public ArgumentParserObject(ArgparseParserOptions options)
        {
            _options = options;
            if (_options.AddHelp)
            {
                RegisterArgument(new ArgumentSpec(
                    ["-h", "--help"],
                    "help",
                    ArgumentAction.Help,
                    Required: false,
                    DefaultValue: ArgparseSuppressValue.Instance,
                    Choices: null,
                    Converter: null,
                    IsPositional: false,
                    Nargs: ArgumentNargs.Default,
                    GroupId: null,
                    ConstValue: PyNone.Instance,
                    HelpText: "show this help message and exit",
                    Metavar: null,
                    VersionText: null,
                    SuppressHelp: false));
            }
        }

        public PyString? Description => _options.Description is null ? null : PyString.FromString(_options.Description);

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "add_argument" => new CustomMethodCallable("argparse.ArgumentParser.add_argument", AddArgument),
                "add_mutually_exclusive_group" => new CustomMethodCallable("argparse.ArgumentParser.add_mutually_exclusive_group", AddMutuallyExclusiveGroup),
                "parse_args" => new CustomMethodCallable("argparse.ArgumentParser.parse_args", ParseArgs),
                "parse_known_args" => new CustomMethodCallable("argparse.ArgumentParser.parse_known_args", ParseKnownArgs),
                "format_usage" => new CustomMethodCallable("argparse.ArgumentParser.format_usage", FormatUsage),
                "format_help" => new CustomMethodCallable("argparse.ArgumentParser.format_help", FormatHelp),
                "print_usage" => new CustomMethodCallable("argparse.ArgumentParser.print_usage", PrintUsage),
                "print_help" => new CustomMethodCallable("argparse.ArgumentParser.print_help", PrintHelp),
                "error" => new CustomMethodCallable("argparse.ArgumentParser.error", Error),
                "exit" => new CustomMethodCallable("argparse.ArgumentParser.exit", Exit),
                "set_defaults" => new CustomMethodCallable("argparse.ArgumentParser.set_defaults", SetDefaults),
                "get_default" => new CustomMethodCallable("argparse.ArgumentParser.get_default", GetDefault),
                "add_subparsers" => new CustomMethodCallable("argparse.ArgumentParser.add_subparsers", AddSubparsers),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

    }

    internal sealed class ArgparseNamespaceObject
        : IPyDynamicAttributes, IPyRenderableValue
    {
        private readonly Dictionary<string, object> _members;

        public ArgparseNamespaceObject(Dictionary<string, object> members)
        {
            _members = members;
        }

        public IReadOnlyDictionary<string, object> Members => _members;

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value) => _members.TryGetValue(name, out value);

        public bool TrySetMember(string name, object value)
        {
            _members[name] = value;
            return true;
        }

        public void ReplaceMembers(Dictionary<string, object> members)
        {
            _members.Clear();
            foreach (var pair in members)
            {
                _members[pair.Key] = pair.Value;
            }
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            var parts = _members
                .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Key + "=" + PyRendering.ToReprPyString(pair.Value, context).AsString());
            return PyString.FromString("Namespace(" + string.Join(", ", parts) + ")");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    internal sealed class ArgparseMutuallyExclusiveGroupObject
    {
        private readonly ArgumentParserObject _parser;

        public ArgparseMutuallyExclusiveGroupObject(ArgumentParserObject parser, int id, bool required)
        {
            _parser = parser;
            Id = id;
            Required = required;
        }

        public int Id { get; }

        public bool Required { get; }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "add_argument" => new CustomMethodCallable("argparse._MutuallyExclusiveGroup.add_argument", AddArgument),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private object AddArgument(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _parser.AddArgumentToGroup(arguments, span, Id, context);
            return PyNone.Instance;
        }
    }

    private enum ArgumentAction
    {
        Store,
        StoreTrue,
        StoreFalse,
        Append,
        StoreConst,
        Count,
        Help,
        Version,
    }

    private enum ArgumentNargsKind
    {
        Default,
        Optional,
        ZeroOrMore,
        OneOrMore,
        Fixed,
    }

    private readonly record struct ArgumentNargs(ArgumentNargsKind Kind, int Count)
    {
        public static readonly ArgumentNargs Default = new(ArgumentNargsKind.Default, 1);
        public static readonly ArgumentNargs Optional = new(ArgumentNargsKind.Optional, 0);
        public static readonly ArgumentNargs ZeroOrMore = new(ArgumentNargsKind.ZeroOrMore, 0);
        public static readonly ArgumentNargs OneOrMore = new(ArgumentNargsKind.OneOrMore, 1);

        public static ArgumentNargs Fixed(int count) => new(ArgumentNargsKind.Fixed, count);
    }

    private sealed record ArgumentSpec(
        IReadOnlyList<string> OptionNames,
        string Destination,
        ArgumentAction Action,
        bool Required,
        object DefaultValue,
        IReadOnlyList<object>? Choices,
        object? Converter,
        bool IsPositional,
        ArgumentNargs Nargs,
        int? GroupId,
        object ConstValue,
        string? HelpText,
        string? Metavar,
        string? VersionText,
        bool SuppressHelp)
    {
        public string DisplayMetavar => Metavar ?? Destination.ToUpperInvariant();
    }

    private sealed record ParseResult(ArgparseNamespaceObject Namespace, List<string> Unknown);

    private sealed class ArgparseFormatterClass(string name) : IPyRenderableValue
    {
        public string Name { get; } = name;

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<class 'argparse." + Name + "'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class ArgparseSuppressValue : IPyRenderableValue
    {
        public static readonly ArgparseSuppressValue Instance = new();

        private ArgparseSuppressValue()
        {
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("==SUPPRESS==");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class ArgparseUnspecifiedValue
    {
        public static readonly ArgparseUnspecifiedValue Instance = new();

        private ArgparseUnspecifiedValue()
        {
        }
    }

    private sealed class ArgparseFileTypeObject(string mode, string? encoding, string? errors) : ICallable, IPyRenderableValue
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length != 1 || arguments[0].Name is not null || !PyStringOps.TryAsString(arguments[0].Value, out var filename))
            {
                throw new LythonRuntimeException("TypeError", "argparse.FileType callable expects one filename argument.", span);
            }

            var values = new List<object> { filename, PyString.FromString(mode) };
            if (encoding is not null || errors is not null)
            {
                values.Add(PyNone.Instance);
                values.Add(encoding is null ? PyNone.Instance : PyString.FromString(encoding));
                values.Add(errors is null ? PyNone.Instance : PyString.FromString(errors));
                values.Add(PyString.Empty);
            }

            return Open(values.ToArray(), span, context);
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("FileType('" + mode + "')");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class CustomMethodCallable : ICallable
    {
        private readonly string _name;
        private readonly Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, object> _implementation;

        public CustomMethodCallable(string name, Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, object> implementation)
        {
            _name = name;
            _implementation = implementation;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            return _implementation(arguments, span, context);
        }
    }

}

using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static PyString RequireArgparseStringValue(object value, string name, string owner, LythonSourceSpan? span)
    {
        if (!PyStringOps.TryAsString(value, out var text))
        {
            throw new LythonRuntimeException("TypeError", $"{owner}(..., {name}=...) expects a string.", span);
        }

        return text;
    }

    private static PyString? OptionalArgparseString(object value, string name, string owner, LythonSourceSpan span)
    {
        if (ReferenceEquals(value, PyNone.Instance))
        {
            return null;
        }

        return RequireArgparseStringValue(value, name, owner, span);
    }

    private sealed class ArgparseModule : PyModule
    {
        public static readonly ArgparseModule Instance = new();
        internal static readonly PyString Suppress = PyString.FromString("==SUPPRESS==");
        internal static readonly PyBuiltinRuntimeType ParserType = new("argparse.ArgumentParser", CreateParser,
            null, (args, span, context) => CreateParserCoreAsync(args, span, context, true));
        internal static readonly PyBuiltinRuntimeType NamespaceType = new("argparse.Namespace", CreateNamespace);

        internal static readonly IReadOnlyDictionary<string, ArgparseFormatterClass> FormatterClasses =
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
                "ArgumentParser" => ParserType,
                "Namespace" => NamespaceType,
                "FileType" => BuiltinCallable.Create(LythonKnownCallableSignatures.ArgparseFileType, CreateFileType),
                "ArgumentError" => new ExceptionTypeValue(ModuleException("argparse", "ArgumentError")),
                "ArgumentTypeError" => new ExceptionTypeValue(ModuleException("argparse", "ArgumentTypeError")),
                "SUPPRESS" => ArgparseModule.Suppress,
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
            => CreateParserCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();

        private static async ValueTask<object> CreateParserCoreAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            using var storage = context.MemoryGovernor.ReserveTemporary(2048, span);
            var options = new ArgparseParserOptions(DefaultProgramName(context, span));

            static PyString DefaultProgramName(ExecutionContext context, LythonSourceSpan span)
            {
                if (context.SourcePath is not { } path) return PyString.FromString("lython");
                using var scratch = context.MemoryGovernor.ReserveTemporary(32L + 2L * path.Length, span);
                var slash = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
                return PyString.FromString(path[(slash + 1)..], context.MemoryGovernor, span);
            }
            var positionalIndex = 0;
            var assigned = new HashSet<string>(StringComparer.Ordinal);

            foreach (var argument in arguments)
            {
                if (argument.IsPositional)
                {
                    if (positionalIndex >= ArgparseParserOptions.SupportedConstructorParameters.Length)
                    {
                        throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser(...) received too many positional arguments.", span);
                    }

                    AssignParserOption(options, ArgparseParserOptions.SupportedConstructorParameters[positionalIndex++], argument.Value, assigned, span);
                    continue;
                }

                if (argument.KeywordName is "fromfile_prefix_chars" or "parents" or "conflict_handler" or "prefix_chars" or "argument_default")
                {
                    throw new LythonRuntimeException(
                        "NotImplementedError",
                        $"argparse.ArgumentParser(..., {argument.KeywordName}=...) is not supported by Lython.",
                        span);
                }

                if (!ArgparseParserOptions.SupportedConstructorParameters.Contains(argument.KeywordName, StringComparer.Ordinal))
                {
                    throw new LythonRuntimeException("TypeError", $"argparse.ArgumentParser(...) got an unexpected keyword argument '{argument.KeywordName}'.", span);
                }

                AssignParserOption(options, argument.KeywordName, argument.Value, assigned, span);
            }

            var addHelp = asynchronous ? await IsTruthyAsync(options.AddHelp, context, span).ConfigureAwait(false)
                : IsTruthy(options.AddHelp, context, span);
            var parser = new ArgumentParserObject(options, addHelp, context, span);
            storage.Commit(2048);
            return parser;
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
                    options.FormatterClass = value;
                    break;
                case "add_help":
                    options.AddHelp = value;
                    break;
                case "allow_abbrev":
                    options.AllowAbbrev = value;
                    break;
                case "exit_on_error":
                    options.ExitOnError = value;
                    break;
            }
        }

        private static object CreateFileType(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            var modeText = arguments.Length >= 1 && !ReferenceEquals(arguments[0], PyNone.Instance)
                ? RequireArgparseStringValue(arguments[0], "mode", "argparse.FileType", span).AsString()
                : "r";
            if (modeText.Contains('b'))
            {
                throw new LythonRuntimeException("ValueError", "argparse.FileType only supports host-mediated text modes.", span);
            }

            var operation = ParseTextOpenMode(PyString.FromString(modeText), "argparse.FileType", span);
            if (arguments.Length >= 2)
            {
                ValidateTextBuffering(arguments[1], "argparse.FileType", span);
            }

            var encodingMode = arguments.Length >= 3
                ? ParseTextEncoding(arguments[2], "argparse.FileType", span)
                : TextEncodingMode.Utf8;
            var errorsMode = arguments.Length >= 4
                ? ParseTextErrors(arguments[3], "argparse.FileType", span)
                : TextErrorMode.Strict;
            return new ArgparseFileTypeObject(operation, encodingMode, errorsMode);
        }

        private static object CreateNamespace(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            foreach (var argument in arguments)
            {
                context.CheckExecution(span);
                if (argument.IsPositional) throw RuntimeErrors.Type("argparse.Namespace(...) accepts keyword arguments only.", span);
                if (argument.KeywordName == "__dict__" && argument.Value is not PyDict)
                    throw RuntimeErrors.Type("__dict__ must be set to a dictionary", span);
            }
            var result = new ArgparseNamespaceObject(context.MemoryGovernor, span);
            foreach (var argument in arguments) result.TrySetMember(argument.KeywordName, argument.Value);
            return result;
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

        public ArgparseParserOptions(PyString prog)
        {
            Prog = prog;
        }

        public PyString Prog { get; set; }

        public PyString? Usage { get; set; }

        public PyString? Description { get; set; }

        public PyString? Epilog { get; set; }

        public object FormatterClass { get; set; } = ArgparseModule.FormatterClasses["HelpFormatter"];

        public object AddHelp { get; set; } = true;

        public object AllowAbbrev { get; set; } = true;

        public object ExitOnError { get; set; } = true;
    }

    internal sealed partial class ArgumentParserObject
    {
        // One spec record plus its list slot and option lookup entries.
        private const long ArgumentSpecInfrastructureBytes = 256;
        // One group record plus its list slot.
        private const long ArgumentGroupBytes = 128;
        // One defaults-table slot; keys and values stay caller-owned.
        private const long DefaultsSlotBytes = 64;
        private readonly List<ArgumentSpec> _arguments = [];
        private readonly Dictionary<string, ArgumentSpec> _optionalArgumentsByName = new(StringComparer.Ordinal);
        private readonly List<ArgparseMutuallyExclusiveGroupObject> _groups = [];
        private readonly Dictionary<string, object> _defaults = new(StringComparer.Ordinal);
        private readonly ArgparseParserOptions _options;
        private int _nextGroupId;

        public ArgumentParserObject(ArgparseParserOptions options, bool addHelp, ExecutionContext context, LythonSourceSpan span)
        {
            _options = options;
            if (addHelp)
            {
                EnsureFormatter(span);
                RegisterArgument(new ArgumentSpec(
                    ["-h", "--help"],
                    "help",
                    ArgumentAction.Help,
                    Required: false,
                    DefaultValue: ArgparseModule.Suppress,
                    Choices: null,
                    Converter: null,
                    IsPositional: false,
                    Nargs: ArgumentNargs.Default,
                    GroupId: null,
                    ConstValue: PyNone.Instance,
                    HelpText: PyString.FromString("show this help message and exit"),
                    Metavar: null,
                    VersionText: null));
            }
        }


        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "add_argument" => new CustomMethodCallable(AddArgument),
                "add_mutually_exclusive_group" => new CustomMethodCallable(AddMutuallyExclusiveGroup),
                "parse_args" => new CustomMethodCallable(ParseArgs, ParseArgsAsync),
                "parse_known_args" => new CustomMethodCallable(ParseKnownArgs, ParseKnownArgsAsync),
                "format_usage" => new CustomMethodCallable(FormatUsage, FormatUsageAsync),
                "format_help" => new CustomMethodCallable(FormatHelp, FormatHelpAsync),
                "print_usage" => new CustomMethodCallable(PrintUsage, PrintUsageAsync),
                "print_help" => new CustomMethodCallable(PrintHelp, PrintHelpAsync),
                "error" => new CustomMethodCallable(Error, ErrorAsync),
                "exit" => new CustomMethodCallable(Exit, ExitAsync),
                "set_defaults" => new CustomMethodCallable(SetDefaults),
                "get_default" => new CustomMethodCallable(GetDefault),
                "add_subparsers" => new CustomMethodCallable(AddSubparsers),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

    }

    internal sealed class ArgparseNamespaceObject : IPyMutableDynamicAttributes, IPyDeletableDynamicAttributes, IPyRenderableValue, IPyGovernedValue
    {
        private PyDict _members;
        public MemoryGovernor OwnerMemoryGovernor { get; }
        public LythonSourceSpan? AllocationSpan { get; }
        public ArgparseNamespaceObject(MemoryGovernor governor, LythonSourceSpan span)
        {
            using var shell = governor.ReserveTemporary(64, span);
            OwnerMemoryGovernor = governor; AllocationSpan = span;
            _members = new PyDict(governor, span);
            shell.Commit(64);
        }
        public PyDict Members => _members;
        public bool HasMember(string name) => TryGetMember(name, out _);
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name == "__dict__") { value = _members; return true; }
            if (name == "__class__") { value = ArgparseModule.NamespaceType; return true; }
            using var key = OwnerMemoryGovernor.ReserveTemporary(PyString.EstimateApproximateBytes(Encoding.UTF8.GetByteCount(name)), AllocationSpan);
            return _members.TryGetValue(PyString.FromString(name), out value);
        }
        public bool TrySetMember(string name, object value)
        {
            if (name == "__dict__")
            {
                _members = value as PyDict ?? throw RuntimeErrors.Type("__dict__ must be set to a dictionary", AllocationSpan);
                return true;
            }
            _members.SetItem(PyString.FromString(name, OwnerMemoryGovernor, AllocationSpan), value);
            return true;
        }
        public bool TryDeleteMember(string name, LythonSourceSpan? span)
        {
            if (name == "__dict__") { _members = new PyDict(OwnerMemoryGovernor, span); return true; }
            using var key = OwnerMemoryGovernor.ReserveTemporary(PyString.EstimateApproximateBytes(Encoding.UTF8.GetByteCount(name)), span);
            return _members.Remove(PyString.FromString(name));
        }
        public override bool Equals(object? other) => other is ArgparseNamespaceObject fellow && PyEquality.AreEqual(_members, fellow._members);
        public override int GetHashCode() => throw new PyUnhashableException();
        public PyString RenderPython(PyRenderingContext context)
        {
            var span = PyStructuralGuard.AmbientSpan;
            using var snapshot = context.Context.MemoryGovernor.ReserveTemporary(32L + 32L * _members.Count, span);
            var items = _members.Items.ToArray();
            using var ambient = PyStructuralGuard.PushAmbient(context.Context, span);
            var builder = new GovernedByteBuilder(context.Context.MemoryGovernor, span);
            try
            {
                builder.AppendAscii("Namespace(");
                var first = true;
                foreach (var pair in items)
                {
                    context.Context.CheckExecution(span);
                    var name = pair.Key as PyString ?? throw RuntimeErrors.Type("namespace attribute names must be strings", span);
                    if (!PyStringOps.IsIdentifier(name)) continue;
                    if (!first) builder.AppendAscii(", "); first = false;
                    builder.Append(name); builder.Append((byte)'=');
                    builder.Append(PyRendering.ToReprPyString(pair.Value, context));
                }
                var extra = false;
                foreach (var pair in items)
                {
                    context.Context.CheckExecution(span);
                    var name = (PyString)pair.Key;
                    if (PyStringOps.IsIdentifier(name)) continue;
                    if (!extra) { if (!first) builder.AppendAscii(", "); builder.AppendAscii("**{"); extra = true; }
                    else builder.AppendAscii(", ");
                    builder.Append(PyRendering.ToReprPyString(name, context)); builder.AppendAscii(": ");
                    builder.Append(PyRendering.ToReprPyString(pair.Value, context));
                }
                if (extra) builder.Append((byte)'}');
                builder.Append((byte)')');
                return builder.ToPyStringAndRelease();
            }
            finally { builder.Release(); }
        }
        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    internal sealed class ArgparseMutuallyExclusiveGroupObject
    {
        private readonly ArgumentParserObject _parser;

        public ArgparseMutuallyExclusiveGroupObject(ArgumentParserObject parser, int id, object required)
        {
            _parser = parser;
            Id = id;
            Required = required;
        }

        public int Id { get; }

        public object Required { get; }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "add_argument" => new CustomMethodCallable(AddArgument),
                _ => MissingMemberValue.Instance
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private object AddArgument(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            return _parser.AddArgumentToGroup(arguments, span, Id, context);
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

    // One retained specification is also the returned action metadata. Layout
    // stays private; supported edits update exactly the state parsing consumes.
    private sealed class ArgumentSpec(IReadOnlyList<string> OptionNames, string Destination,
        ArgumentAction Action, object Required, object DefaultValue, object? Choices,
        ICallable? Converter, bool IsPositional, ArgumentNargs Nargs, int? GroupId,
        object ConstValue, PyString? HelpText, PyString? Metavar, PyString? VersionText,
        MemoryGovernor? governor = null, LythonSourceSpan? span = null) : IPyMutableDynamicAttributes
    {
        public IReadOnlyList<string> OptionNames { get; } = OptionNames;
        public string Destination { get; private set; } = Destination;
        public ArgumentAction Action { get; } = Action;
        public object Required { get; private set; } = Required;
        public object DefaultValue { get; set; } = DefaultValue;
        public object? Choices { get; private set; } = Choices;
        public ICallable? Converter { get; private set; } = Converter;
        public bool IsPositional { get; } = IsPositional;
        public ArgumentNargs Nargs { get; } = Nargs;
        public int? GroupId { get; } = GroupId;
        public object ConstValue { get; private set; } = ConstValue;
        public PyString? HelpText { get; private set; } = HelpText;
        public PyString? Metavar { get; private set; } = Metavar;
        public PyString? VersionText { get; private set; } = VersionText;
        public bool SuppressHelp => ReferenceEquals(HelpText, ArgparseModule.Suppress);
        private PyString? _destinationLabel;
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "dest" => _destinationLabel ??= governor is null ? PyString.FromString(Destination) : PyString.FromString(Destination, governor, span),
                "default" => DefaultValue,
                "required" => Required,
                "type" => Converter ?? (object)PyNone.Instance,
                "choices" => Choices ?? PyNone.Instance,
                "const" => ConstValue,
                "help" => HelpText ?? (object)PyNone.Instance,
                "metavar" => Metavar ?? (object)PyNone.Instance,
                "version" when Action == ArgumentAction.Version => VersionText ?? (object)PyNone.Instance,
                "nargs" => Nargs.Kind switch { ArgumentNargsKind.Default => PyNone.Instance, ArgumentNargsKind.Optional => PyString.FromString("?"), ArgumentNargsKind.ZeroOrMore => PyString.FromString("*"), ArgumentNargsKind.OneOrMore => PyString.FromString("+"), _ => new BigInteger(Nargs.Count) },
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
        public bool TrySetMember(string name, object value)
        {
            switch (name)
            {
                case "dest":
                    var label = RequireArgparseStringValue(value, "dest", "argparse action", span);
                    var bytes = 32L + 2L * label.GetUtf16CodeUnitCount();
                    using (var storage = governor?.ReserveTemporary(bytes, span))
                    { Destination = label.AsString(); storage?.Commit(bytes); _destinationLabel = label; }
                    return true;
                case "default": DefaultValue = value; return true;
                case "required": Required = value; return true;
                case "choices": Choices = value is PyNone ? null : value; return true;
                case "const": ConstValue = value; return true;
                case "type":
                    if (value is not PyNone && !IsCallable(value)) throw RuntimeErrors.Value("action type must be callable or None", span);
                    Converter = value as ICallable; return true;
                case "help": HelpText = value is PyNone ? null : RequireArgparseStringValue(value, name, "argparse action", span); return true;
                case "metavar": Metavar = value is PyNone ? null : RequireArgparseStringValue(value, name, "argparse action", span); return true;
                case "version" when Action == ArgumentAction.Version: VersionText = value is PyNone ? null : RequireArgparseStringValue(value, name, "argparse action", span); return true;
                case "nargs" or "option_strings": throw new LythonRuntimeException("NotImplementedError", "Changing argparse action layout is unsupported.", span);
                default: return false;
            }
        }
    }

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

    private sealed class ArgparseUnspecifiedValue
    {
        public static readonly ArgparseUnspecifiedValue Instance = new();

        private ArgparseUnspecifiedValue()
        {
        }
    }

    private sealed class ArgparseFileTypeObject(
        TextFileOperation operation,
        TextEncodingMode encoding,
        TextErrorMode errors) : ICallable, IPyRenderableValue
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => OpenAsync(arguments, span, context, false).GetAwaiter().GetResult();
        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => OpenAsync(arguments, span, context, true);
        private async ValueTask<object> OpenAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            if (arguments.Length != 1 || arguments[0].IsKeyword || arguments[0].Value is not PyString filename)
                throw RuntimeErrors.Type("argparse.FileType callable expects one filename argument.", span);
            using var pathStorage = context.MemoryGovernor.ReserveTemporary(32L + 2L * filename.GetUtf16CodeUnitCount(), span);
            var path = filename.AsString();
            var handle = operation switch
            {
                TextFileOperation.Read => asynchronous ? await ExecutionContext.TextFileHandle.ForReadAsync(path, context, encoding, errors, TextNewlineMode.TranslateUniversal).ConfigureAwait(false)
                    : ExecutionContext.TextFileHandle.ForRead(path, context, encoding, errors, TextNewlineMode.TranslateUniversal),
                TextFileOperation.Write => asynchronous ? await ExecutionContext.TextFileHandle.ForWriteAsync(path, context, encoding, errors, TextNewlineMode.TranslateUniversal).ConfigureAwait(false)
                    : ExecutionContext.TextFileHandle.ForWrite(path, context, encoding, errors, TextNewlineMode.TranslateUniversal),
                TextFileOperation.Append => asynchronous ? await ExecutionContext.TextFileHandle.ForAppendAsync(path, context, encoding, errors, TextNewlineMode.TranslateUniversal).ConfigureAwait(false)
                    : ExecutionContext.TextFileHandle.ForAppend(path, context, encoding, errors, TextNewlineMode.TranslateUniversal),
                _ => throw new InvalidOperationException("Unknown text file operation."),
            };
            pathStorage.Commit(32L + 2L * filename.GetUtf16CodeUnitCount());
            return handle;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("FileType('" + TextOpenModeName(operation) + "')");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    private sealed class CustomMethodCallable : ICallable
    {
        private readonly Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, object> _implementation;
        private readonly Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, ValueTask<object>>? _asyncImplementation;

        public CustomMethodCallable(Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, object> implementation,
            Func<CallArgumentValue[], LythonSourceSpan, ExecutionContext, ValueTask<object>>? asyncImplementation = null)
        { _implementation = implementation; _asyncImplementation = asyncImplementation; }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        { context.CheckExecution(span); return _implementation(arguments, span, context); }

        public ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        { context.CheckExecution(span); return _asyncImplementation is null ? new(_implementation(arguments, span, context)) : _asyncImplementation(arguments, span, context); }
    }
}

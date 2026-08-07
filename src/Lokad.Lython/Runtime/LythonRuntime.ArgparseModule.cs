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
                    options.Prog = OptionalString(value, "prog", "argparse.ArgumentParser", span) ?? options.Prog;
                    break;
                case "usage":
                    options.Usage = OptionalString(value, "usage", "argparse.ArgumentParser", span);
                    break;
                case "description":
                    options.Description = OptionalString(value, "description", "argparse.ArgumentParser", span);
                    break;
                case "epilog":
                    options.Epilog = OptionalString(value, "epilog", "argparse.ArgumentParser", span);
                    break;
                case "formatter_class":
                    options.FormatterClass = ReferenceEquals(value, PyNone.Instance) ? null : value;
                    break;
                case "add_help":
                    options.AddHelp = RequireBool(value, "add_help", "argparse.ArgumentParser", span);
                    break;
                case "allow_abbrev":
                    options.AllowAbbrev = RequireBool(value, "allow_abbrev", "argparse.ArgumentParser", span);
                    break;
                case "exit_on_error":
                    options.ExitOnError = RequireBool(value, "exit_on_error", "argparse.ArgumentParser", span);
                    break;
            }
        }

        private static object CreateFileType(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            var mode = arguments.Length >= 1 && !ReferenceEquals(arguments[0], PyNone.Instance)
                ? RequireStringValue(arguments[0], "mode", "argparse.FileType", span).AsString()
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

        private static PyString RequireStringValue(object value, string name, string owner, LythonSourceSpan span)
        {
            if (!PyStringOps.TryAsString(value, out var text))
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(..., {name}=...) expects a string.", span);
            }

            return text;
        }

        private static string? OptionalString(object value, string name, string owner, LythonSourceSpan span)
        {
            if (ReferenceEquals(value, PyNone.Instance))
            {
                return null;
            }

            return RequireStringValue(value, name, owner, span).AsString();
        }

        private static bool RequireBool(object value, string name, string owner, LythonSourceSpan span)
        {
            if (value is bool boolean)
            {
                return boolean;
            }

            throw new LythonRuntimeException("TypeError", $"{owner}(..., {name}=...) expects a bool.", span);
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

    internal sealed class ArgumentParserObject
    {
        private readonly List<ArgumentSpec> _arguments = [];
        private readonly List<ArgparseMutuallyExclusiveGroupObject> _groups = [];
        private readonly Dictionary<string, object> _defaults = new(StringComparer.Ordinal);
        private readonly ArgparseParserOptions _options;
        private int _nextGroupId;

        public ArgumentParserObject(ArgparseParserOptions options)
        {
            _options = options;
            if (_options.AddHelp)
            {
                _arguments.Add(new ArgumentSpec(
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

        private object AddArgument(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _arguments.Add(CreateArgumentSpec(arguments, span, groupId: null, context));
            return PyNone.Instance;
        }

        private object AddMutuallyExclusiveGroup(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            ValidateSupportedKeywords(arguments, ["required"], "argparse.ArgumentParser.add_mutually_exclusive_group", span);

            if (arguments.Length > 1)
            {
                throw new LythonRuntimeException(
                    "TypeError",
                    "argparse.ArgumentParser.add_mutually_exclusive_group([required]) expects zero or one argument.",
                    span);
            }

            var required = arguments.Length == 1 && IsTruthy(arguments[0].Value);
            var group = new ArgparseMutuallyExclusiveGroupObject(this, _nextGroupId++, required);
            _groups.Add(group);
            return group;
        }

        internal void AddArgumentToGroup(CallArgumentValue[] arguments, LythonSourceSpan span, int groupId, ExecutionContext context)
        {
            _arguments.Add(CreateArgumentSpec(arguments, span, groupId, context));
        }

        private object ParseArgs(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var result = ParseArguments(arguments, "argparse.ArgumentParser.parse_args", collectUnknown: false, span, context);
            if (result.Unknown.Count != 0)
            {
                throw CreateParseFailure($"unrecognized arguments: {JoinUnknownArguments(result.Unknown)}", span);
            }

            return result.Namespace;
        }

        private object ParseKnownArgs(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var result = ParseArguments(arguments, "argparse.ArgumentParser.parse_known_args", collectUnknown: true, span, context);
            return new PyTuple(
                [
                    result.Namespace,
                    new PyList(result.Unknown.Select(static item => (object)PyString.FromString(item)), context.MemoryGovernor, span)
                ],
                context.MemoryGovernor,
                span);
        }

        private ParseResult ParseArguments(
            CallArgumentValue[] arguments,
            string methodName,
            bool collectUnknown,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var (argv, namespaceObject) = ResolveParseInvocation(arguments, methodName, span, context);
            var values = namespaceObject is null
                ? new Dictionary<string, object>(StringComparer.Ordinal)
                : new Dictionary<string, object>(namespaceObject.Members, StringComparer.Ordinal);
            var seenSpecs = new HashSet<ArgumentSpec>();
            var unknown = new List<string>();

            ApplyDefaults(values, context, span);

            var positionalSpecs = new List<ArgumentSpec>(_arguments.Count);
            foreach (var argument in _arguments)
            {
                if (argument.IsPositional)
                {
                    positionalSpecs.Add(argument);
                }
            }

            var positionalIndex = 0;

            for (var index = 0; index < argv.Count; index++)
            {
                var token = argv[index];
                if (token == "--")
                {
                    index++;
                    while (index < argv.Count)
                    {
                        ConsumePositional(argv, ref index, positionalSpecs, ref positionalIndex, seenSpecs, values, unknown, collectUnknown, span, context);
                        index++;
                    }

                    break;
                }

                if (TryExpandShortFlagCluster(token, out var clusterSpecs))
                {
                    foreach (var clusterSpec in clusterSpecs)
                    {
                        ApplyNoValueOptional(clusterSpec, token, seenSpecs, values, span, context);
                    }

                    continue;
                }

                var optionalSpec = FindOptionalArgument(token, out var inlineValue, out var optionError);
                if (optionError is not null)
                {
                    throw CreateParseFailure(optionError, span);
                }

                if (optionalSpec is not null)
                {
                    ApplyOptional(optionalSpec, token, inlineValue, argv, ref index, seenSpecs, values, span, context);
                    continue;
                }

                if (LooksLikeOptionalToken(token) &&
                    (!LooksLikeNegativeNumber(token) || HasNegativeNumberOptions()))
                {
                    if (collectUnknown)
                    {
                        unknown.Add(token);
                        continue;
                    }

                    throw CreateParseFailure($"unrecognized arguments: {token}", span);
                }

                ConsumePositional(argv, ref index, positionalSpecs, ref positionalIndex, seenSpecs, values, unknown, collectUnknown, span, context);
            }

            ValidateRequiredArguments(seenSpecs, values, span);
            ValidateMutuallyExclusiveGroups(seenSpecs, span);
            var resultNamespace = namespaceObject ?? new ArgparseNamespaceObject(new Dictionary<string, object>(StringComparer.Ordinal));
            resultNamespace.ReplaceMembers(values);
            return new ParseResult(resultNamespace, unknown);
        }

        private (List<string> Argv, ArgparseNamespaceObject? Namespace) ResolveParseInvocation(
            CallArgumentValue[] arguments,
            string methodName,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            object? argsValue = ArgparseUnspecifiedValue.Instance;
            object? namespaceValue = ArgparseUnspecifiedValue.Instance;
            var positionalIndex = 0;
            var assignedArgs = false;
            var assignedNamespace = false;

            foreach (var argument in arguments)
            {
                if (argument.Name is null)
                {
                    if (positionalIndex == 0)
                    {
                        if (assignedArgs)
                        {
                            throw new LythonRuntimeException("TypeError", $"{methodName}(...) got multiple values for argument 'args'.", span);
                        }

                        argsValue = argument.Value;
                        assignedArgs = true;
                    }
                    else if (positionalIndex == 1)
                    {
                        if (assignedNamespace)
                        {
                            throw new LythonRuntimeException("TypeError", $"{methodName}(...) got multiple values for argument 'namespace'.", span);
                        }

                        namespaceValue = argument.Value;
                        assignedNamespace = true;
                    }
                    else
                    {
                        throw new LythonRuntimeException("TypeError", $"{methodName}([args][, namespace]) expects zero to two arguments.", span);
                    }

                    positionalIndex++;
                    continue;
                }

                switch (argument.Name)
                {
                    case "args":
                        if (assignedArgs)
                        {
                            throw new LythonRuntimeException("TypeError", $"{methodName}(...) got multiple values for argument 'args'.", span);
                        }

                        argsValue = argument.Value;
                        assignedArgs = true;
                        break;
                    case "namespace":
                        if (assignedNamespace)
                        {
                            throw new LythonRuntimeException("TypeError", $"{methodName}(...) got multiple values for argument 'namespace'.", span);
                        }

                        namespaceValue = argument.Value;
                        assignedNamespace = true;
                        break;
                    default:
                        throw new LythonRuntimeException("TypeError", $"{methodName}(...) got an unexpected keyword argument '{argument.Name}'.", span);
                }
            }

            var argv = ReferenceEquals(argsValue, ArgparseUnspecifiedValue.Instance) || ReferenceEquals(argsValue, PyNone.Instance)
                ? context.State.Args.Select(static item => item.AsString()).ToList()
                : ToStringList(argsValue.RequireNotNull(), $"{methodName}(args) expects an iterable of strings.", span);

            var namespaceObject = ReferenceEquals(namespaceValue, ArgparseUnspecifiedValue.Instance) || ReferenceEquals(namespaceValue, PyNone.Instance)
                ? null
                : namespaceValue as ArgparseNamespaceObject ??
                  throw new LythonRuntimeException("TypeError", $"{methodName}(namespace) expects an argparse.Namespace instance.", span);

            return (argv, namespaceObject);
        }

        private void ApplyDefaults(Dictionary<string, object> values, ExecutionContext context, LythonSourceSpan span)
        {
            foreach (var spec in _arguments)
            {
                var defaultValue = _defaults.TryGetValue(spec.Destination, out var parserDefault)
                    ? parserDefault
                    : spec.DefaultValue;
                if (IsSuppress(defaultValue) || values.ContainsKey(spec.Destination))
                {
                    continue;
                }

                values[spec.Destination] = CloneDefault(defaultValue, context, span);
            }

            foreach (var pair in _defaults)
            {
                if (!values.ContainsKey(pair.Key) && !IsSuppress(pair.Value))
                {
                    values[pair.Key] = CloneDefault(pair.Value, context, span);
                }
            }
        }

        private void ConsumePositional(
            List<string> argv,
            ref int index,
            IReadOnlyList<ArgumentSpec> positionalSpecs,
            ref int positionalIndex,
            HashSet<ArgumentSpec> seenSpecs,
            Dictionary<string, object> values,
            List<string> unknown,
            bool collectUnknown,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            if (positionalIndex >= positionalSpecs.Count)
            {
                if (collectUnknown)
                {
                    unknown.Add(argv[index]);
                    return;
                }

                throw CreateParseFailure($"unrecognized arguments: {argv[index]}", span);
            }

            var spec = positionalSpecs[positionalIndex];
            var tokens = CollectPositionalTokens(spec, argv, ref index, positionalSpecs, positionalIndex, span);
            if (tokens.Count == 0)
            {
                return;
            }

            StoreParsedValue(spec, CreateParsedValue(spec, tokens, spec.Destination, span, context), seenSpecs, values, context, span);
            positionalIndex++;
        }

        private List<string> CollectPositionalTokens(
            ArgumentSpec spec,
            IReadOnlyList<string> argv,
            ref int index,
            IReadOnlyList<ArgumentSpec> positionalSpecs,
            int positionalIndex,
            LythonSourceSpan span)
        {
            var tokens = new List<string>();
            if (spec.Nargs.Kind == ArgumentNargsKind.Fixed)
            {
                for (var i = 0; i < spec.Nargs.Count; i++)
                {
                    if (index >= argv.Count || HasOptionalArgumentNamed(argv[index]))
                    {
                        throw CreateParseFailure($"argument {spec.Destination}: expected {spec.Nargs.Count} arguments", span);
                    }

                    tokens.Add(argv[index]);
                    if (i + 1 < spec.Nargs.Count)
                    {
                        index++;
                    }
                }

                return tokens;
            }

            switch (spec.Nargs.Kind)
            {
                case ArgumentNargsKind.Default:
                case ArgumentNargsKind.Optional:
                    tokens.Add(argv[index]);
                    return tokens;
                case ArgumentNargsKind.ZeroOrMore:
                case ArgumentNargsKind.OneOrMore:
                    var requiredAfter = RequiredPositionalSlotsAfter(positionalSpecs, positionalIndex);
                    while (index < argv.Count &&
                           !HasOptionalArgumentNamed(argv[index]) &&
                           CountRemainingPositionalCandidates(argv, index) > requiredAfter)
                    {
                        tokens.Add(argv[index]);
                        if (index + 1 >= argv.Count || HasOptionalArgumentNamed(argv[index + 1]))
                        {
                            break;
                        }

                        index++;
                    }

                    if (spec.Nargs.Kind == ArgumentNargsKind.OneOrMore && tokens.Count == 0)
                    {
                        throw CreateParseFailure($"the following arguments are required: {spec.Destination}", span);
                    }

                    return tokens;
                default:
                    throw new InvalidOperationException("Unsupported argparse nargs shape.");
            }
        }

        private void ApplyOptional(
            ArgumentSpec spec,
            string optionToken,
            string? inlineValue,
            IReadOnlyList<string> argv,
            ref int index,
            HashSet<ArgumentSpec> seenSpecs,
            Dictionary<string, object> values,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            if (IsNoValueAction(spec.Action))
            {
                if (inlineValue is not null)
                {
                    throw CreateParseFailure($"argument {optionToken}: ignored explicit argument '{inlineValue}'", span);
                }

                ApplyNoValueOptional(spec, optionToken, seenSpecs, values, span, context);
                return;
            }

            var tokens = CollectOptionalTokens(spec, optionToken, inlineValue, argv, ref index, span);
            StoreParsedValue(spec, CreateParsedValue(spec, tokens, optionToken, span, context), seenSpecs, values, context, span);
        }

        private void ApplyNoValueOptional(
            ArgumentSpec spec,
            string optionToken,
            HashSet<ArgumentSpec> seenSpecs,
            Dictionary<string, object> values,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            switch (spec.Action)
            {
                case ArgumentAction.StoreTrue:
                    values[spec.Destination] = true;
                    break;
                case ArgumentAction.StoreFalse:
                    values[spec.Destination] = false;
                    break;
                case ArgumentAction.StoreConst:
                    values[spec.Destination] = CloneDefault(spec.ConstValue, context, span);
                    break;
                case ArgumentAction.Count:
                    values[spec.Destination] = IncrementCount(values.TryGetValue(spec.Destination, out var current) ? current : PyNone.Instance);
                    break;
                case ArgumentAction.Help:
                    WriteToTarget(FormatHelpText(context), context.State.Stdout, span);
                    throw CreateSystemExit(string.Empty, span, status: 0);
                case ArgumentAction.Version:
                    var versionText = spec.VersionText ?? string.Empty;
                    if (!versionText.EndsWith('\n'))
                    {
                        versionText += "\n";
                    }

                    WriteToTarget(PyString.FromString(ApplyFormatSubstitutions(versionText, spec, context)), context.State.Stdout, span);
                    throw CreateSystemExit(string.Empty, span, status: 0);
                default:
                    throw new InvalidOperationException("Unsupported no-value argparse action.");
            }

            seenSpecs.Add(spec);
        }

        private List<string> CollectOptionalTokens(
            ArgumentSpec spec,
            string optionToken,
            string? inlineValue,
            IReadOnlyList<string> argv,
            ref int index,
            LythonSourceSpan span)
        {
            var tokens = new List<string>();
            if (inlineValue is not null)
            {
                tokens.Add(inlineValue);
            }

            if (spec.Nargs.Kind == ArgumentNargsKind.Fixed)
            {
                while (tokens.Count < spec.Nargs.Count)
                {
                    if (index + 1 >= argv.Count || HasOptionalArgumentNamed(argv[index + 1]))
                    {
                        throw CreateParseFailure($"argument {optionToken}: expected {spec.Nargs.Count} arguments", span);
                    }

                    index++;
                    tokens.Add(argv[index]);
                }

                return tokens;
            }

            switch (spec.Nargs.Kind)
            {
                case ArgumentNargsKind.Default:
                    if (tokens.Count == 0)
                    {
                        if (index + 1 >= argv.Count || HasOptionalArgumentNamed(argv[index + 1]))
                        {
                            throw CreateParseFailure($"argument {optionToken}: expected one argument", span);
                        }

                        index++;
                        tokens.Add(argv[index]);
                    }

                    return tokens;
                case ArgumentNargsKind.Optional:
                    if (tokens.Count == 0)
                    {
                        if (index + 1 < argv.Count && !HasOptionalArgumentNamed(argv[index + 1]))
                        {
                            index++;
                            tokens.Add(argv[index]);
                        }
                    }

                    return tokens;
                case ArgumentNargsKind.ZeroOrMore:
                case ArgumentNargsKind.OneOrMore:
                    while (index + 1 < argv.Count && !HasOptionalArgumentNamed(argv[index + 1]))
                    {
                        index++;
                        tokens.Add(argv[index]);
                    }

                    if (spec.Nargs.Kind == ArgumentNargsKind.OneOrMore && tokens.Count == 0)
                    {
                        throw CreateParseFailure($"argument {optionToken}: expected at least one argument", span);
                    }

                    return tokens;
                default:
                    throw new InvalidOperationException("Unsupported argparse nargs shape.");
            }
        }

        private object CreateParsedValue(ArgumentSpec spec, IReadOnlyList<string> tokens, string displayName, LythonSourceSpan span, ExecutionContext context)
        {
            if (spec.Nargs.Kind == ArgumentNargsKind.Optional && tokens.Count == 0)
            {
                return ReferenceEquals(spec.ConstValue, PyNone.Instance)
                    ? CloneDefault(spec.DefaultValue, context, span)
                    : CloneDefault(spec.ConstValue, context, span);
            }

            if (ProducesListValue(spec))
            {
                var values = new PyList([], context.MemoryGovernor, span);
                foreach (var token in tokens)
                {
                    values.Add(ConvertArgumentValue(spec, displayName, token, span, context));
                }

                return values;
            }

            if (tokens.Count == 0)
            {
                return PyNone.Instance;
            }

            return ConvertArgumentValue(spec, displayName, tokens[0], span, context);
        }

        private void StoreParsedValue(
            ArgumentSpec spec,
            object parsed,
            HashSet<ArgumentSpec> seenSpecs,
            Dictionary<string, object> values,
            ExecutionContext context,
            LythonSourceSpan span)
        {
            if (spec.Action == ArgumentAction.Append)
            {
                var list = values.TryGetValue(spec.Destination, out var existing) && existing is PyList existingList
                    ? existingList
                    : new PyList([], context.MemoryGovernor, span);
                list.Add(parsed);
                values[spec.Destination] = list;
            }
            else
            {
                values[spec.Destination] = parsed;
            }

            seenSpecs.Add(spec);
        }

        private object ConvertArgumentValue(ArgumentSpec spec, string optionName, string token, LythonSourceSpan span, ExecutionContext context)
        {
            object converted = PyString.FromString(token);
            if (spec.Converter is ICallable callable)
            {
                try
                {
                    converted = RuntimeValue(callable.Invoke([new CallArgumentValue(null, converted)], span, context));
                }
                catch (LythonRuntimeException ex) when (ex.ExceptionType is "ArgumentTypeError" or "ValueError" or "TypeError")
                {
                    throw CreateParseFailure($"argument {optionName}: {ex.Message}", span);
                }
            }

            if (spec.Choices is not null)
            {
                var matched = false;
                for (var i = 0; i < spec.Choices.Count; i++)
                {
                    if (PyEquality.AreEqual(spec.Choices[i], converted))
                    {
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    throw CreateParseFailure($"argument {optionName}: invalid choice: '{token}'", span);
                }
            }

            return converted;
        }

        private void ValidateRequiredArguments(HashSet<ArgumentSpec> seenSpecs, Dictionary<string, object> values, LythonSourceSpan span)
        {
            var missing = new List<string>();
            foreach (var spec in _arguments)
            {
                if (spec.Action is ArgumentAction.Help or ArgumentAction.Version)
                {
                    continue;
                }

                if (spec.IsPositional)
                {
                    if (spec.Nargs.Kind is ArgumentNargsKind.ZeroOrMore or ArgumentNargsKind.Optional)
                    {
                        continue;
                    }

                    if (!seenSpecs.Contains(spec))
                    {
                        missing.Add(spec.Destination);
                    }

                    continue;
                }

                if (spec.Required && !seenSpecs.Contains(spec))
                {
                    missing.Add(spec.OptionNames.FirstOrDefault() ?? spec.Destination);
                }
            }

            if (missing.Count != 0)
            {
                throw CreateParseFailure($"the following arguments are required: {string.Join(", ", missing)}", span);
            }
        }

        private void ValidateMutuallyExclusiveGroups(HashSet<ArgumentSpec> seenSpecs, LythonSourceSpan span)
        {
            foreach (var group in _groups)
            {
                var present = 0;
                foreach (var spec in seenSpecs)
                {
                    if (spec.GroupId == group.Id)
                    {
                        present++;
                    }
                }

                if (present > 1)
                {
                    throw CreateParseFailure("mutually exclusive arguments must not be used together", span);
                }

                if (group.Required && present == 0)
                {
                    throw CreateParseFailure("one of the mutually exclusive arguments is required", span);
                }
            }
        }

        private static object CloneDefault(object value, ExecutionContext context, LythonSourceSpan span)
        {
            return value switch
            {
                PyList list => new PyList(list, context.MemoryGovernor, span),
                PyDict dict => new PyDict(dict, context.MemoryGovernor, span),
                PySet set => new PySet(set, context.MemoryGovernor, span),
                _ => value
            };
        }

        private static object DefaultForAction(ArgumentAction action, ExecutionContext context, LythonSourceSpan span)
        {
            return action switch
            {
                ArgumentAction.StoreTrue => false,
                ArgumentAction.StoreFalse => true,
                ArgumentAction.Append => new PyList([], context.MemoryGovernor, span),
                ArgumentAction.StoreConst => PyNone.Instance,
                ArgumentAction.Count => PyNone.Instance,
                ArgumentAction.Help or ArgumentAction.Version => ArgparseSuppressValue.Instance,
                _ => PyNone.Instance
            };
        }

        private static ArgumentAction ParseAction(string action, LythonSourceSpan span)
        {
            return action switch
            {
                "store" => ArgumentAction.Store,
                "store_true" => ArgumentAction.StoreTrue,
                "store_false" => ArgumentAction.StoreFalse,
                "append" => ArgumentAction.Append,
                "store_const" => ArgumentAction.StoreConst,
                "count" => ArgumentAction.Count,
                "version" => ArgumentAction.Version,
                _ => throw new LythonRuntimeException(
                    "ValueError",
                    "argparse.ArgumentParser.add_argument(..., action=...) only supports 'store', 'store_true', 'store_false', 'append', 'store_const', 'count', or 'version'.",
                    span),
            };
        }

        private static object? ValidateConverter(object value, LythonSourceSpan span)
        {
            if (ReferenceEquals(value, PyNone.Instance))
            {
                return null;
            }

            if (value is ICallable)
            {
                return value;
            }

            throw new LythonRuntimeException(
                "TypeError",
                "argparse.ArgumentParser.add_argument(..., type=...) expects a callable or None.",
                span);
        }

        private static ArgumentNargs ValidateNargs(object value, ArgumentAction action, LythonSourceSpan span)
        {
            if (IsNoValueAction(action))
            {
                throw new LythonRuntimeException(
                    "TypeError",
                    "argparse.ArgumentParser.add_argument(..., nargs=...) is not supported for no-value actions.",
                    span);
            }

            if (value is BigInteger integer)
            {
                if (integer > BigInteger.Zero && integer <= int.MaxValue)
                {
                    return ArgumentNargs.Fixed((int)integer);
                }

                throw new LythonRuntimeException(
                    "TypeError",
                    "argparse.ArgumentParser.add_argument(..., nargs=...) expects '?', '*', '+', or a positive integer.",
                    span);
            }

            if (!PyStringOps.TryAsString(value, out var text))
            {
                throw new LythonRuntimeException(
                    "TypeError",
                    "argparse.ArgumentParser.add_argument(..., nargs=...) expects '?', '*', '+', or a positive integer.",
                    span);
            }

            var nargs = text.AsString();
            if (nargs is "?" or "*" or "+")
            {
                return nargs switch
                {
                    "?" => ArgumentNargs.Optional,
                    "*" => ArgumentNargs.ZeroOrMore,
                    _ => ArgumentNargs.OneOrMore,
                };
            }

            if (int.TryParse(nargs, NumberStyles.None, CultureInfo.InvariantCulture, out var fixedCount) && fixedCount > 0)
            {
                return ArgumentNargs.Fixed(fixedCount);
            }

            throw new LythonRuntimeException(
                "TypeError",
                "argparse.ArgumentParser.add_argument(..., nargs=...) expects '?', '*', '+', or a positive integer.",
                span);
        }

        private static ArgumentSpec CreateArgumentSpec(CallArgumentValue[] arguments, LythonSourceSpan span, int? groupId, ExecutionContext context)
        {
            if (arguments.Length == 0)
            {
                throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser.add_argument(...) expects at least one argument name.", span);
            }

            var positional = new List<object>(arguments.Length);
            var keyword = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var argument in arguments)
            {
                if (argument.Name is null)
                {
                    positional.Add(argument.Value);
                }
                else
                {
                    keyword[argument.Name] = argument.Value;
                }
            }

            ValidateSupportedKeywords(
                keyword.Keys,
                ["dest", "action", "required", "default", "choices", "type", "nargs", "help", "const", "metavar", "version"],
                "argparse.ArgumentParser.add_argument",
                span);
            if (positional.Count == 0)
            {
                throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser.add_argument(...) expects at least one argument name.", span);
            }

            var optionNames = new string[positional.Count];
            for (var i = 0; i < positional.Count; i++)
            {
                var value = positional[i];
                if (!PyStringOps.TryAsString(value, out var text))
                {
                    throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser.add_argument(...) expects option names to be strings.", span);
                }

                optionNames[i] = text.AsString();
            }

            var dest = keyword.TryGetValue("dest", out var explicitDest)
                ? RequireString("dest", explicitDest, span).AsString()
                : InferDestination(optionNames, span);
            var action = keyword.TryGetValue("action", out var actionValue)
                ? ParseAction(RequireString("action", actionValue, span).AsString(), span)
                : ArgumentAction.Store;
            var isPositional = optionNames[0].Length != 0 && !optionNames[0].StartsWith("-", StringComparison.Ordinal);
            if (isPositional && keyword.ContainsKey("required"))
            {
                throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser.add_argument(..., required=...) is not valid for positional arguments.", span);
            }

            var nargs = keyword.TryGetValue("nargs", out var nargsValue)
                ? ValidateNargs(nargsValue, action, span)
                : ArgumentNargs.Default;
            var required = keyword.TryGetValue("required", out var requiredValue) && IsTruthy(requiredValue);
            var defaultValue = keyword.TryGetValue("default", out var maybeDefault)
                ? maybeDefault
                : nargs.Kind == ArgumentNargsKind.ZeroOrMore
                    ? new PyList([], context.MemoryGovernor, span)
                    : DefaultForAction(action, context, span);
            object[]? choices = keyword.TryGetValue("choices", out var choicesValue)
                ? [.. ToSequence(choicesValue, span)]
                : null;
            var converter = keyword.TryGetValue("type", out var typeValue) ? ValidateConverter(typeValue, span) : null;
            var constValue = (action == ArgumentAction.StoreConst || nargs.Kind == ArgumentNargsKind.Optional) && keyword.TryGetValue("const", out var constant)
                ? constant
                : PyNone.Instance;
            var suppressHelp = keyword.TryGetValue("help", out var helpValue) && IsSuppress(helpValue);
            var helpText = keyword.TryGetValue("help", out helpValue)
                ? suppressHelp
                    ? null
                    : OptionalString(helpValue, "help", "argparse.ArgumentParser.add_argument", span)
                : null;
            var metavar = keyword.TryGetValue("metavar", out var metavarValue)
                ? OptionalString(metavarValue, "metavar", "argparse.ArgumentParser.add_argument", span)
                : null;
            var versionText = keyword.TryGetValue("version", out var versionValue)
                ? OptionalString(versionValue, "version", "argparse.ArgumentParser.add_argument", span)
                : null;

            return new ArgumentSpec(optionNames, dest, action, required, defaultValue, choices, converter, isPositional, nargs, groupId, constValue, helpText, metavar, versionText, suppressHelp);
        }

        private static void ValidateSupportedKeywords(IEnumerable<string> providedNames, IReadOnlyCollection<string> supportedNames, string signature, LythonSourceSpan span)
        {
            foreach (var name in providedNames)
            {
                if (!supportedNames.Contains(name))
                {
                    throw new LythonRuntimeException("TypeError", $"{signature}(...) got an unexpected keyword argument '{name}'.", span);
                }
            }
        }

        private static void ValidateSupportedKeywords(CallArgumentValue[] arguments, IReadOnlyCollection<string> supportedNames, string signature, LythonSourceSpan span)
        {
            for (var i = 0; i < arguments.Length; i++)
            {
                var name = arguments[i].Name;
                if (name is not null && !supportedNames.Contains(name))
                {
                    throw new LythonRuntimeException("TypeError", $"{signature}(...) got an unexpected keyword argument '{name}'.", span);
                }
            }
        }

        private bool HasOptionalArgumentNamed(string token)
            => FindOptionalArgument(token, out _, out _) is not null;

        private ArgumentSpec? FindOptionalArgument(string token, out string? inlineValue, out string? error)
        {
            inlineValue = null;
            error = null;
            if (!LooksLikeOptionalToken(token))
            {
                return null;
            }

            var optionToken = token;
            var equalsIndex = token.IndexOf('=');
            if (equalsIndex > 0)
            {
                optionToken = token[..equalsIndex];
                inlineValue = token[(equalsIndex + 1)..];
            }

            var exact = FindExactOptionalArgument(optionToken);
            if (exact is not null)
            {
                return exact;
            }

            if (!optionToken.StartsWith("--", StringComparison.Ordinal) &&
                optionToken.Length > 2 &&
                FindExactOptionalArgument(optionToken[..2]) is { } shortWithInline &&
                !IsNoValueAction(shortWithInline.Action))
            {
                inlineValue = optionToken[2..] + (inlineValue is null ? string.Empty : "=" + inlineValue);
                return shortWithInline;
            }

            if (_options.AllowAbbrev && optionToken.StartsWith("--", StringComparison.Ordinal))
            {
                var matches = new List<ArgumentSpec>();
                foreach (var candidate in _arguments)
                {
                    if (candidate.IsPositional)
                    {
                        continue;
                    }

                    foreach (var name in candidate.OptionNames)
                    {
                        if (name.StartsWith("--", StringComparison.Ordinal) &&
                            name.StartsWith(optionToken, StringComparison.Ordinal))
                        {
                            matches.Add(candidate);
                            break;
                        }
                    }
                }

                if (matches.Count == 1)
                {
                    return matches[0];
                }

                if (matches.Count > 1)
                {
                    error = $"ambiguous option: {optionToken}";
                    return null;
                }
            }

            return null;
        }

        private ArgumentSpec? FindExactOptionalArgument(string token)
        {
            foreach (var candidate in _arguments)
            {
                if (!candidate.IsPositional && candidate.OptionNames.Contains(token, StringComparer.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        private bool TryExpandShortFlagCluster(string token, out List<ArgumentSpec> specs)
        {
            specs = [];
            if (!LooksLikeOptionalToken(token) ||
                token.StartsWith("--", StringComparison.Ordinal) ||
                token.Length <= 2 ||
                FindExactOptionalArgument(token) is not null)
            {
                return false;
            }

            for (var i = 1; i < token.Length; i++)
            {
                var spec = FindExactOptionalArgument("-" + token[i]);
                if (spec is null || !IsNoValueAction(spec.Action))
                {
                    specs.Clear();
                    return false;
                }

                specs.Add(spec);
            }

            return specs.Count != 0;
        }

        private static PyString RequireString(string name, object value, LythonSourceSpan span)
        {
            return RequireStringValue(value, name, "argparse.ArgumentParser.add_argument", span);
        }

        private static string InferDestination(IReadOnlyList<string> optionNames, LythonSourceSpan span)
        {
            string? preferred = null;
            for (var i = 0; i < optionNames.Count; i++)
            {
                var candidate = optionNames[i];
                if (candidate.StartsWith("--", StringComparison.Ordinal))
                {
                    preferred = candidate;
                    break;
                }

                preferred ??= candidate;
            }

            if (string.IsNullOrWhiteSpace(preferred))
            {
                throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser.add_argument(...) expects a valid option name.", span);
            }

            return preferred.TrimStart('-').Replace('-', '_');
        }

        private object FormatUsage(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            RequireNoArguments(arguments, "argparse.ArgumentParser.format_usage", span);
            return FormatUsageText();
        }

        private object FormatHelp(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            RequireNoArguments(arguments, "argparse.ArgumentParser.format_help", span);
            return FormatHelpText(context);
        }

        private object PrintUsage(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var target = ResolveOptionalFile(arguments, "argparse.ArgumentParser.print_usage", span);
            WriteToTarget(FormatUsageText(), target ?? context.State.Stdout, span);
            return PyNone.Instance;
        }

        private object PrintHelp(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var target = ResolveOptionalFile(arguments, "argparse.ArgumentParser.print_help", span);
            WriteToTarget(FormatHelpText(context), target ?? context.State.Stdout, span);
            return PyNone.Instance;
        }

        private object Error(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var message = RequireSingleStringArgument(arguments, "argparse.ArgumentParser.error", "message", span);
            WriteParserError(message.AsString(), context, span);
            throw CreateSystemExit(message.AsString(), span, status: 2);
        }

        private object Exit(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var status = BigInteger.Zero;
            object message = PyNone.Instance;
            BindExitArguments(arguments, ref status, ref message, span);
            if (!ReferenceEquals(message, PyNone.Instance))
            {
                if (!PyStringOps.TryAsString(message, out var text))
                {
                    throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser.exit(..., message=...) expects a string or None.", span);
                }

                WriteToTarget(text, context.State.Stderr, span);
            }

            throw CreateSystemExit(string.Empty, span, status);
        }

        private object SetDefaults(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            foreach (var argument in arguments)
            {
                if (argument.Name is null)
                {
                    throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser.set_defaults(...) accepts keyword arguments only.", span);
                }

                _defaults[argument.Name] = argument.Value;
            }

            return PyNone.Instance;
        }

        private object GetDefault(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            var destination = RequireSingleStringArgument(arguments, "argparse.ArgumentParser.get_default", "dest", span).AsString();
            if (_defaults.TryGetValue(destination, out var parserDefault))
            {
                return parserDefault;
            }

            foreach (var argument in _arguments)
            {
                if (string.Equals(argument.Destination, destination, StringComparison.Ordinal))
                {
                    return argument.DefaultValue;
                }
            }

            return PyNone.Instance;
        }

        private object AddSubparsers(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = arguments;
            _ = context;
            throw new LythonRuntimeException("NotImplementedError", "argparse.ArgumentParser.add_subparsers(...) is not supported by Lython.", span);
        }

        private PyString FormatUsageText()
        {
            var usage = _options.Usage;
            if (usage is not null)
            {
                return PyString.FromString(usage.StartsWith("usage:", StringComparison.Ordinal) ? EnsureTrailingNewline(usage) : "usage: " + EnsureTrailingNewline(usage));
            }

            var parts = new List<string> { "usage:", _options.Prog };
            foreach (var argument in _arguments)
            {
                if (argument.Action == ArgumentAction.Help && !_options.AddHelp || argument.SuppressHelp)
                {
                    continue;
                }

                parts.Add(RenderUsagePart(argument));
            }

            return PyString.FromString(string.Join(" ", parts) + "\n");
        }

        private PyString FormatHelpText(ExecutionContext context)
        {
            var builder = new StringBuilder();
            builder.Append(FormatUsageText().AsString());
            if (_options.Description is not null)
            {
                builder.Append('\n');
                builder.AppendLine(_options.Description);
            }

            AppendHelpGroup(builder, "positional arguments", _arguments.Where(static argument => argument.IsPositional), context);
            AppendHelpGroup(builder, "options", _arguments.Where(static argument => !argument.IsPositional), context);

            if (_options.Epilog is not null)
            {
                builder.Append('\n');
                builder.AppendLine(_options.Epilog);
            }

            return PyString.FromString(builder.ToString());
        }

        private void AppendHelpGroup(StringBuilder builder, string title, IEnumerable<ArgumentSpec> arguments, ExecutionContext context)
        {
            var visible = arguments.Where(static argument => !argument.SuppressHelp).ToArray();
            if (visible.Length == 0)
            {
                return;
            }

            builder.Append('\n');
            builder.Append(title);
            builder.AppendLine(":");
            foreach (var argument in visible)
            {
                builder.Append("  ");
                builder.Append(RenderHelpInvocation(argument));
                if (argument.HelpText is not null)
                {
                    builder.Append("  ");
                    builder.Append(ApplyFormatSubstitutions(argument.HelpText, argument, context));
                }

                if (ShowsArgumentDefaults() && !IsSuppress(argument.DefaultValue) && argument.DefaultValue is not PyNone)
                {
                    builder.Append(" (default: ");
                    builder.Append(PyRendering.ToPythonString(argument.DefaultValue, new PyRenderingContext(context)));
                    builder.Append(')');
                }

                builder.Append('\n');
            }
        }

        private bool ShowsArgumentDefaults()
            => _options.FormatterClass is ArgparseFormatterClass { Name: "ArgumentDefaultsHelpFormatter" };

        private string RenderUsagePart(ArgumentSpec argument)
        {
            if (argument.IsPositional)
            {
                return argument.Nargs.Kind switch
                {
                    ArgumentNargsKind.Optional => $"[{argument.DisplayMetavar}]",
                    ArgumentNargsKind.ZeroOrMore => $"[{argument.DisplayMetavar} ...]",
                    ArgumentNargsKind.OneOrMore => $"{argument.DisplayMetavar} [{argument.DisplayMetavar} ...]",
                    ArgumentNargsKind.Fixed => string.Join(" ", Enumerable.Repeat(argument.DisplayMetavar, argument.Nargs.Count)),
                    _ => argument.DisplayMetavar
                };
            }

            var name = argument.OptionNames.FirstOrDefault(static item => item.StartsWith("--", StringComparison.Ordinal)) ??
                       argument.OptionNames.FirstOrDefault() ??
                       argument.Destination;
            var invocation = IsNoValueAction(argument.Action)
                ? name
                : $"{name} {argument.DisplayMetavar}";
            return argument.Required ? invocation : $"[{invocation}]";
        }

        private string RenderHelpInvocation(ArgumentSpec argument)
        {
            if (argument.IsPositional)
            {
                return argument.DisplayMetavar;
            }

            var names = string.Join(", ", argument.OptionNames);
            return IsNoValueAction(argument.Action) ? names : $"{names} {argument.DisplayMetavar}";
        }

        private string ApplyFormatSubstitutions(string text, ArgumentSpec argument, ExecutionContext context)
            => text
                .Replace("%(prog)s", _options.Prog, StringComparison.Ordinal)
                .Replace("%(default)s", PyRendering.ToPythonString(argument.DefaultValue, new PyRenderingContext(context)), StringComparison.Ordinal);

        private void WriteParserError(string message, ExecutionContext context, LythonSourceSpan span)
        {
            WriteToTarget(FormatUsageText(), context.State.Stderr, span);
            WriteToTarget(PyString.FromString($"{_options.Prog}: error: {message}\n"), context.State.Stderr, span);
        }

        private LythonRuntimeException CreateParseFailure(string message, LythonSourceSpan span)
            => _options.ExitOnError
                ? CreateSystemExit(message, span, status: 2)
                : new LythonRuntimeException("ArgumentError", message, span);

        private static LythonRuntimeException CreateSystemExit(string message, LythonSourceSpan span, BigInteger status)
            => new("SystemExit", message, span, innerException: null, payload: status);

        private static LythonRuntimeException CreateSystemExit(string message, LythonSourceSpan span, int status)
            => CreateSystemExit(message, span, new BigInteger(status));

        private static bool LooksLikeOptionalToken(string token)
            => token.Length > 1 && token[0] == '-' && token != "-";

        private static bool LooksLikeNegativeNumber(string token)
            => decimal.TryParse(
                token,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out _);

        private bool HasNegativeNumberOptions()
            => _arguments.Any(static argument =>
                !argument.IsPositional && argument.OptionNames.Any(LooksLikeNegativeNumber));

        private static bool IsNoValueAction(ArgumentAction action)
            => action is ArgumentAction.StoreTrue or ArgumentAction.StoreFalse or ArgumentAction.StoreConst or
                ArgumentAction.Count or ArgumentAction.Help or ArgumentAction.Version;

        private static bool ProducesListValue(ArgumentSpec spec)
            => spec.Nargs.Kind is ArgumentNargsKind.ZeroOrMore or ArgumentNargsKind.OneOrMore or ArgumentNargsKind.Fixed;

        private static BigInteger IncrementCount(object current)
            => current switch
            {
                BigInteger integer => integer + BigInteger.One,
                int integer => new BigInteger(integer + 1),
                PyNone => BigInteger.One,
                _ => BigInteger.One
            };

        private static int RequiredPositionalSlotsAfter(IReadOnlyList<ArgumentSpec> positionals, int index)
        {
            var count = 0;
            for (var i = index + 1; i < positionals.Count; i++)
            {
                var spec = positionals[i];
                count += spec.Nargs.Kind switch
                {
                    ArgumentNargsKind.Default => 1,
                    ArgumentNargsKind.OneOrMore => 1,
                    ArgumentNargsKind.Fixed => spec.Nargs.Count,
                    _ => 0
                };
            }

            return count;
        }

        private bool HasOptionalArgumentNamed(string token, bool exactOnly)
        {
            _ = exactOnly;
            return HasOptionalArgumentNamed(token);
        }

        private int CountRemainingPositionalCandidates(IReadOnlyList<string> argv, int index)
        {
            var count = 0;
            for (var i = index; i < argv.Count; i++)
            {
                if (HasOptionalArgumentNamed(argv[i]))
                {
                    break;
                }

                count++;
            }

            return count;
        }

        private static string JoinUnknownArguments(IEnumerable<string> unknown)
            => string.Join(" ", unknown);

        private static List<string> ToStringList(object value, string message, LythonSourceSpan span)
        {
            if (PyStringOps.TryAsString(value, out _))
            {
                throw new LythonRuntimeException("TypeError", message, span);
            }

            var result = new List<string>();
            foreach (var item in ToSequence(value, span))
            {
                if (!PyStringOps.TryAsString(item, out var text))
                {
                    throw new LythonRuntimeException("TypeError", message, span);
                }

                result.Add(text.AsString());
            }

            return result;
        }

        private static PyString RequireSingleStringArgument(CallArgumentValue[] arguments, string methodName, string parameterName, LythonSourceSpan span)
        {
            object? value = ArgparseUnspecifiedValue.Instance;
            var assigned = false;
            foreach (var argument in arguments)
            {
                if (argument.Name is null)
                {
                    if (assigned)
                    {
                        throw new LythonRuntimeException("TypeError", $"{methodName}(...) got multiple values for argument '{parameterName}'.", span);
                    }

                    value = argument.Value;
                    assigned = true;
                    continue;
                }

                if (argument.Name != parameterName)
                {
                    throw new LythonRuntimeException("TypeError", $"{methodName}(...) got an unexpected keyword argument '{argument.Name}'.", span);
                }

                if (assigned)
                {
                    throw new LythonRuntimeException("TypeError", $"{methodName}(...) got multiple values for argument '{parameterName}'.", span);
                }

                value = argument.Value;
                assigned = true;
            }

            if (!assigned)
            {
                throw new LythonRuntimeException("TypeError", $"{methodName}({parameterName}) expects one argument.", span);
            }

            return RequireStringValue(value.RequireNotNull(), parameterName, methodName, span);
        }

        private static void RequireNoArguments(CallArgumentValue[] arguments, string methodName, LythonSourceSpan span)
        {
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", $"{methodName}() expects no arguments.", span);
            }
        }

        private static object? ResolveOptionalFile(CallArgumentValue[] arguments, string methodName, LythonSourceSpan span)
        {
            object? target = null;
            var assigned = false;
            foreach (var argument in arguments)
            {
                if (argument.Name is null)
                {
                    if (assigned)
                    {
                        throw new LythonRuntimeException("TypeError", $"{methodName}([file]) expects zero or one argument.", span);
                    }

                    target = ReferenceEquals(argument.Value, PyNone.Instance) ? null : argument.Value;
                    assigned = true;
                    continue;
                }

                if (argument.Name != "file")
                {
                    throw new LythonRuntimeException("TypeError", $"{methodName}(...) got an unexpected keyword argument '{argument.Name}'.", span);
                }

                if (assigned)
                {
                    throw new LythonRuntimeException("TypeError", $"{methodName}(...) got multiple values for argument 'file'.", span);
                }

                target = ReferenceEquals(argument.Value, PyNone.Instance) ? null : argument.Value;
                assigned = true;
            }

            return target;
        }

        private static void BindExitArguments(CallArgumentValue[] arguments, ref BigInteger status, ref object message, LythonSourceSpan span)
        {
            var positionalIndex = 0;
            var assignedStatus = false;
            var assignedMessage = false;
            foreach (var argument in arguments)
            {
                if (argument.Name is null)
                {
                    if (positionalIndex == 0)
                    {
                        if (assignedStatus)
                        {
                            throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser.exit(...) got multiple values for argument 'status'.", span);
                        }

                        status = RequireInteger(argument.Value, "status", "argparse.ArgumentParser.exit", span);
                        assignedStatus = true;
                    }
                    else if (positionalIndex == 1)
                    {
                        if (assignedMessage)
                        {
                            throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser.exit(...) got multiple values for argument 'message'.", span);
                        }

                        message = argument.Value;
                        assignedMessage = true;
                    }
                    else
                    {
                        throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser.exit([status][, message]) expects zero to two arguments.", span);
                    }

                    positionalIndex++;
                    continue;
                }

                switch (argument.Name)
                {
                    case "status":
                        if (assignedStatus)
                        {
                            throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser.exit(...) got multiple values for argument 'status'.", span);
                        }

                        status = RequireInteger(argument.Value, "status", "argparse.ArgumentParser.exit", span);
                        assignedStatus = true;
                        break;
                    case "message":
                        if (assignedMessage)
                        {
                            throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser.exit(...) got multiple values for argument 'message'.", span);
                        }

                        message = argument.Value;
                        assignedMessage = true;
                        break;
                    default:
                        throw new LythonRuntimeException("TypeError", $"argparse.ArgumentParser.exit(...) got an unexpected keyword argument '{argument.Name}'.", span);
                }
            }
        }

        private static void WriteToTarget(PyString text, object target, LythonSourceSpan span)
        {
            switch (target)
            {
                case HostTextOutputHandle output:
                    _ = output.Write(text, span);
                    break;
                case ExecutionContext.TextFileHandle file:
                    _ = file.Write(text);
                    break;
                default:
                    throw new LythonRuntimeException("TypeError", "argparse print methods expect a writable Lython text stream or file handle.", span);
            }
        }

        private static string EnsureTrailingNewline(string text)
            => text.EndsWith('\n') ? text : text + "\n";

        private static PyString RequireStringValue(object value, string name, string owner, LythonSourceSpan span)
        {
            if (!PyStringOps.TryAsString(value, out var text))
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(..., {name}=...) expects a string.", span);
            }

            return text;
        }

        private static string? OptionalString(object value, string name, string owner, LythonSourceSpan span)
        {
            if (ReferenceEquals(value, PyNone.Instance))
            {
                return null;
            }

            return RequireStringValue(value, name, owner, span).AsString();
        }

        private static bool RequireBool(object value, string name, string owner, LythonSourceSpan span)
        {
            if (value is bool boolean)
            {
                return boolean;
            }

            throw new LythonRuntimeException("TypeError", $"{owner}(..., {name}=...) expects a bool.", span);
        }

        private static BigInteger RequireInteger(object value, string name, string owner, LythonSourceSpan span)
        {
            if (value is BigInteger integer)
            {
                return integer;
            }

            throw new LythonRuntimeException("TypeError", $"{owner}(..., {name}=...) expects an integer.", span);
        }

        private static bool IsSuppress(object? value)
            => ReferenceEquals(value, ArgparseSuppressValue.Instance) ||
               value is ArgparseSuppressValue ||
               (PyStringOps.TryAsString(value ?? PyNone.Instance, out var text) &&
                text.AsString() is "SUPPRESS" or "==SUPPRESS==");
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

using System.Globalization;
using System.Numerics;
using System.Text.Encodings.Web;
using System.Text;
using System.Text.Json;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ArgumentParserObject
    {
        private readonly record struct ParseInvocation(List<string> Argv, ArgparseNamespaceObject? Namespace);

        private object AddArgument(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            RegisterArgument(CreateArgumentSpec(arguments, span, groupId: null, context));
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
            RegisterArgument(CreateArgumentSpec(arguments, span, groupId, context));
        }
        private void RegisterArgument(ArgumentSpec spec)
        {
            _arguments.Add(spec);
            _argumentsByDestination.TryAdd(spec.Destination, spec);
            if (spec.IsPositional)
            {
                return;
            }
            foreach (var optionName in spec.OptionNames)
            {
                // Preserve the parser's established first-registration behavior for conflicts.
                _optionalArgumentsByName.TryAdd(optionName, spec);
            }
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
        private ParseInvocation ResolveParseInvocation(
            CallArgumentValue[] arguments,
            string methodName,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            object argsValue = ArgparseUnspecifiedValue.Instance;
            object namespaceValue = ArgparseUnspecifiedValue.Instance;
            var positionalIndex = 0;
            var assignedArgs = false;
            var assignedNamespace = false;
            foreach (var argument in arguments)
            {
                if (argument.IsPositional)
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
                switch (argument.KeywordName)
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
                        throw new LythonRuntimeException("TypeError", $"{methodName}(...) got an unexpected keyword argument '{argument.KeywordName}'.", span);
                }
            }
            var argv = ReferenceEquals(argsValue, ArgparseUnspecifiedValue.Instance) || ReferenceEquals(argsValue, PyNone.Instance)
                ? context.State.Args.Select(static item => item.AsString()).ToList()
                : ToStringList(argsValue, $"{methodName}(args) expects an iterable of strings.", span);
            var namespaceObject = ReferenceEquals(namespaceValue, ArgparseUnspecifiedValue.Instance) || ReferenceEquals(namespaceValue, PyNone.Instance)
                ? null
                : namespaceValue as ArgparseNamespaceObject ??
                  throw new LythonRuntimeException("TypeError", $"{methodName}(namespace) expects an argparse.Namespace instance.", span);
            return new ParseInvocation(argv, namespaceObject);
        }
    }
}

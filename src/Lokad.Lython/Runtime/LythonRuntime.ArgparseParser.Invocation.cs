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
            AddArgumentToGroup(arguments, span, null, context);
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
            // Groups are retained alongside the parser like specs below.
            context.MemoryGovernor.Reserve(ArgumentGroupBytes, span);
            context.MemoryGovernor.Commit(ArgumentGroupBytes);
            var group = new ArgparseMutuallyExclusiveGroupObject(this, _nextGroupId++, required);
            _groups.Add(group);
            return group;
        }
        internal void AddArgumentToGroup(CallArgumentValue[] arguments, LythonSourceSpan span, int? groupId, ExecutionContext context)
        {
            var estimate = checked(256L + 128L * arguments.Length);
            foreach (var argument in arguments)
                if ((argument.IsPositional || argument.KeywordName is "dest" or "action" or "nargs" or "help" or "metavar" or "version") && PyStringOps.TryAsString(argument.Value, out var text))
                    estimate = checked(estimate + 6L * text.Utf8Bytes.Length);
            using var construction = context.MemoryGovernor.ReserveTemporary(estimate, span);
            RegisterArgument(CreateArgumentSpec(arguments, span, groupId, context), span, construction);
        }
        private void RegisterArgument(ArgumentSpec spec)
        {
            RegisterArgument(spec, null, null);
        }
        private void RegisterArgument(ArgumentSpec spec, LythonSourceSpan? span, MemoryGovernor.TemporaryMemoryReservation? construction)
        {
            foreach (var name in spec.OptionNames)
                if (_optionalArgumentsByName.ContainsKey(name))
                    throw new LythonRuntimeException(ModuleException("argparse", "ArgumentError"), $"argument {string.Join('/', spec.OptionNames)}: conflicting option string: {name}", span);
            // Spec records, list slots and both lookup indexes are retained for
            // the parser lifetime alongside every retained option string, so
            // own them here at two bytes per character plus infrastructure.
            var stringChars = (long)spec.Destination.Length
                + (spec.HelpText?.Length ?? 0)
                + (spec.Metavar?.Length ?? 0)
                + (spec.VersionText?.Length ?? 0);
            foreach (var optionName in spec.OptionNames)
            {
                stringChars += optionName.Length;
            }

            // The constructor fixed help spec arrives without a governor and stays
            // free like other engine-owned constants; every guest call pays.
            if (construction is not null)
            {
                var charge = checked(ArgumentSpecInfrastructureBytes + (2L * stringChars));
                construction.Commit(charge);
            }
            _arguments.Add(spec);
            _argumentsByDestination.TryAdd(spec.Destination, spec);
            if (spec.IsPositional)
            {
                return;
            }
            foreach (var optionName in spec.OptionNames)
            {
                _optionalArgumentsByName[optionName] = spec;
            }
        }
        private object ParseArgs(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => ParseArgsCoreAsync(arguments, span, context, false, false).GetAwaiter().GetResult();
        private ValueTask<object> ParseArgsAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => ParseArgsCoreAsync(arguments, span, context, true, false);
        private object ParseKnownArgs(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => ParseArgsCoreAsync(arguments, span, context, false, true).GetAwaiter().GetResult();
        private ValueTask<object> ParseKnownArgsAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => ParseArgsCoreAsync(arguments, span, context, true, true);

        private async ValueTask<object> ParseArgsCoreAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous, bool known)
        {
            using var working = context.MemoryGovernor.ReserveTemporary(checked(512L + 256L * (_arguments.Count + (long)_groups.Count + arguments.Length)), span);
            var (argv, suppliedNamespace) = await ResolveParseInvocationAsync(arguments, known ? "argparse.ArgumentParser.parse_known_args" : "argparse.ArgumentParser.parse_args", span, context, working, asynchronous).ConfigureAwait(false);
            var values = suppliedNamespace ?? new ArgparseNamespaceObject(new Dictionary<string, object>(StringComparer.Ordinal), context.MemoryGovernor, span);
            var seenSpecs = new HashSet<ArgumentSpec>();
            var unknown = new List<string>();
            ApplyDefaults(values, context, span);
            var positionalSpecs = new List<ArgumentSpec>(_arguments.Count);
            foreach (var argument in _arguments) if (argument.IsPositional) positionalSpecs.Add(argument);
            var positionalIndex = 0;
            for (var index = 0; index < argv.Count; index++)
            {
                context.CheckExecution(span);
                var token = argv[index];
                if (token == "--")
                {
                    for (index++; index < argv.Count; index++)
                        (index, positionalIndex) = await ConsumePositionalAsync(argv, index, positionalSpecs, positionalIndex, seenSpecs, values, unknown, span, context, asynchronous).ConfigureAwait(false);
                    break;
                }
                // Short clusters can hold one specification reference per input character.
                using var clusterStorage = token.Length > 2 && token[0] == '-' && token[1] != '-'
                    ? context.MemoryGovernor.ReserveTemporary(64L + 24L * token.Length, span) : null;
                if (TryExpandShortFlagCluster(token, out var clusterSpecs))
                {
                    foreach (var clusterSpec in clusterSpecs)
                        await ApplyNoValueOptionalAsync(clusterSpec, token, seenSpecs, values, span, context, asynchronous).ConfigureAwait(false);
                    continue;
                }
                var optionalSpec = FindOptionalArgument(token, out var inlineValue, out var optionError);
                if (optionError is not null) throw CreateParseFailure(optionError, span);
                if (optionalSpec is not null)
                {
                    index = await ApplyOptionalAsync(optionalSpec, token, inlineValue, argv, index, seenSpecs, values, span, context, asynchronous).ConfigureAwait(false);
                    continue;
                }
                if (LooksLikeOptionalToken(token) && (!LooksLikeNegativeNumber(token) || HasNegativeNumberOptions())) { unknown.Add(token); continue; }
                (index, positionalIndex) = await ConsumePositionalAsync(argv, index, positionalSpecs, positionalIndex, seenSpecs, values, unknown, span, context, asynchronous).ConfigureAwait(false);
            }
            await ConvertUnseenDefaultsAsync(seenSpecs, values, context, span, asynchronous).ConfigureAwait(false);
            ValidateRequiredArguments(seenSpecs, span);
            ValidateMutuallyExclusiveGroups(seenSpecs, span);
            if (!known)
            {
                if (unknown.Count != 0) throw CreateParseFailure($"unrecognized arguments: {JoinUnknownArguments(unknown)}", span);
                return values;
            }
            var extras = new PyList([], context.MemoryGovernor, span);
            foreach (var item in unknown) extras.Add(PyString.FromString(item, context.MemoryGovernor, span));
            return new PyTuple([values, extras], context.MemoryGovernor, span);
        }

        private async ValueTask<ParseInvocation> ResolveParseInvocationAsync(
            CallArgumentValue[] arguments,
            string methodName,
            LythonSourceSpan span,
            ExecutionContext context, MemoryGovernor.TemporaryMemoryReservation working, bool asynchronous)
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
            var argv = new List<string>();
            if (ReferenceEquals(argsValue, ArgparseUnspecifiedValue.Instance) || ReferenceEquals(argsValue, PyNone.Instance))
                foreach (var item in context.State.Args) AddArgumentText(item);
            else if (asynchronous)
                await foreach (var item in ToSequenceAsync(argsValue, span, context).ConfigureAwait(false)) AddArgumentText(item);
            else foreach (var item in ToSequence(argsValue, span, context)) AddArgumentText(item);
            void AddArgumentText(object item)
            {
                context.CheckExecution(span);
                if (!PyStringOps.TryAsString(item, out var text)) throw RuntimeErrors.Type($"{methodName}(args) expects an iterable of strings.", span);
                working.Grow(128L + 4L * text.Utf8Bytes.Length, span);
                argv.Add(text.AsString());
            }
            var namespaceObject = ReferenceEquals(namespaceValue, ArgparseUnspecifiedValue.Instance) || ReferenceEquals(namespaceValue, PyNone.Instance)
                ? null
                : namespaceValue as ArgparseNamespaceObject ??
                  throw new LythonRuntimeException("TypeError", $"{methodName}(namespace) expects an argparse.Namespace instance.", span);
            return new ParseInvocation(argv, namespaceObject);
        }
    }
}

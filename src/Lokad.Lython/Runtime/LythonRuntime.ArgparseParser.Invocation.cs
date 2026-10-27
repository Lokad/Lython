using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ArgumentParserObject
    {
        private readonly record struct ParseInvocation(List<string> Argv, ArgparseNamespaceObject? Namespace);

        private object AddArgument(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => AddArgumentToGroup(arguments, span, null, context);
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
            object required = arguments.Length == 1 ? arguments[0].Value : false;
            // Groups are retained alongside the parser like specs below.
            context.MemoryGovernor.Reserve(ArgumentGroupBytes, span);
            context.MemoryGovernor.Commit(ArgumentGroupBytes);
            var group = new ArgparseMutuallyExclusiveGroupObject(this, _nextGroupId++, required);
            _groups.Add(group);
            return group;
        }
        internal object AddArgumentToGroup(CallArgumentValue[] arguments, LythonSourceSpan span, int? groupId, ExecutionContext context)
        {
            var estimate = checked(256L + 128L * arguments.Length);
            foreach (var argument in arguments)
                if ((argument.IsPositional || argument.KeywordName is "dest" or "action" or "nargs" or "help" or "metavar" or "version") && PyStringOps.TryAsString(argument.Value, out var text))
                    estimate = checked(estimate + 6L * text.Utf8Bytes.Length);
            using var construction = context.MemoryGovernor.ReserveTemporary(estimate, span);
            EnsureFormatter(span);
            var spec = CreateArgumentSpec(arguments, span, groupId, context);
            RegisterArgument(spec, span, construction);
            return spec;
        }
        private void RegisterArgument(ArgumentSpec spec, LythonSourceSpan? span = null, MemoryGovernor.TemporaryMemoryReservation? construction = null)
        {
            foreach (var name in spec.OptionNames)
                if (_optionalArgumentsByName.ContainsKey(name))
                    throw new LythonRuntimeException(ModuleException("argparse", "ArgumentError"), $"argument {string.Join('/', spec.OptionNames)}: conflicting option string: {name}", span);
            // Spec records, list slots and the option lookup index are retained for
            // the parser lifetime alongside every retained option string, so
            // own them here at two bytes per character plus infrastructure.
            var stringChars = (long)spec.Destination.Length;
            foreach (var optionName in spec.OptionNames)
            {
                stringChars += optionName.Length;
            }

            // Constructor help metadata is covered by the parser's shell;
            // guest additions transfer their construction reservation here.
            if (construction is not null)
            {
                var charge = checked(ArgumentSpecInfrastructureBytes + (2L * stringChars));
                construction.Commit(charge);
            }
            _arguments.Add(spec);
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
            var values = suppliedNamespace ?? new ArgparseNamespaceObject(context.MemoryGovernor, span);
            ApplyDefaults(values, context, span);
            var exitOnError = await FlagAsync(_options.ExitOnError, context, span, asynchronous).ConfigureAwait(false);
            try
            {
                working.Grow(64L * argv.Count, span);
                var pattern = new char[argv.Count];
                var options = new (ArgumentSpec? Spec, string? Inline, bool HasEquals, string? Error)[argv.Count];
                var ended = false;
                var negativeOptions = _arguments.Any(a => !a.IsPositional && a.OptionNames.Any(n => LooksLikeNegativeNumber(n, context, span)));
                for (var i = 0; i < argv.Count; i++)
                {
                    context.CheckExecution(span);
                    var token = argv[i];
                    if (ended) { pattern[i] = 'A'; continue; }
                    if (token == "--") { pattern[i] = '-'; ended = true; continue; }
                    options[i] = await FindOptionalArgumentAsync(token, span, context, asynchronous).ConfigureAwait(false);
                    if (options[i].Error is { } error) throw CreateParseFailure(error, span);
                    pattern[i] = options[i].Spec is not null || token.Length > 1 && token[0] == '-' &&
                        !token.Contains(' ') && (negativeOptions || !LooksLikeNegativeNumber(token, context, span)) ? 'O' : 'A';
                }
                var seen = new HashSet<ArgumentSpec>();
                var groups = new Dictionary<int, ArgumentSpec>();
                var unknown = new List<string>();
                var positional = _arguments.Where(a => a.IsPositional).ToArray();
                var position = 0;
                for (var index = 0; index < argv.Count;)
                {
                    context.CheckExecution(span);
                    if (pattern[index] == 'O')
                    {
                        if (options[index].Spec is not { } spec) { unknown.Add(argv[index++]); continue; }
                        index = await ConsumeOptionAsync(spec, options[index].Inline, options[index].HasEquals, argv, pattern, index, unknown,
                            seen, groups, values, context, span, asynchronous).ConfigureAwait(false);
                    }
                    else
                    {
                        var end = index;
                        while (end < argv.Count && pattern[end] != 'O') { context.CheckExecution(span); end++; }
                        (index, position) = await ConsumePositionalsAsync(argv, pattern, index, end, positional, position, seen, groups, values, context, span, asynchronous).ConfigureAwait(false);
                        while (index < end) unknown.Add(argv[index++]);
                    }
                }
                await ConsumePositionalsAsync(argv, pattern, argv.Count, argv.Count, positional, position, seen, groups, values, context, span, asynchronous).ConfigureAwait(false);
                await ConvertUnseenDefaultsAsync(seen, values, context, span, asynchronous).ConfigureAwait(false);
                await ValidateRequiredArgumentsAsync(seen, span, context, asynchronous).ConfigureAwait(false);
                foreach (var group in _groups)
                    if (!groups.ContainsKey(group.Id) && await FlagAsync(group.Required, context, span, asynchronous).ConfigureAwait(false))
                        throw CreateParseFailure("one of the mutually exclusive arguments is required", span);
                if (!known && unknown.Count != 0) throw CreateParseFailure("unrecognized arguments: " + string.Join(" ", unknown), span);
                if (!known) return values;
                var extras = new PyList([], context.MemoryGovernor, span);
                foreach (var item in unknown) extras.Add(PyString.FromString(item, context.MemoryGovernor, span));
                return new PyTuple([values, extras], context.MemoryGovernor, span);
            }
            catch (LythonRuntimeException error) when (error.Identity == ModuleException("argparse", "ArgumentError"))
            {
                context.CheckExecution(span);
                if (!exitOnError) throw;
                await ReportErrorAsync(PyString.FromString(error.Message, context.MemoryGovernor, span), span, context, asynchronous).ConfigureAwait(false);
                throw CreateSystemExit(error.Message, span, new BigInteger(2));
            }
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

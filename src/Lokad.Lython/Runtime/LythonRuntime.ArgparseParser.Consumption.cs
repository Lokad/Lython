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
        private void ApplyDefaults(ArgparseNamespaceObject values, ExecutionContext context, LythonSourceSpan span)
        {
            foreach (var spec in _arguments)
            {
                var defaultValue = _defaults.TryGetValue(spec.Destination, out var parserDefault)
                    ? parserDefault
                    : spec.DefaultValue;
                if (IsSuppress(defaultValue) || values.Members.ContainsKey(spec.Destination))
                {
                    continue;
                }
                values.TrySetMember(spec.Destination, defaultValue);
            }
            foreach (var pair in _defaults)
            {
                if (!values.Members.ContainsKey(pair.Key) && !IsSuppress(pair.Value))
                {
                    values.TrySetMember(pair.Key, pair.Value);
                }
            }
        }
        private async ValueTask<(int Index, int PositionalIndex)> ConsumePositionalAsync(
            List<string> argv, int index, IReadOnlyList<ArgumentSpec> positionalSpecs, int positionalIndex,
            HashSet<ArgumentSpec> seenSpecs, ArgparseNamespaceObject values, List<string> unknown,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            if (positionalIndex >= positionalSpecs.Count)
            {
                unknown.Add(argv[index]);
                return (index, positionalIndex);
            }
            var spec = positionalSpecs[positionalIndex];
            var tokens = CollectPositionalTokens(spec, argv, ref index, positionalSpecs, positionalIndex, span, context);
            if (tokens.Count == 0) return (index, positionalIndex);
            var parsed = await CreateParsedValueAsync(spec, tokens, spec.Destination, span, context, asynchronous).ConfigureAwait(false);
            StoreParsedValue(spec, parsed, seenSpecs, values, context, span);
            return (index, positionalIndex + 1);
        }
        private List<string> CollectPositionalTokens(
            ArgumentSpec spec,
            IReadOnlyList<string> argv,
            ref int index,
            IReadOnlyList<ArgumentSpec> positionalSpecs,
            int positionalIndex,
            LythonSourceSpan span, ExecutionContext context)
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
                    context.CheckExecution(span);
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
                    context.CheckExecution(span);
                    tokens.Add(argv[index]);
                    return tokens;
                case ArgumentNargsKind.ZeroOrMore:
                case ArgumentNargsKind.OneOrMore:
                    var requiredAfter = RequiredPositionalSlotsAfter(positionalSpecs, positionalIndex);
                    while (index < argv.Count &&
                           !HasOptionalArgumentNamed(argv[index]) &&
                           CountRemainingPositionalCandidates(argv, index) > requiredAfter)
                    {
                        context.CheckExecution(span);
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
        private async ValueTask<int> ApplyOptionalAsync(
            ArgumentSpec spec, string optionToken, string? inlineValue, IReadOnlyList<string> argv, int index,
            HashSet<ArgumentSpec> seenSpecs, ArgparseNamespaceObject values, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            if (IsNoValueAction(spec.Action))
            {
                if (inlineValue is not null) throw CreateParseFailure($"argument {optionToken}: ignored explicit argument '{inlineValue}'", span);
                await ApplyNoValueOptionalAsync(spec, optionToken, seenSpecs, values, span, context, asynchronous).ConfigureAwait(false);
                return index;
            }
            var tokens = CollectOptionalTokens(spec, optionToken, inlineValue, argv, ref index, span, context);
            var parsed = await CreateParsedValueAsync(spec, tokens, optionToken, span, context, asynchronous).ConfigureAwait(false);
            StoreParsedValue(spec, parsed, seenSpecs, values, context, span);
            return index;
        }
        private async ValueTask ApplyNoValueOptionalAsync(
            ArgumentSpec spec,
            string optionToken,
            HashSet<ArgumentSpec> seenSpecs,
            ArgparseNamespaceObject values,
            LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            switch (spec.Action)
            {
                case ArgumentAction.StoreTrue:
                    values.TrySetMember(spec.Destination, true);
                    break;
                case ArgumentAction.StoreFalse:
                    values.TrySetMember(spec.Destination, false);
                    break;
                case ArgumentAction.StoreConst:
                    values.TrySetMember(spec.Destination, spec.ConstValue);
                    break;
                case ArgumentAction.Count:
                    values.TrySetMember(spec.Destination, IncrementCount(values.Members.TryGetValue(spec.Destination, out var current) ? current : PyNone.Instance));
                    break;
                case ArgumentAction.Help:
                    if (asynchronous) await context.State.Stdout.WriteAsync(FormatHelpText(context), span).ConfigureAwait(false);
                    else WriteToTarget(FormatHelpText(context), context.State.Stdout, span);
                    throw CreateSystemExit(string.Empty, span, status: 0);
                case ArgumentAction.Version:
                    var versionText = spec.VersionText ?? string.Empty;
                    if (!versionText.EndsWith('\n'))
                    {
                        versionText += "\n";
                    }
                    var version = PyString.FromString(ApplyFormatSubstitutions(versionText, spec, context), context.MemoryGovernor, span);
                    if (asynchronous) await context.State.Stdout.WriteAsync(version, span).ConfigureAwait(false);
                    else WriteToTarget(version, context.State.Stdout, span);
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
            LythonSourceSpan span, ExecutionContext context)
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
                    context.CheckExecution(span);
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
                        context.CheckExecution(span);
                        tokens.Add(argv[index]);
                    }
                    return tokens;
                case ArgumentNargsKind.Optional:
                    if (tokens.Count == 0)
                    {
                        if (index + 1 < argv.Count && !HasOptionalArgumentNamed(argv[index + 1]))
                        {
                            index++;
                            context.CheckExecution(span);
                            tokens.Add(argv[index]);
                        }
                    }
                    return tokens;
                case ArgumentNargsKind.ZeroOrMore:
                case ArgumentNargsKind.OneOrMore:
                    while (index + 1 < argv.Count && !HasOptionalArgumentNamed(argv[index + 1]))
                    {
                        index++;
                        context.CheckExecution(span);
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
        private async ValueTask<object> CreateParsedValueAsync(ArgumentSpec spec, IReadOnlyList<string> tokens, string displayName, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            if (spec.Nargs.Kind == ArgumentNargsKind.Optional && tokens.Count == 0)
            {
                var fallback = spec.IsPositional ? (_defaults.TryGetValue(spec.Destination, out var value) ? value : spec.DefaultValue) : spec.ConstValue;
                return fallback is PyString text ? await ConvertArgumentValueAsync(spec, displayName, text, span, context, asynchronous).ConfigureAwait(false) : fallback;
            }
            if (ProducesListValue(spec))
            {
                var values = new PyList([], context.MemoryGovernor, span);
                foreach (var token in tokens)
                    values.Add(await ConvertArgumentValueAsync(spec, displayName, PyString.FromString(token, context.MemoryGovernor, span), span, context, asynchronous).ConfigureAwait(false));
                return values;
            }
            if (tokens.Count == 0) return PyNone.Instance;
            return await ConvertArgumentValueAsync(spec, displayName, PyString.FromString(tokens[0], context.MemoryGovernor, span), span, context, asynchronous).ConfigureAwait(false);
        }

        private void StoreParsedValue(ArgumentSpec spec, object parsed, HashSet<ArgumentSpec> seenSpecs, ArgparseNamespaceObject values, ExecutionContext context, LythonSourceSpan span)
        {
            if (spec.Action == ArgumentAction.Append)
            {
                // Append copies on use; absent/default values otherwise retain their aliases.
                values.Members.TryGetValue(spec.Destination, out var existing);
                var list = existing is null or PyNone ? new PyList([], context.MemoryGovernor, span)
                    : existing is PyList source ? new PyList(source, context.MemoryGovernor, span)
                    : throw RuntimeErrors.Type("argparse append requires a list default or None", span);
                list.Add(parsed);
                values.TrySetMember(spec.Destination, list);
            }
            else values.TrySetMember(spec.Destination, parsed);
            seenSpecs.Add(spec);
        }

        private async ValueTask<object> ConvertArgumentValueAsync(ArgumentSpec spec, string optionName, PyString token, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            var converted = await InvokeArgumentConverterAsync(spec, optionName, token, span, context, asynchronous).ConfigureAwait(false);
            if (spec.Choices is not null)
            {
                var matched = asynchronous ? await ContainsAsync(spec.Choices, converted, context, span).ConfigureAwait(false) : Contains(spec.Choices, converted, context, span);
                context.CheckExecution(span);
                if (!matched) throw CreateParseFailure($"argument {optionName}: invalid choice: '{token.AsString()}'", span);
            }
            return converted;
        }

        private async ValueTask<object> InvokeArgumentConverterAsync(ArgumentSpec spec, string optionName, PyString token, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            if (spec.Converter is not { } callable) return token;
            try
            {
                var value = asynchronous ? await CallableInvocation.InvokeUnaryAsync(callable, token, span, context).ConfigureAwait(false)
                    : CallableInvocation.InvokeUnary(callable, token, span, context);
                context.CheckExecution(span);
                return RuntimeValue(value);
            }
            catch (LythonRuntimeException ex) when (ex.ExceptionType is "ArgumentTypeError" or "ValueError" or "TypeError")
            { throw CreateParseFailure($"argument {optionName}: {ex.Message}", span); }
        }

        private async ValueTask ConvertUnseenDefaultsAsync(HashSet<ArgumentSpec> seen, ArgparseNamespaceObject values, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            for (var index = 0; index < _arguments.Count; index++)
            {
                context.CheckExecution(span);
                var spec = _arguments[index];
                var fallback = _defaults.TryGetValue(spec.Destination, out var value) ? value : spec.DefaultValue;
                if (!seen.Contains(spec) && fallback is PyString text && values.Members.TryGetValue(spec.Destination, out var existing) && ReferenceEquals(existing, fallback))
                    values.TrySetMember(spec.Destination, await InvokeArgumentConverterAsync(spec, spec.OptionNames.FirstOrDefault() ?? spec.Destination, text, span, context, asynchronous).ConfigureAwait(false));
            }
        }
        private void ValidateRequiredArguments(HashSet<ArgumentSpec> seenSpecs, LythonSourceSpan span)
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
            var presentByGroup = new Dictionary<int, int>();
            foreach (var spec in seenSpecs)
            {
                if (spec.GroupId is { } groupId)
                {
                    presentByGroup.TryGetValue(groupId, out var count);
                    presentByGroup[groupId] = count + 1;
                }
            }
            foreach (var group in _groups)
            {
                presentByGroup.TryGetValue(group.Id, out var present);
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
    }
}

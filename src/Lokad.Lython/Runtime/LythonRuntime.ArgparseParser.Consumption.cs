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

using System.Numerics;
using System.Text;
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
                var defaultValue = spec.DefaultValue;
                if (IsSuppress(defaultValue) || values.HasMember(spec.Destination))
                {
                    continue;
                }
                values.TrySetMember(spec.Destination, defaultValue);
            }
            foreach (var pair in _defaults)
            {
                if (!values.HasMember(pair.Key) && !IsSuppress(pair.Value))
                {
                    values.TrySetMember(pair.Key, pair.Value);
                }
            }
        }
        private static int MinimumArguments(ArgumentSpec spec) => spec.Nargs.Kind switch
        { ArgumentNargsKind.Default or ArgumentNargsKind.OneOrMore => 1, ArgumentNargsKind.Fixed => spec.Nargs.Count, _ => 0 };
        private static int MaximumArguments(ArgumentSpec spec) => spec.Nargs.Kind switch
        { ArgumentNargsKind.Default or ArgumentNargsKind.Optional => 1, ArgumentNargsKind.Fixed => spec.Nargs.Count, _ => int.MaxValue };

        // Allocate the largest matching prefix, greedily reserving only the
        // remaining minimum. Each input segment is counted once, not per token.
        private async ValueTask<(int Index, int Position)> ConsumePositionalsAsync(IReadOnlyList<string> argv, char[] pattern,
            int index, int end, IReadOnlyList<ArgumentSpec> positional, int position, HashSet<ArgumentSpec> seen,
            Dictionary<int, ArgumentSpec> groups, ArgparseNamespaceObject values, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            var available = 0;
            for (var i = index; i < end; i++) { context.CheckExecution(span); if (pattern[i] == 'A') available++; }
            long minimum = 0; var prefix = position;
            while (prefix < positional.Count && minimum + MinimumArguments(positional[prefix]) <= available)
            { context.CheckExecution(span); minimum += MinimumArguments(positional[prefix++]); }
            var counts = new int[prefix - position];
            var remaining = available;
            for (var i = position; i < prefix; i++)
            {
                context.CheckExecution(span);
                minimum -= MinimumArguments(positional[i]);
                var take = (int)Math.Min(MaximumArguments(positional[i]), remaining - minimum);
                counts[i - position] = take; remaining -= take;
            }
            // Empty trailing groups before an option remain available for the
            // next segment, matching Python's partial positional consumption.
            if (end < argv.Count) while (prefix > position && counts[prefix - position - 1] == 0) prefix--;
            for (var i = position; i < prefix; i++)
            {
                var spec = positional[i];
                var tokens = new List<string>();
                while (index < end && pattern[index] == '-') index++;
                for (var n = 0; n < counts[i - position]; n++)
                {
                    context.CheckExecution(span);
                    tokens.Add(argv[index++]);
                    while (index < end && pattern[index] == '-') index++;
                }
                if (IsNoValueAction(spec.Action)) await ApplyNoValueAsync(spec, seen, groups, values, context, span, asynchronous).ConfigureAwait(false);
                else
                {
                    var parsed = await CreateParsedValueAsync(spec, tokens, spec.Destination, span, context, asynchronous).ConfigureAwait(false);
                    StoreParsedValue(spec, parsed, seen, groups, values, context, span);
                }
            }
            return (index, prefix);
        }
        private async ValueTask<int> ConsumeOptionAsync(ArgumentSpec first, string? explicitValue, bool equals,
            IReadOnlyList<string> argv, char[] pattern, int index, List<string> unknown, HashSet<ArgumentSpec> seen,
            Dictionary<int, ArgumentSpec> groups, ArgparseNamespaceObject values, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            var spec = first; var flags = 0; var offset = 0;
            // Resolve the entire lexical cluster and arity before effects.
            // Conversion still runs after preceding flag actions, in order.
            while (IsNoValueAction(spec.Action))
            {
                context.CheckExecution(span);
                flags++;
                if (equals) throw CreateParseFailure("argument " + argv[index] + ": ignored explicit argument '" + explicitValue + "'", span);
                if (explicitValue is null || offset == explicitValue.Length) { spec = null; break; }
                if (explicitValue[offset] == '-') throw CreateParseFailure("argument " + argv[index] + ": ignored explicit argument '" + explicitValue + "'", span);
                var rune = Rune.GetRuneAt(explicitValue, offset);
                var previous = offset;
                offset += rune.Utf16SequenceLength;
                spec = _optionalArgumentsByName.GetValueOrDefault("-" + rune.ToString());
                if (spec is null) { unknown.Add("-" + explicitValue[previous..]); break; }
                if (offset < explicitValue.Length && explicitValue[offset] == '=') { equals = true; offset++; }
            }
            var tokens = new List<string>();
            var stop = index + 1;
            if (spec is not null)
            {
                if (explicitValue is not null)
                {
                    if (MinimumArguments(spec) > 1 || MaximumArguments(spec) < 1)
                        throw CreateParseFailure("argument " + argv[index] + ": expected " + MinimumArguments(spec) + " arguments", span);
                    tokens.Add(explicitValue[offset..]);
                }
                else
                {
                    while (stop < argv.Count && pattern[stop] == 'A' && tokens.Count < MaximumArguments(spec))
                    { context.CheckExecution(span); tokens.Add(argv[stop++]); }
                    if (tokens.Count < MinimumArguments(spec))
                        throw CreateParseFailure("argument " + argv[index] + ": expected " + (MinimumArguments(spec) == 1 ? "one argument" : MinimumArguments(spec) + " arguments"), span);
                }
            }
            var flag = first; var flagOffset = 0;
            for (var n = 0; n < flags; n++)
            {
                await ApplyNoValueAsync(flag, seen, groups, values, context, span, asynchronous).ConfigureAwait(false);
                if (n + 1 < flags)
                {
                    var rune = Rune.GetRuneAt(explicitValue!, flagOffset); flagOffset += rune.Utf16SequenceLength;
                    flag = _optionalArgumentsByName["-" + rune.ToString()];
                }
            }
            if (spec is not null)
            {
                var parsed = await CreateParsedValueAsync(spec, tokens, spec.OptionNames.FirstOrDefault() ?? spec.Destination, span, context, asynchronous).ConfigureAwait(false);
                StoreParsedValue(spec, parsed, seen, groups, values, context, span);
            }
            return stop;
        }
        private async ValueTask ApplyNoValueAsync(ArgumentSpec spec, HashSet<ArgumentSpec> seen,
            Dictionary<int, ArgumentSpec> groups, ArgparseNamespaceObject values, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            object parsed;
            switch (spec.Action)
            {
                case ArgumentAction.StoreTrue: parsed = true; break;
                case ArgumentAction.StoreFalse: parsed = false; break;
                case ArgumentAction.StoreConst: parsed = spec.ConstValue; break;
                case ArgumentAction.Count:
                    values.TryGetMember(spec.Destination, out var current);
                    parsed = current is null or PyNone ? BigInteger.One : asynchronous
                        ? await EvaluateBinaryOperatorAsync(Frontend.BinaryOperatorSyntax.Add, current, BigInteger.One, context, span).ConfigureAwait(false)
                        : EvaluateBinaryOperator(Frontend.BinaryOperatorSyntax.Add, current, BigInteger.One, context, span);
                    break;
                case ArgumentAction.Help:
                    var help = await FormatHelpTextAsync(context, span, asynchronous).ConfigureAwait(false);
                    await WriteAsync(context.State.Stdout, help, span, context, asynchronous).ConfigureAwait(false);
                    throw CreateSystemExit(string.Empty, span, BigInteger.Zero);
                case ArgumentAction.Version:
                    await WriteAsync(context.State.Stdout, FormatVersionText(spec, context, span), span, context, asynchronous).ConfigureAwait(false);
                    throw CreateSystemExit(string.Empty, span, BigInteger.Zero);
                default: throw new InvalidOperationException("Unsupported no-value argparse action.");
            }
            StoreParsedValue(spec, parsed, seen, groups, values, context, span);
        }

        private async ValueTask<object> CreateParsedValueAsync(ArgumentSpec spec, IReadOnlyList<string> tokens, string displayName, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            if (spec.Nargs.Kind == ArgumentNargsKind.Optional && tokens.Count == 0)
            {
                var fallback = spec.IsPositional ? spec.DefaultValue : spec.ConstValue;
                return fallback is PyString text ? await ConvertArgumentValueAsync(spec, displayName, text, span, context, asynchronous).ConfigureAwait(false) : fallback;
            }
            if (spec.IsPositional && spec.Nargs.Kind == ArgumentNargsKind.ZeroOrMore && tokens.Count == 0 && spec.DefaultValue is not PyNone)
                return spec.DefaultValue;
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

        private void StoreParsedValue(ArgumentSpec spec, object parsed, HashSet<ArgumentSpec> seenSpecs, Dictionary<int, ArgumentSpec> groups, ArgparseNamespaceObject values, ExecutionContext context, LythonSourceSpan span)
        {
            context.CheckExecution(span);
            if (spec.GroupId is { } group && (IsNoValueAction(spec.Action) || !AreIdentical(parsed, spec.DefaultValue)))
            {
                if (groups.TryGetValue(group, out var other) && !ReferenceEquals(other, spec)) throw CreateParseFailure("mutually exclusive arguments must not be used together", span);
                groups[group] = spec;
            }
            if (IsSuppress(parsed)) { seenSpecs.Add(spec); return; }
            if (spec.Action == ArgumentAction.Append)
            {
                // Append copies on use; absent/default values otherwise retain their aliases.
                values.TryGetMember(spec.Destination, out var existing);
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
                var fallback = spec.DefaultValue;
                if (!seen.Contains(spec) && fallback is PyString text && values.TryGetMember(spec.Destination, out var existing) && ReferenceEquals(existing, fallback))
                    values.TrySetMember(spec.Destination, await InvokeArgumentConverterAsync(spec, spec.OptionNames.FirstOrDefault() ?? spec.Destination, text, span, context, asynchronous).ConfigureAwait(false));
            }
        }
        private async ValueTask ValidateRequiredArgumentsAsync(HashSet<ArgumentSpec> seenSpecs, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
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
                if (!seenSpecs.Contains(spec) && await FlagAsync(spec.Required, context, span, asynchronous).ConfigureAwait(false))
                {
                    missing.Add(spec.OptionNames.FirstOrDefault() ?? spec.Destination);
                }
            }
            if (missing.Count != 0)
            {
                throw CreateParseFailure($"the following arguments are required: {string.Join(", ", missing)}", span);
            }
        }
    }
}

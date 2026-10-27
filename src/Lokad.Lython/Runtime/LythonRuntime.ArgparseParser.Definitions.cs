using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ArgumentParserObject
    {
        private static object DefaultForAction(ArgumentAction action)
        {
            return action switch
            {
                ArgumentAction.StoreTrue => false,
                ArgumentAction.StoreFalse => true,
                ArgumentAction.Append => PyNone.Instance,
                ArgumentAction.StoreConst => PyNone.Instance,
                ArgumentAction.Count => PyNone.Instance,
                ArgumentAction.Help or ArgumentAction.Version => ArgparseModule.Suppress,
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
        private static ICallable? ValidateConverter(object value, LythonSourceSpan span)
        {
            if (ReferenceEquals(value, PyNone.Instance))
            {
                return null;
            }
            if (value is ICallable callable && IsCallable(value))
            {
                return callable;
            }
            throw new LythonRuntimeException(
                "ValueError",
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
            throw new LythonRuntimeException(
                "ValueError",
                "argparse.ArgumentParser.add_argument(..., nargs=...) expects '?', '*', '+', or a positive integer.",
                span);
        }
        private ArgumentSpec CreateArgumentSpec(CallArgumentValue[] arguments, LythonSourceSpan span, int? groupId, ExecutionContext context)
        {
            if (arguments.Length == 0)
            {
                throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser.add_argument(...) expects at least one argument name.", span);
            }
            var positional = new List<object>(arguments.Length);
            var keyword = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var argument in arguments)
            {
                if (argument.IsPositional)
                {
                    positional.Add(argument.Value);
                }
                else
                {
                    keyword[argument.KeywordName] = argument.Value;
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
                : IsNoValueAction(action) ? ArgumentNargs.Fixed(0) : ArgumentNargs.Default;
            object required = keyword.TryGetValue("required", out var requiredValue) ? requiredValue : false;
            var defaultValue = keyword.TryGetValue("default", out var maybeDefault)
                ? maybeDefault
                : _defaults.TryGetValue(dest, out var parserDefault) ? parserDefault : DefaultForAction(action);
            // Keep the original choices value; parse-time membership owns its protocols.
            object? choices = keyword.TryGetValue("choices", out var choicesValue) && choicesValue is not PyNone
                ? choicesValue
                : null;
            var converter = keyword.TryGetValue("type", out var typeValue) ? ValidateConverter(typeValue, span) : null;
            var constValue = (action == ArgumentAction.StoreConst || nargs.Kind == ArgumentNargsKind.Optional) && keyword.TryGetValue("const", out var constant)
                ? constant
                : PyNone.Instance;
            var helpText = keyword.TryGetValue("help", out var helpValue)
                ? OptionalArgparseString(helpValue, "help", "argparse.ArgumentParser.add_argument", span)
                : null;
            var metavar = keyword.TryGetValue("metavar", out var metavarValue)
                ? OptionalArgparseString(metavarValue, "metavar", "argparse.ArgumentParser.add_argument", span)
                : null;
            var versionText = keyword.TryGetValue("version", out var versionValue)
                ? OptionalArgparseString(versionValue, "version", "argparse.ArgumentParser.add_argument", span)
                : null;
            return new ArgumentSpec(optionNames, dest, action, required, defaultValue, choices, converter, isPositional, nargs, groupId, constValue, helpText, metavar, versionText, context.MemoryGovernor, span);
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
                if (arguments[i].IsKeyword && !supportedNames.Contains(arguments[i].KeywordName))
                {
                    throw new LythonRuntimeException("TypeError", $"{signature}(...) got an unexpected keyword argument '{arguments[i].KeywordName}'.", span);
                }
            }
        }
        private async ValueTask<(ArgumentSpec? Spec, string? Inline, bool HasEquals, string? Error)> FindOptionalArgumentAsync(string token,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            if (token.Length < 2 || token[0] != '-') return default;
            if (_optionalArgumentsByName.TryGetValue(token, out var exact)) return (exact, null, false, null);
            var equals = token.IndexOf('=');
            var name = equals > 0 ? token[..equals] : token;
            var value = equals > 0 ? token[(equals + 1)..] : null;
            if (_optionalArgumentsByName.TryGetValue(name, out exact)) return (exact, value, equals > 0, null);
            if (token[1] != '-')
            {
                var length = 1 + Rune.GetRuneAt(token, 1).Utf16SequenceLength;
                if (name.Length > length && _optionalArgumentsByName.TryGetValue(name[..length], out var shortOption))
                    return (shortOption, token[length..], false, null);
            }
            if (name.StartsWith("--", StringComparison.Ordinal) && await FlagAsync(_options.AllowAbbrev, context, span, asynchronous).ConfigureAwait(false))
            {
                ArgumentSpec? match = null;
                foreach (var pair in _optionalArgumentsByName)
                {
                    context.CheckExecution(span);
                    if (!pair.Key.StartsWith("--", StringComparison.Ordinal) || !pair.Key.StartsWith(name, StringComparison.Ordinal)) continue;
                    if (match is not null && !ReferenceEquals(match, pair.Value)) return (null, null, false, "ambiguous option: " + name);
                    match = pair.Value;
                }
                return (match, value, equals > 0, null);
            }
            return default;
        }
        private static bool LooksLikeNegativeNumber(string token, ExecutionContext context, LythonSourceSpan span)
        {
            if (token.Length < 2 || token[0] != '-') return false;
            var dot = false; var digits = 0; var scanned = 0;
            foreach (var rune in token.AsSpan(1).EnumerateRunes())
            {
                if ((scanned++ & 1023) == 0) context.CheckExecution(span);
                if (rune.Value == '.' && !dot) { dot = true; digits = 0; continue; }
                if (Rune.GetUnicodeCategory(rune) != System.Globalization.UnicodeCategory.DecimalDigitNumber) return false;
                digits++;
            }
            return digits != 0;
        }
        private static PyString RequireString(string name, object value, LythonSourceSpan span)
        {
            return RequireArgparseStringValue(value, name, "argparse.ArgumentParser.add_argument", span);
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
    }
}

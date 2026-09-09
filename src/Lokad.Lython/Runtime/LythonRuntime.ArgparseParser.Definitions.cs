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
        private static ICallable? ValidateConverter(object value, LythonSourceSpan span)
        {
            if (ReferenceEquals(value, PyNone.Instance))
            {
                return null;
            }
            if (value is ICallable callable)
            {
                return callable;
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
                : ArgumentNargs.Default;
            var required = keyword.TryGetValue("required", out var requiredValue) && IsTruthy(requiredValue);
            var defaultValue = keyword.TryGetValue("default", out var maybeDefault)
                ? maybeDefault
                : nargs.Kind == ArgumentNargsKind.ZeroOrMore
                    ? new PyList([], context.MemoryGovernor, span)
                    : DefaultForAction(action, context, span);
            object[]? choices = keyword.TryGetValue("choices", out var choicesValue)
                ? [.. ToSequence(choicesValue, span, context)]
                : null;
            var converter = keyword.TryGetValue("type", out var typeValue) ? ValidateConverter(typeValue, span) : null;
            var constValue = (action == ArgumentAction.StoreConst || nargs.Kind == ArgumentNargsKind.Optional) && keyword.TryGetValue("const", out var constant)
                ? constant
                : PyNone.Instance;
            var suppressHelp = keyword.TryGetValue("help", out var helpValue) && IsSuppress(helpValue);
            var helpText = keyword.TryGetValue("help", out helpValue)
                ? suppressHelp
                    ? null
                    : OptionalArgparseString(helpValue, "help", "argparse.ArgumentParser.add_argument", span)
                : null;
            var metavar = keyword.TryGetValue("metavar", out var metavarValue)
                ? OptionalArgparseString(metavarValue, "metavar", "argparse.ArgumentParser.add_argument", span)
                : null;
            var versionText = keyword.TryGetValue("version", out var versionValue)
                ? OptionalArgparseString(versionValue, "version", "argparse.ArgumentParser.add_argument", span)
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
                if (arguments[i].IsKeyword && !supportedNames.Contains(arguments[i].KeywordName))
                {
                    throw new LythonRuntimeException("TypeError", $"{signature}(...) got an unexpected keyword argument '{arguments[i].KeywordName}'.", span);
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
                ArgumentSpec? match = null;
                foreach (var (name, candidate) in _optionalArgumentsByName)
                {
                    if (name.StartsWith("--", StringComparison.Ordinal) &&
                        name.StartsWith(optionToken, StringComparison.Ordinal))
                    {
                        if (match is not null && !ReferenceEquals(match, candidate))
                        {
                            error = $"ambiguous option: {optionToken}";
                            return null;
                        }
                        match = candidate;
                    }
                }
                return match;
            }
            return null;
        }
        private ArgumentSpec? FindExactOptionalArgument(string token)
            => _optionalArgumentsByName.GetValueOrDefault(token);
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

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
            foreach (var argument in arguments)
            {
                if (argument.IsPositional)
                {
                    throw new LythonRuntimeException("TypeError", "argparse.ArgumentParser.set_defaults(...) accepts keyword arguments only.", span);
                }
                if (!_defaults.ContainsKey(argument.KeywordName))
                {
                    context.MemoryGovernor.Reserve(DefaultsSlotBytes, span);
                    context.MemoryGovernor.Commit(DefaultsSlotBytes);
                }
                _defaults[argument.KeywordName] = argument.Value;
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
            return _argumentsByDestination.TryGetValue(destination, out var argument)
                ? argument.DefaultValue
                : PyNone.Instance;
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
                : new LythonRuntimeException(ModuleException("argparse", "ArgumentError"), message, span);
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
        private static List<string> ToStringList(object value, string message, LythonSourceSpan span, ExecutionContext context)
        {
            if (PyStringOps.TryAsString(value, out _))
            {
                throw new LythonRuntimeException("TypeError", message, span);
            }
            var result = new List<string>();
            foreach (var item in ToSequence(value, span, context))
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
            object value = ArgparseUnspecifiedValue.Instance;
            var assigned = false;
            foreach (var argument in arguments)
            {
                if (argument.IsPositional)
                {
                    if (assigned)
                    {
                        throw new LythonRuntimeException("TypeError", $"{methodName}(...) got multiple values for argument '{parameterName}'.", span);
                    }
                    value = argument.Value;
                    assigned = true;
                    continue;
                }
                if (argument.KeywordName != parameterName)
                {
                    throw new LythonRuntimeException("TypeError", $"{methodName}(...) got an unexpected keyword argument '{argument.KeywordName}'.", span);
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
            return RequireArgparseStringValue(value, parameterName, methodName, span);
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
                if (argument.IsPositional)
                {
                    if (assigned)
                    {
                        throw new LythonRuntimeException("TypeError", $"{methodName}([file]) expects zero or one argument.", span);
                    }
                    target = ReferenceEquals(argument.Value, PyNone.Instance) ? null : argument.Value;
                    assigned = true;
                    continue;
                }
                if (argument.KeywordName != "file")
                {
                    throw new LythonRuntimeException("TypeError", $"{methodName}(...) got an unexpected keyword argument '{argument.KeywordName}'.", span);
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
                if (argument.IsPositional)
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
                switch (argument.KeywordName)
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
                        throw new LythonRuntimeException("TypeError", $"argparse.ArgumentParser.exit(...) got an unexpected keyword argument '{argument.KeywordName}'.", span);
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
}

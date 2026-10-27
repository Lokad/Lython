using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ArgumentParserObject
    {
        private object FormatUsage(CallArgumentValue[] args, LythonSourceSpan span, ExecutionContext context)
            => FormatCoreAsync(args, span, context, false, false).GetAwaiter().GetResult();
        private ValueTask<object> FormatUsageAsync(CallArgumentValue[] args, LythonSourceSpan span, ExecutionContext context)
            => FormatCoreAsync(args, span, context, false, true);
        private object FormatHelp(CallArgumentValue[] args, LythonSourceSpan span, ExecutionContext context)
            => FormatCoreAsync(args, span, context, true, false).GetAwaiter().GetResult();
        private ValueTask<object> FormatHelpAsync(CallArgumentValue[] args, LythonSourceSpan span, ExecutionContext context)
            => FormatCoreAsync(args, span, context, true, true);
        private async ValueTask<object> FormatCoreAsync(CallArgumentValue[] args, LythonSourceSpan span, ExecutionContext context, bool help, bool asynchronous)
        {
            RequireNoArguments(args, "argparse.ArgumentParser.format_" + (help ? "help" : "usage"), span);
            return help ? await FormatHelpTextAsync(context, span, asynchronous).ConfigureAwait(false)
                        : await FormatUsageTextAsync(context, span, asynchronous).ConfigureAwait(false);
        }
        private object PrintUsage(CallArgumentValue[] args, LythonSourceSpan span, ExecutionContext context)
            => PrintCoreAsync(args, span, context, false, false).GetAwaiter().GetResult();
        private ValueTask<object> PrintUsageAsync(CallArgumentValue[] args, LythonSourceSpan span, ExecutionContext context)
            => PrintCoreAsync(args, span, context, false, true);
        private object PrintHelp(CallArgumentValue[] args, LythonSourceSpan span, ExecutionContext context)
            => PrintCoreAsync(args, span, context, true, false).GetAwaiter().GetResult();
        private ValueTask<object> PrintHelpAsync(CallArgumentValue[] args, LythonSourceSpan span, ExecutionContext context)
            => PrintCoreAsync(args, span, context, true, true);
        private async ValueTask<object> PrintCoreAsync(CallArgumentValue[] args, LythonSourceSpan span, ExecutionContext context, bool help, bool asynchronous)
        {
            var target = ResolveOptionalFile(args, "argparse.ArgumentParser.print_" + (help ? "help" : "usage"), span) ?? context.State.Stdout;
            var text = help ? await FormatHelpTextAsync(context, span, asynchronous).ConfigureAwait(false)
                            : await FormatUsageTextAsync(context, span, asynchronous).ConfigureAwait(false);
            await WriteAsync(target, text, span, context, asynchronous).ConfigureAwait(false);
            return PyNone.Instance;
        }
        private object Error(CallArgumentValue[] args, LythonSourceSpan span, ExecutionContext context)
            => ErrorAsyncCore(args, span, context, false).GetAwaiter().GetResult();
        private ValueTask<object> ErrorAsync(CallArgumentValue[] args, LythonSourceSpan span, ExecutionContext context)
            => ErrorAsyncCore(args, span, context, true);
        private async ValueTask<object> ErrorAsyncCore(CallArgumentValue[] args, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var message = RequireSingleStringArgument(args, "argparse.ArgumentParser.error", "message", span);
            await ReportErrorAsync(message, span, context, asynchronous).ConfigureAwait(false);
            throw CreateSystemExit(string.Empty, span, new BigInteger(2));
        }
        private object Exit(CallArgumentValue[] args, LythonSourceSpan span, ExecutionContext context)
            => ExitCoreAsync(args, span, context, false).GetAwaiter().GetResult();
        private ValueTask<object> ExitAsync(CallArgumentValue[] args, LythonSourceSpan span, ExecutionContext context)
            => ExitCoreAsync(args, span, context, true);
        private async ValueTask<object> ExitCoreAsync(CallArgumentValue[] args, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            object status = BigInteger.Zero, message = PyNone.Instance;
            BindExitArguments(args, ref status, ref message, span);
            if (await FlagAsync(message, context, span, asynchronous).ConfigureAwait(false))
                await WriteAsync(context.State.Stderr, message, span, context, asynchronous).ConfigureAwait(false);
            throw CreateSystemExit(string.Empty, span, status);
        }
        private object SetDefaults(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            foreach (var argument in arguments)
            {
                if (argument.IsPositional) throw RuntimeErrors.Type("argparse.ArgumentParser.set_defaults(...) accepts keyword arguments only.", span);
                if (!_defaults.ContainsKey(argument.KeywordName))
                { context.MemoryGovernor.Reserve(DefaultsSlotBytes, span); context.MemoryGovernor.Commit(DefaultsSlotBytes); }
                _defaults[argument.KeywordName] = argument.Value;
                foreach (var spec in _arguments) if (spec.Destination == argument.KeywordName) spec.DefaultValue = argument.Value;
            }
            return PyNone.Instance;
        }
        private object GetDefault(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var name = RequireSingleStringArgument(arguments, "argparse.ArgumentParser.get_default", "dest", span);
            using var scratch = context.MemoryGovernor.ReserveTemporary(32L + 2L * name.GetUtf16CodeUnitCount(), span);
            var destination = name.AsString();
            var action = _arguments.FirstOrDefault(a => a.Destination == destination);
            return action is not null ? action.DefaultValue : _defaults.TryGetValue(destination, out var value) ? value : PyNone.Instance;
        }
        private object AddSubparsers(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
            => throw new LythonRuntimeException("NotImplementedError", "argparse.ArgumentParser.add_subparsers(...) is not supported by Lython.", span);

        private void EnsureFormatter(LythonSourceSpan span)
        {
            if (_options.FormatterClass is ArgparseFormatterClass) return;
            if (!IsCallable(_options.FormatterClass)) throw RuntimeErrors.Value("formatter_class must be a supported formatter", span);
            throw new LythonRuntimeException("NotImplementedError", "Custom argparse formatter factories are unsupported.", span);
        }
        private async ValueTask<PyString> FormatUsageTextAsync(ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            EnsureFormatter(span);
            var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
            try
            {
                await AppendUsageAsync(builder, context, span, asynchronous).ConfigureAwait(false);
                return builder.ToPyStringAndRelease();
            }
            finally { builder.Release(); }
        }
        private async ValueTask AppendUsageAsync(GovernedByteBuilder builder, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            if (_options.Usage is { } usage)
            {
                if (!usage.Utf8Bytes.Span.StartsWith("usage:"u8)) builder.AppendAscii("usage: ");
                AppendFormatted(builder, usage, null, context, span);
                if (usage.Utf8Bytes.IsEmpty || usage.Utf8Bytes.Span[^1] != '\n') builder.Append((byte)'\n');
                return;
            }
            builder.AppendAscii("usage: "); builder.Append(_options.Prog);
            for (int i = 0, count = _arguments.Count; i < count; i++)
            {
                context.CheckExecution(span);
                var argument = _arguments[i];
                if (argument.SuppressHelp) continue;
                builder.Append((byte)' ');
                if (argument.IsPositional)
                {
                    switch (argument.Nargs.Kind)
                    {
                        case ArgumentNargsKind.Optional: builder.Append((byte)'['); AppendMetavar(builder, argument, context, span); builder.Append((byte)']'); break;
                        case ArgumentNargsKind.ZeroOrMore: builder.Append((byte)'['); AppendMetavar(builder, argument, context, span); builder.AppendAscii(" ...]"); break;
                        case ArgumentNargsKind.OneOrMore: AppendMetavar(builder, argument, context, span); builder.AppendAscii(" ["); AppendMetavar(builder, argument, context, span); builder.AppendAscii(" ...]"); break;
                        case ArgumentNargsKind.Fixed:
                            for (var n = 0; n < argument.Nargs.Count; n++)
                            { context.CheckExecution(span); if (n != 0) builder.Append((byte)' '); AppendMetavar(builder, argument, context, span); }
                            break;
                        default: AppendMetavar(builder, argument, context, span); break;
                    }
                }
                else
                {
                    var required = await FlagAsync(argument.Required, context, span, asynchronous).ConfigureAwait(false);
                    if (!required) builder.Append((byte)'[');
                    builder.AppendString(argument.OptionNames.FirstOrDefault(n => n.StartsWith("--", StringComparison.Ordinal)) ?? argument.OptionNames.FirstOrDefault() ?? argument.Destination);
                    if (!IsNoValueAction(argument.Action)) { builder.Append((byte)' '); AppendMetavar(builder, argument, context, span); }
                    if (!required) builder.Append((byte)']');
                }
            }
            builder.Append((byte)'\n');
        }
        private async ValueTask<PyString> FormatHelpTextAsync(ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            EnsureFormatter(span);
            var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
            try
            {
                await AppendUsageAsync(builder, context, span, asynchronous).ConfigureAwait(false);
                if (_options.Description is { } description) { builder.Append((byte)'\n'); builder.Append(description); builder.Append((byte)'\n'); }
                AppendHelpGroup(builder, true, context, span);
                AppendHelpGroup(builder, false, context, span);
                if (_options.Epilog is { } epilog) { builder.Append((byte)'\n'); builder.Append(epilog); builder.Append((byte)'\n'); }
                return builder.ToPyStringAndRelease();
            }
            finally { builder.Release(); }
        }
        private void AppendHelpGroup(GovernedByteBuilder builder, bool positional, ExecutionContext context, LythonSourceSpan span)
        {
            var started = false;
            for (int i = 0, count = _arguments.Count; i < count; i++)
            {
                context.CheckExecution(span);
                var argument = _arguments[i];
                if (argument.IsPositional != positional || argument.SuppressHelp) continue;
                if (!started) { builder.AppendAscii(positional ? "\npositional arguments:\n" : "\noptions:\n"); started = true; }
                builder.AppendAscii("  ");
                if (positional) AppendMetavar(builder, argument, context, span);
                else
                {
                    for (var n = 0; n < argument.OptionNames.Count; n++)
                    { if (n != 0) builder.AppendAscii(", "); builder.AppendString(argument.OptionNames[n]); }
                    if (!IsNoValueAction(argument.Action)) { builder.Append((byte)' '); AppendMetavar(builder, argument, context, span); }
                }
                if (argument.HelpText is { } help) { builder.AppendAscii("  "); AppendFormatted(builder, help, argument, context, span); }
                if (_options.FormatterClass is ArgparseFormatterClass { Name: "ArgumentDefaultsHelpFormatter" } && !IsSuppress(argument.DefaultValue) && argument.DefaultValue is not PyNone)
                { builder.AppendAscii(" (default: "); builder.Append(PyRendering.ToPythonPyString(argument.DefaultValue, new(context))); builder.Append((byte)')'); }
                builder.Append((byte)'\n');
            }
        }
        private static void AppendMetavar(GovernedByteBuilder builder, ArgumentSpec argument, ExecutionContext context, LythonSourceSpan span)
        {
            if (argument.Metavar is { } name) { builder.Append(name); return; }
            using var scratch = context.MemoryGovernor.ReserveTemporary(32L + 2L * argument.Destination.Length, span);
            builder.AppendString(argument.Destination.ToUpperInvariant());
        }
        private void AppendFormatted(GovernedByteBuilder builder, PyString text, ArgumentSpec? argument, ExecutionContext context, LythonSourceSpan span)
        {
            var source = text.Utf8Bytes.Span;
            var start = 0;
            for (var i = 0; i < source.Length; i++)
            {
                if ((i & 1023) == 0) context.CheckExecution(span);
                var prog = source[i..].StartsWith("%(prog)s"u8);
                var fallback = argument is not null && source[i..].StartsWith("%(default)s"u8);
                if (!prog && !fallback) continue;
                builder.Append(source[start..i]);
                builder.Append(prog ? _options.Prog : PyRendering.ToPythonPyString(argument!.DefaultValue, new(context)));
                i += prog ? 7 : 10;
                start = i + 1;
            }
            builder.Append(source[start..]);
        }
        private PyString FormatVersionText(ArgumentSpec argument, ExecutionContext context, LythonSourceSpan span)
        {
            var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
            var text = argument.VersionText ?? PyString.Empty;
            try
            {
                AppendFormatted(builder, text, argument, context, span);
                if (text.Utf8Bytes.IsEmpty || text.Utf8Bytes.Span[^1] != '\n') builder.Append((byte)'\n');
                return builder.ToPyStringAndRelease();
            }
            finally { builder.Release(); }
        }
        private async ValueTask ReportErrorAsync(PyString message, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var usage = await FormatUsageTextAsync(context, span, asynchronous).ConfigureAwait(false);
            await WriteAsync(context.State.Stderr, usage, span, context, asynchronous).ConfigureAwait(false);
            var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
            PyString text;
            try { builder.Append(_options.Prog); builder.AppendAscii(": error: "); builder.Append(message); builder.Append((byte)'\n'); text = builder.ToPyStringAndRelease(); }
            finally { builder.Release(); }
            await WriteAsync(context.State.Stderr, text, span, context, asynchronous).ConfigureAwait(false);
        }
        private static ValueTask WriteAsync(object target, object text, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            if (asynchronous) return PyTextStream.WriteAsync(target, text, span, context);
            PyTextStream.Write(target, text, span, context);
            return ValueTask.CompletedTask;
        }
        private static ValueTask<bool> FlagAsync(object value, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
            => asynchronous ? IsTruthyAsync(value, context, span) : ValueTask.FromResult(IsTruthy(value, context, span));
        private static LythonRuntimeException CreateParseFailure(string message, LythonSourceSpan span)
            => new(ModuleException("argparse", "ArgumentError"), message, span);
        private static LythonRuntimeException CreateSystemExit(string message, LythonSourceSpan span, object status)
            => new("SystemExit", message, span, innerException: null, payload: status);
        private static bool IsNoValueAction(ArgumentAction action)
            => action is ArgumentAction.StoreTrue or ArgumentAction.StoreFalse or ArgumentAction.StoreConst or ArgumentAction.Count or ArgumentAction.Help or ArgumentAction.Version;
        private static bool ProducesListValue(ArgumentSpec spec)
            => spec.Nargs.Kind is ArgumentNargsKind.ZeroOrMore or ArgumentNargsKind.OneOrMore or ArgumentNargsKind.Fixed;
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
        private static void BindExitArguments(CallArgumentValue[] arguments, ref object status, ref object message, LythonSourceSpan span)
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
                        status = argument.Value;
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
                        status = argument.Value;
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
        private static bool IsSuppress(object? value) => ReferenceEquals(value, ArgparseModule.Suppress);
    }
}

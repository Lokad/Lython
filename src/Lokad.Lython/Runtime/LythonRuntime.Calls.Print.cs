using Lokad.Lython.Runtime.Calls;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class PrintCallable : ICallable, IPyDynamicAttributes
    {
        public string Name => "print";

        // print exposes CPython-style __name__/__module__ like the other
        // singleton builtins: the fixed name and the shared builtins label.
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            if (name is "__name__" or "__qualname__")
            {
                value = PyString.FromString(Name);
                return true;
            }

            if (name == "__module__")
            {
                value = ExceptionTypeValue.SharedModuleLabel("builtins");
                return true;
            }

            value = PyNone.Instance;
            return false;
        }

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var bound = BindArguments(arguments, span);
            var hasWrittenValue = false;
            foreach (var argument in arguments)
            {
                if (!argument.IsPositional)
                {
                    continue;
                }

                if (hasWrittenValue)
                {
                    AppendOutput(bound.Separator, context, bound.OutputTarget, span);
                }

                AppendOutput(ToInterpolatedPyString(argument.Value, context), context, bound.OutputTarget, span);
                hasWrittenValue = true;
            }

            AppendOutput(bound.Ending, context, bound.OutputTarget, span);
            if (bound.Flush)
            {
                FlushOutput(context, bound.OutputTarget, span);
            }

            return PyNone.Instance;

            static void FlushOutput(ExecutionContext context, PrintOutputTarget outputTarget, LythonSourceSpan span)
            {
                switch (outputTarget)
                {
                    case StandardOutputTarget:
                        _ = context.State.Stdout.Flush(span);
                        break;
                    case TextFileOutputTarget file:
                        _ = file.Handle.Flush();
                        break;
                    case HostOutputTarget host:
                        _ = host.Handle.Flush(span);
                        break;
                    case PopenInputOutputTarget process:
                        process.Handle.FlushValue(span);
                        break;
                    default:
                        throw new InvalidOperationException("Unsupported print output target.");
                }
            }
        }

        public async ValueTask<object> InvokeAsync(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            var bound = BindArguments(arguments, span);
            var hasWrittenValue = false;
            foreach (var argument in arguments)
            {
                if (!argument.IsPositional)
                {
                    continue;
                }

                if (hasWrittenValue)
                {
                    await AppendOutputAsync(bound.Separator, context, bound.OutputTarget, span).ConfigureAwait(false);
                }

                await AppendOutputAsync(ToInterpolatedPyString(argument.Value, context), context, bound.OutputTarget, span).ConfigureAwait(false);
                hasWrittenValue = true;
            }

            await AppendOutputAsync(bound.Ending, context, bound.OutputTarget, span).ConfigureAwait(false);
            if (bound.Flush)
            {
                await FlushOutputAsync(context, bound.OutputTarget, span).ConfigureAwait(false);
            }

            return PyNone.Instance;
        }

        private static BoundPrintArguments BindArguments(CallArgumentValue[] arguments, LythonSourceSpan span)
        {
            var separator = DefaultPrintSeparator;
            var ending = DefaultPrintEnding;
            PrintOutputTarget outputTarget = StandardOutputTarget.Instance;
            var flush = false;
            var seenSeparator = false;
            var seenEnding = false;
            var seenFile = false;
            var seenFlush = false;

            foreach (var argument in arguments)
            {
                if (argument.IsPositional)
                {
                    continue;
                }

                switch (argument.KeywordName)
                {
                    case "sep":
                        if (seenSeparator)
                        {
                            throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "print", "sep", span);
                        }

                        separator = ReferenceEquals(argument.Value, PyNone.Instance)
                            ? DefaultPrintSeparator
                            : PyStringOps.TryAsString(argument.Value, out var separatorValue)
                                ? separatorValue
                                : throw new LythonRuntimeException("TypeError", "print(..., sep=...) expects a string or None.", span);
                        seenSeparator = true;
                        break;

                    case "end":
                        if (seenEnding)
                        {
                            throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "print", "end", span);
                        }

                        ending = ReferenceEquals(argument.Value, PyNone.Instance)
                            ? DefaultPrintEnding
                            : PyStringOps.TryAsString(argument.Value, out var endingValue)
                                ? endingValue
                                : throw new LythonRuntimeException("TypeError", "print(..., end=...) expects a string or None.", span);
                        seenEnding = true;
                        break;

                    case "file":
                        if (seenFile)
                        {
                            throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "print", "file", span);
                        }

                        outputTarget = argument.Value switch
                        {
                            PyNone => StandardOutputTarget.Instance,
                            ExecutionContext.TextFileHandle handle => new TextFileOutputTarget(handle),
                            HostTextOutputHandle handle => new HostOutputTarget(handle),
                            PopenInputStream handle => new PopenInputOutputTarget(handle),
                            _ => throw new LythonRuntimeException(
                                "TypeError",
                                "print(..., file=...) expects a writable Lython text stream, writable file handle, or None.",
                                span)
                        };
                        seenFile = true;
                        break;

                    case "flush":
                        if (seenFlush)
                        {
                            throw CallErrors.MultipleValues(PythonCallableKind.Builtin, "print", "flush", span);
                        }

                        flush = IsTruthy(argument.Value);
                        seenFlush = true;
                        break;

                    default:
                        throw CallErrors.UnexpectedKeyword(PythonCallableKind.Builtin, "print", argument.KeywordName, span);
                }
            }

            return new BoundPrintArguments(separator, ending, outputTarget, flush);
        }

        private static void AppendOutput(PyString value, ExecutionContext context, PrintOutputTarget outputTarget, LythonSourceSpan span)
        {
            switch (outputTarget)
            {
                case StandardOutputTarget:
                    _ = context.State.Stdout.Write(value, span);
                    break;
                case TextFileOutputTarget file:
                    _ = file.Handle.Write(value);
                    break;
                case HostOutputTarget host:
                    _ = host.Handle.Write(value, span);
                    break;
                case PopenInputOutputTarget process:
                    _ = process.Handle.Write(value, span);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported print output target.");
            }
        }


        private static async ValueTask AppendOutputAsync(PyString value, ExecutionContext context, PrintOutputTarget outputTarget, LythonSourceSpan span)
        {
            switch (outputTarget)
            {
                case StandardOutputTarget:
                    _ = await context.State.Stdout.WriteAsync(value, span).ConfigureAwait(false);
                    break;
                case TextFileOutputTarget file:
                    _ = file.Handle.Write(value);
                    break;
                case HostOutputTarget host:
                    _ = await host.Handle.WriteAsync(value, span).ConfigureAwait(false);
                    break;
                case PopenInputOutputTarget process:
                    _ = process.Handle.Write(value, span);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported print output target.");
            }
        }

        private static async ValueTask FlushOutputAsync(ExecutionContext context, PrintOutputTarget outputTarget, LythonSourceSpan span)
        {
            switch (outputTarget)
            {
                case StandardOutputTarget:
                    _ = await context.State.Stdout.FlushAsync(span).ConfigureAwait(false);
                    break;
                case TextFileOutputTarget file:
                    _ = await file.Handle.FlushAsync().ConfigureAwait(false);
                    break;
                case HostOutputTarget host:
                    _ = await host.Handle.FlushAsync(span).ConfigureAwait(false);
                    break;
                case PopenInputOutputTarget process:
                    process.Handle.FlushValue(span);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported print output target.");
            }
        }

        private readonly record struct BoundPrintArguments(
            PyString Separator,
            PyString Ending,
            PrintOutputTarget OutputTarget,
            bool Flush);

        private abstract record PrintOutputTarget
        {
            private protected PrintOutputTarget() { }
        }

        private sealed record StandardOutputTarget : PrintOutputTarget
        {
            public static StandardOutputTarget Instance { get; } = new();
        }

        private sealed record TextFileOutputTarget(ExecutionContext.TextFileHandle Handle) : PrintOutputTarget;

        private sealed record HostOutputTarget(HostTextOutputHandle Handle) : PrintOutputTarget;

        private sealed record PopenInputOutputTarget(PopenInputStream Handle) : PrintOutputTarget;
    }
}

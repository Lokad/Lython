using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static async ValueTask<PyString?> RenderSubprocessErrorAsync(
        PyException exception, PyRenderingContext rendering, bool asynchronous)
    {
        if (!exception.IsSubprocessError) return null;
        var context = rendering.Context;
        var span = PopenSyntheticSpan;
        object Field(string name) => exception.CustomDict is not null &&
            exception.CustomDict.TryGetValue(PyString.FromString(name), out var value) ? value :
            throw new LythonRuntimeException("AttributeError", $"'{exception.TypeName}' object has no attribute '{name}'", span);
        ValueTask<PyString> Text(object value) => asynchronous
            ? PyRendering.ToInterpolatedPyStringAsync(value, rendering)
            : ValueTask.FromResult(PyRendering.ToInterpolatedPyString(value, rendering));
        ValueTask<bool> Truth(object value) => asynchronous ? IsTruthyAsync(value, context, span)
            : ValueTask.FromResult(IsTruthy(value, context, span));

        var negative = false;
        object number;
        if (exception.TypeName == "CalledProcessError")
        {
            if (await Truth(Field("returncode")).ConfigureAwait(false))
            {
                var code = Field("returncode");
                var comparison = asynchronous
                    ? await EvaluateBinaryOperatorAsync(BinaryOperatorSyntax.Less, code, BigInteger.Zero, context, span).ConfigureAwait(false)
                    : EvaluateBinaryOperator(BinaryOperatorSyntax.Less, code, BigInteger.Zero, context, span);
                negative = await Truth(comparison).ConfigureAwait(false);
            }
            number = Field("returncode");
            if (negative) number = asynchronous
                ? await EvaluateUnaryOperatorAsync(UnaryOperatorSyntax.Minus, number, context, span).ConfigureAwait(false)
                : EvaluateUnaryOperator(UnaryOperatorSyntax.Minus, number, context, span);
        }
        else number = Field("timeout");

        // Capture both formatting operands before callbacks can mutate fields.
        var command = Field("cmd");
        var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
        try
        {
            builder.AppendAscii("Command '");
            builder.Append(await Text(command).ConfigureAwait(false));
            if (exception.TypeName == "TimeoutExpired")
            {
                builder.AppendAscii("' timed out after ");
                builder.Append(await Text(number).ConfigureAwait(false));
                builder.AppendAscii(" seconds");
            }
            else
            {
                string? signal = null;
                if (negative)
                {
                    var knownNumber = PyNumberOps.TryAsInteger(number, out var signalNumber);
                    if (!knownNumber && number is double floating && double.IsFinite(floating) && floating == Math.Truncate(floating))
                    { signalNumber = new BigInteger(floating); knownNumber = true; }
                    signal = knownNumber && signalNumber >= 0 && signalNumber <= int.MaxValue ? SubprocessSignalName((int)signalNumber) : null;
                }
                builder.AppendAscii(!negative ? "' returned non-zero exit status " : signal is null
                    ? "' died with unknown signal " : "' died with <Signals.");
                if (signal is not null) { builder.AppendAscii(signal); builder.AppendAscii(": "); }
                if (number is PyInstance instance) number = asynchronous
                    ? await ConvertInstanceToOwnedIntegerAsync(instance, context, span).ConfigureAwait(false)
                    : OwnFreshInteger(ConvertInstanceToInteger(instance, context, span), context.MemoryGovernor, span);
                builder.Append(FormatPercentString(PyString.FromString("%d"), number, context, span));
                builder.AppendAscii(signal is null ? "." : ">.");
            }
            return builder.ToPyStringAndRelease();
        }
        finally { builder.Release(); }
    }

    private static string? SubprocessSignalName(int signal) => signal switch
    {
        2 => "SIGINT", 4 => "SIGILL", 6 when !OperatingSystem.IsWindows() => "SIGABRT", 8 => "SIGFPE", 11 => "SIGSEGV", 15 => "SIGTERM",
        21 when OperatingSystem.IsWindows() => "SIGBREAK", 22 when OperatingSystem.IsWindows() => "SIGABRT",
        0 when OperatingSystem.IsWindows() => "CTRL_C_EVENT", 1 when OperatingSystem.IsWindows() => "CTRL_BREAK_EVENT",
        _ when OperatingSystem.IsWindows() => null,
        1 => "SIGHUP", 3 => "SIGQUIT", 5 => "SIGTRAP", 7 => "SIGBUS", 9 => "SIGKILL", 10 => "SIGUSR1",
        12 => "SIGUSR2", 13 => "SIGPIPE", 14 => "SIGALRM", 16 => "SIGSTKFLT", 17 => "SIGCHLD",
        18 => "SIGCONT", 19 => "SIGSTOP", 20 => "SIGTSTP", 21 => "SIGTTIN", 22 => "SIGTTOU",
        23 => "SIGURG", 24 => "SIGXCPU", 25 => "SIGXFSZ", 26 => "SIGVTALRM", 27 => "SIGPROF",
        28 => "SIGWINCH", 29 => "SIGIO", 30 => "SIGPWR", 31 => "SIGSYS", 34 => "SIGRTMIN", 64 => "SIGRTMAX",
        _ => null
    };
}

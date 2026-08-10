using System.Collections;
using System.Globalization;
using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class ExecutionContext
    {
        public long ReadHostMonotonicNanoseconds(LythonSourceSpan? span)
        {
            var timing = RequireHostTiming(span);
            RegisterHostCall(span);
            var value = HostOperation.Invoke(() => timing.MonotonicNanoseconds, "time.monotonic", span);
            if (value < 0)
            {
                throw RuntimeErrors.Runtime("host timing capability returned a negative monotonic reading.", span);
            }

            return value;
        }

        public long ReadHostMonotonicResolutionNanoseconds(LythonSourceSpan? span)
        {
            var timing = RequireHostTiming(span);
            RegisterHostCall(span);
            var value = HostOperation.Invoke(() => timing.MonotonicResolutionNanoseconds, "time.get_clock_info", span);
            if (value <= 0)
            {
                throw RuntimeErrors.Runtime("host timing capability returned a non-positive monotonic resolution.", span);
            }

            return value;
        }

        public void DelayHost(TimeSpan duration, LythonSourceSpan? span)
        {
            var timing = RequireHostTiming(span);
            RegisterHostCall(span);
            HostOperation.Await(timing, () => timing.DelayAsync(duration, Limits.CancellationToken), "time.sleep", span);
        }

        public ValueTask DelayHostAsync(TimeSpan duration, LythonSourceSpan? span)
        {
            var timing = RequireHostTiming(span);
            RegisterHostCall(span);
            return HostOperation.AwaitAsync(() => timing.DelayAsync(duration, Limits.CancellationToken), "time.sleep", span);
        }

        private ILythonTiming RequireHostTiming(LythonSourceSpan? span)
            => Host.Timing ?? throw RuntimeErrors.Runtime(
                "host timing/sleep capability is not available in this host.",
                span);

    }
}

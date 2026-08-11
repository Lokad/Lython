using System.Collections;
using System.Globalization;
using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class TimeModule : PyModule, IPyContextualDynamicAttributes
    {
        public static readonly TimeModule Instance = new();

        private static readonly string[] Names =
        [
            "time",
            "time_ns",
            "monotonic",
            "monotonic_ns",
            "perf_counter",
            "perf_counter_ns",
            "sleep",
            "get_clock_info",
            "process_time",
            "process_time_ns",
            "thread_time",
            "thread_time_ns",
            "clock_gettime",
            "clock_gettime_ns",
            "clock_getres",
            "clock_settime",
            "clock_settime_ns",
            "pthread_getcpuclockid",
            "gmtime",
            "localtime",
            "ctime",
            "mktime",
            "asctime",
            "strftime",
            "strptime",
            "struct_time",
            "timezone",
            "altzone",
            "daylight",
            "tzname",
            "tzset",
        ];

        private TimeModule() : base("time")
        {
        }

        public override IReadOnlyList<string> ExportedNames => Names;

        public override IReadOnlyList<string> MemberNames => Names;

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "time" => BuiltinCallable.Create(LythonKnownCallableSignatures.TimeTime, CurrentTime),
                "time_ns" => BuiltinCallable.Create(LythonKnownCallableSignatures.TimeTimeNs, CurrentTimeNanoseconds),
                "monotonic" => BuiltinCallable.Create(LythonKnownCallableSignatures.TimeMonotonic, Monotonic),
                "monotonic_ns" => BuiltinCallable.Create(LythonKnownCallableSignatures.TimeMonotonicNs, MonotonicNanoseconds),
                "perf_counter" => BuiltinCallable.Create(LythonKnownCallableSignatures.TimePerfCounter, Monotonic),
                "perf_counter_ns" => BuiltinCallable.Create(LythonKnownCallableSignatures.TimePerfCounterNs, MonotonicNanoseconds),
                "sleep" => BuiltinCallable.Create(LythonKnownCallableSignatures.TimeSleep, Sleep, SleepAsync),
                "get_clock_info" => BuiltinCallable.Create(LythonKnownCallableSignatures.TimeGetClockInfo, GetClockInfo),
                "process_time" => Unsupported(LythonKnownCallableSignatures.TimeProcessTime, "process CPU clocks"),
                "process_time_ns" => Unsupported(LythonKnownCallableSignatures.TimeProcessTimeNs, "process CPU clocks"),
                "thread_time" => Unsupported(LythonKnownCallableSignatures.TimeThreadTime, "thread CPU clocks"),
                "thread_time_ns" => Unsupported(LythonKnownCallableSignatures.TimeThreadTimeNs, "thread CPU clocks"),
                "clock_gettime" => Unsupported(LythonKnownCallableSignatures.TimeClockGetTime, "platform clock IDs"),
                "clock_gettime_ns" => Unsupported(LythonKnownCallableSignatures.TimeClockGetTimeNs, "platform clock IDs"),
                "clock_getres" => Unsupported(LythonKnownCallableSignatures.TimeClockGetRes, "platform clock IDs"),
                "clock_settime" => Unsupported(LythonKnownCallableSignatures.TimeClockSetTime, "clock mutation"),
                "clock_settime_ns" => Unsupported(LythonKnownCallableSignatures.TimeClockSetTimeNs, "clock mutation"),
                "pthread_getcpuclockid" => Unsupported(LythonKnownCallableSignatures.TimePthreadGetCpuClockId, "platform thread clocks"),
                "gmtime" => BuiltinCallable.Create(LythonKnownCallableSignatures.TimeGmtime, Gmtime),
                "localtime" => BuiltinCallable.Create(LythonKnownCallableSignatures.TimeLocaltime, Localtime),
                "ctime" => BuiltinCallable.Create(LythonKnownCallableSignatures.TimeCtime, Ctime),
                "mktime" => BuiltinCallable.Create(LythonKnownCallableSignatures.TimeMktime, Mktime),
                "asctime" => BuiltinCallable.Create(LythonKnownCallableSignatures.TimeAsctime, Asctime),
                "strftime" => BuiltinCallable.Create(LythonKnownCallableSignatures.TimeStrftime, Strftime),
                "strptime" => BuiltinCallable.Create(LythonKnownCallableSignatures.TimeStrptime, Strptime),
                "struct_time" => TimeStructTimeType.Instance,
                "tzset" => BuiltinCallable.Create(
                    LythonKnownCallableSignatures.TimeTzset,
                    (_, span, _) => throw new LythonRuntimeException(
                        "NotImplementedError",
                        "time.tzset() is unsupported because Lython uses the host's fixed local offset and has no ambient timezone database.",
                        span)),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TryGetMember(string name, ExecutionContext context, LythonSourceSpan span, [MaybeNullWhen(false)] out object value)
        {
            if (name is not ("timezone" or "altzone" or "daylight" or "tzname"))
            {
                return TryGetMember(name, out value);
            }

            context.RegisterHostCall(span);
            var offset = context.Host.LocalNow.Offset;
            var secondsWest = new BigInteger(-(long)offset.TotalSeconds);
            var zoneName = FixedZoneName(offset);
            value = name switch
            {
                "timezone" or "altzone" => secondsWest,
                "daylight" => BigInteger.Zero,
                "tzname" => new PyTuple(
                    [PyString.FromString(zoneName), PyString.FromString(zoneName)],
                    context.MemoryGovernor,
                    span),
                _ => MissingMemberValue.Instance,
            };

            return true;
        }

        private static object CurrentTime(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = arguments;
            context.RegisterHostCall(span);
            return UnixSeconds(context.Host.UtcNow);
        }

        private static object CurrentTimeNanoseconds(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = arguments;
            context.RegisterHostCall(span);
            var ticks = context.Host.UtcNow.UtcDateTime.Ticks - DateTime.UnixEpoch.Ticks;
            return new BigInteger(ticks) * 100;
        }

        private static object Monotonic(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = arguments;
            return context.ReadHostMonotonicNanoseconds(span) / 1_000_000_000d;
        }

        private static object MonotonicNanoseconds(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = arguments;
            return new BigInteger(context.ReadHostMonotonicNanoseconds(span));
        }

        private static object Sleep(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var duration = ReadSleepDuration(arguments[0], span);
            context.DelayHost(duration, span);
            return PyNone.Instance;
        }

        private static async ValueTask<object> SleepAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var duration = ReadSleepDuration(arguments[0], span);
            await context.DelayHostAsync(duration, span).ConfigureAwait(false);
            return PyNone.Instance;
        }

        private static object GetClockInfo(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (!PyStringOps.TryAsString(arguments[0], out var nameValue))
            {
                throw new LythonRuntimeException("TypeError", "time.get_clock_info(name) expects a string.", span);
            }

            var name = nameValue.AsString();
            return name switch
            {
                "time" => new TimeClockInfoValue(
                    adjustable: true,
                    implementation: "Lython host UTC wall clock",
                    monotonic: false,
                    resolution: 1d / TimeSpan.TicksPerSecond),
                "monotonic" or "perf_counter" => new TimeClockInfoValue(
                    adjustable: false,
                    implementation: "Lython host monotonic clock",
                    monotonic: true,
                    resolution: context.ReadHostMonotonicResolutionNanoseconds(span) / 1_000_000_000d),
                "process_time" or "thread_time" => throw new LythonRuntimeException(
                    "NotImplementedError",
                    $"time.get_clock_info('{name}') is unsupported because Lython has no host {name.Replace('_', ' ')} capability.",
                    span),
                _ => throw new LythonRuntimeException("ValueError", $"unknown clock: {name}", span),
            };
        }

        private static TimeSpan ReadSleepDuration(object value, LythonSourceSpan span)
        {
            if (!PyNumberOps.TryAsNumber(value, out var number))
            {
                throw new LythonRuntimeException("TypeError", "time.sleep(seconds) expects a real number.", span);
            }

            var seconds = number.ToDouble();
            if (double.IsNaN(seconds))
            {
                throw new LythonRuntimeException("ValueError", "Invalid value NaN (not a number)", span);
            }

            if (seconds < 0)
            {
                throw new LythonRuntimeException("ValueError", "sleep length must be non-negative", span);
            }

            if (!double.IsFinite(seconds) || seconds > TimeSpan.MaxValue.TotalSeconds)
            {
                throw new LythonRuntimeException("OverflowError", "timestamp out of range for platform time_t", span);
            }

            try
            {
                return TimeSpan.FromSeconds(seconds);
            }
            catch (OverflowException ex)
            {
                throw new LythonRuntimeException("OverflowError", ex.Message, span);
            }
        }

        private static BuiltinCallable Unsupported(LythonCallableSignature signature, string capability)
            => BuiltinCallable.Create(
                signature,
                (_, span, _) => throw new LythonRuntimeException(
                    "NotImplementedError",
                    $"{signature.Name}() is unsupported because Lython has no host {capability} capability.",
                    span));

        private static object Gmtime(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            DateTime utc;
            if (arguments.Length == 0 || arguments[0] is PyNone)
            {
                context.RegisterHostCall(span);
                utc = context.Host.UtcNow.UtcDateTime;
            }
            else
            {
                utc = TimestampToInstant(arguments[0], "time.gmtime", span).UtcDateTime;
            }

            return TimeStructTimeValue.FromDateTime(
                DateTime.SpecifyKind(utc, DateTimeKind.Unspecified),
                isDst: 0,
                PyString.FromString("GMT"),
                BigInteger.Zero,
                context,
                span);
        }

        private static object Localtime(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            DateTimeOffset local;
            TimeSpan offset;
            context.RegisterHostCall(span);
            var localNow = context.Host.LocalNow;
            if (arguments.Length == 0 || arguments[0] is PyNone)
            {
                local = localNow;
                offset = localNow.Offset;
            }
            else
            {
                offset = localNow.Offset;
                local = TimestampToInstant(arguments[0], "time.localtime", span).ToOffset(offset);
            }

            return TimeStructTimeValue.FromDateTime(
                DateTime.SpecifyKind(local.DateTime, DateTimeKind.Unspecified),
                isDst: 0,
                PyString.FromString(FixedZoneName(offset)),
                new BigInteger((long)offset.TotalSeconds),
                context,
                span);
        }

        private static object Ctime(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var local = (TimeStructTimeValue)Localtime(arguments, span, context);
            return PyString.FromString(FormatAsctime(ReadTimeTuple(local, "time.ctime", span)), context.MemoryGovernor, span);
        }

        private static object Mktime(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var fields = ReadTimeTuple(arguments[0], "time.mktime", span);
            var local = CreateDateTime(fields, "time.mktime", span);
            try
            {
                context.RegisterHostCall(span);
                var offset = context.Host.LocalNow.Offset;
                return UnixSeconds(new DateTimeOffset(local, offset));
            }
            catch (ArgumentException ex)
            {
                throw new LythonRuntimeException("OverflowError", ex.Message, span);
            }
        }

        private static object Asctime(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            TimeFields fields;
            if (arguments.Length == 0)
            {
                fields = ReadTimeTuple(Localtime([], span, context), "time.asctime", span);
            }
            else
            {
                fields = ReadTimeTuple(arguments[0], "time.asctime", span);
            }

            return PyString.FromString(FormatAsctime(fields), context.MemoryGovernor, span);
        }

        private static object Strftime(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (!PyStringOps.TryAsString(arguments[0], out var format))
            {
                throw new LythonRuntimeException("TypeError", "time.strftime(format[, t]) expects format to be a string.", span);
            }

            TimeFields fields;
            if (arguments.Length < 2)
            {
                fields = ReadTimeTuple(Localtime([], span, context), "time.strftime", span);
            }
            else
            {
                fields = ReadTimeTuple(arguments[1], "time.strftime", span);
            }

            var dateTime = CreateDateTime(fields, "time.strftime", span);
            var timezone = TimezoneForFormatting(fields, context, span);
            var result = PyDateTimeOps.FormatStrftime(
                dateTime,
                timezone,
                format.AsString(),
                span,
                fields.Weekday,
                fields.YearDay);
            return PyString.FromString(result, context.MemoryGovernor, span);
        }

        private static object Strptime(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (!PyStringOps.TryAsString(arguments[0], out var text) ||
                arguments.Length >= 2 && !PyStringOps.TryAsString(arguments[1], out _))
            {
                throw new LythonRuntimeException("TypeError", "time.strptime(string[, format]) expects string arguments.", span);
            }

            var format = arguments.Length >= 2
                ? ((PyString)arguments[1]).AsString()
                : "%a %b %d %H:%M:%S %Y";
            try
            {
                var parsed = PyDateTimeOps.ParseStrptime(text.AsString(), format, span);
                object zone = parsed.TzInfo is not null && format.Contains("%Z", StringComparison.Ordinal)
                    ? PyString.FromString(parsed.TzInfo.Name)
                    : PyNone.Instance;
                object offset = parsed.TzInfo is null
                    ? PyNone.Instance
                    : new BigInteger((long)parsed.TzInfo.Offset.TotalSeconds);
                return TimeStructTimeValue.FromDateTime(parsed.Value, -1, zone, offset, context, span);
            }
            catch (FormatException ex)
            {
                throw new LythonRuntimeException("ValueError", ex.Message, span);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new LythonRuntimeException("ValueError", ex.Message, span);
            }
        }

        private static double UnixSeconds(DateTimeOffset value)
            => (value.UtcDateTime.Ticks - DateTime.UnixEpoch.Ticks) / (double)TimeSpan.TicksPerSecond;

        private static DateTimeOffset TimestampToInstant(object value, string owner, LythonSourceSpan span)
        {
            if (!PyNumberOps.TryAsNumber(value, out var number))
            {
                throw new LythonRuntimeException("TypeError", $"{owner}([secs]) expects a real number or None.", span);
            }

            var seconds = number.ToDouble();
            if (double.IsNaN(seconds))
            {
                throw new LythonRuntimeException("ValueError", "Invalid value NaN (not a number)", span);
            }

            if (!double.IsFinite(seconds))
            {
                throw new LythonRuntimeException("OverflowError", "timestamp out of range for platform time_t", span);
            }

            try
            {
                var ticks = checked((long)Math.Round(seconds * TimeSpan.TicksPerSecond, MidpointRounding.ToEven));
                return DateTimeOffset.UnixEpoch.AddTicks(ticks);
            }
            catch (Exception ex) when (ex is OverflowException or ArgumentOutOfRangeException)
            {
                throw new LythonRuntimeException("OverflowError", "timestamp out of range for platform time_t", span);
            }
        }

        private static TimeFields ReadTimeTuple(object value, string owner, LythonSourceSpan span)
        {
            IReadOnlyList<object> sequence = value switch
            {
                TimeStructTimeValue structTime => structTime,
                PyTuple tuple => tuple,
                PyNamedTupleObject namedTuple => namedTuple,
                PyTypingNamedTupleObject typingTuple => typingTuple,
                _ => throw new LythonRuntimeException("TypeError", "Tuple or struct_time argument required", span),
            };

            if (sequence.Count != 9)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}(): illegal time tuple argument", span);
            }

            return new TimeFields(
                IntegerField(sequence[0], owner, span),
                IntegerField(sequence[1], owner, span),
                IntegerField(sequence[2], owner, span),
                IntegerField(sequence[3], owner, span),
                IntegerField(sequence[4], owner, span),
                IntegerField(sequence[5], owner, span),
                IntegerField(sequence[6], owner, span),
                IntegerField(sequence[7], owner, span),
                IntegerField(sequence[8], owner, span),
                value is TimeStructTimeValue timeValue ? timeValue.Zone : PyNone.Instance,
                value is TimeStructTimeValue offsetValue ? offsetValue.GmtOffset : PyNone.Instance);
        }

        private static int IntegerField(object value, string owner, LythonSourceSpan span)
        {
            if (!PyNumberOps.TryAsInteger(value, out var integer) || integer < int.MinValue || integer > int.MaxValue)
            {
                throw new LythonRuntimeException("TypeError", $"{owner}() requires integer time tuple fields.", span);
            }

            return (int)integer;
        }

        private static DateTime CreateDateTime(TimeFields fields, string owner, LythonSourceSpan span)
        {
            try
            {
                return new DateTime(
                    fields.Year,
                    fields.Month,
                    fields.MonthDay,
                    fields.Hour,
                    fields.Minute,
                    fields.Second,
                    DateTimeKind.Unspecified);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new LythonRuntimeException("ValueError", $"{owner}(): {ex.Message}", span);
            }
        }

        private static string FormatAsctime(TimeFields fields)
        {
            string[] weekdays = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
            string[] months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
            if (fields.Weekday is < 0 or > 6 || fields.Month is < 1 or > 12 || fields.MonthDay is < 1 or > 31 ||
                fields.Hour is < 0 or > 23 || fields.Minute is < 0 or > 59 || fields.Second is < 0 or > 61)
            {
                throw new LythonRuntimeException("ValueError", "invalid time tuple fields", null);
            }

            return FormattableString.Invariant(
                $"{weekdays[fields.Weekday]} {months[fields.Month - 1]} {fields.MonthDay,2} {fields.Hour:00}:{fields.Minute:00}:{fields.Second:00} {fields.Year}");
        }

        private static PyTimezone TimezoneForFormatting(TimeFields fields, ExecutionContext context, LythonSourceSpan span)
        {
            if (fields.GmtOffset is not PyNone)
            {
                var seconds = IntegerField(fields.GmtOffset, "time.strftime", span);
                try
                {
                    var name = PyStringOps.TryAsString(fields.Zone, out var zone) ? zone.AsString() : string.Empty;
                    return new PyTimezone(TimeSpan.FromSeconds(seconds), name);
                }
                catch (OverflowException ex)
                {
                    throw new LythonRuntimeException("ValueError", ex.Message, span);
                }
            }

            context.RegisterHostCall(span);
            var offset = context.Host.LocalNow.Offset;
            return new PyTimezone(offset, FixedZoneName(offset));
        }

        private static string FixedZoneName(TimeSpan offset)
            => offset == TimeSpan.Zero ? "UTC" : "UTC" + PyDateTimeOps.FormatOffset(offset);

        private sealed record TimeFields(
            int Year,
            int Month,
            int MonthDay,
            int Hour,
            int Minute,
            int Second,
            int Weekday,
            int YearDay,
            int IsDst,
            object Zone,
            object GmtOffset);
    }


}

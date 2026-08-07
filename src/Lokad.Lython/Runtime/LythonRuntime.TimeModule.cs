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
                "time" => new BuiltinCallable(LythonKnownCallableSignatures.TimeTime, CurrentTime),
                "time_ns" => new BuiltinCallable(LythonKnownCallableSignatures.TimeTimeNs, CurrentTimeNanoseconds),
                "monotonic" => new BuiltinCallable(LythonKnownCallableSignatures.TimeMonotonic, Monotonic),
                "monotonic_ns" => new BuiltinCallable(LythonKnownCallableSignatures.TimeMonotonicNs, MonotonicNanoseconds),
                "perf_counter" => new BuiltinCallable(LythonKnownCallableSignatures.TimePerfCounter, Monotonic),
                "perf_counter_ns" => new BuiltinCallable(LythonKnownCallableSignatures.TimePerfCounterNs, MonotonicNanoseconds),
                "sleep" => new BuiltinCallable(LythonKnownCallableSignatures.TimeSleep, Sleep, SleepAsync),
                "get_clock_info" => new BuiltinCallable(LythonKnownCallableSignatures.TimeGetClockInfo, GetClockInfo),
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
                "gmtime" => new BuiltinCallable(LythonKnownCallableSignatures.TimeGmtime, Gmtime),
                "localtime" => new BuiltinCallable(LythonKnownCallableSignatures.TimeLocaltime, Localtime),
                "ctime" => new BuiltinCallable(LythonKnownCallableSignatures.TimeCtime, Ctime),
                "mktime" => new BuiltinCallable(LythonKnownCallableSignatures.TimeMktime, Mktime),
                "asctime" => new BuiltinCallable(LythonKnownCallableSignatures.TimeAsctime, Asctime),
                "strftime" => new BuiltinCallable(LythonKnownCallableSignatures.TimeStrftime, Strftime),
                "strptime" => new BuiltinCallable(LythonKnownCallableSignatures.TimeStrptime, Strptime),
                "struct_time" => TimeStructTimeType.Instance,
                "tzset" => new BuiltinCallable(
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

            return !ReferenceEquals(value, MissingMemberValue.Instance) || TryGetMember(name, out value);
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
            => new(
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
            var offset = context.Host.LocalNow.Offset;
            if (arguments.Length == 0 || arguments[0] is PyNone)
            {
                context.RegisterHostCall(span);
                local = context.Host.LocalNow;
            }
            else
            {
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
                return UnixSeconds(new DateTimeOffset(local, context.Host.LocalNow.Offset));
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

    private sealed class TimeClockInfoValue(
        bool adjustable,
        string implementation,
        bool monotonic,
        double resolution) : IPyDynamicAttributes, IPyRenderableValue
    {
        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "adjustable" => adjustable,
                "implementation" => PyString.FromString(implementation),
                "monotonic" => monotonic,
                "resolution" => resolution,
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            _ = name;
            _ = value;
            return false;
        }

        public PyString RenderPython(PyRenderingContext context)
            => PyString.FromString(
                "namespace(" +
                $"adjustable={PyRendering.ToPythonString(adjustable, context)}, " +
                $"implementation={PyRendering.ToPythonString(PyString.FromString(implementation), context)}, " +
                $"monotonic={PyRendering.ToPythonString(monotonic, context)}, " +
                $"resolution={PyRendering.ToPythonString(resolution, context)})");

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    internal sealed class TimeStructTimeType : ICallable, INamedRuntimeCallable, IPyRenderableValue, IPyDynamicAttributes
    {
        public static readonly TimeStructTimeType Instance = new();

        private TimeStructTimeType()
        {
        }

        public string Name => "time.struct_time";

        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecutionBudget(span);
            if (arguments.Length != 1 || arguments[0].IsKeyword)
            {
                throw new LythonRuntimeException("TypeError", "time.struct_time() expects one positional sequence argument.", span);
            }

            var values = ToSequence(arguments[0].Value, span, context).ToArray();
            if (values.Length < 9)
            {
                throw new LythonRuntimeException("TypeError", $"time.struct_time() takes an at least 9-sequence ({values.Length}-sequence given)", span);
            }

            if (values.Length > 11)
            {
                throw new LythonRuntimeException("TypeError", $"time.struct_time() takes an at most 11-sequence ({values.Length}-sequence given)", span);
            }

            return new TimeStructTimeValue(
                values[..9],
                values.Length >= 10 ? values[9] : PyNone.Instance,
                values.Length >= 11 ? values[10] : PyNone.Instance,
                context.MemoryGovernor,
                span);
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "__name__" => PyString.FromString("struct_time"),
                "n_fields" => new BigInteger(11),
                "n_sequence_fields" => new BigInteger(9),
                "n_unnamed_fields" => BigInteger.Zero,
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            _ = name;
            _ = value;
            return false;
        }

        public PyString RenderPython(PyRenderingContext context)
        {
            _ = context;
            return PyString.FromString("<class 'time.struct_time'>");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);
    }

    internal sealed class TimeStructTimeValue :
        IPySequenceValue,
        IPyIndexableValue,
        IPyIterableValue,
        IPyRenderableValue,
        IPyDynamicAttributes,
        IPyHashableValue,
        IEquatable<TimeStructTimeValue>
    {
        private static readonly string[] FieldNames =
        [
            "tm_year",
            "tm_mon",
            "tm_mday",
            "tm_hour",
            "tm_min",
            "tm_sec",
            "tm_wday",
            "tm_yday",
            "tm_isdst",
        ];

        private readonly PyTuple _values;

        public TimeStructTimeValue(
            IEnumerable<object> values,
            object zone,
            object gmtOffset,
            MemoryGovernor governor,
            LythonSourceSpan span)
        {
            _values = new PyTuple(values, governor, span);
            if (_values.Count != 9)
            {
                throw new ArgumentException("struct_time requires nine sequence fields", nameof(values));
            }

            Zone = zone;
            GmtOffset = gmtOffset;
        }

        public object Zone { get; }

        public object GmtOffset { get; }

        public int Count => 9;

        public int Length => 9;

        public object this[int index] => _values[index];

        public object GetItem(int index) => _values[index];

        public object CreateSlice(IEnumerable<object> items) => new PyTuple(items);

        public object GetIndex(int index) => _values[index];

        public object GetSlice(IEnumerable<int> indices) => new PyTuple(indices.Select(index => _values[index]));

        public IEnumerable<object> Iterate() => _values;

        public IEnumerator<object> GetEnumerator() => _values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            var fieldIndex = Array.IndexOf(FieldNames, name);
            if (fieldIndex >= 0)
            {
                value = _values[fieldIndex];
                return true;
            }

            value = name switch
            {
                "tm_zone" => Zone,
                "tm_gmtoff" => GmtOffset,
                "n_fields" => new BigInteger(11),
                "n_sequence_fields" => new BigInteger(9),
                "n_unnamed_fields" => BigInteger.Zero,
                "count" => new BoundCallable((arguments, span, _) => CountValue(arguments, span), "struct_time.count", ["value"]),
                "index" => new BoundCallable((arguments, span, _) => IndexValue(arguments, span), "struct_time.index", ["value", "start", "stop"], 1),
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        public bool TrySetMember(string name, object value)
        {
            _ = name;
            _ = value;
            return false;
        }

        public int GetPyHashCode()
        {
            var hash = new HashCode();
            foreach (var value in _values)
            {
                hash.Add(PyValueComparer.Instance.GetHashCode(value));
            }

            return hash.ToHashCode();
        }

        public bool Equals(TimeStructTimeValue? other)
            => other is not null && _values.SequenceEqual(other._values, PyValueComparer.Instance);

        public override bool Equals(object? obj) => obj is TimeStructTimeValue other && Equals(other);

        public override int GetHashCode() => GetPyHashCode();

        public PyString RenderPython(PyRenderingContext context)
        {
            var fields = FieldNames.Select((name, index) => $"{name}={PyRendering.ToPythonString(_values[index], context)}");
            return PyString.FromString($"time.struct_time({string.Join(", ", fields)})");
        }

        public PyString RenderInterpolated(PyRenderingContext context) => RenderPython(context);

        public static TimeStructTimeValue FromDateTime(
            DateTime value,
            int isDst,
            object zone,
            object gmtOffset,
            ExecutionContext context,
            LythonSourceSpan span)
            => new(
                [
                    new BigInteger(value.Year),
                    new BigInteger(value.Month),
                    new BigInteger(value.Day),
                    new BigInteger(value.Hour),
                    new BigInteger(value.Minute),
                    new BigInteger(value.Second),
                    new BigInteger(((int)value.DayOfWeek + 6) % 7),
                    new BigInteger(value.DayOfYear),
                    new BigInteger(isDst),
                ],
                zone,
                gmtOffset,
                context.MemoryGovernor,
                span);

        private object CountValue(object[] arguments, LythonSourceSpan span)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "struct_time.count(value) expects one argument.", span);
            }

            return new BigInteger(_values.Count(item => AreEqual(item, arguments[0])));
        }

        private object IndexValue(object[] arguments, LythonSourceSpan span)
        {
            var start = arguments.Length >= 2 ? NormalizeBound(arguments[1], 0, span) : 0;
            var stop = arguments.Length >= 3 ? NormalizeBound(arguments[2], Count, span) : Count;
            for (var index = start; index < stop; index++)
            {
                if (AreEqual(_values[index], arguments[0]))
                {
                    return new BigInteger(index);
                }
            }

            throw new LythonRuntimeException("ValueError", "tuple.index(x): x not in tuple", span);
        }

        private static int NormalizeBound(object value, int fallback, LythonSourceSpan span)
        {
            if (value is PyNone)
            {
                return fallback;
            }

            if (!PyNumberOps.TryAsInteger(value, out var integer))
            {
                throw new LythonRuntimeException("TypeError", "slice indices must be integers", span);
            }

            var normalized = integer < 0 ? integer + 9 : integer;
            return (int)BigInteger.Clamp(normalized, BigInteger.Zero, new BigInteger(9));
        }
    }

    internal sealed partial class ExecutionContext
    {
        public long ReadHostMonotonicNanoseconds(LythonSourceSpan? span)
        {
            var timing = RequireHostTiming(span);
            RegisterHostCall(span);
            var value = ReadTimingValue(() => timing.MonotonicNanoseconds, "time.monotonic", span);
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
            var value = ReadTimingValue(() => timing.MonotonicResolutionNanoseconds, "time.get_clock_info", span);
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
            AwaitHost(timing, () => timing.DelayAsync(duration, Limits.CancellationToken), "time.sleep", span);
        }

        public ValueTask DelayHostAsync(TimeSpan duration, LythonSourceSpan? span)
        {
            var timing = RequireHostTiming(span);
            RegisterHostCall(span);
            return AwaitHostAsync(() => timing.DelayAsync(duration, Limits.CancellationToken), "time.sleep", span);
        }

        private ILythonTiming RequireHostTiming(LythonSourceSpan? span)
            => Host.Timing ?? throw RuntimeErrors.Runtime(
                "host timing/sleep capability is not available in this host.",
                span);

        private static long ReadTimingValue(Func<long> read, string name, LythonSourceSpan? span)
        {
            try
            {
                return read();
            }
            catch (LythonRuntimeException)
            {
                throw;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                throw RuntimeErrors.Host(name, ex, span);
            }
        }
    }
}

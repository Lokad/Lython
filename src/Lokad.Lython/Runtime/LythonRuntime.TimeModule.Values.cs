using System.Collections;
using System.Globalization;
using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
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
                "count" => BoundCallable.Create((arguments, span, _) => CountValue(arguments, span), "struct_time.count", ["value"]),
                "index" => BoundCallable.Create((arguments, span, _) => IndexValue(arguments, span), "struct_time.index", ["value", "start", "stop"], 1),
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
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
}

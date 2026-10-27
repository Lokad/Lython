using System.Numerics;
using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static class PublicProjection
{
    public static object? NormalizeValue(object? value)
        => NormalizeValue(value, null);

    public static object? NormalizeValue(object? value, ProjectionBudget? budget)
        => NormalizeValue(value, budget, new HashSet<object>(ReferenceEqualityComparer.Instance));

    // Containers thread one identity/depth tracker along the current path so a
    // value that references itself fails instead of exhausting the host stack,
    // and nesting past the interpreter depth fails before CLR recursion does.
    // Shared aliases encountered sequentially (not on the active path) still
    // project once per occurrence. Capture paths use their own allowance.
    private static object? NormalizeValue(object? value, ProjectionBudget? budget, HashSet<object> active)
    {
        return value switch
        {
            null => null,
            PyNone => ProjectNone(),
            bool => value,
            sbyte => value,
            byte => value,
            short => value,
            ushort => value,
            int => value,
            uint => value,
            long => value,
            ulong => value,
            BigInteger integer => ProjectInteger(value, integer, budget),
            float => value,
            double => value,
            PyComplex complex => new Complex(complex.Real, complex.Imaginary),
            decimal => value,
            string text => ProjectClrString(text, budget),
            byte[] bytes => ProjectByteArray(bytes, budget),
            PyString text => ProjectString(text, budget),
            PyBytes bytes => ProjectBytes(bytes, budget),
            PyPath path => ProjectPath(path, budget),
            PyDecimal decimalValue => ProjectDecimal(decimalValue),
            PyTimedelta delta => ProjectTimedelta(delta),
            PyDate date => ProjectDate(date),
            PyTime time => ProjectTime(time),
            PyDateTime dateTime => ProjectDateTime(dateTime),
            PyTimezone timezone => ProjectTimezone(timezone),
            PyList list => ProjectList(list, budget, active),
            PyTuple tuple => ProjectTuple(tuple, budget, active),
            PyDecimalTuple decimalTuple => ProjectSequence(decimalTuple, budget, active),
            PyIsoCalendarDate calendar => ProjectSequence(calendar, budget, active),
            LythonRuntime.TimeStructTimeValue structTime => ProjectSequence(structTime, budget, active),
            LythonRuntime.UrllibParseModule.UrlResult result => ProjectSequence(result, budget, active),
            LythonRuntime.StatisticsModule.StatisticsLinearRegressionResult result => ProjectSequence(result, budget, active),
            PyMappingProxy view => ProjectDictionary(view.Dictionary, budget, active),
            PyDict dict => ProjectDictionary(dict, budget, active),
            PySet set => ProjectSet(set, budget, active),
            _ => throw new ProjectionException("unsupported runtime value cannot be projected to a public CLR value.")
        };
    }

    private static void EnterContainer(object container, HashSet<object> active)
    {
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            throw new ProjectionException("insufficient stack headroom while projecting a nested value.")
            { Attribution = new(LythonFailureReason.ProjectionDepthLimit) };
        }

        if (!active.Add(container))
        {
            throw new ProjectionException("cyclic value cannot be projected to a public CLR value.");
        }

        if (active.Count > LythonRuntime.ExecutionLimits.MaxInterpreterDepth)
        {
            active.Remove(container);
            throw new ProjectionException("maximum projection depth exceeded while projecting a nested value.")
            {
                Attribution = new(LythonFailureReason.ProjectionDepthLimit, LythonRuntime.ExecutionLimits.MaxInterpreterDepth,
                    active.Count + 1L)
            };
        }
    }

    private static void ExitContainer(object container, HashSet<object> active)
        => active.Remove(container);

    public static object? ProjectNone() => null;

    private static object ProjectInteger(object value, BigInteger integer, ProjectionBudget? budget)
    {
        // Large magnitudes may alias execution storage. Establish retained
        // result funding before the execution owner is disposed. Small inline
        // scalars remain part of the documented approximation allowance.
        if (integer.GetByteCount() > 9) budget?.Reserve(64L + integer.GetByteCount());
        return value;
    }

    public static string ProjectString(PyString text)
        => ProjectString(text, null);

    public static string ProjectString(PyString text, ProjectionBudget? budget)
    {
        budget?.Reserve(32L + (2L * text.GetUtf16CodeUnitCount()));
        return text.AsString();
    }

    public static string ProjectClrString(string text, ProjectionBudget? budget)
    {
        budget?.Reserve(32L + (2L * text.Length));
        return text;
    }

    public static byte[] ProjectBytes(PyBytes bytes)
        => ProjectBytes(bytes, null);

    public static byte[] ProjectBytes(PyBytes bytes, ProjectionBudget? budget)
    {
        budget?.Reserve(32L + bytes.Length);
        // The projection owner already admits this independent copy. Asking
        // the guest value to own it as well double-charges the same backing
        // and can leak an execution refusal out of return projection.
        return budget is null ? bytes.ToArray() : bytes.Bytes.ToArray();
    }

    public static byte[] ProjectByteArray(byte[] bytes, ProjectionBudget? budget)
    {
        budget?.Reserve(32L + bytes.Length);
        return bytes.ToArray();
    }

    public static string ProjectPath(PyPath path)
        => ProjectPath(path, null);

    public static string ProjectPath(PyPath path, ProjectionBudget? budget)
    {
        budget?.Reserve(32L + (2L * path.Value.GetUtf16CodeUnitCount()));
        return path.Value.AsString();
    }

    public static decimal ProjectDecimal(PyDecimal decimalValue) => decimalValue.Value;

    public static TimeSpan ProjectTimedelta(PyTimedelta delta)
    {
        var ticks = delta.TotalMicroseconds * 10;
        if (ticks < long.MinValue || ticks > long.MaxValue) throw new ProjectionException("timedelta exceeds the CLR TimeSpan representation");
        return new TimeSpan((long)ticks);
    }

    public static DateOnly ProjectDate(PyDate date) => date.Value;

    public static object ProjectTime(PyTime time)
    {
        if (time.TzInfo is null) return time.Value;
        var offset = time.TzInfo.Offset;
        ValidateOffset(offset);
        // Preserve the original year-1 anchor where representable. Midnight at
        // a positive offset needs the next day to represent its UTC instant.
        var anchor = new DateTime(1, 1, 1).AddTicks(time.Value.Ticks);
        if (anchor.Ticks - offset.Ticks < 0) anchor = anchor.AddDays(1);
        return new DateTimeOffset(anchor, offset);
    }

    public static object ProjectDateTime(PyDateTime dateTime)
    {
        if (dateTime.TzInfo is null) return dateTime.Value;
        var offset = dateTime.TzInfo.Offset;
        ValidateOffset(offset);
        var utcTicks = dateTime.Value.Ticks - offset.Ticks;
        if (utcTicks < DateTime.MinValue.Ticks || utcTicks > DateTime.MaxValue.Ticks)
            throw new ProjectionException("datetime UTC instant exceeds the CLR DateTimeOffset representation");
        return new DateTimeOffset(DateTime.SpecifyKind(dateTime.Value, DateTimeKind.Unspecified), offset);
    }

    private static void ValidateOffset(TimeSpan offset)
    {
        if (offset.Ticks % TimeSpan.TicksPerMinute != 0 || offset.Duration() > TimeSpan.FromHours(14))
            throw new ProjectionException("timezone offset cannot be represented by CLR DateTimeOffset (whole minutes within 14 hours required)");
    }

    public static TimeSpan ProjectTimezone(PyTimezone timezone) => timezone.Offset;

    public static Dictionary<object, object?> ProjectDictionary(PyDict dict)
        => ProjectDictionary(dict, null);

    public static Dictionary<object, object?> ProjectDictionary(PyDict dict, ProjectionBudget? budget)
        => ProjectDictionary(dict, budget, new HashSet<object>(ReferenceEqualityComparer.Instance));

    private static Dictionary<object, object?> ProjectDictionary(PyDict dict, ProjectionBudget? budget, HashSet<object> active)
    {
        EnterContainer(dict, active);
        try
        {
            budget?.Reserve(64L + (32L * dict.Count));
            var normalized = new Dictionary<object, object?>(dict.Count);
            foreach (var pair in dict)
            {
                normalized[ProjectDictionaryKey(pair.Key, budget, active)] = NormalizeValue(pair.Value, budget, active);
            }

            return normalized;
        }
        finally
        {
            ExitContainer(dict, active);
        }
    }

    private static object ProjectDictionaryKey(object key, ProjectionBudget? budget, HashSet<object> active)
        => NormalizeValue(key, budget, active) ?? throw new ProjectionException("None dictionary keys cannot be projected to a CLR dictionary");

    public static List<object?> ProjectList(PyList list)
        => ProjectList(list, null);

    public static List<object?> ProjectList(PyList list, ProjectionBudget? budget)
        => ProjectList(list, budget, new HashSet<object>(ReferenceEqualityComparer.Instance));

    private static List<object?> ProjectList(PyList list, ProjectionBudget? budget, HashSet<object> active)
    {
        EnterContainer(list, active);
        try
        {
            budget?.Reserve(64L + (16L * list.Count));
            var normalized = new List<object?>(list.Count);
            foreach (var item in list)
            {
                normalized.Add(NormalizeValue(item, budget, active));
            }

            return normalized;
        }
        finally
        {
            ExitContainer(list, active);
        }
    }

    public static object?[] ProjectTuple(PyTuple tuple)
        => ProjectTuple(tuple, null);

    public static object?[] ProjectTuple(PyTuple tuple, ProjectionBudget? budget)
        => ProjectTuple(tuple, budget, new HashSet<object>(ReferenceEqualityComparer.Instance));

    private static object?[] ProjectTuple(PyTuple tuple, ProjectionBudget? budget, HashSet<object> active)
    {
        EnterContainer(tuple, active);
        try
        {
            budget?.Reserve(48L + (16L * tuple.Count));
            var normalized = new object?[tuple.Count];
            var index = 0;
            foreach (var item in tuple)
            {
                normalized[index++] = NormalizeValue(item, budget, active);
            }

            return normalized;
        }
        finally
        {
            ExitContainer(tuple, active);
        }
    }

    public static HashSet<object?> ProjectSet(PySet set)
        => ProjectSet(set, null);

    public static HashSet<object?> ProjectSet(PySet set, ProjectionBudget? budget)
        => ProjectSet(set, budget, new HashSet<object>(ReferenceEqualityComparer.Instance));

    private static HashSet<object?> ProjectSet(PySet set, ProjectionBudget? budget, HashSet<object> active)
    {
        EnterContainer(set, active);
        try
        {
            budget?.Reserve(64L + (24L * set.Count));
            var normalized = new HashSet<object?>(set.Count);
            foreach (var item in set)
            {
                normalized.Add(NormalizeValue(item, budget, active));
            }

            return normalized;
        }
        finally
        {
            ExitContainer(set, active);
        }
    }

    private static object?[] ProjectSequence(IReadOnlyList<object> value, ProjectionBudget? budget, HashSet<object> active)
    {
        EnterContainer(value, active);
        try
        {
            budget?.Reserve(48L + (16L * value.Count));
            var normalized = new object?[value.Count];
            var index = 0;
            foreach (var item in value)
            {
                normalized[index++] = NormalizeValue(item, budget, active);
            }

            return normalized;
        }
        finally
        {
            ExitContainer(value, active);
        }
    }
}

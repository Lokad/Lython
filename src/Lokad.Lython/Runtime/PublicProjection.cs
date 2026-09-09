using System.Numerics;
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
            BigInteger => value,
            float => value,
            double => value,
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
            LythonRuntime.TimeStructTimeValue structTime => ProjectStructTime(structTime, budget, active),
            PyDict dict => ProjectDictionary(dict, budget, active),
            PySet set => ProjectSet(set, budget, active),
            LythonRuntime.ReFindAllResult matches => ProjectFindAllResult(matches, budget, active),
            _ => throw new ProjectionException("unsupported runtime value cannot be projected to a public CLR value.")
        };
    }

    private static void EnterContainer(object container, HashSet<object> active)
    {
        if (!active.Add(container))
        {
            throw new ProjectionException("cyclic value cannot be projected to a public CLR value.");
        }

        if (active.Count > LythonRuntime.ExecutionLimits.MaxInterpreterDepth)
        {
            active.Remove(container);
            throw new ProjectionException("maximum projection depth exceeded while projecting a nested value.");
        }
    }

    private static void ExitContainer(object container, HashSet<object> active)
        => active.Remove(container);

    public static object? ProjectNone() => null;

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
        return bytes.ToArray();
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

    public static TimeSpan ProjectTimedelta(PyTimedelta delta) => delta.Value;

    public static DateOnly ProjectDate(PyDate date) => date.Value;

    public static object ProjectTime(PyTime time)
        => time.TzInfo is null
            ? time.Value
            : new DateTimeOffset(
                1,
                1,
                1,
                time.Value.Hour,
                time.Value.Minute,
                time.Value.Second,
                time.Value.Millisecond,
                time.TzInfo.Offset)
            .AddTicks(time.Value.Ticks % TimeSpan.TicksPerMillisecond);

    public static object ProjectDateTime(PyDateTime dateTime)
        => dateTime.TzInfo is null
            ? (object)dateTime.Value
            : new DateTimeOffset(dateTime.Value, dateTime.TzInfo.Offset);

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
                normalized[ProjectDictionaryKey(pair.Key, dict, budget, active)] = NormalizeValue(pair.Value, budget, active);
            }

            return normalized;
        }
        finally
        {
            ExitContainer(dict, active);
        }
    }

    public static object ProjectDictionaryKey(object key, PyDict owner)
        => ProjectDictionaryKey(key, owner, null);

    public static object ProjectDictionaryKey(object key, PyDict owner, ProjectionBudget? budget)
        => ProjectDictionaryKey(key, owner, budget, new HashSet<object>(ReferenceEqualityComparer.Instance));

    private static object ProjectDictionaryKey(object key, PyDict owner, ProjectionBudget? budget, HashSet<object> active)
    {
        var normalized = NormalizeValue(key, budget, active);
        return normalized
            ?? throw new InvalidOperationException($"Cannot normalize dictionary keys with value None to {PublicProjectionContract.Describe(owner)}.");
    }

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

    public static LythonRuntime.ReFindAllResult ProjectFindAllResult(LythonRuntime.ReFindAllResult matches)
        => ProjectFindAllResult(matches, null);

    public static LythonRuntime.ReFindAllResult ProjectFindAllResult(LythonRuntime.ReFindAllResult matches, ProjectionBudget? budget)
        => ProjectFindAllResult(matches, budget, new HashSet<object>(ReferenceEqualityComparer.Instance));

    private static LythonRuntime.ReFindAllResult ProjectFindAllResult(LythonRuntime.ReFindAllResult matches, ProjectionBudget? budget, HashSet<object> active)
    {
        EnterContainer(matches, active);
        try
        {
            budget?.Reserve(32L + (16L * matches.Items.Count));
            var normalized = new PyList();
            foreach (var item in matches.Items)
            {
                normalized.Add(NormalizeValue(item, budget, active) ?? PyNone.Instance);
            }

            return new LythonRuntime.ReFindAllResult(normalized);
        }
        finally
        {
            ExitContainer(matches, active);
        }
    }

    private static object?[] ProjectStructTime(LythonRuntime.TimeStructTimeValue value, ProjectionBudget? budget, HashSet<object> active)
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

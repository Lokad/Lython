using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static class PublicProjection
{
    public static object? NormalizeValue(object value, ProjectionBudget? budget = null)
    {
        return value switch
        {
            PyNone => ProjectNone(),
            PyString text => ProjectString(text, budget),
            PyBytes bytes => ProjectBytes(bytes, budget),
            PyPath path => ProjectPath(path, budget),
            PyDecimal decimalValue => ProjectDecimal(decimalValue),
            PyTimedelta delta => ProjectTimedelta(delta),
            PyDate date => ProjectDate(date),
            PyTime time => ProjectTime(time),
            PyDateTime dateTime => ProjectDateTime(dateTime),
            PyTimezone timezone => ProjectTimezone(timezone),
            PyList list => ProjectList(list, budget),
            PyTuple tuple => ProjectTuple(tuple, budget),
            LythonRuntime.TimeStructTimeValue structTime => ProjectStructTime(structTime, budget),
            PyDict dict => ProjectDictionary(dict, budget),
            PySet set => ProjectSet(set, budget),
            LythonRuntime.ReFindAllResult matches => ProjectFindAllResult(matches, budget),
            _ => value
        };
    }

    public static object? ProjectNone() => null;

    public static string ProjectString(PyString text, ProjectionBudget? budget = null)
    {
        budget?.Reserve(32L + (2L * text.Length));
        return text.AsString();
    }

    public static byte[] ProjectBytes(PyBytes bytes, ProjectionBudget? budget = null)
    {
        budget?.Reserve(32L + bytes.Length);
        return bytes.ToArray();
    }

    public static string ProjectPath(PyPath path, ProjectionBudget? budget = null)
    {
        budget?.Reserve(32L + (2L * path.Value.Length));
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

    public static Dictionary<object, object?> ProjectDictionary(PyDict dict, ProjectionBudget? budget = null)
    {
        budget?.Reserve(64L + (32L * dict.Count));
        var normalized = new Dictionary<object, object?>(dict.Count);
        foreach (var pair in dict)
        {
            normalized[ProjectDictionaryKey(pair.Key, dict, budget)] = NormalizeValue(pair.Value, budget);
        }

        return normalized;
    }

    public static object ProjectDictionaryKey(object key, PyDict owner, ProjectionBudget? budget = null)
    {
        var normalized = NormalizeValue(key, budget);
        return normalized
            ?? throw new InvalidOperationException($"Cannot normalize dictionary keys with value None to {PublicProjectionContract.Describe(owner)}.");
    }

    public static List<object?> ProjectList(PyList list, ProjectionBudget? budget = null)
    {
        budget?.Reserve(64L + (16L * list.Count));
        var normalized = new List<object?>(list.Count);
        foreach (var item in list)
        {
            normalized.Add(NormalizeValue(item, budget));
        }

        return normalized;
    }

    public static object?[] ProjectTuple(PyTuple tuple, ProjectionBudget? budget = null)
    {
        budget?.Reserve(48L + (16L * tuple.Count));
        var normalized = new object?[tuple.Count];
        var index = 0;
        foreach (var item in tuple)
        {
            normalized[index++] = NormalizeValue(item, budget);
        }

        return normalized;
    }

    public static object?[] ProjectStructTime(LythonRuntime.TimeStructTimeValue value, ProjectionBudget? budget = null)
    {
        budget?.Reserve(48L + (16L * value.Count));
        return value.Select(item => NormalizeValue(item, budget)).ToArray();
    }

    public static HashSet<object?> ProjectSet(PySet set, ProjectionBudget? budget = null)
    {
        budget?.Reserve(64L + (24L * set.Count));
        var normalized = new HashSet<object?>(set.Count);
        foreach (var item in set)
        {
            normalized.Add(NormalizeValue(item, budget));
        }

        return normalized;
    }

    public static LythonRuntime.ReFindAllResult ProjectFindAllResult(LythonRuntime.ReFindAllResult matches, ProjectionBudget? budget = null)
    {
        budget?.Reserve(32L + (16L * matches.Items.Count));
        var normalized = new PyList();
        foreach (var item in matches.Items)
        {
            normalized.Add(NormalizeValue(item, budget) ?? PyNone.Instance);
        }

        return new LythonRuntime.ReFindAllResult(normalized);
    }
}

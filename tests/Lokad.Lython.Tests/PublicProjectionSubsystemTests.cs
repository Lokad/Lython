using System.Numerics;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class PublicProjectionSubsystemTests
{
    [Fact]
    public void Run_ProjectsRuntimeValuesToClrShapesAtEmbeddingBoundary()
    {
        var engine = new LythonEngine();
        var host = new Harness.MockLythonHost();
        var script = engine.Compile("""
value = [1, (2, 3), {"name": "lokad", "items": [4]}]
return value
""");

        var result = script.Run(host);

        Assert.True(result.Success, result.Failure?.Message);
        var list = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Equal(new BigInteger(1), list[0]);

        var tuple = Assert.IsType<object?[]>(list[1]);
        Assert.Equal(new BigInteger(2), tuple[0]);
        Assert.Equal(new BigInteger(3), tuple[1]);

        var dict = Assert.IsType<Dictionary<object, object?>>(list[2]);
        Assert.Equal("lokad", dict["name"]);
        var nested = Assert.IsType<List<object?>>(dict["items"]);
        Assert.Single(nested);
        Assert.Equal(new BigInteger(4), nested[0]);
    }

    [Fact]
    public void Run_ProjectsRuntimeNoneBackToClrNull()
    {
        var engine = new LythonEngine();
        var host = new Harness.MockLythonHost();
        var script = engine.Compile("""
value = [None, {"x": None}]
return value
""");

        var result = script.Run(host);

        Assert.True(result.Success);
        var list = Assert.IsType<List<object?>>(result.ReturnValue);
        Assert.Null(list[0]);
        var dict = Assert.IsType<Dictionary<object, object?>>(list[1]);
        Assert.Null(dict["x"]);
    }

    [Fact]
    public void PublicProjection_NormalizesRuntimeValuesWithoutLeakingRuntimeContainers()
    {
        var runtimeDict = new PyDict();
        runtimeDict.SetItem(PyString.FromString("name"), PyString.FromString("lokad"));

        var runtime = new PyList(
        [
            new PyTuple([new BigInteger(1), PyNone.Instance]),
            new PySet([PyString.FromString("x")]),
            runtimeDict,
        ]);

        var projected = Assert.IsType<List<object?>>(PublicProjection.NormalizeValue(runtime));

        var tuple = Assert.IsType<object?[]>(projected[0]);
        Assert.Equal(new BigInteger(1), tuple[0]);
        Assert.Null(tuple[1]);

        var set = Assert.IsType<HashSet<object?>>(projected[1]);
        Assert.Contains("x", set);

        var projectedDict = Assert.IsType<Dictionary<object, object?>>(projected[2]);
        Assert.Equal("lokad", projectedDict["name"]);
    }

    [Fact]
    public void PublicProjection_RejectsDictionaryKeysThatProjectToNull()
    {
        var dict = new PyDict();
        dict.SetItem(PyNone.Instance, PyString.FromString("value"));

        var ex = Assert.Throws<InvalidOperationException>(() => PublicProjection.ProjectDictionary(dict));

        Assert.Contains("dictionary keys with value None", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PublicProjection_ProjectsFindAllResultExplicitly()
    {
        var matches = new LythonRuntime.ReFindAllResult(new PyList([
            PyString.FromString("a"),
            PyNone.Instance
        ]));

        var projected = PublicProjection.ProjectFindAllResult(matches);

        Assert.Collection(
            projected.Items,
            item => Assert.Equal("a", Assert.IsType<string>(item)),
            item => Assert.Same(PyNone.Instance, item));
    }

    [Fact]
    public void PublicProjection_ProjectsDateTimeRuntimeValuesToClrShapes()
    {
        var naiveDateTime = new PyDateTime(new DateTime(2026, 4, 28, 10, 11, 12, 130));
        Assert.IsType<DateTime>(PublicProjection.ProjectDateTime(naiveDateTime));

        var runtime = new PyList([
            new PyTimedelta(TimeSpan.FromDays(2) + TimeSpan.FromSeconds(3)),
            new PyDate(new DateOnly(2026, 4, 28)),
            new PyTime(new TimeOnly(10, 11, 12, 130)),
            new PyTime(new TimeOnly(10, 11, 12, 130), PyTimezone.Utc),
            naiveDateTime,
            new PyDateTime(new DateTime(2026, 4, 28, 10, 11, 12, 130), PyTimezone.Utc),
            PyTimezone.Utc,
        ]);

        var values = Assert.IsType<List<object?>>(PublicProjection.NormalizeValue(runtime));

        Assert.Equal(TimeSpan.FromDays(2) + TimeSpan.FromSeconds(3), Assert.IsType<TimeSpan>(values[0]));
        Assert.Equal(new DateOnly(2026, 4, 28), Assert.IsType<DateOnly>(values[1]));
        Assert.Equal(new TimeOnly(10, 11, 12, 130), Assert.IsType<TimeOnly>(values[2]));

        var awareTime = Assert.IsType<DateTimeOffset>(values[3]);
        Assert.Equal(new TimeSpan(10, 11, 12) + TimeSpan.FromMilliseconds(130), awareTime.TimeOfDay);
        Assert.Equal(1300000, awareTime.TimeOfDay.Ticks % TimeSpan.TicksPerSecond);
        Assert.Equal(TimeSpan.Zero, awareTime.Offset);

        Assert.Equal(
            new DateTime(2026, 4, 28, 10, 11, 12, 130).AddTicks(0),
            Assert.IsType<DateTime>(values[4]));

        var awareDateTime = Assert.IsType<DateTimeOffset>(values[5]);
        Assert.Equal(new DateTimeOffset(2026, 4, 28, 10, 11, 12, 130, TimeSpan.Zero).AddTicks(0), awareDateTime);
        Assert.Equal(TimeSpan.Zero, Assert.IsType<TimeSpan>(values[6]));
    }

    [Fact]
    public void PublicProjection_OptionalProjectionBudgetFailsDistinctly()
    {
        var runtime = new PyList([
            PyString.FromString(new string('a', 64)),
            PyString.FromString(new string('b', 64)),
        ]);

        var ex = Assert.Throws<ProjectionException>(() => PublicProjection.NormalizeValue(runtime, new ProjectionBudget(128)));

        Assert.Contains("projection memory budget exceeded", ex.Message, StringComparison.Ordinal);
    }
}

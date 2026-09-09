using System.Numerics;
using Lokad.Lython.Runtime;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG23: projection tracks container identity and depth, so cyclic or
/// pathologically deep values fail instead of exhausting the host stack.
/// </summary>
public sealed class ProjectionGraphGuardTests
{
    private static PyList Nest(int depth)
    {
        object current = new BigInteger(1);
        for (var i = 0; i < depth; i++)
        {
            current = new PyList([current]);
        }

        return Assert.IsType<PyList>(current);
    }

    [Fact]
    public void NestingAtLimitProjects()
    {
        var nested = Nest(LythonRuntime.ExecutionLimits.MaxInterpreterDepth);

        var projected = PublicProjection.NormalizeValue(nested);
        Assert.NotNull(projected);
    }

    [Fact]
    public void NestingPastLimitThrowsDistinctly()
    {
        var nested = Nest(LythonRuntime.ExecutionLimits.MaxInterpreterDepth + 1);

        var ex = Assert.Throws<ProjectionException>(() => PublicProjection.NormalizeValue(nested));
        Assert.Contains("maximum projection depth", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SelfReferenceThrowsDistinctly()
    {
        var list = new PyList();
        list.Add(list);

        var ex = Assert.Throws<ProjectionException>(() => PublicProjection.NormalizeValue(list));
        Assert.Contains("cyclic", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedSubgraphProjectsEachAlias()
    {
        var shared = new PyList([new BigInteger(1)]);
        var root = new PyList([shared, shared]);

        var projected = Assert.IsType<List<object?>>(PublicProjection.NormalizeValue(root));
        Assert.Equal(2, projected.Count);
        Assert.Equal(new BigInteger(1), Assert.IsType<List<object?>>(projected[0])[0]);
        Assert.Equal(new BigInteger(1), Assert.IsType<List<object?>>(projected[1])[0]);
    }
}
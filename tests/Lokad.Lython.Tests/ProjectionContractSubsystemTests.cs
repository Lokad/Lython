using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Tests;

public sealed class ProjectionContractSubsystemTests
{
    [Fact]
    public void PublicProjectionContract_DescribesProjectedShapes()
    {
        Assert.Equal("null", PublicProjectionContract.Describe(PyNone.Instance));
        Assert.Equal("string", PublicProjectionContract.Describe(PyString.FromString("x")));
        Assert.Equal("List<object?>", PublicProjectionContract.Describe(new PyList()));
        Assert.Equal("Dictionary<object, object?>", PublicProjectionContract.Describe(new PyDict()));
        Assert.Equal("ReFindAllResult", PublicProjectionContract.Describe(new LythonRuntime.ReFindAllResult(new PyList())));
    }
}

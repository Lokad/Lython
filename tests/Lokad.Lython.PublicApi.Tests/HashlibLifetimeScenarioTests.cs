using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;
// M05: fresh hash shells own lifetime like the other factory results do.
public class HashlibLifetimeScenarioTests : LifetimeScenario
{
    private long StreamingBudget => Budget(3145728);

    private LythonRunOptions Budgeted() => new() { MemoryBudget = new LythonMemoryBudget(StreamingBudget) };

    private Task AssertCompletes(string source, string expected, int iterations = 0) => AssertBatchCompletes(source, expected, StreamingBudget, iterations);

    [Fact]
    public async Task HashCtorDiscardCompletes()
        => await AssertCompletes(
            "import hashlib\nfor i in range(" + BatchIterations(50000) + "):\n    h = hashlib.md5(b'x' * 10)\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task HashNewDiscardCompletes()
        => await AssertCompletes(
            "import hashlib\nfor i in range(" + BatchIterations(50000) + "):\n    h = hashlib.new('sha256', b'x' * 10)\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task HashCopyDiscardCompletes()
        => await AssertCompletes(
            "import hashlib\nh = hashlib.md5(b'x' * 10)\nfor i in range(" + BatchIterations(50000) + "):\n    c = h.copy()\nreturn 0\n", "0", Iterations(50000));

    [Fact]
    public async Task HashCopyBehaves()
        => await AssertCompletes(
            "import hashlib\nh = hashlib.md5(b'abc')\nc = h.copy()\nc.update(b'd')\nreturn h.hexdigest() + ' ' + c.hexdigest()\n", "900150983cd24fb0d6963f7d28e17f72 e2fc714c4727ee9395f324cd2e7f331f");
}

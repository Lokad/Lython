using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RandomModuleFunctionTests
{
    [Theory]
    [InlineData("random.choice({1})", "random.choice")]
    [InlineData("random.choices({1}, k=1)", "random.choices")]
    [InlineData("random.sample({1}, 1)", "random.sample")]
    public void RandomModule_PopulationsMustBeSequences(string expression, string owner)
    {
        var result = new LythonEngine().Run(
            $"import random\n{expression}\n",
            new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal("TypeError", result.Failure?.ExceptionType);
        Assert.Contains(owner, result.Failure?.Message, StringComparison.Ordinal);
        Assert.Contains("sequence", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RandomModule_Functions_HaveDirectCoverage()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
import random

def snapshot():
    vals = []
    vals.append(str(random.random()))
    vals.append(str(random.randrange(10)))
    vals.append(str(random.randint(3, 7)))
    vals.append(str(random.choice(["a", "b", "c"])))
    vals.append(str(random.choices([10, 20, 30], k=3)))
    vals.append(str(random.choices([10, 20], [0, 1], None, 2)))
    items = [1, 2, 3]
    random.shuffle(items)
    vals.append(str(items))
    vals.append(str(random.sample([1, 2, 3, 4], 2)))
    vals.append(str(random.getrandbits(5)))
    return "|".join(vals)

random.seed(123)
first = snapshot()
random.seed(123)
second = snapshot()
__lython_file = open("/out.txt", "w")
__lython_file.write(str(first == second) + "|" + first)
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.StartsWith("True|", host.ReadText("/out.txt"), StringComparison.Ordinal);
    }

    [Fact]
    public void RandomModule_ExpandedSurface_HasDirectCoverage()
    {
        var host = new MockLythonHost();
        var result = new LythonEngine().Run(
            """
import math
import random

random.seed("agent", version=2)
state = random.getstate()
first = [
    random.random(),
    random.randrange(start=2, stop=20, step=3),
    random.randint(a=5, b=8),
    random.randbytes(n=4),
    random.getrandbits(k=7),
]
random.setstate(state)
repeat = [
    random.random(),
    random.randrange(start=2, stop=20, step=3),
    random.randint(a=5, b=8),
    random.randbytes(n=4),
    random.getrandbits(k=7),
]

a = random.Random(123)
b = random.Random(123)
c = random.Random(456)
r = random.Random(7)
saved = r.getstate()
draw1 = r.random()
draw2 = r.random()
r.setstate(saved)

sampled = random.sample(["red", "blue"], k=3, counts=[2, 4])
valid_sample = True
for item in sampled:
    if item != "red" and item != "blue":
        valid_sample = False

choices = random.choices(["a", "b", "c"], cum_weights=[1, 3, 6], k=5)
valid_choices = True
for item in choices:
    if item != "a" and item != "b" and item != "c":
        valid_choices = False

u = random.uniform(10, 20)
t = random.triangular(low=1, high=5, mode=2)
beta = random.betavariate(2, 5)
exp = random.expovariate()
gamma = random.gammavariate(2, 3)
gauss = random.gauss()
normal = random.normalvariate(mu=1, sigma=2)
logn = random.lognormvariate(0, 1)
pareto = random.paretovariate(2)
vm = random.vonmisesvariate(0, 1)
weibull = random.weibullvariate(1, 2)

vals = []
vals.append(str(first == repeat))
vals.append(str(a.random() == b.random()))
vals.append(str(a.random() != c.random()))
vals.append(str(draw1 == r.random()))
vals.append(str(draw2 == r.random()))
vals.append(str(len(random.randbytes(5))))
vals.append(str(isinstance(a, random.Random)))
vals.append(type(a).__name__)
vals.append(str(random.BPF))
vals.append(str(random.RECIP_BPF < 1))
vals.append(str(len(sampled)) + ":" + str(valid_sample))
vals.append(str(len(choices)) + ":" + str(valid_choices))
vals.append(str(10 <= u <= 20))
vals.append(str(1 <= t <= 5))
vals.append(str(0 <= beta <= 1))
vals.append(str(exp > 0))
vals.append(str(gamma > 0))
vals.append(str(isinstance(gauss, float)))
vals.append(str(isinstance(normal, float)))
vals.append(str(logn > 0))
vals.append(str(pareto >= 1))
vals.append(str(0 <= vm <= math.tau))
vals.append(str(weibull >= 0))
__lython_file = open("/out.txt", "w")
__lython_file.write("|".join(vals))
__lython_file.close()
""",
            host);

        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal(
            "True|True|True|True|True|5|True|Random|53|True|3:True|5:True|True|True|True|True|True|True|True|True|True|True|True",
            host.ReadText("/out.txt"));
    }

    [Fact]
    public void RandomModule_StaticContractsCoverExpandedSurface()
    {
        var valid = new LythonEngine().Run(
            """
import random

rng = random.Random(a=1)
data = rng.randbytes(n=2)
items = [1, 2, 3]
rng.shuffle(items)
out = [
    str(len(data)),
    str(rng.randrange(start=1, stop=5)),
    str(len(rng.sample([1, 2, 3], k=2, counts=[1, 1, 1]))),
    str(rng.uniform(a=1, b=2) > 0),
]
return "|".join(out)
""",
            new MockLythonHost());

        Assert.True(
            valid.Success,
            valid.Failure?.Message ?? string.Join(" | ", valid.Diagnostics.Select(d => d.Code + ":" + d.Message)));

        var invalid = new LythonEngine().Run(
            """
import random

random.SystemRandom()
random.Random([])
random.randrange(start="x")
random.randint("a", 2)
random.randbytes("x")
random.sample(1, "x", counts=1)
random.shuffle((1, 2))
random.uniform("a", 1)
rng = random.Random(1)
rng.randbytes()
rng.uniform("a", 1)
rng.missing()
""",
            new MockLythonHost());

        Assert.False(invalid.Success);
        Assert.Null(invalid.Failure);
        Assert.True(
            invalid.Diagnostics.Count(d => d.Code is "LA3158" or "LA3164" or "LA3075") >= 10,
            string.Join(" | ", invalid.Diagnostics.Select(d => d.Code + ":" + d.Message)));
    }

    [Theory]
    [InlineData(
        """
import random
random.choice([])
""",
        "IndexError",
        "empty sequence")]
    [InlineData(
        """
import random
random.randrange(0, 0)
""",
        "ValueError",
        "empty range")]
    [InlineData(
        """
import random
random.choices([1, 2], [0, 0])
""",
        "ValueError",
        "greater than zero")]
    [InlineData(
        """
import random
random.setstate(("bad", 1))
""",
        "ValueError",
        "state object")]
    [InlineData(
        """
import random
random.randbytes(-1)
""",
        "TypeError",
        "non-negative integer")]
    [InlineData(
        """
import random
random.sample([1], 2, counts=[1])
""",
        "ValueError",
        "Sample larger")]
    [InlineData(
        """
import random
random.sample([1, 2], 1, counts=[1])
""",
        "ValueError",
        "one count")]
    [InlineData(
        """
import random
random.choices([1, 2], cum_weights=[2, 1])
""",
        "ValueError",
        "monotonically")]
    [InlineData(
        """
import random
import math
random.choices([1, 2], weights=[math.inf, 1])
""",
        "ValueError",
        "finite")]
    [InlineData(
        """
import random
random.triangular(0, 1, 2)
""",
        "ValueError",
        "between low and high")]
    [InlineData(
        """
import random
random.betavariate(0, 1)
""",
        "ValueError",
        "positive finite")]
    [InlineData(
        """
import random
random.expovariate(0)
""",
        "ValueError",
        "non-zero")]
    public void RandomModule_NearMissContracts_FailPrecisely(string source, string exceptionType, string messageFragment)
    {
        var result = new LythonEngine().Run(source, new MockLythonHost());

        Assert.False(result.Success);
        Assert.NotNull(result.Failure);
        Assert.Equal(exceptionType, result.Failure?.ExceptionType);
        Assert.Contains(messageFragment, result.Failure?.Message, StringComparison.Ordinal);
    }
}



using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeScalarDispatchTests
{
    [Fact]
    public async Task RetainedMixedIntegerResultKeepsItsOwner()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile("value=(1<<4096)+True\nalias=value\nvalue=None\nreturn alias", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(retained.Success, retained.Failure?.Message);
                Assert.Equal((System.Numerics.BigInteger.One << 4096) + 1, retained.ReturnValue);
                var held = budget.ReservedBytes;
                Assert.True(held > baseline);
                using (var next = script.Run(new MockLythonHost())) Assert.True(next.Success, next.Failure?.Message);
                Assert.Equal(held, budget.ReservedBytes);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("binary", false)]
    [InlineData("binary", true)]
    [InlineData("augmented", false)]
    [InlineData("augmented", true)]
    [InlineData("unary", false)]
    [InlineData("unary", true)]
    [InlineData("truth", false)]
    [InlineData("truth", true)]
    public async Task AProtocolOperandStillSuspendsWithoutReplay(string operation, bool cancel)
    {
        var (method, expression, expected) = operation switch
        {
            "binary" => ("__radd__", "print(1.5+Operand())", "3.5\n"),
            "augmented" => ("__radd__", "value=1.5\nvalue+=Operand()\nprint(value)", "3.5\n"),
            "unary" => ("__pos__", "print(+Operand())", "3.5\n"),
            _ => ("__bool__", "print(not Operand())", "False\n")
        };
        var source = $"class Operand:\n def {method}(self" + (method == "__radd__" ? ",other" : "") +
            $"):\n  with open('events.txt','a') as output:output.write('{operation}')\n  return " +
            (operation == "truth" ? "True" : "3.5") + "\nvalue=1.5\nvalue+=True\n" + expression;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/events.txt", "before");
        var entered = cancel ? host.PauseWriteUntilCancellation("/events.txt") : null;
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            if (entered is not null)
            {
                var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(10));
                if (ReferenceEquals(first, pending))
                {
                    using var early = await pending;
                    Assert.Fail(early.Failure?.Message ?? "Execution completed before the protocol operation suspended.");
                }
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                cancellation.Cancel();
            }
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(!cancel, result.Success);
            Assert.Equal(cancel ? "" : expected, result.StandardOutput);
            Assert.Equal(cancel ? "before" : "before" + operation, host.ReadText("/events.txt"));
            Assert.True(host.CompletedAsynchronously > 0);
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    // Expected stdout checked independently with isolated CPython 3.13.
    public static TheoryData<string, string, string> Programs => new()
    {
        { "float_arithmetic", "print(1.5+2.25,5.5-2.0,1.5*2.0,3.0/2.0,5.5//2.0,5.5%2.0,1.5**2)", "3.75 3.5 3.0 1.5 2.0 1.5 2.25\n" },
        { "float_augmented", "value=1.5\nvalue+=2.25\nvalue*=2.0\nvalue-=0.5\nvalue/=2.0\nprint(value)", "3.5\n" },
        { "bool_bitwise", "for a,b in ((True,False),(True,True),(False,False)):\n for value in (a&b,a|b,a^b):print(value,type(value).__name__)", "False bool\nTrue bool\nTrue bool\nTrue bool\nTrue bool\nFalse bool\nFalse bool\nFalse bool\nFalse bool\n" },
        { "mixed", "print(2+True,True+1.5,1.5+True,3*False,5.5//True,5.5%True)", "3 2.5 2.5 0 5.0 0.5\n" },
        { "bool_augmented", "value=True\nvalue&=False\nprint(value,type(value).__name__)\nvalue=True\nvalue+=2\nprint(value,type(value).__name__)", "False bool\n3 int\n" },
        { "unary", "print(+1.5,-1.5,not 0.0,not 1.5,+True,-False,not False)", "1.5 -1.5 True False 1 0 True\n" },
        { "negative_zero", "import math\na=-0.0\nprint(math.copysign(1.0,a),math.copysign(1.0,-a),bool(a))", "-1.0 1.0 False\n" },
        { "nonfinite", "a=float(\"nan\")\nb=float(\"inf\")\nprint(a==a,a!=a,not a,b>b,b==b,-b,+b)", "False True False False True -inf inf\n" },
        { "chain_true", "print(0.5<1.5<2.5,False<True<=1,0<True<1.5)", "True True True\n" },
        { "chain_false", "events=[]\ndef tail():\n events.append(\"tail\")\n return 9.5\nprint(2.5<1.5<tail(),events)", "False []\n" },
        { "chain_once", "events=[]\ndef middle():\n events.append(\"middle\")\n return 1.5\nprint(0.5<middle()<2.5,events)", "True ['middle']\n" },
        { "chain_nonbool", "class N:\n def __gt__(self,other):return [1]\nprint(0.5<1.5<N())", "[1]\n" },
        { "reflected", "class N:\n def __radd__(self,other):return other+2.5\nprint(1.5+N())", "4.0\n" },
        { "truth_protocol", "events=[]\nclass N:\n def __bool__(self):\n  events.append(\"truth\")\n  return True\nprint(not N(),events)", "False ['truth']\n" },
        { "divide_zero", "def op(value):return value/0.0\ntry:print(op(1.5))\nexcept Exception as error:print(type(error).__name__)", "ZeroDivisionError\n" },
        { "floor_zero", "def op(value):return value//0.0\ntry:print(op(1.5))\nexcept Exception as error:print(type(error).__name__)", "ZeroDivisionError\n" },
        { "modulo_zero", "def op(value):return value%0.0\ntry:print(op(1.5))\nexcept Exception as error:print(type(error).__name__)", "ZeroDivisionError\n" },
        { "bad_bitwise", "def op(value):return ~value\ntry:print(op(1.5))\nexcept Exception as error:print(type(error).__name__)", "TypeError\n" },
        { "bad_shift", "def op(value):return value<<1\ntry:print(op(1.5))\nexcept Exception as error:print(type(error).__name__)", "TypeError\n" },
        { "bad_matrix", "def op(value):return value@value\ntry:print(op(1.5))\nexcept Exception as error:print(type(error).__name__)", "TypeError\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ScalarOperationsKeepPythonResultsAndErrors(string label, string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, label);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
                Assert.Equal("", result.StandardError);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }
}

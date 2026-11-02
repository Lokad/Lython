using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeReturnFunctionTests
{
    // Goldens independently captured with isolated CPython 3.13.2.
    public static TheoryData<string, string, string> Cases => new()
    {
        { "native-conversions", "def integer(v):return int(v)\ndef floating(v):return float(v)\ndef text(v):return str(v)\ndef length(v):return len(v)\ndef absolute(v):return abs(v)\nprint(integer(' 42 '), integer(b'-3'), floating('1.25'))\nprint(text(123), length({'a':1}), absolute(-7))\n", "42 -3 1.25\n123 1 7\n" },
        { "dictionary-alias", "def pick(row):return row['value']\nitems=[]\nrow={'value':items}\npicked=pick(row)\npicked.append('ok')\nprint(picked is items, row['value'])\n", "True ['ok']\n" },
        { "key-error-context", "def pick(row):return row['value']\ntry:raise RuntimeError('outer')\nexcept RuntimeError as outer:\n try:pick({})\n except KeyError as error:print(error.args, error.__context__ is outer)\n", "('value',) True\n" },
        { "conversion-error-context", "def convert(v):return int(v)\ntry:raise RuntimeError('outer')\nexcept RuntimeError as outer:\n try:convert('bad')\n except ValueError as error:print(type(error).__name__, error.__context__ is outer)\n", "ValueError True\n" },
        { "builtin-rebinding", "def convert(v):return int(v)\noriginal=int\nint=lambda value:value+'!'\nprint(convert('4'))\nint=original\nprint(convert('4'))\n", "4!\n4\n" },
        { "local-callable", "def apply(operation,value):return operation(value)\nprint(apply(int,'7'))\nprint(apply(lambda value:value*2,7))\n", "7\n14\n" },
        { "nested-capture", "def outer():\n int=lambda value:value+'?'\n def convert(value):return int(value)\n return convert\nprint(outer()('5'))\n", "5?\n" },
        { "integer-protocol", "def convert(value):return int(value)\nclass Number:\n def __int__(self):\n  print('hook')\n  return 7\nprint(convert(Number()))\n", "hook\n7\n" },
        { "mapping-protocol", "def pick(row):return row['value']\nclass Row:\n def __getitem__(self,key):\n  print(key)\n  return 'custom'\nprint(pick(Row()))\n", "value\ncustom\n" },
        { "protocol-dictionary-key", "def pick(row):return row['value']\nclass Key:\n def __hash__(self):return hash('value')\n def __eq__(self,other):\n  print('equal')\n  return other=='value'\nrow={Key():9}\nprint(pick(row))\n", "equal\n9\n" },
        { "class-native-body", "class Convert:\n def number(self,value):return int(value)\nprint(Convert().number('12'))\n", "12\n" },
        { "explicit-global", "def outer():\n def convert(value):\n  global int\n  return int(value)\n return convert\nprint(outer()('23'))\n", "23\n" },
        { "compound-text", "def words(value):return value.lower().split()\ndef normalize(value):return value.strip().upper().replace('A','z')\ndef count(value):return len(value.lower().split(None,1))\ndef wanted(value):return value.strip().startswith('hello')\nprint(words(' A B '),normalize(' ab '),count('A B C'),wanted(' hello'))\n", "['a', 'b'] zB 2 True\n" },
        { "compound-numeric", "parse=int\ndef number(value):return abs(parse(value.strip())+3)\ndef text(value):return str(abs(value)+2).startswith('4')\nprint(number(' -8 '),text(-2))\n", "5 True\n" },
        { "compound-rebinding", "def count(value):return len(value.lower().split())\noriginal=len\nlen=lambda value:99\nprint(count('A B'))\nlen=original\nprint(count('A B'))\n", "99\n2\n" },
        { "compound-protocol-once", "def words(value):return value.lower().split()\nclass Text:\n def lower(self):\n  print('hook')\n  return 'A B'\nprint(words(Text()))\n", "hook\n['A', 'B']\n" },
        { "compound-error-order", "def number(value):return int(value.strip())+missing\ntry:number('bad')\nexcept ValueError:print('conversion first')\n", "conversion first\n" },
        { "unknown-mapping-result", "def count(row):return len(row['value'])\nclass Value:\n def __len__(self):\n  print('length hook')\n  return 3\nprint(count({'value':Value()}))\n", "length hook\n3\n" },
        { "compound-exception-context", "def number(value):return int(value.strip())+2\ntry:raise RuntimeError('outer')\nexcept RuntimeError as outer:\n try:number(' bad ')\n except ValueError as error:print(type(error).__name__,error.__context__ is outer)\n", "ValueError True\n" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task NativeReturnsPreservePythonBehavior(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                var options = new LythonRunOptions { MemoryBudget = budget };
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options)
                    : script.Run(new MockLythonHost(), options))
                {
                    Assert.True(result.Success, name + ": " + result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task CompoundReturnKeepsSuspendingReboundBuiltinAndCleanup()
    {
        const string source = "def count(value):return len(value.lower().split())\n" +
            "def length(value):\n with open('/pause') as file:\n  return int(file.read())\n" +
            "len=length\nprint(count('A B'))\nprint('late')\n";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost();
            host.SeedFile("/pause", "7");
            var reached = host.PauseReadUntilCancellation("/pause");
            using var cancellation = new CancellationTokenSource();
            var pending = script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget, CancellationToken = cancellation.Token });
            await reached.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            cancellation.Cancel();
            using (var result = await pending.WaitAsync(TimeSpan.FromSeconds(10)))
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                Assert.DoesNotContain("late", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void DeepNativeExpressionRemainsSupported()
    {
        // Beyond native-return eligibility, within the existing frontend chain bound.
        var expression = "value" + string.Concat(Enumerable.Repeat(".strip()", 20));
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile($"def clean(value):return {expression}\nprint(clean(' x '))", budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            using var result = script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("x\n", result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProtocolReturnRetainsItsOwnerUntilSuspendedInputCancels(bool mapping)
    {
        var definition = mapping
            ? "def helper(value):return value['key']\nclass Value:\n def __getitem__(self,key):\n"
            : "def helper(value):return int(value)\nclass Value:\n def __int__(self):\n";
        var source = definition + "  with open('/pause') as file:\n   value=file.read()\n" +
            (mapping ? "  return value\n" : "  return int(value)\n") + "print(helper(Value()))\nprint('late')\n";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost();
            host.SeedFile("/pause", "7");
            var reached = host.PauseReadUntilCancellation("/pause");
            using var cancellation = new CancellationTokenSource();
            var pending = script.RunAsync(host, new LythonRunOptions { MemoryBudget = budget, CancellationToken = cancellation.Token });
            await reached.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            cancellation.Cancel();
            using (var result = await pending)
            {
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                Assert.DoesNotContain("late", result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

}

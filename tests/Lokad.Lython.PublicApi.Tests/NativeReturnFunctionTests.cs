using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeReturnFunctionTests
{
    // Goldens independently captured with isolated CPython 3.13.
    public static TheoryData<string, string, string> Cases => new()
    {
        { "fused-siblings-and-forwarded-local", "def words(value):\n prepared=value.lower()\n return prepared.split()\ndef combined(left,right,size=len):\n return size(left.upper().split())+size(right.lower().split())\nprint(words('  İΣ ΣΙΣ ß\\u2003'))\nprint(combined(' ß ΣΙΣ ', ' İΣ '))\nprint(words(''),combined('', ''))\n", "['i̇ς', 'σις', 'ß']\n3\n[] 0\n" },
        { "canonical-closed-compositions", "def folded(value):return value.casefold().rsplit()\ndef capitalized(value):return value.capitalize().splitlines()\ndef titled(value):return value.title().lstrip().rstrip()\ndef swapped(value):return value.swapcase().expandtabs()\ndef encoded(value):return len(value.encode())\nprint(folded(' Aİß Σ C\\n'))\nprint(capitalized('élAN\\nΣΙΣ'))\nprint(titled('  élAN σΙΣ  '))\nprint(swapped('ß\\tÉ😀'))\nprint(encoded('A😀é'))\nprint(folded(''),capitalized(''),encoded(''))\n", "['ai̇ss', 'σ', 'c']\n['Élan', 'σις']\nÉlan Σις\nSS      é😀\n7\n[] [] 0\n" },
        { "canonical-closed-dynamic-receiver", "def folded(value):return value.casefold().rsplit()\nclass Subject:\n def casefold(self):\n  print('casefold once')\n  return self\n def rsplit(self):\n  print('rsplit once')\n  return ['custom']\nprint(folded(Subject()))\n", "casefold once\nrsplit once\n['custom']\n" },
        { "all-native-string-methods", "def helper_strip(value):return value.strip()\ndef helper_lower(value):return value.lower()\ndef helper_upper(value):return value.upper()\ndef helper_islower(value):return value.islower()\ndef helper_isupper(value):return value.isupper()\ndef helper_istitle(value):return value.istitle()\ndef helper_isalpha(value):return value.isalpha()\ndef helper_isdigit(value):return value.isdigit()\ndef helper_isalnum(value):return value.isalnum()\ndef helper_isascii(value):return value.isascii()\ndef helper_isdecimal(value):return value.isdecimal()\ndef helper_isidentifier(value):return value.isidentifier()\ndef helper_isnumeric(value):return value.isnumeric()\ndef helper_isprintable(value):return value.isprintable()\ndef helper_isspace(value):return value.isspace()\nfor value in [' aÉ2 ', 'é', '²', '\\x1c', 'ÉLAN', 'Élan']:\n print([helper_strip(value),helper_lower(value),helper_upper(value),helper_islower(value),helper_isupper(value),helper_istitle(value),helper_isalpha(value),helper_isdigit(value),helper_isalnum(value),helper_isascii(value),helper_isdecimal(value),helper_isidentifier(value),helper_isnumeric(value),helper_isprintable(value),helper_isspace(value)])\n", "['aÉ2', ' aé2 ', ' AÉ2 ', False, False, False, False, False, False, False, False, False, False, True, False]\n['é', 'é', 'É', True, False, False, True, False, True, False, False, True, False, True, False]\n['²', '²', '²', False, False, False, False, True, True, False, False, False, True, True, False]\n['', '\\x1c', '\\x1c', False, False, False, False, False, False, True, False, False, False, False, True]\n['ÉLAN', 'élan', 'ÉLAN', False, True, False, True, False, True, False, False, True, False, True, False]\n['Élan', 'élan', 'ÉLAN', False, False, True, True, False, True, False, False, True, False, True, False]\n" },
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
        { "shared-unary-aliases", "native=sum\ndef checksum(value):return native(value)\ndef apply(operation,value):return operation(value)\nprint(checksum(b'\\x01\\xff'),apply(abs,True),apply(float,b'1.5'),apply(str,42))\nnative=lambda value:99\nprint(checksum(b''))\nnative=sum\nprint(checksum([2,3]))\n", "256 1 1.5 42\n99\n5\n" },
        { "length-proof-boundary", "from collections import defaultdict,deque,ChainMap\ndef length(value):return len(value)\nprint(length('abc'),length(b'ab'),length([1]),length((1,2)),length({'a':1}),length({1,2}),length(range(7)),length(deque([1,2,3])))\nprint(length(defaultdict(int,{'x':1})),length(ChainMap({'x':1},{'y':2})))\nfor value in [None,3,iter([1])]:\n try:length(value)\n except TypeError:print('type')\n", "3 2 1 2 1 2 7 3\n1 2\ntype\ntype\ntype\n" },
        { "native-defaults-and-call-forms", "def clean(value=' A '):return value.strip()\ndef nothing():pass\nprint(clean(),clean(value=' B '),clean(*[' C ']),clean(**{'value':' D '}),nothing())\nclean.__defaults__=(' E ',)\nprint(clean(),list(map(clean,[' F ',' G '])))\n", "A B C D None\nE ['F', 'G']\n" },
        { "native-live-module-slots", "def add(value):return value+offset\noffset=3\nprint(add(2))\noffset=7\nprint(add(2))\ndef outer():\n offset=99\n return add(2)\nprint(outer())\n", "5\n9\n9\n" },
        { "native-defining-builtins", "def convert(value):return int(value)\ndef invoke():\n int=lambda value:99\n return convert('7')\nprint(invoke())\n", "7\n" },
        { "native-local-callable-default", "def convert(value,operation=int):return operation(value)\nprint(convert('7'),convert('8',str))\nconvert.__defaults__=(lambda value:99,)\nprint(convert('9'))\n", "7 8\n99\n" },
        { "forwarded-local-chain", "def words(value):\n normalized=value.lower()\n result=normalized.split()\n return result\ndef number(value):\n parsed=int(value)\n return parsed\nprint(words(' A B '),number('7'))\n", "['a', 'b'] 7\n" },
        { "forwarded-parameter-read-again", "def combine(value):\n value=value.strip()\n upper=value.upper()\n return upper+value\nprint(combine(' a '))\n", "Aa\n" },
        { "forwarded-parameter-reassignment", "def clean(value):\n value=value.strip()\n value=value.upper()\n return value\nprint(clean(' a '))\n", "A\n" },
        { "forwarded-indirect-locals", "class Result:\n @property\n def split(self):return locals\nclass Source:\n def lower(self):return Result()\ndef words(value):\n normalized=value.lower()\n result=normalized.split()\n return result\nprint(sorted(words(Source())))\n", "['normalized', 'value']\n" },
        { "forwarded-live-callable", "def convert(value):\n operation=int\n return operation(value)\noriginal=int\nint=lambda value:'changed'\nprint(convert('7'))\nint=original\nprint(convert('7'))\n", "changed\n7\n" },
        { "forwarded-error-order", "def number(value):\n parsed=int(value.strip())\n return parsed+missing\ntry:number('bad')\nexcept ValueError:print('conversion first')\n", "conversion first\n" },
        { "forwarded-retained-alias", "def pick(row):\n chosen=row['value']\n return chosen\nitems=[]\nreturned=pick({'value':items})\nreturned.append('ok')\nprint(returned is items,items)\n", "True ['ok']\n" },
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
    public async Task BorrowedReturnSurvivesOperandReuseAndCodeDisposal()
    {
        const string source = "def keep(value):return value\npayload='é'*4096\nheld=keep(payload)\n" +
            "for i in range(16):keep(str(i))\nprint(held is payload)\nreturn held\n";
        foreach (var asynchronous in new[] { false, true })
        {
            var budget = new LythonMemoryBudget(4 * 1024 * 1024);
            using var script = new LythonEngine().Compile(source, budget);
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(retained.Success, retained.Failure?.Message);
                Assert.Equal("True\n", retained.StandardOutput);
                var held = budget.ReservedBytes;
                Assert.True(held > baseline);
                using (var second = script.Run(new MockLythonHost()))
                {
                    Assert.True(second.Success, second.Failure?.Message);
                    Assert.Equal(retained.ReturnValue, second.ReturnValue);
                }
                Assert.Equal(held, budget.ReservedBytes);
                script.Dispose();
                Assert.True(budget.ReservedBytes > 0);
                Assert.Equal(new string('é', 4096), retained.ReturnValue);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }

    [Fact]
    public async Task CompoundReturnKeepsSuspendingReboundBuiltinAndCleanup()
    {
        const string source = "def count(value):\n result=len(value.lower().split())\n return result\n" +
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
    public async Task ImportedNativeFailureKeepsDefiningSourceAndFunctionFrame(bool asynchronous)
    {
        var host = new MockLythonHost();
        host.SeedFile("/helpers.py", "def convert(value):return int(value.strip())\n");
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile("from helpers import convert\nconvert(' bad ')\n", budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var options = new LythonRunOptions
            {
                SourcePath = "/main.py",
                AllowedLocalModules = new HashSet<string>(StringComparer.Ordinal) { "helpers" }
            };
            using (var result = asynchronous ? await script.RunAsync(host, options) : script.Run(host, options))
            {
                Assert.False(result.Success);
                Assert.Equal("ValueError", result.Failure?.ExceptionType);
                var frame = Assert.Single(result.Failure!.StackTrace);
                Assert.Equal("convert", frame.FunctionName);
                Assert.Equal("/helpers.py", frame.SourcePath);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
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

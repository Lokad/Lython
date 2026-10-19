using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeScalarDispatchTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetainedScalarResultKeepsItsOwner(bool text)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        var expression = text ? "left+right" : "(1<<4096)+True";
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile($"value={expression}\nalias=value\nvalue=None\nreturn alias", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions
        { Globals = text ? new Dictionary<string, object?> { ["left"] = "café", ["right"] = "😀" } : null };
        object expected = text ? "café😀" : (System.Numerics.BigInteger.One << 4096) + 1;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.True(retained.Success, retained.Failure?.Message);
                Assert.Equal(expected, retained.ReturnValue);
                var held = budget.ReservedBytes;
                Assert.True(held > baseline);
                using (var next = script.Run(new MockLythonHost(), options)) Assert.True(next.Success, next.Failure?.Message);
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
    [InlineData("text_binary", false)]
    [InlineData("text_binary", true)]
    [InlineData("text_augmented", false)]
    [InlineData("text_augmented", true)]
    [InlineData("text_left", false)]
    [InlineData("text_left", true)]
    [InlineData("absolute", false)]
    [InlineData("absolute", true)]
    [InlineData("operator_absolute", false)]
    [InlineData("operator_absolute", true)]
    [InlineData("operator_negative", false)]
    [InlineData("operator_negative", true)]
    [InlineData("operator_positive", false)]
    [InlineData("operator_positive", true)]
    [InlineData("operator_invert", false)]
    [InlineData("operator_invert", true)]
    [InlineData("operator_negative_descriptor", false)]
    [InlineData("operator_negative_descriptor", true)]
    [InlineData("absolute_argument", false)]
    [InlineData("absolute_argument", true)]
    [InlineData("split_count", false)]
    [InlineData("split_count", true)]
    [InlineData("split_count_exposed", false)]
    [InlineData("split_count_exposed", true)]
    [InlineData("split_count_keyword", false)]
    [InlineData("split_count_keyword", true)]
    [InlineData("split_count_expanded", false)]
    [InlineData("split_count_expanded", true)]
    [InlineData("split_count_descriptor", false)]
    [InlineData("split_count_descriptor", true)]
    [InlineData("split_argument", false)]
    [InlineData("split_argument", true)]
    public async Task AProtocolOperandStillSuspendsWithoutReplay(string operation, bool cancel)
    {
        var (method, expression, expected) = operation switch
        {
            "binary" => ("__radd__", "print(1.5+Operand())", "3.5\n"),
            "augmented" => ("__radd__", "value=1.5\nvalue+=Operand()\nprint(value)", "3.5\n"),
            "unary" => ("__pos__", "print(+Operand())", "3.5\n"),
            "text_binary" => ("__radd__", "print('left'+Operand())", "3.5\n"),
            "text_augmented" => ("__radd__", "value='left'\nvalue+=Operand()\nprint(value)", "3.5\n"),
            "text_left" => ("__add__", "print(Operand()+'right')", "3.5\n"),
            "absolute" => ("__abs__", "print(abs(Operand()))", "3.5\n"),
            "operator_absolute" => ("__abs__", "import operator\nprint(operator.abs(Operand()))", "3.5\n"),
            "operator_negative" => ("__neg__", "import operator\nprint(operator.neg(Operand()))", "3.5\n"),
            "operator_positive" => ("__pos__", "import operator\nprint(operator.pos(Operand()))", "3.5\n"),
            "operator_invert" => ("__invert__", "import operator\nprint(operator.invert(Operand()))", "3.5\n"),
            "operator_negative_descriptor" => ("__neg__", "import operator\nprint(operator.neg(Operand()))", "3.5\n"),
            "absolute_argument" => ("__abs__", "print(abs(abs(Operand())))", "3.5\n"),
            "split_count" => ("__index__", "print('a,b,c'.split(',',Operand()))", "['a', 'b,c']\n"),
            "split_count_exposed" => ("__index__", "method='a,b,c'.split\nprint(method(',',Operand()))", "['a', 'b,c']\n"),
            "split_count_keyword" => ("__index__", "print('a,b,c'.split(sep=',',maxsplit=Operand()))", "['a', 'b,c']\n"),
            "split_count_expanded" => ("__index__", "print('a,b,c'.split(*[',',Operand()]))", "['a', 'b,c']\n"),
            "split_count_descriptor" => ("__index__", "print('a,b,c'.split(',',Operand()))", "['a', 'b,c']\n"),
            "split_argument" => ("__index__", "print('a,b,c'.split(',',int(Operand())))", "['a', 'b,c']\n"),
            _ => ("__bool__", "print(not Operand())", "False\n")
        };
        var source = operation is "operator_negative_descriptor" or "split_count_descriptor"
            ? $"class Descriptor:\n def __get__(self,instance,owner):\n  with open('events.txt','a') as output:output.write('{operation}')\n  return lambda:{(method == "__index__" ? "1" : "3.5")}\nclass Operand:\n {method}=Descriptor()\n"
            : $"class Operand:\n def {method}(self" + (method is "__radd__" or "__add__" ? ",other" : "") +
                $"):\n  with open('events.txt','a') as output:output.write('{operation}')\n  return " +
                (method == "__index__" ? "1" : operation == "truth" ? "True" : "3.5") + "\n";
        source += "value=1.5\nvalue+=True\n" + expression;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
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
        { "abs_primitives", "import math\nprint(abs(-7),abs(7),abs(True),abs(False),abs(-1.25),abs(-0.0))\nprint(math.copysign(1.0,abs(-0.0)),abs(float('-inf')))", "7 7 1 0 1.25 0.0\n1.0 inf\n" },
        { "abs_numeric_types", "import operator,decimal,datetime\nfor call in (abs,operator.abs):print(call(3+4j),call(decimal.Decimal('-1.25')),call(datetime.timedelta(seconds=-2)))", "5.0 1.25 0:00:02\n5.0 1.25 0:00:02\n" },
        { "abs_protocol_values", "import operator\nclass Number:\n def __abs__(self):return 'custom'\nprint(abs(Number()),operator.abs(Number()))", "custom custom\n" },
        { "abs_missing_and_noncallable", "import operator\nclass Missing:pass\nclass Nil:__abs__=None\nfor call in (abs,operator.abs):\n for value in ('text',None,Missing(),Nil()):\n  try:call(value)\n  except Exception as error:print(type(error).__name__)", "TypeError\nTypeError\nTypeError\nTypeError\nTypeError\nTypeError\nTypeError\nTypeError\n" },
        { "abs_arity", "import operator\nfor call in (abs,operator.abs):\n for args in ((),(1,2)):\n  try:call(*args)\n  except Exception as error:print(type(error).__name__)", "TypeError\nTypeError\nTypeError\nTypeError\n" },
        { "abs_alias_and_expansion", "import operator\nsaved=abs\nprint(saved(-7),abs(*[-7]),operator.abs(-7))\nabs=lambda value:'custom'\nprint(abs(-7),saved(-7))", "7 7 7\ncustom 7\n" },
        { "abs_large_alias", "value=1<<4096\nprint(abs(value) is value,abs(-value)==value)\nsaved=abs\nabs=lambda value:'changed'\nprint(saved(value) is value,abs(value))", "True True\nTrue changed\n" },
        { "operator_neg_protocol", "import operator\nclass Number:\n def __neg__(self):return 'custom-neg'\nprint(operator.neg(Number()))", "custom-neg\n" },
        { "operator_pos_protocol", "import operator\nclass Number:\n def __pos__(self):return ['custom-pos']\nprint(operator.pos(Number()))", "['custom-pos']\n" },
        { "operator_invert_protocol", "import operator\nclass Number:\n def __invert__(self):return None\nprint(operator.invert(Number()))", "None\n" },
        { "operator_unary_complex", "import operator\nvalue=3+4j\nprint(operator.neg(value),operator.pos(value),operator.pos(value) is value)", "(-3-4j) (3+4j) True\n" },
        { "operator_unary_native", "import operator,math,decimal,datetime\nprint(operator.neg(True),operator.pos(False),operator.invert(1),operator.neg(1.5))\nvalue=-0.0\nprint(math.copysign(1.0,operator.neg(value)),math.copysign(1.0,operator.pos(value)))\nfor call in (operator.neg,operator.pos):print(call(decimal.Decimal('-1.25')),call(datetime.timedelta(seconds=-2)))", "-1 0 -2 -1.5\n1.0 -1.0\n1.25 0:00:02\n-1.25 -1 day, 23:59:58\n" },
        { "operator_unary_missing_and_noncallable", "import operator\nclass Missing:pass\nclass Nil:__neg__=None;__pos__=None;__invert__=None\nfor call in (operator.neg,operator.pos,operator.invert):\n for value in ('text',None,Missing(),Nil()):\n  try:call(value)\n  except Exception as error:print(type(error).__name__)", "TypeError\nTypeError\nTypeError\nTypeError\nTypeError\nTypeError\nTypeError\nTypeError\nTypeError\nTypeError\nTypeError\nTypeError\n" },
        { "operator_unary_protocol_errors", "import operator\nclass Number:\n def __neg__(self):raise ValueError('neg')\n def __pos__(self):raise ValueError('pos')\n def __invert__(self):raise ValueError('invert')\nfor call in (operator.neg,operator.pos,operator.invert):\n try:call(Number())\n except Exception as error:print(type(error).__name__,str(error))", "ValueError neg\nValueError pos\nValueError invert\n" },
        { "operator_unary_arity", "import operator\nfor call in (operator.neg,operator.pos,operator.invert):\n for args in ((),(1,2)):\n  try:call(*args)\n  except Exception as error:print(type(error).__name__)", "TypeError\nTypeError\nTypeError\nTypeError\nTypeError\nTypeError\n" },
        { "operator_unary_type_lookup", "import operator\nclass Number:\n def __neg__(self):return 'type-neg'\n def __pos__(self):return 'type-pos'\n def __invert__(self):return 'type-invert'\n def __abs__(self):return 'type-abs'\nclass Child(Number):pass\nvalue=Child()\nvalue.__neg__=lambda:'instance-neg'\nvalue.__pos__=lambda:'instance-pos'\nvalue.__invert__=lambda:'instance-invert'\nvalue.__abs__=lambda:'instance-abs'\nprint(operator.neg(value),operator.pos(value),operator.invert(value))\nprint(-value,+value,~value)\nprint(abs(value),operator.abs(value))", "type-neg type-pos type-invert\ntype-neg type-pos type-invert\ntype-abs type-abs\n" },
        { "unary_bypasses_getattribute", "import operator\nclass Number:\n def __getattribute__(self,name):raise AssertionError('instance lookup')\n def __neg__(self):return 7\n def __pos__(self):return 8\n def __invert__(self):return 9\n def __abs__(self):return 10\nvalue=Number()\nprint(-value,+value,~value,abs(value))\nprint(operator.neg(value),operator.pos(value),operator.invert(value),operator.abs(value))", "7 8 9 10\n7 8 9 10\n" },
    };

    [Theory]
    [InlineData(false, "abs", "__abs__")]
    [InlineData(true, "abs", "__abs__")]
    [InlineData(false, "operator.abs", "__abs__")]
    [InlineData(true, "operator.abs", "__abs__")]
    [InlineData(false, "operator.neg", "__neg__")]
    [InlineData(true, "operator.neg", "__neg__")]
    [InlineData(false, "operator.pos", "__pos__")]
    [InlineData(true, "operator.pos", "__pos__")]
    [InlineData(false, "operator.invert", "__invert__")]
    [InlineData(true, "operator.invert", "__invert__")]
    [InlineData(false, "str.split", "__index__")]
    [InlineData(true, "str.split", "__index__")]
    public async Task UnaryProtocolRefusalStopsGuestSuppressionAndReturnsAllowance(bool asynchronous, string operation, string method)
    {
        var call = operation == "str.split" ? "'a,b,c'.split(',',Number())" : operation + "(Number())";
        var source = "import operator\nclass Number:\n def " + method + "(self):\n  with open('/events.txt','a') as output:output.write('entered')\n  try:\n   payload='x'*750000\n  finally:\n   return 7\ntry:\n print(" + call + ")\nexcept MemoryError:\n print('swallowed')";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var delayed = asynchronous ? new DelayedLythonHost() : null;
            var mock = asynchronous ? null : new MockLythonHost();
            delayed?.SeedFile("/events.txt", "");
            mock?.SeedFile("/events.txt", "");
            var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(262144) };
            using (var result = asynchronous ? await script.RunAsync(delayed!, options) : script.Run(mock!, options))
            {
                Assert.False(result.Success);
                Assert.Equal("MemoryError", result.Failure?.ExceptionType);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Equal("", result.StandardOutput);
                Assert.Equal("entered", delayed is null ? mock!.ReadText("/events.txt") : delayed.ReadText("/events.txt"));
                if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("neg", "__neg__")]
    [InlineData("pos", "__pos__")]
    [InlineData("invert", "__invert__")]
    public async Task RetainedUnaryProtocolResultKeepsItsOwner(string operation, string method)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("import operator\nclass Number:\n def " + method + "(self):return 'x'*100000\nreturn operator." + operation + "(Number())", budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            foreach (var asynchronous in new[] { false, true })
            {
                using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
                {
                    Assert.True(retained.Success, retained.Failure?.Message);
                    Assert.Equal(100000, Assert.IsType<string>(retained.ReturnValue).Length);
                    var held = budget.ReservedBytes;
                    Assert.True(held > baseline);
                    using (var next = script.Run(new MockLythonHost())) Assert.True(next.Success, next.Failure?.Message);
                    Assert.Equal(held, budget.ReservedBytes);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ScalarOperationsKeepPythonResultsAndErrors(string label, string source, string expected)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
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

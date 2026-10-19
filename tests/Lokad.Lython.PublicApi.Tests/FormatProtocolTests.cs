using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FormatProtocolTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "instance-format-shadow", "class Value:\n def __format__(self,spec):return 'type:'+spec\nv=Value()\nv.__format__=lambda spec:'shadow:'+spec\nprint(format(v,'>4'),f'{v:>4}','{0:>4}'.format(v),v.__format__('>4'))", "type:>4 type:>4 type:>4 shadow:>4\n" },
        { "format-interception", "events=[]\nclass Value:\n def __format__(self,spec):return 'type'\n def __getattribute__(self,name):\n  events.append(name)\n  if name=='__format__':return lambda spec:'intercepted'\n  return object.__getattribute__(self,name)\nv=Value()\nevents=[]\nprint(format(v),f'{v:>4}','{0:>4}'.format(v))\nprint(events)", "type type type\n[]\n" },
        { "noncallable-format", "class Value:__format__=None\nfor convert in [format,lambda v:f'{v}',lambda v:'{}'.format(v)]:\n try:convert(Value());print('accepted')\n except TypeError:print('not-callable')", "not-callable\nnot-callable\nnot-callable\n" },
        { "nonstring-format", "class Value:\n def __format__(self,spec):return 7\nfor convert in [format,lambda v:f'{v}',lambda v:'{0:>4}'.format(v)]:\n try:convert(Value())\n except TypeError:print('nonstring')", "nonstring\nnonstring\nnonstring\n" },
        { "format-descriptor-errors", "class Descriptor:\n def __get__(self,instance,owner):raise AttributeError('binding')\nclass Value:__format__=Descriptor()\nfor convert in [format,lambda v:f'{v}',lambda v:'{0:>4}'.format(v)]:\n try:convert(Value())\n except AttributeError as error:print(str(error))", "binding\nbinding\nbinding\n" },
        { "explicit-format-control", "class Value:\n def __format__(self,spec):return 'type:'+spec\nv=Value()\nv.__format__=lambda spec:'shadow:'+spec\nprint(v.__format__('x'),Value.__format__(v,'x'),object.__format__(7,''))", "shadow:x type:x 7\n" },
        { "default-format-control", "class Value:\n def __str__(self):return 'text'\nv=Value()\nprint(format(v),f'{v}','{}'.format(v))\ntry:format(v,'>4')\nexcept TypeError:print('unsupported')", "text text text\nunsupported\n" },
        { "complete-csv-report-control", "import csv\nimport io\n\nclass Total:\n    def __init__(self):\n        self.amount = 0\n\n    def add(self, amount):\n        self.amount += amount\n\n    def __format__(self, spec):\n        return format(self.amount, spec)\n\ntotals = {}\naccepted = 0\nsource = io.StringIO('kind,amount\\nred,3\\nblue,2\\nred,-1\\nblue,5\\n')\nfor row in csv.DictReader(source):\n    amount = int(row['amount'])\n    if amount >= 0:\n        kind = row['kind']\n        if kind not in totals:\n            totals[kind] = Total()\n        totals[kind].add(amount)\n        accepted += 1\ntarget = io.StringIO()\ntarget.write('kind,total\\n')\nfor kind in sorted(totals):\n    target.write(f'{kind},{totals[kind]:04d}\\n')\nprint(target.getvalue(), end='')\nprint(accepted)\n", "kind,total\nblue,0007\nred,0003\n3\n" },
        { "complete-csv-report-type-protocol", "import csv\nimport io\n\nclass Total:\n    def __init__(self):\n        self.amount = 0\n\n    def __getattribute__(self, name):\n        if name.startswith('__'):\n            raise AttributeError(name)\n        return object.__getattribute__(self, name)\n\n    def add(self, amount):\n        self.amount += amount\n\n    def __format__(self, spec):\n        return format(self.amount, spec)\n\ntotals = {}\naccepted = 0\nsource = io.StringIO('kind,amount\\nred,3\\nblue,2\\nred,-1\\nblue,5\\n')\nfor row in csv.DictReader(source):\n    amount = int(row['amount'])\n    if amount >= 0:\n        kind = row['kind']\n        if kind not in totals:\n            totals[kind] = Total()\n        totals[kind].add(amount)\n        accepted += 1\ntarget = io.StringIO()\ntarget.write('kind,total\\n')\nfor kind in sorted(totals):\n    target.write(f'{kind},{totals[kind]:04d}\\n')\nprint(target.getvalue(), end='')\nprint(accepted)\n", "kind,total\nblue,0007\nred,0003\n3\n" },
        { "empty-format-fields", "class Value:\n def __format__(self,spec):return 'format:'+spec\n def __str__(self):return 'text'\nv=Value()\nprint(format(v),f'{v}','{}'.format(v),'{value}'.format_map({'value':v}),v.__format__(''))", "format: format: format: format: format:\n" },
        { "complete-csv-empty-fields", "import csv\nimport io\n\nclass Total:\n    def __init__(self):\n        self.amount = 0\n\n    def add(self, amount):\n        self.amount += amount\n\n    def __format__(self, spec):\n        return format(self.amount, spec)\n\ntotals = {}\naccepted = 0\nsource = io.StringIO('kind,amount\\nred,3\\nblue,2\\nred,-1\\nblue,5\\n')\nfor row in csv.DictReader(source):\n    amount = int(row['amount'])\n    if amount >= 0:\n        kind = row['kind']\n        if kind not in totals:\n            totals[kind] = Total()\n        totals[kind].add(amount)\n        accepted += 1\ntarget = io.StringIO()\ntarget.write('kind,total\\n')\nfor kind in sorted(totals):\n    target.write('{},{}\\n'.format(kind, totals[kind]))\nprint(target.getvalue(), end='')\nprint(accepted)\n", "kind,total\nblue,7\nred,3\n3\n" },
        { "explicit-conversion-fields", "events=[]\nclass Value:\n def __format__(self,spec):events.append('format:'+spec);return 'F'\n def __str__(self):events.append('str');return '\u00e9'\n def __repr__(self):events.append('repr');return 'r\u00e9'\nv=Value()\nprint('{0!s}|{0!r}|{0!a}'.format(v))\nprint('{v!s:>3}'.format_map({'v':v}))\nprint(events)", "\u00e9|r\u00e9|r\\xe9\n  \u00e9\n['str', 'repr', 'repr', 'str']\n" },
        { "specifier-before-binding", "events=[]\ndef mark(name,value):events.append(name);return value\nclass Descriptor:\n def __get__(self,instance,owner):\n  events.append('bind')\n  def render(spec):events.append('call:'+spec);return 'ok'\n  return render\nclass Value:__format__=Descriptor()\nprint(format(mark('value',Value()),mark('spec','>4')),events)", "ok ['value', 'spec', 'bind', 'call:>4']\n" },
        { "invalid-specifier-before-binding", "events=[]\ndef mark(name,value):events.append(name);return value\nclass Descriptor:\n def __get__(self,instance,owner):events.append('bind');return lambda spec:'ok'\nclass Value:__format__=Descriptor()\ntry:format(mark('value',Value()),mark('spec',7))\nexcept TypeError:print(events)", "['value', 'spec']\n" },
        { "static-class-inherited-format", "class Static:\n @staticmethod\n def __format__(spec):return 'static:'+spec\nclass Base:\n @classmethod\n def __format__(cls,spec):return cls.__name__+':'+spec\nclass Child(Base):pass\na=Static()\nb=Child()\na.__format__=lambda spec:'shadow'\nb.__format__=lambda spec:'shadow'\nprint(format(a,'s'),format(b,'c'),b.__format__('x'))", "static:s Child:c shadow\n" },
        { "format-binding-errors-propagate", "class Descriptor:\n def __get__(self,instance,owner):raise error_type('binding')\nclass Value:__format__=Descriptor()\nfor error_type in [AttributeError,ValueError,MemoryError]:\n for convert in [format,lambda v:f'{v}',lambda v:'{}'.format(v),lambda v:'{v}'.format_map({'v':v})]:\n  try:convert(Value())\n  except Exception as error:print(type(error).__name__,str(error))", "AttributeError binding\nAttributeError binding\nAttributeError binding\nAttributeError binding\nValueError binding\nValueError binding\nValueError binding\nValueError binding\nMemoryError binding\nMemoryError binding\nMemoryError binding\nMemoryError binding\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ImplicitFormattingUsesTypeSlotsIncludingEmptyFields(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, name + ": " + string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    internal static string IoSource(string slot, string conversion, bool descriptor)
    {
        const string method = """
class Value:
 def SLOT(selfPARAMETERS):
  with open('call.txt','a') as target:target.write('call')
  return 'rendered'
print(CONVERT(Value()))
""";
        const string binding = """
class Descriptor:
 def __get__(self,instance,owner):
  with open('bind.txt','a') as target:target.write('bind')
  def render(PARAMETERS):
   with open('call.txt','a') as target:target.write('call')
   return 'rendered'
  return render
class Value:SLOT=Descriptor()
print(CONVERT(Value()))
""";
        var parameters = slot == "__format__" ? (descriptor ? "spec" : ",spec") : "";
        return (descriptor ? binding : method).Replace("SLOT", slot).Replace("PARAMETERS", parameters)
            .Replace("CONVERT", conversion);
    }

    [Theory]
    [InlineData("__format__", "format", false)]
    [InlineData("__format__", "format", true)]
    [InlineData("__str__", "format", false)]
    [InlineData("__str__", "format", true)]
    [InlineData("__str__", "(lambda value:f'{value}')", false)]
    [InlineData("__format__", "(lambda value:f'{value}')", true)]
    [InlineData("__str__", "(lambda value:object.__format__(value,''))", false)]
    [InlineData("__str__", "(lambda value:value.__format__(''))", false)]
    public async Task RootFormattingAwaitsProtocolAndDescriptorIo(string slot, string conversion, bool descriptor)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(IoSource(slot, conversion, descriptor), budget);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            var delayed = asynchronous ? new DelayedLythonHost() : null;
            var mock = asynchronous ? null : new MockLythonHost();
            foreach (var path in new[] { "/call.txt", "/bind.txt" })
            {
                if (delayed is not null) delayed.SeedFile(path, "before");
                else mock!.SeedFile(path, "before");
            }
            using (var result = asynchronous ? await script.RunAsync(delayed!) : script.Run(mock!))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("rendered\n", result.StandardOutput);
                Assert.Equal("beforecall", delayed?.ReadText("/call.txt") ?? mock!.ReadText("/call.txt"));
                Assert.Equal(descriptor ? "beforebind" : "before", delayed?.ReadText("/bind.txt") ?? mock!.ReadText("/bind.txt"));
                if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("(lambda value:'{}'.format(value))")]
    [InlineData("(lambda value:'{0:>4}'.format(value))")]
    [InlineData("(lambda value:'{v}'.format_map({'v':value}))")]
    [InlineData("(lambda value:'{v:>4}'.format_map({'v':value}))")]
    public async Task StringMethodCallbacksKeepTheirExplicitSynchronousBoundary(string conversion)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(IoSource("__format__", conversion, false), budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var mock = new MockLythonHost();
        mock.SeedFile("/call.txt", "before");
        using (var result = script.Run(mock))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("rendered\n", result.StandardOutput);
            Assert.Equal("beforecall", mock.ReadText("/call.txt"));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        var delayed = new DelayedLythonHost();
        delayed.SeedFile("/call.txt", "before");
        using (var result = await script.RunAsync(delayed))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.HostCapabilityUnavailable, result.FailureAttribution?.Reason);
            Assert.Contains("cannot run synchronously", result.Failure?.Message);
            Assert.Empty(result.StandardOutput);
            Assert.Equal("before", delayed.ReadText("/call.txt"));
            Assert.Equal(0, delayed.CompletedAsynchronously);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("__format__", "format", false, "call")]
    [InlineData("__format__", "format", true, "bind")]
    [InlineData("__str__", "format", false, "call")]
    [InlineData("__str__", "format", true, "bind")]
    [InlineData("__str__", "(lambda value:f'{value}')", false, "call")]
    [InlineData("__str__", "(lambda value:object.__format__(value,''))", false, "call")]
    [InlineData("__str__", "(lambda value:value.__format__(''))", false, "call")]
    public async Task CancellationDuringFormattingDrainsBeforeRefund(string slot, string conversion, bool descriptor, string paused)
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(IoSource(slot, conversion, descriptor), budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var host = new DelayedLythonHost();
        host.SeedFile("/call.txt", "before");
        host.SeedFile("/bind.txt", "before");
        var entered = host.PauseWriteUntilCancellation("/" + paused + ".txt");
        using var cancellation = new CancellationTokenSource();
        var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
        try
        {
            var first = await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5));
            if (ReferenceEquals(first, pending))
            {
                using var early = await pending;
                Assert.Fail(early.Failure?.Message ?? "Formatting completed before the selected I/O suspended.");
            }
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            cancellation.Cancel();
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
            Assert.Equal("before", host.ReadText("/call.txt"));
            Assert.Equal("before", host.ReadText("/bind.txt"));
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("__format__", "format", false)]
    [InlineData("__format__", "format", true)]
    [InlineData("__str__", "format", false)]
    [InlineData("__str__", "format", true)]
    [InlineData("__format__", "(lambda value:'{}'.format(value))", false)]
    [InlineData("__format__", "(lambda value:'{v}'.format_map({'v':value}))", false)]
    public async Task RefusedFormatStorageCannotFallThroughOrRecover(string slot, string conversion, bool descriptor)
    {
        const string binding = """
class Descriptor:
 def __get__(self,instance,owner):
  refuse()
  return lambda *args:'rendered'
class Value:SLOT=Descriptor()
""";
        const string method = """
class Value:
 def SLOT(self,*args):
  refuse()
  return 'rendered'
""";
        var source = "def refuse():\n print('before')\n try:unused=[0]*8192\n except BaseException:pass\n" +
            (descriptor ? binding : method).Replace("SLOT", slot) +
            "\ntry:print(" + conversion + "(Value()))\nexcept BaseException:print('caught')\nprint('after')";
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(64 * 1024) };
        foreach (var asynchronous in new[] { false, true })
        {
            using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.False(result.Success);
                Assert.Equal("MemoryError", result.Failure?.ExceptionType);
                Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
                Assert.Equal("before\n", result.StandardOutput);
                Assert.True(result.DeniedReservationBytes > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("__format__", "format", true)]
    [InlineData("__str__", "format", true)]
    [InlineData("__str__", "(lambda value:object.__format__(value,''))", true)]
    [InlineData("__str__", "(lambda value:f'{value}')", true)]
    [InlineData("__format__", "(lambda value:'{}'.format(value))", false)]
    [InlineData("__format__", "(lambda value:'{v}'.format_map({'v':value}))", false)]
    public async Task RetainedFormatResultsStayFundedAcrossSharedExecutions(string slot, string conversion, bool io)
    {
        var source = "class Value:\n def " + slot + "(self,*args):\n" +
            (io ? "  with open('call.txt','a') as target:target.write('call')\n" : "") +
            "  return self.payload\nv=Value()\nv.payload='é'*4096\nreturn " + conversion + "(v)";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var mock = new MockLythonHost();
        mock.SeedFile("/call.txt", "before");
        using (var first = script.Run(mock))
        {
            Assert.True(first.Success, first.Failure?.Message);
            var expected = new string('é', 4096);
            Assert.Equal(expected, Assert.IsType<string>(first.ReturnValue));
            var held = budget.ReservedBytes;
            Assert.True(held > baseline);
            var delayed = new DelayedLythonHost();
            delayed.SeedFile("/call.txt", "before");
            using (var second = await script.RunAsync(delayed))
            {
                Assert.True(second.Success, second.Failure?.Message);
                Assert.Equal(expected, second.ReturnValue);
                Assert.True(budget.ReservedBytes > held);
                if (io) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(held, budget.ReservedBytes);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("__format__", "format")]
    [InlineData("__str__", "format")]
    [InlineData("__str__", "(lambda value:f'{value}')")]
    [InlineData("__str__", "(lambda value:object.__format__(value,''))")]
    public async Task RecursiveFormatKeepsManagedAdmissionAfterIo(string slot, string conversion)
    {
        var source = "entered=False\nclass Value:\n def " + slot + "(self,*args):\n  global entered\n  if not entered:\n   entered=True\n   with open('call.txt','a') as target:target.write('call')\n  return " + conversion + "(self)\n" + conversion + "(Value())";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MaxRecursionDepth = 16 };
        foreach (var asynchronous in new[] { false, true })
        {
            var delayed = asynchronous ? new DelayedLythonHost() : null;
            var mock = asynchronous ? null : new MockLythonHost();
            if (delayed is not null) delayed.SeedFile("/call.txt", "before");
            else mock!.SeedFile("/call.txt", "before");
            using (var result = asynchronous ? await script.RunAsync(delayed!, options) : script.Run(mock!, options))
            {
                Assert.False(result.Success);
                Assert.Contains(result.FailureAttribution?.Reason, new LythonFailureReason?[] { LythonFailureReason.RecursionDepthLimit, LythonFailureReason.InterpreterStackLimit });
                Assert.Equal("beforecall", delayed?.ReadText("/call.txt") ?? mock!.ReadText("/call.txt"));
                if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("format(total,'04d')")]
    [InlineData("f'{total:04d}'")]
    public async Task CompleteCsvReportFormatsTotalsThroughHostIo(string conversion)
    {
        const string source = """
import csv
class Total:
 def __init__(self):self.amount=0
 def add(self,amount):self.amount+=amount
 def __format__(self,spec):
  with open('prefix.txt') as source:prefix=source.read()
  return prefix+format(self.amount,spec)
totals={}
accepted=0
with open('input.csv',newline='') as source:
 for row in csv.DictReader(source):
  amount=int(row['amount'])
  if amount>=0:
   kind=row['kind']
   if kind not in totals:totals[kind]=Total()
   totals[kind].add(amount)
   accepted+=1
lines=['kind,total']
for kind in sorted(totals):
 total=totals[kind]
 lines.append(kind+','+CONVERT)
with open('report.csv','w',newline='') as target:target.write('\n'.join(lines)+'\n')
print(accepted)
""";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source.Replace("CONVERT", conversion), budget);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            var delayed = asynchronous ? new DelayedLythonHost() : null;
            var mock = asynchronous ? null : new MockLythonHost();
            var inputs = new Dictionary<string, string>
            {
                ["/input.csv"] = "kind,amount\nred,3\nblue,2\nred,-1\nblue,5\n",
                ["/prefix.txt"] = "EUR ",
                ["/report.csv"] = "previous report",
            };
            foreach (var (path, text) in inputs)
            {
                if (delayed is not null) delayed.SeedFile(path, text);
                else mock!.SeedFile(path, text);
            }
            using (var result = asynchronous ? await script.RunAsync(delayed!) : script.Run(mock!))
            {
                Assert.True(result.Success, result.Failure?.Message);
                Assert.Equal("3\n", result.StandardOutput);
                Assert.Equal("kind,total\nblue,EUR 0007\nred,EUR 0003\n", delayed?.ReadText("/report.csv") ?? mock!.ReadText("/report.csv"));
                if (delayed is not null) Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }
}

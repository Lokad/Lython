using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StringProtocolTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "instance-str-shadow", "class Value:\n def __str__(self):return 'type'\nvalue=Value()\nvalue.__str__=lambda:'instance'\nprint(str(value),value.__str__())", "type instance\n" },
        { "getattribute-str-interception", "events=[]\nclass Value:\n def __str__(self):return 'type'\n def __getattribute__(self,name):\n  events.append(name)\n  if name=='__str__':return lambda:'intercepted'\n  return object.__getattribute__(self,name)\nvalue=Value()\nevents=[]\nprint(str(value))\nprint(events)", "type\n[]\n" },
        { "instance-repr-shadow", "class Value:\n def __repr__(self):return 'type-repr'\nvalue=Value()\nvalue.__repr__=lambda:'instance-repr'\nprint(str(value),repr(value),value.__repr__())", "type-repr type-repr instance-repr\n" },
        { "nonstring-return-control", "class Value:\n def __str__(self):return 7\ntry:str(Value())\nexcept TypeError:print('nonstring')", "nonstring\n" },
        { "alias-call-control", "class Value:\n def __str__(self):return 'type'\nvalue=Value()\nconvert=str\nprint(convert(value))", "type\n" },
        { "noncallable-str-slot", "class Value:__str__=None\ntry:print(str(Value()));print('accepted')\nexcept TypeError:print('not-callable')", "not-callable\n" },
        { "noncallable-repr-slot", "class Value:__repr__=None\ntry:print(repr(Value()));print('accepted')\nexcept TypeError:print('not-callable')", "not-callable\n" },
        { "ascii-and-field-shadows", "class Value:\n def __str__(self):return 'type-\u00e9'\n def __repr__(self):return 'repr-\u00e9'\nv=Value()\nv.__str__=lambda:'shadow'\nv.__repr__=lambda:'shadow-repr'\nprint(str(v),repr(v),ascii(v))\nprint(f'{v!s}|{v!r}|{v!a}')", "type-\u00e9 repr-\u00e9 repr-\\xe9\ntype-\u00e9|repr-\u00e9|repr-\\xe9\n" },
        { "inherited-slot", "class Base:\n def __str__(self):return 'base'\nclass Child(Base):pass\nv=Child()\nv.__str__=lambda:'shadow'\nprint(str(v),v.__str__())", "base shadow\n" },
        { "descriptor-slot", "events=[]\nclass Descriptor:\n def __get__(self,instance,owner):events.append('bind');return lambda:'slot'\nclass Value:__str__=Descriptor()\nv=Value()\nv.__str__=lambda:'shadow'\nprint(str(v),events,v.__str__())", "slot ['bind'] shadow\n" },
        { "static-and-class-slots", "class Static:\n @staticmethod\n def __str__():return 'static'\nclass Class:\n @classmethod\n def __repr__(cls):return cls.__name__\na=Static()\nb=Class()\na.__str__=lambda:'shadow'\nb.__repr__=lambda:'shadow-repr'\nprint(str(a),repr(b),a.__str__(),b.__repr__())", "static Class shadow shadow-repr\n" },
        { "explicit-base-str", "class Value:\n def __repr__(self):return 'type-repr'\n def __str__(self):return 'type-str'\nv=Value()\nv.__repr__=lambda:'shadow-repr'\nprint(object.__str__(v),str(v),v.__repr__())\nprint(isinstance(object.__repr__(v),str))", "type-repr type-str shadow-repr\nTrue\n" },
        { "container-repr-shadow", "class Value:\n def __repr__(self):return 'type'\nv=Value()\nv.__repr__=lambda:'shadow'\nprint([v],{'x':v},(v,))", "[type] {'x': type} (type,)\n" },
        { "descriptor-error", "events=[]\nclass Descriptor:\n def __get__(self,instance,owner):events.append('bind');raise AttributeError('binding')\nclass Value:__repr__=Descriptor()\ntry:repr(Value())\nexcept AttributeError as error:print(str(error),events)", "" },
        { "dataclass-and-container-cycles", "from dataclasses import dataclass\n@dataclass\nclass Value:\n number:int\nv=Value(7)\nprint(str(v),repr(v))\nitems=[]\nitems.append(items)\nprint(str(items),repr(items))\nd={}\nd['self']=d\nprint(repr(d))", "Value(number=7) Value(number=7)\n[[...]] [[...]]\n{'self': {...}}\n" },
        { "repr-binding-error-fallback", "events=[]\nclass Descriptor:\n def __get__(self,instance,owner):events.append('bind');raise error_type('binding')\nclass Value:__repr__=Descriptor()\nfor error_type in [AttributeError,ValueError,MemoryError]:\n events=[]\n v=Value()\n print(repr(v).startswith('<'),str(v).startswith('<'),ascii(v).startswith('<'),f'{v!r}'.startswith('<'),f'{v!a}'.startswith('<'),events)", "True True True True True ['bind', 'bind', 'bind', 'bind', 'bind']\nTrue True True True True ['bind', 'bind', 'bind', 'bind', 'bind']\nTrue True True True True ['bind', 'bind', 'bind', 'bind', 'bind']\n" },
        { "str-binding-error-propagates", "events=[]\nclass Descriptor:\n def __get__(self,instance,owner):events.append('bind');raise AttributeError('binding')\nclass Value:__str__=Descriptor()\ntry:str(Value())\nexcept AttributeError as error:print(str(error),events)", "binding ['bind']\n" },
        { "repr-body-error-propagates", "events=[]\nclass Value:\n def __repr__(self):events.append('call');raise AttributeError('body')\ntry:repr(Value())\nexcept AttributeError as error:print(str(error),events)", "body ['call']\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ConversionsUseTypeSlotsAndPreservePythonErrors(string name, string source, string expected)
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
 def SLOT(self):
  with open('call.txt','a') as target:target.write('call')
  return 'rendered'
print(CONVERT(Value()))
""";
        const string binding = """
class Descriptor:
 def __get__(self,instance,owner):
  with open('bind.txt','a') as target:target.write('bind')
  def render():
   with open('call.txt','a') as target:target.write('call')
   return 'rendered'
  return render
class Value:SLOT=Descriptor()
print(CONVERT(Value()))
""";
        return (descriptor ? binding : method).Replace("SLOT", slot).Replace("CONVERT", conversion);
    }

    [Theory]
    [InlineData("__str__", "str", false)]
    [InlineData("__repr__", "repr", false)]
    [InlineData("__repr__", "str", false)]
    [InlineData("__str__", "str", true)]
    [InlineData("__repr__", "repr", true)]
    [InlineData("__repr__", "ascii", false)]
    [InlineData("__repr__", "ascii", true)]
    [InlineData("__str__", "(lambda value:f'{value!s}')", false)]
    [InlineData("__repr__", "(lambda value:f'{value!r}')", true)]
    [InlineData("__repr__", "(lambda value:f'{value!a}')", true)]
    public async Task RootConversionAwaitsProtocolAndDescriptorIo(string slot, string conversion, bool descriptor)
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
    [InlineData("__str__", "str", false, "call")]
    [InlineData("__repr__", "repr", false, "call")]
    [InlineData("__repr__", "str", false, "call")]
    [InlineData("__repr__", "ascii", true, "bind")]
    [InlineData("__repr__", "repr", true, "bind")]
    [InlineData("__str__", "str", true, "bind")]
    [InlineData("__repr__", "(lambda value:f'{value!r}')", true, "bind")]
    public async Task CancellationDuringBindingOrCallCannotBecomeDefaultRepr(string slot, string conversion, bool descriptor, string paused)
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
                Assert.Fail(early.Failure?.Message ?? "Conversion completed before the selected I/O suspended.");
            }
            Assert.False(pending.IsCompleted);
            Assert.True(budget.ReservedBytes > baseline);
            cancellation.Cancel();
            using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
            Assert.Equal("before", host.ReadText("/call.txt"));
            Assert.Equal(descriptor && paused == "call" ? "beforebind" : "before", host.ReadText("/bind.txt"));
        }
        finally
        {
            cancellation.Cancel();
            using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("str", false)]
    [InlineData("repr", false)]
    [InlineData("ascii", false)]
    [InlineData("str", true)]
    [InlineData("repr", true)]
    [InlineData("ascii", true)]
    public async Task RefusedBindingOrBodyStorageCannotFallThroughOrRecover(string conversion, bool descriptor)
    {
        const string binding = """
class Descriptor:
 def __get__(self,instance,owner):
  refuse()
  return lambda:'rendered'
class Value:__repr__=Descriptor()
""";
        const string method = """
class Value:
 def __repr__(self):
  refuse()
  return 'rendered'
""";
        var source = "def refuse():\n print('before')\n try:unused=[0]*8192\n except BaseException:pass\n" +
            (descriptor ? binding : method) + "\ntry:print(" + conversion + "(Value()))\nexcept BaseException:print('caught')\nprint('after')";
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
    [InlineData("__str__", "str")]
    [InlineData("__repr__", "repr")]
    [InlineData("__repr__", "ascii")]
    public async Task CallbackResultsKeepTheirOwnersAcrossSharedExecutions(string slot, string conversion)
    {
        var source = "class Value:\n def " + slot + "(self):\n  with open('call.txt','a') as target:target.write('call')\n  return self.payload\nv=Value()\nv.payload='é'*4096\nreturn " + conversion + "(v)";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var mock = new MockLythonHost();
        mock.SeedFile("/call.txt", "before");
        using (var first = script.Run(mock))
        {
            Assert.True(first.Success, first.Failure?.Message);
            var expected = conversion == "ascii" ? string.Concat(Enumerable.Repeat("\\xe9", 4096)) : new string('é', 4096);
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
                Assert.True(delayed.CompletedAsynchronously > 0);
            }
            Assert.Equal(held, budget.ReservedBytes);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("__str__", "str", false)]
    [InlineData("__repr__", "repr", false)]
    [InlineData("__repr__", "ascii", false)]
    [InlineData("__repr__", "repr", true)]
    public async Task RecursiveRenderingKeepsManagedAdmissionAfterIo(string slot, string conversion, bool descriptor)
    {
        var body = "global entered\n  if not entered:\n   entered=True\n   with open('call.txt','a') as target:target.write('call')\n  return " + conversion + "(" + (descriptor ? "instance" : "self") + ")";
        var source = descriptor
            ? "entered=False\nclass Descriptor:\n def __get__(self,instance,owner):\n  " + body + "\nclass Value:" + slot + "=Descriptor()\n" + conversion + "(Value())"
            : "entered=False\nclass Value:\n def " + slot + "(self):\n  " + body + "\n" + conversion + "(Value())";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, string.Join(" | ", script.Diagnostics.Select(d => d.Message)));
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
}

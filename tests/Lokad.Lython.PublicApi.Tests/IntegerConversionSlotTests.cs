using System.Numerics;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class IntegerConversionSlotTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "int_instance_shadow", "class Value:\n def __int__(self): return 7\nvalue=Value()\nvalue.__int__=lambda:99\ntry:\n print((value.__int__(), int(value)))\nexcept Exception as error:\n print(type(error).__name__, str(error))\n", "(99, 7)\n" },
        { "attribute_hook_bypassed", "class Value:\n def __getattribute__(self,name):\n  if name=='__int__': raise RuntimeError('hooked')\n  return object.__getattribute__(self,name)\n def __int__(self): return 7\nvalue=Value()\ntry:\n print(int(value))\nexcept Exception as error:\n print(type(error).__name__, str(error))\n", "7\n" },
        { "index_instance_shadow", "class Value:\n def __index__(self): return 7\nvalue=Value()\nvalue.__index__=None\ntry:\n print((value.__index__ is None, int(value)))\nexcept Exception as error:\n print(type(error).__name__, str(error))\n", "(True, 7)\n" },
        { "instance_hook_without_type_slot", "class Value: pass\nvalue=Value()\nvalue.__int__=lambda:99\ntry:\n print(int(value))\nexcept Exception as error:\n print(type(error).__name__, str(error))\n", "TypeError int() argument must be a string, a bytes-like object or a real number, not 'Value'\n" },
        { "getattr_is_not_a_protocol", "class Value:\n def __getattr__(self,name):\n  print('lookup')\n  return lambda:99\nvalue=Value()\ntry:\n print(int(value))\nexcept Exception as error:\n print(type(error).__name__, str(error))\n", "TypeError int() argument must be a string, a bytes-like object or a real number, not 'Value'\n" },
        { "inherited_slot", "class Base:\n def __int__(self): return 7\nclass Value(Base): pass\nvalue=Value()\ntry:\n print(int(value))\nexcept Exception as error:\n print(type(error).__name__, str(error))\n", "7\n" },
        { "static_slot", "class Value:\n @staticmethod\n def __int__(): return 7\nvalue=Value()\ntry:\n print(int(value))\nexcept Exception as error:\n print(type(error).__name__, str(error))\n", "7\n" },
        { "class_slot", "class Value:\n marker=7\n @classmethod\n def __int__(cls): return cls.marker\nvalue=Value()\ntry:\n print(int(value))\nexcept Exception as error:\n print(type(error).__name__, str(error))\n", "7\n" },
        { "property_int", "events=[]\nclass Value:\n @property\n def __int__(self):\n  events.append('bind')\n  def convert():\n   events.append('call')\n   return 7\n  return convert\nvalue=Value()\ntry:\n print((int(value), events))\nexcept Exception as error:\n print(type(error).__name__, str(error))\n", "(7, ['bind', 'call'])\n" },
        { "property_index_bound_once", "events=[]\nclass Value:\n @property\n def __index__(self):\n  events.append('bind')\n  return lambda:7\nvalue=Value()\ntry:\n print((int(value), events))\nexcept Exception as error:\n print(type(error).__name__, str(error))\n", "(7, ['bind'])\n" },
        { "noncallable_int_blocks_fallback", "class Value:\n __int__=None\n def __index__(self): return 7\nvalue=Value()\ntry:\n print(int(value))\nexcept Exception as error:\n print(type(error).__name__, str(error))\n", "TypeError 'NoneType' object is not callable\n" },
        { "bad_int_result", "class Value:\n def __int__(self): return 'bad'\nvalue=Value()\ntry:\n print(int(value))\nexcept Exception as error:\n print(type(error).__name__, str(error))\n", "TypeError __int__ returned non-int (type str)\n" },
        { "bad_index_result", "class Value:\n def __index__(self): return 'bad'\nvalue=Value()\ntry:\n print(int(value))\nexcept Exception as error:\n print(type(error).__name__, str(error))\n", "TypeError __index__ returned non-int (type str)\n" },
        { "trunc_instance_shadow", "class Value:\n def __trunc__(self): return 7\nvalue=Value()\nvalue.__trunc__=lambda:99\ntry:\n print(int(value))\nexcept Exception as error:\n print(type(error).__name__, str(error))\n", "7\n" },
        { "trunc_result_index_shadow", "class Indexed:\n def __index__(self): return 7\nindexed=Indexed()\nindexed.__index__=None\nclass Value:\n def __trunc__(self): return indexed\nvalue=Value()\ntry:\n print(int(value))\nexcept Exception as error:\n print(type(error).__name__, str(error))\n", "7\n" },
        { "base_index_instance_shadow", "class Value:\n def __index__(self): return 2\nvalue=Value()\nvalue.__index__=lambda:16\ntry:\n print(int('11', value))\nexcept Exception as error:\n print(type(error).__name__, str(error))\n", "3\n" },
        { "base_attribute_hook_bypassed", "class Value:\n def __getattribute__(self,name):\n  if name=='__index__': raise RuntimeError('hooked')\n  return object.__getattribute__(self,name)\n def __index__(self): return 2\nvalue=Value()\ntry:\n print(int('11', value))\nexcept Exception as error:\n print(type(error).__name__, str(error))\n", "3\n" },
        { "noncallable_base_index", "class Value: __index__=None\nvalue=Value()\ntry:\n print(int('11', value))\nexcept Exception as error:\n print(type(error).__name__, str(error))\n", "TypeError 'NoneType' object is not callable\n" },
        { "int_precedes_index_and_trunc", "events=[]\nclass Value:\n def __int__(self):\n  events.append('int')\n  return 7\n def __index__(self):\n  events.append('index')\n  return 9\n def __trunc__(self):\n  events.append('trunc')\n  return 10\nvalue=Value()\ntry:\n print((int(value), events))\nexcept Exception as error:\n print(type(error).__name__, str(error))\n", "(7, ['int'])\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ImplicitIntegerConversionUsesTypeSlots(string label, string source, string expected)
    {
        using var script = new LythonEngine().Compile(source);
        Assert.True(script.IsValid, label);
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
    }

    [Fact]
    public async Task IntegerAliasesStayFundedWhileAnotherExecutionRuns()
    {
        const string source = "original=1<<2048\nclass Value:\n def __int__(self): return original\nclass Indexed:\n def __index__(self): return original\nreturn [int(Value()),int(Indexed()),int(Value()) is original,int(Indexed()) is original]";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(retained.Success, retained.Failure?.Message);
                Assert.Equal(new List<object?> { BigInteger.One << 2048, BigInteger.One << 2048, true, true }, retained.ReturnValue);
                var held = budget.ReservedBytes;
                Assert.True(held > baseline);
                using (var next = script.Run(new MockLythonHost())) Assert.True(next.Success, next.Failure?.Message);
                Assert.Equal(held, budget.ReservedBytes);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefusedCallbackAdmissionIsTerminalAndReleasesItsOwner(bool asynchronous)
    {
        const string source = "class Value:\n def __int__(self):\n  print('entered')\n  return int('9'*40000)\ntry: int(Value())\nexcept MemoryError: print('recovered')\nprint('tail')";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MaxExecutionMemoryBytes = 16 * 1024 };
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.ExecutionMemoryLimit, result.FailureAttribution?.Reason);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.True(result.DeniedReservationBytes > 0);
            Assert.Equal("entered\n", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}

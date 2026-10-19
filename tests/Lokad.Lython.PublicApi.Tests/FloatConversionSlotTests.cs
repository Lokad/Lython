using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FloatConversionSlotTests
{
    // Independent CPython 3.13.2 outputs, including type slots and object identity.
    public static TheoryData<string, string, string> Programs => new()
    {
        { "instance_shadow", "class Value:\n def __float__(self):return 7.0\nvalue=Value()\nvalue.__float__=lambda:99.0\nprint(float(value))", "7.0\n" },
        { "attribute_hook", "class Value:\n def __getattribute__(self,name):\n  if name==\"__float__\":raise RuntimeError(\"hooked\")\n  return object.__getattribute__(self,name)\n def __float__(self):return 7.0\nprint(float(Value()))", "7.0\n" },
        { "index_binding_once", "events=[]\nclass Value:\n @property\n def __index__(self):\n  events.append(\"index\")\n  return lambda:7\nprint(float(Value()),events)", "7.0 ['index']\n" },
        { "inherited_float", "def convert(value):return float(value)\nclass Parent:\n def __float__(self):return 7.0\nclass Value(Parent):pass\ntry:print(convert(Value()))\nexcept Exception as error:print(type(error).__name__,str(error))", "7.0\n" },
        { "property_float", "def convert(value):return float(value)\nclass Value:\n @property\n def __float__(self):return lambda:7.0\ntry:print(convert(Value()))\nexcept Exception as error:print(type(error).__name__,str(error))", "7.0\n" },
        { "static_float", "def convert(value):return float(value)\nclass Value:\n @staticmethod\n def __float__():return 7.0\ntry:print(convert(Value()))\nexcept Exception as error:print(type(error).__name__,str(error))", "7.0\n" },
        { "class_float", "def convert(value):return float(value)\nclass Value:\n @classmethod\n def __float__(cls):return 7.0\ntry:print(convert(Value()))\nexcept Exception as error:print(type(error).__name__,str(error))", "7.0\n" },
        { "inherited_index", "def convert(value):return float(value)\nclass Parent:\n def __index__(self):return 7\nclass Value(Parent):pass\ntry:print(convert(Value()))\nexcept Exception as error:print(type(error).__name__,str(error))", "7.0\n" },
        { "static_index", "def convert(value):return float(value)\nclass Value:\n @staticmethod\n def __index__():return 7\ntry:print(convert(Value()))\nexcept Exception as error:print(type(error).__name__,str(error))", "7.0\n" },
        { "class_index", "def convert(value):return float(value)\nclass Value:\n @classmethod\n def __index__(cls):return 7\ntry:print(convert(Value()))\nexcept Exception as error:print(type(error).__name__,str(error))", "7.0\n" },
        { "float_precedence", "def convert(value):return float(value)\nclass Value:\n def __float__(self):return 7.0\n def __index__(self):raise RuntimeError(\"wrong\")\ntry:print(convert(Value()))\nexcept Exception as error:print(type(error).__name__,str(error))", "7.0\n" },
        { "noncallable_float", "def convert(value):return float(value)\nclass Value:\n __float__=None\n def __index__(self):return 7\ntry:print(convert(Value()))\nexcept Exception as error:print(type(error).__name__,str(error))", "TypeError 'NoneType' object is not callable\n" },
        { "bad_float_result", "def convert(value):return float(value)\nclass Value:\n def __float__(self):return 7\ntry:print(convert(Value()))\nexcept Exception as error:print(type(error).__name__,str(error))", "TypeError Value.__float__ returned non-float (type int)\n" },
        { "bad_index_result", "def convert(value):return float(value)\nclass Value:\n def __index__(self):return 7.0\ntry:print(convert(Value()))\nexcept Exception as error:print(type(error).__name__,str(error))", "TypeError __index__ returned non-int (type float)\n" },
        { "missing_hooks", "def convert(value):return float(value)\nclass Value:pass\ntry:print(convert(Value()))\nexcept Exception as error:print(type(error).__name__,str(error))", "TypeError float() argument must be a string or a real number, not 'Value'\n" },
        { "irrelevant_hooks", "def convert(value):return float(value)\nclass Value:\n def __int__(self):return 7\n def __trunc__(self):return 7\ntry:print(convert(Value()))\nexcept Exception as error:print(type(error).__name__,str(error))", "TypeError float() argument must be a string or a real number, not 'Value'\n" },
        { "getattr_ignored", "def convert(value):return float(value)\nclass Value:\n def __getattr__(self,name):return lambda:99.0\ntry:print(convert(Value()))\nexcept Exception as error:print(type(error).__name__,str(error))", "TypeError float() argument must be a string or a real number, not 'Value'\n" },
        { "descriptor_error", "def convert(value):return float(value)\nclass Value:\n @property\n def __float__(self):raise AttributeError(\"descriptor\")\n def __index__(self):return 7\ntry:print(convert(Value()))\nexcept Exception as error:print(type(error).__name__,str(error))", "AttributeError descriptor\n" },
        { "index_overflow", "def convert(value):return float(value)\nclass Value:\n def __index__(self):return 1<<4096\ntry:print(convert(Value()))\nexcept Exception as error:print(type(error).__name__,str(error))", "OverflowError int too large to convert to float\n" },
        { "native_float_alias", "original=7.5\nprint(float(original) is original)", "True\n" },
        { "protocol_float_alias", "original=7.5\nclass Value:\n def __float__(self):return original\nprint(float(Value()) is original)", "True\n" },
        { "index_instance_shadow", "class Value:\n def __index__(self):return 7\nvalue=Value()\nvalue.__index__=None\nprint(float(value))", "7.0\n" },
        { "noncallable_index", "class Value:__index__=None\ntry:print(float(Value()))\nexcept Exception as error:print(type(error).__name__,str(error))", "TypeError 'NoneType' object is not callable\n" },
        { "index_descriptor_error", "class Value:\n @property\n def __index__(self):raise AttributeError(\"descriptor\")\ntry:print(float(Value()))\nexcept Exception as error:print(type(error).__name__,str(error))", "AttributeError descriptor\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task ImplicitFloatConversionUsesTypeSlots(string label, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid, label);
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
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task RetainedFloatAliasesStayFundedWhileAnotherExecutionRuns()
    {
        const string source = "original=7.5\npayload='x'*4096\nclass Value:\n def __float__(self):return original\nreturn [payload,original,float(original),float(Value()),float(original) is original,float(Value()) is original]";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using (var retained = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
            {
                Assert.True(retained.Success, retained.Failure?.Message);
                Assert.Equal(new List<object?> { new string('x', 4096), 7.5, 7.5, 7.5, true, true }, retained.ReturnValue);
                var held = budget.ReservedBytes;
                Assert.True(held > baseline);
                using (var next = script.Run(new MockLythonHost())) Assert.True(next.Success, next.Failure?.Message);
                Assert.Equal(held, budget.ReservedBytes);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("__float__", false)]
    [InlineData("__float__", true)]
    [InlineData("__index__", false)]
    [InlineData("__index__", true)]
    public async Task RefusedCallbackAdmissionIsTerminalAndReleasesItsOwner(string slot, bool asynchronous)
    {
        var source = $"class Value:\n def {slot}(self):\n  print('entered')\n  payload='x'*40000\n  return {(slot == "__float__" ? "7.0" : "7")}\ntry:float(Value())\nexcept MemoryError:print('recovered')\nprint('tail')";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(16 * 1024) };
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal("MemoryError", result.Failure?.ExceptionType);
            Assert.True(result.DeniedReservationBytes > 0);
            Assert.Equal("entered\n", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}

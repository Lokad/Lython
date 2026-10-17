using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class Utf8IntegerParseTests
{
    public static TheoryData<string, string, string> Programs => new()
    {
        { "00-text", "value='0'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "0 int\n" },
        { "00-bytes", "value=b'0'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "0 int\n" },
        { "01-text", "value='7'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "7 int\n" },
        { "01-bytes", "value=b'7'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "7 int\n" },
        { "02-text", "value='-7'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "-7 int\n" },
        { "02-bytes", "value=b'-7'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "-7 int\n" },
        { "03-text", "value='+7'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "7 int\n" },
        { "03-bytes", "value=b'+7'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "7 int\n" },
        { "04-text", "value='-0'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "0 int\n" },
        { "04-bytes", "value=b'-0'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "0 int\n" },
        { "05-text", "value='+0'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "0 int\n" },
        { "05-bytes", "value=b'+0'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "0 int\n" },
        { "06-text", "value='0007'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "7 int\n" },
        { "06-bytes", "value=b'0007'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "7 int\n" },
        { "07-text", "value='00000000000000000001'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "1 int\n" },
        { "07-bytes", "value=b'00000000000000000001'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "1 int\n" },
        { "08-text", "value='+0000000000000000000'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "0 int\n" },
        { "08-bytes", "value=b'+0000000000000000000'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "0 int\n" },
        { "09-text", "value='-0000000000000000000'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "0 int\n" },
        { "09-bytes", "value=b'-0000000000000000000'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "0 int\n" },
        { "10-text", "value='9223372036854775807'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "9223372036854775807 int\n" },
        { "10-bytes", "value=b'9223372036854775807'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "9223372036854775807 int\n" },
        { "11-text", "value='-9223372036854775808'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "-9223372036854775808 int\n" },
        { "11-bytes", "value=b'-9223372036854775808'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "-9223372036854775808 int\n" },
        { "12-text", "value='+9223372036854775807'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "9223372036854775807 int\n" },
        { "12-bytes", "value=b'+9223372036854775807'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "9223372036854775807 int\n" },
        { "13-text", "value='9223372036854775808'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "9223372036854775808 int\n" },
        { "13-bytes", "value=b'9223372036854775808'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "9223372036854775808 int\n" },
        { "14-text", "value='-9223372036854775809'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "-9223372036854775809 int\n" },
        { "14-bytes", "value=b'-9223372036854775809'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "-9223372036854775809 int\n" },
        { "15-text", "value='18446744073709551615'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "18446744073709551615 int\n" },
        { "15-bytes", "value=b'18446744073709551615'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "18446744073709551615 int\n" },
        { "16-text", "value='18446744073709551616'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "18446744073709551616 int\n" },
        { "16-bytes", "value=b'18446744073709551616'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "18446744073709551616 int\n" },
        { "17-text", "value='000000000000000000001'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "1 int\n" },
        { "17-bytes", "value=b'000000000000000000001'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "1 int\n" },
        { "18-text", "value='99999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "99999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999 int\n" },
        { "18-bytes", "value=b'99999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "99999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999 int\n" },
        { "19-text", "value=' \\t+7\\r\\n'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "7 int\n" },
        { "19-bytes", "value=b' \\t+7\\r\\n'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "7 int\n" },
        { "20-text", "value='1_000'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "1000 int\n" },
        { "20-bytes", "value=b'1_000'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "1000 int\n" },
        { "21-text", "value=' 1_2\\t'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "12 int\n" },
        { "21-bytes", "value=b' 1_2\\t'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "12 int\n" },
        { "22-text", "value='7\\x00'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "ValueError invalid literal for int() with base 10: '7\\x00'\n" },
        { "22-bytes", "value=b'7\\x00'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "ValueError invalid literal for int() with base 10: b'7\\x00'\n" },
        { "23-text", "value='7\\x00\\x00'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "ValueError invalid literal for int() with base 10: '7\\x00\\x00'\n" },
        { "23-bytes", "value=b'7\\x00\\x00'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "ValueError invalid literal for int() with base 10: b'7\\x00\\x00'\n" },
        { "24-text", "value='7\\x001'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "ValueError invalid literal for int() with base 10: '7\\x001'\n" },
        { "24-bytes", "value=b'7\\x001'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "ValueError invalid literal for int() with base 10: b'7\\x001'\n" },
        { "25-text", "value='7x'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "ValueError invalid literal for int() with base 10: '7x'\n" },
        { "25-bytes", "value=b'7x'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "ValueError invalid literal for int() with base 10: b'7x'\n" },
        { "26-text", "value='7e0'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "ValueError invalid literal for int() with base 10: '7e0'\n" },
        { "26-bytes", "value=b'7e0'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "ValueError invalid literal for int() with base 10: b'7e0'\n" },
        { "27-text", "value='7.0'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "ValueError invalid literal for int() with base 10: '7.0'\n" },
        { "27-bytes", "value=b'7.0'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "ValueError invalid literal for int() with base 10: b'7.0'\n" },
        { "28-text", "value=''\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "ValueError invalid literal for int() with base 10: ''\n" },
        { "28-bytes", "value=b''\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "ValueError invalid literal for int() with base 10: b''\n" },
        { "29-text", "value='+'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "ValueError invalid literal for int() with base 10: '+'\n" },
        { "29-bytes", "value=b'+'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "ValueError invalid literal for int() with base 10: b'+'\n" },
        { "30-text", "value='-'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "ValueError invalid literal for int() with base 10: '-'\n" },
        { "30-bytes", "value=b'-'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "ValueError invalid literal for int() with base 10: b'-'\n" },
        { "31-text", "value='--7'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "ValueError invalid literal for int() with base 10: '--7'\n" },
        { "31-bytes", "value=b'--7'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "ValueError invalid literal for int() with base 10: b'--7'\n" },
        { "32-text", "value='+-7'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "ValueError invalid literal for int() with base 10: '+-7'\n" },
        { "32-bytes", "value=b'+-7'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "ValueError invalid literal for int() with base 10: b'+-7'\n" },
        { "33-text", "value='0x10'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "ValueError invalid literal for int() with base 10: '0x10'\n" },
        { "33-bytes", "value=b'0x10'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "ValueError invalid literal for int() with base 10: b'0x10'\n" },
        { "34-text", "value='7\u00e9'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "ValueError invalid literal for int() with base 10: '7\u00e9'\n" },
        { "34-bytes", "value=b'7\\xc3\\xa9'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "ValueError invalid literal for int() with base 10: b'7\\xc3\\xa9'\n" },
        { "35-text", "value='\u22127'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "ValueError invalid literal for int() with base 10: '\u22127'\n" },
        { "35-bytes", "value=b'\\xe2\\x88\\x927'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "ValueError invalid literal for int() with base 10: b'\\xe2\\x88\\x927'\n" },
        { "36-text", "value='\\u2003-7\\u2003'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "-7 int\n" },
        { "36-bytes", "value=b'\\xe2\\x80\\x83-7\\xe2\\x80\\x83'\ntry:\n result=int(value)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "ValueError invalid literal for int() with base 10: b'\\xe2\\x80\\x83-7\\xe2\\x80\\x83'\n" },
        { "base10-text-7", "value='7'\ntry:\n result=int(value,10)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "7 int\n" },
        { "base10-bytes-7", "value=b'7'\ntry:\n result=int(value,10)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "7 int\n" },
        { "base10-text--9223372036854775808", "value='-9223372036854775808'\ntry:\n result=int(value,10)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "-9223372036854775808 int\n" },
        { "base10-bytes--9223372036854775808", "value=b'-9223372036854775808'\ntry:\n result=int(value,10)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "-9223372036854775808 int\n" },
        { "base0-text-0xff", "value='0xff'\ntry:\n result=int(value,0)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "255 int\n" },
        { "base0-bytes-0xff", "value=b'0xff'\ntry:\n result=int(value,0)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "255 int\n" },
        { "base2-text-101", "value='101'\ntry:\n result=int(value,2)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "5 int\n" },
        { "base2-bytes-101", "value=b'101'\ntry:\n result=int(value,2)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "5 int\n" },
        { "base16-text-ff", "value='ff'\ntry:\n result=int(value,16)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "255 int\n" },
        { "base16-bytes-ff", "value=b'ff'\ntry:\n result=int(value,16)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "255 int\n" },
        { "base36-text-z", "value='z'\ntry:\n result=int(value,36)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "35 int\n" },
        { "base36-bytes-z", "value=b'z'\ntry:\n result=int(value,36)\n print(result,type(result).__name__)\nexcept (ValueError,TypeError) as error:\n print(type(error).__name__,str(error))", "35 int\n" },
    };

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task NativeAndFallbackInputsMatchPython(string label, string source, string expected)
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
}

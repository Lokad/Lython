using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Xunit.Abstractions;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class BoundedStartsWithBoundaryTests(ITestOutputHelper output)
{
    private const int MaximumSteps = 10000;
    private const string FrozenGuards = """{"endswith:bad_end:False":"029D84F3F112CCFF07D9D91CA3ED8D7EECE84A3922FE6E5CB0F326BBD05A9A71","endswith:bad_end:True":"79398794BDA2AEA6E6F80EA5E4088E29544FECCCD83E1A4948EE11898B7DBDC8","endswith:bad_prefix:False":"63AC2DC7CDA1FE1BBB86B91DA1AADF8ED1D801C4D976C2DB674D1D7DBC97AF65","endswith:bad_prefix:True":"5BA0945F23190BC31E107CBFECF26A9EED0E6E62AD9C1944417374153ADD3D40","endswith:bad_start:False":"55034303414E4751FE01472441876511E363752D299D544433F0F2FAEC8767F0","endswith:bad_start:True":"3C4EA96D893357A3F95A38D6708B4C8374E91897013D7C3E960709022C8C7B03","endswith:bound_error:False":"2245B07A685709CF1991FB89854EF5163C953559249229A1DDA8FAC541605F88","endswith:bound_error:True":"FC1AD4DA09213F12D53E91C873C903CBBAA2F95333AF0C7F4A079EE9FEFC3C84","endswith:guest-bad-start:False":"D336F8621B2A7A6FDA9D3044EB4F22CF4F30893FFF57AC220448B913FADF9DAE","endswith:guest-bad-start:True":"BD4793D5FE64B7774852419E4D8D9B3D7FD615338DE19DA15C3B70E120CE5AEF","endswith:guest-bounds:False":"87ABE4046DBE59753E466FB2FDA59B888A016ACC3F13B7FDD0B663DBCD3195FB","endswith:guest-bounds:True":"B39EB939D6E241449E5D8497C71A0F1F7F1DB2702D781B7BDA39ABDA6300A8D6","endswith:hooks:False":"AC69C4B24EDC0121D51E42076B0C1506F532E720FB2996C954A35E5638F68B07","endswith:hooks:True":"8A233FFCD619DF4C22019A7A5AA253E8B1B0E268D16AB275A00AF84604845F84","endswith:one-prefix:False":"984BC037B8E15C3C797BA7FE31020935931552E334BBC77E46480103E05AD83B","endswith:one-prefix:True":"7BED734BD7F87A9AEEDB28BEA55BC02E99294BED8C885FA2C04B82D8D0602BCB","endswith:three-arguments:False":"7D91A2DE88CCEF580F426460073BAA9179E89627EAE6AE2C9D0513683261C9B7","endswith:three-arguments:True":"C0681F0BCF30B292A9B03B283EB5AE792AECD4F36DC6A41BA80B75BF2D63E1C4","endswith:two-arguments:False":"D1074153DCA99D562437CE2AF1D95AA0D5A80D870922C0C7FE4D019A2001E051","endswith:two-arguments:True":"453E6BE0325A581A5EF4A050373E34A2A9B2ACFA83290AD1227A051D1EB6BEE0","startswith:bad_end:False":"72C301E474DAD31699664EC3C67630F21979BBBD7A555204509FCB57F384F837","startswith:bad_end:True":"704DA3F657420897180C152B6BC6039A394D397721BBAA6A53F96482E68773FC","startswith:bad_prefix:False":"AADC0AB10E4F793E1283BF8077DC98D0C70D55FC9B35457E613E1F214DD3E8A1","startswith:bad_prefix:True":"7A7D4B22F50C76CA4181098EEDD838B6D8DDD3C283A3D4CF90E9E446748B7883","startswith:bad_start:False":"9CF47D24BBAC733B086AFA0A395FC1E9948384FC3FBDE7841DFFBDA7BE8E4499","startswith:bad_start:True":"3CB813F9EC1B6B6CB264B24DB7A03A17C4B2060371E182ADC6A6C70862196446","startswith:bound_error:False":"33276E9B8E66F06AF413AF99BA8049A4FF2FF122D8AB1280E4C7C247EF0068FC","startswith:bound_error:True":"82AAF06EDA61D3F42CE47294E045E79A1C3FDEB6BD1C555462BD2D41D6001995","startswith:guest-bad-start:False":"B1F1A6168410F76D16B4B8515CABBA1E9D74BD7F40FCA5D8F1043B463C1D1831","startswith:guest-bad-start:True":"747432063F4A28F3F193FE81E0FA9AED8037FD973E719EBFCFBC5167D1F9F005","startswith:guest-bounds:False":"F65A3007F5E8F88F50E6C1E4FADC4E298316797224457E1A122CF9B716A0F6E3","startswith:guest-bounds:True":"F344F0C4010D9A9BFEA555A2EB74DE0C90FE7541758DCD9385AB431D4F64228D","startswith:hooks:False":"160E15EED19DFBEF168DF648B33AA3AC812D2A7D64590071E9E7489C800B50B7","startswith:hooks:True":"B19B4B33BBB3B59C677C97DEB279B4F5C3F8FEBBD9CD3359F47602EC0BD0C82B","startswith:one-prefix:False":"AA1F72485234FC003CC9EA135534C6B20D9628D7C970CA60F60E8273BD0F3D1C","startswith:one-prefix:True":"0E2BDCEAF64B2CE74F46CA8F274F1ABEF452CF31A754B70DBD03FBCFBB652259","startswith:three-arguments:False":"3729113EC1561063FCE855E4CCC595120B6BC8EFD14A932C184898222D4072C1","startswith:three-arguments:True":"09E770DD94CBE2D98D44B5D27CA5522853CF04EF169AD06FCB821FEDDB50C5D5","startswith:two-arguments:False":"FDC67EF9DD9DE43C589A3B5E83B229C602ADDBB30C88D4EBF746B707380BF720","startswith:two-arguments:True":"C8415078FC819FBE5C39A8C152873AC79C600176B3B05EC33D53C63C1CFBB94D"}""";

    public static TheoryData<string, string, bool> Programs => new()
    {
        { "one-prefix", "return source.startswith('a')", false },
        { "two-arguments", "return source.startswith('😀',1)", false },
        { "three-arguments", "return source.startswith('😀',1,2)", false },
        { "hooks", "return source.startswith(prefix(),start(),end())", false },
        { "bad_prefix", "return source.startswith(bad(),start(),end())", true },
        { "bad_start", "return source.startswith(prefix(),wrong(),end())", true },
        { "bad_end", "return source.startswith(prefix(),start(),wrong())", true },
        { "bound_error", "return source.startswith(prefix(),start(),fail())", true },
        { "guest-bounds", "class Bound:\n def __init__(self,name):self.name=name\n def __index__(self):return note(self.name)\nreturn source.startswith('😀',Bound('start'),Bound('end'))", false },
        { "guest-bad-start", "class Start:\n def __index__(self):note('start');return 'bad'\nclass End:\n @property\n def __index__(self):note('end');return lambda:2\nreturn source.startswith('😀',Start(),End())", true },
    };

    public static IEnumerable<object[]> AffixPrograms()
    {
        foreach (var row in Programs)
        foreach (var method in new[] { "startswith", "endswith" })
        {
            var source = ((string)row[1]).Replace("startswith", method, StringComparison.Ordinal);
            // Both native two-argument controls match the shared a😀a source.
            if (method == "endswith" && (string)row[0] == "two-arguments")
                source = source.Replace("'😀',1)", "'a',1)", StringComparison.Ordinal);
            yield return [row[0], method, source, row[2]];
        }
    }

    [Theory]
    [MemberData(nameof(AffixPrograms))]
    public async Task BoundsPreserveEffectsFuelAndUnwind(string label, string method, string source, bool fails)
    {
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            long steps;
            string[] fullEffects;
            string? completedError = null;
            using (var owner = budget.CreateScope())
            {
                var (context, effects) = Prepare(owner);
                object? result = null;
                var failure = await Record.ExceptionAsync(async () => result = await Run(code, context, asynchronous));
                if (fails)
                {
                    var error = Assert.IsType<LythonRuntimeException>(failure);
                    Assert.Equal(label == "bound_error" ? "ValueError" : "TypeError", error.ExceptionType);
                    completedError = Describe(error);
                }
                else
                {
                    Assert.Null(failure);
                    Assert.True(Assert.IsType<bool>(result));
                }
                fullEffects = effects.ToArray();
                Assert.Equal(label switch
                {
                    "one-prefix" or "two-arguments" or "three-arguments" => [],
                    "hooks" => ["prefix", "start", "end"],
                    "bad_prefix" => ["bad", "start", "end"],
                    "bad_start" => ["prefix", "wrong", "end"],
                    "bad_end" => ["prefix", "start", "wrong"],
                    "guest-bounds" => ["start", "end"],
                    "guest-bad-start" => ["start"],
                    _ => new[] { "prefix", "start", "fail" },
                }, fullEffects);
                steps = context.Limits.ExecutionStepCount;
                CheckCleanup(context);
            }
            Assert.Equal(0, budget.ReservedBytes);
            var checkpoints = new List<string>();
            for (var remaining = 0; remaining < steps; remaining++)
            {
                using (var owner = budget.CreateScope())
                {
                    var (context, effects) = Prepare(owner);
                    context.Limits.ExecutionStepCount = MaximumSteps - remaining;
                    var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Run(code, context, asynchronous));
                    Assert.Equal(LythonFailureReason.ExecutionStepLimit, failure.Attribution?.Reason);
                    Assert.Equal(MaximumSteps, failure.Attribution?.Limit);
                    Assert.Equal(fullEffects.Take(effects.Count), effects);
                    checkpoints.Add(Describe(failure) + ":checks=" + context.Limits.ExecutionStepCount + ":" + string.Join(';', effects));
                    CheckCleanup(context);
                }
                Assert.Equal(0, budget.ReservedBytes);
            }
            var record = JsonSerializer.Serialize(new { label, method, asynchronous, steps, completedError, fullEffects, checkpoints });
            output.WriteLine("BOUNDED_STARTSWITH_MATRIX:" + record);
            using var frozen = JsonDocument.Parse(FrozenGuards);
            Assert.Equal(frozen.RootElement.GetProperty($"{method}:{label}:{asynchronous}").GetString(),
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(record))));
        }
    }

    private static (Context Context, List<string> Effects) Prepare(ILythonMemoryBudget owner)
    {
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, MaxExecutionSteps = MaximumSteps });
        var effects = new List<string>();
        context.Variables["source"] = PyString.FromString("a😀a", context.MemoryGovernor, null);
        context.Variables["prefix"] = new Argument(effects, "prefix", PyString.FromString("😀", context.MemoryGovernor, null));
        context.Variables["start"] = new Argument(effects, "start", new BigInteger(1));
        context.Variables["end"] = new Argument(effects, "end", new BigInteger(2));
        context.Variables["bad"] = new Argument(effects, "bad", new BigInteger(7));
        context.Variables["wrong"] = new Argument(effects, "wrong", PyString.FromString("wrong", context.MemoryGovernor, null));
        context.Variables["fail"] = new Fail(effects);
        context.Variables["note"] = new Note(effects);
        return (context, effects);
    }

    private static string Describe(LythonRuntimeException error)
        => $"{error.ExceptionType}:{error.Span?.Start},{error.Span?.Length},{error.Span?.Line},{error.Span?.Column}";

    private static ValueTask<object?> Run(ExecutableCodeObject code, Context context, bool asynchronous)
        => asynchronous ? LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
            : new(LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));

    private static void CheckCleanup(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class Argument(List<string> effects, string name, object value) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Assert.InRange(context.Limits.ExecutionStepCount, 0, MaximumSteps);
            effects.Add(name);
            return value;
        }
    }

    private sealed class Note(List<string> effects) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            var name = Assert.IsType<PyString>(arguments[0].Value).AsString();
            effects.Add(name);
            return new BigInteger(name == "start" ? 1 : 2);
        }
    }

    private sealed class Fail(List<string> effects) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Assert.InRange(context.Limits.ExecutionStepCount, 0, MaximumSteps);
            effects.Add("fail");
            throw new LythonRuntimeException("ValueError", "annotation fixture", span);
        }
    }
}

using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class StringPositionalBindingTests
{
    public static IEnumerable<object[]> Calls()
    {
        var calls = new (string Method, string Arguments)[]
        {
            ("lstrip", "chars='a'"), ("rstrip", "chars='b'"),
            ("center", "width=5"), ("ljust", "width=5"), ("rjust", "width=5"),
            ("center", "5,fillchar='.'"), ("zfill", "width=5"),
            ("find", "sub='x'"), ("index", "sub='x'"), ("rfind", "sub='x'"),
            ("rindex", "sub='x'"), ("count", "sub='x'"),
            ("find", "'x',start=0"), ("find", "'x',0,end=3"),
        };
        foreach (var call in calls)
        foreach (var form in new[] { 0, 1, 2 })
        foreach (var asynchronous in new[] { false, true })
            yield return [call.Method, call.Arguments, form, asynchronous];
    }

    [Theory]
    [MemberData(nameof(Calls))]
    public async Task PositionalMethodsRejectKeywordsForImmediateExposedAndUnboundCalls(string method, string arguments, int form, bool asynchronous)
    {
        var target = form == 0 ? "text." + method : form == 1 ? "method" : "str." + method;
        var source = "text='axb'\nmethod=text." + method + "\ntry:\n " + target + "(" + (form == 2 ? "text," : "") + arguments
            + ")\nexcept TypeError:print('TypeError')\nprint(text)";
        // Every case independently checked under isolated CPython3.13; these
        // used to succeed, so a TypeError from an invalid value is insufficient.
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("TypeError\naxb\n", result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KeywordRejectionFollowsArgumentEvaluationAndPrecedesIndexConversion(bool asynchronous)
    {
        const string source = """
            events = []
            class Width:
                def __index__(self):
                    events.append('coerce')
                    return 5
            def argument():
                events.append('argument')
                return Width()
            try:
                'x'.center(width=argument())
            except TypeError:
                events.append('error')
            print(events)
            """;
        using var script = new LythonEngine().Compile(source);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("['argument', 'error']\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ArgumentAdmissionRefusalPrecedesInvalidKeywordBinding(bool asynchronous)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile("def argument():return 'x'*32000000\ntry:\n 'x'.center(width=argument())\nexcept TypeError:print('hidden')", budget);
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.False(result.Success);
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Empty(result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }
}

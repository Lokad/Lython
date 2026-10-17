using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ContextualCasingExecutionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CasingFollowsIndependentPythonOutputs(bool asynchronous)
    {
        const string source = "for text in ['\u039f\u03a3', '\u039f\u03a3\u0391', '\u03a3', 'A\u03a3\u0301', 'A\u03a3\u0301A', 'A\u03a3.A', 'A\u03a3/A', '\u02b0\u03a3', 'A\u02b0\u03a3', '!AB\u03a3', '\u0301\u039f\u03a3', '\\x00A\u03a3', '\u01c4', '\u01c5', '\u01c6', '\u01c7', '\u01c8', '\u01c9', '\u01ca', '\u01cb', '\u01cc', '\u01f1', '\u01f2', '\u01f3', '\u00df', '\ufb03', '\u0130', '\ud801\udc00', '\ud801\udc28AB\u03a3', 'Stra\u00dfe \u0130 \ufb03 \u03a3', '\u0345A\u03a3', '\u1f88', '']:\n print(text.capitalize().encode(\"utf-8\").hex(),text.title().encode(\"utf-8\").hex(),text.swapcase().encode(\"utf-8\").hex())";
        const string expected = "ce9fcf82 ce9fcf82 cebfcf82\nce9fcf83ceb1 ce9fcf83ceb1 cebfcf83ceb1\ncea3 cea3 cf83\n41cf82cc81 41cf82cc81 61cf82cc81\n41cf83cc8161 41cf83cc8141 61cf83cc8161\n41cf832e61 41cf832e41 61cf832e61\n41cf822f61 41cf822f41 61cf822f61\ncab0cf83 cab0cf83 cab0cf83\n41cab0cf82 41cab0cf82 61cab0cf82\n216162cf82 214162cf82 216162cf82\ncc81cebfcf82 cc81ce9fcf82 cc81cebfcf82\n0061cf82 0041cf82 0061cf82\nc785 c785 c786\nc785 c785 c785\nc785 c785 c784\nc788 c788 c789\nc788 c788 c788\nc788 c788 c787\nc78b c78b c78c\nc78b c78b c78b\nc78b c78b c78a\nc7b2 c7b2 c7b3\nc7b2 c7b2 c7b2\nc7b2 c7b2 c7b1\n5373 5373 5353\n466669 466669 464649\nc4b0 c4b0 69cc87\nf0909080 f0909080 f09090a8\nf09090806162cf82 f09090806162cf82 f09090806162cf82\n53747261c39f652069cc8720efac8320cf83 53747261c39f6520c4b02046666920cea3 735452415353452069cc872046464920cf83\nce9961cf82 ce9961cf82 ce9961cf82\ne1be88 e1be88 e1be88\n  \n";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost()))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, "capitalize", "Aς")]
    [InlineData(true, "capitalize", "Aς")]
    [InlineData(false, "title", "Aς")]
    [InlineData(true, "title", "Aς")]
    [InlineData(false, "swapcase", "aς")]
    [InlineData(true, "swapcase", "aς")]
    [InlineData(false, "upper", "AΣ")]
    [InlineData(true, "upper", "AΣ")]
    public async Task RetainedCasingResultsKeepTheirFunding(bool asynchronous, string method, string expected)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile("return getattr(source, method)()", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["source"] = "AΣ", ["method"] = method } };
        using (var first = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
        {
            Assert.True(first.Success, first.Failure?.Message);
            Assert.Equal(expected, first.ReturnValue);
            var retained = budget.ReservedBytes;
            Assert.True(retained > baseline);
            using (var second = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
            {
                Assert.True(second.Success, second.Failure?.Message);
                Assert.Equal(expected, second.ReturnValue);
                Assert.Equal(expected, first.ReturnValue);
                Assert.True(budget.ReservedBytes > retained);
            }
            Assert.Equal(retained, budget.ReservedBytes);
        }
        Assert.Equal(baseline, budget.ReservedBytes);
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}

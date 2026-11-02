using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ContextualCasingExecutionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LaterPlatformCasePairsRemainUnchangedAsInPython313(bool asynchronous)
    {
        // Independent CPython 3.13 golden: none of these characters has a
        // simple case partner in Unicode 15.1. Includes older characters whose
        // partner was only assigned in Unicode 16/17, and all supplementary pairs.
        const string text = "\u019b\u0264\u1c89\u1c8a\ua7cb\ua7cc\ua7cd\ua7ce\ua7cf\ua7d2\ua7d3\ua7d4\ua7d5\ua7da\ua7db\ua7dc\U00010d50\U00010d51\U00010d52\U00010d53\U00010d54\U00010d55\U00010d56\U00010d57\U00010d58\U00010d59\U00010d5a\U00010d5b\U00010d5c\U00010d5d\U00010d5e\U00010d5f\U00010d60\U00010d61\U00010d62\U00010d63\U00010d64\U00010d65\U00010d70\U00010d71\U00010d72\U00010d73\U00010d74\U00010d75\U00010d76\U00010d77\U00010d78\U00010d79\U00010d7a\U00010d7b\U00010d7c\U00010d7d\U00010d7e\U00010d7f\U00010d80\U00010d81\U00010d82\U00010d83\U00010d84\U00010d85\U00016ea0\U00016ea1\U00016ea2\U00016ea3\U00016ea4\U00016ea5\U00016ea6\U00016ea7\U00016ea8\U00016ea9\U00016eaa\U00016eab\U00016eac\U00016ead\U00016eae\U00016eaf\U00016eb0\U00016eb1\U00016eb2\U00016eb3\U00016eb4\U00016eb5\U00016eb6\U00016eb7\U00016eb8\U00016ebb\U00016ebc\U00016ebd\U00016ebe\U00016ebf\U00016ec0\U00016ec1\U00016ec2\U00016ec3\U00016ec4\U00016ec5\U00016ec6\U00016ec7\U00016ec8\U00016ec9\U00016eca\U00016ecb\U00016ecc\U00016ecd\U00016ece\U00016ecf\U00016ed0\U00016ed1\U00016ed2\U00016ed3";
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var script = new LythonEngine().Compile("return [text.lower(), text.upper()]", budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        var options = new LythonRunOptions { Globals = new Dictionary<string, object?> { ["text"] = text } };
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options) : script.Run(new MockLythonHost(), options))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(new object?[] { text, text }, Assert.IsType<List<object?>>(result.ReturnValue));
        }
        Assert.Equal(baseline, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CasingFollowsIndependentPythonOutputs(bool asynchronous)
    {
        const string source = "for text in ['\u039f\u03a3', '\u039f\u03a3\u0391', '\u03a3', 'A\u03a3\u0301', 'A\u03a3\u0301A', 'A\u03a3.A', 'A\u03a3/A', '\u02b0\u03a3', 'A\u02b0\u03a3', '!AB\u03a3', '\u0301\u039f\u03a3', '\\x00A\u03a3', '\u01c4', '\u01c5', '\u01c6', '\u01c7', '\u01c8', '\u01c9', '\u01ca', '\u01cb', '\u01cc', '\u01f1', '\u01f2', '\u01f3', '\u00df', '\ufb03', '\u0130', '\ud801\udc00', '\ud801\udc28AB\u03a3', 'Stra\u00dfe \u0130 \ufb03 \u03a3', '\u0345A\u03a3', '\u1f88', '']:\n print(text.capitalize().encode(\"utf-8\").hex(),text.title().encode(\"utf-8\").hex(),text.swapcase().encode(\"utf-8\").hex())";
        const string expected = "ce9fcf82 ce9fcf82 cebfcf82\nce9fcf83ceb1 ce9fcf83ceb1 cebfcf83ceb1\ncea3 cea3 cf83\n41cf82cc81 41cf82cc81 61cf82cc81\n41cf83cc8161 41cf83cc8141 61cf83cc8161\n41cf832e61 41cf832e41 61cf832e61\n41cf822f61 41cf822f41 61cf822f61\ncab0cf83 cab0cf83 cab0cf83\n41cab0cf82 41cab0cf82 61cab0cf82\n216162cf82 214162cf82 216162cf82\ncc81cebfcf82 cc81ce9fcf82 cc81cebfcf82\n0061cf82 0041cf82 0061cf82\nc785 c785 c786\nc785 c785 c785\nc785 c785 c784\nc788 c788 c789\nc788 c788 c788\nc788 c788 c787\nc78b c78b c78c\nc78b c78b c78b\nc78b c78b c78a\nc7b2 c7b2 c7b3\nc7b2 c7b2 c7b2\nc7b2 c7b2 c7b1\n5373 5373 5353\n466669 466669 464649\nc4b0 c4b0 69cc87\nf0909080 f0909080 f09090a8\nf09090806162cf82 f09090806162cf82 f09090806162cf82\n53747261c39f652069cc8720efac8320cf83 53747261c39f6520c4b02046666920cea3 735452415353452069cc872046464920cf83\nce9961cf82 ce9961cf82 ce9961cf82\ne1be88 e1be88 e1be88\n  \n";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile(source, budget);
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
        using var script = new LythonEngine(Utf8RegexProvider.Instance).Compile("return getattr(source, method)()", budget);
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

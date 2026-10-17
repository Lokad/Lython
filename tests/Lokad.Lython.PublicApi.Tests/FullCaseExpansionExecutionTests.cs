using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class FullCaseExpansionExecutionTests
{
    // Generated independent CPython 3.13 / Unicode 15.1 stdout golden.
    [Fact]
    public async Task AllFullExpansionsMatchPythonUnderBothExecutionApis()
    {
        const string source = "for text in '\u00df\u0149\u01f0\u0390\u03b0\u0587\u1e96\u1e97\u1e98\u1e99\u1e9a\u1f50\u1f52\u1f54\u1f56\u1f80\u1f81\u1f82\u1f83\u1f84\u1f85\u1f86\u1f87\u1f88\u1f89\u1f8a\u1f8b\u1f8c\u1f8d\u1f8e\u1f8f\u1f90\u1f91\u1f92\u1f93\u1f94\u1f95\u1f96\u1f97\u1f98\u1f99\u1f9a\u1f9b\u1f9c\u1f9d\u1f9e\u1f9f\u1fa0\u1fa1\u1fa2\u1fa3\u1fa4\u1fa5\u1fa6\u1fa7\u1fa8\u1fa9\u1faa\u1fab\u1fac\u1fad\u1fae\u1faf\u1fb2\u1fb3\u1fb4\u1fb6\u1fb7\u1fbc\u1fc2\u1fc3\u1fc4\u1fc6\u1fc7\u1fcc\u1fd2\u1fd3\u1fd6\u1fd7\u1fe2\u1fe3\u1fe4\u1fe6\u1fe7\u1ff2\u1ff3\u1ff4\u1ff6\u1ff7\u1ffc\ufb00\ufb01\ufb02\ufb03\ufb04\ufb05\ufb06\ufb13\ufb14\ufb15\ufb16\ufb17':\n print(text.upper().encode('utf-8').hex(),text.title().encode('utf-8').hex(),text.capitalize().encode('utf-8').hex(),text.swapcase().encode('utf-8').hex())";
        const string expected = "5353 5373 5373 5353\ncabc4e cabc4e cabc4e cabc4e\n4acc8c 4acc8c 4acc8c 4acc8c\nce99cc88cc81 ce99cc88cc81 ce99cc88cc81 ce99cc88cc81\ncea5cc88cc81 cea5cc88cc81 cea5cc88cc81 cea5cc88cc81\nd4b5d592 d4b5d682 d4b5d682 d4b5d592\n48ccb1 48ccb1 48ccb1 48ccb1\n54cc88 54cc88 54cc88 54cc88\n57cc8a 57cc8a 57cc8a 57cc8a\n59cc8a 59cc8a 59cc8a 59cc8a\n41cabe 41cabe 41cabe 41cabe\ncea5cc93 cea5cc93 cea5cc93 cea5cc93\ncea5cc93cc80 cea5cc93cc80 cea5cc93cc80 cea5cc93cc80\ncea5cc93cc81 cea5cc93cc81 cea5cc93cc81 cea5cc93cc81\ncea5cc93cd82 cea5cc93cd82 cea5cc93cd82 cea5cc93cd82\ne1bc88ce99 e1be88 e1be88 e1bc88ce99\ne1bc89ce99 e1be89 e1be89 e1bc89ce99\ne1bc8ace99 e1be8a e1be8a e1bc8ace99\ne1bc8bce99 e1be8b e1be8b e1bc8bce99\ne1bc8cce99 e1be8c e1be8c e1bc8cce99\ne1bc8dce99 e1be8d e1be8d e1bc8dce99\ne1bc8ece99 e1be8e e1be8e e1bc8ece99\ne1bc8fce99 e1be8f e1be8f e1bc8fce99\ne1bc88ce99 e1be88 e1be88 e1be88\ne1bc89ce99 e1be89 e1be89 e1be89\ne1bc8ace99 e1be8a e1be8a e1be8a\ne1bc8bce99 e1be8b e1be8b e1be8b\ne1bc8cce99 e1be8c e1be8c e1be8c\ne1bc8dce99 e1be8d e1be8d e1be8d\ne1bc8ece99 e1be8e e1be8e e1be8e\ne1bc8fce99 e1be8f e1be8f e1be8f\ne1bca8ce99 e1be98 e1be98 e1bca8ce99\ne1bca9ce99 e1be99 e1be99 e1bca9ce99\ne1bcaace99 e1be9a e1be9a e1bcaace99\ne1bcabce99 e1be9b e1be9b e1bcabce99\ne1bcacce99 e1be9c e1be9c e1bcacce99\ne1bcadce99 e1be9d e1be9d e1bcadce99\ne1bcaece99 e1be9e e1be9e e1bcaece99\ne1bcafce99 e1be9f e1be9f e1bcafce99\ne1bca8ce99 e1be98 e1be98 e1be98\ne1bca9ce99 e1be99 e1be99 e1be99\ne1bcaace99 e1be9a e1be9a e1be9a\ne1bcabce99 e1be9b e1be9b e1be9b\ne1bcacce99 e1be9c e1be9c e1be9c\ne1bcadce99 e1be9d e1be9d e1be9d\ne1bcaece99 e1be9e e1be9e e1be9e\ne1bcafce99 e1be9f e1be9f e1be9f\ne1bda8ce99 e1bea8 e1bea8 e1bda8ce99\ne1bda9ce99 e1bea9 e1bea9 e1bda9ce99\ne1bdaace99 e1beaa e1beaa e1bdaace99\ne1bdabce99 e1beab e1beab e1bdabce99\ne1bdacce99 e1beac e1beac e1bdacce99\ne1bdadce99 e1bead e1bead e1bdadce99\ne1bdaece99 e1beae e1beae e1bdaece99\ne1bdafce99 e1beaf e1beaf e1bdafce99\ne1bda8ce99 e1bea8 e1bea8 e1bea8\ne1bda9ce99 e1bea9 e1bea9 e1bea9\ne1bdaace99 e1beaa e1beaa e1beaa\ne1bdabce99 e1beab e1beab e1beab\ne1bdacce99 e1beac e1beac e1beac\ne1bdadce99 e1bead e1bead e1bead\ne1bdaece99 e1beae e1beae e1beae\ne1bdafce99 e1beaf e1beaf e1beaf\ne1bebace99 e1bebacd85 e1bebacd85 e1bebace99\nce91ce99 e1bebc e1bebc ce91ce99\nce86ce99 ce86cd85 ce86cd85 ce86ce99\nce91cd82 ce91cd82 ce91cd82 ce91cd82\nce91cd82ce99 ce91cd82cd85 ce91cd82cd85 ce91cd82ce99\nce91ce99 e1bebc e1bebc e1bebc\ne1bf8ace99 e1bf8acd85 e1bf8acd85 e1bf8ace99\nce97ce99 e1bf8c e1bf8c ce97ce99\nce89ce99 ce89cd85 ce89cd85 ce89ce99\nce97cd82 ce97cd82 ce97cd82 ce97cd82\nce97cd82ce99 ce97cd82cd85 ce97cd82cd85 ce97cd82ce99\nce97ce99 e1bf8c e1bf8c e1bf8c\nce99cc88cc80 ce99cc88cc80 ce99cc88cc80 ce99cc88cc80\nce99cc88cc81 ce99cc88cc81 ce99cc88cc81 ce99cc88cc81\nce99cd82 ce99cd82 ce99cd82 ce99cd82\nce99cc88cd82 ce99cc88cd82 ce99cc88cd82 ce99cc88cd82\ncea5cc88cc80 cea5cc88cc80 cea5cc88cc80 cea5cc88cc80\ncea5cc88cc81 cea5cc88cc81 cea5cc88cc81 cea5cc88cc81\ncea1cc93 cea1cc93 cea1cc93 cea1cc93\ncea5cd82 cea5cd82 cea5cd82 cea5cd82\ncea5cc88cd82 cea5cc88cd82 cea5cc88cd82 cea5cc88cd82\ne1bfbace99 e1bfbacd85 e1bfbacd85 e1bfbace99\ncea9ce99 e1bfbc e1bfbc cea9ce99\nce8fce99 ce8fcd85 ce8fcd85 ce8fce99\ncea9cd82 cea9cd82 cea9cd82 cea9cd82\ncea9cd82ce99 cea9cd82cd85 cea9cd82cd85 cea9cd82ce99\ncea9ce99 e1bfbc e1bfbc e1bfbc\n4646 4666 4666 4646\n4649 4669 4669 4649\n464c 466c 466c 464c\n464649 466669 466669 464649\n46464c 46666c 46666c 46464c\n5354 5374 5374 5354\n5354 5374 5374 5354\nd584d586 d584d5b6 d584d5b6 d584d586\nd584d4b5 d584d5a5 d584d5a5 d584d4b5\nd584d4bb d584d5ab d584d5ab d584d4bb\nd58ed586 d58ed5b6 d58ed5b6 d58ed586\nd584d4bd d584d5ad d584d5ad d584d4bd\n";
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var script = new LythonEngine().Compile(source, budget);
        Assert.True(script.IsValid);
        var baseline = budget.ReservedBytes;
        foreach (var asynchronous in new[] { false, true })
        {
            using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal(expected, result.StandardOutput);
            result.Dispose();
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        script.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }
}

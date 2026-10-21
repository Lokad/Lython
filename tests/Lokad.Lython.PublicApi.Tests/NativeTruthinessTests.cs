using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class NativeTruthinessTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task HashFlagValueErrorCannotHideRefusedAdmission(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import hashlib
            class Flag:
                def __bool__(self):
                    try: bytes(200000)
                    finally: raise ValueError('replace refusal')
            try: hashlib.new('sha256',usedforsecurity=Flag())
            except Exception: print('unreachable handler')
            print('unreachable continuation')
            """);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(32768);
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                                        : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.Equal(LythonFailureReason.SharedMemoryAllowance, result.FailureAttribution?.Reason);
            Assert.Equal("", result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task HashSecurityOptionsRunTheirPythonConversions(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import hashlib
            class Flag:
                def __bool__(self): print('flag'); return False
            class BadValue:
                def __bool__(self): print('value'); raise ValueError('bad flag')
            class BadType:
                def __bool__(self): print('type'); raise TypeError('bad flag')
            print(hashlib.sha256(b'abc',usedforsecurity=Flag()).hexdigest() == hashlib.sha256(b'abc').hexdigest())
            print(hashlib.new('sha256',b'abc',usedforsecurity=Flag()).hexdigest() == hashlib.sha256(b'abc').hexdigest())
            try: hashlib.sha256(usedforsecurity=BadValue())
            except ValueError as error: print(str(error))
            try: hashlib.new('sha256',usedforsecurity=BadType())
            except TypeError as error: print(str(error))
            print(len(hashlib.new('sha256',usedforsecurity=BadValue()).digest()))
            """);
        Assert.True(script.IsValid);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.True(result.Success, result.Failure?.Message);
        Assert.Equal("flag\nTrue\nflag\nTrue\nvalue\nbad flag\ntype\nbad flag\nvalue\n32\n", result.StandardOutput);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task MatcherOptionsAndPredicateResultsUseContextualTruthiness(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import difflib
            class Flag:
                def __init__(self): self.fail=False
                def __bool__(self):
                    print('flag')
                    if self.fail: raise ValueError('bad flag')
                    return False
            a='a'*200
            flag=Flag()
            matcher=difflib.SequenceMatcher(None,a,'b'+a,autojunk=flag)
            print(matcher.ratio())
            old=matcher.b
            flag.fail=True
            try: matcher.set_seq2('c'+a)
            except ValueError as error: print(str(error))
            print(matcher.b is old,matcher.ratio())
            print(difflib.SequenceMatcher(None,a,'b'+a,autojunk=None).ratio())
            print(difflib.SequenceMatcher(lambda c:Flag(),'aba','aca',autojunk=False).ratio())
            """);
        Assert.True(script.IsValid);
        var budget = new LythonMemoryBudget(524288);
        using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget })
                                        : script.Run(new MockLythonHost(), new LythonRunOptions { MemoryBudget = budget }))
        {
            Assert.True(result.Success, result.Failure?.Message);
            Assert.Equal("flag\n0.9975062344139651\nflag\nbad flag\nTrue 0.9975062344139651\n0.9975062344139651\nflag\nflag\n0.6666666666666666\n", result.StandardOutput);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task HashesStayUsableDuringErrorProjection(bool asynchronous)
    {
        using var script = new LythonEngine().Compile("""
            import hashlib
            h=hashlib.md5(b'abc')
            class Key:
                def __repr__(self): return h.hexdigest()
            raise KeyError(Key())
            """);
        Assert.True(script.IsValid);
        using var result = asynchronous ? await script.RunAsync(new MockLythonHost()) : script.Run(new MockLythonHost());
        Assert.Equal("KeyError", result.Failure?.ExceptionType);
        Assert.Contains("900150983cd24fb0d6963f7d28e17f72", result.Failure?.Message, StringComparison.Ordinal);
    }
}

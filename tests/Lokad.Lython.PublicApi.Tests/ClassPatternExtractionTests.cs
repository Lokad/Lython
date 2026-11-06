using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ClassPatternExtractionTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["all attributes before subpatterns", """
            class C:
             __match_args__=('a',)
             @property
             def a(self):print('a');return 0
             @property
             def b(self):print('b');return 1
            class K:expected=1
            match C():
             case C(K.expected,b=x):print(x)
             case _:print('no match')
            """, "a\nb\nno match\n"];
        yield return ["duplicate positional names", """
            class C:
             __match_args__=('a','a')
             @property
             def a(self):print('read');return 7
            try:
             match C():
              case C(x,y):print('matched')
            except TypeError as error:print(error)
            """, "read\nC() got multiple sub-patterns for attribute 'a'\n"];
        yield return ["duplicate positional and keyword", """
            class C:
             __match_args__=('a',)
             @property
             def a(self):print('read');return 7
            try:
             match C():
              case C(x,a=y):print('matched')
            except TypeError as error:print(error)
            """, "read\nC() got multiple sub-patterns for attribute 'a'\n"];
        yield return ["unused malformed name", """
            class C:
             __match_args__=('a',17)
             a=7
            match C():
             case C(x):print(x)
            """, "7\n"];
        yield return ["match args descriptor", """
            class Args:
             def __get__(self,obj,owner):print('args');return ('a',)
            class C:
             __match_args__=Args()
             a=7
            match C():
             case C(x):print(x)
            """, "args\n7\n"];
        yield return ["missing attribute mismatch", """
            class C:
             @property
             def a(self):raise AttributeError('missing')
            match C():
             case C(a=x):print(x)
             case _:print('no match')
            """, "no match\n"];
        yield return ["value comparison protocol", """
            class V:
             def __eq__(self,other):print('eq');return True
            class K:expected=7
            match V():
             case K.expected:print('match')
             case _:print('no match')
            """, "eq\nmatch\n"];
        yield return ["object root class", """
            match 7:
             case object():print('match')
             case _:print('no match')
            """, "match\n"];
        yield return ["non tuple match args", """
            class C:__match_args__='a'
            try:
             match C():
              case C(x):print(x)
            except TypeError as error:print(error)
            """, "C.__match_args__ must be a tuple (got str)\n"];
        yield return ["later malformed name preserves earlier getter", """
            class C:
             __match_args__=('a',17)
             @property
             def a(self):print('first');return 7
            try:
             match C():
              case C(x,y):pass
            except TypeError as error:print(error)
            """, "first\n__match_args__ elements must be strings (got int)\n"];
        yield return ["subpattern AttributeError is not a mismatch", """
            class C:a=7
            class D:
             def __get__(self,obj,owner):raise AttributeError('comparison')
            class K:expected=D()
            try:
             match C():
              case C(a=K.expected):print('match')
              case _:print('no match')
            except AttributeError as error:print(error)
            """, "comparison\n"];
        yield return ["extraction snapshot survives subject mutation", """
            class C:
             __match_args__=('a','b')
             def __init__(self):self.a=1;self.b=[7,8]
            source=C()
            class D:
             def __get__(self,obj,owner):source.b=[];return 1
            class K:expected=D()
            match source:
             case C(K.expected,[first,last] as held):print(first,last,held,source.b)
            """, "7 8 [7, 8] []\n"];
        yield return ["native named records share extraction", """
            from collections import namedtuple
            from urllib.parse import urlsplit, SplitResult
            Row=namedtuple('Row','left right')
            match Row(3,4):
             case Row(a,right=b):print(a+b)
            match urlsplit('https://example.com/a?q=7'):
             case SplitResult(scheme,netloc,path,query,fragment):print(scheme,netloc,path,query,fragment)
            try:
             match Row(3,4):
              case Row(a,left=b):pass
            except TypeError as error:print(error)
            """, "7\nhttps example.com /a q=7 \nRow() got multiple sub-patterns for attribute 'left'\n"];
        yield return ["value equality reflection and truth", """
            class Answer:
             def __bool__(self):print('truth');return True
            class Left:
             def __eq__(self,other):print('left');return NotImplemented
            class Right:
             def __eq__(self,other):print('right');return Answer()
            class K:expected=Right()
            match Left():
             case K.expected:print('match')
            """, "left\nright\ntruth\nmatch\n"];
        yield return ["metadata is skipped without positions", """
            class Args:
             def __get__(self,obj,owner):raise AssertionError('unused metadata')
            class C:__match_args__=Args();a=7
            match C():
             case C(a=x):print(x)
            match 7:
             case object():print('root')
            """, "7\nroot\n"];
        yield return ["zero positions and wrong type", """
            class C:a=7
            try:
             match C():
              case C(x):pass
            except TypeError as error:print(error)
            match 7:
             case C(x):raise AssertionError('wrong type')
             case _:print('skip')
            """, "C() accepts 0 positional sub-patterns (1 given)\nskip\n"];
    }

    [Fact]
    public async Task ExtractedAliasRemainsFundedThroughSuspendedBodyAndCleanup()
    {
        const string source = """
            class C:
             __match_args__=('a','b')
             def __init__(self):self.a=1;self.b=[1<<65536]
            source=C()
            class D:
             def __get__(self,obj,owner):source.b=[];return 1
            class K:expected=D()
            match source:
             case C(K.expected,[value] as held):
              assert value==1<<65536 and held[0] is value and source.b==[]
              with open('/hold','w') as stream:stream.write('held')
            """;
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost();
            var entered = host.PauseWriteUntilCancellation("/hold");
            using var cancellation = new CancellationTokenSource();
            var pending = script.RunAsync(host, cancellationToken: cancellation.Token);
            try
            {
                Assert.Same(entered, await Task.WhenAny(entered, pending).WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline + 8192);
            }
            finally
            {
                cancellation.Cancel();
                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ClassAndValuePatternsPreservePythonEffects(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics));
            var baseline = budget.ReservedBytes;
            using (var result = script.Run(new MockLythonHost()))
            {
                Assert.True(result.Success, name + ": " + result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            using (var result = await script.RunAsync(new MockLythonHost()))
            {
                Assert.True(result.Success, name + ": " + result.Failure?.Message);
                Assert.Equal(expected, result.StandardOutput);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}

using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class ReturnedIterationTests
{
    public static TheoryData<string, string, string> PythonGoldens => new()
    {
        { "nested-native-and-string-sources", NativeSources, "['A', 'B', 'X', 'Y', 1, 2, 'c', 'X', 'Y', 1, 2, 'E', 'F', 'X', 'Y', 1, 2] ['done']\n" },
        { "captured-and-forwarded", "def outer():\n count=0\n def words(value=' A B '):\n  nonlocal count\n  count+=1\n  return value.lower().split()\n def forward(value):return words(value=value)\n for word in forward(' C D '):print(word)\n result=forward(' E F ')\n result.append('g')\n print(type(result).__name__,result)\n words.__defaults__=(' NEW WORD ',)\n print([word for word in words()])\n print(count)\nouter()\n", "c\nd\nlist ['e', 'f', 'g']\n['new', 'word']\n3\n" },
        { "stored-result-remains-observable", "kept=[]\ndef outer():\n count=0\n def words(value):\n  nonlocal count\n  count+=1\n  result=value.lower().split()\n  kept.append(result)\n  return result\n seen=[]\n for word in words(' A B '):\n  seen.append(word)\n  if word=='a':kept[0].append('c')\n print(seen,type(kept[0]).__name__,count)\nouter()\n", "['a', 'b', 'c'] list 1\n" },
        { "cleanup-and-overridden-return", "events=[]\nclass Manager:\n def __enter__(self):events.append('enter')\n def __exit__(self,*args):events.append('exit');return False\ndef outer():\n count=0\n def words(value,override=False):\n  nonlocal count\n  count+=1\n  with Manager():\n   try:return value.upper().split()\n   finally:\n    events.append('finally')\n    if override:return ['override']\n for word in words(' a b '):events.append(word)\n for word in words(' ignored ',True):events.append(word)\n result=words(' c d ')\n print(events,type(result).__name__,result,count)\nouter()\n", "['enter', 'finally', 'exit', 'A', 'B', 'enter', 'finally', 'exit', 'override', 'enter', 'finally', 'exit'] list ['C', 'D'] 3\n" },
        { "generator-return-and-yield-from", "def outer():\n count=0\n def words(value):\n  nonlocal count\n  count+=1\n  return value.lower().split()\n def generate():\n  yield 'start'\n  return words(' A B ')\n generator=generate()\n print(next(generator))\n try:next(generator)\n except StopIteration as done:print(type(done.value).__name__,done.value)\n def delegated():yield from words(' C D ')\n print(list(delegated()),count)\nouter()\n", "start\nlist ['a', 'b']\n['c', 'd'] 2\n" },
        { "bound-default-and-dynamic-protocol", "class Words:\n def __init__(self):self.count=0\n def words(self,value=' A B '):\n  self.count+=1\n  return value.lower().split()\nclass Text:\n def lower(self):print('lower');return self\n def split(self):print('split');return ['custom']\ninstance=Words()\nsaved=instance.words\nprint([word for word in saved()])\nWords.words.__defaults__=(' C D ',)\nprint([word for word in instance.words()])\nprint([word for word in saved(value=Text())])\nprint(type(saved()).__name__,instance.count)\n", "['a', 'b']\n['c', 'd']\nlower\nsplit\n['custom']\nlist 4\n" },
        { "handler-and-return-exception", "def outer():\n count=0\n def words(value):\n  nonlocal count\n  count+=1\n  try:raise ValueError('local')\n  except ValueError as error:\n   return value.lower().split()\n try:raise RuntimeError('outer')\n except RuntimeError as previous:\n  for word in words(' A B '):\n   try:raise LookupError(word)\n   except LookupError as error:print(word,error.__context__ is previous)\n print(count)\nouter()\n", "a True\nb True\n1\n" },
        { "direct-branch-versus-stored-branch", "kept=[]\ndef outer():\n count=0\n def words(value,store):\n  nonlocal count\n  count+=1\n  if store:\n   result=value.upper().rsplit()\n   kept.append(result)\n   return result\n  return value.lower().split()\n print([word for word in words(' A B ',False)])\n for word in words(' c d ',True):\n  print(word,type(kept[-1]).__name__)\n print(words(' E F ',False),count)\nouter()\n", "['a', 'b']\nC list\nD list\n['e', 'f'] 3\n" },
    };

    private const string NativeSources = """
        events=[]
        def words(text,protected):
            if protected:
                try:return text.lower().split()
                finally:events.append('done')
            return text.upper().split()
        def outer():
            seen=[]
            for item in [(' A B ',False),(' C D ',True),(' E F ',False)]:
                for word in words(item[0],item[1]):
                    seen.append(word)
                    if word=='c':break
                for character in 'XY':seen.append(character)
                for number in (1,2):seen.append(number)
            print(seen,events)
        outer()
        """;

    [Fact]
    public async Task ProtectedReturnKeepsWithdrawnSourceFundedUntilCleanupFinishes()
    {
        const string source = """
            def words():
                global payload
                try:return payload.lower().split()
                finally:
                    payload=None
                    with open('/cleanup') as source:source.read()
            for word in words():print(len(word))
            """;
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join(';', script.Diagnostics));
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost();
            host.SeedFile("/cleanup", "ready");
            var readEntered = host.PauseReadUntilCancellation("/cleanup");
            var closeEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var closeReleased = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            host.BeforeFileClose = async () =>
            {
                closeEntered.TrySetResult();
                await closeReleased.Task;
            };
            using var cancellation = new CancellationTokenSource();
            var options = new LythonRunOptions
            {
                MemoryBudget = budget,
                CancellationToken = cancellation.Token,
                Globals = new Dictionary<string, object?> { ["payload"] = new string('A', 4096) + " B" },
            };
            var pending = script.RunAsync(host, options);
            try
            {
                await readEntered.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline + 8192);
                await cancellation.CancelAsync();
                await closeEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline + 8192);
                closeReleased.TrySetResult();
                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                Assert.Equal("", result.StandardOutput);
            }
            finally
            {
                await cancellation.CancelAsync();
                closeReleased.TrySetResult();
                using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            foreach (var asynchronous in new[] { false, true })
            {
                var reusedHost = new MockLythonHost();
                reusedHost.SeedFile("/cleanup", "ready");
                var reusedOptions = new LythonRunOptions
                {
                    MemoryBudget = budget,
                    Globals = new Dictionary<string, object?> { ["payload"] = new string('A', 4096) + " B" },
                };
                using (var result = asynchronous ? await script.RunAsync(reusedHost, reusedOptions)
                    : script.Run(reusedHost, reusedOptions))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("4096\n1\n", result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [MemberData(nameof(PythonGoldens))]
    public async Task DirectReturnConsumptionPreservesObservablePythonValues(string name, string source, string expected)
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, name + ": " + string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            var options = new LythonRunOptions { MemoryBudget = budget };
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options)
                    : script.Run(new MockLythonHost(), options))
                {
                    Assert.True(result.Success, name + ": " + result.Failure?.Message);
                    Assert.Equal(expected, result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false), InlineData(false, true)]
    [InlineData(true, false), InlineData(true, true)]
    public async Task CustomCasingResultIsConsumedByTheFollowingMemberAccess(bool forwarding, bool captured)
    {
        var source = "def outer():\n count=0\n class Text:\n  def lower(self):\n" +
            (captured ? "   nonlocal count\n   count+=1\n" : "") +
            "   return ' A B '.lower().split()\n def words(value):\n  nonlocal count\n  count+=1\n  return value.lower().split()\n try:\n  for word in " +
            (forwarding ? "words(Text())" : "Text().lower().split()") +
            ":pass\n except AttributeError as error:print(\"'list'\" in str(error),\"'split'\" in str(error))\nouter()";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid, string.Join("; ", script.Diagnostics.Select(d => d.Message)));
            var baseline = budget.ReservedBytes;
            var options = new LythonRunOptions { MemoryBudget = budget };
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options)
                    : script.Run(new MockLythonHost(), options))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal("True True\n", result.StandardOutput);
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    private const string FileSource = """
        def outer():
            count=0
            def words():
                nonlocal count
                count+=1
                with open('/source') as source:
                    text=source.read()
                return text.upper().split()
            result=[]
            for word in words():
                result.append(word)
            assert count==1
            return result
        return outer()
        """;

    [Theory]
    [MemberData(nameof(CasedWhitespacePipelineTests.UnicodeCases), MemberType = typeof(CasedWhitespacePipelineTests))]
    public async Task CapturedReturnPreservesUnicodeExpansionAndContext(string operation, string text, string[] expected)
    {
        var source = $"""
            def outer():
                count=0
                def words(value):
                    nonlocal count
                    count+=1
                    return value.{operation}().split()
                result=[]
                for word in words(value=text):result.append(word)
                assert count==1
                return result
            return outer()
            """;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var options = new LythonRunOptions { MemoryBudget = budget, Globals = new Dictionary<string, object?> { ["text"] = text } };
            foreach (var asynchronous in new[] { false, true })
            {
                using (var result = asynchronous ? await script.RunAsync(new MockLythonHost(), options)
                    : script.Run(new MockLythonHost(), options))
                {
                    Assert.True(result.Success, result.Failure?.Message);
                    Assert.Equal(expected, Assert.IsType<List<object?>>(result.ReturnValue).Cast<string>());
                }
                Assert.Equal(baseline, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public async Task PromotedReturnSurvivesRealIoAndRetainedProjection()
    {
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(FileSource, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost();
            host.SeedFile("/source", "Straße ος");
            host.BeforeFileClose = () =>
            {
                Assert.True(budget.ReservedBytes > baseline);
                return ValueTask.CompletedTask;
            };
            var options = new LythonRunOptions { MemoryBudget = budget };
            using (var retained = await script.RunAsync(host, options))
            {
                Assert.True(retained.Success, retained.Failure?.Message);
                Assert.True(host.CompletedAsynchronously > 0);
                Assert.Equal(1, host.ClosedFileCount);
                Assert.Equal(new[] { "STRASSE", "ΟΣ" }, Assert.IsType<List<object?>>(retained.ReturnValue).Cast<string>());
                var held = budget.ReservedBytes;
                var otherHost = new MockLythonHost();
                otherHost.SeedFile("/source", "other value");
                using (var other = script.Run(otherHost, options))
                {
                    Assert.True(other.Success, other.Failure?.Message);
                    Assert.True(budget.ReservedBytes > held);
                    Assert.Equal(new[] { "OTHER", "VALUE" }, Assert.IsType<List<object?>>(other.ReturnValue).Cast<string>());
                }
                Assert.Equal(held, budget.ReservedBytes);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false), InlineData(true)]
    public async Task CancellationJoinsCleanupBeforeRetiringReturnedWords(bool pauseBeforeReturn)
    {
        var source = FileSource.Replace("result.append(word)", "with open('/pause') as paused: paused.read()")
            .Replace("return outer()", "outer()\nprint('late')");
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using (var script = new LythonEngine().Compile(source, budget))
        {
            Assert.True(script.IsValid);
            var baseline = budget.ReservedBytes;
            var host = new DelayedLythonHost();
            host.SeedFile("/source", "first second");
            host.SeedFile("/pause", "pause");
            var reached = host.PauseReadUntilCancellation(pauseBeforeReturn ? "/source" : "/pause");
            var closeEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var closeReleased = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var closes = 0;
            host.BeforeFileClose = async () =>
            {
                if (++closes == (pauseBeforeReturn ? 1 : 2))
                {
                    closeEntered.TrySetResult();
                    await closeReleased.Task;
                }
            };
            using var cancellation = new CancellationTokenSource();
            var pending = script.RunAsync(host, new LythonRunOptions
            { MemoryBudget = budget, CancellationToken = cancellation.Token });
            try
            {
                await reached.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(pending.IsCompleted);
                cancellation.Cancel();
                await closeEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(pending.IsCompleted);
                Assert.True(budget.ReservedBytes > baseline);
                closeReleased.TrySetResult();
                using var result = await pending.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.False(result.Success);
                Assert.Equal(LythonFailureReason.Cancelled, result.FailureAttribution?.Reason);
                Assert.DoesNotContain("late", result.StandardOutput);
                Assert.Equal(pauseBeforeReturn ? 1 : 2, host.ClosedFileCount);
            }
            finally
            {
                cancellation.Cancel();
                closeReleased.TrySetResult();
                using var drained = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            }
            Assert.Equal(baseline, budget.ReservedBytes);
            var reusedHost = new MockLythonHost();
            reusedHost.SeedFile("/source", "");
            using (var reused = script.Run(reusedHost, new LythonRunOptions { MemoryBudget = budget }))
                Assert.True(reused.Success, reused.Failure?.Message);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}

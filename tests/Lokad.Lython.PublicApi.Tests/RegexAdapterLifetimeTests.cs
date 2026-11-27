using System.Text.Json;
using System.Security.Cryptography;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RegexAdapterLifetimeTests
{
    [Fact]
    public void PatternDisposalReturnsProgramFundingWhileParentRemainsUsable()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var owner = budget.CreateScope();
        Assert.True(owner.TryReserve(123, out var unrelated));
        var pattern = Utf8RegexProvider.Instance.Compile("(?P<value>a)"u8.ToArray(), new(32, false), owner, default);
        var input = "a"u8.ToArray();
        var window = new LythonRegexWindow(0, 1, 0, 1);
        var captures = new LythonRegexCapture?[pattern.CaptureSlotCount];
        Assert.True(pattern.TryMatch(input, window, LythonRegexMatchMode.Match, captures, out _, owner, default));
        Assert.True(budget.ReservedBytes > 123);
        pattern.Dispose();
        pattern.Dispose();
        Assert.Equal(123, budget.ReservedBytes);
        Assert.Throws<ObjectDisposedException>(() => pattern.Prepare(input, window, owner, default));
        Assert.Throws<ObjectDisposedException>(() => pattern.TryMatch(input, window, LythonRegexMatchMode.Match, captures, out _, owner, default));
        Assert.Throws<ObjectDisposedException>(() => pattern.NamedGroups);
        Assert.True(unrelated.TryResize(456));
        unrelated.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BoundedWindowUsesFundedPrefixWithoutReservingForTheSuffix(bool iterate)
    {
        var budget = new LythonMemoryBudget(256 * 1024);
        using (var owner = budget.CreateScope())
        {
            var prefix = "éx"u8;
            var length = prefix.Length + 64 * 1024;
            Assert.True(owner.TryReserve(length + 32L, out var inputStorage));
            using var inputFunding = inputStorage;
            var input = new byte[length];
            prefix.CopyTo(input);
            input.AsSpan(prefix.Length).Fill((byte)'a');
            using var pattern = Utf8RegexProvider.Instance.Compile("(?<=é)(?P<value>x)"u8.ToArray(), new(32, false), owner, default);
            var baseline = budget.ReservedBytes;
            var window = new LythonRegexWindow(1, 2, 2, 3);
            Span<LythonRegexCapture?> captures = stackalloc LythonRegexCapture?[2];
            int? lastIndex;
            if (iterate)
            {
                using var subject = pattern.Prepare(input, window, owner, default);
                using var cursor = subject.Find(default);
                Assert.True(cursor.TryNext(captures, out lastIndex, default));
            }
            else Assert.True(pattern.TryMatch(input, window, LythonRegexMatchMode.Match, captures, out lastIndex, owner, default));
            Assert.Equal(new LythonRegexCapture(2, 3), captures[0]);
            Assert.Equal(captures[0], captures[1]);
            Assert.Equal(1, lastIndex);
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(48)]
    public void UnicodeGroupNamesAndLastCloseUseAbsoluteByteRanges(int nameLength)
    {
        var budget = new LythonMemoryBudget();
        using (var owner = budget.CreateScope())
        {
            var name = new string('é', nameLength);
            var bytes = System.Text.Encoding.UTF8.GetBytes($"(?<=é)(?P<{name}>(a))b");
            using var pattern = Utf8RegexProvider.Instance.Compile(bytes, new(32, false), owner, default);
            var named = Assert.Single(pattern.NamedGroups);
            Assert.Equal(1, named.Index);
            Assert.Equal(name, System.Text.Encoding.UTF8.GetString(named.Name.Span));
            Span<LythonRegexCapture?> captures = stackalloc LythonRegexCapture?[3];
            Assert.True(pattern.TryMatch("préab!"u8.ToArray(), new(3, 5, 4, 6), LythonRegexMatchMode.FullMatch,
                captures, out var lastIndex, owner, default));
            Assert.Equal(new LythonRegexCapture(4, 6), captures[0]);
            Assert.Equal(new LythonRegexCapture(4, 5), captures[1]);
            Assert.Equal(captures[1], captures[2]);
            Assert.Equal(1, lastIndex);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("compile")]
    [InlineData("match")]
    [InlineData("value-match")]
    [InlineData("grouped-match")]
    [InlineData("cursor")]
    public void ActualBackendWorkObservesInFlightCancellationAndUnwinds(string stage)
    {
        var fixture = SubprocessProbeRunner.WorkerFixturePath();
        var result = SubprocessProbeRunner.Run(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            [fixture, "regex-cancellation", stage], timeout: TimeSpan.FromSeconds(25));
        Assert.True(result.ExitCode == 0, result.StandardError);
        Assert.Empty(result.StandardError);
        using var document = JsonDocument.Parse(result.StandardOutput);
        var libraries = document.RootElement.GetProperty("libraries");
        foreach (var assembly in new[]
        {
            typeof(LythonEngine).Assembly,
            typeof(Utf8RegexProvider).Assembly,
            typeof(Lokad.Utf8Regex.Utf8Regex).Assembly,
            typeof(Lokad.Utf8Regex.PythonRe.Utf8PythonRegex).Assembly,
        })
            Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))),
                libraries.GetProperty(assembly.GetName().Name!).GetString());
        Assert.True(document.RootElement.GetProperty("runningAtCancellation").GetBoolean());
        Assert.True(document.RootElement.GetProperty("cancelled").GetBoolean());
        Assert.True(document.RootElement.GetProperty("originalToken").GetBoolean());
        if (stage is "match" or "value-match")
            Assert.True(document.RootElement.GetProperty("backendInterrupted").GetBoolean());
        Assert.Equal(0, document.RootElement.GetProperty("balance").GetInt64());
    }

    [Fact]
    public void CursorUsesCurrentAdvanceTokenAndReturnsItsWorkingAllowance()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            using var earlier = new CancellationTokenSource();
            using var pattern = Utf8RegexProvider.Instance.Compile("a"u8.ToArray(), new(32, false), owner, earlier.Token);
            var baseline = budget.ReservedBytes;
            using (var subject = pattern.Prepare(Enumerable.Repeat((byte)'a', 4096).ToArray(), new(0, 4096, 0, 4096), owner, default))
            {
                using var cursor = subject.Find(earlier.Token);
                earlier.Cancel();
                var working = budget.ReservedBytes;
                var captures = new LythonRegexCapture?[pattern.CaptureSlotCount];
                for (var i = 0; i < 4096; i++)
                {
                    Assert.True(cursor.TryNext(captures, out _, default));
                    Assert.Equal(i, captures[0]!.Value.StartByte);
                    // Native cursor scratch may first grow on advance. Keep
                    // its working capacity funded until exhaustion/disposal.
                    Assert.True(budget.ReservedBytes >= working);
                }
                Assert.False(cursor.TryNext(captures, out _, default));
                Assert.Equal(baseline, budget.ReservedBytes);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void FirstWorkingAdmissionCancellationReturnsTheAdvanceTokenAndUnwinds()
    {
        var budget = new AdmissionGateBudget();
        using (var owner = budget.CreateScope())
        {
            using var cancellation = new CancellationTokenSource();
            using var pattern = Utf8RegexProvider.Instance.Compile("a"u8.ToArray(), new(32, false), owner, default);
            var baseline = budget.Root.ReservedBytes;
            using var subject = pattern.Prepare("a"u8.ToArray(), new(0, 1, 0, 1), owner, default);
            using var cursor = subject.Find(default);
            var captures = new LythonRegexCapture?[pattern.CaptureSlotCount];
            budget.OnNextReserve = cancellation.Cancel;
            var error = Assert.ThrowsAny<OperationCanceledException>(() => cursor.TryNext(captures, out _, cancellation.Token));
            Assert.Equal(cancellation.Token, error.CancellationToken);
            Assert.Equal(baseline, budget.Root.ReservedBytes);
            using var next = subject.Find(default);
            Assert.True(next.TryNext(captures, out _, default));
        }
        Assert.Equal(0, budget.Root.ReservedBytes);
    }

    [Theory]
    [InlineData(false, "(a)", "a")]
    [InlineData(false, "(a)(b)", "ab")]
    [InlineData(false, "(?:ab|ac)+", "abacab")]
    [InlineData(true, "a", "a")]
    public void RefusedWorkingAdmissionLeavesTheFundedPatternReusable(bool iterate, string expression, string input)
    {
        var retained = new LythonMemoryBudget();
        var working = new LythonMemoryBudget(0);
        using (var owner = retained.CreateScope())
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(input);
            var window = new LythonRegexWindow(0, input.Length, 0, bytes.Length);
            using var pattern = Utf8RegexProvider.Instance.Compile(System.Text.Encoding.UTF8.GetBytes(expression), new(32, false), owner, default);
            var programBytes = retained.ReservedBytes;
            Assert.True(programBytes > 0);
            var captures = new LythonRegexCapture?[pattern.CaptureSlotCount];
            var error = Assert.Throws<LythonRegexException>(() =>
            {
                if (iterate)
                {
                    using var subject = pattern.Prepare(bytes, window, working, default);
                    using var cursor = subject.Find(default);
                    cursor.TryNext(captures, out _, default);
                }
                else pattern.TryMatch(bytes, window, LythonRegexMatchMode.Match, captures, out _, working, default);
            });
            Assert.Equal(LythonRegexFailure.MemoryAdmission, error.Failure);
            Assert.True(error.RequestedBytes > 0);
            Assert.Equal(0, working.ReservedBytes);
            Assert.Equal(programBytes, retained.ReservedBytes);
            Assert.True(pattern.TryMatch(bytes, window, LythonRegexMatchMode.Match, captures, out _, owner, default));
        }
        Assert.Equal(0, retained.ReservedBytes);
    }

    [Fact]
    public void RefusedProgramConstructionReleasesItsTemporaryOwner()
    {
        var budget = new LythonMemoryBudget(0);
        var error = Assert.Throws<LythonRegexException>(() =>
            Utf8RegexProvider.Instance.Compile("(?P<name>a+)"u8.ToArray(), new(32, false), budget, default));
        Assert.Equal(LythonRegexFailure.MemoryAdmission, error.Failure);
        Assert.True(error.RequestedBytes > 0);
        Assert.Equal(0, budget.ReservedBytes);
    }

}

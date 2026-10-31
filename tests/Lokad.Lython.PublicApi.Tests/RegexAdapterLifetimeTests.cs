using System.Reflection;
using System.Text.Json;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.PublicApi.Tests;

public sealed class RegexAdapterLifetimeTests
{
    [Fact]
    public void PatternDisposalReturnsProgramAndTemplateFundingWhileParentRemainsUsable()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using var owner = budget.CreateScope();
        Assert.True(owner.TryReserve(123, out var unrelated));
        var pattern = Utf8RegexProvider.Instance.Compile("(?P<value>a)"u8.ToArray(), new(32, false), owner, default);
        using (var subject = pattern.Prepare("a"u8.ToArray(), new(0, 1, 0, 1), owner, default))
            Assert.Equal("b"u8.ToArray(), subject.Substitute("b"u8.ToArray(), 0, default).Value.ToArray());
        Assert.True(budget.ReservedBytes > 123);
        pattern.Dispose();
        pattern.Dispose();
        Assert.Equal(123, budget.ReservedBytes);
        Assert.Throws<ObjectDisposedException>(() => pattern.Prepare("a"u8.ToArray(), new(0, 1, 0, 1), owner, default));
        Assert.Throws<ObjectDisposedException>(() => pattern.NamedGroups);
        Assert.True(unrelated.TryResize(456));
        unrelated.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData("compile")]
    [InlineData("match")]
    [InlineData("cursor")]
    [InlineData("substitute")]
    [InlineData("split")]
    public void ActualBackendWorkObservesInFlightCancellationAndUnwinds(string stage)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Directory.Build.props"))) root = root.Parent;
        Assert.NotNull(root);
        var configuration = GetType().Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var fixture = Path.Combine(root.FullName, "tests", "Lokad.Lython.WorkerFixture", "bin", configuration,
            SubprocessProbeRunner.TargetFramework, "Lokad.Lython.WorkerFixture.dll");
        Assert.True(File.Exists(fixture));
        var result = SubprocessProbeRunner.Run(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            [fixture, "regex-cancellation", stage], timeout: TimeSpan.FromSeconds(25));
        Assert.True(result.ExitCode == 0, result.StandardError);
        Assert.Empty(result.StandardError);
        using var document = JsonDocument.Parse(result.StandardOutput);
        Assert.True(document.RootElement.GetProperty("runningAtCancellation").GetBoolean());
        Assert.True(document.RootElement.GetProperty("cancelled").GetBoolean());
        Assert.True(document.RootElement.GetProperty("originalToken").GetBoolean());
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
                for (var i = 0; i < 4096; i++)
                {
                    var match = Assert.IsType<LythonRegexMatch>(cursor.Next(default));
                    Assert.Equal(i, match.Captures[0]!.Value.StartByte);
                    Assert.Equal(working, budget.ReservedBytes);
                }
                Assert.Null(cursor.Next(default));
                Assert.Equal(baseline, budget.ReservedBytes);
            }
            Assert.Equal(baseline, budget.ReservedBytes);
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void ReplacementCacheSurvivesWorkingCleanupWithoutCumulativeCharging()
    {
        var budget = new LythonMemoryBudget(2 * 1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            using var pattern = Utf8RegexProvider.Instance.Compile("a"u8.ToArray(), new(32, false), owner, default);
            var compiled = budget.ReservedBytes;
            using (var subject = pattern.Prepare("a"u8.ToArray(), new(0, 1, 0, 1), owner, default))
                Assert.Equal("first"u8.ToArray(), subject.Substitute("first"u8.ToArray(), 0, default).Value.ToArray());
            var cached = budget.ReservedBytes;
            Assert.True(cached > compiled);
            foreach (var template in new[] { "other", "third", "first" })
            {
                using (var subject = pattern.Prepare("a"u8.ToArray(), new(0, 1, 0, 1), owner, default))
                    _ = subject.Substitute(System.Text.Encoding.UTF8.GetBytes(template), 0, default);
                Assert.Equal(cached, budget.ReservedBytes);
            }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Fact]
    public void RefusedCursorAdmissionLeavesPatternAndWorkingOwnerReusable()
    {
        var retained = new LythonMemoryBudget();
        var working = new LythonMemoryBudget(0);
        using (var owner = retained.CreateScope())
        {
            using var pattern = Utf8RegexProvider.Instance.Compile("a"u8.ToArray(), new(32, false), owner, default);
            using (var subject = pattern.Prepare("a"u8.ToArray(), new(0, 1, 0, 1), working, default))
            {
                var error = Assert.Throws<LythonRegexException>(() => subject.Find(default));
                Assert.Equal(LythonRegexFailure.MemoryAdmission, error.Failure);
                Assert.True(error.RequestedBytes > 0);
            }
            Assert.Equal(0, working.ReservedBytes);
            using var next = pattern.Prepare("a"u8.ToArray(), new(0, 1, 0, 1), owner, default);
            Assert.NotNull(next.Match(LythonRegexMatchMode.Match, default));
        }
        Assert.Equal(0, retained.ReservedBytes);
    }

    [Fact]
    public void ReplacementCacheAdmissionPrecedesPublicationAndKeepsItsOwnerFunded()
    {
        var retained = new LythonMemoryBudget(100 * 1024);
        var working = new LythonMemoryBudget();
        using (var owner = retained.CreateScope())
        {
            using var pattern = Utf8RegexProvider.Instance.Compile("a"u8.ToArray(), new(32, false), owner, default);
            var baseline = retained.ReservedBytes;
            using (var subject = pattern.Prepare("a"u8.ToArray(), new(0, 1, 0, 1), working, default))
            {
                var error = Assert.Throws<LythonRegexException>(() => subject.Substitute(new byte[64], 0, default));
                Assert.Equal(LythonRegexFailure.MemoryAdmission, error.Failure);
                Assert.True(error.RequestedBytes > 0);
                Assert.Equal(baseline, retained.ReservedBytes);
            }
            Assert.Equal(0, working.ReservedBytes);
            // A fresh operation can still use the allowance after failed cleanup.
            using (var next = pattern.Prepare("a"u8.ToArray(), new(0, 1, 0, 1), working, default))
                Assert.Equal("b"u8.ToArray(), next.Substitute("b"u8.ToArray(), 0, default).Value.ToArray());
            Assert.True(retained.ReservedBytes > baseline);
            Assert.Equal(0, working.ReservedBytes);
        }
        Assert.Equal(0, retained.ReservedBytes);
    }
}

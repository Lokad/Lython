using System.Runtime.CompilerServices;
using Lokad.Lython.Utf8Regex;

namespace Lokad.Lython.PublicApi.Tests;

[Collection("RuntimeStress")]
public sealed class Utf8RegexSubjectLifetimeTests
{
    [Fact]
    public void DisposedSubjectAndCursorReleaseTheBorrowedInput()
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using var pattern = Utf8RegexProvider.Instance.Compile("(?<=b)(a)"u8.ToArray(), new(32, false), budget, default);
        var baseline = budget.ReservedBytes;
        var (prepared, input) = OpenSubject(pattern, budget);
        using var subject = prepared;
        using var cursor = subject.Find(default);
        Span<LythonRegexCapture?> captures = stackalloc LythonRegexCapture?[2];
        Assert.True(cursor.TryNext(captures, out var lastIndex, default));
        Assert.Equal(new LythonRegexCapture(1, 2), captures[0]);
        Assert.Equal(captures[0], captures[1]);
        Assert.Equal(1, lastIndex);
        Assert.True(budget.ReservedBytes > baseline);

        cursor.Dispose();
        subject.Dispose();
        Assert.Equal(baseline, budget.ReservedBytes);
        GC.Collect();
        Assert.False(input.TryGetTarget(out _));
        GC.KeepAlive(cursor);
        GC.KeepAlive(subject);
        pattern.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (ILythonRegexSubject, WeakReference<byte[]>) OpenSubject(ILythonRegexPattern pattern, ILythonMemoryBudget budget)
    {
        var bytes = "ba"u8.ToArray();
        return (pattern.Prepare(bytes, new(0, 2, 0, 2), budget, default), new(bytes));
    }
}

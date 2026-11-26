using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class RegexFindIteratorReentryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReentrantPullPreservesTheActiveMatchAndCursorFunding(bool duringPreparation)
    {
        var budget = new LythonMemoryBudget(1024 * 1024);
        using (var owner = budget.CreateScope())
        {
            var admission = new AdmissionHookMemoryBudget(owner);
            var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
                new() { MemoryBudget = admission }, regexProvider: Utf8RegexProvider.Instance);
            var span = new LythonSourceSpan(4, 9, 1, 5);
            try
            {
                var pattern = await LythonRuntime.RegexCompiler.CreatePattern([PyString.FromString("(é)|(x)")],
                    "re.compile", span, context, asynchronous: false);
                var subject = PyString.FromString("éxé", context.MemoryGovernor, span);
                var range = LythonRuntime.RegexCompiler.CreateSubjectRange(subject, 0, subject.Length, span, context);
                using var iterator = LythonRuntime.PyRegexFindIterator.Create(pattern, range, context, span);
                if (!duringPreparation) AssertMatch(0, 2, 0, 1);

                LythonRuntimeException? reentry = null;
                admission.AfterGrowth = _ =>
                {
                    admission.AfterGrowth = null;
                    reentry = Assert.Throws<LythonRuntimeException>(() => iterator.TryMoveNext());
                };

                if (duringPreparation) AssertMatch(0, 2, 0, 1);
                AssertMatch(2, 3, 1, 2);
                Assert.NotNull(reentry);
                Assert.Equal("ValueError", reentry.ExceptionType);
                Assert.Equal("regular expression scanner already executing", reentry.Message);
                Assert.Equal(span, reentry.Span);

                AssertMatch(3, 5, 2, 1);
                Assert.False(iterator.TryMoveNext().HasValue);
                Assert.False(iterator.TryMoveNext().HasValue);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);

                void AssertMatch(int startByte, int endByte, int start, int lastIndex)
                {
                    var match = Assert.IsType<LythonRuntime.ReMatchObject>(iterator.TryMoveNext().Value);
                    Assert.Same(subject, match.String);
                    Assert.Equal(new LythonRuntime.ReCaptureRange(startByte, endByte, start, start + 1), match.Captures[0]);
                    Assert.Equal(lastIndex, match.LastCapturedIndex);
                }
            }
            finally { context.State.CloseResources(); }
        }
        Assert.Equal(0, budget.ReservedBytes);
    }
}

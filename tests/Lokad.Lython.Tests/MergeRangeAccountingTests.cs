using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG17: merged-range entries commit per new range and release on unmerge;
/// duplicate merges and ungoverned sheets stay free.
/// </summary>
public sealed class MergeRangeAccountingTests
{
    private sealed class MergeHarness
    {
        public MergeHarness()
        {
            Host = new MockLythonHost();
            Context = new LythonRuntime.ExecutionContext(Host, new LythonRunOptions());
            Span = new LythonSourceSpan(0, 0, 0, 0);
            Sheet = new LythonRuntime.OpenPyxlWorksheet("S");
            Sheet.AttachMemoryGovernor(Context.MemoryGovernor, Span);
        }

        public MockLythonHost Host { get; }

        public LythonRuntime.ExecutionContext Context { get; }

        public LythonSourceSpan Span { get; }

        public LythonRuntime.OpenPyxlWorksheet Sheet { get; }

        public object Call(string member, params object[] arguments)
        {
            if (!Lokad.Lython.Runtime.PyMemberAccess.TryResolve(Sheet, member, Context, Span, out var found) ||
                found is not LythonRuntime.ICallable callable)
            {
                throw new InvalidOperationException($"Member {member} not found.");
            }

            var bound = new CallArgumentValue[arguments.Length];
            for (var i = 0; i < arguments.Length; i++)
            {
                bound[i] = CallArgumentValue.Positional(arguments[i]);
            }

            return callable.Invoke(bound, Span, Context);
        }
    }

    [Fact]
    public void MergeSlotsCommitAndReleaseExactly()
    {
        var harness = new MergeHarness();
        harness.Call("merge_cells", PyString.FromString("A1:A2"));
        harness.Call("merge_cells", PyString.FromString("B1:B2"));
        harness.Call("merge_cells", PyString.FromString("A1:A2"));
        Assert.Equal(2L * 64L, harness.Context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, harness.Context.MemoryGovernor.CurrentReservedBytes);
        harness.Call("unmerge_cells", PyString.FromString("A1:A2"));
        Assert.Equal(64L, harness.Context.MemoryGovernor.CurrentCommittedBytes);
    }

    [Fact]
    public void UngovernedMergesStayFree()
    {
        var sheet = new LythonRuntime.OpenPyxlWorksheet("S");
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        if (!Lokad.Lython.Runtime.PyMemberAccess.TryResolve(sheet, "merge_cells", context, span, out var found) ||
            found is not LythonRuntime.ICallable callable)
        {
            throw new InvalidOperationException("Member merge_cells not found.");
        }

        _ = callable.Invoke([CallArgumentValue.Positional(PyString.FromString("A1:A2"))], span, context);
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}
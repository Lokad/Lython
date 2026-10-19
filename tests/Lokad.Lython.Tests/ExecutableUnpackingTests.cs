using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

public sealed class ExecutableUnpackingTests
{
    private static readonly LythonSourceSpan Span = new(0, 0, 0, 0);

    [Fact]
    public void OrdinaryNestedGroupsAndLeavesUseExecutableOperations()
    {
        var syntax = LythonFrontend.Compile("def run(values,box):\n first,(second,*rest),box.value=values\n return first,second,rest,box.value").Script!;
        var code = Assert.Single(ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint.Functions).CodeObject!;
        Assert.Empty(code.ExpressionFallbacks);
        Assert.Empty(code.StatementFallbacks);
        Assert.Equal(2, code.Constants.OfType<ExecutableUnpackValues>().Count());
        Assert.Single(code.Constants.OfType<ExecutableStoreMember>());
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public async Task StarredRestAdmissionPrecedesPrefixEffects(bool asynchronous, bool yielding, bool nested)
    {
        const long limit = 1024 * 1024;
        var context = new LythonRuntime.ExecutionContext(new MockLythonHost(),
            new LythonRunOptions { MemoryBudget = new LythonMemoryBudget(limit) });
        var sink = new Sink();
        var outerSink = new Sink();
        using var values = new LimitAfterEnumeration(context.MemoryGovernor, limit);
        context.Variables["sink"] = sink;
        context.Variables["outerSink"] = outerSink;
        context.Variables["source"] = values;
        context.Variables["outer"] = new PyList([PyNone.Instance, values], context.MemoryGovernor, Span);
        var source = (yielding, nested) switch
        {
            (false, false) => "sink.value,*rest=source",
            (true, false) => "def generate():\n (yield 'sink').value,*rest=source\nnext(generate())",
            (false, true) => "outerSink.value,(sink.value,*rest)=outer",
            _ => "def generate():\n outerSink.value,((yield 'sink').value,*rest)=outer\nnext(generate())",
        };
        var syntax = LythonFrontend.Compile(source).Script!;
        var code = ExecutableScript.Compile(LoweredScript.Lower(syntax)).EntryPoint;
        try
        {
            LythonRuntimeException error;
            if (asynchronous)
                error = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context));
            else error = Assert.Throws<LythonRuntimeException>(() => LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null));
            Assert.Equal("MemoryError", error.ExceptionType);
            Assert.True(values.LimitedAllowance);
            Assert.Equal(1, values.DisposedEnumerations);
            Assert.Equal(0, sink.Stores);
            Assert.Equal(nested ? 1 : 0, outerSink.Stores);
            Assert.Null(context.CurrentExecutableFrame);
            Assert.True(context.MemoryGovernor.PeakAccountedBytes <= limit);
        }
        finally { values.Dispose(); }
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    internal sealed class Sink : IPyMutableDynamicAttributes
    {
        public int Stores { get; private set; }
        public bool TryGetMember(string name, out object value) { value = PyNone.Instance; return false; }
        public bool TrySetMember(string name, object value) { Stores++; return true; }
    }

    internal sealed class LimitAfterEnumeration(MemoryGovernor governor, long limit) : IEnumerable<object>, IDisposable
    {
        private IDisposable? _reservation;
        public bool LimitedAllowance { get; private set; }
        public int DisposedEnumerations { get; private set; }
        public IEnumerator<object> GetEnumerator()
        {
            try
            {
                for (var i = 0; i < 256; i++) yield return PyNone.Instance;
                // Leave control headroom, insufficient for the required rest list.
                _reservation = governor.ReserveTemporary(limit - governor.CurrentAccountedBytes - 1024, Span);
                LimitedAllowance = true;
            }
            finally { DisposedEnumerations++; }
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        public void Dispose() { _reservation?.Dispose(); _reservation = null; }
    }
}

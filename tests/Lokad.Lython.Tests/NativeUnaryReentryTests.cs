using System.Diagnostics.CodeAnalysis;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class NativeUnaryReentryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LaterGlobalReadObservesAdmissionRebinding(bool asynchronous)
    {
        var root = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = root.CreateScope();
        var budget = new RebindingBudget(owner);
        var mock = new MockLythonHost();
        var delayed = new DelayedLythonHost();
        var context = new Context(asynchronous ? delayed : mock,
            new LythonRunOptions { MemoryBudget = budget });
        var definition = Compile("word='old'\nseed=1<<100000\nclass Text:\n def upper(self):\n  with open('effect.txt','w') as target:target.write('once')\n  return 'replacement'\nreplacement=Text()\nscope=globals()\ndef read(value):\n global word\n if ~value:return word.upper()\n return ''");
        await Run(definition);
        var globals = Assert.IsType<PyDict>(context.Variables["scope"]);
        var replacement = context.Variables["replacement"];
        var key = PyString.FromString("word");
        var changed = false;
        context.Variables["arm"] = new Arm(() => budget.AfterMajorGrowth = () =>
        {
            globals.SetItem(key, replacement);
            changed = true;
        });
        var result = await Run(Compile("arm()\nreturn read(seed)"));
        Assert.True(changed);
        Assert.Equal("replacement", Assert.IsType<PyString>(result).AsString());
        Assert.Equal("once", asynchronous ? delayed.ReadText("/effect.txt") : mock.ReadText("/effect.txt"));
        Assert.Equal(0, context.State.CurrentInterpreterDepth);
        Assert.Equal(0, context.State.CurrentRecursionDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
        owner.Dispose();
        Assert.Equal(0, root.ReservedBytes);

        async ValueTask<object?> Run(ExecutableCodeObject code) => asynchronous
            ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
            : LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
    }

    private static ExecutableCodeObject Compile(string source)
        => ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;

    private sealed class Arm(Action action) : LythonRuntime.ICallable
    {
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        { action(); return PyNone.Instance; }
    }

    private sealed class RebindingBudget(ILythonMemoryBudget inner) : ILythonMemoryBudget
    {
        internal Action? AfterMajorGrowth;
        public ILythonMemoryScope CreateScope() => inner.CreateScope();
        public bool TryReserve(long bytes, [NotNullWhen(true)] out ILythonMemoryReservation? reservation)
        {
            if (!inner.TryReserve(bytes, out var admitted)) { reservation = null; return false; }
            reservation = new Reservation(this, admitted);
            return true;
        }
        private sealed class Reservation(RebindingBudget budget, ILythonMemoryReservation inner) : ILythonMemoryReservation
        {
            public long Bytes => inner.Bytes;
            public bool TryResize(long bytes)
            {
                var before = inner.Bytes;
                var accepted = inner.TryResize(bytes);
                if (accepted && bytes - before >= 8192 && budget.AfterMajorGrowth is { } action)
                { budget.AfterMajorGrowth = null; action(); }
                return accepted;
            }
            public void Dispose() => inner.Dispose();
        }
    }
}

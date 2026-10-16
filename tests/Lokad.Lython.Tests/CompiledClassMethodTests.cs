using System.Numerics;
using System.Reflection;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;
using ICallable = Lokad.Lython.Runtime.LythonRuntime.ICallable;

namespace Lokad.Lython.Tests;

public sealed class CompiledClassMethodTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealClassDefinitionsReuseInstructionsWithPrivateFunctionValues(bool asynchronous)
    {
        const string source = "class Counter:\n def __init__(self,start=0):self.value=start\n def add(self,n):\n  self.value+=n\n  return self.value\ncounter=Counter(2)\nanswer=counter.add(3)";
        var lowered = LoweredScript.Lower(LythonFrontend.Compile(source).Script!);
        var definition = Assert.Single(lowered.Statements.OfType<LoweredClassDefinitionStatement>());
        var method = Assert.Single(definition.Body.OfType<LoweredFunctionDefinitionStatement>(), f => f.Syntax.Name == "add");
        var prepared = Assert.IsType<ExecutableCodeObject>(method.PreparedCode);
        Assert.Empty(prepared.ExpressionFallbacks);
        Assert.Empty(prepared.StatementFallbacks);
        Assert.False(prepared.RequiresLocalVariableMirroring);
        var module = ExecutableScript.Compile(lowered).EntryPoint;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        PyExecutableFunction? previous = null;
        for (var i = 0; i < 2; i++)
        {
            var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
            _ = await Execute(module, context, asynchronous);
            Assert.Equal(new BigInteger(5), context.Variables["answer"]);
            var type = Assert.IsType<PyType>(context.Variables["Counter"]);
            Assert.True(type.TryGetOwnMember("add", out var raw));
            var function = Assert.IsType<PyExecutableFunction>(raw);
            Assert.NotSame(previous, function);
            Assert.Same(prepared, typeof(PyExecutableFunction).GetField("_codeObject", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(function));
            Assert.Same(type, function.OwnerType);
            previous = function;
            AssertUnwound(context);
        }
        Assert.True(budget.ReservedBytes > 0);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NamespaceCellAdmissionPrecedesFunctionPublication(bool asynchronous)
    {
        const long limit = 1024 * 1024;
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner, MaxExecutionMemoryBytes = limit });
        using var pad = new Pad(PyString.FromString("padding", context.MemoryGovernor, null), limit);
        context.Variables["fill"] = pad;
        const string source = "def make():\n value=7\n class Box:\n  fill()\n  def read(self):\n   nonlocal value\n   return value,__class__\n return Box\nmake()";
        var module = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
        var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Execute(module, context, asynchronous));
        Assert.Same(context.MemoryGovernor.AdmissionFailure, failure);
        Assert.Equal(LythonFailureReason.ExecutionMemoryLimit, failure.Attribution?.Reason);
        Assert.Equal(5, failure.Span?.Line);
        Assert.Equal(1, pad.Calls);
        Assert.False(context.Variables.ContainsKey("Box"));
        Assert.True(context.MemoryGovernor.CurrentReservedBytes > 0);
        pad.Dispose();
        AssertUnwound(context);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FiniteCopyHookCanRevisitTheSameObject(bool asynchronous)
    {
        const string source = "import copy\nclass Item:\n def __init__(self):self.left=3\n def __deepcopy__(self,memo):\n  self.left-=1\n  return copy.deepcopy(self,memo) if self.left else 7\nanswer=copy.deepcopy(Item())";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
        await Execute(code, context, asynchronous);
        Assert.Equal(new BigInteger(7), context.Variables["answer"]);
        Assert.Equal(0, context.State.StructuralTraversal.Depth);
        AssertUnwound(context);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RecursiveCopyAdmissionUnwindsExistingCountersAndCleanup(bool asynchronous, bool helper)
    {
        var source = "import copy\ncleanup=0\nclass Loop:\n def __deepcopy__(self,memo):\n  global cleanup\n  try:return " +
            (helper ? "self.again(memo)" : "copy.deepcopy(self,memo)") +
            "\n  finally:cleanup+=1\n def again(self,memo):return copy.deepcopy(self,memo)\ncopy.deepcopy(Loop())";
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        using var owner = budget.CreateScope();
        var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
        var failure = await Assert.ThrowsAsync<LythonRuntimeException>(async () => await Execute(code, context, asynchronous));
        Assert.Equal(LythonFailureReason.InterpreterStackLimit, failure.Attribution?.Reason);
        Assert.Equal(LythonRuntime.ExecutionLimits.MaxStructuralCallbackDepth, failure.Attribution?.Limit);
        Assert.True(Assert.IsType<BigInteger>(context.Variables["cleanup"]) > 0);
        Assert.Equal(0, context.State.StructuralTraversal.Depth);
        AssertUnwound(context);
        const string recovery = "answer=copy.deepcopy([1,[2]])";
        code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(recovery).Script!)).EntryPoint;
        await Execute(code, context, asynchronous);
        Assert.Equal(0, context.State.StructuralTraversal.Depth);
        AssertUnwound(context);
        owner.Dispose();
        Assert.Equal(0, budget.ReservedBytes);
    }

    private static async ValueTask<object?> Execute(ExecutableCodeObject code, Context context, bool asynchronous)
        => asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
            : LythonRuntime.ExecuteExecutableCodeObject(code, context, default, [], null);

    private static void AssertUnwound(Context context)
    {
        Assert.Null(context.CurrentExecutableFrame);
        Assert.Equal(0, context.Limits.CurrentRecursionDepth);
        Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private sealed class Pad(PyString result, long limit) : ICallable, IDisposable
    {
        private MemoryGovernor.TemporaryMemoryReservation? _storage;
        internal int Calls;
        public object Invoke(CallArgumentValue[] arguments, LythonSourceSpan span, Context context)
        {
            Calls++;
            _storage = context.MemoryGovernor.ReserveTemporary(limit - context.MemoryGovernor.CurrentAccountedBytes, span);
            // The result aliases funded storage; no transient scalar preflight
            // should precede the method's deliberately refused cell admission.
            return result;
        }
        public void Dispose() => _storage?.Dispose();
    }
}

using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime;
using Lokad.Lython.Tests.Harness;
using Context = Lokad.Lython.Runtime.LythonRuntime.ExecutionContext;

namespace Lokad.Lython.Tests;

public sealed class AnnotatedHelperExecutionTests
{
    [Theory]
    [InlineData("def helper(value:int):return value\nreturn helper")]
    [InlineData("def helper(value)->int:return value\nreturn helper")]
    [InlineData("def helper(value:int)->int:return value+1\nreturn helper")]
    [InlineData("from __future__ import annotations\ndef helper(value:missing)->missing:return value\nreturn helper")]
    [InlineData("def make(seed):\n Kind=int\n def helper(value:Kind=2)->Kind:return seed+value\n seed+=1\n return helper\nreturn make(3)")]
    [InlineData("def helper(value:'int')->'int':return value+1\nreturn helper")]
    [InlineData("def helper(value):\n result:int\n result=value+1\n return result\nreturn helper")]
    [InlineData("def helper(value):\n value:int\n return value\nreturn helper")]
    [InlineData("def helper(value):\n value.field:missing()\n return 7\nreturn helper")]
    [InlineData("def outer():\n result:int\n result=7\n def inner():return result\n return inner\nreturn outer")]
    public async Task OrdinaryAnnotatedBodiesRemainExecutable(string source)
    {
        var code = ExecutableScript.Compile(LoweredScript.Lower(LythonFrontend.Compile(source).Script!)).EntryPoint;
        Assert.False(code.RequiresLocalVariableMirroring);
        var budget = new LythonMemoryBudget(4 * 1024 * 1024);
        foreach (var asynchronous in new[] { false, true })
        {
            using (var owner = budget.CreateScope())
            {
                var context = new Context(new MockLythonHost(), new LythonRunOptions { MemoryBudget = owner });
                var result = asynchronous ? await LythonRuntime.ExecuteExecutableCodeObjectAsync(code, context)
                    : LythonRuntime.ExecuteExecutableCodeObject(code, context, default, null);
                Assert.IsType<PyExecutableFunction>(result);
                Assert.Null(context.CurrentExecutableFrame);
                Assert.Equal(0, context.Limits.CurrentInterpreterDepth);
                Assert.Equal(0, context.Limits.CurrentRecursionDepth);
                Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
            }
            Assert.Equal(0, budget.ReservedBytes);
        }
    }
}

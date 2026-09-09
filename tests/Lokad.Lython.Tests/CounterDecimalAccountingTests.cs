using System.Numerics;
using System.Reflection;
using Lokad.Lython.Runtime;
using Lokad.Lython.Runtime.Text;
using Lokad.Lython.Tests.Harness;

namespace Lokad.Lython.Tests;

/// <summary>
/// MG11: fresh decimal counts produced on Counter paths own their storage;
/// increments, totals and arithmetic over decimal counts commit per fresh value.
/// Ungoverned counters stay free.
/// </summary>
public sealed class CounterDecimalAccountingTests
{
    [Fact]
    public void CounterDecimalIncrementsCommitExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var key = PyString.FromString("a");
        var counter = new PyCounter(context.MemoryGovernor, span);
        counter.SetItem(key, new PyDecimal(1m));
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        counter.Increment(key, new PyDecimal(2m), span);
        Assert.True(counter.TryGetValue(key, out var stored));
        Assert.Equal(3m, ((PyDecimal)stored).Value);
        Assert.Equal(before + 64L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void CounterDecimalTotalsCommitExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var counter = new PyCounter(context.MemoryGovernor, span);
        counter.SetItem(PyString.FromString("a"), new PyDecimal(1m));
        counter.SetItem(PyString.FromString("b"), new PyDecimal(2m));
        Assert.True(LythonRuntime.CounterMembers.TryGetMember(counter, "total", out var member));
        var callable = (LythonRuntime.ICallable)member;
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        var total = callable.Invoke([], span, context);
        Assert.Equal(3m, ((PyDecimal)total).Value);
        Assert.Equal(before + 128L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void UngovernedCounterDecimalsStayFree()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var counter = new PyCounter();
        counter.SetItem(PyString.FromString("a"), new PyDecimal(1m));
        counter.Increment(PyString.FromString("a"), new PyDecimal(2m), span);
        Assert.Equal(0, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    [Fact]
    public void CounterDecimalSubtractsCommitExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var key = PyString.FromString("a");
        var counter = new PyCounter(context.MemoryGovernor, span);
        counter.SetItem(key, new PyDecimal(1m));
        var delta = new PyDict(context.MemoryGovernor, span);
        delta.SetItem(key, new PyDecimal(2m));
        Assert.True(LythonRuntime.CounterMembers.TryGetMember(counter, "subtract", out var member));
        var callable = (LythonRuntime.ICallable)member;
        var before = context.MemoryGovernor.CurrentCommittedBytes;
        callable.Invoke([CallArgumentValue.Positional(delta)], span, context);
        Assert.True(counter.TryGetValue(key, out var stored));
        Assert.Equal(-1m, ((PyDecimal)stored).Value);
        Assert.Equal(before + 128L, context.MemoryGovernor.CurrentCommittedBytes);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }

    private static object InvokeOperator(
        string name,
        LythonRuntime.ExecutionContext context,
        LythonSourceSpan span,
        params object[] operands)
    {
        var method = typeof(LythonRuntime).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(name + " not found.");
        var arguments = new object?[operands.Length + 2];
        Array.Copy(operands, arguments, operands.Length);
        arguments[^2] = context;
        arguments[^1] = span;
        return method.Invoke(null, arguments)
            ?? throw new InvalidOperationException(name + " returned null.");
    }

    [Fact]
    public void CounterDecimalOperatorsCommitExactly()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var key = PyString.FromString("a");
        static PyCounter CounterWith(LythonRuntime.ExecutionContext context, LythonSourceSpan span, object key, object value)
        {
            var counter = new PyCounter(context.MemoryGovernor, span);
            counter.SetItem(key, value);
            return counter;
        }

        static long Committed(LythonRuntime.ExecutionContext context) => context.MemoryGovernor.CurrentCommittedBytes;

        var beforeIntAdd = Committed(context);
        _ = InvokeOperator("EvaluateAdd", context, span,
            CounterWith(context, span, key, new BigInteger(1)), CounterWith(context, span, key, new BigInteger(2)));
        var intAddBacking = Committed(context) - beforeIntAdd;
        var beforeDecAdd = Committed(context);
        var sum = (PyCounter)InvokeOperator("EvaluateAdd", context, span,
            CounterWith(context, span, key, new PyDecimal(1m)), CounterWith(context, span, key, new PyDecimal(2m)));
        Assert.True(sum.TryGetValue(key, out var sumValue));
        Assert.Equal(3m, ((PyDecimal)sumValue).Value);
        Assert.Equal(intAddBacking + 64L, Committed(context) - beforeDecAdd);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);

        var beforeIntSub = Committed(context);
        _ = InvokeOperator("EvaluateSubtract", context, span,
            CounterWith(context, span, key, new BigInteger(2)), CounterWith(context, span, key, new BigInteger(1)));
        var intSubBacking = Committed(context) - beforeIntSub;
        var beforeDecSub = Committed(context);
        var diff = (PyCounter)InvokeOperator("EvaluateSubtract", context, span,
            CounterWith(context, span, key, new PyDecimal(2m)), CounterWith(context, span, key, new PyDecimal(1m)));
        Assert.True(diff.TryGetValue(key, out var diffValue));
        Assert.Equal(1m, ((PyDecimal)diffValue).Value);
        Assert.Equal(intSubBacking + 64L, Committed(context) - beforeDecSub);

        var beforeIntNeg = Committed(context);
        var intNeg = (PyCounter)InvokeOperator("EvaluateUnaryMinus", context, span,
            CounterWith(context, span, key, new BigInteger(2)));
        var intNegBacking = Committed(context) - beforeIntNeg;
        Assert.Equal(0, intNeg.Count);
        var beforeDecNeg = Committed(context);
        var neg = (PyCounter)InvokeOperator("EvaluateUnaryMinus", context, span,
            CounterWith(context, span, key, new PyDecimal(2m)));
        Assert.Equal(0, neg.Count);
        Assert.Equal(intNegBacking + 64L, Committed(context) - beforeDecNeg);
    }

    [Fact]
    public void CounterBitwiseSelectionsStayFree()
    {
        var host = new MockLythonHost();
        var context = new LythonRuntime.ExecutionContext(host, new LythonRunOptions());
        var span = new LythonSourceSpan(0, 0, 0, 0);
        var key = PyString.FromString("a");
        static PyCounter CounterWith(LythonRuntime.ExecutionContext context, LythonSourceSpan span, object key, object value)
        {
            var counter = new PyCounter(context.MemoryGovernor, span);
            counter.SetItem(key, value);
            return counter;
        }

        static long Committed(LythonRuntime.ExecutionContext context) => context.MemoryGovernor.CurrentCommittedBytes;

        var leftCount = new PyDecimal(1m);
        var rightCount = new PyDecimal(2m);
        var beforeIntOr = Committed(context);
        _ = InvokeOperator("EvaluateBitwiseOr", context, span,
            CounterWith(context, span, key, new BigInteger(1)), CounterWith(context, span, key, new BigInteger(2)));
        var intOrBacking = Committed(context) - beforeIntOr;
        var beforeDecOr = Committed(context);
        var union = (PyCounter)InvokeOperator("EvaluateBitwiseOr", context, span,
            CounterWith(context, span, key, leftCount), CounterWith(context, span, key, rightCount));
        Assert.True(union.TryGetValue(key, out var unionValue));
        Assert.Same(rightCount, unionValue);
        Assert.Equal(intOrBacking, Committed(context) - beforeDecOr);

        var beforeIntAnd = Committed(context);
        _ = InvokeOperator("EvaluateBitwiseAnd", context, span,
            CounterWith(context, span, key, new BigInteger(1)), CounterWith(context, span, key, new BigInteger(2)));
        var intAndBacking = Committed(context) - beforeIntAnd;
        var beforeDecAnd = Committed(context);
        var intersection = (PyCounter)InvokeOperator("EvaluateBitwiseAnd", context, span,
            CounterWith(context, span, key, leftCount), CounterWith(context, span, key, rightCount));
        Assert.True(intersection.TryGetValue(key, out var intersectionValue));
        Assert.Same(leftCount, intersectionValue);
        Assert.Equal(intAndBacking, Committed(context) - beforeDecAnd);
        Assert.Equal(0, context.MemoryGovernor.CurrentReservedBytes);
    }
}


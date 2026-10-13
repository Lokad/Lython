using System.Buffers;
using System.Globalization;
using Lokad.Lython.Frontend;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static object Any(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "any() takes exactly one argument (" + arguments.Length + " given)", span);
        }

        foreach (var item in ToSequence(arguments[0], span, context))
        {
            if (IsTruthy(item, context, span))
            {
                return true;
            }
        }

        return false;
    }

    private static async ValueTask<object> AnyAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "any() takes exactly one argument (" + arguments.Length + " given)", span);
        }

        await foreach (var item in ToSequenceAsync(arguments[0], span, context).ConfigureAwait(false))
        {
            if (await IsTruthyAsync(item, context, span).ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }

    private static object All(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "all() takes exactly one argument (" + arguments.Length + " given)", span);
        }

        foreach (var item in ToSequence(arguments[0], span, context))
        {
            if (!IsTruthy(item, context, span))
            {
                return false;
            }
        }

        return true;
    }

    private static async ValueTask<object> AllAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length != 1)
        {
            throw new LythonRuntimeException("TypeError", "all() takes exactly one argument (" + arguments.Length + " given)", span);
        }

        await foreach (var item in ToSequenceAsync(arguments[0], span, context).ConfigureAwait(false))
        {
            if (!await IsTruthyAsync(item, context, span).ConfigureAwait(false))
            {
                return false;
            }
        }

        return true;
    }

    private static object Sum(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", "sum(iterable[, start]) expects one iterable and optional start argument.", span);
        }

        var total = arguments.Length == 2 ? arguments[1] : BigInteger.Zero;
        // A string or bytes start fails up front with the join hint like
        // CPython; item failures surface through binary dispatch instead.
        if (PyStringOps.TryAsString(total, out _))
        {
            throw new LythonRuntimeException("TypeError", "sum() can't sum strings [use ''.join(seq) instead]", span);
        }

        if (total is PyBytes)
        {
            throw new LythonRuntimeException("TypeError", "sum() can't sum bytes [use b''.join(seq) instead]", span);
        }

        foreach (var item in ToSequence(arguments[0], span, context))
        {
            // Instances resolve __add__/__radd__ through operator dispatch like
            // CPython; plain values take the direct path with identical results.
            total = total is PyInstance || item is PyInstance
                ? EvaluateBinaryOperator(BinaryOperatorSyntax.Add, total, item, context, span)
                : EvaluateAdd(total, item, context, span);
        }

        return total;
    }

    private static async ValueTask<object> SumAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
    {
        if (arguments.Length is < 1 or > 2)
        {
            throw new LythonRuntimeException("TypeError", "sum(iterable[, start]) expects one iterable and optional start argument.", span);
        }

        var total = arguments.Length == 2 ? arguments[1] : BigInteger.Zero;
        // A string or bytes start fails up front with the join hint like
        // CPython; item failures surface through binary dispatch instead.
        if (PyStringOps.TryAsString(total, out _))
        {
            throw new LythonRuntimeException("TypeError", "sum() can't sum strings [use ''.join(seq) instead]", span);
        }

        if (total is PyBytes)
        {
            throw new LythonRuntimeException("TypeError", "sum() can't sum bytes [use b''.join(seq) instead]", span);
        }

        await foreach (var item in ToSequenceAsync(arguments[0], span, context).ConfigureAwait(false))
        {
            total = total is PyInstance || item is PyInstance
                ? await EvaluateBinaryOperatorAsync(BinaryOperatorSyntax.Add, total, item, context, span).ConfigureAwait(false)
                : EvaluateAdd(total, item, context, span);
        }

        return total;
    }

}

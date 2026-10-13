using System.Numerics;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class UrllibParseModule
    {
        private static ValueTask<bool> TruthAsync(object value, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
            => asynchronous ? IsTruthyAsync(value, context, span) : ValueTask.FromResult(IsTruthy(value, context, span));

        private static ValueTask<object> CallAsync(object callable, CallArgumentValue[] arguments,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
            => asynchronous ? InvokeCallableTargetAsync(callable, span, span, context, () => ValueTask.FromResult(arguments))
                : ValueTask.FromResult(InvokeCallableTarget(callable, span, span, context, () => arguments));

        private static async IAsyncEnumerable<object> QuerySequenceAsync(object value, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            if (asynchronous)
            {
                await foreach (var item in PyIteration.ToSequenceAsync(value, span, context).ConfigureAwait(false))
                {
                    context.CheckExecution(span);
                    yield return item;
                }
            }
            else
            {
                foreach (var item in PyIteration.ToSequence(value, span, context))
                {
                    context.CheckExecution(span);
                    yield return item;
                }
            }
        }

        private static async ValueTask<BigInteger> QueryLengthAsync(object value, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            BigInteger length;
            if (value is PyInstance instance)
            {
                if (!instance.Type.TryLookupInMro("__len__", 0, out var raw, out _))
                    throw RuntimeErrors.Type("object has no len()", span);
                var member = asynchronous
                    ? await PyAttributeLookup.BindForInstanceAsync(instance, raw, context, span).ConfigureAwait(false)
                    : PyAttributeLookup.BindForInstance(instance, raw, context, span);
                var result = await CallAsync(member, [], span, context, asynchronous).ConfigureAwait(false);
                if (!PyNumberOps.TryAsInteger(result, out length))
                    throw RuntimeErrors.Type("__len__() should return an integer", span);
                if (length < 0) throw new LythonRuntimeException("ValueError", "__len__() should return >= 0", span);
            }
            else length = (BigInteger)Len([value], span, context);
            if (length > long.MaxValue)
                throw new LythonRuntimeException("OverflowError", "cannot fit length into an index-sized integer", span);
            return length;
        }

        private static async ValueTask<object> UrlEncodeAsync(object[] args, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            var query = args[0];
            (bool Found, object Value) items;
            try
            {
                items = asynchronous
                    ? await TryResolveRuntimeMemberAsync(query, "items", context, span).ConfigureAwait(false)
                    : TryResolveRuntimeMember(query, "items", context, span, out var member)
                        ? (true, member) : (false, PyNone.Instance);
            }
            catch (LythonRuntimeException ex) when (ex.ExceptionType == "AttributeError")
            { items = (false, PyNone.Instance); }
            if (items.Found)
            {
                // hasattr and the subsequent method call perform separate
                // attribute lookups, including guest property effects.
                var method = asynchronous ? await PyTextStream.ResolveMemberAsync(query, "items", span, context).ConfigureAwait(false)
                    : PyTextStream.ResolveMember(query, "items", span, context);
                query = await CallAsync(method, [], span, context, asynchronous).ConfigureAwait(false);
            }
            else
            {
                var length = await QueryLengthAsync(query, span, context, asynchronous).ConfigureAwait(false);
                if (length != 0)
                {
                    var first = query is PyInstance instance && asynchronous
                        ? await GetUserItemAsync(instance, BigInteger.Zero, context, span).ConfigureAwait(false)
                        : ReadSubscriptValue(query, BigInteger.Zero, span, context);
                    if (!PyTupleLike.TryGetItems(first, out _)) throw RuntimeErrors.Type("query must be a mapping or sequence of tuples", span);
                }
            }
            var doseq = await TruthAsync(args[1], span, context, asynchronous).ConfigureAwait(false);
            var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
            long scalars = 0;
            long count = 0;
            try
            {
                await foreach (var pair in QuerySequenceAsync(query, span, context, asynchronous).ConfigureAwait(false))
                {
                    await using var iterator = QuerySequenceAsync(pair, span, context, asynchronous).GetAsyncEnumerator();
                    if (!await iterator.MoveNextAsync().ConfigureAwait(false)) throw BadPair(span);
                    var key = iterator.Current;
                    if (!await iterator.MoveNextAsync().ConfigureAwait(false)) throw BadPair(span);
                    var value = iterator.Current;
                    if (await iterator.MoveNextAsync().ConfigureAwait(false)) throw BadPair(span);
                    var quotedKey = await QuoteQueryPartAsync(key, args, span, context, asynchronous).ConfigureAwait(false);
                    if (!doseq || value is PyBytes or PyString)
                        await AddValueAsync(value).ConfigureAwait(false);
                    else
                    {
                        var sized = true;
                        try { _ = await QueryLengthAsync(value, span, context, asynchronous).ConfigureAwait(false); }
                        catch (LythonRuntimeException ex) when (ex.ExceptionType == "TypeError") { sized = false; }
                        if (!sized) await AddValueAsync(value).ConfigureAwait(false);
                        else
                            await foreach (var item in QuerySequenceAsync(value, span, context, asynchronous).ConfigureAwait(false))
                                await AddValueAsync(item).ConfigureAwait(false);
                    }

                    async ValueTask AddValueAsync(object item)
                    {
                        var quotedValue = await QuoteQueryPartAsync(item, args, span, context, asynchronous).ConfigureAwait(false);
                        if (quotedKey is not PyString k || quotedValue is not PyString v)
                            throw RuntimeErrors.Type("quote_via must return strings", span);
                        var addition = (long)k.Length + v.Length + 1 + (count == 0 ? 0 : 1);
                        CheckTextLength(scalars + addition, context, span);
                        if (count != 0) builder.Append((byte)'&');
                        builder.Append(k);
                        builder.Append((byte)'=');
                        builder.Append(v);
                        scalars += addition;
                        count++;
                        GC.KeepAlive(k);
                        GC.KeepAlive(v);
                    }
                }
                var result = builder.ToPyStringAndRelease();
                context.State.CallTemporaries.TrackFreshString(result, span);
                return result;
            }
            finally { builder.Release(); GC.KeepAlive(query); GC.KeepAlive(args); }
        }

        private static LythonRuntimeException BadPair(LythonSourceSpan span)
            => new("ValueError", "query sequence elements must contain exactly two values", span);

        private static async ValueTask<object> QuoteQueryPartAsync(object value, object[] args, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            CallArgumentValue[] parameters;
            if (value is PyBytes)
                parameters = [CallArgumentValue.Positional(value), CallArgumentValue.Positional(args[2])];
            else
            {
                var text = await FormatSuspendedFieldAsync(value, 's', null, context, span, asynchronous).ConfigureAwait(false);
                context.State.CallTemporaries.TrackCallResult(text, span);
                context.ObserveString(text, span);
                parameters = [CallArgumentValue.Positional(text), CallArgumentValue.Positional(args[2]),
                    CallArgumentValue.Positional(args[3]), CallArgumentValue.Positional(args[4])];
            }
            return await CallAsync(args[5], parameters, span, context, asynchronous).ConfigureAwait(false);
        }

        private static PyList QueryList(ExecutionContext context, LythonSourceSpan span)
        {
            var result = new PyList(Array.Empty<object>(), context.MemoryGovernor, span);
            context.State.CallTemporaries.TrackFreshMutable(result, result.CommittedStorageBytes, span);
            return result;
        }

        private static async ValueTask<object> ParseQueryAsync(object[] args, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous, bool dictionary)
        {
            var separator = args[6];
            if (!await TruthAsync(separator, span, context, asynchronous).ConfigureAwait(false) || separator is not (PyString or PyBytes))
                throw new LythonRuntimeException("ValueError", "Separator must be of type string or bytes", span);
            var query = args[0];
            var textQuery = query is PyString;
            ReadOnlyMemory<byte> source;
            ReadOnlyMemory<byte> delimiter;
            if (textQuery)
            {
                source = ((PyString)query).Utf8Bytes;
                delimiter = separator is PyString textSeparator ? textSeparator.Utf8Bytes : ((PyBytes)separator).Memory;
                if (separator is PyBytes) CheckAscii(delimiter.Span, false, context, span);
            }
            else
            {
                if (!await TruthAsync(query, span, context, asynchronous).ConfigureAwait(false)) return Finish(QueryList(context, span));
                if (query is not PyBytes bytes) throw RuntimeErrors.Type("query must be a string or bytes", span);
                source = bytes.Memory;
                delimiter = separator is PyBytes byteSeparator ? byteSeparator.Memory : ((PyString)separator).Utf8Bytes;
                if (separator is PyString) CheckAscii(delimiter.Span, true, context, span);
            }
            if (source.Length == 0) return Finish(QueryList(context, span));
            if (args[5] is not PyNone)
            {
                long fields = 1;
                for (var start = 0; ;)
                {
                    var found = FindSeparator(source.Span, start, delimiter.Span, context, span);
                    if (found < 0) break;
                    fields++;
                    start = found + delimiter.Length;
                }
                var exceeded = asynchronous
                    ? await EvaluateBinaryOperatorAsync(BinaryOperatorSyntax.Less, args[5], new BigInteger(fields), context, span).ConfigureAwait(false)
                    : EvaluateBinaryOperator(BinaryOperatorSyntax.Less, args[5], new BigInteger(fields), context, span);
                if (await TruthAsync(exceeded, span, context, asynchronous).ConfigureAwait(false))
                    throw new LythonRuntimeException("ValueError", "Max number of fields exceeded", span);
            }
            var pairs = QueryList(context, span);
            try
            {
                for (var start = 0; ;)
                {
                    context.CheckExecution(span);
                    var found = FindSeparator(source.Span, start, delimiter.Span, context, span);
                    var field = source.Slice(start, (found < 0 ? source.Length : found) - start);
                    if (field.Length != 0 || await TruthAsync(args[2], span, context, asynchronous).ConfigureAwait(false))
                    {
                        var equals = field.Span.IndexOf((byte)'=');
                        if (equals < 0 && await TruthAsync(args[2], span, context, asynchronous).ConfigureAwait(false))
                            throw new LythonRuntimeException("ValueError", "bad query field", span);
                        var rawValue = equals < 0 ? ReadOnlyMemory<byte>.Empty : field[(equals + 1)..];
                        if (rawValue.Length != 0 || await TruthAsync(args[1], span, context, asynchronous).ConfigureAwait(false))
                        {
                            var name = await DecodeFieldAsync(equals < 0 ? field : field[..equals]).ConfigureAwait(false);
                            var value = await DecodeFieldAsync(rawValue).ConfigureAwait(false);
                            context.ObserveCollectionCount(pairs.Count + 1, span);
                            context.ObserveCollectionCount(2, span);
                            var pair = PyTuple.FromOwnedArray([name, value], context.MemoryGovernor, span);
                            context.State.CallTemporaries.TrackFreshMutable(pair, pair.CommittedStorageBytes, span);
                            pairs.Add(pair);
                        }
                    }
                    if (found < 0) break;
                    start = found + delimiter.Length;
                }
                return Finish(pairs);
            }
            finally { GC.KeepAlive(query); GC.KeepAlive(separator); }

            async ValueTask<object> DecodeFieldAsync(ReadOnlyMemory<byte> field)
            {
                if (!textQuery) return PercentDecodeBytes(field.Span, context, span, true);
                var text = PyString.FromUtf8(field, context.MemoryGovernor, span);
                context.State.CallTemporaries.TrackFreshString(text, span);
                return await UnquoteAsync([text, args[3], args[4]], span, context, asynchronous, true).ConfigureAwait(false);
            }

            object Finish(PyList list)
            {
                if (!dictionary) return list;
                var result = new PyDict(context.MemoryGovernor, span);
                context.State.CallTemporaries.TrackFreshMutable(result, result.CommittedStorageBytes, span);
                foreach (PyTuple pair in list.Iterate())
                {
                    context.CheckExecution(span);
                    if (result.TryGetValue(pair[0], out var existing))
                    {
                        var values = (PyList)existing;
                        context.ObserveCollectionCount(values.Count + 1, span);
                        values.Add(pair[1]);
                    }
                    else
                    {
                        context.ObserveCollectionCount(result.Count + 1, span);
                        var values = QueryList(context, span);
                        values.Add(pair[1]);
                        result.SetItem(pair[0], values);
                    }
                }
                GC.KeepAlive(list);
                return result;
            }
        }

        private static void CheckAscii(ReadOnlySpan<byte> source, bool encoding, ExecutionContext context, LythonSourceSpan span)
        {
            for (var i = 0; i < source.Length; i++)
            {
                if ((i & 1023) == 0) context.CheckExecution(span);
                if (source[i] >= 128) throw new LythonRuntimeException(encoding ? "UnicodeEncodeError" : "UnicodeDecodeError",
                    "query separator must be ASCII when mixing strings and bytes", span);
            }
        }

        private static int FindSeparator(ReadOnlySpan<byte> source, int start, ReadOnlySpan<byte> separator,
            ExecutionContext context, LythonSourceSpan span)
        {
            for (var i = start; i <= source.Length - separator.Length; i++)
            {
                if (((i - start) & 1023) == 0) context.CheckExecution(span);
                if (source[i] != separator[0]) continue;
                var j = 1;
                for (; j < separator.Length; j++)
                {
                    if ((j & 1023) == 0) context.CheckExecution(span);
                    if (source[i + j] != separator[j]) break;
                }
                if (j == separator.Length) return i;
            }
            return -1;
        }
    }
}

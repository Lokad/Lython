using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static class CsvDictWriterMembers
    {
        private static readonly LythonCallableSignature HeaderSignature = LythonCallableSignature.Create("csv.DictWriter.writeheader", []);
        private static readonly LythonCallableSignature RowSignature = LythonCallableSignature.Create("csv.DictWriter.writerow", ["rowdict"]);
        private static readonly LythonCallableSignature RowsSignature = LythonCallableSignature.Create("csv.DictWriter.writerows", ["rowdicts"]);
        public static bool TryGetMember(CsvDictWriterObject writer, string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "writeheader" => BoundCallable.Create((args, span, context) => HeaderAsync(writer, span, context, false).GetAwaiter().GetResult(),
                    HeaderSignature, (args, span, context) => HeaderAsync(writer, span, context, true)),
                "writerow" => BoundCallable.Create((args, span, context) => RowAsync(writer, args[0], span, context, false).GetAwaiter().GetResult(),
                    RowSignature, (args, span, context) => RowAsync(writer, args[0], span, context, true)),
                "writerows" => BoundCallable.Create((args, span, context) => RowsAsync(writer, args[0], span, context, false).GetAwaiter().GetResult(),
                    RowsSignature, (args, span, context) => RowsAsync(writer, args[0], span, context, true)),
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
        private static ValueTask<object> RowAsync(CsvDictWriterObject writer, object row, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
            => CsvWriterMembers.WriteRowCoreAsync(writer.Writer, PyNone.Instance, span, context, asynchronous,
                ValuesAsync(writer, row, span, context, asynchronous));

        private static async ValueTask<object> RowsAsync(CsvDictWriterObject writer, object rows, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            if (asynchronous)
                await foreach (var row in ToSequenceAsync(rows, span, context).ConfigureAwait(false))
                    await RowAsync(writer, row, span, context, true).ConfigureAwait(false);
            else foreach (var row in ToSequence(rows, span, context)) await RowAsync(writer, row, span, context, false).ConfigureAwait(false);
            return PyNone.Instance;
        }
        private static async ValueTask<object> HeaderAsync(CsvDictWriterObject writer, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var header = new PyDict(context.MemoryGovernor, span);
            await using var keys = asynchronous ? await PyIteration.Cursor.CreateAsync(writer.FieldNames, span, context).ConfigureAwait(false)
                : PyIteration.Cursor.Create(writer.FieldNames, span, context);
            await using var values = asynchronous ? await PyIteration.Cursor.CreateAsync(writer.FieldNames, span, context).ConfigureAwait(false)
                : PyIteration.Cursor.Create(writer.FieldNames, span, context);
            while (true)
            {
                var key = asynchronous ? await keys.TryMoveNextAsync().ConfigureAwait(false)
                    : keys.TryMoveNext(out var k) ? PyIterationResult.Yield(k) : PyIterationResult.End;
                if (!key.HasValue) break;
                var value = asynchronous ? await values.TryMoveNextAsync().ConfigureAwait(false)
                    : values.TryMoveNext(out var v) ? PyIterationResult.Yield(v) : PyIterationResult.End;
                if (!value.HasValue) break;
                using var ambient = PyStructuralGuard.PushAmbient(context, span);
                header.SetItem(ValidateDictionaryKey(key.Value, span), value.Value);
            }
            return await RowAsync(writer, header, span, context, asynchronous).ConfigureAwait(false);
        }
        private static async IAsyncEnumerable<object> ValuesAsync(CsvDictWriterObject writer, object row, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var action = asynchronous
                ? await EvaluateBinaryOperatorAsync(BinaryOperatorSyntax.Equal, writer.ExtrasAction, PyString.FromString("raise"), context, span).ConfigureAwait(false)
                : EvaluateBinaryOperator(BinaryOperatorSyntax.Equal, writer.ExtrasAction, PyString.FromString("raise"), context, span);
            if (asynchronous ? await IsTruthyAsync(action, context, span).ConfigureAwait(false) : IsTruthy(action, context, span))
            {
                var member = asynchronous ? await PyTextStream.ResolveMemberAsync(row, "keys", span, context).ConfigureAwait(false)
                    : PyTextStream.ResolveMember(row, "keys", span, context);
                var keys = asynchronous ? await InvokeCallableTargetAsync(member, span, span, context, Array.Empty<CallArgumentValue>()).ConfigureAwait(false)
                    : InvokeCallableTarget(member, span, span, context, Array.Empty<CallArgumentValue>());
                var wrong = asynchronous
                    ? await EvaluateBinaryOperatorAsync(BinaryOperatorSyntax.Subtract, keys, writer.FieldNames, context, span).ConfigureAwait(false)
                    : EvaluateBinaryOperator(BinaryOperatorSyntax.Subtract, keys, writer.FieldNames, context, span);
                if (asynchronous ? await IsTruthyAsync(wrong, context, span).ConfigureAwait(false) : IsTruthy(wrong, context, span))
                    throw new LythonRuntimeException("ValueError", "dict contains fields not in fieldnames", span);
            }
            if (writer.FieldNames is PyList list)
            {
                for (var i = 0; i < list.Count; i++) yield return await GetAsync(list[i]).ConfigureAwait(false);
            }
            else if (asynchronous)
                await foreach (var name in ToSequenceAsync(writer.FieldNames, span, context).ConfigureAwait(false))
                    yield return await GetAsync(name).ConfigureAwait(false);
            else foreach (var name in ToSequence(writer.FieldNames, span, context)) yield return await GetAsync(name).ConfigureAwait(false);

            async ValueTask<object> GetAsync(object name)
            {
                context.CheckExecution(span);
                var get = asynchronous ? await PyTextStream.ResolveMemberAsync(row, "get", span, context).ConfigureAwait(false)
                    : PyTextStream.ResolveMember(row, "get", span, context);
                CallArgumentValue[] args = [CallArgumentValue.Positional(name), CallArgumentValue.Positional(writer.RestValue)];
                return asynchronous ? await InvokeCallableTargetAsync(get, span, span, context, args).ConfigureAwait(false)
                    : InvokeCallableTarget(get, span, span, context, args);
            }
        }
    }
}

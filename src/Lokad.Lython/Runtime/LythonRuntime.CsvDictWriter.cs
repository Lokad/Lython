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
                "writerows" => BoundCallable.Create((args, span, context) => CsvWriterMembers.WriteRowsAsync(writer.Writer, args[0], span, context, false, writer).GetAwaiter().GetResult(),
                    RowsSignature, (args, span, context) => CsvWriterMembers.WriteRowsAsync(writer.Writer, args[0], span, context, true, writer)),
                _ => MissingMemberValue.Instance,
            };
            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }
        private static ValueTask<object> RowAsync(CsvDictWriterObject writer, object row, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
            => CsvWriterMembers.WriteRowCoreAsync(writer.Writer, row, span, context, asynchronous, writer);

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
                    : (keys.TryMoveNext() is { HasValue: true, Value: var k }) ? PyIterationResult.Yield(k) : PyIterationResult.End;
                if (!key.HasValue) break;
                var value = asynchronous ? await values.TryMoveNextAsync().ConfigureAwait(false)
                    : (values.TryMoveNext() is { HasValue: true, Value: var v }) ? PyIterationResult.Yield(v) : PyIterationResult.End;
                if (!value.HasValue) break;
                using var ambient = PyStructuralGuard.PushAmbient(context, span);
                header.SetItem(ValidateDictionaryKey(key.Value, span), value.Value);
            }
            return await RowAsync(writer, header, span, context, asynchronous).ConfigureAwait(false);
        }
        internal static async ValueTask ValidateRowAsync(CsvDictWriterObject writer, object row,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var action = asynchronous
                ? await EvaluateBinaryOperatorAsync(BinaryOperatorSyntax.Equal, writer.ExtrasAction, PyString.FromString("raise"), context, span).ConfigureAwait(false)
                : EvaluateBinaryOperator(BinaryOperatorSyntax.Equal, writer.ExtrasAction, PyString.FromString("raise"), context, span);
            if (asynchronous ? await IsTruthyAsync(action, context, span).ConfigureAwait(false) : IsTruthy(action, context, span))
            {
                if (row is not PyDict dictionary || writer.FieldNames is not IPySequenceValue fields ||
                    fields is not (PyList or PyTuple) || !dictionary.TryHasExtraStringKeys(fields, context, span, out var hasExtra))
                {
                    var member = asynchronous ? await PyTextStream.ResolveMemberAsync(row, "keys", span, context).ConfigureAwait(false)
                        : PyTextStream.ResolveMember(row, "keys", span, context);
                    var keys = asynchronous ? await InvokeCallableTargetAsync(member, span, span, context, Array.Empty<CallArgumentValue>()).ConfigureAwait(false)
                        : InvokeCallableTarget(member, span, span, context, Array.Empty<CallArgumentValue>());
                    var wrong = asynchronous
                        ? await EvaluateBinaryOperatorAsync(BinaryOperatorSyntax.Subtract, keys, writer.FieldNames, context, span).ConfigureAwait(false)
                        : EvaluateBinaryOperator(BinaryOperatorSyntax.Subtract, keys, writer.FieldNames, context, span);
                    hasExtra = asynchronous ? await IsTruthyAsync(wrong, context, span).ConfigureAwait(false) : IsTruthy(wrong, context, span);
                }
                if (hasExtra)
                    throw new LythonRuntimeException("ValueError", "dict contains fields not in fieldnames", span);
            }
        }

        internal static async ValueTask<object> GetFieldAsync(CsvDictWriterObject writer, object row, object name,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
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

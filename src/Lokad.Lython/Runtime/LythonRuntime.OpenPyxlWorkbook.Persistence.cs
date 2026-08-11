namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed partial class OpenPyxlWorkbook
    {
        private object Save(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "Workbook.save(filename) expects one filename argument.", span);
            }

            EnsureCanSave(span);
            var path = NormalizeWorkbookPath(arguments[0], context, span);
            var payload = OpenPyxlPackage.Save(this, context, span);
            context.RegisterHostCall(span);
            context.WriteHostBytes(path, payload, span);
            _saved = true;
            return PyNone.Instance;
        }

        private async ValueTask<object> SaveAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            if (arguments.Length != 1)
            {
                throw new LythonRuntimeException("TypeError", "Workbook.save(filename) expects one filename argument.", span);
            }

            EnsureCanSave(span);
            var path = NormalizeWorkbookPath(arguments[0], context, span);
            var payload = OpenPyxlPackage.Save(this, context, span);
            context.RegisterHostCall(span);
            await context.WriteHostBytesAsync(path, payload, span).ConfigureAwait(false);
            _saved = true;
            return PyNone.Instance;
        }

        private void EnsureCanSave(LythonSourceSpan span)
        {
            EnsureCanMutate(span);
            if (SaveGuard.TryGetUnsafeReason(out var unsafeReason))
            {
                throw new LythonRuntimeException(
                    "NotImplementedError",
                    "Workbook.save() would discard unsupported openpyxl workbook content: " + unsafeReason,
                    span);
            }

            var stalePreservedFeatureReason = StructurallyStalePreservedFeatureReason();
            if (stalePreservedFeatureReason is not null)
            {
                throw new LythonRuntimeException(
                    "NotImplementedError",
                    "Workbook.save() would leave stale preserved openpyxl worksheet metadata after structural edits: " + stalePreservedFeatureReason,
                    span);
            }

            if (WriteOnly && _saved)
            {
                throw new LythonRuntimeException("WorkbookAlreadySaved", "Workbook has already been saved and cannot be saved again.", span);
            }
        }

        private string? StructurallyStalePreservedFeatureReason()
        {
            foreach (var worksheet in _worksheets)
            {
                var reason = OpenPyxlPackage.StructuralMutationPreservedFeatureReason(worksheet);
                if (reason is not null)
                {
                    return reason;
                }
            }

            return null;
        }

        internal void EnsureCanMutate(LythonSourceSpan? span)
        {
            if (ReadOnly)
            {
                throw new LythonRuntimeException("TypeError", "Workbook is read-only.", span);
            }
        }

        private static object Close(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = context;
            if (arguments.Length != 0)
            {
                throw new LythonRuntimeException("TypeError", "Workbook.close() expects no arguments.", span);
            }

            return PyNone.Instance;
        }
    }
}

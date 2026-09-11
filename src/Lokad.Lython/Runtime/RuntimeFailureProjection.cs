namespace Lokad.Lython.Runtime;

internal static class RuntimeFailureProjection
{
    // Marks a failure message cut to fit the projection budget, so hosts can
    // tell truncation apart from a short original. Kept ASCII so its UTF-16
    // length is exact.
    internal const string TruncatedMessageMarker = "...[truncated to fit the projection budget]";

    public static LythonRuntimeFailure ToPublicFailure(LythonRuntimeException exception, ProjectionBudget? budget, LythonRuntime.ExecutionContext? context = null)
    {
        LythonStackFrame[] frames;
        if (exception.Frames.Count == 0)
        {
            frames = [new LythonStackFrame("<module>", exception.Span, exception.SourcePath)];
        }
        else
        {
            frames = new LythonStackFrame[exception.Frames.Count];
            for (var index = 0; index < frames.Length; index++)
            {
                frames[index] = exception.Frames[^(index + 1)];
            }
        }

        // Frame records alias engine state; only the new array rides the budget.
        // Reserves below check fit first, so a failed failure-projection never
        // pushes the reported peak past the budget; the caller's minimal
        // fallback then keeps the original type with empty details.
        ReserveOrThrow(budget, checked(32L + (16L * frames.Length)));
        var message = FitMessage(RenderFailureMessage(exception, context), budget);
        return new LythonRuntimeFailure(
            exception.ExceptionType,
            message,
            exception.Span,
            frames,
            exception.SourcePath);
    }

    // Mapping misses carry their key as the payload; like CPython and the
    // caught str() path, the projected message renders the key through repr.
    // Anything payload-free (or unrenderable, which must never mask the
    // original failure) keeps the stored message.
    private static string RenderFailureMessage(LythonRuntimeException exception, LythonRuntime.ExecutionContext? context)
    {
        if (context is null ||
            !string.Equals(exception.ExceptionType, "KeyError", StringComparison.Ordinal) ||
            exception.Payload is null ||
            ReferenceEquals(exception.Payload, PyNone.Instance))
        {
            return exception.Message;
        }

        try
        {
            return PyRendering.ToReprPyString(exception.Payload, new PyRenderingContext(context)).AsString();
        }
        catch (Exception)
        {
            return exception.Message;
        }
    }

    private static string FitMessage(string message, ProjectionBudget? budget)
    {
        var fullBytes = checked(32L + (2L * message.Length));
        if (budget?.MaxBytes is not { } maxBytes)
        {
            budget?.Reserve(fullBytes);
            return message;
        }

        // CurrentBytes never exceeds MaxBytes by more than prior capture
        // overruns, so this stays a plain comparison without checked arithmetic.
        if (fullBytes <= maxBytes - budget.CurrentBytes)
        {
            budget.Reserve(fullBytes);
            return message;
        }

        // Truncate with an explicit marker; never split a surrogate pair.
        var room = maxBytes - budget.CurrentBytes - 32L - (2L * TruncatedMessageMarker.Length);
        var keepUnits = room <= 0 ? 0 : (int)Math.Min(room / 2, message.Length);
        while (keepUnits > 0
            && char.IsHighSurrogate(message[keepUnits - 1])
            && keepUnits < message.Length
            && char.IsLowSurrogate(message[keepUnits]))
        {
            keepUnits--;
        }

        var kept = message.Substring(0, keepUnits) + TruncatedMessageMarker;
        ReserveOrThrow(budget, checked(32L + (2L * kept.Length)));
        return kept;
    }

    private static void ReserveOrThrow(ProjectionBudget? budget, long bytes)
    {
        if (budget?.MaxBytes is not { } maxBytes)
        {
            budget?.Reserve(bytes);
            return;
        }

        if (bytes > maxBytes - budget.CurrentBytes)
        {
            throw new ProjectionException($"projection memory budget exceeded ({maxBytes})");
        }

        budget.Reserve(bytes);
    }
}

using System.Runtime.CompilerServices;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed partial class JsonModule
    {
        private struct JsonDecodeFrame(object result, PyDict.RecordKeys? expected, int position, bool isObject)
        {
            internal object Result = result;
            internal PyDict.RecordKeys? Expected = expected;
            internal PyString? Key;
            internal int Position = position;
            internal bool IsObject = isObject;
        }

        // Shallow documents need no frame allocation. Deeper construction owns
        // admitted backing, transferred together with actual suspended callbacks.
        [InlineArray(4)]
        private struct JsonInlineFrames { private JsonDecodeFrame _first; }

        private struct JsonDecodeState(PyString input, JsonLoadOptions options,
            ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            internal readonly PyString Input = input;
            internal readonly JsonLoadOptions Options = options;
            internal readonly ExecutionContext Context = context;
            internal readonly LythonSourceSpan Span = span;
            internal readonly bool Asynchronous = asynchronous;
            internal JsonInlineFrames InlineFrames;
            internal JsonDecodeFrame[]? HeapFrames;
            internal MemoryGovernor.TemporaryMemoryReservation? Working;
            internal int Depth;
            internal bool AwaitingContainer;
        }

        private static Span<JsonDecodeFrame> JsonFrames(ref JsonDecodeState state)
            => state.HeapFrames is { } frames ? frames : state.InlineFrames;

        private static void ReleaseJsonFrames(ref JsonDecodeState state)
        {
            JsonFrames(ref state).Clear();
            state.HeapFrames = null;
            state.Depth = 0;
            state.Working?.Dispose();
            state.Working = null;
        }

        private static void PushJsonContainer(ref JsonDecodeState state, int opening, bool isObject)
        {
            var context = state.Context;
            context.CheckExecution(state.Span);
            if (state.Depth >= 64)
                throw CreateJsonDepthError(state.Input, opening, !isObject, state.Span, context);
            if (state.Depth == JsonFrames(ref state).Length)
            {
                var old = state.HeapFrames;
                var next = JsonFrames(ref state).Length * 2;
                var bytes = checked(32L + 48L * next);
                if (state.Working is null)
                    state.Working = context.MemoryGovernor.ReserveTemporary(bytes, state.Span);
                else state.Working.Grow(bytes, state.Span);
                context.CheckExecution(state.Span);
                var replacement = new JsonDecodeFrame[next];
                JsonFrames(ref state)[..state.Depth].CopyTo(replacement);
                JsonFrames(ref state).Clear();
                state.HeapFrames = replacement;
                if (old is not null) state.Working!.Shrink(checked(32L + 48L * old.Length));
            }

            var expected = isObject ? context.State.JsonRecordLayout : null;
            var bytesView = state.Input.Utf8Span;
            var position = SkipJsonWhitespace(bytesView, opening + 1, context, state.Span);
            var capacity = position < bytesView.Length && bytesView[position] == (byte)'"' ? 4 : 0;
            object result = isObject && state.Options.ObjectPairsHook is null
                ? expected is not null && capacity != 0 ? expected.CreateRecord(context, state.Span, capacity)
                    : new PyDict(capacity, context.MemoryGovernor, state.Span)
                : new PyList([], context.MemoryGovernor, state.Span);
            JsonFrames(ref state)[state.Depth++] = new(result, expected, position, isObject);
        }

        private static void AcceptJsonValue(ref JsonDecodeState state, object value, int end)
        {
            ref var parent = ref JsonFrames(ref state)[state.Depth - 1];
            AppendJsonContainerValue(parent.Result, parent.Key, value, state.Context, state.Span);
            parent.Key = null;
            parent.Position = NextJsonContainerToken(state.Input, end, parent.IsObject, state.Context, state.Span);
        }

        private static bool FinishJsonContainer(ref JsonDecodeState state, object value, int end,
            out (object Value, int End) result)
        {
            JsonFrames(ref state)[--state.Depth] = default;
            if (state.Depth == 0) { result = (value, end); return true; }
            AcceptJsonValue(ref state, value, end);
            result = default;
            return false;
        }

        private ValueTask<(object Value, int End)> ReadJsonContainerAsync(
            PyString input, JsonLoadOptions options, ExecutionContext context, LythonSourceSpan span,
            int opening, bool isObject, bool asynchronous)
        {
            var state = new JsonDecodeState(input, options, context, span, asynchronous);
            var transferred = false;
            try
            {
                PushJsonContainer(ref state, opening, isObject);
                if (ContinueJsonConstruction(ref state, out var result, out var pending)) return new(result);
                transferred = true;
                return CompleteJsonConstructionAsync(state, pending);
            }
            finally { if (!transferred) ReleaseJsonFrames(ref state); }
        }

        // One construction loop owns both parent and child containers. Scalar
        // checks/decoders are unchanged; only real callbacks transfer this work.
        private bool ContinueJsonConstruction(ref JsonDecodeState state,
            out (object Value, int End) result, out ValueTask<(object Value, int End)> pending)
        {
            var input = state.Input;
            var options = state.Options;
            var context = state.Context;
            var span = state.Span;
            var bytes = input.Utf8Span;
            while (true)
            {
                context.CheckExecution(span);
                ref var frame = ref JsonFrames(ref state)[state.Depth - 1];
                var position = frame.Position;
                if (position < bytes.Length && bytes[position] == (frame.IsObject ? (byte)'}' : (byte)']'))
                {
                    if (frame.IsObject)
                        context.State.JsonRecordLayout = frame.Result is PyDict dictionary
                            ? dictionary.ShareRecordKeys(context, span) : frame.Expected;
                    var value = frame.Result;
                    var end = position + 1;
                    var hook = frame.IsObject ? options.ObjectPairsHook ?? options.ObjectHook : null;
                    if (hook is not null)
                    {
                        var converted = InvokeJsonCallbackCoreAsync(hook, value, context, span, state.Asynchronous);
                        if (!converted.IsCompleted)
                        {
                            state.AwaitingContainer = true;
                            result = default;
                            pending = CompleteJsonScalarAsync(converted, end);
                            return false;
                        }
                        value = converted.GetAwaiter().GetResult();
                    }
                    if (FinishJsonContainer(ref state, value, end, out result)) { pending = default; return true; }
                    continue;
                }

                if (position >= bytes.Length || frame.IsObject && bytes[position] != (byte)'"')
                    throw CreateJsonExpectingError(input, Math.Min(position, bytes.Length),
                        frame.IsObject ? JsonIncompleteExpectation.PropertyName : JsonIncompleteExpectation.Value, span, context);
                if (frame.IsObject)
                {
                    var property = ReadJsonObjectKey(input, options, position, context, span, frame.Expected,
                        frame.Result is PyDict dictionary ? dictionary.Count : ((PyList)frame.Result).Count);
                    frame.Key = property.Value;
                    position = EnsureJsonColon(bytes, property.End, input, span, context) + 1;
                }
                position = SkipJsonWhitespace(bytes, position, context, span);
                if (position < bytes.Length && bytes[position] is (byte)'{' or (byte)'[')
                {
                    PushJsonContainer(ref state, position, bytes[position] == (byte)'{');
                    continue;
                }
                var child = ReadJsonValueExactAsync(input, options, context, span, position, state.Asynchronous);
                if (!child.IsCompleted)
                {
                    state.AwaitingContainer = false;
                    result = default;
                    pending = child;
                    return false;
                }
                var decoded = child.GetAwaiter().GetResult();
                AcceptJsonValue(ref state, decoded.Value, decoded.End);
            }
        }

        private async ValueTask<(object Value, int End)> CompleteJsonConstructionAsync(
            JsonDecodeState state, ValueTask<(object Value, int End)> pending)
        {
            try
            {
                while (true)
                {
                    var decoded = await pending.ConfigureAwait(false);
                    // Resume on the current managed stack before descending into
                    // construction; the operation's existing admission stays live.
                    state.Context.CheckExecution(state.Span);
                    ExecutionState.EnsureInterpreterStack(state.Span);
                    if (state.AwaitingContainer)
                    {
                        if (FinishJsonContainer(ref state, decoded.Value, decoded.End, out var completed)) return completed;
                    }
                    else AcceptJsonValue(ref state, decoded.Value, decoded.End);
                    if (ContinueJsonConstruction(ref state, out var result, out pending)) return result;
                }
            }
            finally { ReleaseJsonFrames(ref state); }
        }
    }
}

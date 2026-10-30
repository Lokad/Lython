using System.Numerics;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private enum RegexSubstitutionMode { TextOnly, TextAndCount }

    private static class RegexMatcher
    {
        internal static ILythonRegexSubject Prepare(RePatternObject pattern, RegexSubjectRange range,
            ExecutionContext context, LythonSourceSpan span)
            => RegexCompiler.ProviderCall(() => pattern.Regex.Prepare(range.Original.Utf8Bytes, new(range.Pos, range.EndPos),
                context.MemoryGovernor.Budget, context.Limits.CancellationToken), context.MemoryGovernor,
                context.Limits.CancellationToken, span);

        internal static object Match(RePatternObject pattern, RegexSubjectRange range, LythonRegexMatchMode mode,
            LythonSourceSpan span, ExecutionContext context)
        {
            using var subject = Prepare(pattern, range, context, span);
            var match = RegexCompiler.ProviderCall(() => subject.Match(mode, context.Limits.CancellationToken),
                context.MemoryGovernor, context.Limits.CancellationToken, span);
            return match is null ? PyNone.Instance : CreateMatchObject(pattern, range, match, context, span);
        }

        internal static PyRegexFindIterator CreateFindIterMatches(RePatternObject pattern, RegexSubjectRange range,
            ExecutionContext context, LythonSourceSpan span) => new(pattern, range, context, span);

        internal static PyList CreateFindAllResult(RePatternObject pattern, RegexSubjectRange range,
            LythonSourceSpan span, ExecutionContext context)
        {
            var result = new PyList([], context.MemoryGovernor, span);
            if (!range.IsValid) return result;
            using var subject = Prepare(pattern, range, context, span);
            using var cursor = RegexCompiler.ProviderCall(() => subject.Find(context.Limits.CancellationToken),
                context.MemoryGovernor, context.Limits.CancellationToken, span);
            while (RegexCompiler.ProviderCall(() => cursor.Next(context.Limits.CancellationToken),
                context.MemoryGovernor, context.Limits.CancellationToken, span) is { } match)
            {
                context.CheckExecution(span);
                if (pattern.CaptureSlotCount <= 2)
                {
                    var slot = pattern.CaptureSlotCount == 1 ? 0 : 1;
                    result.Add(match.Captures[slot] is { } capture ? CreateUtf8String(capture.Value, context, span) : PyString.Empty);
                }
                else result.Add(CreateTuple(pattern.CaptureSlotCount - 1,
                    i => match.Captures[i + 1] is { } capture ? CreateUtf8String(capture.Value, context, span) : PyString.Empty,
                    context, span));
            }
            return result;
        }

        internal static ReMatchObject CreateMatchObject(RePatternObject pattern, RegexSubjectRange range,
            LythonRegexMatch match, ExecutionContext context, LythonSourceSpan span)
        {
            context.CheckExecution(span);
            if (match.Captures.Count != pattern.CaptureSlotCount || match.Captures[0] is not { } whole)
                throw RuntimeErrors.Runtime("Regex provider returned inconsistent capture metadata.", span);
            var bytes = checked(128L + 96L * match.Captures.Count);
            using var storage = context.MemoryGovernor.ReserveTemporary(bytes, span);
            var wholeValue = CreateUtf8String(whole.Value, context, span);
            var captures = new ReCapture?[match.Captures.Count - 1];
            for (var i = 1; i < match.Captures.Count; i++)
            {
                context.CheckExecution(span);
                if (match.Captures[i] is { } capture)
                    captures[i - 1] = new ReCapture(CreateUtf8String(capture.Value, context, span),
                        new BigInteger(capture.Start), new BigInteger(capture.End));
            }
            var result = new ReMatchObject(wholeValue, new BigInteger(whole.Start),
                new BigInteger(whole.End), pattern, range.Original,
                new BigInteger(range.Pos), new BigInteger(range.EndPos), pattern.CaptureSlotCount,
                captures, pattern.NamedGroups, match.LastIndex);
            storage.Commit(bytes);
            return result;
        }

        internal static PyList Split(RePatternObject pattern, RegexSubjectRange range, int maxSplit,
            LythonSourceSpan span, ExecutionContext context)
        {
            using var subject = Prepare(pattern, range, context, span);
            var parts = RegexCompiler.ProviderCall(() => subject.Split(maxSplit, context.Limits.CancellationToken),
                context.MemoryGovernor, context.Limits.CancellationToken, span);
            var result = new PyList([], context.MemoryGovernor, span);
            for (var i = 0; i < parts.Count; i++)
            {
                context.CheckExecution(span);
                result.Add(parts[i] is { } part ? CreateUtf8String(part, context, span) : PyNone.Instance);
            }
            return result;
        }

        internal static object ExecuteSubstitute(RePatternObject pattern, object replacement, RegexSubjectRange range,
            int count, LythonSourceSpan span, ExecutionContext context, RegexSubstitutionMode mode)
            => SubstituteCoreAsync(pattern, replacement, range, count, span, context, mode, false).GetAwaiter().GetResult();

        internal static ValueTask<object> ExecuteSubstituteAsync(RePatternObject pattern, object replacement, RegexSubjectRange range,
            int count, LythonSourceSpan span, ExecutionContext context, RegexSubstitutionMode mode)
            => SubstituteCoreAsync(pattern, replacement, range, count, span, context, mode, true);

        private static async ValueTask<object> SubstituteCoreAsync(RePatternObject pattern, object replacement, RegexSubjectRange range,
            int count, LythonSourceSpan span, ExecutionContext context, RegexSubstitutionMode mode, bool asynchronous)
        {
            if (!range.IsValid)
                throw new LythonRuntimeException("ValueError", "Regex substitution endpos must not precede pos.", span);
            using var subject = Prepare(pattern, range, context, span);
            PyString result;
            int replaced;
            if (PyStringOps.TryAsString(replacement, out var template))
            {
                var value = RegexCompiler.ProviderCall(() => subject.Substitute(template.Utf8Bytes, count,
                    context.Limits.CancellationToken), context.MemoryGovernor, context.Limits.CancellationToken, span);
                result = CreateUtf8String(value.Value, context, span);
                replaced = value.Count;
            }
            else
            {
                if (replacement is not ICallable)
                    throw RuntimeErrors.Type("Regex replacement must be a string or callable.", span);
                using var cursor = RegexCompiler.ProviderCall(() => subject.Find(context.Limits.CancellationToken),
                    context.MemoryGovernor, context.Limits.CancellationToken, span);
                var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
                replaced = 0;
                var lastByte = range.Original.GetByteIndexForRuneBoundary(range.Pos);
                var endByte = range.Original.GetByteIndexForRuneBoundary(range.EndPos);
                try
                {
                    while (count >= 0 && (count == 0 || replaced < count) &&
                        RegexCompiler.ProviderCall(() => cursor.Next(context.Limits.CancellationToken), context.MemoryGovernor,
                            context.Limits.CancellationToken, span) is { } match)
                    {
                        context.CheckExecution(span);
                        var whole = match.Captures[0]!.Value;
                        var start = whole.StartByte;
                        var end = whole.EndByte;
                        builder.Append(range.Original.Utf8Bytes.Span[lastByte..start]);
                        var projected = CreateMatchObject(pattern, range, match, context, span);
                        var value = asynchronous
                            ? await InvokeCallableTargetAsync(replacement, span, span, context,
                                [CallArgumentValue.Positional(projected)]).ConfigureAwait(false)
                            : InvokeCallableTarget(replacement, span, span, context,
                                () => [CallArgumentValue.Positional(projected)]);
                        context.CheckExecution(span);
                        if (!PyStringOps.TryAsString(value, out var text))
                            throw RuntimeErrors.Type("Regex replacement callable must return a string.", span);
                        builder.Append(text);
                        lastByte = end;
                        replaced++;
                    }
                    builder.Append(range.Original.Utf8Bytes.Span[lastByte..endByte]);
                    result = builder.ToPyStringAndRelease();
                }
                finally { builder.Release(); }
            }
            result = SpliceRangeResult(range, result, span);
            return mode == RegexSubstitutionMode.TextAndCount
                ? PyTuple.FromOwnedArray([result, new BigInteger(replaced)], context.MemoryGovernor, span) : result;
        }

        private static PyString SpliceRangeResult(RegexSubjectRange range, PyString replacement, LythonSourceSpan? span)
        {
            if (range.Pos == 0 && range.EndPos == range.Original.Length) return replacement;
            var start = range.Original.GetByteIndexForRuneBoundary(range.Pos);
            var end = range.Original.GetByteIndexForRuneBoundary(range.EndPos);
            return range.Original.SliceByByteRange(0, start).Concat(replacement)
                .Concat(range.Original.SliceByByteRange(end, range.Original.Utf8Bytes.Length));
        }
    }
}

using System.Numerics;
using Lokad.Lython.Runtime.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private static class RegexMatcher
    {
        internal static object ExecuteSubstitute(
            RePatternObject pattern,
            object replacement,
            RegexSubjectRange range,
            int count,
            LythonSourceSpan span,
            ExecutionContext context,
            RegexSubstitutionMode mode)
        {
            if (UsesDotStarLazyProgression(pattern))
            {
                return ExecuteDotStarLazySubstitute(pattern, replacement, range, count, span, context, mode);
            }

            if (PyStringOps.TryAsString(replacement, out var replacementText))
            {
                if (mode == RegexSubstitutionMode.TextOnly)
                {
                    var replacedText = CreateUtf8String(pattern.Regex.Replace(range.Segment.Utf8Bytes.Span, replacementText.AsString(), count), context, span);
                    return SpliceRangeResult(range, replacedText);
                }

                var result = pattern.Regex.Subn(range.Segment.Utf8Bytes.Span, replacementText.AsString(), count);
                var replacedTextWithCount = CreateUtf8String(result.ResultBytes, context, span);
                return new PyTuple([SpliceRangeResult(range, replacedTextWithCount), new BigInteger(result.ReplacementCount)], context.MemoryGovernor, span);
            }

            if (replacement is not ICallable)
            {
                throw new LythonRuntimeException("TypeError", mode == RegexSubstitutionMode.TextAndCount
                    ? "re.subn(...) replacement must be a string or callable."
                    : "re.sub(...) replacement must be a string or callable.", span);
            }

            var resultWithCount = ExecuteCallableSubstitute(pattern, replacement, range, count, span, context);
            return mode == RegexSubstitutionMode.TextAndCount
                ? new PyTuple([resultWithCount.Result, new BigInteger(resultWithCount.ReplacementCount)], context.MemoryGovernor, span)
                : resultWithCount.Result;
        }

        internal static PyRegexFindIterator CreateFindIterMatches(
            RePatternObject pattern,
            RegexSubjectRange range,
            ExecutionContext context,
            LythonSourceSpan span)
            => new(pattern, range, context, span);

        internal static ReFindAllResult CreateFindAllResult(
            RePatternObject pattern,
            RegexSubjectRange range,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            if (!UsesDotStarLazyProgression(pattern))
            {
                return new ReFindAllResult(RePatternMembers.ProjectFindAllResult(pattern.Regex.FindAllToUtf8(range.Segment.Utf8Bytes.Span), span, context));
            }

            var values = CreateDotStarLazyMatches(pattern, range)
                .Select(match => (object)CreateString(match.Value.ValueText, context, span));
            return new ReFindAllResult(new PyList(values, context.MemoryGovernor, span));
        }

        internal static IEnumerable<Utf8PythonDetailedMatchData> CreateDetailedFindMatches(
            RePatternObject pattern,
            RegexSubjectRange range)
            => UsesDotStarLazyProgression(pattern)
                ? CreateDotStarLazyMatches(pattern, range)
                : range.IsValid ? pattern.Regex.FindIterDetailed(range.Segment.Utf8Bytes.Span) : [];

        internal static ReMatchObject CreateMatchObject(
            RePatternObject pattern,
            RegexSubjectRange range,
            Utf8PythonDetailedMatchData match)
            => CreateMatchObject(pattern, range, match, null, null);

        internal static ReMatchObject CreateMatchObject(
            RePatternObject pattern,
            RegexSubjectRange range,
            Utf8PythonDetailedMatchData match,
            ExecutionContext? context)
            => CreateMatchObject(pattern, range, match, context, null);

        internal static ReMatchObject CreateMatchObject(
            RePatternObject pattern,
            RegexSubjectRange range,
            Utf8PythonDetailedMatchData match,
            ExecutionContext? context,
            LythonSourceSpan? span)
        {
            if (!match.TryGetGroup(0, out var wholeGroup) || !wholeGroup.Success)
            {
                throw new InvalidOperationException("Detailed regex match is missing the whole-match capture.");
            }

            var wholeStart = wholeGroup.HasContiguousByteRange
                ? range.Pos + range.Segment.ByteIndexToRuneIndex(wholeGroup.StartOffsetInBytes)
                : wholeGroup.StartOffsetInUtf16;
            var wholeEnd = wholeGroup.HasContiguousByteRange
                ? range.Pos + range.Segment.ByteIndexToRuneIndex(wholeGroup.EndOffsetInBytes)
                : wholeGroup.EndOffsetInUtf16;
            var wholeValue = context is null
                ? PyString.FromString(wholeGroup.ValueText)
                : CreateString(wholeGroup.ValueText, context, span);

            var captures = match.CaptureSlotCount <= 1
                ? []
                : new ReCapture?[match.CaptureSlotCount - 1];
            for (var i = 1; i < match.CaptureSlotCount; i++)
            {
                if (!match.TryGetGroup(i, out var group) || !group.Success)
                {
                    continue;
                }

                var start = group.HasContiguousByteRange
                    ? range.Pos + range.Segment.ByteIndexToRuneIndex(group.StartOffsetInBytes)
                    : group.StartOffsetInUtf16;
                var end = group.HasContiguousByteRange
                    ? range.Pos + range.Segment.ByteIndexToRuneIndex(group.EndOffsetInBytes)
                    : group.EndOffsetInUtf16;

                captures[i - 1] = new ReCapture(
                    context is null ? PyString.FromString(group.ValueText) : CreateString(group.ValueText, context, span),
                    new BigInteger(start),
                    new BigInteger(end));
            }

            Dictionary<string, int>? namedGroups = null;
            foreach (var entry in match.NameEntries)
            {
                namedGroups ??= new Dictionary<string, int>(StringComparer.Ordinal);
                namedGroups[entry.Name] = entry.Number;
            }

            return new ReMatchObject(
                wholeValue,
                new BigInteger(wholeStart),
                new BigInteger(wholeEnd),
                pattern,
                range.Original,
                new BigInteger(range.Pos),
                new BigInteger(range.EndPos),
                match.CaptureSlotCount,
                captures,
                namedGroups ?? pattern.NamedGroups);
        }

        internal static PyList ProjectSplitResult(
            Utf8PythonSplitItem[] parts,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var items = new object[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                context.CheckExecutionBudget(span);
                var part = parts[i];
                items[i] = part.ValueText is null ? PyNone.Instance : CreateString(part.ValueText, context, span);
            }

            return new PyList(items, context.MemoryGovernor, span);
        }

        private static PyString SpliceRangeResult(RegexSubjectRange range, PyString segmentReplacement)
        {
            if (range.Pos == 0 && range.EndPos == range.Original.Length)
            {
                return segmentReplacement;
            }

            var startByte = range.Original.GetByteIndexForRuneBoundary(range.Pos);
            var endByte = range.Original.GetByteIndexForRuneBoundary(range.EndPos);
            var prefix = range.Original.SliceByByteRange(0, startByte);
            var suffix = range.Original.SliceByByteRange(endByte, range.Original.Utf8Bytes.Length);
            return prefix.Concat(segmentReplacement).Concat(suffix);
        }

        private static bool UsesDotStarLazyProgression(RePatternObject pattern)
            => pattern.Pattern.AsString() == ".*?" && pattern.CaptureSlotCount == 1;

        private static IEnumerable<Utf8PythonDetailedMatchData> CreateDotStarLazyMatches(
            RePatternObject pattern,
            RegexSubjectRange range)
        {
            if (!range.IsValid)
            {
                yield break;
            }

            var dotAll = (pattern.Options & PythonReCompileOptions.DotAll) != 0;
            for (var runeIndex = 0; runeIndex <= range.Segment.Length; runeIndex++)
            {
                var startByte = range.Segment.GetByteIndexForRuneBoundary(runeIndex);
                yield return CreateSyntheticDetailedMatch(startByte, startByte, runeIndex, runeIndex, string.Empty);
                if (runeIndex == range.Segment.Length)
                {
                    continue;
                }

                var endByte = range.Segment.GetByteIndexForRuneBoundary(runeIndex + 1);
                var value = range.Segment.SliceByByteRange(startByte, endByte).AsString();
                if (dotAll || value != "\n")
                {
                    yield return CreateSyntheticDetailedMatch(startByte, endByte, runeIndex, runeIndex + value.Length, value);
                }
            }
        }

        private static Utf8PythonDetailedMatchData CreateSyntheticDetailedMatch(
            int startByte,
            int endByte,
            int startUtf16,
            int endUtf16,
            string value)
            => new()
            {
                Groups =
                [
                    new Utf8PythonGroupMatchData
                    {
                        Number = 0,
                        Success = true,
                        StartOffsetInBytes = startByte,
                        EndOffsetInBytes = endByte,
                        StartOffsetInUtf16 = startUtf16,
                        EndOffsetInUtf16 = endUtf16,
                        HasContiguousByteRange = true,
                        ValueText = value,
                    }
                ],
                NameEntries = [],
            };

        private static object ExecuteDotStarLazySubstitute(
            RePatternObject pattern,
            object replacement,
            RegexSubjectRange range,
            int count,
            LythonSourceSpan span,
            ExecutionContext context,
            RegexSubstitutionMode mode)
        {
            var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
            var sourceBytes = range.Segment.Utf8Bytes.Span;
            var lastByte = 0;
            var replaced = 0;
            foreach (var match in CreateDotStarLazyMatches(pattern, range))
            {
                if (count != 0 && replaced >= count)
                {
                    break;
                }

                var whole = match.Value;
                builder.Append(sourceBytes[lastByte..whole.StartOffsetInBytes]);
                var matchObject = CreateMatchObject(pattern, range, match, context, span);
                if (PyStringOps.TryAsString(replacement, out var template))
                {
                    builder.Append(ReMatchMembers.ExpandReplacementTemplate(matchObject, template, context, span));
                }
                else
                {
                    var replacementValue = InvokeCallableTarget(
                        replacement,
                        span,
                        span,
                        context,
                        () => [CallArgumentValue.Positional(matchObject)]);
                    if (!PyStringOps.TryAsString(replacementValue, out var replacementText))
                    {
                        throw new LythonRuntimeException("TypeError", "Regex replacement callable must return a string.", span);
                    }

                    builder.Append(replacementText);
                }

                lastByte = whole.EndOffsetInBytes;
                replaced++;
            }

            builder.Append(sourceBytes[lastByte..]);
            var result = SpliceRangeResult(range, builder.ToPyStringAndRelease());
            return mode == RegexSubstitutionMode.TextAndCount
                ? new PyTuple([result, new BigInteger(replaced)], context.MemoryGovernor, span)
                : result;
        }

        private static RegexSubstitutionResult ExecuteCallableSubstitute(
            RePatternObject pattern,
            object replacement,
            RegexSubjectRange range,
            int count,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var state = new RegexReplacementState(pattern, range, replacement, span, context);
            var result = pattern.Regex.Subn(
                range.Segment.Utf8Bytes.Span,
                state,
                static (replacementState, match) => EvaluateRegexReplacement(replacementState, match),
                count);
            return new RegexSubstitutionResult(
                SpliceRangeResult(range, CreateUtf8String(result.ResultBytes, context, span)),
                result.ReplacementCount);
        }

        private static string EvaluateRegexReplacement(RegexReplacementState state, Utf8PythonDetailedMatchData match)
        {
            state.Context.CheckExecutionBudget(state.Span);
            var matchObject = CreateMatchObject(state.Pattern, state.Range, match, state.Context, state.Span);
            var replacementValue = InvokeCallableTarget(
                state.Replacement,
                state.Span,
                state.Span,
                state.Context,
                () => [CallArgumentValue.Positional(matchObject)]);

            if (!PyStringOps.TryAsString(replacementValue, out var replacementText))
            {
                throw new LythonRuntimeException("TypeError", "Regex replacement callable must return a string.", state.Span);
            }

            return replacementText.AsString();
        }

        private readonly record struct RegexSubstitutionResult(PyString Result, int ReplacementCount);

        private sealed record RegexReplacementState(
            RePatternObject Pattern,
            RegexSubjectRange Range,
            object Replacement,
            LythonSourceSpan Span,
            ExecutionContext Context);
    }

    private enum RegexSubstitutionMode
    {
        TextOnly,
        TextAndCount,
    }
}

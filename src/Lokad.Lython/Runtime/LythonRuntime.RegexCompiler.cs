using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using Lokad.Lython.Frontend;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static class RegexCompiler
    {
        internal const int InlineCaptureCapacity = 8;

        [InlineArray(InlineCaptureCapacity)]
        internal struct InlineCaptureBuffer { private LythonRegexCapture? _first; }

        internal static long CaptureBufferBytes(int count) => checked(32L + 16L * count);

        internal const int PythonIgnoreCaseFlag = 2;
        internal const int PythonLocaleFlag = 4;
        internal const int PythonMultilineFlag = 8;
        internal const int PythonDotAllFlag = 16;
        internal const int PythonUnicodeFlag = 32;
        internal const int PythonVerboseFlag = 64;
        internal const int RegexDebugFlag = 128;
        internal const int PythonAsciiFlag = 256;

        // Bounds are independently clamped before this test. Python's anchored
        // match may still succeed without consuming text in a reversed range.
        internal static bool IsImpossibleWindow(LythonRegexMatchMode mode, int pos, int endPos)
            => pos > endPos && mode is LythonRegexMatchMode.Search or LythonRegexMatchMode.FullMatch;

        internal static LythonRegexWindow CreateWindow(ReadOnlySpan<byte> subject, int scalarLength,
            int pos, int endPos, CancellationToken token, LythonSourceSpan span, ReadOnlySpan<int> offsets = default)
        {
            if (token.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
            pos = Math.Clamp(pos, 0, scalarLength);
            endPos = Math.Clamp(endPos, 0, scalarLength);
            if (scalarLength == subject.Length) return new(pos, endPos, pos, endPos);
            if (!offsets.IsEmpty) return new(pos, endPos, offsets[pos], offsets[endPos]);

            var lower = Math.Min(pos, endPos);
            var upper = Math.Max(pos, endPos);
            int lowerByte, upperByte;
            // Resolve the bound nearest either original edge, then use it
            // for the other bound when that walk is shorter.
            if (lower <= scalarLength - upper)
            {
                lowerByte = MoveToScalarBoundary(subject, 0, 0, lower, token, span);
                upperByte = upper - lower <= scalarLength - upper
                    ? MoveToScalarBoundary(subject, lower, lowerByte, upper, token, span)
                    : MoveToScalarBoundary(subject, scalarLength, subject.Length, upper, token, span);
            }
            else
            {
                upperByte = MoveToScalarBoundary(subject, scalarLength, subject.Length, upper, token, span);
                lowerByte = upper - lower < lower
                    ? MoveToScalarBoundary(subject, upper, upperByte, lower, token, span)
                    : MoveToScalarBoundary(subject, 0, 0, lower, token, span);
            }

            if (token.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
            return pos <= endPos ? new(pos, endPos, lowerByte, upperByte) : new(pos, endPos, upperByte, lowerByte);
        }

        private static int MoveToScalarBoundary(ReadOnlySpan<byte> subject, int scalar, int offset, int target,
            CancellationToken token, LythonSourceSpan span)
        {
            // The original subject is valid UTF-8. A boundary needs its
            // leading-byte position; scalar decoding would discard the value.
            for (; scalar < target; scalar++)
            {
                if ((scalar & 1023) == 0 && token.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
                do { offset++; } while (offset < subject.Length && (subject[offset] & 0xc0) == 0x80);
            }
            for (; scalar > target; scalar--)
            {
                if ((scalar & 1023) == 0 && token.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
                do { offset--; } while ((subject[offset] & 0xc0) == 0x80);
            }
            return offset;
        }

        // Pass borrowed operation state explicitly so a subject or cursor
        // acquisition needs no per-call closure or delegate allocation.
        internal static T ProviderCall<TState, T>(TState state, Func<TState, T> operation, MemoryGovernor? governor,
            CancellationToken cancellationToken, LythonSourceSpan? span, PyString? pattern = null,
            string operationName = "Regex provider")
        {
            governor?.ThrowIfAdmissionFailed();
            if (cancellationToken.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
            try
            {
                var result = operation(state);
                try
                {
                    governor?.ThrowIfAdmissionFailed();
                    if (cancellationToken.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
                    return result is not null ? result
                        : throw RuntimeErrors.Runtime($"{operationName} returned no result.", span);
                }
                catch
                {
                    // Ownership has not reached the caller's using/field yet.
                    // A token changing as preparation returns must still close
                    // the acquired working resource before its owner can end.
                    if (result is ILythonRegexSubject subject) subject.Dispose();
                    else if (result is ILythonRegexCursor cursor) cursor.Dispose();
                    else if (result is ILythonRegexPattern patternHandle) patternHandle.Dispose();
                    throw;
                }
            }
            catch (Exception error) when (error is not LythonRuntimeException and not OutOfMemoryException)
            { throw ProviderFailure(error, governor, cancellationToken, span, pattern, operationName); }
        }

        internal static bool TryMatch(ILythonRegexSubject subject, LythonRegexMatchMode mode,
            Span<LythonRegexCapture?> captures, out int? lastIndex, MemoryGovernor? governor,
            CancellationToken token, LythonSourceSpan? span, PyString? pattern = null,
            string operationName = "Regex provider")
        {
            governor?.ThrowIfAdmissionFailed();
            if (token.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
            try
            {
                var success = subject.TryMatch(mode, captures, out lastIndex, token);
                governor?.ThrowIfAdmissionFailed();
                if (token.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
                return success;
            }
            catch (Exception error) when (error is not LythonRuntimeException and not OutOfMemoryException)
            { throw ProviderFailure(error, governor, token, span, pattern, operationName); }
        }

        internal static bool TryNext(ILythonRegexCursor cursor, Span<LythonRegexCapture?> captures,
            out int? lastIndex, MemoryGovernor governor, CancellationToken token,
            LythonSourceSpan span, PyString pattern)
        {
            governor.ThrowIfAdmissionFailed();
            if (token.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
            try
            {
                var success = cursor.TryNext(captures, out lastIndex, token);
                governor.ThrowIfAdmissionFailed();
                if (token.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
                return success;
            }
            catch (Exception error) when (error is not LythonRuntimeException and not OutOfMemoryException)
            { throw ProviderFailure(error, governor, token, span, pattern); }
        }

        internal static string? MatchResultError(ReadOnlySpan<byte> input, LythonRegexWindow window,
            LythonRegexMatchMode mode, ReadOnlySpan<LythonRegexCapture?> captures, int? lastIndex,
            MemoryGovernor? governor, CancellationToken token, LythonSourceSpan span)
        {
            if (captures[0] is not { } whole ||
                lastIndex is { } last && (last <= 0 || last >= captures.Length || captures[last] is null))
                return "Regex provider returned inconsistent capture metadata.";

            // Lookbehind may capture before Pos, and lookahead may capture
            // past the whole match. Neither can consume beyond EndPos.
            // A reversed anchored match can still capture an empty range at Pos.
            var reversedStart = mode == LythonRegexMatchMode.Match && window.StartByte > window.EndByte
                ? window.StartByte : -1;
            for (var i = 0; i < captures.Length; i++)
            {
                governor?.ThrowIfAdmissionFailed();
                if (token.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
                if (captures[i] is not { } value) continue;
                if (i != 0 && lastIndex is null)
                    return "Regex provider returned inconsistent capture metadata.";
                if (value.StartByte < 0 || value.EndByte < value.StartByte || value.EndByte > input.Length ||
                    value.StartByte < input.Length && (input[value.StartByte] & 0xc0) == 0x80 ||
                    value.EndByte < input.Length && (input[value.EndByte] & 0xc0) == 0x80)
                    return "Regex provider returned inconsistent capture ranges.";
                if (i != 0 && value.EndByte > window.EndByte &&
                    (value.StartByte != reversedStart || value.EndByte != reversedStart))
                    return "Regex provider returned a capture outside its window.";
            }

            // An anchored match can be empty at Pos in a reversed window.
            var valid = mode switch
            {
                LythonRegexMatchMode.Match => whole.StartByte == window.StartByte &&
                    whole.EndByte <= Math.Max(window.EndByte, window.StartByte),
                LythonRegexMatchMode.FullMatch => whole.StartByte == window.StartByte && whole.EndByte == window.EndByte,
                _ => whole.StartByte >= window.StartByte && whole.EndByte <= window.EndByte
            };
            return valid ? null : "Regex provider returned a match outside its window.";
        }

        private static LythonRuntimeException ProviderFailure(Exception error, MemoryGovernor? governor,
            CancellationToken token, LythonSourceSpan? span, PyString? pattern,
            string operationName = "Regex provider")
        {
            if (error is OperationCanceledException)
            {
                governor?.ThrowIfAdmissionFailed();
                return RuntimeErrors.Cancelled(span);
            }
            if (error is not LythonRegexException providerError)
                return RuntimeErrors.Host("regex provider", error, span);
            governor?.ThrowIfAdmissionFailed();
            if (providerError.Failure != LythonRegexFailure.MemoryAdmission && token.IsCancellationRequested)
                return RuntimeErrors.Cancelled(span);
            return providerError.Failure switch
            {
                LythonRegexFailure.Capability => RuntimeErrors.Capability(providerError.Message, span),
                LythonRegexFailure.Flags => RuntimeErrors.Value(providerError.Message, span),
                LythonRegexFailure.MemoryAdmission => governor?.RefuseAdmission($"{operationName}: {providerError.Message}", providerError.RequestedBytes, span, providerError)
                    ?? RuntimeErrors.Attributed("MemoryError", $"{operationName}: {providerError.Message}", span,
                        LythonFailureReason.SharedMemoryAllowance, requested: providerError.RequestedBytes),
                _ => CreatePatternErrorFailure(providerError.Message, pattern, providerError.Position, governor, token, span),
            };
        }

        internal readonly record struct RegexPatternRange(RePatternObject Pattern, RegexSubjectRange Range);

        internal readonly record struct RegexSubstituteInputs(
            RePatternObject Pattern,
            object Replacement,
            RegexSubjectRange Range,
            int Count);

        internal readonly record struct RegexSplitInputs(RePatternObject Pattern, RegexSubjectRange Range, int MaxSplit);

        internal static int ReadCaptureSlotCount(ILythonRegexPattern pattern, MemoryGovernor? governor,
            CancellationToken token, LythonSourceSpan? span)
        {
            var count = ProviderCall(pattern, static p => p.CaptureSlotCount, governor, token, span);
            if (count <= 0 || count > Array.MaxLength)
                throw RuntimeErrors.Capability("Regex provider returned an invalid capture slot count.", span);
            return count;
        }

        internal static IReadOnlyDictionary<PyString, int> CopyNamedGroups(ILythonRegexPattern pattern, int captureSlotCount,
            MemoryGovernor? governor, CancellationToken token, LythonSourceSpan? span, PyString? sourcePattern = null)
            => ProviderCall((pattern, captureSlotCount, governor, token, span), static state =>
            {
                var (pattern, captureSlotCount, governor, token, span) = state;
                var (entries, count) = ReadNamedGroupEntries(pattern, captureSlotCount, governor, token, span);
                var bytes = checked(64L + 96L * count);
                using var storage = governor?.ReserveTemporary(bytes, span);
                var names = new Dictionary<PyString, int>(count);
                PopulateNamedGroups(entries, count, captureSlotCount, governor, token, span, names,
                    static (table, name, index) => table.TryAdd(name, index));
                storage?.Commit(bytes);
                return names;
            }, governor, token, span, sourcePattern);

        private static readonly PyDict EmptyRuntimeNamedGroups = new();

        private static PyDict CopyRuntimeNamedGroups(ILythonRegexPattern pattern, int captureSlotCount,
            ExecutionContext context, CancellationToken token, LythonSourceSpan span, PyString sourcePattern)
            => ProviderCall((pattern, captureSlotCount, context, token, span), static state =>
            {
                var (pattern, captureSlotCount, context, token, span) = state;
                var governor = context.MemoryGovernor;
                var (entries, count) = ReadNamedGroupEntries(pattern, captureSlotCount, governor, token, span);
                if (count == 0) return EmptyRuntimeNamedGroups;
                var names = new PyDict(count, governor, span);
                try
                {
                    PopulateNamedGroups(entries, count, captureSlotCount, governor, token, span, (names, context, span),
                        static (state, name, index) => state.names.TryAddNativeStringInteger(name, index, state.context, state.span));
                    return names;
                }
                catch { names.DiscardUnpublishedStorage(); throw; }
            }, context.MemoryGovernor, token, span, sourcePattern);

        private static (IReadOnlyList<LythonRegexNamedGroup> Entries, int Count) ReadNamedGroupEntries(
            ILythonRegexPattern pattern, int captureSlotCount, MemoryGovernor? governor, CancellationToken token, LythonSourceSpan? span)
        {
            var entries = pattern.NamedGroups;
            var count = entries?.Count ?? -1;
            governor?.ThrowIfAdmissionFailed();
            if (token.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
            if ((uint)count >= (uint)captureSlotCount)
                throw RuntimeErrors.Runtime("Regex provider returned inconsistent named-group metadata.", span);
            return (entries!, count);
        }

        private static void PopulateNamedGroups<T>(IReadOnlyList<LythonRegexNamedGroup> entries, int count,
            int captureSlotCount, MemoryGovernor? governor, CancellationToken token, LythonSourceSpan? span,
            T table, Func<T, PyString, int, bool> add)
        {
            var previous = 0;
            for (var i = 0; i < count; i++)
            {
                if (token.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
                var entry = entries[i];
                governor?.ThrowIfAdmissionFailed();
                if (token.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
                if (entry.Index <= previous || entry.Index >= captureSlotCount || entry.Name.IsEmpty)
                    throw RuntimeErrors.Runtime("Regex provider returned inconsistent named-group metadata.", span);
                var input = entry.Name.Span;
                for (int offset = 0, scalar = 0; offset < input.Length; scalar++)
                {
                    if ((scalar & 1023) == 0 && token.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
                    if (Rune.DecodeFromUtf8(input[offset..], out _, out var width) != System.Buffers.OperationStatus.Done)
                        throw RuntimeErrors.Runtime("Regex provider returned invalid UTF-8 group names.", span);
                    offset += width;
                }
                // Provider metadata is borrowed until the compiled handle closes.
                var name = PyString.CopyFromUtf8(input, governor, span, cancellationToken: token);
                _ = name.GetHashCode(token, span);
                if (!add(table, name, entry.Index))
                    throw RuntimeErrors.Runtime("Regex provider returned duplicate group names.", span);
                previous = entry.Index;
            }
            if (token.IsCancellationRequested) throw RuntimeErrors.Cancelled(span);
        }

        internal static ValueTask<RePatternObject> CreatePattern(object[] arguments, string signature,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            if (arguments.Length is < 1 or > 2)
                throw RuntimeErrors.Type($"{signature} expects a string pattern and optional flags.", span);

            var flags = arguments.Length == 2 ? arguments[1] : null;
            CheckFlagsHashable(flags, span);
            return asynchronous ? ResolvePatternAsync(arguments[0], flags, span, context)
                : new(ResolvePattern(arguments[0], flags, span, context));
        }

        private static ValueTask<RePatternObject> ResolvePatternAsync(object value, object? requestedFlags,
            LythonSourceSpan span, ExecutionContext context)
            => requestedFlags is PyInstance instance
                ? ResolveProtocolPatternAsync(value, instance, span, context)
                : new(ResolvePattern(value, requestedFlags, span, context));

        private static async ValueTask<RePatternObject> ResolveProtocolPatternAsync(object value, PyInstance flags,
            LythonSourceSpan span, ExecutionContext context)
        {
            if (value is RePatternObject existing)
            {
                RequireFalsyCompiledFlags(await IsTruthyAsync(flags, context, span).ConfigureAwait(false), span);
                return existing;
            }

            var pattern = RequireStringPattern(value, span);
            var options = await ParseFlags(flags, span, context, asynchronous: true).ConfigureAwait(false);
            return CompilePattern(pattern, options, null, span, context);
        }

        private static void RequireFalsyCompiledFlags(bool supplied, LythonSourceSpan span)
        {
            if (supplied)
                throw RuntimeErrors.Value("cannot process flags argument with a compiled pattern", span);
        }

        private static PyString RequireStringPattern(object value, LythonSourceSpan span)
            => PyStringOps.TryAsString(value, out var pattern) ? pattern
                : throw RuntimeErrors.Type("first argument must be string or compiled pattern", span);

        private static RePatternObject ResolvePattern(object value, object? requestedFlags, LythonSourceSpan span,
            ExecutionContext context)
        {
            if (value is RePatternObject existing)
            {
                RequireFalsyCompiledFlags(requestedFlags is not null && IsTruthy(requestedFlags, context, span), span);
                return existing;
            }

            var pattern = RequireStringPattern(value, span);
            // Python's wrapper cache distinguishes implicit Unicode from an
            // explicit flag even when both compile to the same program.
            int? requestedBits = requestedFlags switch
            {
                null => 0,
                bool boolean => boolean ? 1 : 0,
                int small => small,
                BigInteger integer when integer >= int.MinValue && integer <= int.MaxValue => (int)integer,
                _ => null,
            };
            var options = requestedBits is { } bits && TryNormalizeExactFlags(bits, out var exact)
                ? exact : ParseFlags(requestedFlags.RequireNotNull(), span, context, asynchronous: false).GetAwaiter().GetResult();
            return CompilePattern(pattern, options, requestedBits, span, context);
        }

        private static RePatternObject CompilePattern(PyString pattern, LythonRegexOptions options, int? requestedBits,
            LythonSourceSpan span, ExecutionContext context)
        {
            var key = new RegexPatternKey(pattern, options, requestedBits);
            context.CheckExecution(span);
            var token = context.State.CancellationToken;
            _ = pattern.GetHashCode(token, span);
            var cache = context.State.RegexCache ??= new RegexPatternCache();
            if (cache.TryGet(key, out var cached, token, span))
            {
                return cached;
            }

            var provider = context.State.RegexProvider
                ?? throw RuntimeErrors.Capability("Python regex functionality requires a host-supplied Lython regex provider.", span);
            var artifact = context.State.RegexPrograms?.Find(pattern, options, token, span) ??
                GetGlobalContext(context).ImportedRegexPrograms?.Find(pattern, options, token, span);
            var compiled = artifact?.Program ?? ProviderCall((provider, pattern, options, context, token),
                static state => state.provider.Compile(state.pattern.Utf8Bytes, state.options,
                    state.context.MemoryGovernor.Budget, state.token), context.MemoryGovernor, token, span, pattern);
            PyDict? names = null;
            var published = false;
            try
            {
                var captureSlotCount = artifact?.CaptureSlotCount
                    ?? ReadCaptureSlotCount(compiled, context.MemoryGovernor, token, span);
                var flags = ProviderCall(compiled, static p => p.Flags, context.MemoryGovernor, token, span, pattern);
                var wrapperBytes = checked(256L + 64L * captureSlotCount);
                using var storage = context.MemoryGovernor.ReserveTemporary(wrapperBytes, span);
                // Guest-visible name strings can acquire caches and produce new
                // strings. Give those values the current run's governor rather
                // than sharing an analysis governor across concurrent runs.
                names = CopyRuntimeNamedGroups(compiled, captureSlotCount, context, token, span, pattern);
                var result = new RePatternObject(pattern, flags, compiled, captureSlotCount, names, options);
                var canonical = cache.GetOrAdd(key, result, context.MemoryGovernor, span, token);
                if (!ReferenceEquals(canonical, result)) return canonical;
                published = true;
                storage.Commit(wrapperBytes);
                if (artifact is null) context.State.TrackRegexProgramOwner(compiled);
                return result;
            }
            finally
            {
                if (!published)
                {
                    try
                    {
                        if (names is not null && !ReferenceEquals(names, EmptyRuntimeNamedGroups))
                        {
                            foreach (var name in names.Keys) context.MemoryGovernor.RefundUnpublishedValue(name);
                            names.DiscardUnpublishedStorage();
                        }
                    }
                    finally { if (artifact is null) compiled.Dispose(); }
                }
            }
        }

        // The cache lookup hashes flags first, so unhashable flags fail before
        // any dispatch or compiled-pattern check like CPython.
        private static void CheckFlagsHashable(object? flags, LythonSourceSpan span)
        {
            if (flags is null) return;
            try
            {
                _ = PyValueComparer.Instance.GetHashCode(flags);
            }
            catch (InvalidOperationException)
            {
                throw RuntimeErrors.UnhashableType(flags, span);
            }
        }

        internal static ValueTask<RegexPatternRange> CreatePatternAndRange(BoundBuiltinArguments bound, string signature,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var arguments = bound.Values;
            if (!PyStringOps.TryAsString(arguments[1], out var text))
                throw RuntimeErrors.Type($"{signature} expects pattern, string, optional flags, pos, and endpos.", span);

            var flags = bound.Assigned[2] ? arguments[2] : null;
            CheckFlagsHashable(flags, span);
            var pos = arguments[3];
            var endPos = arguments[4];
            if (asynchronous && (pos is PyInstance || endPos is PyInstance || flags is PyInstance))
                return AwaitPatternArguments(arguments[0], text, flags, pos, endPos, span, context);

            return new(FinishPatternRange(arguments[0], text, flags, ParseOptionalPosition(pos, 0, span, context),
                ParseOptionalPosition(endPos, int.MaxValue, span, context), span, context));
        }

        private static async ValueTask<RegexPatternRange> AwaitPatternArguments(object pattern, PyString text, object? flags,
            object pos, object end, LythonSourceSpan span, ExecutionContext context)
        {
            var start = await ParseOptionalPositionAsync(pos, 0, span, context).ConfigureAwait(false);
            var stop = await ParseOptionalPositionAsync(end, int.MaxValue, span, context).ConfigureAwait(false);
            var range = CreateSubjectRange(text, start, stop, span, context);
            return new(await ResolvePatternAsync(pattern, flags, span, context).ConfigureAwait(false), range);
        }

        private static RegexPatternRange FinishPatternRange(object pattern, PyString text, object? flags, int pos, int end,
            LythonSourceSpan span, ExecutionContext context)
        {
            var range = CreateSubjectRange(text, pos, end, span, context);
            return new(ResolvePattern(pattern, flags, span, context), range);
        }

        internal static ValueTask<RegexSubstituteInputs> CreateSubstituteInputs(BoundBuiltinArguments bound, string signature,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var arguments = bound.Values;
            if (!PyStringOps.TryAsString(arguments[2], out var text))
                throw RuntimeErrors.Type($"{signature} expects pattern, replacement, text, optional count, flags, pos, and endpos.", span);

            var replacement = arguments[1];
            if (!PyStringOps.TryAsString(replacement, out _) && replacement is not ICallable)
                throw RuntimeErrors.Type($"{signature} expects replacement to be a string or callable.", span);

            var count = arguments[3];
            var flags = bound.Assigned[4] ? arguments[4] : null;
            var pos = arguments[5];
            var endPos = arguments[6];
            if (asynchronous && (count is PyInstance || pos is PyInstance || endPos is PyInstance || flags is PyInstance))
                return AwaitSubstituteArguments(arguments[0], text, replacement, flags, count, pos, endPos, signature, span, context);

            return new(FinishSubstituteInputs(arguments[0], text, replacement, flags,
                ParseOptionalIntOrDefault(count, 0, "count", signature, span, context),
                ParseOptionalPosition(pos, 0, span, context),
                ParseOptionalPosition(endPos, int.MaxValue, span, context), span, context));
        }

        private static async ValueTask<RegexSubstituteInputs> AwaitSubstituteArguments(object pattern, PyString text, object replacement,
            object? flags, object count, object pos, object end, string signature, LythonSourceSpan span, ExecutionContext context)
        {
            var limit = await ParseOptionalIntAsync(count, "count", signature, span, context).ConfigureAwait(false);
            var start = await ParseOptionalPositionAsync(pos, 0, span, context).ConfigureAwait(false);
            var stop = await ParseOptionalPositionAsync(end, int.MaxValue, span, context).ConfigureAwait(false);
            CheckFlagsHashable(flags, span);
            var compiled = await ResolvePatternAsync(pattern, flags, span, context).ConfigureAwait(false);
            return new(compiled, replacement, CreateSubjectRange(text, start, stop, span, context), limit);
        }

        private static RegexSubstituteInputs FinishSubstituteInputs(object sourcePattern, PyString text, object replacement,
            object? flags, int count, int pos, int end, LythonSourceSpan span, ExecutionContext context)
        {
            CheckFlagsHashable(flags, span);
            var pattern = ResolvePattern(sourcePattern, flags, span, context);
            return new(pattern, replacement, CreateSubjectRange(text, pos, end, span, context), count);
        }

        internal static ValueTask<RegexSplitInputs> CreateSplitInputs(BoundBuiltinArguments bound, string signature,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var arguments = bound.Values;
            if (!PyStringOps.TryAsString(arguments[1], out var text))
                throw RuntimeErrors.Type($"{signature} expects pattern, string, optional maxsplit, flags, pos, and endpos.", span);

            var count = arguments[2];
            var flags = bound.Assigned[3] ? arguments[3] : null;
            var pos = arguments[4];
            var endPos = arguments[5];
            if (asynchronous && (count is PyInstance || pos is PyInstance || endPos is PyInstance || flags is PyInstance))
                return AwaitSplitArguments(arguments[0], text, flags, count, pos, endPos, signature, span, context);

            var start = ParseOptionalPosition(pos, 0, span, context);
            var stop = ParseOptionalPosition(endPos, int.MaxValue, span, context);
            CheckFlagsHashable(flags, span);
            var pattern = ResolvePattern(arguments[0], flags, span, context);
            var limit = ParseOptionalIntOrDefault(count, 0, "maxsplit", signature, span, context);
            return new(new RegexSplitInputs(pattern, CreateSubjectRange(text, start, stop, span, context), limit));
        }

        private static async ValueTask<RegexSplitInputs> AwaitSplitArguments(object sourcePattern, PyString text, object? flags,
            object count, object pos, object end, string signature, LythonSourceSpan span, ExecutionContext context)
        {
            var start = await ParseOptionalPositionAsync(pos, 0, span, context).ConfigureAwait(false);
            var stop = await ParseOptionalPositionAsync(end, int.MaxValue, span, context).ConfigureAwait(false);
            CheckFlagsHashable(flags, span);
            var pattern = await ResolvePatternAsync(sourcePattern, flags, span, context).ConfigureAwait(false);
            var limit = await ParseOptionalIntAsync(count, "maxsplit", signature, span, context).ConfigureAwait(false);
            return new(pattern, CreateSubjectRange(text, start, stop, span, context), limit);
        }

        // Regex bounds coerce through __index__ like CPython. Module-level
        // calls arrive binder-filled, where None marks an omitted optional,
        // while compiled-method calls pass raw values where explicit None is
        // rejected like any other non-integer. Out-of-ssize magnitudes report
        // the ssize_t overflow while int-range overflows keep the historical
        // out-of-range shape.
        internal static int ParseOptionalIntOrDefault(object? value, int defaultValue, string name, string signature, LythonSourceSpan span, ExecutionContext context)
        {
            if (value is null || ReferenceEquals(value, PyNone.Instance))
            {
                return defaultValue;
            }

            return ParseExplicitInt(value, name, signature, span, context);
        }

        internal static int ParseExplicitInt(object value, string name, string signature, LythonSourceSpan span, ExecutionContext context)
            => CheckExplicitInt(ParseBoundInteger(value, span, context), name, signature, span);

        internal static async ValueTask<int> ParseExplicitIntAsync(object value, string name, string signature,
            LythonSourceSpan span, ExecutionContext context)
            => CheckExplicitInt(await ParseBoundIntegerAsync(value, span, context).ConfigureAwait(false), name, signature, span);

        private static int CheckExplicitInt(long integer, string name, string signature, LythonSourceSpan span)
        {
            if (integer < int.MinValue || integer > int.MaxValue)
                throw new LythonRuntimeException("ValueError", $"{signature} {name} is out of range.", span);
            return (int)integer;
        }

        internal static int ParsePosition(object value, LythonSourceSpan span, ExecutionContext context)
            => (int)Math.Clamp(ParseBoundInteger(value, span, context), 0, int.MaxValue);

        internal static async ValueTask<int> ParsePositionAsync(object value, LythonSourceSpan span, ExecutionContext context)
            => (int)Math.Clamp(await ParseBoundIntegerAsync(value, span, context).ConfigureAwait(false), 0, int.MaxValue);

        private static int ParseOptionalPosition(object value, int fallback, LythonSourceSpan span, ExecutionContext context)
            => ReferenceEquals(value, PyNone.Instance) ? fallback : ParsePosition(value, span, context);

        private static ValueTask<int> ParseOptionalPositionAsync(object value, int fallback, LythonSourceSpan span, ExecutionContext context)
            => value is PyInstance ? ParsePositionAsync(value, span, context) : new(ParseOptionalPosition(value, fallback, span, context));

        private static ValueTask<int> ParseOptionalIntAsync(object value, string name, string signature, LythonSourceSpan span, ExecutionContext context)
            => value is PyInstance ? ParseExplicitIntAsync(value, name, signature, span, context)
                : new(ParseOptionalIntOrDefault(value, 0, name, signature, span, context));

        private static long ParseBoundInteger(object value, LythonSourceSpan span, ExecutionContext context)
            => ReadBoundInteger(value, LythonRuntime.CoerceIndexProtocol(value, context, span), span, context);

        private static async ValueTask<long> ParseBoundIntegerAsync(object value, LythonSourceSpan span, ExecutionContext context)
        {
            var coerced = await CoerceTypeIndexAsync(value, context, span, asynchronous: true).ConfigureAwait(false);
            return ReadBoundInteger(value, coerced, span, context);
        }

        private static long ReadBoundInteger(object value, object? coerced, LythonSourceSpan span, ExecutionContext context)
        {
            BigInteger integer;
            if (coerced is bool flag)
            {
                integer = flag ? BigInteger.One : BigInteger.Zero;
            }
            else if (coerced is int small)
            {
                integer = new BigInteger(small);
            }
            else if (coerced is not BigInteger big)
            {
                throw new LythonRuntimeException("TypeError", "'" + RuntimeErrors.DatetimeQualifiedTypeName(value, context) + "' object cannot be interpreted as an integer", span);
            }
            else
            {
                integer = big;
            }

            if (integer > long.MaxValue || integer < long.MinValue)
            {
                throw new LythonRuntimeException("OverflowError", "Python int too large to convert to C ssize_t", span);
            }

            return (long)integer;
        }

        internal static RegexSubjectRange CreateSubjectRange(PyString text, int pos, int endPos,
            LythonSourceSpan span, ExecutionContext context)
        {
            context.CheckExecution(span);
            var length = text.GetCharacterCount(context.State.CancellationToken, span);
            return new RegexSubjectRange(text, Math.Clamp(pos, 0, length), Math.Clamp(endPos, 0, length));
        }

        internal static bool TryNormalizeExactFlags(int flags, out LythonRegexOptions options)
        {
            var mode = flags & (PythonAsciiFlag | PythonUnicodeFlag);
            // The provider checks Python mode conflicts after parsing. DEBUG
            // still reaches the explicit unsupported-feature diagnostic.
            if ((flags & RegexDebugFlag) != 0)
            {
                options = default;
                return false;
            }

            var implicitUnicode = mode == 0;
            options = new(implicitUnicode ? flags | PythonUnicodeFlag : flags, (flags & PythonVerboseFlag) != 0)
                { ImplicitUnicode = implicitUnicode };
            return true;
        }

        // Flags flow through operator dispatch like CPython (sre fix_flags plus
        // the _code/_sre combines): verbose/locale/ascii checks, the unicode
        // combine, debug checks, then the state combines; the converted C int
        // carries unknown bits through to the echo.
        private static async ValueTask<LythonRegexOptions> ParseFlags(object value, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            var verbose = await TestFlag(PythonVerboseFlag).ConfigureAwait(false);
            if (await TestFlag(PythonLocaleFlag).ConfigureAwait(false))
                throw RuntimeErrors.Value("cannot use LOCALE flag with a str pattern", span);

            var stateFlags = value;
            if (!await TestFlag(PythonAsciiFlag).ConfigureAwait(false))
            {
                var unicode = new BigInteger(PythonUnicodeFlag);
                if (value is PyInstance)
                {
                    stateFlags = asynchronous
                        ? await EvaluateAugmentedAssignmentAsync(value, unicode, AugmentedAssignmentOperatorSyntax.BitwiseOr, context, span).ConfigureAwait(false)
                        : EvaluateAugmentedAssignment(value, unicode, AugmentedAssignmentOperatorSyntax.BitwiseOr, context, span);
                }
                else stateFlags = await EvaluateFlagOperation(BinaryOperatorSyntax.BitwiseOr, value, unicode).ConfigureAwait(false);
            }
            else if (await TestFlag(PythonUnicodeFlag).ConfigureAwait(false))
                throw RuntimeErrors.Value("ASCII and UNICODE flags are incompatible", span);

            // The DEBUG probes are observable guest operations even when the
            // converted flag bits leave debugging disabled.
            var debug = new BigInteger(RegexDebugFlag);
            _ = await EvaluateFlagOperation(BinaryOperatorSyntax.BitwiseAnd, value, debug).ConfigureAwait(false);
            var combined = await EvaluateFlagOperation(BinaryOperatorSyntax.BitwiseOr, stateFlags, value).ConfigureAwait(false);
            _ = await EvaluateFlagOperation(BinaryOperatorSyntax.BitwiseAnd, value, debug).ConfigureAwait(false);
            var converted = await EvaluateFlagOperation(BinaryOperatorSyntax.BitwiseOr, value, combined).ConfigureAwait(false);
            var flagBits = ToRegexCInt(converted, span, context);
            _ = await EvaluateFlagOperation(BinaryOperatorSyntax.BitwiseAnd, value, debug).ConfigureAwait(false);

            if ((flagBits & RegexDebugFlag) != 0)
                throw new LythonRuntimeException("NotImplementedError", "re.DEBUG is not supported by Lython's regex runtime.", span);

            return new(flagBits, verbose);

            ValueTask<object> EvaluateFlagOperation(BinaryOperatorSyntax operation, object left, object right)
                => asynchronous ? EvaluateBinaryOperatorAsync(operation, left, right, context, span)
                    : new(EvaluateBinaryOperator(operation, left, right, context, span));

            async ValueTask<bool> TestFlag(int flag)
            {
                var result = await EvaluateFlagOperation(BinaryOperatorSyntax.BitwiseAnd, value, new BigInteger(flag)).ConfigureAwait(false);
                return asynchronous ? await IsTruthyAsync(result, context, span).ConfigureAwait(false)
                    : IsTruthy(result, context, span);
            }
        }

        // The converted C int carries unknown bits through; out-of-range
        // magnitudes report the C-int overflow while other shapes name the type.
        private static int ToRegexCInt(object value, LythonSourceSpan span, ExecutionContext context)
            => value switch
            {
                bool flag => flag ? 1 : 0,
                int small => small,
                BigInteger big when big >= int.MinValue && big <= int.MaxValue => (int)big,
                BigInteger => throw new LythonRuntimeException("OverflowError", "Python int too large to convert to C int", span),
                _ => throw new LythonRuntimeException("TypeError", "'" + RuntimeErrors.DatetimeQualifiedTypeName(value, context) + "' object cannot be interpreted as an integer", span),
            };

    }
}

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
            string operationName = "Regex provider", bool materializePatternError = true)
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
            { throw ProviderFailure(error, governor, cancellationToken, span, pattern, operationName, materializePatternError); }
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
            string operationName = "Regex provider", bool materializePatternError = true)
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
                // Speculative frontend analysis leaves this error for the
                // runtime call. Only that call exposes Python error fields.
                LythonRegexFailure.Pattern when !materializePatternError =>
                    new LythonRuntimeException("PatternError", providerError.Message, span),
                _ => CreatePatternErrorFailure(providerError.Message, pattern, providerError.Position, governor, token, span),
            };
        }

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

            return ResolvePattern(arguments[0], arguments.Length == 2 ? arguments[1] : null, span, context, asynchronous);
        }

        private readonly record struct RequestedFlags(object? Value, int? Bits, int Hash = 0, bool Protocol = false)
        {
            internal RegexPatternCache.Key CreateKey(PyString pattern, CancellationToken token, LythonSourceSpan span)
                => Bits is { } bits ? RegexPatternCache.Key.Exact(pattern, Value, bits, token, span)
                    : RegexPatternCache.Key.Prepared(pattern, Value.RequireNotNull(), Hash, Protocol, token, span);
        }

        private static ValueTask<RequestedFlags> PrepareRequestKey(object pattern, object? flags, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            if (pattern is not (PyString or IPyStringCoercibleValue or RePatternObject))
            {
                // An invalid pattern can still fail or run guest code while
                // hashing its request key. That precedes the flag key.
                if (asynchronous) return AwaitPatternHash(pattern, flags, span, context);
                _ = PyHashProtocols.PrepareKeyHash(pattern, context, span);
                context.CheckExecution(span);
            }
            return PrepareRequestedFlags(flags, span, context, asynchronous);

            static async ValueTask<RequestedFlags> AwaitPatternHash(object pattern, object? flags,
                LythonSourceSpan span, ExecutionContext context)
            {
                _ = await PyHashProtocols.PrepareKeyHashAsync(pattern, context, span).ConfigureAwait(false);
                context.CheckExecution(span);
                return await PrepareRequestedFlags(flags, span, context, asynchronous: true).ConfigureAwait(false);
            }
        }

        private static ValueTask<RequestedFlags> PrepareRequestedFlags(object? flags, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            // The wrapper cache uses the original requested flags, including a
            // guest type's hash, before normalization or compiled-pattern truth.
            switch (flags)
            {
                case null: return new(new RequestedFlags(null, 0));
                case bool boolean: return new(new RequestedFlags(flags, boolean ? 1 : 0));
                case int small: return new(new RequestedFlags(flags, small));
                case BigInteger integer when integer >= int.MinValue && integer <= int.MaxValue:
                    return new(new RequestedFlags(flags, (int)integer));
            }
            if (asynchronous) return AwaitRequestedFlags(flags, span, context);
            var (hash, protocol) = PyHashProtocols.PrepareKeyHash(flags, context, span);
            return new(new RequestedFlags(flags, null, hash, protocol));
        }

        private static async ValueTask<RequestedFlags> AwaitRequestedFlags(object flags, LythonSourceSpan span,
            ExecutionContext context)
        {
            var (hash, protocol) = await PyHashProtocols.PrepareKeyHashAsync(flags, context, span).ConfigureAwait(false);
            return new(flags, null, hash, protocol);
        }

        private static void RequireFalsyCompiledFlags(bool supplied, LythonSourceSpan span)
        {
            if (supplied)
                throw RuntimeErrors.Value("cannot process flags argument with a compiled pattern", span);
        }

        private static PyString RequireStringPattern(object value, LythonSourceSpan span)
            => PyStringOps.TryAsString(value, out var pattern) ? pattern
                : throw RuntimeErrors.Type("first argument must be string or compiled pattern", span);

        internal static ValueTask<RePatternObject> ResolvePattern(object value, object? flags, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            var requested = PrepareRequestKey(value, flags, span, context, asynchronous);
            return requested.IsCompletedSuccessfully ? ResolvePreparedPattern(value, requested.Result, span, context, asynchronous)
                : AwaitRequest(value, requested, span, context);

            static async ValueTask<RePatternObject> AwaitRequest(object value, ValueTask<RequestedFlags> pending,
                LythonSourceSpan span, ExecutionContext context)
                => await ResolvePreparedPattern(value, await pending.ConfigureAwait(false), span, context, asynchronous: true).ConfigureAwait(false);
        }

        private static ValueTask<RePatternObject> ResolvePreparedPattern(object value, RequestedFlags requested,
            LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            if (value is RePatternObject existing)
            {
                if (asynchronous && requested.Value is PyInstance instance)
                    return AwaitCompiledFlags(existing, instance, span, context);
                RequireFalsyCompiledFlags(requested.Value is not null && IsTruthy(requested.Value, context, span), span);
                return new(existing);
            }

            var pattern = RequireStringPattern(value, span);
            var token = context.State.CancellationToken;
            var key = requested.CreateKey(pattern, token, span);
            var cache = context.State.RegexCache ??= new RegexPatternCache();
            var found = cache.Find(key, token, span, context, asynchronous);
            return found.IsCompletedSuccessfully
                ? found.Result is { } match ? new(match) : CompileRequest(cache, key, requested, span, context, asynchronous)
                : AwaitCacheLookup(found, cache, key, requested, span, context);

            static async ValueTask<RePatternObject> AwaitCompiledFlags(RePatternObject pattern, PyInstance flags,
                LythonSourceSpan span, ExecutionContext context)
            {
                RequireFalsyCompiledFlags(await IsTruthyAsync(flags, context, span).ConfigureAwait(false), span);
                return pattern;
            }

            static async ValueTask<RePatternObject> AwaitCacheLookup(ValueTask<RePatternObject?> pending,
                RegexPatternCache cache, RegexPatternCache.Key key, RequestedFlags requested, LythonSourceSpan span,
                ExecutionContext context)
                => await pending.ConfigureAwait(false)
                    ?? await CompileRequest(cache, key, requested, span, context, asynchronous: true).ConfigureAwait(false);
        }

        private static ValueTask<RePatternObject> CompileRequest(RegexPatternCache cache, RegexPatternCache.Key key,
            RequestedFlags requested, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            if (requested.Bits is { } bits && TryNormalizeExactFlags(bits, out var exact))
                return CompilePattern(cache, key, exact, span, context, asynchronous);
            return CompileProtocolRequest(cache, key, requested.Value.RequireNotNull(), span, context, asynchronous);
        }

        private static async ValueTask<RePatternObject> CompileProtocolRequest(RegexPatternCache cache, RegexPatternCache.Key key,
            object flags, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var state = new GuestRegexFlags(flags, span, context, asynchronous);
            if (flags is PyInstance && context.State.RegexProvider is ILythonRegexStagedProvider staged)
                return await CompileStagedPattern(staged, cache, key, state, span, context, asynchronous).ConfigureAwait(false);

            var verboseValue = await state.ReadVerboseAsync().ConfigureAwait(false);
            var verbose = await verboseValue.IsEnabledAsync().ConfigureAwait(false);
            await state.FinishAsync().ConfigureAwait(false);
            var programFlags = await state.ReadProgramFlagsAsync().ConfigureAwait(false);
            var reportedFlags = await state.ReadReportedFlagsAsync().ConfigureAwait(false);
            // Identical explicit modes use the same request as exact flags.
            var options = new LythonRegexOptions(reportedFlags, verbose)
            {
                ProgramFlags = programFlags == (reportedFlags & GuestRegexFlags.ProgramMask)
                    && (reportedFlags & (PythonUnicodeFlag | PythonAsciiFlag)) != 0 ? null : programFlags,
            };
            return await CompilePattern(cache, key, options, span, context, asynchronous).ConfigureAwait(false);
        }

        private static async ValueTask<RePatternObject> CompileStagedPattern(ILythonRegexStagedProvider provider,
            RegexPatternCache cache, RegexPatternCache.Key key, GuestRegexFlags flags, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous)
        {
            var token = context.State.CancellationToken;
            var governor = context.MemoryGovernor;
            var pattern = key.Pattern;
            ILythonRegexPattern? unpublished = null;
            try
            {
                using (var parsed = await ParseStagedPattern(provider, flags, pattern, context, span, token).ConfigureAwait(false))
                {
                    var programFlags = await flags.ReadProgramFlagsAsync().ConfigureAwait(false);
                    unpublished = ProviderCall((parsed, programFlags, governor, token),
                        static state => state.parsed.Compile(state.programFlags, state.governor.Budget, state.token),
                        governor, token, span, pattern);
                }

                var reported = await flags.ReadReportedFlagsAsync().ConfigureAwait(false);
                var compiled = unpublished.RequireNotNull();
                unpublished = null;
                // An integer option key cannot describe parser decisions made
                // by guest callbacks. Compare actual compiled programs.
                return await PublishPattern(cache, key, compiled, artifact: null, options: null, reported,
                    span, context, asynchronous).ConfigureAwait(false);
            }
            finally { unpublished?.Dispose(); }
        }

        private static async ValueTask<ILythonRegexParsedPattern> ParseStagedPattern(ILythonRegexStagedProvider provider,
            GuestRegexFlags flags, PyString pattern, ExecutionContext context, LythonSourceSpan span, CancellationToken token)
        {
            context.CheckExecution(span);
            ILythonRegexParsedPattern? parsed = null;
            try
            {
                parsed = await provider.ParseAsync(pattern.Utf8Bytes, flags, context.MemoryGovernor.Budget, token)
                    .ConfigureAwait(false);
                context.CheckExecution(span);
                if (parsed is null || !flags.ParsingComplete)
                    throw RuntimeErrors.Capability("Regex provider returned without completing staged parsing.", span);
                var complete = parsed;
                parsed = null;
                return complete;
            }
            catch (Exception error) when (error is not LythonRuntimeException and not OutOfMemoryException)
            { throw ProviderFailure(error, context.MemoryGovernor, token, span, pattern); }
            finally
            {
                flags.CloseParsing();
                parsed?.Dispose();
            }
        }

        private static ValueTask<RePatternObject> CompilePattern(RegexPatternCache cache, RegexPatternCache.Key key,
            LythonRegexOptions options, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            context.CheckExecution(span);
            var token = context.State.CancellationToken;
            var pattern = key.Pattern;
            var provider = context.State.RegexProvider
                ?? throw RuntimeErrors.Capability("Python regex functionality requires a host-supplied Lython regex provider.", span);
            var artifact = context.State.RegexPrograms?.Find(pattern, options, token, span) ??
                GetGlobalContext(context).ImportedRegexPrograms?.Find(pattern, options, token, span);
            var compiled = artifact?.Program ?? ProviderCall((provider, pattern, options, context, token),
                static state => state.provider.Compile(state.pattern.Utf8Bytes, state.options,
                    state.context.MemoryGovernor.Budget, state.token), context.MemoryGovernor, token, span, pattern);
            return PublishPattern(cache, key, compiled, artifact, options, reported: null, span, context, asynchronous);
        }

        private static async ValueTask<RePatternObject> PublishPattern(RegexPatternCache cache, RegexPatternCache.Key key,
            ILythonRegexPattern compiled, CompiledRegexPrograms.Entry? artifact, LythonRegexOptions? options,
            int? reported, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var token = context.State.CancellationToken;
            var pattern = key.Pattern;
            PyDict? names = null;
            var published = false;
            try
            {
                var captureSlotCount = artifact?.CaptureSlotCount
                    ?? ReadCaptureSlotCount(compiled, context.MemoryGovernor, token, span);
                var flags = reported ?? ProviderCall(compiled, static p => p.Flags, context.MemoryGovernor, token, span, pattern);
                var wrapperBytes = checked(256L + 64L * captureSlotCount);
                using var storage = context.MemoryGovernor.ReserveTemporary(wrapperBytes, span);
                // Guest-visible name strings can acquire caches and produce new
                // strings. Give those values the current run's governor rather
                // than sharing an analysis governor across concurrent runs.
                names = CopyRuntimeNamedGroups(compiled, captureSlotCount, context, token, span, pattern);
                var result = new RePatternObject(pattern, flags, compiled, captureSlotCount, names, options);
                var canonical = await cache.GetOrAdd(key, result, context.MemoryGovernor, span, token, context, asynchronous).ConfigureAwait(false);
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

        internal static int ParsePosition(object value, LythonSourceSpan span, ExecutionContext context)
            => (int)Math.Clamp(ParseIndex(value, span, context), 0, int.MaxValue);

        internal static async ValueTask<int> ParsePositionAsync(object value, LythonSourceSpan span, ExecutionContext context)
            => (int)Math.Clamp(await ParseIndexAsync(value, span, context).ConfigureAwait(false), 0, int.MaxValue);

        internal static long ParseIndex(object value, LythonSourceSpan span, ExecutionContext context)
            => ReadBoundInteger(value, LythonRuntime.CoerceIndexProtocol(value, context, span), span, context);

        internal static async ValueTask<long> ParseIndexAsync(object value, LythonSourceSpan span, ExecutionContext context)
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

        // The provider sees only integer modes and truth values. Guest
        // semantics and the evolving parser operand stay with the execution.
        private sealed class GuestRegexFlags(object requested, LythonSourceSpan span,
            ExecutionContext context, bool asynchronous) : ILythonRegexParserFlags
        {
            internal const int ProgramMask = PythonIgnoreCaseFlag | PythonLocaleFlag | PythonMultilineFlag |
                PythonDotAllFlag | PythonUnicodeFlag | PythonAsciiFlag;

            private readonly object _requested = requested;
            private object _parserFlags = requested;
            private bool _parsingClosed;
            internal bool ParsingComplete { get; private set; }
            internal void CloseParsing() => _parsingClosed = true;

            public async ValueTask<LythonRegexVerboseFlag> ReadVerboseAsync()
            {
                CheckParsing();
                var operand = await EvaluateParserFlag(PythonVerboseFlag).ConfigureAwait(false);
                return operand switch
                {
                    bool boolean => new(boolean),
                    int number => new(number != 0),
                    BigInteger number => new(!number.IsZero),
                    _ => new(() =>
                    {
                        CheckParsing();
                        return ReadTruthAsync(operand);
                    }),
                };
            }

            public ValueTask AddGlobalAsync(int flagBits)
            {
                CheckParsing();
                return AddParserFlags(flagBits);
            }

            private async ValueTask AddParserFlags(int flagBits)
            {
                var flags = new BigInteger(flagBits);
                if (_parserFlags is PyInstance)
                {
                    _parserFlags = asynchronous
                        ? await EvaluateAugmentedAssignmentAsync(_parserFlags, flags, AugmentedAssignmentOperatorSyntax.BitwiseOr,
                            context, span).ConfigureAwait(false)
                        : EvaluateAugmentedAssignment(_parserFlags, flags, AugmentedAssignmentOperatorSyntax.BitwiseOr, context, span);
                }
                else _parserFlags = await Evaluate(BinaryOperatorSyntax.BitwiseOr, _parserFlags, flags).ConfigureAwait(false);
            }

            public async ValueTask FinishAsync()
            {
                CheckParsing();
                if (await TestParserFlag(PythonLocaleFlag).ConfigureAwait(false))
                    throw RuntimeErrors.Value("cannot use LOCALE flag with a str pattern", span);
                if (!await TestParserFlag(PythonAsciiFlag).ConfigureAwait(false))
                    await AddParserFlags(PythonUnicodeFlag).ConfigureAwait(false);
                else if (await TestParserFlag(PythonUnicodeFlag).ConfigureAwait(false))
                    throw RuntimeErrors.Value("ASCII and UNICODE flags are incompatible", span);
                ParsingComplete = true;
            }

            internal async ValueTask<int> ReadProgramFlagsAsync()
            {
                context.CheckExecution(span);
                await ProbeDebugAsync().ConfigureAwait(false);
                var program = await Evaluate(BinaryOperatorSyntax.BitwiseOr, _parserFlags, _requested).ConfigureAwait(false);
                // Wide integers can carry arbitrary unused bits. Mask through
                // the managed numeric path with its allocation admission.
                return program switch
                {
                    bool => 0,
                    int bits => bits & ProgramMask,
                    BigInteger bits when bits >= int.MinValue && bits <= int.MaxValue => (int)bits & ProgramMask,
                    BigInteger => ToRegexCInt(await Evaluate(BinaryOperatorSyntax.BitwiseAnd,
                        program, new BigInteger(ProgramMask)).ConfigureAwait(false), span, context),
                    _ => throw RuntimeErrors.Capability("Regex compilation requires integer matching flags.", span),
                };
            }

            internal async ValueTask<int> ReadReportedFlagsAsync()
            {
                context.CheckExecution(span);
                await ProbeDebugAsync().ConfigureAwait(false);
                var reported = await Evaluate(BinaryOperatorSyntax.BitwiseOr, _requested, _parserFlags).ConfigureAwait(false);
                if (reported is PyInstance)
                    reported = (await CoerceTypeIndexAsync(reported, context, span, asynchronous).ConfigureAwait(false))!;
                var bits = ToRegexCInt(reported, span, context);
                await ProbeDebugAsync().ConfigureAwait(false);
                if ((bits & RegexDebugFlag) != 0)
                    throw new LythonRuntimeException("NotImplementedError", "re.DEBUG is not supported by Lython's regex runtime.", span);
                return bits;
            }

            private void CheckParsing()
            {
                if (ParsingComplete || _parsingClosed)
                    throw RuntimeErrors.Capability("Regex provider used flag callbacks after staged parsing finished.", span);
                context.CheckExecution(span);
            }

            private ValueTask<object> ProbeDebugAsync()
                => Evaluate(BinaryOperatorSyntax.BitwiseAnd, _requested, new BigInteger(RegexDebugFlag));

            private ValueTask<object> Evaluate(BinaryOperatorSyntax operation, object left, object right)
                => asynchronous ? EvaluateBinaryOperatorAsync(operation, left, right, context, span)
                    : new(EvaluateBinaryOperator(operation, left, right, context, span));

            private async ValueTask<bool> TestParserFlag(int flag)
                => await ReadTruthAsync(await EvaluateParserFlag(flag).ConfigureAwait(false)).ConfigureAwait(false);

            private ValueTask<object> EvaluateParserFlag(int flag)
                => Evaluate(BinaryOperatorSyntax.BitwiseAnd, _parserFlags, new BigInteger(flag));

            private ValueTask<bool> ReadTruthAsync(object value)
                => asynchronous ? IsTruthyAsync(value, context, span) : new(IsTruthy(value, context, span));
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

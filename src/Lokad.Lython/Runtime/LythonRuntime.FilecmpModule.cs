using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    private sealed class FilecmpModule : PyModule
    {
        public static readonly FilecmpModule Instance = new();

        private static readonly string[] Members = ["cmp", "clear_cache", "dircmp"];

        private FilecmpModule() : base("filecmp")
        {
        }

        public override IReadOnlyList<string> ExportedNames => Members;

        public override IReadOnlyList<string> MemberNames => Members;

        public override bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "cmp" => BuiltinCallable.Create(LythonKnownCallableSignatures.FilecmpCmp, CompareFiles, CompareFilesAsync),
                "clear_cache" => BuiltinCallable.Create(LythonKnownCallableSignatures.FilecmpClearCache, ClearCache),
                "dircmp" => BuiltinCallable.Create(
                    LythonKnownCallableSignatures.FilecmpDircmp,
                    (_, span, _) => throw new LythonRuntimeException(
                        "NotImplementedError",
                        "filecmp.dircmp is unsupported because recursive directory comparison is outside Lython's contained filecmp surface.",
                        span)),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        private static object CompareFiles(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => CompareFilesCoreAsync(arguments, span, context, false).GetAwaiter().GetResult();

        private static ValueTask<object> CompareFilesAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context)
            => CompareFilesCoreAsync(arguments, span, context, true);

        private static async ValueTask<object> CompareFilesCoreAsync(object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
        {
            var first = PathOps.Normalize(CoercePathLike(arguments[0], context, span, "filecmp.cmp").AsString(), context.Host.Cwd);
            var second = PathOps.Normalize(CoercePathLike(arguments[1], context, span, "filecmp.cmp").AsString(), context.Host.Cwd);
            context.CheckExecution(span);
            var firstStat = asynchronous ? await context.HostStatAsync(first, span).ConfigureAwait(false) : context.HostStat(first, span);
            if (!firstStat.Exists) throw MissingFile(first, span);
            context.CheckExecution(span);
            var secondStat = asynchronous ? await context.HostStatAsync(second, span).ConfigureAwait(false) : context.HostStat(second, span);
            if (!secondStat.Exists) throw MissingFile(second, span);
            if (!firstStat.IsFile || !secondStat.IsFile) return false;
            var shallow = arguments.Length < 3 || (asynchronous ? await IsTruthyAsync(arguments[2], context, span).ConfigureAwait(false) : IsTruthy(arguments[2], context, span));
            context.CheckExecution(span);
            if (shallow && HaveEqualStatSignature(firstStat, secondStat)) return true;
            if (firstStat.Size != secondStat.Size) return false;

            const int windowSize = 8192;
            using var buffers = context.MemoryGovernor.ReserveTemporary(2L * (32 + windowSize), span);
            var firstBuffer = new byte[windowSize];
            var secondBuffer = new byte[windowSize];
            long offset = 0;
            while (true)
            {
                var firstCount = await ReadComparisonWindowAsync(first, offset, firstBuffer, context, span, asynchronous).ConfigureAwait(false);
                var secondCount = await ReadComparisonWindowAsync(second, offset, secondBuffer, context, span, asynchronous).ConfigureAwait(false);
                context.CheckExecution(span);
                if (firstCount != secondCount || !firstBuffer.AsSpan(0, firstCount).SequenceEqual(secondBuffer.AsSpan(0, secondCount))) return false;
                if (firstCount == 0) return true;
                offset = checked(offset + firstCount);
            }
        }

        private static async ValueTask<int> ReadComparisonWindowAsync(string path, long offset, byte[] buffer, ExecutionContext context, LythonSourceSpan span, bool asynchronous)
        {
            var filled = 0;
            while (filled < buffer.Length)
            {
                context.CheckExecution(span);
                var remaining = buffer.Length - filled;
                using var acquisition = context.MemoryGovernor.ReserveTemporary(32L + remaining, span);
                var payload = asynchronous ? await context.ReadHostBytesRangeAsync(path, checked(offset + filled), remaining, span).ConfigureAwait(false)
                    : context.ReadHostBytesRange(path, checked(offset + filled), remaining, span);
                context.CheckExecution(span);
                if (payload.Length > remaining)
                    throw RuntimeErrors.Attributed("RuntimeError", "host binary range read exceeded its requested count", span, LythonFailureReason.HostOperationFailed);
                if (payload.IsEmpty) break;
                payload.Span.CopyTo(buffer.AsSpan(filled));
                filled += payload.Length;
            }
            return filled;
        }

        private static bool HaveEqualStatSignature(LythonPathStat first, LythonPathStat second)
            => first.IsFile == second.IsFile &&
               first.IsDir == second.IsDir &&
               first.Size == second.Size &&
               first.ModifiedAtTimestamp == second.ModifiedAtTimestamp;

        private static LythonRuntimeException MissingFile(string path, LythonSourceSpan span)
            => new("FileNotFoundError", $"No such file or directory: '{path}'", span);

        private static object ClearCache(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            _ = arguments;
            _ = span;
            _ = context;
            return PyNone.Instance;
        }
    }
}

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
                "cmp" => new BuiltinCallable(LythonKnownCallableSignatures.FilecmpCmp, CompareFiles, CompareFilesAsync),
                "clear_cache" => new BuiltinCallable(LythonKnownCallableSignatures.FilecmpClearCache, ClearCache),
                "dircmp" => new BuiltinCallable(
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
        {
            var request = ParseCompareArguments(arguments, span, context);
            context.RegisterHostCall(span);
            var firstStat = context.HostStat(request.First, span);
            context.RegisterHostCall(span);
            var secondStat = context.HostStat(request.Second, span);
            var metadataResult = CompareMetadata(request, firstStat, secondStat, span);
            if (metadataResult != FileComparisonDisposition.CompareContents)
            {
                return metadataResult == FileComparisonDisposition.Equal;
            }

            using var firstBytes = ReadGovernedHostBytes(request.First, context, span);
            using var secondBytes = ReadGovernedHostBytes(request.Second, context, span);
            return firstBytes.Span.SequenceEqual(secondBytes.Span);
        }

        private static async ValueTask<object> CompareFilesAsync(
            object[] arguments,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var request = ParseCompareArguments(arguments, span, context);
            context.RegisterHostCall(span);
            var firstStat = await context.HostStatAsync(request.First, span).ConfigureAwait(false);
            context.RegisterHostCall(span);
            var secondStat = await context.HostStatAsync(request.Second, span).ConfigureAwait(false);
            var metadataResult = CompareMetadata(request, firstStat, secondStat, span);
            if (metadataResult != FileComparisonDisposition.CompareContents)
            {
                return metadataResult == FileComparisonDisposition.Equal;
            }

            using var firstBytes = await ReadGovernedHostBytesAsync(request.First, context, span).ConfigureAwait(false);
            using var secondBytes = await ReadGovernedHostBytesAsync(request.Second, context, span).ConfigureAwait(false);
            return firstBytes.Span.SequenceEqual(secondBytes.Span);
        }

        private static FileComparisonRequest ParseCompareArguments(
            object[] arguments,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var first = PathOps.Normalize(
                CoercePathLike(arguments[0], context, span, "filecmp.cmp(f1, f2, shallow=True)").AsString(),
                context.Host.Cwd);
            var second = PathOps.Normalize(
                CoercePathLike(arguments[1], context, span, "filecmp.cmp(f1, f2, shallow=True)").AsString(),
                context.Host.Cwd);
            var mode = arguments.Length < 3 || IsTruthy(arguments[2])
                ? FileComparisonMode.Shallow
                : FileComparisonMode.Exact;
            return new FileComparisonRequest(first, second, mode);
        }

        private enum FileComparisonMode
        {
            Exact,
            Shallow
        }

        private enum FileComparisonDisposition
        {
            Different,
            Equal,
            CompareContents
        }

        private readonly record struct FileComparisonRequest(
            string First,
            string Second,
            FileComparisonMode Mode);

        private static FileComparisonDisposition CompareMetadata(
            FileComparisonRequest request,
            LythonPathStat first,
            LythonPathStat second,
            LythonSourceSpan span)
        {
            if (!first.Exists)
            {
                throw MissingFile(request.First, span);
            }

            if (!second.Exists)
            {
                throw MissingFile(request.Second, span);
            }

            if (!first.IsFile || !second.IsFile || first.Size != second.Size)
            {
                return FileComparisonDisposition.Different;
            }

            return request.Mode == FileComparisonMode.Shallow && HaveEqualStatSignature(first, second)
                ? FileComparisonDisposition.Equal
                : FileComparisonDisposition.CompareContents;
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

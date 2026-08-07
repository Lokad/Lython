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

        public override bool TryGetMember(string name, out object value)
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
                _ => null!,
            };

            return value is not null;
        }

        private static object CompareFiles(object[] arguments, LythonSourceSpan span, ExecutionContext context)
        {
            var (first, second, shallow) = ParseCompareArguments(arguments, span, context);
            context.RegisterHostCall(span);
            var firstStat = context.HostStat(first, span);
            context.RegisterHostCall(span);
            var secondStat = context.HostStat(second, span);
            if (!firstStat.Exists)
            {
                throw MissingFile(first, span);
            }

            if (!secondStat.Exists)
            {
                throw MissingFile(second, span);
            }

            if (!firstStat.IsFile || !secondStat.IsFile)
            {
                return false;
            }

            if (shallow && HaveEqualStatSignature(firstStat, secondStat))
            {
                return true;
            }

            if (firstStat.Size != secondStat.Size)
            {
                return false;
            }

            var firstBytes = ReadGovernedHostBytes(first, context, span);
            var secondBytes = ReadGovernedHostBytes(second, context, span);
            return firstBytes.Span.SequenceEqual(secondBytes.Span);
        }

        private static async ValueTask<object> CompareFilesAsync(
            object[] arguments,
            LythonSourceSpan span,
            ExecutionContext context)
        {
            var (first, second, shallow) = ParseCompareArguments(arguments, span, context);
            context.RegisterHostCall(span);
            var firstStat = await context.HostStatAsync(first, span).ConfigureAwait(false);
            context.RegisterHostCall(span);
            var secondStat = await context.HostStatAsync(second, span).ConfigureAwait(false);
            if (!firstStat.Exists)
            {
                throw MissingFile(first, span);
            }

            if (!secondStat.Exists)
            {
                throw MissingFile(second, span);
            }

            if (!firstStat.IsFile || !secondStat.IsFile)
            {
                return false;
            }

            if (shallow && HaveEqualStatSignature(firstStat, secondStat))
            {
                return true;
            }

            if (firstStat.Size != secondStat.Size)
            {
                return false;
            }

            var firstBytes = await ReadGovernedHostBytesAsync(first, context, span).ConfigureAwait(false);
            var secondBytes = await ReadGovernedHostBytesAsync(second, context, span).ConfigureAwait(false);
            return firstBytes.Span.SequenceEqual(secondBytes.Span);
        }

        private static (string First, string Second, bool Shallow) ParseCompareArguments(
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
            var shallow = arguments.Length < 3 || IsTruthy(arguments[2]);
            return (first, second, shallow);
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

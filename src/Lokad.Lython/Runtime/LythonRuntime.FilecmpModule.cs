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
            var first = (asynchronous ? await CoercePathLikeAsync(arguments[0],context,span,"filecmp.cmp").ConfigureAwait(false) : CoercePathLike(arguments[0],context,span,"filecmp.cmp")).AsString();
            var second = (asynchronous ? await CoercePathLikeAsync(arguments[1],context,span,"filecmp.cmp").ConfigureAwait(false) : CoercePathLike(arguments[1],context,span,"filecmp.cmp")).AsString();
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

            const int windowSize=8192;
            using var buffers=context.MemoryGovernor.ReserveTemporary(2L*(128+32+windowSize),span);
            var firstBuffer=new byte[windowSize];var secondBuffer=new byte[windowSize];
            var firstFile=new OpenedByteFile(context.Services);var secondFile=new OpenedByteFile(context.Services);
            bool equal;
            try
            {
                var options=new LythonFileOpenOptions(LythonFileAccess.Read);
                await firstFile.OpenAsync(first,options,span,asynchronous).ConfigureAwait(false);
                await secondFile.OpenAsync(second,options,span,asynchronous).ConfigureAwait(false);
                while(true)
                {
                    var firstCount=await ReadComparisonWindowAsync(firstFile,firstBuffer,span,asynchronous).ConfigureAwait(false);
                    var secondCount=await ReadComparisonWindowAsync(secondFile,secondBuffer,span,asynchronous).ConfigureAwait(false);
                    context.CheckExecution(span);
                    if (firstCount != secondCount || !firstBuffer.AsSpan(0, firstCount).SequenceEqual(secondBuffer.AsSpan(0, secondCount)))
                    { equal = false; break; }
                    if (firstCount == 0) { equal = true; break; }
                }
            }
            catch
            {
                // Join both native users without replacing the operation's failure.
                try { await secondFile.CloseAfterFailureAsync(span, asynchronous).ConfigureAwait(false); }
                finally { await firstFile.CloseAfterFailureAsync(span, asynchronous).ConfigureAwait(false); }
                throw;
            }
            try { await secondFile.CloseAsync(span, asynchronous).ConfigureAwait(false); }
            finally { await firstFile.CloseAsync(span, asynchronous).ConfigureAwait(false); }
            return equal;
        }

        private static async ValueTask<int> ReadComparisonWindowAsync(OpenedByteFile file,byte[] buffer,LythonSourceSpan span,bool asynchronous)
        {
            var filled=0;
            while(filled<buffer.Length)
            {
                var count=await file.ReadAsync(buffer.AsMemory(filled),span,asynchronous).ConfigureAwait(false);
                if(count==0)break;
                filled+=count;
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

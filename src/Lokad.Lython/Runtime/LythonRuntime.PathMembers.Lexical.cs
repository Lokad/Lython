using System.Numerics;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal static partial class PathMembers
    {
        private sealed class LexicalPathMemberProvider : IPathMemberProvider
        {
            private static readonly PyString DoubleSlash = PyString.FromString("//");
            private static readonly LythonCallableSignature PathMatchSignature = LythonCallableSignature.Create("Path.match", ["pattern"]);
            private static readonly LythonCallableSignature PathIsRelativeToSignature = LythonCallableSignature.Create("Path.is_relative_to", ["other"]);
            private static readonly LythonCallableSignature PathRelativeToSignature = LythonCallableSignature.Create("Path.relative_to", ["other"]);
            private static readonly LythonCallableSignature PathWithSuffixSignature = LythonCallableSignature.Create("Path.with_suffix", ["suffix"]);
            private static readonly LythonCallableSignature PathWithNameSignature = LythonCallableSignature.Create("Path.with_name", ["name"]);
            private static readonly LythonCallableSignature PathWithStemSignature = LythonCallableSignature.Create("Path.with_stem", ["stem"]);
            private static readonly LythonCallableSignature PathExpanduserSignature = LythonCallableSignature.Create("Path.expanduser");
            public static readonly LexicalPathMemberProvider Instance = new();

            private static async ValueTask<object> JoinPath(PyPath path, object[] arguments, LythonSourceSpan span, ExecutionContext context, bool asynchronous)
            {
                var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
                try
                {
                    PathOps.AppendNormalized(builder, path.Value, context, span);
                    foreach (var argument in arguments)
                    {
                        var part = asynchronous ? await CoercePathLikeAsync(argument, context, span, "Path.joinpath").ConfigureAwait(false)
                            : CoercePathLike(argument, context, span, "Path.joinpath");
                        PathOps.AppendNormalized(builder, part, context, span);
                    }
                    var result = PathOps.Finish(builder);
                    return OwnPathResult(result, result, context.MemoryGovernor, span, path.IsPure);
                }
                finally { builder.Release(); }
            }

            private static async ValueTask<object> RelativePath(PyPath path, object argument, LythonSourceSpan span, ExecutionContext context, bool asynchronous, bool producePath)
            {
                var other = asynchronous ? await CoercePathLikeAsync(argument, context, span, "Path.relative_to").ConfigureAwait(false)
                    : CoercePathLike(argument, context, span, "Path.relative_to");
                other = PathOps.NormalizeLexical(other, context, span);
                var relative = PathOps.TryRelative(path.Value, other, out var start);
                if (!producePath) return relative;
                if (!relative) throw RuntimeErrors.Value("path is not under the specified parent", span);
                var result = start == path.Value.Utf8Bytes.Length ? PyStringOps.DotLiteral
                    : PyString.CopyFromUtf8(path.Value.Utf8Bytes.Span[start..], context.MemoryGovernor, span);
                return OwnPathResult(result, result, context.MemoryGovernor, span, path.IsPure);
            }

            public bool TryGetMember(PyPath path, string name, [MaybeNullWhen(false)] out object value)
            {
                value = name switch
                {
                    "drive" => PyString.Empty,
                    "root" or "anchor" => PathOps.RootLength(path.Value.Utf8Bytes.Span) switch { 0 => PyString.Empty, 2 => DoubleSlash, _ => PyStringOps.SlashLiteral },
                    "__fspath__" => BoundCallable.CreateNoArguments(path, "Path.__fspath__", static (receiver, _, _) => receiver.Value),
                    "is_absolute" => BoundCallable.CreateNoArguments(
                        path,
                        "Path.is_absolute",
                        static (receiver, _, _) => PathOps.RootLength(receiver.Value.Utf8Bytes.Span) != 0),
                    "is_reserved" => BoundCallable.CreateNoArguments(path, "Path.is_reserved", static (_, _, _) => false),
                    "is_mount" when !path.IsPure => BoundCallable.CreateNoArguments(path, "Path.is_mount", static (receiver, _, _) => receiver.Value.Utf8Bytes.Span.SequenceEqual("/"u8)),
                    "joinpath" => BoundCallable.Create(
                        (arguments, span, context) => JoinPath(path, arguments, span, context, false).GetAwaiter().GetResult(),
                        (arguments, span, context) => JoinPath(path, arguments, span, context, true)),
                    "expanduser" => UnsupportedPathMember(PathExpanduserSignature, "Path.expanduser() is not supported by Lython; the host does not expose an ambient user home directory."),
                    "match" => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var pattern))
                        {
                            throw new LythonRuntimeException("TypeError", "Path.match(pattern) expects one string argument.", span);
                        }

                        using var scratch = PathOps.ReserveWorking(path.Value, context, span, pattern);
                        return PathOps.Match(path.Value.AsString(), pattern.AsString(), context.Limits.CancellationToken);
                    }, PathMatchSignature),
                    "is_relative_to" => BoundCallable.Create(
                        (arguments, span, context) => RelativePath(path, arguments[0], span, context, false, false).GetAwaiter().GetResult(),
                        PathIsRelativeToSignature,
                        (arguments, span, context) => RelativePath(path, arguments[0], span, context, true, false)),
                    "as_posix" => BoundCallable.CreateNoArguments(path, "Path.as_posix", static (receiver, _, _) => receiver.Value),
                    "resolve" when !path.IsPure => BoundCallable.CreateNoArguments(
                        path,
                        "Path.resolve",
                        static (receiver, span, context) => OwnPathResult(PathOps.Absolute(receiver.Value, true, context, span), receiver.Value, context.MemoryGovernor, span)),
                    "absolute" when !path.IsPure => BoundCallable.CreateNoArguments(
                        path,
                        "Path.absolute",
                        static (receiver, span, context) => OwnPathResult(PathOps.Absolute(receiver.Value, false, context, span), receiver.Value, context.MemoryGovernor, span)),
                    "relative_to" => BoundCallable.Create(
                        (arguments, span, context) => RelativePath(path, arguments[0], span, context, false, true).GetAwaiter().GetResult(),
                        PathRelativeToSignature,
                        (arguments, span, context) => RelativePath(path, arguments[0], span, context, true, true)),
                    "with_suffix" => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var suffix))
                        {
                            throw new LythonRuntimeException("TypeError", "Path.with_suffix(suffix) expects one string argument.", span);
                        }

                        try
                        {
                            return OwnPathResult(PathOps.WithSuffix(path.Value, suffix, context, span), path.Value, context.MemoryGovernor, span, path.IsPure);
                        }
                        catch (InvalidOperationException ex)
                        {
                            throw new LythonRuntimeException("ValueError", ex.Message, span);
                        }
                    }, PathWithSuffixSignature),
                    "with_name" => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var name))
                        {
                            throw new LythonRuntimeException("TypeError", "Path.with_name(name) expects one string argument.", span);
                        }

                        try
                        {
                            return OwnPathResult(PathOps.WithName(path.Value, name, context, span), path.Value, context.MemoryGovernor, span, path.IsPure);
                        }
                        catch (InvalidOperationException ex)
                        {
                            throw new LythonRuntimeException("ValueError", ex.Message, span);
                        }
                    }, PathWithNameSignature),
                    "with_stem" => BoundCallable.Create((arguments, span, context) =>
                    {
                        if (arguments.Length != 1 || !PyStringOps.TryAsString(arguments[0], out var stem))
                        {
                            throw new LythonRuntimeException("TypeError", "Path.with_stem(stem) expects one string argument.", span);
                        }

                        try
                        {
                            return OwnPathResult(PathOps.WithStem(path.Value, stem, context, span), path.Value, context.MemoryGovernor, span, path.IsPure);
                        }
                        catch (InvalidOperationException ex)
                        {
                            throw new LythonRuntimeException("ValueError", ex.Message, span);
                        }
                    }, PathWithStemSignature),
                    _ => MissingMemberValue.Instance,
                };

                return !ReferenceEquals(value, MissingMemberValue.Instance);
            }
        }
    }
}

using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal static partial class PathOps
{
    internal static int RootLength(ReadOnlySpan<byte> bytes)
        => bytes.IsEmpty || bytes[0] != (byte)'/' ? 0
            : bytes.Length >= 2 && bytes[1] == (byte)'/' && (bytes.Length == 2 || bytes[2] != (byte)'/') ? 2 : 1;

    internal static MemoryGovernor.TemporaryMemoryReservation ReserveWorking(PyString first,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span, PyString? second = null)
    {
        // Covers decoded text, native component metadata and overlapping native
        // replacements. Pure UTF-8 operations below do not require this scratch.
        long length = first.Utf8Bytes.Length + (long)(second?.Utf8Bytes.Length ?? 0);
        var segments = 2L;
        Count(first); if (second is not null) Count(second);
        return context.MemoryGovernor.ReserveTemporary(checked(256L + 12L * length + 96L * segments), span);
        void Count(PyString path)
        {
            var bytes = path.Utf8Bytes.Span;
            for (var i = 0; i < bytes.Length; i++)
            { if ((i & 4095) == 0) context.CheckExecution(span); if (bytes[i] == (byte)'/') segments++; }
        }
    }

    internal static PyString NormalizeLexical(PyString value, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        if (Canonical(value, context, span)) return value;
        var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
        try { AppendNormalized(builder, value, context, span); return Finish(builder); }
        finally { builder.Release(); }
    }

    private static bool Canonical(PyString value, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var bytes = value.Utf8Bytes.Span;
        if (bytes.IsEmpty) return false;
        if (bytes.SequenceEqual("."u8) || bytes.SequenceEqual("/"u8) || bytes.SequenceEqual("//"u8)) return true;
        var start = RootLength(bytes);
        if (bytes[^1] == (byte)'/') return false;
        for (var i = start; i <= bytes.Length; i++)
        {
            if ((i & 4095) == 0) context.CheckExecution(span);
            if (i != bytes.Length && bytes[i] != (byte)'/') continue;
            if (i == start || bytes.Slice(start, i - start).SequenceEqual("."u8)) return false;
            start = i + 1;
        }
        return true;
    }

    internal static void AppendNormalized(GovernedByteBuilder builder, PyString value,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span, bool resolveParents = false)
    {
        var bytes = value.Utf8Bytes.Span;
        var root = RootLength(bytes);
        if (root != 0) { builder.Truncate(0); builder.Append(root == 2 ? "//"u8 : "/"u8); }
        var start = root;
        for (var i = start; i <= bytes.Length; i++)
        {
            if ((i & 4095) == 0) context.CheckExecution(span);
            if (i != bytes.Length && bytes[i] != (byte)'/') continue;
            var part = bytes.Slice(start, i - start); start = i + 1;
            if (part.IsEmpty || part.SequenceEqual("."u8)) continue;
            var current = builder.WrittenSpan;
            var currentRoot = RootLength(current);
            if (resolveParents && part.SequenceEqual(".."u8))
            {
                var slash = current.LastIndexOf((byte)'/');
                var tail = current[(slash + 1)..];
                if (current.Length > currentRoot && !tail.SequenceEqual(".."u8))
                { builder.Truncate(Math.Max(currentRoot, slash)); continue; }
                if (currentRoot != 0) continue;
            }
            if (builder.Length > currentRoot) builder.Append((byte)'/');
            Append(builder, part, context, span);
        }
    }

    internal static void Append(GovernedByteBuilder builder, ReadOnlySpan<byte> bytes,
        LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        for (var i = 0; i < bytes.Length;)
        {
            context.CheckExecution(span); var count = Math.Min(4096, bytes.Length - i);
            builder.Append(bytes.Slice(i, count)); i += count;
        }
    }

    internal static PyString Finish(GovernedByteBuilder builder)
        => builder.Length == 0 ? PyStringOps.DotLiteral : builder.ToPyStringAndRelease();

    internal static PyString Join(PyString left, PyString right, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
        try { AppendNormalized(builder, left, context, span); AppendNormalized(builder, right, context, span); return Finish(builder); }
        finally { builder.Release(); }
    }

    internal static PyString Absolute(PyString value, bool resolve, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
        try
        {
            if (RootLength(value.Utf8Bytes.Span) == 0)
                AppendNormalized(builder, PyString.FromString(context.Host.Cwd, context.MemoryGovernor, span), context, span, resolve);
            AppendNormalized(builder, value, context, span, resolve); return Finish(builder);
        }
        finally { builder.Release(); }
    }

    internal static PyString Parent(PyString value, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var bytes = value.Utf8Bytes.Span; var root = RootLength(bytes);
        if (bytes.Length == root) return value;
        var slash = bytes.LastIndexOf((byte)'/');
        var end = Math.Max(root, slash);
        return end == 0 ? PyStringOps.DotLiteral : PyString.CopyFromUtf8(bytes[..end], context.MemoryGovernor, span);
    }

    internal static ReadOnlyMemory<byte> NameBytes(PyString value)
    {
        var bytes = value.Utf8Bytes;
        return bytes.Span.SequenceEqual("."u8) ? ReadOnlyMemory<byte>.Empty : bytes[(bytes.Span.LastIndexOf((byte)'/') + 1)..];
    }

    internal static ReadOnlyMemory<byte> SuffixBytes(PyString value)
    {
        var bytes = NameBytes(value); var dot = bytes.Span.LastIndexOf((byte)'.');
        return dot <= 0 || dot == bytes.Length - 1 ? ReadOnlyMemory<byte>.Empty : bytes[dot..];
    }

    internal static PyString Name(PyString value, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        => PyString.CopyFromUtf8(NameBytes(value).Span, context.MemoryGovernor, span);
    internal static PyString Suffix(PyString value, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
        => PyString.CopyFromUtf8(SuffixBytes(value).Span, context.MemoryGovernor, span);
    internal static PyString Stem(PyString value, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var name = NameBytes(value); var suffix = SuffixBytes(value);
        return PyString.CopyFromUtf8(name.Span[..(name.Length - suffix.Length)], context.MemoryGovernor, span);
    }

    internal static PyString WithName(PyString value, PyString name, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var bytes = name.Utf8Bytes.Span;
        if (NameBytes(value).IsEmpty || bytes.IsEmpty || bytes.SequenceEqual("."u8) || bytes.Contains((byte)'/'))
            throw RuntimeErrors.Value("Invalid name.", span);
        var original = value.Utf8Bytes.Span; var prefix = original.LastIndexOf((byte)'/') + 1;
        var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
        try { Append(builder, original[..prefix], context, span); Append(builder, bytes, context, span); return builder.ToPyStringAndRelease(); }
        finally { builder.Release(); }
    }

    internal static PyString WithSuffix(PyString value, PyString suffix, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var bytes = suffix.Utf8Bytes.Span;
        if (NameBytes(value).IsEmpty || !bytes.IsEmpty && (bytes[0] != (byte)'.' || bytes.Length == 1 || bytes.Contains((byte)'/')))
            throw RuntimeErrors.Value("Invalid suffix.", span);
        var original = value.Utf8Bytes.Span; var end = original.Length - SuffixBytes(value).Length;
        var builder = new GovernedByteBuilder(context.MemoryGovernor, span);
        try { Append(builder, original[..end], context, span); Append(builder, bytes, context, span); return builder.ToPyStringAndRelease(); }
        finally { builder.Release(); }
    }

    internal static PyString WithStem(PyString value, PyString stem, LythonRuntime.ExecutionContext context, LythonSourceSpan span)
    {
        var builder = new GovernedByteBuilder(context.MemoryGovernor,span);
        try
        {
            Append(builder,stem.Utf8Bytes.Span,context,span);
            Append(builder,SuffixBytes(value).Span,context,span);
            return WithName(value,builder.ToPyStringAndRelease(),context,span);
        }
        finally { builder.Release(); }
    }

    internal static bool TryRelative(PyString value, PyString parent, out int start)
    {
        var left = value.Utf8Bytes.Span; var right = parent.Utf8Bytes.Span;
        start = 0;
        if (RootLength(left) == 0 && right.SequenceEqual("."u8)) return true;
        if (!left.StartsWith(right) || RootLength(left) != RootLength(right)) return false;
        start = right.Length;
        if (left.Length == right.Length) return true;
        if (right[^1] == (byte)'/') return true;
        if (left[start] != (byte)'/') return false;
        start++; return true;
    }
}

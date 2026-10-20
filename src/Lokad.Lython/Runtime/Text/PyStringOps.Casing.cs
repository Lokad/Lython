using System.Numerics;
using System.Text;
using System.Text.Unicode;
using System.Globalization;

namespace Lokad.Lython.Runtime.Text;

internal static partial class PyStringOps
{
    public static bool IsLower(PyString value, LythonRuntime.ExecutionContext? context = null, LythonSourceSpan? span = null)
        => IsCase(value, RuneCase.Lower, context, span);

    public static bool IsUpper(PyString value, LythonRuntime.ExecutionContext? context = null, LythonSourceSpan? span = null)
        => IsCase(value, RuneCase.Upper, context, span);

    private static bool IsCase(PyString value, RuneCase expected, LythonRuntime.ExecutionContext? context, LythonSourceSpan? span)
    {
        var foundCased = false;
        var source = value.Utf8Bytes.Span;
        context ??= PyStructuralGuard.AmbientContext;
        span ??= PyStructuralGuard.AmbientSpan;
        for (var offset = 0; offset < source.Length;)
        {
            context?.CheckExecution(span);
            // Decode complete scalars across the small chunk boundary. No
            // per-scalar work counter is needed for this nonallocating scan.
            var end = offset + Math.Min(64, source.Length - offset);
            while (offset < end)
            {
                Rune.DecodeFromUtf8(source[offset..], out var rune, out var consumed);
                var kind = GetRuneCase(rune);
                if (kind != RuneCase.Uncased)
                {
                    if (kind != expected)
                    {
                        return false;
                    }

                    foundCased = true;
                }

                offset += consumed;
            }
        }

        return foundCased;
    }

    public static PyString Lower(PyString value, LythonRuntime.ExecutionContext? context = null, LythonSourceSpan? span = null)
        => MapCase(value, CaseMapping.Lower, context, span);

    public static PyString Upper(PyString value, LythonRuntime.ExecutionContext? context = null, LythonSourceSpan? span = null)
        => MapCase(value, CaseMapping.Upper, context, span);

    public static bool IsAlpha(PyString value, LythonRuntime.ExecutionContext? context = null, LythonSourceSpan? span = null)
    {
        return CheckAllRunes(value, static rune => Rune.IsLetter(rune), context, span);
    }

    public static bool IsAlnum(PyString value, LythonRuntime.ExecutionContext? context = null, LythonSourceSpan? span = null)
    {
        return CheckAllRunes(value, static rune =>
        {
            var category = Rune.GetUnicodeCategory(rune);
            return Rune.IsLetter(rune) || category is UnicodeCategory.DecimalDigitNumber
                or UnicodeCategory.LetterNumber
                or UnicodeCategory.OtherNumber;
        }, context, span);
    }

    public static bool IsSpace(PyString value, LythonRuntime.ExecutionContext? context = null, LythonSourceSpan? span = null)
    {
        return CheckAllRunes(value, static rune => IsPythonWhitespace(rune), context, span);
    }

    public static PyString Capitalize(PyString value, LythonRuntime.ExecutionContext? context = null, LythonSourceSpan? span = null)
    {
        context ??= PyStructuralGuard.AmbientContext;
        span ??= PyStructuralGuard.AmbientSpan ?? value.AllocationSpan;
        var source = value.Utf8Bytes.Span;
        if (source.Length == 0)
        {
            return PyString.Empty;
        }

        var builder = CreateBuilder(context?.MemoryGovernor ?? value.OwnerMemoryGovernor, span, source.Length);
        try
        {
            var precedingCased = false;
            for (var offset = 0; offset < source.Length;)
            {
                context?.CheckExecution(span);
                Rune.DecodeFromUtf8(source[offset..], out var rune, out var consumed);
                AppendMappedRune(builder, rune, offset == 0 ? CaseMapping.Title : CaseMapping.Lower,
                    precedingCased, source[(offset + consumed)..], context, span);
                if (!SigmaPropertyContains(rune.Value, SigmaIgnorableRanges))
                    precedingCased = SigmaPropertyContains(rune.Value, SigmaCasedRanges);
                offset += consumed;
            }
            return builder.ToPyString();
        }
        finally { builder.Release(); }
    }

    public static PyString SwapCase(PyString value, LythonRuntime.ExecutionContext? context = null, LythonSourceSpan? span = null)
    {
        context ??= PyStructuralGuard.AmbientContext;
        span ??= PyStructuralGuard.AmbientSpan ?? value.AllocationSpan;
        var builder = CreateBuilder(context?.MemoryGovernor ?? value.OwnerMemoryGovernor, span, value.Utf8Bytes.Length);
        try
        {
            var source = value.Utf8Bytes.Span;
            var precedingCased = false;
            for (var offset = 0; offset < source.Length;)
            {
                context?.CheckExecution(span);
                Rune.DecodeFromUtf8(source[offset..], out var rune, out var consumed);
                var kind = GetRuneCase(rune);
                if (kind is RuneCase.Upper or RuneCase.Lower)
                    AppendMappedRune(builder, rune, kind == RuneCase.Upper ? CaseMapping.Lower : CaseMapping.Upper,
                        precedingCased, source[(offset + consumed)..], context, span);
                else builder.Append(rune);
                if (!SigmaPropertyContains(rune.Value, SigmaIgnorableRanges))
                    precedingCased = SigmaPropertyContains(rune.Value, SigmaCasedRanges);
                offset += consumed;
            }
            return builder.ToPyString();
        }
        finally { builder.Release(); }
    }

    public static PyString Title(PyString value, LythonRuntime.ExecutionContext? context = null, LythonSourceSpan? span = null)
    {
        context ??= PyStructuralGuard.AmbientContext;
        span ??= PyStructuralGuard.AmbientSpan ?? value.AllocationSpan;
        var builder = CreateBuilder(context?.MemoryGovernor ?? value.OwnerMemoryGovernor, span, value.Utf8Bytes.Length);
        try
        {
            var previousWasCased = false;
            var precedingCased = false;
            var source = value.Utf8Bytes.Span;
            for (var offset = 0; offset < source.Length;)
            {
                context?.CheckExecution(span);
                Rune.DecodeFromUtf8(source[offset..], out var rune, out var consumed);
                AppendMappedRune(builder, rune, previousWasCased ? CaseMapping.Lower : CaseMapping.Title,
                    precedingCased, source[(offset + consumed)..], context, span);
                previousWasCased = SigmaPropertyContains(rune.Value, SigmaCasedRanges);
                if (!SigmaPropertyContains(rune.Value, SigmaIgnorableRanges))
                    precedingCased = previousWasCased;
                offset += consumed;
            }
            return builder.ToPyString();
        }
        finally { builder.Release(); }
    }
}

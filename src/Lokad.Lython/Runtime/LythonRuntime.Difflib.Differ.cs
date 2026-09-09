using System.Net;
using System.Numerics;
using System.Text;
using Lokad.Lython.Runtime.Numbers;
using Lokad.Lython.Runtime.Text;

namespace Lokad.Lython.Runtime;

internal sealed partial class LythonRuntime
{
    internal sealed class DifflibDifferObject
    {
        // Must remain a power of two: chunk checks below use it as a bit mask.
        private const int BudgetCheckInterval = 64;
        private readonly ICallable? _linejunk;
        private readonly ICallable? _charjunk;

        public DifflibDifferObject(ICallable? linejunk, ICallable? charjunk)
        {
            _linejunk = linejunk;
            _charjunk = charjunk;
        }

        public bool TryGetMember(string name, [MaybeNullWhen(false)] out object value)
        {
            value = name switch
            {
                "linejunk" => _linejunk is null ? PyNone.Instance : _linejunk,
                "charjunk" => _charjunk is null ? PyNone.Instance : _charjunk,
                "compare" => BoundCallable.Create((arguments, span, context) =>
                {
                    if (arguments.Length != 2)
                    {
                        throw new LythonRuntimeException("TypeError", "Differ.compare(a, b) expects two arguments.", span);
                    }

                    var a = DifflibModule.RequireStringSequence(arguments[0], "Differ.compare(a, b)", span, context);
                    var b = DifflibModule.RequireStringSequence(arguments[1], "Differ.compare(a, b)", span, context);
                    return new PyList(CompareLines(a, b, span, context), context.MemoryGovernor, span);
                }, "Differ.compare", ["a", "b"]),
                _ => MissingMemberValue.Instance,
            };

            return !ReferenceEquals(value, MissingMemberValue.Instance);
        }

        internal IEnumerable<object> CompareLines(IReadOnlyList<PyString> a, IReadOnlyList<PyString> b, LythonSourceSpan span, ExecutionContext context)
        {
            var matcher = new DifflibSequenceMatcherObject(_linejunk, new PyList(a.Cast<object>(), context.MemoryGovernor, span), new PyList(b.Cast<object>(), context.MemoryGovernor, span), autojunk: true, span, context);
            foreach (var opcode in matcher.BuildOpcodes(span, context))
            {
                foreach (var line in opcode.Tag switch
                {
                    DiffTag.Replace => FancyReplace(a, opcode.I1, opcode.I2, b, opcode.J1, opcode.J2, span, context),
                    DiffTag.Delete => Dump("-", a, opcode.I1, opcode.I2, context.MemoryGovernor, span),
                    DiffTag.Insert => Dump("+", b, opcode.J1, opcode.J2, context.MemoryGovernor, span),
                    DiffTag.Equal => Dump(" ", a, opcode.I1, opcode.I2, context.MemoryGovernor, span),
                    _ => throw new InvalidOperationException($"Unknown diff opcode: {opcode.Tag}")
                })
                {
                    yield return line;
                }
            }
        }

        private static IEnumerable<object> Dump(string tag, IReadOnlyList<PyString> lines, int lo, int hi, MemoryGovernor governor, LythonSourceSpan span)
        {
            for (var i = lo; i < hi; i++)
            {
                yield return DifflibModule.Prefix(tag + " ", lines[i], governor, span);
            }
        }

        private IEnumerable<object> PlainReplace(IReadOnlyList<PyString> a, int alo, int ahi, IReadOnlyList<PyString> b, int blo, int bhi, MemoryGovernor governor, LythonSourceSpan span)
        {
            if (bhi - blo < ahi - alo)
            {
                foreach (var line in Dump("+", b, blo, bhi, governor, span))
                {
                    yield return line;
                }

                foreach (var line in Dump("-", a, alo, ahi, governor, span))
                {
                    yield return line;
                }
            }
            else
            {
                foreach (var line in Dump("-", a, alo, ahi, governor, span))
                {
                    yield return line;
                }

                foreach (var line in Dump("+", b, blo, bhi, governor, span))
                {
                    yield return line;
                }
            }
        }

        private IEnumerable<object> FancyReplace(IReadOnlyList<PyString> a, int alo, int ahi, IReadOnlyList<PyString> b, int blo, int bhi, LythonSourceSpan span, ExecutionContext context)
        {
            var bestRatio = 0.74;
            const double cutoff = 0.75;
            var cruncher = new DifflibSequenceMatcherObject(_charjunk, PyString.Empty, PyString.Empty, autojunk: true, span, context);
            int? equalI = null;
            int? equalJ = null;
            var bestI = alo;
            var bestJ = blo;

            var gridWork = 0;
            for (var j = blo; j < bhi; j++)
            {
                var bj = b[j];
                cruncher.SetSeq2(bj, span, context);
                for (var i = alo; i < ahi; i++)
                {
                    if ((++gridWork & (BudgetCheckInterval - 1)) == 0)
                    {
                        context.CheckExecutionBudget(span);
                    }

                    var ai = a[i];
                    if (AreEqual(ai, bj))
                    {
                        equalI ??= i;
                        equalJ ??= j;
                        continue;
                    }

                    cruncher.SetSeq1(ai, span, context);
                    if (cruncher.RealQuickRatio() > bestRatio &&
                        cruncher.QuickRatio(span, context) > bestRatio &&
                        cruncher.Ratio(span, context) > bestRatio)
                    {
                        bestRatio = cruncher.Ratio(span, context);
                        bestI = i;
                        bestJ = j;
                    }
                }
            }

            if (bestRatio < cutoff)
            {
                if (equalI is null || equalJ is null)
                {
                    foreach (var line in PlainReplace(a, alo, ahi, b, blo, bhi, context.MemoryGovernor, span))
                    {
                        yield return line;
                    }

                    yield break;
                }

                bestI = equalI.Value;
                bestJ = equalJ.Value;
            }
            else
            {
                equalI = null;
            }

            foreach (var line in FancyHelper(a, alo, bestI, b, blo, bestJ, span, context))
            {
                yield return line;
            }

            var aLine = a[bestI];
            var bLine = b[bestJ];
            if (equalI is null)
            {
                var aTags = new StringBuilder();
                var bTags = new StringBuilder();
                cruncher.SetSeqs(aLine, bLine, span, context);
                foreach (var opcode in cruncher.BuildOpcodes(span, context))
                {
                    var leftLength = opcode.I2 - opcode.I1;
                    var rightLength = opcode.J2 - opcode.J1;
                    switch (opcode.Tag)
                    {
                        case DiffTag.Replace:
                            aTags.Append('^', leftLength);
                            bTags.Append('^', rightLength);
                            break;
                        case DiffTag.Delete:
                            aTags.Append('-', leftLength);
                            break;
                        case DiffTag.Insert:
                            bTags.Append('+', rightLength);
                            break;
                        case DiffTag.Equal:
                            aTags.Append(' ', leftLength);
                            bTags.Append(' ', rightLength);
                            break;
                    }
                }

                foreach (var line in QFormat(aLine.AsString(), bLine.AsString(), aTags.ToString(), bTags.ToString(), context.MemoryGovernor, span))
                {
                    yield return line;
                }
            }
            else
            {
                yield return DifflibModule.Prefix("  ", aLine, context.MemoryGovernor, span);
            }

            foreach (var line in FancyHelper(a, bestI + 1, ahi, b, bestJ + 1, bhi, span, context))
            {
                yield return line;
            }
        }

        private IEnumerable<object> FancyHelper(IReadOnlyList<PyString> a, int alo, int ahi, IReadOnlyList<PyString> b, int blo, int bhi, LythonSourceSpan span, ExecutionContext context)
        {
            if (alo < ahi)
            {
                if (blo < bhi)
                {
                    return FancyReplace(a, alo, ahi, b, blo, bhi, span, context);
                }

                return Dump("-", a, alo, ahi, context.MemoryGovernor, span);
            }

            return blo < bhi ? Dump("+", b, blo, bhi, context.MemoryGovernor, span) : [];
        }

        private static IEnumerable<object> QFormat(string aLine, string bLine, string aTags, string bTags, MemoryGovernor governor, LythonSourceSpan span)
        {
            aTags = KeepOriginalWhitespace(aLine, aTags).TrimEnd();
            bTags = KeepOriginalWhitespace(bLine, bTags).TrimEnd();

            yield return PyString.FromString("- " + aLine, governor, span);
            if (aTags.Length != 0)
            {
                yield return PyString.FromString("? " + aTags + "\n", governor, span);
            }

            yield return PyString.FromString("+ " + bLine, governor, span);
            if (bTags.Length != 0)
            {
                yield return PyString.FromString("? " + bTags + "\n", governor, span);
            }
        }

        private static string KeepOriginalWhitespace(string source, string tags)
        {
            var builder = new StringBuilder(Math.Min(source.Length, tags.Length));
            var count = Math.Min(source.Length, tags.Length);
            for (var i = 0; i < count; i++)
            {
                builder.Append(tags[i] == ' ' && char.IsWhiteSpace(source[i]) ? source[i] : tags[i]);
            }

            return builder.ToString();
        }
    }

}

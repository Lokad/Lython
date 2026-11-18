# Shared numeric compiler facts

The initial R532 change gives lowered floating and imaginary literals a parsed
value, as lowered integers already have. Emission and lowered execution read
that value directly. The change removes reparsing from the lowered expression
dispatcher and preserves arithmetic and Python value creation. Parsing still
occurs after syntax and frontend validation. The change is isolated in the
development worktree pending the rest of the numeric facts migration.

A nonincremental Release probe build succeeded on .NET 11 and .NET 10. For
each target and through both execution APIs, all 26 public probe results match
the qualified reference 2c6b304b exactly. The cases cover malformed numeric
tokens, separators and radix, large integers, float overflow and underflow,
signed zero, imaginary values, matches, postponed annotations, class bodies,
defaults, numeric keys, and regex flag folding. The comparison includes output,
structured diagnostics and failures, and accounted execution and projection peaks.
The focused .NET 11 Release run passed 224 existing public regressions for the
parser, floating and complex values, annotations, cancellation, class methods
and patterns.

The current abstract-value representation carries numeric spelling and re-parses
it in multiple consumers. Some spelling currently affects analysis. For
example, a known out-of-range decimal subscript with underscores reports LA3117
at compile time, while its hexadecimal equivalent reaches runtime and reports
IndexError. The next migration must preserve those diagnostic boundaries
unless there is a separately justified change. The cases and reference messages
are preserved for that work. Whole-script and allocation measurements remain pending.

[Probe results and cases](probes.tar.xz) · [Comparison](comparison.json) ·
[Isolated patch](initial-change.patch.gz)

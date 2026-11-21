# Map-based text report and Unicode casing audit

November 21, 2026 UTC. The Windows .NET 11 RC3 worker uses frozen
`837fae29b`, with the same production source as `d09acaf69`.
The map-based text report processes 1,024 lines and 35,840 bytes
through the comparison host. All 32 frozen image files and the
CPython output and file goldens verified. The worker completed 768
verified reports around the six-second sample window and joined.

The EventPipe Speedscope export records managed thread residency.
Within 4.815 seconds under the guest executable root, 76.40% has
byte file open as the nearest resolved Lython method and 19.89%
has `MapCaseRune`. These are inclusive of deeper unmanaged work
and waits. The local disk trace provides no estimate of exclusive
CPU cost or of the quiet VM's warm gap.

A separate CPython 3.13.2 audit compares all non-ASCII Unicode
scalars on the same Windows core. It finds 1 upper, 47 title and
1 swapcase difference. They include dotless I and Georgian titlecase.
The audit motivates testing pinned UTF-8 mapping data in place of
platform Rune casing, even if subsequent throughput is inconclusive.

[Trace attribution](summary.json) · [Baseline differences](windows-differences.txt)
· [Raw traces, source, verification and audit procedure](trace-and-checks.tar.xz).

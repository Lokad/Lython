# B1826 Collect deferred binding proof during operand layout

The trial on `c574fdfc2` removes the compiler's separate
scan of the original instruction list. Operand layout checks local reads
before they can be forwarded and preserves the terminal assignment read
when forwarding replaces its store and load. The existing linearization
still selects candidate sites whose stores survive. Runtime staging and
accounting use the existing layout.

Both images have identical instruction tapes, deferred sites, frame
capacities and capture metadata across 14 fixtures. The ordinary factory
has 93 deferred sites in its 933-instruction build function on both
images. Admission order, diagnostics and first refusals match at all
104 tested allowance levels. The compile owner returns its allowance and
the cancellation check confirms the same rollback. Windows .NET 11 and
Ubuntu WSL .NET 10 each pass 187 public and 406 white-box focused Release
checks on the final source, which also matches the measured candidate tapes and
admission traces. All four complete scripts match independent CPython 3.13.2
stdout and file goldens. Execution allocation matches on both factories
and admitted peaks match across the catalog in both APIs.

The local Ubuntu WSL run uses .NET 11 RC3 and one logical CPU. Two workers
per source warm for five seconds and contribute seven rotated batches.
Ranges span all four pairs of worker medians. Compile-only includes
compiled-script disposal. Through-result adds complete execution, result
and file verification, and cleanup. Process CPU also covers fixture reset
and request framing. Process startup sits outside both lanes.

| Complete workload | Phase | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Ordinary definitions | Compile only | −0.12% to +0.96% | −0.44% to +4.60% |
| Escaping rules | Through result | −39.71% to −23.43% | −23.15% to −15.58% |
| Protected file search control | Compile only | −1.31% to +6.85% | −0.61% to +7.95% |
| Ordinary definitions | Through result | −5.98% to +2.54% | −9.03% to −0.64% |
| CSV invoice control | Compile only | −1.66% to +5.65% | −1.37% to +0.42% |

Keep the compiler simplification. Ordinary compile-only timing spans zero.
Ordinary through-result CPU falls 0.64 to 9.03 percent, with mixed elapsed
and 7.48 percent CPU spread across the reference workers. The escaping
rules control bypasses the removed pass, and its through-result CPU falls
15.58 to 23.15 percent in the same collection. The timing leaves the
source effect unresolved. The final source also removes one unused
import and documents the forwarded read.

All 140 timed requests verify. All workers join and the persistent
archive verifies. This compares the candidate against its Lython
reference. The earlier B1822 timing concern remains open against its
own reference.

[Tape and admission summary](tape-summary.json) · [Frozen images](identity.json) ·
[Medians and spreads](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Observations](observations.tar.xz) · [Archive receipt](archive-receipt.json) ·
[Exact patch and procedures](procedures.tar.xz).

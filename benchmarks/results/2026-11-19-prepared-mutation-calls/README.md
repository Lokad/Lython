# B1152 and B1154 prepare mutation candidates during syntax validation

Compared `0d74b5b18` with prototype `2c60914d1` on November 19, 2026 UTC.
The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3. Both complete
32-file images matched six CPython goldens.

The syntax validator records the possible mutating call sites for each
statement. Binding analysis uses that compilation-only map instead of
recursively visiting the statement's expressions again. It still checks
receiver bindings and call shape when applying effects, visits the
candidate calls in the original order, and skips lambda bodies and
defaults in the same way as the prior mutation walk.

| Workload | Boundary | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | --- | ---: | ---: | ---: |
| Generated validator | Compilation | −1.00% to +1.35% | −1.59% to +1.22% | 1.72 / 0.64% |
| Configuration manifest | Compilation | −5.50% to +0.46% | −4.63% to −0.32% | 3.62 / 2.59% |
| Generated validator | Compile-to-result | −1.14% to +6.12% | +0.15% to +7.48% | 2.62 / 4.60% |
| Annotated rules | Compilation | −7.63% to −4.02% | −6.33% to −3.60% | 3.01 / 0.88% |
| CSV invoice | Warm control | −3.43% to −1.22% | −5.77% to −1.33% | 2.22 / 0.06% |
| Ordinary rules | Warm control | −2.40% to +0.88% | −1.92% to +3.12% | 2.84 / 0.51% |
| B1154 annotated rules | Compilation | −6.47% to +5.03% | −6.95% to +4.69% | 1.92 / 10.18% |
| B1154 generated validator | Compile-to-result | −2.74% to +1.22% | −5.05% to −0.76% | 2.70 / 1.33% |

Ranges cover the four cross-source pairs of process medians. Annotated
compilation takes less time and CPU. Validator compilation spans zero.
Validator compile-to-result uses more process CPU, with moving candidate
medians. The source change is in the compiler. The warm invoice difference
does not establish an execution improvement caused by this change.

The unchanged-image repeat reverses worker order and covers the two
affected lanes. Validator process CPU is lower in the repeat, and elapsed
still spans zero. The original CPU increase did not recur. Annotated
compilation has 10.18% candidate worker spread in the repeat. Its speedup
remains uncertain. Retained at `ad8bb0c4e` for eliminating the expression
rewalk and reducing compilation allocations.

Local compilation allocation falls 0.88% for the validator, 1.93% for
the manifest and 3.47% for annotated rules. Warm allocations are unchanged
except for 660 additional bytes on the report. Accounted warm peaks remain
equal and borrowed memory balances return to baseline. Windows .NET 11
and Ubuntu .NET 10 each passed 730 public and 15 white-box Release checks.

The first run completed 120 timed batches in 325.10 seconds. The repeat
completed 40 in 112.31 seconds. Quiet gates passed, workers joined,
fixtures were removed and the VM verified both persistent archives under
their leases. The first staging retired only the duplicate cores already
preserved in prior archives. Warm includes mediated I/O, verification and
previous-result cleanup. Compilation includes compiled-owner disposal.
Compile-to-result measures both. Process CPU covers the wider request.

[Measurements](summary.json) · [Raw observations](observations.tgz) ·
[Validation and procedures](checks-and-procedures.tar.xz) ·
[Lease receipt](archive-receipt.json) ·
[Repeat measurements](confirmation/summary.json) ·
[Repeat observations](confirmation/observations.tgz).

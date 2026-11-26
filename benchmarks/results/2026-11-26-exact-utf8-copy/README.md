# B1778 Construct exact UTF-8 copies directly

The candidate against `e222c5221` constructs an independent exact-size
string in its final inline or array storage. It skips the writable-string
state and completion step while preserving advance admission, cancellation
checks and rollback. The separately instrumented core records 4,103 inline
copies per JSON document run and 4,101 per JSON Lines report. The Unicode
report and ordinary definitions take nine and four inline copies.
All four workloads have zero array-copy hits. [Source patch](candidate.patch) ·
[Routes](routes.json)

| Warm script | Lane | Elapsed change | Wider process CPU change |
| --- | --- | ---: | ---: |
| Ordinary definitions | Sync | −5.70% to +0.62% | −3.85% to +1.45% |
| JSON service totals | Sync | −3.52% to +23.20% | −12.71% to +12.77% |
| Unicode report | Sync | +2.64% to +8.15% | −1.37% to 0.00% |
| JSON service totals | Async | −16.28% to +8.06% | −17.98% to −9.91% |
| JSON Lines helper report | Sync | −6.17% to +3.99% | −4.79% to +3.60% |

The November 26, 2026 local WSL diagnostic uses Ubuntu 24.04 and .NET
11 RC3, pinned to one logical CPU. Two workers per source warm for four
seconds and supply seven rotated batches targeting 350 ms each. Ranges
span the four pairings of worker medians. Negative values mean less
time. Warm elapsed includes mediated RAM-backed I/O, output verification
and previous-result disposal. Wider CPU also includes reset and framing.
Startup, compilation and final cleanup sit outside these clocks. All
140 batches complete, all workers join and the verified archive survives
fixture removal. [Medians and spreads](summary.json) ·
[Observations](observations.tar.xz) · [Frozen images](identity.json) ·
[Archive receipt](archive-receipt.json)

Keep the existing copy routine. The local async CPU decrease merits
recording, and the same route has mixed sync measurements. Sync document
reference CPU spread reaches 20.20%. JSON Lines crosses zero and the
low-exposure Unicode control runs slower on elapsed. These observations
do not establish a whole-script speedup. CLR allocation changes by at
most 222 B per run, peak admission matches and every cleanup restores its
allowance. [Allocation and admission](allocation-summary.json)

Both 32-file images match independent CPython 3.13 output and file
goldens on all four scripts. Windows .NET 11 passes 5,203 focused
Release tests. Ubuntu .NET 10 passes the same selection across the
initial run and a retry of the one probe CLI comparison. The initial
Ubuntu environment lacked the `python` executable name expected by the
probe. The retry supplies CPython 3.13.2 and rebuilds the probe for the
matching configuration. [Verification, tests and procedures](procedures.tar.xz)

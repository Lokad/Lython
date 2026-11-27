# B1825 Keep JSON key layouts across empty objects

An empty object has no reusable key layout. The candidate on
`9043062b4` retains the previous funded layout when
that object closes. A matching later record writes its own values against the
existing layout, avoiding repeated key admission and table construction.
The existing ordered-key checks and pair-hook restoration still apply.

The sparse document keeps all 2,048 data records and
adds 512 empty objects (80,291 B).
The sparse Lines script keeps 4,096 data records and
adds 1,024 empty objects alongside its existing blank
lines (113,664 B). Both scripts skip empty records and
retain their original output and file goldens. The unchanged inputs serve as
dense controls. CPython 3.13.2 output and file checks pass on both frozen
images for all four scripts. Both local Release targets pass 925 public and
238 white-box checks. The extended shared-key test fails at the newly covered
layout reuse on the reference in sync and async and passes on the candidate.

The Windows allocation diagnostic compares medians. Execution teardown restores
the borrowed allowance and compilation admission matches on every case.

| Complete workload | API | CLR allocation change | Admitted execution peak change |
| --- | --- | ---: | ---: |
| Dense document totals | sync | +0.02% | +0.00% |
| Dense JSON Lines helpers | sync | +0.01% | +0.00% |
| Sparse document totals | sync | −18.59% | −16.76% |
| Sparse JSON Lines helpers | sync | −20.79% | −18.59% |
| Dense document totals | async | +0.00% | +0.00% |
| Dense JSON Lines helpers | async | +0.00% | +0.00% |
| Sparse document totals | async | −18.57% | −16.76% |
| Sparse JSON Lines helpers | async | −20.77% | −18.59% |

The local Ubuntu WSL timing uses .NET 11 RC3 with two workers per source,
three seconds of warmup per worker and seven rotated timed batches per worker.
Ranges span the four pairings of worker medians. Warm elapsed includes mediated
I/O, output verification and preceding-result disposal. Process CPU also covers
fixture reset and request framing. Compilation, startup and final teardown
sit outside those intervals. Negative changes mean less time.

| Complete workload | Warm lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Sparse document totals | warm | −13.09% to −10.71% | −11.72% to −8.64% |
| Dense JSON Lines helpers | warm-async | −7.35% to +3.45% | −6.42% to +5.02% |
| Sparse JSON Lines helpers | warm-async | −8.23% to −5.35% | −8.47% to −4.76% |
| Dense document totals | warm | +2.45% to +29.92% | +2.49% to +22.95% |
| Sparse document totals | warm-async | −29.46% to −8.25% | −16.18% to +15.78% |

The repeat pins one logical CPU, warms for six seconds per worker and keeps
the original timing boundary and both source images.

| Complete workload, warm sync | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Dense document totals | −5.37% to +22.28% | −8.61% to +41.05% |
| Sparse document totals | −14.80% to −12.66% | −18.86% to −10.98% |

Keep the change. Sparse document warm sync improves on both local runs, with
the repeat using 10.98 to 18.86 percent less process CPU. Sparse Lines warm
async uses 4.76 to 8.47 percent less CPU in the first run. Sparse document
async CPU spans zero. The first dense document sync run is adverse, and
the pinned repeat spans zero with large same-source spreads. The dense
timing impact remains unresolved. The sparse allocation and admitted peak
reductions hold in both APIs and dense memory metrics remain effectively flat.

All 196 timed batches verify. The workers join, temporary fixtures are removed and
the persistent archive verifies. This comparison isolates the code change
locally. It supplies no current CPython timing ratio.

[Allocation and admission](allocation-summary.json) · [Medians and spreads](summary.json) ·
[Frozen images](identity.json) · [Observations](observations.tar.xz) ·
[Archive receipt](archive-receipt.json) · [Exact patches and procedures](procedures.tar.xz) ·
[Repeat medians and spreads](confirmation/summary.json) ·
[Repeat observations](confirmation/observations.tar.xz) ·
[Repeat archive receipt](confirmation/archive-receipt.json).

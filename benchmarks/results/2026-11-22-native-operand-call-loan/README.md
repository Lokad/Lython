# B1392 Reuse evaluated operands for native calls

The quiet Azure F4as_v7 compared frozen `207b9d567` and private
`cdb213d36` on November 22, 2026 UTC with Ubuntu 24.04.4 and .NET 11 RC3.
The reference contains the retained word-storage change. Both exact
32-file images use the same worker and PythonRe 0.4.0 adapter.

An eligible complete positional call in a framed caller runs the
existing native prefix with the argument slots already filled.
Its remaining slots fit in the caller's dead operand tail. The
candidate removes the separate inline workspace and argument copy
for that call without widening the caller. Suspended or deferred
work moves to the existing funded continuation before the borrow
ends. Both managed entry checks, cleanup and source spans remain.

| Complete script | Boundary | Elapsed change | Wider CPU change | Worker spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Word count with nonlocal width | Warm | −1.37 to +0.46% | −1.46 to +1.50% | 0.07 / 1.78% |
| Quote-heavy CSV invoice | Warm | +0.01 to +2.24% | 0.00 to +2.69% | 1.37 / 0.84% |
| Function-scoped positional helper | Warm | −0.97 to +1.04% | −0.26 to +0.49% | 1.27 / 0.75% |
| Mixed closure factory | Compile to result | +2.11 to +3.06% | +1.26 to +3.04% | 0.54 / 0.38% |
| Positional call control | Warm | −2.16 to +1.78% | −2.51 to +0.75% | 2.63 / 1.36% |

Two fresh processes per source warm for eight seconds and each complete
seven rotated batches. Ranges span all pairings of their medians. Warm
elapsed includes applicable mediated RAM-backed I/O, output verification
and prior-result cleanup. Compile to result adds compilation and compiled
owner disposal. Wider CPU includes reset and protocol. Process startup
and final teardown stay outside the clocks.

Keep this version private. The target's warm changes span zero and the
mixed closure factory takes longer with stable reference and candidate
workers. Allocation and admitted peaks match on all five rebased
workloads. The small adverse quote result compares against the
word-storage source and leaves the separate B1391 quote debt open.
[Word-storage confirmation](../2026-11-22-inline-cased-words-confirmed/README.md).

Windows .NET 11 and Ubuntu .NET 10 each pass 371 focused Release checks
on the same patch before rebasing, including refused native promotion,
async callbacks and descendant cleanup. Both rebased images match
CPython on all five workloads and fully return their allowance.
All 140 timed batches and both quiet machine gates pass. Workers join,
fixtures are removed and the archive verifies under the lease.

A separate managed route probe logged 3,072 entries into the borrowed
workspace on the 3,072-line report. The other four scripts logged
zero, including the slower mixed factory and quoted invoice. The probe
validates the route and outputs with separate instrumentation. It gives
no timing or attribution for the common-path regression.
[Route counts](routes.json) · [Probe source and build](route-procedures.tar.xz).

[Source and images](source.json) · [Timings](summary.json) ·
[Allocation](allocation-summary.json) · [Raw observations](observations.tgz) ·
[Tests and procedures](procedures.tar.xz) · [Archive receipt](archive-receipt.json).

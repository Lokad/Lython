# B1400 Store a singleton default in its tuple

The quiet Azure F4as_v7 compared frozen `70ea1083d` and `2cd7317e3`
on November 22, 2026 UTC with Ubuntu 24.04.4 and .NET 11 RC3.
The measured patch is retained at `6b4afc809`. The separate exact
tuple conversion fix at `c9d7d555c` is outside the measured pair.

A dynamic one-item default tuple stores its executable value slot
directly in the tuple. This removes the per-definition array. Calls
and the public `__defaults__` read share the same tuple-owned lazy
integer identity. The existing preflight, wide scalar funding and
cleanup apply before evaluating the default and through suspension.

| Complete workload | Boundary | Elapsed change | Wider CPU change | Worker spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Postponed nested factories | Warm | −8.63 to −2.09% | −9.03 to −1.68% | 3.02 / 4.02% |
| Postponed nested factories | Compile to result | −8.94 to +0.20% | −9.07 to −1.02% | 4.59 / 5.20% |
| Eager nested factories | Warm | −0.23 to +1.16% | −0.66 to +0.69% | 0.62 / 0.77% |
| Factories observing default tuples | Warm | −0.34 to +0.73% | −1.05 to +0.32% | 0.09 / 0.98% |
| Fully supplied helper | Warm | −0.09 to +0.80% | −0.19 to +1.86% | 0.50 / 0.39% |
| Quote-heavy CSV policy invoice | Warm | −1.39 to +3.70% | −2.22 to +3.45% | 1.88 / 3.22% |

Two fresh processes per source and boundary warm for eight seconds
and complete seven rotated batches each. Ranges span pairings of
process medians. Warm elapsed includes output checks, prior-result
disposal, and mediated RAM-backed I/O for the invoice. Time to
result includes compilation and compiled-owner disposal. Wider CPU
includes fixture reset and request handling. Startup and final
teardown use separate boundaries.

The postponed and default-observing factories each allocate 49,152
fewer bytes locally. The eager factory allocates 49,096 fewer bytes.
Requests and admitted peaks match and all owners refund. The
tuple-observer slowdown in the earlier deferred-publication design
does not reproduce. First-use elapsed spans zero and the current
quote comparison leaves the earlier quote regression open.
[Earlier design](../2026-11-22-single-positional-default/README.md) ·
[Quote regression](../2026-11-22-inline-cased-words-confirmed/README.md).

Windows .NET 11 and Ubuntu .NET 10 each pass 655 focused Release
checks for the measured patch. The new case covers distinct
per-definition tuple identity, stable value identity across calls
and iteration, hashing, slicing and retained old defaults. Both
frozen images match five script goldens. All 168 timed batches and
both quiet gates pass. Workers join and the archive verifies after
guest fixture cleanup. Staging verified canonical copies before
reclaiming old archive replicas.

An isolated initial test run needed a copied scenario fixture and
also exposed the existing `tuple(t)` identity difference. The
measured test checks tuple conversion values. A separate CPython
probe drove the conversion fix, which passes in both public execution
modes and has its own 292 focused checks on each platform.

[Source and images](source.json) · [Timings](summary.json) ·
[Allocation and admission](local-allocation.json) ·
[Raw observations](observations.tgz) · [Checks and procedures](procedures.tar.xz).

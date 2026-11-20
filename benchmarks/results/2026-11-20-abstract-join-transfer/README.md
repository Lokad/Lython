# B1234 and B1235 abstract branch joins

The frontend joins completed paths in their existing binding maps.
The conditional-expression, annotation and function-summary paths now share
the in-place join formerly used by selected statements. One path for joining
into a third state disappears, along with 26 net production lines. The
left path supplies spans and insertion order. The right path keeps its facts.
Both maps retain only common facts, and mutation clears cached resolutions.

| Quiet workload | Phase | Initial elapsed change | Initial process CPU change |
| --- | --- | ---: | ---: |
| 18-helper guarded manifest | Compilation | −1.34% to +0.02% | −1.98% to +0.46% |
| Generated validator | Compilation | −1.94% to +0.34% | −1.80% to +0.37% |
| 18-helper guarded manifest | Compile-to-result | +0.04% to +12.78% | +1.70% to +11.02% |
| Original manifest control | Warm | −1.24% to +0.46% | −1.68% to +0.16% |

The initial compile-to-result candidate worker spread is 11.51%, with
within-worker IQR no higher than 1.01%. Both workers report higher process
CPU. Compile-only timing and the warm control are inconclusive. The guarded
manifest's current-thread compilation allocation falls from 436,376 to
434,624 bytes. Five other compilation cases match exactly. All diagnostic
compilation owners return the allowance to baseline.

The earlier private snapshot design `51a69cd05` shares both maps until the
first write, then clones them. It raises validator compilation allocation
1.46% and lowers inherited dataclasses and the 18-helper manifest by 2.56%
and 1.87%. The original manifest and eager rule changes are −0.20% and
−0.21%. Both platforms pass the focused checks, and five whole scripts verify.
That prototype stays private. Allocation data does not attribute the
validator increase to any one write path.

The measured pair uses delivered `d61cfc85b`, frozen at `11de545f6`, and
private `05db3861b`. Windows .NET 11 and Ubuntu .NET 10 each pass 920
white-box and 1,621 public focused Release checks, plus 30 targeted
cancellation and admission checks. Both exact 32-file images verify six
complete CPython goldens. Another 36 programs match ordered diagnostics,
stdout, stderr and exit status through both execution modes.

Capacity preflight and identifier facts use existing compilation ownership.
The warmed compilation phase includes code-owner disposal. Compile-to-result adds
mediated RAM I/O, golden checks and previous-result cleanup. Worker setup,
fixture reset and final teardown sit outside the guest clocks. Process CPU
covers the wider request. The quiet Azure F4as_v7 ran Ubuntu 24.04.4 with
.NET 11 RC3 on 2026-11-20 UTC. Each image has two separately warmed workers.
The four initial lanes completed in 301.58 seconds, with 12 seconds of
per-worker warmup and seven rotated batches. Both quiet gates passed,
workers joined and the persistent archive verified under the lease.

## Exact-image confirmation

B1235 repeats the guarded manifest compile-to-result lane after 15 seconds
of warmup per worker and reverses the worker startup order. Across all
pairings, elapsed falls 0.65–1.28% and process CPU falls 1.30–2.48%.
Reference and candidate elapsed spreads are 0.37% and 0.28%. The original
adverse result does not recur. Its cause remains unresolved. This repeat
completed in 93.23 seconds with all 28 batches, both quiet checks, worker
joins and archive verification.

Retain the single join path for its reduction in compiler state machinery.
The small compile-to-result improvement applies to this manifest on the
repeat. Compile-only and the warm control remain inconclusive. The full
source diffs, allocation samples and original process variation are preserved.

[Initial comparison](summary.json) · [Confirmation](confirmation/summary.json) ·
[Original observations](observations.tgz) · [Checks and procedures](checks-and-procedures.tar.xz).

# B1210 Compact operand metadata for direct calls

Measures private `9f998d234` against production source `ed43ffe95`
on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on
2026-11-20 UTC. The reference binary was built from source-equivalent
`7f583edfa`. Each revision ran in two independent processes with at
least eight seconds of warmup. Five rotated rounds targeted 0.4-second
requests. Ranges cross the four pairings of process medians.

| Workload | Boundary | Elapsed change | Process CPU change | Duplicate elapsed spreads, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Generated validator | Compile | −3.11% to +0.35% | −1.22% to +1.38% | 1.54% / 2.00% |
| Configuration manifest | Compile | −2.64% to −0.32% | −2.93% to −1.03% | 1.24% / 1.12% |
| Generated validator | Compile-to-result | −1.24% to +2.57% | −3.48% to +0.58% | 1.33% / 2.50% |
| Configuration manifest | Compile-to-result | −0.97% to +0.72% | −2.50% to −1.24% | 1.68% / 0.03% |
| Eager annotated rules | Warm | −0.58% to +1.11% | +1.50% to +2.83% | 1.11% / 0.59% |
| Named shipment | Warm | +0.96% to +2.92% | +1.01% to +1.88% | 0.01% / 1.93% |

Compilation includes code-owner disposal. Compile-to-result adds one
execution. Warm execution includes mediated RAM-backed I/O where
applicable, golden checks and prior-result cleanup. Process CPU covers
the request loop. Startup, fixture reset and final teardown sit outside
guest elapsed.

The compiler stores the total arity and only the keyword suffix. This
removes the array allocation for ordinary positional calls and the
post-copy scan for call shape. In local compilation diagnostics,
managed allocation falls 1.95% on validators, 1.38% on manifests,
1.19% on eager rules and 1.61% on named shipments. Accounted execution
peaks match and warm allocation is effectively flat. The borrowed
allowance returns to baseline in all four workloads.

The initial implementation stays private. Manifest compilation improves on
both clocks and compile-to-result elapsed is mixed. Both warm controls
use more CPU, and named shipment elapsed rises. The selected refinement
keeps the compact suffix and restores the prepared flag read used by the
previous runtime. The measurements leave the cause of the warm differences
open until a source-pair confirmation.

Windows .NET 11 and Ubuntu .NET 10 each passed 2,282 focused public
checks. Ubuntu also passed 1,438 white-box checks. Three initial Windows
subprocess checks used the system SDK and could not load .NET 11. The
successful rerun rebuilt the probe and put the pinned SDK on PATH.
Seven CPython probes matched on both exact images in both execution modes.
The initial diagnostic case used a statically known invalid call that Lython
rejects at compile time. The final probe uses a dynamic target to exercise
execution-time argument effects. Both machine checks and all 120 timed
batches passed. Workers joined and the persistent archive matched its
verified hash.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source, checks and procedures](source-and-checks.tar.xz).

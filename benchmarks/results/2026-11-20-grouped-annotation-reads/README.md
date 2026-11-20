# B1186 grouped function annotation reads

Private `8bd9b2d9b` groups simple executable annotation reads into the
function creation operation. The compiler uses the normal lexical operand
indexes and diagnostic spans. Function creation fills admitted final
annotation values directly. Defaults and decorator expressions run before
the reads. Annotations with callbacks, yields or dynamic scope lookup
use their existing execution route.

| Workload | Boundary | Quiet elapsed change | Quiet process CPU change |
| --- | --- | ---: | ---: |
| Eager annotated rules | Warm | −3.91% to −2.82% | −4.86% to −2.75% |
| Nested annotated factories | Warm | −0.47% to +4.07% | −0.76% to +4.37% |
| Postponed annotated rules | Warm | −1.95% to +5.40% | −4.92% to +6.62% |
| Eager annotated rules | Compile-to-result | −2.35% to +1.22% | −2.24% to +1.96% |
| Batch of annotation callbacks | Warm | −1.13% to +8.23% | +0.00% to +9.80% |
| File-backed CSV invoice | Warm | −1.54% to +0.65% | −1.32% to +1.35% |

Keep the prototype private pending a targeted confirmation. Eager warm
improves across all pairings, with reference and candidate spreads below
1%. Nested reference spread is 4.44%. The postponed candidate spread is
4.94%. The callback candidate spread is 7.16%, and its CPU is adverse
in three pairings. Callback expressions follow their existing path, and
the measurement cannot attribute the movement to a particular stage.
A follow-up should isolate nested, postponed and callback workloads
against the consolidated declaration metadata, retaining an invoice control.

Local warm allocation changes by less than 0.01% in all seven workloads.
The accounted peaks match or fall by 24 bytes. Every workload returns the
borrowed allowance to baseline. Windows .NET 11 and Ubuntu .NET 10 each
passed 522 focused Release checks, including default callbacks rebinding
annotation names and the exact line for NameError. Both complete images
matched seven workload goldens and eight CPython probes in both Lython modes.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 20,
2026 UTC. The reference `848cbf783` matches `8fafe1a5a` in tracked core,
adapter, test and worker sources. Warm includes I/O where used, golden
verification and prior-result cleanup. Compile-to-result adds compilation
and code-owner disposal. Process CPU covers the wider batch. Range endpoints
are the extreme pairings of the duplicate process medians.

Both quiet gates and all 120 timed batches passed. Workers joined, fixtures
were removed and the raw archive was verified under the lease.

[Summary](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Checks and procedures](checks-and-procedures.tar.xz).

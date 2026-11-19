# B1074 shared direct capture access

Prototype `776761552`, retained as `773be8dc2`, stores a singleton capture
in its shared cell and uses the existing array for multiple captures. Both
executable interpreters use the same direct slot lookup for loads, stores,
and closure arithmetic. The reference is `12fabffb1`. This revisits B1072's
captured-rule slowdown while keeping its ownership changes.

Windows .NET 11 and WSL .NET 10 passed 587 focused Release cases each,
including shared rebinding, generators, namespace-backed captures, alias
identity, cancellation, and admission refusal. Both complete 32-file images
passed five CPython goldens. Local warm allocation fell 6.39% for retained
siblings and 2.76% for captured rules. Accounted peaks fell 28,800 B and
163,840 B respectively. Mutable counters stayed flat and all budgets returned
to baseline after cleanup.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-19 UTC.
Ranges cover all four combinations of duplicate process medians. Negative
values favor direct capture access.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Retained siblings | Warm | −2.33…+1.57% | −2.69…+1.92% |
| Retained siblings | Compile-to-result | −4.62…−2.38% | −6.16…−3.49% |
| Captured rules | Warm | −0.57…+2.53% | −0.63…+1.92% |
| Mutable counters | Warm | −0.69…+0.80% | −1.63…+1.02% |
| Annotated rules | Warm | −3.56…−1.16% | −2.77…−0.36% |
| Expanded forwarding | Compile-to-result | −1.50…+1.00% | −3.22…+0.23% |

Retained siblings improve through compile-to-result with lower CPU, and both
target allocation reductions persist. Sibling warm elapsed and CPU span zero.
Captured-rule warm elapsed spans zero with three of four pairs slower. The
all-pairs CPU increase from B1072 did not recur. The favorable movement on
the annotated control limits attribution. Keep captured-rule CPU and elapsed
open at the next call checkpoint. This pair alone cannot isolate the effect
of dispatch from all other capture changes.

Both quiet gates and all 120 timed goldens passed in 275.52 seconds. Workers
joined, guest fixtures were removed and the archive verified under the lease.
Warm includes mediated I/O, goldens and previous-result cleanup. Compile-to-
result adds compilation and compiled-owner disposal. Process CPU observes a
wider boundary.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Checks and procedures](checks-and-procedures.tar.xz) ·
[B1072](../2026-11-19-direct-singleton-capture/README.md).

# B1729 Warm default evaluation and call attribution

Two warm CPU captures use production source `857cc8001` and the exact
B1728 scripts and frozen worker. Both quiet gates, both complete output
checks and archive verification pass. Worker and profiler processes join
before guest fixtures are removed. Linux `perf` samples all worker threads
at 199 Hz on the Azure F4as_v7 running Ubuntu 24.04.4 and .NET 11 RC3.
Each phase warms for at least eight seconds and captures about six seconds.
The boundary includes framing, output verification and result cleanup.

| Warm case | All samples | Resolved runtime ancestry | Nearest native call | Nearest native instruction loop | Nearest direct operand return | Nearest admission |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Complete definitions | 1,172 | 1,153 | 205 | 302 | 78 | 82 |
| Default effects | 1,135 | 1,117 | 292 | 253 | 133 | 7 |

The captures have 94 and 109 unresolved leaves. A resolved ancestor can
still exist for an unresolved leaf. Sampling and inlining limit attribution.
These counts describe code on a sampled stack and cannot predict the savings
from removing an individual operation.

Inspection of the same core shows 160 `LoadGlobal` → `Binary` → `Call`
sequences per factory run in both scripts. The factory has 101 slots in the
complete script and eight in the diagnostic. `shift` already runs its binary
and return directly against caller operands. A prototype should remove
the intermediate target and computed integer argument publication on the
same tape. Keep the original path for protocols and non-direct callees.
Preserve name and operand error order, exact mutable bindings, managed
stack admission, cancellation and already admitted default storage.

[B1728 effects comparison](../2026-11-25-ordinary-definition-effects/README.md) ·
[Identity](identity.json) · [Instruction layout](instruction-layout.json) ·
[Attribution](attribution.json) · [Raw profiles](observations.tar.xz) ·
[Terminal receipt](terminal-status.json) · [Procedures](procedures.tar.xz).

# B1473 CPU attribution on retained CSV and text workloads

Profiles use `fed081ff0`, frozen at source-equivalent `8a44f644d`,
on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on
November 23, 2026 UTC. The 32-file image and three CPython fixture
goldens verify before collection. Each worker warms for eight seconds,
then perf samples CPU-clock at 199 Hz for about five seconds across
complete batches. Worker clocks include execution, mediated RAM I/O,
verification and prior-result disposal. Process samples also cover
worker work outside those clocks.

| Nearest runtime method or overlapping ancestry | Explicit float CSV | Typed ASCII CSV | Short nonlocal text |
| --- | ---: | ---: | ---: |
| All process samples | 1,030 | 1,054 | 1,003 |
| Resolved runtime ancestry | 998 | 996 | 934 |
| Unmapped leaf | 89 | 115 | 130 |
| Instruction prefix | 259 | 238 | 93 |
| ResolveName | 70 | 33 | — |
| CSV input and row ancestry | 204 | 245 | — |
| Short decimal parser leaf | 21 | 0 | — |
| File or host I/O ancestry | 187 | 236 | 106 |
| Closed helper entry and operand binding ancestry | — | — | 483 |
| Native text word and dictionary ancestry | — | — | 249 |
| Retirement and cleanup ancestry | 31 | 21 | 19 |

The numeric leaf count excludes inlined parsing. Name resolution is
more visible on the explicit meter, which calls a global `float` three
times per row. A root module can use the existing live module/builtin
binding path where the compiler can prove the absence of a lexical
search. Test fresh and warm execution, mutable namespaces, local imports,
dynamic builtin shadowing and the narrower scopes still needing general
resolution before considering retention. The text report has both call
and actual word-processing costs. Its inclusive call ancestry includes
work performed by the callee and gives no standalone frame cost.

The collection leaves all workloads joined and fixtures removed.
The persistent archive verifies under the machine lease.
These are attribution samples. External ratios and source-pair timing
retain their own measurement boundaries.

[Identity](identity.json) · [Attribution](attribution.json) ·
[Raw profiles](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures and goldens](procedures.tar.xz).

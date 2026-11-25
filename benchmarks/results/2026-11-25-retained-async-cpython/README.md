# B1694 Retained async runtime against CPython

This checkpoint measures `f6cf9796c` against stock CPython 3.13.16 on the
quiet Azure F4as_v7 running Ubuntu 24.04.4 and .NET 11 RC3 on November 25,
2026 UTC. The nonincremental B1692 build matches the committed source
patch. The archived identity identifies all 32 image files, both
toolchains and six complete CPython 3.13.2 goldens. The source predates
single-pass boolean parsing.

Two processes per engine each contribute seven rotated batches per phase.
Each phase warms the four processes for five seconds, then targets 0.46 s
per batch. Ratios divide Lython by CPython across the four possible
pairings of process medians. The async rows use Lython's `RunAsync` and
CPython's synchronous execution of the same guest script. Warm and
compile-to-result clocks include applicable RAM-backed reads and writes,
output and file verification and result disposal. Lython's mediated host
provides efficient range and append operations with shared admission active.
CPython uses native file APIs. Compile-only includes compiled-owner disposal.
Fixture reset and request framing sit inside wider process CPU and outside
the elapsed clock. Startup and final teardown sit outside both.

| Workload | Phase | Lython | CPython | Elapsed ratio | Wider CPU ratio |
| --- | --- | ---: | ---: | ---: | ---: |
| Shipment callback report | Warm async | 2.61576–2.68352 ms | 1.61220–1.62845 ms | 1.606–1.664× | 1.601–1.649× |
| Ordinary generated definitions | Warm | 0.31790–0.32041 ms | 0.14716–0.14855 ms | 2.140–2.177× | 2.134–2.193× |
| Defaulted rule pairs | Warm | 0.89580–0.90843 ms | 0.68316–0.69232 ms | 1.294–1.330× | 1.305–1.346× |
| Shipment callback report | Warm sync | 2.43455–2.50138 ms | 1.61714–1.64107 ms | 1.484–1.547× | 1.454–1.514× |
| Bound `starmap` shipment report | Warm async | 3.01119–3.04841 ms | 1.67604–1.69917 ms | 1.772–1.819× | 1.770–1.800× |
| Ordinary generated definitions | Compilation | 0.57619–0.58162 ms | 0.94630–0.95017 ms | 0.606–0.615× | 0.616–0.624× |
| Configuration manifest | Compile to result | 0.37025–0.37255 ms | 0.46072–0.46259 ms | 0.800–0.809× | 0.793–0.802× |
| Defaulted rule pairs | Compile to result | 0.93271–0.93350 ms | 0.73196–0.73372 ms | 1.271–1.275× | 1.262–1.274× |
| Shipment callback report | Compile to result | 2.48041–2.59144 ms | 1.90683–1.91453 ms | 1.296–1.359× | 1.278–1.335× |
| Ordinary generated definitions | Compile to result | 0.91328–0.91620 ms | 1.12301–1.12417 ms | 0.812–0.816× | 0.813–0.838× |
| Quote-heavy invoice | Warm | 6.16012–6.39276 ms | 6.24988–6.30029 ms | 0.978–1.023× | 0.979–1.028× |

Shipment and defaulted rules remain behind CPython through result. Ordinary
definitions take over twice as long warm and reach the first result sooner.
Their CPython compile-only worker spread was 16.75% in the prior checkpoint
and is 0.41% here. The invoice spans parity. Lython's shipment time-to-result
worker spread is 4.48% and invoice spread is 3.78%. The phases are measured
independently and the ratios are diagnostic. Comparative claims about
runtime changes use their dedicated same-source controls.

Both quiet gates passed. All 308 batches verify source, fixtures and
expected outputs as applicable. The workers joined, temporary guest
fixtures were removed, and the observation archive was hash-verified
while the machine lease was held.

[Identity](identity.json) · [Timings and spreads](summary.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures](procedures.tar.xz) ·
[Previous checkpoint](../2026-11-25-retained-defaults-cpython/README.md).

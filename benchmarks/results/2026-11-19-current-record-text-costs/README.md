# B1044 retained record and text attribution

Profiled `aa29c019e` with core `a4cbe8d2` on the quiet Ubuntu 24.04.4
Azure F4as_v7 and .NET 11 RC3 on 2026-11-19 UTC. The three cases reuse
the B1040 source, inputs and goldens. The complete retained worker image
and the catalog verified before staging. [Workloads](../2026-11-19-retained-runtime-cpython/README.md).

| Workload | Resolved runtime stacks | Largest observed operation groups |
| --- | ---: | --- |
| Expanded forwarding | 1,039 | 171 borrowed invocation, 78 formatting, 68 split construction, 45 rstrip |
| CSV invoice | 1,045 | 452 native calls, 232 CSV parsing, 97 integer conversion/arithmetic |
| Shipment report with format selectors | 1,083 | 458 formatting, 443 method-format selector rendering, 90 JSON load/conversion |

For the selector report, the nearest runtime frame is the main method-format
loop on 60 samples and the separate selector continuation on 33. String
formatting, integer formatting and indexed reads also appear. The library
currently recreates each guest lookup key from a CLR string. Exact native
dictionaries can reuse prepared UTF-8 keys while generic subscripts receive
their own values. Moving selector lookup into the ordered renderer also
removes its separate continuation. Those are source-level candidates to
evaluate on the report and its unaffected controls.

The CSV parser and native calls appear regularly. These samples alone give
limited evidence for a new record representation. The source already has
final record storage and transfers eligible parsed rows. In forwarding,
direct positional text construction appears on 60 samples. Split and trim
remain observable stages alongside dictionary and call work.

Groups use overlapping stack ancestry and incomplete symbol resolution.
They describe where sampled work occurs. Each case uses whole-batch work
after a warmup. Output and file goldens passed. Both quiet gates passed,
workers joined and RAM traces were verified in the persistent archive
under the lease before guest fixtures were removed.

[Attribution](attribution.json) · [Raw traces](observations.tgz) · [Procedures](procedures.tar.xz).

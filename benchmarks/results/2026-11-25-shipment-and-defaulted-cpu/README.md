# B1664 Shipment callbacks and defaulted-rule CPU

The profile uses the exact production `788be8f35` image from the
[B1663 CPython comparison](../2026-11-25-shared-locals-cpython/README.md).
The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 24,
2026 UTC. Each profile warms for at least eight seconds, samples CPU
at 199 Hz, and verifies its complete golden before and after the batch.
The shipment report uses warm execution. The rules include compilation,
execution and disposal inside each invocation.

| Workload | Invocations | All samples | Resolved runtime ancestry | Nearest frontend samples |
| --- | ---: | ---: | ---: | ---: |
| Shipment callback report, warm | 2,304 | 1,216 | 1,184 | 7 |
| Defaulted rules, compile-to-result | 6,400 | 1,205 | 1,166 | 41 |

For shipment, the nearest Lython frames include `PyMapIterator.TryMoveNext`
in 77 samples and `PyZipIterator.TryMoveNext` in 46. Tuple admission inside
zip accounts for 21 more nearest reservation callers. The executable
instruction dispatch accounts for 189 nearest frames. The row fields and
callback inputs already exist when the loop receives the zip tuple. A
fixed unpack can use existing operand slots and preserve ordered iteration
without allocating the tuple that this loop immediately discards.

Defaulted-rule definition construction accounts for 598 of 1,166 resolved
runtime stacks. Pair member construction is the closest runtime frame in
275 samples. There are 166 nearest calls to explicit `Reserve` in that
region, including 89 from default storage and 72 from function creation.
The earlier [B1657 profile](../2026-11-24-current-default-call-cpu/README.md)
locates the same lifecycle costs. A redesign needs to preserve each
definition's identity, storage owner and distinct admission/error points.

The counts locate code and do not predict speedup. Both quiet gates pass,
workers and profiler join, the fixture directory is removed and the
archive verifies under the lease.

[Attribution](attribution.json) · [Identity](identity.json) · [Raw observations](observations.tar.xz) ·
[Terminal receipt](terminal-status.json) · [Procedures](procedures.tar.xz).

# Representative CSV versus CPython

2026-11-15 UTC, **51e3c2c1 / frozen 22e5b72c**, versus stock **CPython 3.13.16**.
All 585 production, four build and two changed test inputs match the committed
runtime after explicit newline normalization; no independently rebuilt binary
identity is inferred. [Full delivery run43708277205 passes](../2026-11-15-tuple-operand-prototype/delivery/README.md); original measurement metadata retains its at-capture status.

| Complete workload/phase | Lython | CPython | Lython / CPython |
| --- | ---: | ---: | ---: |
| Invoice / warm `Run` | 6.777–7.094 ms | 6.045–6.047 ms | 1.12–1.17× |
| Invoice / compilation only | 101.232–102.939 µs | 92.982–93.560 µs | 1.08–1.11× |
| Invoice / compile and execute once | 6.970–7.027 ms | 6.316–6.340 ms | 1.10–1.11× |
| Fulfillment / warm `Run` | 7.740–7.885 ms | 6.669–6.678 ms | 1.16–1.18× |

Invoice uses 4,096 records/127,031 B input and 1,222 B source; fulfillment uses
4,096 records/126,821 B input and 1,360 B source. Sources, inputs, outputs and
worker/catalog hashes match. Warm invoice has **4.47% Lython process spread**;
other Lython spreads are 1.69% compile, 0.82% fresh and 1.84% fulfillment. CPython
spreads are 0.03%, 0.62%, 0.38%, 0.13%; maximum IQRs 2.25%, 1.61%, 1.76%, 1.33%.
These ranges are diagnostic observations, not confidence intervals or exact gains
from any earlier Lython collection. No adverse process is discarded.

Quiet Azure F4as_v7/Ubuntu 24.04.4, four AMD EPYC 9V45 vCPUs, 16 GiB/no swap,
.NET 11 RC3 workstation GC/normal tiering, CPython GIL/GC. Two independent
processes per engine/phase, five rotating 0.5 s batches, five seconds full-request
warmup each. Four jobs finish in **137.835 s** under one 240 s owner.

Warm clocks include mediated RAM-file I/O, full stdout/file validation and
prior-result cleanup, excluding fixture reset/staging, protocol, compilation,
startup and final compiled/cache-owner disposal. Compilation-only includes
compiled-owner disposal; compile-plus-one-run includes compilation, execution
and compiled-owner disposal in one measured operation. Independently warmed
phase medians are not additive. Wider process CPU includes reset/protocol.
The Lython host efficiently overrides ranged/stream file operations and completes
I/O synchronously. CPython uses native buffered files without Lython's host
authorization or shared-budget admission: actual embedding costs, not equal
containment or isolated-VM CPU. No startup/pending-I/O/heap/tail claim.

All identity, golden, quiet, memory-balance, Closed/exit/join and archive gates
pass; no users/RAM fixtures remain. Existing tools and frozen image are reused,
without a new benchmark framework or runtime image upload. This CPython baseline
is separate from the [Lython source-pair improvement](../2026-11-15-tuple-operand-prototype/refined/README.md).

[All phase controls](summary.json), [identity](identity.json),
[raw observations](observations.tgz), [owned cleanup](terminal-status.json),
[exact collection procedure](procedure.tar.xz).

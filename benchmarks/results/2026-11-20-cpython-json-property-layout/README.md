# B1233 JSON shipment checkpoint against CPython

Measures delivered Lython `d61cfc85b` using frozen `11de545f6`, and
CPython 3.13.16 on the Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3,
on 2026-11-20 UTC. The named report processes 3,072 three-field objects
from 189,441 bytes. The wide report processes 3,072 24-field objects from
1,329,057 bytes. Both produce complete Markdown reports.

| Workload and phase | Lython | CPython | Lython / CPython |
| --- | ---: | ---: | ---: |
| Named report, warm | 3.732–3.755 ms | 3.357–3.410 ms | 1.09–1.12× |
| Named report, compile-to-result | 3.818–3.873 ms | 3.487–3.501 ms | 1.09–1.11× |
| Named report, compilation | 0.02448–0.02453 ms | 0.03435–0.03456 ms | 0.708–0.714× |
| Wide report, warm | 6.504–6.536 ms | 12.600–13.123 ms | 0.496–0.519× |

Ratios cross all combinations of two independent process medians per engine.
The wide CPython worker spread is 4.15%. Its maximum within-worker elapsed
IQR is 4.60%. Compile-only measures compilation and code-owner disposal.
Warm includes mediated RAM I/O, full output verification and previous-result
cleanup. Compile-to-result adds compilation and code-owner disposal. Each
phase warms independently. Fixture reset, startup and final teardown sit
outside the guest clocks. Wider process CPU is in the summary. Memory and
startup were outside this collection. The earlier B1190 named report stays
in the record. Use B1231 to compare the two Lython revisions directly.

All 80 batches passed. Both quiet gates passed, workers joined and the
persistent archive was verified under the VM lease. The first attempt
stopped before timing because the staging manifest lacked the worker's
auxiliary hash list. The corrected run verified the same complete image
and goldens. The original status and zero-job archive remain available.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Procedures](procedures.tar.xz) · [Failed setup](initial-setup/failed-setup.tgz) ·
[Controlled Lython source pair](../2026-11-20-plain-json-property-names/README.md).

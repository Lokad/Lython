# Current async hosting against CPython

R393/B780, 2026-11-15 UTC, fully qualified **b58e2dc4 / frozen f304b3db** versus
stock **CPython 3.13.16**. Existing complete sources and unchanged workers/protocol;
Lython uses public `RunAsync`, awaited to completion, CPython uses warm synchronous
execution. The benchmark host's file operations return completed ValueTasks.
This measures the deployment boundary, not genuinely pending-I/O latency.

| Complete workload | Input | Lython RunAsync | CPython | Lython / CPython | Status |
| --- | --- | ---: | ---: | ---: | --- |
| Captured text helper/counter | 3,072 lines / 35,840 B | 1.241–1.296 ms | 0.888–0.906 ms | 1.37–1.46× | Variable; process spreads 4.20/2.10%, max IQR 0.99% |
| Protected file search | 2,049 lines / 42,010 B; stop after 1,025 | 0.271–0.283 ms | 0.150–0.152 ms | 1.78–1.88× | Variable; process spreads 4.21/1.40%, max IQR 2.06% |

Above one means Lython slower. Two independent processes per engine, five rotating
rounds; ranges span process medians and all four ratio pairings, not confidence
intervals. Keep moving controls visible. No cross-source speedup is inferred.

Quiet Azure F4as_v7, Ubuntu 24.04.4, four EPYC 9V45 vCPUs, 16 GiB/no swap, .NET 11
RC3, ordinary workstation GC/tiering and CPython GIL/GC. Twelve-second full-request
warmup per text worker, five seconds for search, 0.5 s request batches. Both jobs
finish in 99.638 s. Elapsed includes mediated RAM-file I/O, public invocation,
prior-result disposal and full stdout/file verification. It excludes compilation,
startup, reset/protocol and final cache teardown. Wider process CPU includes reset/
protocol. CPython has ordinary native file access; Lython retains host authorization,
cooperative admission/cancellation and managed stack protection. These are actual
embedding costs, not an isolated interpreter comparison or equal-memory claim.

The [earlier named-revision phases](../2026-11-15-config-manifest-phases/README.md)
still separate compilation, warm execution and compile-to-result. No current async
fresh/startup, heap/RSS, contention, pending-I/O or embedding-tail claim.

All exact identities, source/input/output goldens, quiet gates, shared balances,
Closed/join and archive checks pass. No users or RAM fixtures remain; final free
space 269,602,816 B. Existing images/catalog/CPython worker reused, no new runtime
image or framework. Storage floors are unchanged. This source passes
[full platform, original stress and installed-package qualification](../2026-11-15-invocation-lifetime/delivery/README.md);
the raw launch identity retains the pending qualification status known at preparation.
Publication remains postponed.

[All controls, IQRs and wider CPU](summary.json), [raw observations and procedures](observations.tgz),
[local launch preparation](local-procedures.tar.xz), [archive proof](archive-receipt.json),
[terminal state](terminal-status.json), [preceding guide](prior-BENCHMARKS.md).

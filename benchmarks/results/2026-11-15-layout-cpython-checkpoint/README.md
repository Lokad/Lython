# Reused-layout CPython checkpoint

Committed**58eeea6f / frozen9d071eba** versus stock**CPython3.13.16**,
2026-11-15UTC, quiet F4as_v7/Ubuntu24.04.4/four EPYC9V45vCPUs/16GiB/no swap,
.NET11RC3 normal tiering/workstationGC and CPython GIL/GC. One existing complete
JSON-to-Markdown report,1,024records/189,441B JSON, method-format and f-string variants.
All stdout/file goldens agree. Two processes/engine,5s full-request warmup/five rotated
~0.5s batches. B813four phases succeed139.049s/all users joined/RAM fixtures removed.

| Phase | Lython ms | CPython ms | Lython/CPython | Controls Lython/CPython | Max IQR |
| --- | ---: | ---: | ---: | ---: | ---: |
| Method warm |4.364–4.403|2.614–2.633|1.657–1.685×|0.89/0.75%|4.49%|
| Method compilation |0.03669–0.03670|0.03565–0.03599|1.019–1.030×|0.05/0.95%|0.94%|
| Method compile-to-result |4.438–4.479|2.727–2.771|1.601–1.643×|0.92/1.62%|4.15%|
| F-string warm |2.789–2.803|2.366–2.384|1.170–1.185×|0.51/0.72%|4.63%|

Ratios are all pairings of process medians, not confidence intervals. Diagnostic
complete embedding costs, not isolated interpreter CPU. Warm includes synchronous
Run/mediated RAM I/O/full output/file checks/prior-result cleanup; excludes compile,
startup,fixture reset/protocol/finalcache teardown. Compile includes compiled-owner
disposal; fresh includes compilation/one execution/compiled-owner disposal. Separate
medians are not additive. Wider processCPU includes reset/protocol. No actual pending
I/O, startup,RSS/heap, contention or embedding-tail comparison. CPython native files
do not implement equivalent capability/shared-budget containment.

The method-vs-f-string difference does not isolate calls, and cross-collection changes
are not causal speedups. [Controlled R418source pair](../2026-11-15-method-format-layout/README.md)
supports the retained design separately. [Prior B810](../2026-11-15-format-cpython-checkpoint/README.md)
keeps all earlier observations, including noisy8.86%fresh CPython, with original labels.
The material remaining method gap motivates continued complete-call/transport work.

All full-image/catalog/golden/budget/quiet/Closed/exit/join/archive gates pass.
Evidence archived/hash-verified persistently under the lease; no workers/RAM remain.
254/252MiB floors preserved. Only exact committed B812raw/archive working replicas
retired with fresh UID/path/hash/link/all-alias proofs; canonical evidence and current
images remain. Terminal cached forever.12internal/682public actual11/10 focused checks
and593frozen input proofs pass; **full43771040409 active at capture**, not yet claimed
qualified. Publication held. [Prior guide](prior-BENCHMARKS.md),[summary](summary.json),
[identity](identity.json),[raw](observations.tgz),[inputs/procedure](procedure-and-inputs.tar.xz).

[Subsequent full runtime qualification passes](../2026-11-15-method-format-layout/delivery/README.md).

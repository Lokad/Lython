# B1370 Current call and first-use CPU attribution

Profiled the same two 32-file images as B1368 on November 22, 2026
UTC. The retained core `7a8df051a` matches production `880c6d958`.
The reference core `638fc1138` matches production `9e8ec712d`.
The Azure F4as_v7 runs Ubuntu 24.04.4 and .NET 11 RC3 with normal
tiering. Linux perf sampled the worker at 199 Hz after warmup.
These profiles cover all worker threads, including result
verification and cleanup. Mixed time to result includes compilation.
Neither workload uses guest files.

| Profile | Process samples | Unmapped leaf | Native loop | Generic loop | Native definition | Explicit reserve | General binder | Frontend ancestry |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Prior image, mixed time to result | 1,112 | 98 | 154 | 141 | 111 | 90 | 33 | 25 |
| Retained image, warm function helper | 1,110 | 83 | 515 | 0 | 0 | 1 | 0 | 0 |
| Retained image, mixed time to result | 1,101 | 105 | 161 | 155 | 100 | 75 | 31 | 26 |

Runtime columns count the closest resolved runtime method. Frontend
ancestry can overlap those columns. The native loop, call and entry
frames contain useful execution. Sample counts do not estimate the
cost of removing an entire method or establish a source speedup.

The mixed images show similar workloads and no new dominant sampled
cost. Callable construction accounts for 87 of 90 and 74 of 75 nearest
reserve samples. Native continuation construction has no closest
runtime samples in either mixed capture. Allocations and allowance
peaks match, and neither newly added native branch occurs on the
mixed tape. The B1368 CPU debt remains open.

The function helper has 515 of 1,110 samples nearest the native
loop. Another 87 are nearest the native call, 49 the inline entry,
and 26 native argument entry. The general binder has no resolved
ancestry there. A design focused on general binding alone would miss
that script. Inspect complete native entry, operand execution and
publication before replacing work storage. Keep logical recursion,
managed stack checks, terminal refusal, live aliases and transfer
through genuine suspension.

Both complete images and their existing CPython goldens verify.
All three workers returned Closed and joined. Both quiet gates passed.
Raw traces were archived and verified under the lease before the
fixture mount was removed.

[Attribution](attribution.json) · [Raw observations](observations.tgz) ·
[Verification and procedures](procedures.tar.xz) ·
[Source comparison](../2026-11-22-native-fixed-returns-refined/README.md) ·
[CPython checkpoint](../2026-11-22-retained-call-transport-cpython/README.md).

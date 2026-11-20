# B1224 Nested factory CPU attribution after final default storage

Profiles `72a1426dd` with exact frozen source `842c2c8f9` on the quiet
Azure F4as_v7 running Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-20 UTC.
The subsequent core source difference at `d948e0682` is confined to
regex windows, outside these scripts. Each worker warms for eight
seconds and `perf` samples roughly five seconds of all-thread CPU
while running complete warm invocations. The sampled boundary includes
prior-result cleanup, mediated RAM-backed CSV I/O, goldens and worker
framing. Each workload verified immediately before and after sampling.

| Stack observations | Eager nested | Postponed nested | CSV invoice |
| --- | ---: | ---: | ---: |
| Process samples | 1,060 | 1,026 | 1,036 |
| Resolved runtime ancestry | 1,042 | 1,006 | 1,015 |
| Nearest native instruction loop | 162 | 89 | 138 |
| Nearest general instruction prefix | 126 | 151 | 113 |
| Call entry, binding or retirement in ancestry | 689 | 633 | 386 |
| Definition construction in ancestry | 125 | 150 | 0 |
| Final default construction in ancestry | 44 | 82 | 0 |
| Nearest storage reservation | 102 | 122 | 39 |
| Reservation for callable shell | 45 | 50 | 0 |
| Reservation for capture | 24 | 14 | 0 |
| Reservation for positional / keyword default | 14 / 16 | 21 / 34 | 0 / 0 |
| Unresolved leaf samples | 85 | 87 | 84 |

No resolved stack includes scope promotion or previous-result disposal.
Sampling cannot separate inlined work or prove zero cost. Call ancestry
includes the executed function body, which makes the inclusive count
unsuitable as an estimate for removing call setup. The callable shell,
captures and default containers all have live guest users. Consider a
full construction or operand replacement only with a concrete lifetime
argument and an end-to-end comparison covering both factory cases and
the invoice. The profile gives no elapsed or CPU improvement estimate.

An initial WSL capture selected the Linux-kernel profiler through the
managed `collect` command and stopped. The corrected local managed
capture used `dotnet-sampled-thread-time` on `d948e0682`. Most CPU-tagged
factory stack intervals terminate at `StartNativeCall` among resolved
runtime frames, obscuring the inner phases. Both local attempts are
preserved. The quiet VM frames distinguish the instruction loop and
admission callers.

All 32 image files and three CPython goldens verified before staging.
Both quiet gates passed, all workers joined, and the raw archive
verified under the lease before guest fixture removal.

[CPU attribution](attribution.json) · [Quiet VM profiles](observations.tgz) ·
[Procedures and local observations](procedures-and-local-observations.tar.xz).

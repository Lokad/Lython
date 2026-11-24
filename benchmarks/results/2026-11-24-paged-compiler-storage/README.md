# B1592 Shared paged compiler storage

Private `2e32391b5` emits blocks into shared instruction pages and reserves
scratch before allocating. Ordinary blocks are contiguous. Reopened blocks
keep ranges in logical order. The first page grows to 128 slots and later
pages append without copying older instruction storage. The fixed-page
address removes the first version's varied-width lookup. Prepared operands,
source spans and protected ranges publish into the same final tape.

| Complete source | Boundary | Elapsed change | Wider process CPU change | Worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Configuration manifest | Compile | +3.00 to +3.46% | +2.95 to +3.23% | 0.38 / 0.07% |
| Sparse generated validator | Compile | +4.06 to +5.05% | +5.06 to +7.14% | 0.32 / 0.63% |
| Postponed annotated factories | Compile | +4.56 to +6.41% | +4.01 to +6.59% | 1.33 / 0.44% |
| Inherited dataclass | Compile | +5.99 to +7.70% | +4.00 to +6.21% | 0.06 / 1.55% |
| Dead-heavy validator | Compile | −1.23 to +0.25% | −2.58 to +0.41% | 0.82 / 0.67% |
| Configuration manifest | Compile to result | +2.47 to +3.12% | +1.17 to +1.78% | 0.53 / 0.10% |
| Sparse generated validator | Compile to result | +3.34 to +4.69% | +1.30 to +6.04% | 0.72 / 0.58% |

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 24,
2026 UTC. The reference core was frozen at `4eb8c9559` with runtime and test
source matching `5313f9e71`. The two 32-file images differ only in the core
DLL. Two workers per image warm for eight seconds and supply seven rotated
batches. Ranges cover pairings of worker medians. Compilation includes
compiled-owner disposal. Compile to result adds execution, mediated
RAM-backed I/O, verification and previous-result disposal. Wider process
CPU also includes reset and request handling. Startup and final process
teardown sit outside the clocks.

| Compile allocation diagnostic | CLR allocation change | Admitted peak before → candidate |
| --- | ---: | ---: |
| Configuration manifest | +4.61% | 212,960 → 221,424 B |
| Sparse generated validator | −10.22 to −10.17% | 2,294,048 → 2,454,464 B |
| Postponed annotated factories | +2.24% | 78,718 → 82,558 B |
| Inherited dataclass | +4.05% | 414,656 → 426,048 B |
| Dead-heavy validator | −10.72% | 1,054,592 → 1,101,104 B |

Keep the paged design private. It removes large backing copies and reduces
CLR allocation on the large validators. It also raises admitted peak and
consistently costs more CPU and elapsed on the configuration manifest and
sparse validator, including complete runs. The dead-heavy control spans
zero. The initial varied-width prototype saved roughly the same large
allocations with a higher sparse admitted peak of 2,550,032 bytes. Its
local elapsed varied too much for a CPU comparison. Future work on this
boundary should remove an entire representation or pass and account for
its remaining storage together. A repeat of page placement alone would
leave the measured indexing and admission costs in place.

The protected-emission prototype eliminates 192 sparse block reopens with
identical instructions. New tests exercise reads and writes across pages
and reopened ranges, admission refusal and cancellation during successful
reservation. The first prototype captured a stale block view in an
augmented assignment. Its failure was reproduced on the original image
and the corrected layouts match across 14 sources. Both platforms pass
378 focused white-box and 196 public Release tests. Five CPython goldens,
196 timed batches and both quiet gates verify. Every local allocation run
refunds its allowance. Workers join, guest fixtures retire and the raw
archive verifies under the VM lease.

[Timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Initial prototype allocation](initial-allocation-summary.json) · [Images](identity.json) ·
[Raw observations](observations.tar.xz) · [Procedures and patches](procedures.tar.xz) ·
[Terminal status](terminal-status.json) ·
[Reference core](../2026-11-24-decoded-closed-defaults/candidate-core.dll.gz).

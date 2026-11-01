# Differential helper-call CPU attribution (B65)

2026-11-01, source **7256808e**, production **35d8942c**. One fixed capture
compares the exact original/expanded CSV source forms from
[B58](../2026-11-01-helper-cost-assessment/README.md), followed by an identical
original control. No production change, qualified latency, CPython ratio or
removable CPU fraction is established.

## Boundary and controls

[Plan before collection](pre-collection-plan.md). The private existing-driver
adaptation loads three identical frozen core/host images in one process, sharing
CoreLib and verified key hashes. Core SHA256
`526b0a185fd4e3d9a960ea2734f4175c6f1cd58ba7e0d829955156254de8ce99`.
The expansion replaces only `parse_amount(row['amount'])` with
`int(row['amount'])`; definitions and annotations remain. Reuse B58's isolated
CPython goldens and the maintained 4,096-record fixture.

Dedicated Ubuntu24.04.4 Azure F4as_v7, four EPYC9V45 vCPUs/16GiB, .NET11RC3,
ordinary GC/tiering. After 128 interleaved complete warmups per image and two
seconds settling per stage, each capture runs exactly 384 complete public jobs.
Compilation/startup excluded; fixture reset, mediated file I/O, full stdout/file
checks and result disposal included. Linux cpu-clock sampling at199Hz with frame
pointers and CLR maps stays outside production.

| Source form | Capture wall | Actual guest CPU | Guest samples | Unknown leaves |
| --- | ---: | ---: | ---: | ---: |
| Original helper | 3.210s | 2.55s | 506 | 41 |
| Expanded expression | 2.711s | 2.10s | 415 | 26 |
| Identical original | 3.232s | 2.58s | 511 | 33 |

Total capture9.153s, below15s; every stage below6s. The owned service runs24s,
below60s. Before/after quiet gates, full output/file goldens, shared hashes,
per-execution baseline balances, final zero balances and fixture removal pass.
All profilers/users join: exit0, inactive/dead/success, MainPID0, empty group and
lease free. Tiered-JIT CPU is0.31/0.24/0.22s, main-thread CPU0.10/0.07/0.09s;
all other thread deltas are retained. This is attribution with profiling and
separate-image state, not a qualified before/after timing campaign.

## Findings and stopping decision

| Exact managed self leaf | Original | Expanded | Original control |
| --- | ---: | ---: | ---: |
| ExecuteUntilCall | 51 | 29 | 51 |
| ResolveName | 14 | 7 | 14 |
| CreateExecutableInterpreter | 6 | 0 | 6 |
| ExecutableValueStack.Push | 11 | 7 | 10 |

Other differences spread over pending calls, native transitions, storage
admission, arithmetic, CSV work and unknown leaves. Monitor leaves total38/25/44,
distributed over real owners; they do not identify a removable accounting lock.
Mapped unknown leaves include frame construction and managed stack admission.
All maps contain one malformed trailing record, reported rather than repaired;
chains can omit inlined/native-transition frames. Counts in such small samples
do not establish per-component causal deltas or a dominant redundant wrapper.

The complete helper path remains a material source-shape cost, consistent with
B58. Current source already binds ordinary operands into final local storage,
shares native arithmetic/index/truthiness paths across hosting modes and retains
real lexical identity. B61's rejected call-control record did not remove that
state. This capture does not justify replaying it, removing cancellation or
admission checks, caching a global value across Python rebinding, or inferring
that all helper overhead can disappear.

Stop this attribution after one source decision. The next design assessment
concerns executable operand transport across whole call paths, preserving the
existing engine, stable lexical contexts and suspension ownership. It must identify
actual instructions/storage that disappear before a prototype or timing is selected.
Existing B45 compiler evidence remains useful independently: scope facts are
already shared, while annotation and general abstract analysis deliberately use
different branch rules. Their merger needs a diagnostic contract, not a pass deletion.

[All identities, thread deltas, gates and observations](summary.json), the three
complete origin files and [raw capture](raw-capture.tar.gz), SHA256
`dfa7ac5f8cdf2723c94408f036c3c1de27b9b35ced4db2b56c2c62aac7ddfa86`,
preserve every event/map, source/catalog, collector/driver and cleanup result.
Portable retrieval omits control FIFOs and uses read access for root-owned perf
files; an initial incomplete retrieval was corrected without rerunning workloads.

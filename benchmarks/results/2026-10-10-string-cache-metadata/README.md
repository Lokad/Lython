# Lazy string cache metadata experiment, 2026-10-10

**Rejected.** Moving Unicode cache fields into lazy metadata reduces cumulative
managed allocation by 5.11% in the ASCII pipeline, but does not give a repeatable
latency improvement: the pipeline takes 2.38% more time in the first repetition
and 0.04% more in the second. The candidate remains isolated; production sources
and tests are unchanged. No full comparison lane ran and no collection was
repeated to seek a favorable result.

The preceding instruction-reference experiment's mixed pipeline result remains
open. This experiment does not explain its slower repetition or establish a
cause for the separate intermittent CSV lifetime failure.

## Change and producers

Baseline is `6fbbbbc2af1ebdce7739bc0e2bd99fddf73451e9`; isolated candidate is
`af3eaad2a9d73ffcd076a8ed594440f02040af8b`. The candidate changes only `PyString`
cache representation and its ownership/denial tests. Its common object replaces
two cache-array references and a committed-cache-byte field with one nullable
metadata reference. Ordinary ASCII strings do not allocate metadata. A separate
fixed-count layout diagnostic measures 88 bytes per baseline wrapper and 72 per
candidate wrapper, excluding payload, governor and reclamation entries.

The first Unicode cache reserves an additional 48-byte metadata charge before
allocation. The measured metadata object occupies 40 bytes. Completed caches
are published only after construction; denial or partial construction rolls back
new charges and preserves any earlier cache and tracked ownership coupon. Both
cache construction orders charge metadata once. This adds a first-cache budget
cost; lower ASCII allocation alone does not justify that tradeoff.

Construction estimates, ordinary limits, weak ownership registration, public
execution boundaries and checkpoints stay the same. Both independent producers
were frozen clean, built in Release with SDK 10.0.401/runtime 10.0.12, and checked
before collection on the dedicated four-core AMD EPYC 9V45 Ubuntu 24.04.4 VM.
Workers use ordinary workstation/Interactive GC, tiering and PGO, with no runtime
overrides. The supervisor alone disables tiering and explicitly clears that
setting when launching workers. CPython is the pinned PGO/LTO 3.13.16 build,
isolated with `-I -S`, ordinary GC/GIL and no experimental JIT.

## Declared short comparisons

Five cases were declared in order, then repeated once: empty invocation, basic
integer loop, ASCII pipeline, Unicode scan and fresh Unicode index cache.
Each micro uses the existing unchanged comparison command: identical precompiled
source, independent golden output, fresh guest state, public invocation and full
result projection. Compilation, transport and hashing are outside worker timers.
Each engine warms for one second, followed by two seconds of settling and seven
rotating-order batches calibrated to 25 ms. Results are diagnostic observations;
there is no noise qualification, confidence interval or certified Python ratio.

The canonical 14-case quick catalog is preserved exactly. One additional
diagnostic forces a fresh owned Unicode string and offset cache each invocation:

```python
owned = TEXT + 'a'
total = 0
for index in range(len(owned)):
    total += ord(owned[index])
print(len(owned), total)
```

`TEXT` is the existing supplementary Unicode fixture: 4,096 code points and
10,240 UTF-8 bytes. Its golden `4097 253748321\n` is calculated independently
from fixture code points. The ordinary scan iterates strings without forcing this
cache. [create-diagnostic-catalog.py](create-diagnostic-catalog.py) reproduces the
extended catalog from a fresh `--compare list --profile quick` export; its output
must be a new file. This extra case is outside milestone qualification.

Median times are microseconds per invocation. IQR/median columns report percent;
the change column is candidate time relative to baseline, so positive is slower.
Collection duration includes worker setup, warmup, verification and settling.

| Repetition / case | Baseline, us | Candidate, us | CPython, us | IQR/median baseline / candidate, % | Time change, % | Collection, seconds |
| --- | ---: | ---: | ---: | --- | ---: | ---: |
| 1 / Empty | 23.43 | 22.86 | 1.50 | 2.98 / 4.67 | -2.42 | 6.43 |
| 1 / Integer loop | 1621.55 | 1643.77 | 580.15 | 0.87 / 1.21 | +1.37 | 6.95 |
| 1 / ASCII pipeline | 334.64 | 342.59 | 41.29 | 8.72 / 3.63 | +2.38 | 6.88 |
| 1 / Unicode scan | 976.90 | 948.03 | 246.80 | 0.74 / 1.49 | -2.95 | 6.86 |
| 1 / Fresh Unicode index cache | 1147.37 | 1182.41 | 319.90 | 1.00 / 1.07 | +3.05 | 6.98 |
| 2 / Empty | 23.88 | 23.30 | 1.47 | 1.25 / 1.90 | -2.41 | 6.46 |
| 2 / Integer loop | 1635.32 | 1636.77 | 627.02 | 1.01 / 1.08 | +0.09 | 6.93 |
| 2 / ASCII pipeline | 332.39 | 332.54 | 41.51 | 2.34 / 3.83 | +0.04 | 6.92 |
| 2 / Unicode scan | 889.52 | 864.96 | 212.67 | 1.07 / 1.74 | -2.76 | 6.93 |
| 2 / Fresh Unicode index cache | 1186.17 | 1112.74 | 308.40 | 1.53 / 1.38 | -6.19 | 6.97 |

The smaller wrapper gives no repeatable target pipeline gain. Unicode scan is
about 3% faster in both repetitions, while fresh index-cache results are mixed.
Empty control changes are small amid similar variation. Keep all observations;
neither allocation savings nor a different workload's result establishes the
target latency improvement.

## Separate allocation and layout diagnostics

Another prospective declaration specified baseline then candidate, two-second
warmups and exactly 1,024 measured pipeline invocations each. One identical
private helper calls the frozen workers' existing `Verify`/`Batch` methods and
brackets 32 batches of 32 with `GC.GetTotalAllocatedBytes(precise: true)`.
No collection is forced and no JIT instrumentation is used. The delta includes
helper response serialization and all runtime threads; it is cumulative managed
allocation, not retained or peak memory, and cannot form a CPython memory ratio.

| Producer | Whole diagnostic, seconds | Managed allocated bytes | Collection delta, generations 0 / 1 / 2 |
| --- | ---: | ---: | --- |
| baseline | 2.58 | 659,055,880 | 39 / 0 / 0 |
| candidate | 2.60 | 625,354,552 | 37 / 0 / 0 |

Allocation falls by 33,701,328 bytes, or 5.1136%. The layout check separately
allocates 1,024 retained wrappers around an existing byte array after preparing
factory delegates; it excludes payloads, governors and registry bookkeeping.
Both layout diagnostics complete in under 0.02 seconds. Metadata allocation can
make cached Unicode strings larger even though common ASCII wrappers shrink.
These observations do not establish a latency or native/GC-cost improvement.

## Correctness, audit and disposition

Candidate Windows focused checks passed 66 Debug white-box, 100 Release
white-box and 1,019 Release public checks after an explicit matching probe build.
Frozen Linux baseline passed 97 white-box and 1,019 public Release checks;
candidate passed 100 and 1,019. New guards cover first-cache denial/retry,
second-cache denial preserving the earlier cache/coupon and metadata charged once
in either order. Existing aliases, partial-cache failure, reclamation and public
memory contracts remain checked. Broad local suites were not repeated for the
rejected candidate. The unchanged production/test trees retain their preceding
9,067-check full Debug pass; final delivery CI checks Release on both platforms.

Independent audits recompute all 30 medians/IQRs from raw clocks and counts,
validate 437 micro responses and 342 allocation responses / 10,824 invocations,
including warmup and verification, and confirm exactly 1,024 measured allocation
invocations per producer. All original catalog cases and six preceding
instruction-reference micro receipts are rehashed unchanged. Declared order,
non-overlap, scripts, loaded identities, source/fixture/golden digests and test
outcomes are retained.

All ten micros and both allocation services enforce a 30-second owned
process-group collection/cleanup cap (`RuntimeMaxSec=30`, `TimeoutStopSec=0`,
`KillMode=control-group`). Every collection succeeds. Final proof confirms all
14 preparation/collection services inactive/dead with zero main PIDs, previously
recorded worker PIDs absent, both frozen source trees clean and the VM lease
free. No benchmark lane was collected or qualified.

Next investigate string construction/ownership registration with a short
diagnostic that isolates a concrete boundary, preserving escaped split-item
lifetime, funding, denial recovery and all checkpoints. The earlier trace's
unresolved native leaves do not justify attributing its cost to registration.
Keep the accepted basic-loop improvement and the possible pipeline regression
visible in [the preceding diagnostics](../2026-10-09-pipeline-diagnostics/README.md).

Raw receipts, declarations, bounded helpers, source bundles, correctness results
and independent audits remain local under
`.git/agent-notes/string-cache-metadata-20261010/`. The candidate is retained in
the isolated `perf/string-cache-metadata` branch and is not merged.

| Evidence | SHA-256 |
| --- | --- |
| Extended diagnostic catalog | `f97227e6889b524c693bbe1e994bbd5e8fb1f208df7b48312cc598006c24eb25` |
| baseline library | `2e0ff05c867f152d3f93e862972ffa135df3e8940a815b9d21c735a6745e6139` |
| baseline allocation receipt | `bd0fe46b6d7d355a9052331ff153bbc7c924704fd4d54a9a770eebb871cb8a17` |
| baseline layout receipt | `049468f921df80b3e76b7d4b58e53a3d662dba42c56568f15aad7fea32f3b1e1` |
| candidate library | `9fc347c3422ac61ab0157dbd49384f469d96ee88338039c2447629a40b1d6ac1` |
| candidate allocation receipt | `186ba4c7d513add2b7b7dc0f0d1df76406acb466d218383ad2f7413bc4956ba6` |
| candidate layout receipt | `de8476158f7c70d6d996cbcf15a0a9fdc582d6b09eabd149a70258fc6935ec36` |

# B1346 Resolve executable jump targets on demand

Measured `067665c0e` uses one block table for target resolution,
operand depths and physical offsets. It removes the separate target
array, its initialization and the proactive pass over every block.
Reachability and linearization resolve targets only when referenced.
Cycle entry, protected entry, abrupt transfers and fallthrough keep
their existing meaning.

Compared with `fd41c741e` on November 22, 2026 UTC on the quiet
Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. Both 32-file images
use the same worker and optional PythonRe 0.4.0 adapter. Two
independently warmed processes per revision supply seven rotated
batches each after twelve seconds of warmup. Ranges span process
median pairings.

Compilation includes code-owner disposal. Time to result adds
execution with mediated RAM-backed I/O and result retirement.
Process CPU also covers request handling. Startup and final
teardown are separate.

| Workload | Boundary | Elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Sparse generated validator | Compile | −1.11 to +0.11% | −1.73 to +0.46% | 0.32 / 0.90% |
| Postponed nested factories | Compile | −1.25 to +0.01% | −3.31 to +0.30% | 0.85 / 0.42% |
| Inherited dataclass | Compile | −1.42 to +0.63% | −1.90 to −0.53% | 0.70 / 1.37% |
| Dead-heavy validator | Compile | −0.69 to +1.42% | −1.23 to +1.02% | 1.25 / 0.86% |
| Sparse generated validator | Time to result | −0.26 to +1.00% | −2.01 to +0.78% | 0.96 / 0.31% |

Retain the eliminated traversal and backing. The dataclass uses
less compiler CPU. Its elapsed range, the dead-heavy control and
validator time to result span zero. This comparison establishes
no broader elapsed improvement. The earlier table-only candidate's
adverse factory compilation is retained separately.
[B1344](../2026-11-22-shared-block-layout/README.md).

Local compilation allocates 856 fewer bytes for the dataclass,
104 fewer for the postponed factory and 72 fewer for the dead-heavy
control. The wide validator's whole-compile allocation varies. Both
compilation owners return to baseline. Exact published instructions,
diagnostic operand spans, exception routing, native segments and
storage extents match 83 code objects across 12 scripts.

Windows .NET 11 and Ubuntu .NET 10 each pass 362 white-box and
1,992 public focused Release checks. A native-call continuation
scenario failed in the first Ubuntu group run. The retained log
does not identify the cause. The scenario and the full focused
group pass on the same build in separate reruns. The reference and
candidate each pass three additional direct subprocess checks,
using matched versioned fixtures and the expected small managed
stack. The fixture's four code objects match exactly.

All 140 timed batches and both quiet gates passed in 353 seconds.
Both 32-file images match four CPython goldens. Workers joined and
the persistent archive verified under the lease after fixture cleanup.
Staging retired only the B1344 working core, whose bytes survive in
its pushed archive and frozen image.

[Timing](summary.json) · [Compiler allocation](compile-allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source and checks](checks-and-procedures.tar.xz).

# B1320 Fuse executable tape finalization

Copy reachable blocks into one exact-sized tape using bounded bulk
copies. Resolve ordinary and protected branch addresses while preparing
native segments. This removes the branch decision from the copy pass.
The native dictionary loop recognizer resolves its forward lookahead
while the back edge still contains a block address.

Compared `8c81fb8d2` with private `49fed6128` on November 22, 2026
UTC on the quiet Azure F4as_v7, Ubuntu 24.04.4, .NET 11 RC3. Two
independent processes per revision each supplied seven rotated batches
following 12 seconds of warm-up. Values are the range across pairings
of process medians. Negative changes favor the candidate. Compilation
includes code-owner disposal. Time to result includes compilation,
execution and cleanup of each result and code owner. Process CPU
includes the worker's request handling.

| Workload | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Sparse generated validator compilation | −0.39 to +0.10% | −0.89 to 0.00% | 0.13 / 0.36% |
| Postponed factory compilation | −1.76 to −1.37% | −1.99 to −0.60% | 0.33 / 0.06% |
| Inherited dataclass compilation | −1.61 to −0.18% | −1.41 to +1.02% | 0.47 / 0.98% |
| Dead-heavy validator compilation | +0.22 to +1.60% | +0.70 to +2.06% | 0.90 / 0.47% |
| Sparse generated validator time to result | −1.76 to −0.31% | −2.02 to +0.51% | 0.81 / 0.65% |

Keep the candidate private. The dead-heavy case consistently uses more
CPU even with its exact-size tape and flat local allocation. Representative
elapsed changes are small and mostly favorable. The compiler's native
segment preparation would have to reason about both block and physical
addresses for these gains. Keep the current separation. A design that
eliminates backing storage or another full pass could justify revisiting
layout. The initial local fixture identity mismatch was corrected before
measuring. Its failed validation is preserved alongside the checks.

Windows .NET 11 and Ubuntu .NET 10 each pass 410 white-box and 1,256
public focused Release checks. The inspection compares instructions,
regions, source metadata, calls and storage for 79 code objects across 11
fixtures. Local dead-heavy, factory, dataclass and native-loop allocation
is unchanged. All compile probes restore their allowance. Both frozen
worker images pass four CPython goldens and all 140 timed batches verify.
Workers joined and the archive was verified after fixture cleanup under
the quiet-VM lease.

[Timing](summary.json) · [Allocation](compile-allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source and checks](checks-and-procedures.tar.xz) ·
[Reference core](../2026-11-22-sparse-lexical-preparation/candidate-core.dll.gz).

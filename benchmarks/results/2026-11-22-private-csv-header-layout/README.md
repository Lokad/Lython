# B1355 Reuse the layout of an unexposed CSV header

Private `e04d22fc2` compares with `2f9f818b9` on November 22, 2026
UTC. A DictReader with an unexposed, automatically parsed header
remembers the layout once. It skips the two per-row name identity
scans while that proof remains valid. Property reads and assignments
clear the proof, including reentrant access during admission. Both
routes adopt the existing funded row capacity.

Both frozen 32-file images run on the quiet Azure F4as_v7 with Ubuntu
24.04.4 and .NET 11 RC3. Each revision uses two workers, eight seconds
of warmup per worker and seven rotated batches. Ranges span process
median pairings. Warm includes mediated RAM-backed reads and writes,
output verification and prior-result cleanup. Time to result also
includes compilation and code-owner disposal. Process CPU covers the
wider worker request. Startup and final teardown are separate.

| Complete script | Boundary | Elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Ordinary CSV invoice | Warm | −5.43 to +1.31% | −6.16 to +0.40% | 4.51 / 2.51% |
| Typed ASCII meter | Warm | +0.96 to +3.18% | +1.47 to +2.91% | 0.29 / 1.90% |
| Quoted policy invoice | Warm | −1.99 to +0.94% | −1.88 to +0.75% | 1.55 / 1.41% |
| Restock export | Warm | −3.62 to +1.44% | −4.32 to +1.47% | 1.80 / 3.38% |
| Ordinary CSV invoice | Time to result | −3.25 to +0.75% | −2.40 to 0.00% | 3.39 / 0.71% |

Keep the prototype private. All three affected warm workloads span
zero. The typed meter uses csv.reader and has no automatic DictReader
header. Its observed elapsed and CPU regression remains unexplained.
The added proof does remove two scans when valid. It also adds reader
state, and this comparison does not establish a whole-script gain.
Revisit if a design can remove the per-row work with the existing
layout or if independent attribution makes the opportunity clearer.

Managed allocation changes by 16 bytes on each affected workload and
174 bytes in the typed control diagnostic. Admitted peaks match and
all allowances refund. Windows .NET 11 and Ubuntu .NET 10 each pass
123 white-box and 405 public focused Release checks. The new tests
expose and mutate an automatic header during admission, verify the
changed keys, and keep earlier rows intact. Both images match four
CPython goldens. All 140 timed batches and both quiet gates pass in
279 seconds. All workers join and the persisted archive verifies
under the lease after fixture cleanup.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source and checks](checks-and-procedures.tar.xz).

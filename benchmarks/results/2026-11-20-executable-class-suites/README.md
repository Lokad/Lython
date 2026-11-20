# B1194 Executable class suites

The private prototype `c1d49c4da` runs class suites on the existing instruction
engine with the live class namespace. It removes both lowered statement
interpreters, their statement types and their duplicate protected-suite
cleanup. It retains lowered evaluation for expressions that use it. The patch
adds 111 and removes 2,296 production lines relative to `2dad15d55`.
Keep the prototype private while reducing its compilation cost.

Both complete images ran on the quiet Azure F4as_v7 with Ubuntu 24.04.4
and .NET 11 RC3 on 2026-11-20 UTC. Each process warmed for at least
10 seconds. Five rotated rounds targeted 0.5-second requests. Ranges cover
four pairings of duplicate process medians. Negative values indicate less
time in the candidate.

| Workload | Phase | Elapsed change | Process CPU change | Duplicate elapsed spreads, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Inherited dataclass configuration | Compilation | +20.36% to +21.45% | +20.21% to +23.51% | 0.13% / 0.77% |
| Inherited dataclass configuration | Compile to result | +9.83% to +15.16% | +8.68% to +14.57% | 1.87% / 2.93% |
| Inherited dataclass configuration | Warm | −1.44% to +1.16% | −0.62% to +0.60% | 1.01% / 1.61% |
| Eager annotated rules | Warm | −3.56% to −2.39% | −5.47% to −3.92% | 0.37% / 0.84% |
| File-backed CSV invoice | Warm | −1.17% to +0.43% | 0.00% to +2.66% | 0.32% / 1.30% |

Compilation includes disposal of the compiled owner. Compile to result adds
execution, mediated I/O, output checks and prior-result cleanup. Warm includes
execution and the same result boundary. Process CPU covers the wider request
loop. The two warm controls exercise unchanged source paths. Their movement
limits attribution of the small warm differences. The dataclass compilation
and compile-to-result costs exceed the in-lane control spreads in both
clocks. This run did not refresh CPython performance.

The initial class prototype allocated 22.62% more during dataclass compilation.
The consolidated prototype allocates 20.86% more (637,504 to 770,496 bytes).
Its warm allocation rises 0.27%. The dataclass accounted execution peak
remains 285,027 bytes. The four allocation controls are effectively unchanged,
and all five workloads return the execution allowance to baseline. Next,
attribute per-suite compilation allocation to separate instruction buffers,
control-flow preparation and persistent metadata. Pursue a shared first-pass
or finalization simplification if it explains a material portion of this cost.

The first class pass exposed class annotation writes and comprehension lambda
class-cell lookup differences. Correcting both restores the corresponding
regressions. The initial test migration also exposed tests that observed locals
through a namespace after slot retirement. The migrated fixtures use existing
mirroring for those assertions. Cleanup coverage includes pending class-body
I/O, exactly one file close and full allowance return. Focused Release checks
passed 4,889 white-box and 1,374 public tests on Windows .NET 11 and Ubuntu
WSL .NET 10. A final Windows subset on the exact measured source passed 235
white-box and 1,374 public tests. Five workload goldens and seven CPython
semantic probes in both public execution modes passed on both frozen images.

Both machine gates passed and all 100 timed batches completed. The archive
step produced a persistent archive but exited with status 1 on the 168 MiB
free-space floor. All workers had joined and the RAM fixtures were gone.
A separate recovery lease verified every archived file against the persistent
originals and streamed the archive to local storage. SHA-256 matched.
Free space at recovery was 118,665,216 bytes. Preserve the failed original
condition and use the timing as diagnostic evidence. The run was not repeated.

[Timing summary](summary.json) · [Allocation summary](allocation-summary.json) ·
[Original service status](terminal-status.json) ·
[Recovery receipt](archive-receipt.json) · [Observations](observations.tgz) ·
[Checks, source patch and procedures](checks-and-procedures.tar.xz) ·
[Local compilation attribution and refinement](compilation-attribution/README.md) ·
[Candidate core](measured-core.dll.gz) ·
[Reference core](../2026-11-20-direct-statement-emission/measured-core.dll.gz).

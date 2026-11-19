# B1134 route malformed integer conversion directly

R704 transfers an expected `int()` syntax failure to the existing Python
exception router without first throwing it through the CLR. A native helper
keeps its current instruction and uses the existing pending failure when it
materializes a frame. The standard binder retains its public error behavior.
The same parser checks the syntax, reserves large magnitudes and refunds
temporary storage across both paths.

The quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 compared
`7d120c9ea` with private `4eeaf9925` on November 19, 2026 UTC. Both frozen
32-file images matched the five stock CPython 3.13.16 goldens. Four workers
per case supplied five batches each. Warm elapsed includes mediated I/O,
golden checks and prior-result cleanup. Process CPU covers the wider batch.
Ranges cross both candidate workers with both reference workers. Negative
values mean less time.

| Complete workload | Warm elapsed change | Process CPU change |
| --- | ---: | ---: |
| Generated validator, six malformed fields | −19.87% to −18.53% | −15.43% to −14.46% |
| Same validator, all fields valid | −0.33% to +2.85% | +0.39% to +1.49% |
| Config manifest | +0.08% to +2.13% | −1.79% to −0.23% |
| CSV invoice | +0.49% to +2.18% | +1.11% to +2.94% |
| In-memory rule closures | −2.15% to −1.64% | −1.46% to −0.98% |

The malformed validator allocates 3,840 fewer bytes per run (4.88%) on
the local exact-source pair. The valid validator and other workloads have
flat allocation. All accounted peaks are identical. The stronger evidence
for the failure path comes from the malformed validator's consistent CPU
and elapsed reductions with less than 1% spread within each source. The
valid validator's reference spread is 2.93%. The unaffected rule control
also moved. The CSV result remains adverse.

Keep this source as a private checkpoint. Refine it by removing the
`long` to `BigInteger` to `long` round trip for successful short native
conversions; compare both validator inputs and CSV against the production
baseline that also includes any shared error-rendering correction.

The separate input contrast held source, file size and normalized sum
constant. The 0/3/6-malformed inputs allocated 67,016 / 73,000 / 77,232
bytes respectively in that earlier local run. Its first timing attempt
reached the deadline and remains visible. The quiet run measures the
source change within each case.

Both Windows .NET 11 and Ubuntu .NET 10 passed 1,250 focused Release
checks. The CPython probes also agreed on error identity, chaining, bare
raise, finally order and an uncaught failure through both APIs. An initial
fixture run lacked a shared workflow directory and the matching pinned SDK
for subprocesses. The corrected run used the original tests and budget
assertions. The 272-second quiet run joined all workers, removed guest
fixtures and verified its persistent archive before releasing the lease.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Checks and procedures](checks-and-procedures.tar.xz).

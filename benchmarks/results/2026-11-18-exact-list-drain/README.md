# Copy exact private list drains into already-admitted storage

B988 compares `45385d6c` with committed `bfe076110` / `610d403d` on the
quiet Azure F4as_v7 VM on 2026-11-18. Verified 32-file .NET 11 RC3 worker
images differ only in the core DLL. Both images passed all four complete
script and output-file goldens against CPython in preflight.

The existing drain admits capacity before reading the live source. An
unstarted exact-list cursor may then bulk-copy a bounded prefix to its
private snapshot without per-item dispatch or asynchronous enumeration.
The copy keeps cooperative cancellation and the snapshot stays funded until
all consumers finish. If a host admission callback grows the source beyond
paid capacity, the same live cursor handles the source and replacement
storage. Other inputs preserve normal iteration, side effects and cleanup.

| Complete script and phase | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Helper normalization, warm | −7.82 to −2.89% | −8.11 to −3.83% | 4.31 / 0.99% |
| Helper normalization, compile-to-result | −4.45 to −0.62% | −5.95 to −0.99% | 0.34 / 3.66% |
| Helper report, warm | −1.60 to −0.14% | −1.06 to +1.08% | 0.76 / 0.71% |
| CSV invoice, warm | −2.93 to +1.08% | −4.61 to +0.00% | 2.53 / 1.56% |
| Positional calls, warm control | −2.54 to −0.44% | −1.70 to −0.41% | 1.44 / 0.70% |

Ranges cover all pairings of two process medians per image. Negative
favors the candidate. The workers use eight-second warmups and five
measured batches with ordinary GC and tiering. Warm includes mediated
RAM I/O, output verification and prior-result cleanup. Compile-to-result
adds compilation and compiled-owner disposal. Startup, fixture reset and
protocol are outside elapsed. Process CPU also covers the latter two.

Local allocation and accounted peaks were effectively flat, and all
allowance balances returned to baseline. Windows Release passed 48 focused
white-box and 206 public cases per actual .NET target. WSL passed 43
white-box cases per target. Reentrant item replacement reaches the owned
snapshot and later list edits do not change it. Growth during admission
also keeps its original live semantics.

Retain the per-element drain removal and the helper warm direction. The
control moved favorably and the helper reference varied by 4.31%, which
limits the expressible magnitude. Helper compile-to-result looks favorable
with wider candidate spread. Report and CSV CPU or elapsed remain mixed.
This comparison between two Lython images establishes no new CPython ratio.

All jobs and quiet checks completed in 232.30 seconds. All workers joined,
fixtures were removed, and the exact SHA-verified observations were copied
locally. The VM retired three older aliases of `12643cec` and three already
committed raw-observation replicas after checking paths, inode, UID, link
counts and exact bytes. [Archived core](../2026-11-18-csv-line-number/candidate-core.dll.gz)

[Timing](summary.json) · [Allocation and reuse](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source and checks](checks-and-procedures.tar.xz)

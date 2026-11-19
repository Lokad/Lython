# B1118 admitted keyword table copies

Private `262dc8ad6` uses the original bucket links for compatible, already
admitted named keyword snapshots. It preserves the source's key objects and
insertion order, materializes private numeric values before sharing them,
and copies the tables into independently mutable storage. The reference is
`47fbd2c1b`. The patch reuses the native table copy used by dictionary copies.

Both complete 32-file images passed all five CPython workload goldens. Four
warm lanes ran on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3
on November 19, 2026 UTC. Warm elapsed includes mediated RAM-backed I/O,
golden checks and prior-result cleanup. Process CPU covers the wider worker
boundary. Changes span all four pairings of duplicate worker medians.

| Workload | Warm elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Named forwarding | +0.69…+2.33% | −0.30…+4.03% | 1.20 / 0.42% |
| Mutable bound forwarding | +0.23…+5.83% | 0.00…+6.72% | 0.04 / 5.54% |
| Inline dynamic keys | +0.58…+1.40% | +0.37…+1.49% | 0.44 / 0.38% |
| CSV invoice | −5.81…+3.34% | −5.37…+2.24% | 4.12 / 5.37% |

Named forwarding is slower across all elapsed pairings. The mutable-bound
effect size is uncertain because one candidate worker is slower than its
duplicate. Inline forwards show a smaller slowdown and the invoice spans
zero. Local allocation changes stay within 0.02%, accounted peaks match
and owners return to baseline. The implementation passed 94 focused checks
on Windows .NET 11 and Ubuntu .NET 10 before rebasing onto the measured
runtime. The changed source and tests match across the rebase.

Keep this prototype private. The compatible snapshot still validates every
key and allocates both tables. Replacing bucket reconstruction with a second
copy pass gives no measured improvement here. Revisit this area for a design
that can reuse already-funded immutable layout storage while preserving live
values, ordering, reentrant admission and independent mutations.

Both quiet gates and all 80 timed batches passed in 217.45 seconds. The
workers joined and guest fixtures were removed. The first preflight found
insufficient headroom with two available archived replicas. The successful
staging verified three committed core and observation archives, including
the earlier independently recovered B1056 archive, before retiring those VM
copies. The headroom floor was preserved and the final archive verified.

[Summary](summary.json) · [Observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Source, checks and procedures](checks-and-procedures.tar.xz).

# B1119 shared generator binding and frame storage

Generator functions now bind directly into the final frame slots. The
suspended frame keeps the same admitted workspace through its last user and
awaited cleanup. Closing or exhausting the generator detaches the frame
before returning its workspace to the existing pool. Escaped closure cells
retain their own funded storage. This removes the parameter-sized loan,
second array and parameter copy.

The comparison uses reference `47fbd2c1b` and prototype `1fe68f961`.
Source changes were retained at `765a5162e`. Both complete 32-file images
passed five CPython output and file goldens. Windows .NET 11 and Ubuntu
.NET 10 each passed 373 focused Release checks, including simultaneous
suspended generators, escaped closures, reentry, close, cancellation and
ownership.

Four warm lanes ran on the quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11
RC3 on November 19, 2026 UTC. The generator script creates and drains
rule factories that yield closures. Warm elapsed includes mediated
RAM-backed I/O, golden checks and prior-result cleanup. Process CPU
covers the wider worker boundary. Ranges span four pairings of duplicate
worker medians.

| Workload | Warm elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Generator rule factories | −1.85…−1.48% | −1.25…−0.51% | 0.09 / 0.28% |
| Ordinary rules | −1.66…+1.12% | −0.74…+1.37% | 1.36 / 1.45% |
| Annotated rules | −3.31…−0.51% | −4.22…−1.35% | 1.44 / 1.43% |
| CSV invoice | −1.96…+1.13% | −2.67…+1.37% | 1.55 / 1.57% |

The generator workload allocated 4,107,952 → 3,944,152 bytes per warm
invocation on the local diagnostic, a 3.99% reduction. Its accounted peak
rose 32 bytes to 4,310,808. The four allocation controls were effectively
flat. All five returned the shared allowance to baseline. The generator's
elapsed and process CPU show a small improvement. Ordinary and invoice
comparisons span zero. The annotated control improved in this run, with
unchanged allocation and no attribution to the generator path.

Both quiet gates and all 80 timed batches passed in 218.49 seconds.
Workers joined, guest fixtures were removed and the archive verified under
the lease. Installed-package and broad platform checks are batched at the
next source delivery.

[Summary](summary.json) · [Observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) ·
[Source, checks and procedures](checks-and-procedures.tar.xz).

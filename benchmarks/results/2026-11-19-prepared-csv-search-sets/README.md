# B1141 prepared CSV search sets

R711 builds the immutable delimiter, quote and escape byte searches once
per reader. The CSV grammar checks complete scalars and retains the
same cancellation boundaries and parser for every dialect. The reader
estimate includes 512 more bytes for its small search sets.

The reference is `fdacfbe20` and the measured prototype is `0c52b0956`.
The change was retained at `4c3f1ddfa` with the independently tested
escaped-newline fix from `65926a62d`. The measured images predate that fix.
Both complete 32-file images matched the four CPython goldens. Windows
.NET 11 and Ubuntu .NET 10 each passed 124 public and 118 white-box
focused checks on the prototype. Local cumulative allocation changed by
less than 0.01%. The accounted peak rose 512 bytes for each CSV workload
and was unchanged for text. Shared budgets returned to baseline.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 19,
2026 UTC. Warm elapsed includes mediated RAM-backed I/O, output and file
verification, and prior-result disposal. Compile-to-result adds compilation
and compiled-owner disposal. Process CPU covers the wider batch boundary.
Ranges compare all four pairs of duplicate process medians.

| Workload | Boundary | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| CSV invoice | Warm | −5.41…−1.70% | −4.62…0.00% |
| CSV keyword invoice | Warm | −9.62…−3.44% | −7.94…−1.05% |
| Long-field CSV validation | Warm | −4.05…−2.43% | −3.77…−0.89% |
| Text summary control | Warm | −1.39…+2.99% | −3.03…+1.06% |
| CSV invoice | Compile-to-result | −3.75…−1.18% | −5.26…0.00% |

All three warm CSV workloads are faster across the process medians. Keyword
invoice and long-field validation also show lower CPU on every pairing.
The ordinary invoice CPU includes one tie. Its reference elapsed medians
vary by 3.41%, and the keyword candidate medians vary by 3.86%. The text
control spans zero. Retain the eliminated setup and preserve these
uncertainties. The comparison measures two Lython revisions.

All 100 batches passed the goldens in 274.19 seconds. Both quiet gates
passed, all workers joined, and the VM verified the archive under the lease.
Two early preflights stopped at the disk floor. The successful stage retired
only exact Git-archived working copies. The first sparse local checkout
omitted sources required by the test worker and was corrected before the
focused checks.

[Summary](summary.json) · [Observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Source and checks](checks-and-procedures.tar.xz).

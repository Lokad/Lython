# Representative CPython checkpoint at ee448715-equivalent source

Five complete CSV, text, JSON/report, file-search and configuration scripts run
through the existing comparison workers. Compilation, warm execution and
compile-to-result have separate lanes: 15 jobs completed in 409.135 seconds.
The [current guide](../../../docs/BENCHMARKS.md) presents the useful timings;
[summary](summary.json) preserves every pairing, control spread and process CPU.

Measured production source matches `ee448715`; core
`b49e50a632ce931724077c314f263494a70952d8cdd2030466874e38c489f313`
was frozen before that commit, not rebuilt afterward. Exact source hashes survive
as `core-source.json` in the [owned archive](observations.tgz). This includes R178
terminal JSON transfer and R179 closed helper eligibility, not the later isolated
operand prototype. Normal adapter/package pins remain unchanged.

Reference: stock isolated CPython 3.13.16; .NET 11 RC3. Dedicated Azure F4as_v7,
Ubuntu 24.04.4, four AMD EPYC 9V45 vCPUs, 16 GiB/no swap. Four processes, two per
engine; five-second role warmups, seven rotated 150 ms rounds, ordinary GC/tiering.
Sources, fixtures, protocol and exact auxiliary identities are frozen. The matching
worker binaries also survive in the [R179 archive](../2026-11-10-handler-native-calls/quiet/observations.tgz).

The measured boundary includes mediated RAM I/O, complete stdout/file validation
and prior-result cleanup. Startup, fixture staging/reset, protocol and final cache
teardown are excluded. Compilation includes construction/disposal; warm reuses the
compiled program. Process CPU covers a wider interval. Lython uses efficient host
overrides while CPython uses builtin files; isolation/capability costs remain real.

Ranges span independent process medians; ratios include all four pairings, not
confidence intervals. Several rows are variable: complete CSV Lython controls
3.46%, text Python 4.13%, report Python 9.00%, search Lython 2.50%, config Python
2.67%. Report warm has elevated within-process variation despite close process
medians. Adverse and inconclusive evidence remains visible. The warm text gap
1.62–1.69× and search gap about1.58× remain material. Cross-checkpoint differences
do not identify single-change speedups or settle existing regression debt.

All identity, output, balance, quiet-host, joined-worker, RAM-unmount, owned-archive
and independent local SHA checks pass. Terminal status is cached; no workers or
fixtures remain. Archive SHA-256:
`403b1f7444d59afab57490b81aed72eb35ebcaa8c2e56883a3acd64a3bcfa27c`.
The finite owner was 540 seconds, with 30-second stop and margin. Persistent
catalog/core/auxiliaries were reused; one exact redundant compressed copy was
removed only after proving its bytes survive inside tracked canonical evidence.

The existing local summarizer initially lacked its root catalog copy; the exact
archived/hash-verified catalog was materialized before analysis. No observation,
source or timing changed. Commands and original conditions are in
[development evidence](development.tar.xz); [the prior guide](prior-BENCHMARKS.md)
preserves historical rows. Final runtime platform qualification is recorded
separately; publication remains postponed.

Delivery source `ee448715` is fully qualified by
[42727417102](https://github.com/Lokad/Lython/actions/runs/42727417102): Windows and
Ubuntu, actual .NET 11/.NET 10, original Stress and installed Release package
consumers all pass. The [cached terminal receipt](../2026-11-10-handler-native-calls/delivery-ci.json)
changes no measured source or observations. It does not qualify later prototypes.

# B1712c Text-control CPU attribution

Three unchanged frozen images run the complete nonlocal word report.
The reference is `6934aafb9`. The layout-only and prepared variants
are the images timed in B1712a and B1712b. Each 32-file image verifies
an independent CPython output and written-file golden before and after
sampling. The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on
November 25, 2026 UTC.

Each worker warms for at least eight guest-clock seconds. Linux perf
samples the whole process at 199 Hz for about eight seconds. Counts below
show the closest resolved Lython method in each sampled stack, normalized
by verified invocations.

| Image | Samples | Total samples per 1,000 runs | Dictionary loop | Word preparation | Executable dispatch |
| --- | ---: | ---: | ---: | ---: | ---: |
| Reference | 1,639 | 200.07 | 30.03 | 56.27 | 20.14 |
| Layout-only | 1,641 | 188.53 | 25.16 | 55.49 | 17.92 |
| Prepared consumers | 1,614 | 185.43 | 23.90 | 53.88 | 18.73 |

Neither dictionary updates nor instruction dispatch show a candidate
increase in this capture. The per-invocation counts reverse the previous
text timing result. A single instrumented worker per source, deeper warmup,
and incomplete inlining visibility limit attribution. These samples
identify no hot path to change. They leave the prior uninstrumented
regression in evidence and justify a single timing check with all three
images and two warmup depths in one collection.

Both quiet gates passed. All workers joined and the archive verified after
guest fixture removal. Included CPU covers reset, framing, mediated
RAM-backed I/O, verification and result cleanup.

[Image identity](identity.json) · [Normalized profiles](profile-summary.json) ·
[Attribution](attribution.json) · [Raw profiles](observations.tar.xz) ·
[Terminal receipt](terminal-status.json) · [Procedures](procedures.tar.xz).

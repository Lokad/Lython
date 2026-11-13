# Captured-text CPython checkpoint — B654

2026-11-13, fully qualified **31d65e01/e34cacaf**, fixedfec79 worker/catalog,
adapter0.4, actual.NET11RC3 versus CPython3.13.16 on quiet Azure F4as_v7/Ubuntu24.04.4.
The later R298 descriptor-owner correction is excluded. Same complete3,072-line /
35,840B helper/nonlocal-counter script; stdout/file goldens match every invocation.

Three materially stale phases refreshed using existing tools/images, four independent
roles (two per engine),12s warmup(5s compilation), seven rotated500ms rounds.
**173.802s**, all normal GC/tiering, quiet gates, identities, balanced ownership,
normal Closed/exit0, joined users and archive/SHA verification before RAM unmount.

| Phase | Lython | CPython | Lython / CPython | L / P control; maxIQR |
| --- | --- | --- | --- | --- |
| warm | 1.444–1.453 ms | 0.888–0.899 ms | 1.61–1.64× | L0.68%, P1.19% / 0.97% |
| compile | 44.390–44.589 µs | 54.191–54.480 µs | 0.81–0.82× | L0.45%, P0.53% / 0.77% |
| compile-run | 1.552–1.562 ms | 1.032–1.038 ms | 1.50–1.51× | L0.63%, P0.58% / 0.83% |

Ratio above one means slower; ranges cover independent medians and all four
pairings, not confidence intervals. Current warm execution remains1.61–1.64×
CPython and fresh1.50–1.51×, while compilation is faster. The tight controls support
further execution/whole-pipeline work; they do not establish a dominant internal
cost or attribute cross-collection changes to one optimization. All previous/adverse
rows survive in the prior guide and original result directories. No broad timing
or unchanged qualification replay, new framework, runtime source or dependency.

Warm excludes compilation; compile includes construction/disposal; compile-run
includes both. Command-v2 times mediated RAM-file I/O, full output validation and
prior-result cleanup. Startup, fixture staging/reset, protocol and final teardown
are excluded. CPython uses builtin I/O; Lython retains efficient host overrides and
capability/path checks. Not a pending-I/O, async CPU, RSS or embedding-tail result.

Last free267202560B; no active VM users/guest mounts at the cached terminal state.
OS240MiB/evidence12MiB floors remain. Publication/tagging stay held. Use this
confirmed gap to prioritize the next coherent text/construction/call redesign;
do not replay slower call controllers or infer boxing dominance from its existence.

[All rows](summary.json), [raw](observations.tgz), [collection/analysis](collection-sources.tar.xz),
[terminal](terminal-status.json), [custody](archive-receipt.json),
[prior guide](prior-BENCHMARKS.md), [same-source delivery](../2026-11-13-formatted-helper-slots/delivery/README.md).

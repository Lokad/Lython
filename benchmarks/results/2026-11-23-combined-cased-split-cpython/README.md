# B1539 compare the retained text paths with CPython

Lython `43688c753` and stock CPython 3.13.16 run the same reports
on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on
November 23, 2026 UTC. Each
report reads 3,072 lines. The short fixture is 35,840 bytes and the
mixed-width fixture is 107,520 bytes. This is the combined cased-split
and direct regex capture delivery source.

| Workload and phase | Lython | CPython | Lython / CPython |
| --- | ---: | ---: | ---: |
| Short nonlocal report, warm | 1.3133–1.3488 ms | 0.9373–0.9415 ms | 1.395–1.439× |
| Short nonlocal report, compile to result | 1.3371–1.4027 ms | 1.0808–1.0944 ms | 1.222–1.298× |
| Short nonlocal report, compilation only | 0.02957–0.02994 ms | 0.05780–0.05816 ms | 0.509–0.518× |
| Mixed-width histogram, warm | 1.5676–1.6514 ms | 1.7181–1.7210 ms | 0.911–0.961× |

Lython's warm workers spread 2.70% for the short report and 5.34% for
the mixed-width histogram. The mixed report is faster across all four
pairs, with an uncertain magnitude. The short report still spends more
time through result despite taking about half the compilation time.
[Earlier short comparison](../2026-11-23-closure-cpython/README.md) ·
[Earlier mixed comparison](../2026-11-23-forwarded-global-cpython/README.md).

Ranges pair the medians from two workers per engine. Each process warms
eight seconds before contributing seven rotated batches. Warm elapsed
includes applicable RAM-backed reads and writes, output verification and
prior-result disposal. Lython uses mediated file access and shared-budget
admission. CPython uses its native file APIs. Compile to result also
includes compilation and compiled-owner cleanup. Compilation alone
includes prior-code disposal. All clocks start in an already-running
worker. Fixture resets, framing and final exit sit outside elapsed.

[B1538](../2026-11-23-marked-cased-split/README.md) attributes the
implementation change to a matched Lython source comparison. All
112 batches, output/file goldens, full image hashes, quiet gates and
joined cleanup verify.

[Identity](identity.json) · [Timing](summary.json) ·
[Observations](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal receipt](terminal-status.json).

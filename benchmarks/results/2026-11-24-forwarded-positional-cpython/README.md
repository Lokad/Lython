# B1625 Forwarded calls against CPython

Five complete scripts compare the exact runtime and tests from
`6466fbb52` with stock CPython 3.13.16. The sealed 32-file Lython
image comes from the verified delivery patch. The quiet Azure F4as_v7
ran Ubuntu 24.04.4 and .NET 11 RC3 on November 24, 2026 UTC. Each lane
uses two separately warmed workers per engine, with five seconds of
warmup and seven rotated batches per worker. Ranges span all pairings
of worker medians. The ratio divides Lython by CPython.

| Script | Boundary | Lython | CPython | Elapsed ratio | Wider CPU ratio | Worker spread Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Generated rules with defaults | Warm | 0.94370–0.94559 ms | 0.67822–0.68434 ms | 1.379–1.394× | 1.376–1.394× | 0.20 / 0.90% |
| Generated rules with defaults | Compilation only | 0.02456–0.02493 ms | 0.04367–0.04376 ms | 0.561–0.571× | 0.564–0.574× | 1.51 / 0.21% |
| Generated rules with defaults | Compile-to-result | 0.94355–0.95683 ms | 0.73084–0.73476 ms | 1.284–1.309× | 1.255–1.318× | 1.41 / 0.54% |
| Generated rules with captured locals | Warm | 0.90121–0.94731 ms | 0.76582–0.77056 ms | 1.170–1.237× | 1.161–1.216× | 5.11 / 0.62% |
| Generated rules with captured locals | Compilation only | 0.02637–0.02653 ms | 0.04681–0.04700 ms | 0.561–0.567× | 0.560–0.566× | 0.59 / 0.42% |
| Generated rules with captured locals | Compile-to-result | 0.90904–0.91645 ms | 0.82989–0.83015 ms | 1.095–1.104× | 1.101–1.158× | 0.82 / 0.03% |
| Eager annotated rules | Warm | 0.45038–0.46467 ms | 0.40554–0.42793 ms | 1.052–1.146× | 1.051–1.168× | 3.17 / 5.52% |
| Eager annotated rules | Compilation only | 0.01623–0.01650 ms | 0.03068–0.03090 ms | 0.525–0.538× | 0.523–0.540× | 1.70 / 0.74% |
| Eager annotated rules | Compile-to-result | 0.45635–0.47615 ms | 0.43789–0.47307 ms | 0.965–1.087× | 0.980–1.091× | 4.34 / 8.03% |
| Short Unicode word-count report with a nonlocal helper | Warm | 0.86462–0.94592 ms | 0.94452–0.94618 ms | 0.914–1.001× | 0.896–1.004× | 9.40 / 0.18% |
| Short Unicode word-count report with a nonlocal helper | Compilation only | 0.02850–0.02854 ms | 0.05601–0.05602 ms | 0.509–0.510× | 0.505–0.506× | 0.15 / 0.02% |
| Short Unicode word-count report with a nonlocal helper | Compile-to-result | 0.93428–0.98361 ms | 1.08737–1.10104 ms | 0.849–0.905× | 0.843–0.892× | 5.28 / 1.26% |
| Quote-heavy CSV policy invoice | Warm | 6.22001–6.28147 ms | 6.25953–6.38260 ms | 0.975–1.004× | 0.966–1.036× | 0.99 / 1.97% |
| Quote-heavy CSV policy invoice | Compilation only | 0.06070–0.06112 ms | 0.10070–0.10165 ms | 0.597–0.607× | 0.605–0.617× | 0.68 / 0.94% |
| Quote-heavy CSV policy invoice | Compile-to-result | 6.44385–6.48525 ms | 6.51016–6.53856 ms | 0.986–0.996× | 0.977–0.977× | 0.64 / 0.44% |

The three rule scripts each use 2,048 groups. The Unicode report reads
3,072 lines and 35,840 bytes. The quoted policy invoice reads 4,096
records and 174,147 bytes. Warm execution includes applicable
RAM-backed host I/O, output and file verification, and previous-result
disposal. Compilation includes compiled-owner disposal.
Compile-to-result combines those operations in a running process.
Request framing and fixture reset are included in wider CPU. Startup
and final process teardown have separate clocks. Lython uses shared
memory admission and a host with ranged reads and appends. CPython
uses native file operations.

Defaulted rules remain 1.379–1.394× warm and 1.284–1.309× through
result. Captured rules remain 1.170–1.237× warm and 1.095–1.104×
through result. Eager annotated rules are slower warm, and their
through-result range crosses parity. The report's warm elapsed spans
parity with 9.40% Lython worker spread. Its through-result advantage
holds across workers. The quoted invoice remains near parity. These
diagnostic comparisons show the current external gaps. B1624 compares
the runtime change and its eager CPU cost against earlier Lython.

All 420 batches and both quiet gates passed. All five output and file
goldens, complete image hashes, worker joins, fixture removal and
persistent archive verified.

[Summary](summary.json) · [Identity](identity.json) ·
[Raw observations](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal receipt](terminal-status.json) ·
[Within-Lython comparison](../2026-11-24-forwarded-positional/framed-entry/README.md).

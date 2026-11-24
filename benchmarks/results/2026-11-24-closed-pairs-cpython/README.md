# B1597 Closed factory pairs against CPython

This checkpoint compares a 32-file Lython image at `67be15bd0`,
matching the runtime and tests delivered at `70b04c0af`, with stock
CPython 3.13.16. The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3
on November 24, 2026 UTC. Seven rotated batches follow eight seconds of
warmup in each of two workers per engine. Ratios divide Lython elapsed by
CPython elapsed over the four worker pairings. Above 1× means Lython takes longer.

| Complete script | Boundary | Lython | CPython | Lython / CPython | Worker spread Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: |
| Generated rules with defaults | Warm | 0.97193–0.97791 ms | 0.68186–0.68939 ms | 1.410–1.434× | 0.62 / 1.10% |
| Generated rules with captured locals | Warm | 1.56112–1.57848 ms | 0.76697–0.77192 ms | 2.022–2.058× | 1.11 / 0.65% |
| Short Unicode word-count report | Warm | 1.25140–1.30890 ms | 0.93864–0.94297 ms | 1.327–1.394× | 4.59 / 0.46% |
| Generated rules with defaults | Compilation only | 0.02384–0.02414 ms | 0.04129–0.04160 ms | 0.573–0.585× | 1.26 / 0.75% |
| Generated rules with defaults | Compile to result | 0.97730–0.97877 ms | 0.72738–0.73606 ms | 1.328–1.346× | 0.15 / 1.19% |
| Generated rules with captured locals | Compilation only | 0.02616–0.02673 ms | 0.04681–0.04689 ms | 0.558–0.571× | 2.18 / 0.17% |
| Generated rules with captured locals | Compile to result | 1.65077–1.66925 ms | 0.82070–0.82885 ms | 1.992–2.034× | 1.12 / 0.99% |

Both rule scripts construct and apply 2,048 groups and capture stdout.
The report reads 3,072 Unicode lines, 35,840 bytes, from RAM-backed
fixtures. Warm execution starts with compiled code and includes applicable
host-mediated I/O, verification and previous-result disposal. Compilation
only includes disposal of the compiled owner. Compile to result adds
one execution and its cleanup. Request framing and fixture reset sit
outside elapsed. Startup and final teardown have separate clocks.

Defaulted rules remain 1.41–1.43× warm against CPython after the within-Lython
improvement in B1596. The captured script remains around 2× both in
warm execution and through its result. Compilation is faster on both
workloads in these diagnostic measurements. The text comparison retains
greater same-source spread on Lython.

The first launch stopped at the disk preflight with 3,674,112 bytes free
against 4,018,176 required. The retry retired the VM copy of one raw B1594
file after checking its exact bytes against the pushed archive. The
original preflight and recovery record are in the procedures. All three
goldens, 196 timed batches, complete worker images, process joins, fixture
removal and quiet gates verify in the measured run.

[Timings and CPU](summary.json) · [Image identity](identity.json) ·
[Raw observations](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal status](terminal-status.json) ·
[Within-Lython comparison](../2026-11-24-closed-factory-pairs/native-entry/README.md) ·
[Earlier CPython checkpoint](../2026-11-24-annotated-factories-cpython/README.md).

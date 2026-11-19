# B1142 inline function binding state

R712 stores the live binding state inside the Python function. This removes
one allocation and the corresponding reference on each function definition.
Both execution paths keep reading the current defaults through reentrant
admission and suspension. Generated callable plans retain their existing
ownership. Function admission keeps its existing estimate.

The reference is `65926a62d` and the measured prototype is `757501cbe`.
The function sources were retained at `353ec074c` with the independently
tested CSV search preparation. The source pair measures the binding change
before that CSV optimization. Both complete images matched five CPython
goldens. Windows .NET 11 and Ubuntu .NET 10 each passed 378 public and 56
white-box focused checks. Local warm allocation fell 4.59% on ordinary
rules, 3.20% on annotated rules and 6.13% on captured rules at identical
accounted peaks. Both budget owners returned to baseline.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 19,
2026 UTC. Warm elapsed includes mediated RAM-backed I/O, output and file
checks and previous-result disposal. Compile-to-result also includes
compilation and compiled-owner disposal. Process CPU covers the wider
batch. Each range contains all four pairs of duplicate process medians.

| Workload | Boundary | Initial elapsed | Initial CPU | Repeat elapsed | Repeat CPU |
| --- | --- | ---: | ---: | ---: | ---: |
| Ordinary rules | Warm | −1.03…+0.74% | −1.33…−0.89% | — | — |
| Annotated rules | Warm | −1.19…−0.10% | −1.68…+2.00% | — | — |
| Captured rules | Warm | −2.96…+2.04% | −2.48…+1.03% | −1.61…+2.20% | −1.45…+2.50% |
| CSV invoice | Warm | −0.95…+4.33% | −1.39…+4.29% | −0.19…+1.89% | +1.35…+2.00% |
| Generated validator | Warm | −0.94…+0.38% | −0.07…+0.77% | — | — |
| Annotated rules | Compile-to-result | −8.42…+3.42% | −7.84…+3.17% | −3.01…−2.12% | −3.40…−1.33% |

The annotated compile-to-result reference workers varied by 9.83% initially.
With reversed order they differed by 0.06% and the candidate improved against
both references. Captured-rule warm elapsed stays inconclusive. Retain the
removed allocation and the annotated result. Keep the repeatable invoice
CPU cost in the delivery decision and refresh the combined revision against
CPython.

Both collections passed quiet gates and all 180 timed batches. Workers
joined and the VM verified the archives before releasing each lease. The
first repeat omitted the Python protocol helper and exited before any worker
started. The verified empty fixture mount was removed, and a new owned repeat
ran with the same images, cases and verified helper. The first sparse checkout
also needed the tracked representative-script fixtures before tests passed.

[Initial summary](summary.json) · [Repeat summary](confirmation/summary.json) ·
[Initial observations](observations.tgz) · [Repeat observations](confirmation/observations.tgz) ·
[Source and checks](checks-and-procedures.tar.xz).

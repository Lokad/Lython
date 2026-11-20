# B1218 Factory defaults against CPython

Measures production `a8a7c6ba2` with the source-matched `9741634b1`
image against stock CPython 3.13.16 on 2026-11-20 UTC. The quiet Azure
F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3. Each workload used two
processes per engine and five rotated rounds targeting 0.4-second batches.
The ratio is Lython elapsed divided by CPython elapsed. Ranges cross
all four pairings of process medians.

| Workload | Boundary | Lython | CPython | Ratio | Duplicate elapsed spreads, Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: |
| Eager nested factories | Warm | 1.426–1.440 ms | 0.610–0.612 ms | 2.33–2.36× | 0.99% / 0.43% |
| Postponed nested factories | Warm | 1.344–1.388 ms | 0.562–0.564 ms | 2.38–2.47× | 3.26% / 0.44% |
| Postponed nested factories | Compilation only | 0.02655–0.02666 ms | 0.04157–0.04169 ms | 0.637–0.641× | 0.43% / 0.30% |
| Postponed nested factories | Compile-to-result | 1.375–1.460 ms | 0.608–0.612 ms | 2.25–2.40× | 6.19% / 0.50% |
| CSV invoice | Warm | 6.610–6.679 ms | 6.307–6.352 ms | 1.04–1.06× | 1.04% / 0.72% |

Each factory script defines 4,096 functions across 2,048 inputs. CSV
processes 4,096 data records from 127,031 bytes. Warm includes mediated
RAM-backed I/O where used, output and file checks, and prior-result
cleanup. Compile-to-result adds one compilation and code-owner disposal.
Compile-only includes code-owner disposal. Reset, protocol framing,
process startup and final teardown are outside the guest clocks.
The wider process CPU includes framing and reset. Lython runs with
an unlimited shared allowance and active accounting. CPython uses its
native file APIs and ordinary runtime.

The warm nested scripts still take more than twice CPython's elapsed
time. The postponed compile-only lane is faster in Lython, but compiling
and running the script exceeds twice CPython's latency. Its two Lython
processes differ by 6.19% from compile to result. The CSV control remains
close to CPython. Earlier external comparisons keep their original
sources and controls. Use the [independent Lython source pair](../2026-11-20-default-operand-storage/README.md)
to attribute any gain to direct default storage.

All 100 batches, exact source and image identities, CPython identity,
goldens and both quiet gates passed. All workers joined, the persistent
archive verified under lease, and the guest fixtures were removed.

[Timings and controls](summary.json) · [Raw observations](observations.tgz) ·
[Source and procedures](procedures.tar.xz) ·
[Prior eager checkpoint](../2026-11-20-cpython-inline-captured-factories/README.md) ·
[Prior postponed checkpoint](../2026-11-20-postponed-factories-cpython/README.md).

# B1543 CSV meter reports against CPython

Lython `43688c753` and stock CPython 3.13.16 run the established explicit
and typed CSV meter scripts. The explicit script reads 4,096 records and
139,299 bytes, and the typed Unicode script reads 4,096 records and
223,267 bytes. The comparison ran on the quiet Azure F4as_v7 with
Ubuntu 24.04.4 and .NET 11 RC3 on November 23, 2026 UTC.

| Script and phase | Lython elapsed | CPython elapsed | Lython / CPython | Worker spread, Lython / CPython |
| --- | ---: | ---: | ---: | ---: |
| Explicit `float()`, warm | 3.20316–3.24848 ms | 2.84710–2.87783 ms | 1.113–1.141× | 1.41 / 1.08% |
| Explicit `float()`, compile to result | 3.38370–3.39006 ms | 3.03145–3.03511 ms | 1.115–1.118× | 0.19 / 0.12% |
| Explicit `float()`, compilation only | 0.04436–0.04460 ms | 0.07162–0.07192 ms | 0.617–0.623× | 0.54 / 0.42% |
| Typed Unicode, warm | 3.68252–3.75653 ms | 3.14820–3.14872 ms | 1.170–1.193× | 2.01 / 0.02% |

Ratios divide Lython elapsed by CPython elapsed and span all four pairings
of worker medians. Warm execution reuses compiled code. Both execution
phases include applicable RAM-backed I/O with mediated Lython file access,
golden output and file verification, and prior-result disposal. First use
adds compilation and compiled-owner disposal. Compile-only includes that
disposal. Reset, protocol and worker startup sit outside elapsed. Wider
process CPU is available in the raw observations. These are diagnostics.

All 112 batches, both quiet gates, joined cleanup and the complete
frozen image verify. The quiet staging reused the image from B1539. Its
first attempt stopped at the disk gate before worker launch. The collector
kept that gate and the second launch retired a redundant working core
with an exact archived copy. Both earlier collections remain intact.

## Local allocation attribution

The same Lython image and fixtures run on Windows x64 .NET 11 RC3. Each
worker warms before a six-second EventPipe allocation sample. The two
scripts complete 352 and 480 checked invocations across 26 sampled
batches. Tick weights approximate attribution across the request,
including resets and verification. Exact allocation totals and CPU
require separate measurements.

| Allocated type | Explicit `float()` weight | Typed Unicode weight |
| --- | ---: | ---: |
| Guest UTF-8 strings | 29.43% | 26.98% |
| Boxed doubles | 23.09% | 38.71% |
| Tuple backing arrays | 19.80% | 11.52% |
| Tuple shells | 11.83% | 11.55% |
| Byte arrays | 13.31% | 9.07% |

`PyTuple.FromSlots` accounts for nearly all object-array and tuple weights
and part of the boxed numbers. Typed numeric boxes also resolve to the
CSV unpacker. Explicit string weights split between unpacking and
publishing a deferred string. B1519 had attributed 58.51% of total weight
to explicit string shells, all in unpacking, on the earlier build.
These separate profiles identify sites to inspect for the next design.
Semantic tests and controlled timing can verify any work removed there.

[Image](identity.json) · [Timings](summary.json) · [Allocation sites](allocation-summary.json) ·
[Raw timing](observations.tar.xz) · [Procedures and allocation traces](procedures-and-allocation-traces.tar.xz) ·
[Terminal receipt](terminal-status.json) · [Earlier allocation attribution](../2026-11-23-retained-meter-allocation/README.md).

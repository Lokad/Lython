# B1274 map-based text report against CPython

Measured frozen Lython `b140244f0`, with the same production source as
`b42c4d508`, against stock CPython 3.13.16 on November 21, 2026 UTC.
The Azure F4as_v7 runs Ubuntu 24.04.4 and .NET 11 RC3.
[Casing source pair](../2026-11-21-pinned-unicode-casing/fold/README.md).

A helper passed to `map` normalizes and splits 3,072 lines from a
35,840-byte input file. The script counts 6,144 words and writes the
ordered report. Both engines verify stdout and file bytes against the
same goldens.

| Boundary | Lython | CPython | Lython / CPython |
| --- | ---: | ---: | ---: |
| Warm execution | 1.440–1.495 ms | 1.217–1.220 ms | 1.181–1.228× |
| Compilation alone | 0.02582–0.02682 ms | 0.04141–0.04188 ms | 0.616–0.648× |
| Compile-to-result | 1.505–1.580 ms | 1.291–1.316 ms | 1.144–1.223× |

Each boundary uses separate workers. The ranges cover every pairing of
two independently warmed process medians per engine. The two Lython
medians differ by 3.84% warm and 4.94% to result. CPython medians differ
by 0.20% and 1.94%. The highest elapsed IQR is 0.83%. Lython compiles this
script faster, while the whole script still takes longer. B1258 remains
an earlier result with its own frozen source and controls.
[Earlier comparison](../2026-11-21-cpython-callback-text/README.md).

Warm elapsed includes execution, mediated RAM-backed I/O, output
verification and prior-result cleanup. Compilation alone includes
code-owner disposal. Compile-to-result adds compilation and code-owner
disposal to execution. Startup, fixture reset and final teardown sit
outside the guest clocks.

All 84 batches and both machine checks passed in 227 seconds. The
complete worker image, stock CPython identity and goldens verified.
Workers joined, guest fixtures were removed and the archive verified
under the lease. The stage retired the unpacked duplicate of the
previous prototype observation after verifying the canonical archive.

[Summary](summary.json) · [Raw observations](observations.tgz)
· [Image verification and procedures](procedures.tar.xz).

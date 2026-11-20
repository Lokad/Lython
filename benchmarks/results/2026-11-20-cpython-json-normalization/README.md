# B1252 and B1253 JSON normalization against CPython

Both campaigns measure frozen Lython `5901c06bd` against stock CPython
3.13.16 on November 20, 2026 UTC. The Azure F4as_v7 runs Ubuntu 24.04.4
and .NET 11 RC3. The measured JSON cursor is retained at `ea04028b5`
alongside an independent regex correction. The exact 32-file image
reuses the [B1251 candidate](../2026-11-20-native-dict-item-cursor/README.md).

| Campaign, workload and boundary | Lython | CPython | Lython / CPython |
| --- | ---: | ---: | ---: |
| B1252 JSON normalization, warm | 2.763–2.773 ms | 2.396–2.417 ms | 1.143–1.158× |
| B1252 JSON normalization, compilation | 0.03536–0.03571 ms | 0.05254–0.05284 ms | 0.669–0.680× |
| B1252 JSON normalization, compile-to-result | 2.813–2.834 ms | 2.590–3.065 ms | 0.918–1.094× |
| B1252 generated validator, warm | 0.07285–0.07347 ms | 0.03984–0.04005 ms | 1.819–1.844× |
| B1253 JSON normalization, warm | 2.762–2.813 ms | 2.505–2.544 ms | 1.086–1.123× |
| B1253 JSON normalization, compile-to-result | 2.771–2.801 ms | 2.813–3.007 ms | 0.922–0.996× |

The normalizer reads and rewrites 3,072 records, 189,441 bytes each
direction. It strips and uppercases text and builds a new dictionary
for each record. The validator reads 96 fields from 1,714 bytes and
includes a negative-value branch. All its integer conversions succeed.

Ranges cross all pairs of two process medians per engine. Warm time
includes mediated RAM-backed I/O, validation of output and output
files, and disposal of the previous result. Compile-to-result adds
compilation and code-owner disposal. Compilation alone excludes guest
execution. Startup, fixture reset and final teardown are outside the
guest clocks. CPython and Lython have different containment boundaries.

The first compile-to-result lane spans parity, with 18.34% spread
between CPython medians. The confirmation uses the same images and
goldens, separate workers for each phase, reversed worker order,
16-second warm-up and longer batches. CPython compile-to-result spread
falls to 6.88% and its maximum IQR is 13.99%. The repeated warm result
still shows an elapsed gap. The two campaigns leave the compile-to-result
comparison sensitive to CPython process variation.

All 168 timed batches and both campaigns' quiet gates pass. The complete
worker images, CPython executable and worker protocol hashes, output
goldens and archives verify. Workers join and the guest fixture mount
is removed before releasing each VM lease.

[Initial summary](summary.json) · [Initial observations](observations.tgz) ·
[Confirmation summary](confirmation/summary.json) ·
[Confirmation observations](confirmation/observations.tgz) ·
[Initial procedures](procedures.tar.xz) ·
[Confirmation procedures](confirmation/procedures.tar.xz).

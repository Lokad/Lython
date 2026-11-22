# B1406 Current 0.11.0 checkpoint against CPython

Measured `55424591f` on November 22, 2026 UTC on the
quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. The comparison
uses stock CPython 3.13.16. Both engines verify the same six complete
scripts, inputs and outputs as B1397. The benchmark worker, protocol,
adapter and CPython executable match the earlier checkpoint by hash.
[B1397](../2026-11-22-retained-source-cpython/README.md).

| Complete script | Boundary | Lython | CPython | Lython / CPython | Worker spread Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: |
| Sparse generated validator, 96 guards | Compile | 1.453–1.459 ms | 1.826–1.832 ms | 0.793–0.799× | 0.42 / 0.31% |
| Sparse generated validator, 96 guards | Compile to result | 1.546–1.550 ms | 1.971–1.982 ms | 0.780–0.786× | 0.28 / 0.54% |
| Postponed factories, 4,096 definitions | Warm | 1.093–1.106 ms | 0.537–0.545 ms | 2.005–2.060× | 1.21 / 1.53% |
| Postponed factories, 4,096 definitions | Compile to result | 1.092–1.112 ms | 0.584–0.587 ms | 1.860–1.904× | 1.87 / 0.46% |
| Quote-heavy CSV invoice, 4,096 records | Warm | 7.244–7.303 ms | 6.256–6.324 ms | 1.145–1.167× | 0.82 / 1.09% |
| Unicode word count with a nonlocal helper, 3,072 lines | Warm | 1.512–1.528 ms | 0.944–0.951 ms | 1.590–1.620× | 1.06 / 0.80% |
| Expanded JSON Lines export, 3,072 records | Warm | 5.428–5.461 ms | 6.462–6.483 ms | 0.837–0.845× | 0.60 / 0.32% |
| Small JSON configuration edit | Compile to result | 0.109–0.110 ms | 0.125–0.126 ms | 0.862–0.882× | 1.53 / 0.72% |

Ratios divide Lython elapsed by CPython elapsed. Above 1× means Lython
takes longer. Ranges span pairings of two independent process medians
per engine. Each worker warms for at least eight seconds and supplies
seven rotated batches. Warm includes applicable RAM-backed reads and
writes, output/file verification and disposal of the previous result.
Lython uses host-mediated I/O with a shared allowance. CPython uses
native file APIs. Compile includes compiled-owner disposal. Time to
result includes one compile, one run and their cleanup. Startup,
request framing, fixture reset and final teardown sit outside these
elapsed clocks. Wider process CPU includes the worker request.

| Same workload | Boundary | Previous B1397 ratio | B1406 ratio |
| --- | --- | ---: | ---: |
| Sparse generated validator, 96 guards | Compile | 0.787–0.807× | 0.793–0.799× |
| Sparse generated validator, 96 guards | Compile to result | 0.789–0.791× | 0.780–0.786× |
| Postponed factories, 4,096 definitions | Warm | 1.873–1.963× | 2.005–2.060× |
| Postponed factories, 4,096 definitions | Compile to result | 1.780–1.870× | 1.860–1.904× |
| Quote-heavy CSV invoice, 4,096 records | Warm | 1.149–1.159× | 1.145–1.167× |
| Unicode word count with a nonlocal helper, 3,072 lines | Warm | 1.660–1.667× | 1.590–1.620× |
| Expanded JSON Lines export, 3,072 records | Warm | 0.819–0.842× | 0.837–0.845× |
| Small JSON configuration edit | Compile to result | 0.867–0.875× | 0.862–0.882× |

The two checkpoints ran at different times. The ratios show the
external gap at each measured revision. Use the paired Lython source
experiments to judge individual changes. This run qualifies no new
startup comparisons.

All 224 batches and both quiet gates passed in
424 seconds. The archive hash was verified under
the lease with all workers joined and guest fixtures removed. The
initial storage check refused staging. The successful attempt retired
only byte-verified contents already archived with their images or
measurements. The refusals and procedure are preserved.

[Timing and CPU ratios](summary.json) · [Source and image hashes](source.json) ·
[Raw observations](observations.tgz) · [Verification and procedures](procedures.tar.xz).

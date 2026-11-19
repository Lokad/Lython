# B1106 validator and manifest against CPython

This comparison uses `27f68744b`, the retained tokenization revision.
The 32-file image comes from source-equivalent `050e2dc5e`, with
core SHA-256 beginning `22c49edc`. It ran on the quiet Azure F4as_v7
with Ubuntu 24.04.4 and .NET 11 RC3 against stock CPython 3.13.16 on
November 19, 2026. Each range covers the combinations of duplicate
worker medians. Lython/CPython is the elapsed ratio. Above one means slower.

| Workload | Boundary | Lython | CPython | Lython / CPython |
| --- | --- | ---: | ---: | ---: |
| Generated validator | Compilation | 2.367–2.402 ms | 1.850–1.857 ms | 1.27–1.30× |
| Generated validator | Compile-to-result | 2.569–2.600 ms | 1.996–2.008 ms | 1.28–1.30× |
| Configuration manifest | Compilation | 0.16381–0.16541 ms | 0.15707–0.15763 ms | 1.04–1.05× |
| Configuration manifest | Compile-to-result | 0.4569–0.4577 ms | 0.4615–0.4753 ms | 0.96–0.99× |

The validator remains slower than CPython. The manifest is close on
compile-to-result, with a 3.00% spread between CPython worker medians. The
paired Lython comparison establishes the change's effect. The external
checks describe each measured revision and its own controls.
[Paired B1105](../2026-11-19-token-source-transport/README.md) ·
[Earlier B1102](../2026-11-19-retained-defaults-cpython/README.md).

Compilation includes disposal of compiled storage. Compile-to-result adds
execution, mediated RAM-backed I/O, output and file validation and result
cleanup. The Lython host uses shared memory accounting. CPython uses native
file APIs. Startup, fixture reset and request framing are outside guest
elapsed. Process CPU covers the wider request. Warm execution has its own
measurements in the benchmark guide.
[Benchmark guide](../../../docs/BENCHMARKS.md).

All 80 timed batches and both quiet gates passed in 81.62 seconds.
Workers joined, guest fixture storage was removed and the observation
archive was verified under the lease.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Procedures](procedures.tar.xz).

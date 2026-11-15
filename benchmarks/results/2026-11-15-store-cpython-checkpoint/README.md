# Representative CPython checkpoint

2026-11-15, committed **2865f619 / frozen55ba4c8a**, stock **CPython3.13.16**.
Full runtime qualification was active at capture, separately from these measurement
gates. This is a diagnostic external baseline, not a causal comparison with earlier
Lython revisions or a claim that the performance objective is complete.

| Complete script / phase | Lython | CPython | Lython / CPython |
| --- | --- | --- | --- |
| CSV invoice / warm | 6.637–6.728ms | 6.087–6.144ms | 1.08–1.11× |
| CSV invoice / compilation | 0.100–0.101ms | 0.094–0.094ms | 1.07–1.08× |
| CSV invoice / compile-to-result | 6.883–6.972ms | 6.249–6.276ms | 1.10–1.12× |
| Captured text helper/counter / warm | 1.205–1.255ms | 0.885–0.898ms | 1.34–1.42× |
| JSON aggregation helpers / warm | 3.467–3.563ms | 4.098–4.117ms | 0.84–0.87× |
| File search / warm | 0.272–0.273ms | 0.152–0.155ms | 1.76–1.80× |
| Six-helper manifest / compilation | 0.198–0.200ms | 0.155–0.158ms | 1.25–1.29× |
| Six-helper manifest / compile-to-result | 0.497–0.498ms | 0.463–0.470ms | 1.06–1.08× |

Ratio above one means slower. Two independent process medians/engine; five rotated
~0.5s batches after 5s full-request warmup, 12s for text. Text Lython controls move
4.14%, JSON2.77%; other unchanged spreads and every IQR remain in the
[summary](summary.json), with all raw samples. These limit exact portable gaps.
Captured text includes a nonlocal counter; it does not isolate call overhead.
There is no unchanged retiming to suppress adverse evidence.

CSV4,096records/127,031B; text3,072lines/35,840B; JSON4,096records/110,592B;
search2,049lines/42,010B, early stop1,025; manifest833Binputs/1,877Bsource.
Quiet Azure F4as_v7, Ubuntu24.04.4, four EPYC9V45vCPUs,16GiB/no swap,
.NET11RC3 workstation GC/normal tiering; CPython normal GIL/GC.

Warm clocks include synchronous Run, host-mediated RAM I/O, complete stdout/file
goldens and prior-result cleanup. They exclude compilation/startup, fixture reset,
protocol and final cache teardown. Compilation includes prior compiled-owner
cleanup; compile-to-result includes compilation, one execution and per-invocation
compiled-owner disposal. Wider process CPU includes reset/protocol. Separate
phase medians are not additive. The host completes I/O synchronously and checks
capabilities/shared budget; CPython native buffered files have no equivalent
containment. No startup, pending-I/O, RSS/heap, contention or embedding-tail claim.

**B807 succeeds295.766s**. Full image/catalog hashes, goldens, budget reuse, quiet
before/after, Closed/exit/join and archive verification under lease pass. No VM
workers or RAM fixtures remain. Terminal cached; never query again. The first
staging attempt references an already-retired catalog and refuses before mutation;
its exact scripts/payload/error remain. Corrected staging links existing verified
catalogs, preserving every selected source/fixture/golden hash. Only exact committed
B805/B806 working duplicates are retired after fresh path/UID/hash/link/all-alias
proof. Current images, toolchains, canonical evidence and disk floors stay.

[Raw observations](observations.tgz), [identity](identity.json),
[original conditions/procedure/inputs](procedure-and-original-condition.tar.xz),
[previous guide](prior-BENCHMARKS.md). Source/binary proof and focused correctness
are in [prepared stores](../2026-11-15-prepared-store-targets/README.md).

[Subsequent full runtime qualification passes](../2026-11-15-prepared-store-targets/delivery/README.md).

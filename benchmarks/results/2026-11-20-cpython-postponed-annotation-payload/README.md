# B1240 postponed factories and shipment report against CPython

Measured Lython `785640f8e` against stock CPython 3.13.16 on
November 20, 2026 UTC. The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and
.NET 11 RC3. Both engines used the same complete scripts and verified
fixtures. The runtime image and all 32 files are identified in the
archive. The Lython image is built from the delivered revision.

| Workload and phase | Lython | CPython | Lython / CPython |
| --- | ---: | ---: | ---: |
| Postponed factories, warm | 1.163–1.214 ms | 0.5362–0.5376 ms | 2.16–2.26× |
| Postponed factories, compilation | 0.02519–0.02552 ms | 0.04000–0.04005 ms | 0.629–0.638× |
| Postponed factories, compile-to-result | 1.195–1.241 ms | 0.5782–0.5810 ms | 2.06–2.15× |
| Shipment using `str.format_map`, warm | 3.622–3.724 ms | 2.851–2.900 ms | 1.25–1.31× |

The factory processes 2,048 inputs and creates 4,096 functions. The
shipment reads 3,072 JSON records from 189,441 bytes and writes a report.
Warm elapsed covers guest execution, mediated RAM-backed file I/O where
used, golden checks and previous-result disposal. Compile-to-result also
includes compilation and code-owner disposal. Compilation only excludes
guest execution. Process startup, fixture reset and final teardown are
outside the guest clocks. The process CPU observations cover the wider
worker request.

The ratio is Lython elapsed divided by CPython elapsed. The ranges cover
four pairings of the two process medians per engine. The Lython factory
warm workers differ by 4.35%, and the shipment's maximum within-worker
IQR is 4.94%. Both machine checks and all 112 batches passed. The workers
joined, guest fixtures were removed and the persistent archive verified
under the lease. These comparisons describe the delivered revision
against CPython. The separate [B1238/B1239 comparison](../2026-11-20-compiled-annotation-payload/README.md)
measures the annotation change between Lython revisions.

[Timing and controls](summary.json) · [Raw observations](observations.tgz) ·
[Procedures](procedures.tar.xz).

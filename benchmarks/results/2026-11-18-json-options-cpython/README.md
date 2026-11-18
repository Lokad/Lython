# Configuration scripts against CPython

B973 measured bdab88c54, frozen core 7170869d, on 2026-11-18 UTC. The quiet
Azure F4as_v7 VM ran .NET 11 RC3 and stock CPython 3.13.16 with PGO/LTO and
GIL enabled. The optional adapter stayed at 0.4.0. All 32 worker files and
the three-case catalog passed hash and full-output verification.

The config edit uses the same 181-byte, 16-path fixture as the earlier
checkpoint. The manifest reads 833 bytes across two files with four declared
targets and writes two complete JSON documents. The text control uses the
same 52,224-byte, 4,096-line fixture and source as the earlier 2e585229 timing.

| Workload | Phase | Lython | CPython | Lython / CPython | Lython / CPython control spread |
| --- | --- | ---: | ---: | ---: | ---: |
| Configuration edit | Compilation only | 0.0362–0.0368 ms | 0.0341–0.0344 ms | 1.05–1.08× | 1.56 / 0.97% |
| Configuration edit | Warm | 0.0765–0.0773 ms | 0.0616–0.0623 ms | 1.23–1.25× | 1.08 / 1.09% |
| Configuration edit | Compile-to-result | 0.135–0.136 ms | 0.124–0.124 ms | 1.09–1.10× | 0.34 / 0.17% |
| Configuration manifest | Compilation only | 0.198–0.198 ms | 0.157–0.157 ms | 1.26–1.26× | 0.36 / 0.00% |
| Configuration manifest | Warm | 0.189–0.192 ms | 0.154–0.156 ms | 1.21–1.24× | 1.42 / 1.22% |
| Configuration manifest | Compile-to-result | 0.489–0.496 ms | 0.462–0.473 ms | 1.03–1.07× | 1.41 / 2.39% |
| Text normalization | Warm | 0.774–0.785 ms | 0.725–0.737 ms | 1.05–1.08× | 1.38 / 1.53% |

Two processes per engine used eight seconds of rotated warmup and five
measurement rounds. Elapsed ranges cover all pairings of the two process
medians. Compilation includes compiled-owner disposal. Warm includes
mediated RAM I/O, stdout/file checks and prior-result cleanup. Compile-to-result
adds compilation and compiled-owner disposal within the invocation. Startup,
fixture resetting, request framing and final cache teardown are outside the guest
clocks. Process CPU, which also includes wider worker costs, is archived.

The largest same-image difference was 2.39% for CPython's manifest
compile-to-result. The largest process IQR was 1.65%. The text control's CPython
time was 0.725–0.737 ms here and 0.501–0.503 ms in the earlier collection.
Both controls were stable within their own collection. This between-collection
movement remains unexplained. The ratios describe these images and conditions.
No source-change speedup follows from comparing the two checkpoints.

All seven jobs completed in 318.05 seconds. Both quiet endpoints, verified
output, worker joins, fixture release and archive SHA checks passed. The staging
lease verified six archived observation replicas against their committed bytes,
owners, paths and full link sets before retiring those replicas.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Verification and procedure](verification-and-procedures.tar.xz)

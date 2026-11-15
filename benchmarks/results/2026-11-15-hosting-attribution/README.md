# Captured-text hosting CPU attribution

R389/B776, 2026-11-15 UTC, unchanged **810379be / frozen 8fbfa3f0**. Two bounded
captures of the complete 3,072-line captured-text script, through `Run` and awaited
`RunAsync` with completed host I/O. This attributes the [previously measured API
gap](../2026-11-15-whole-script-hosting/README.md); the instrumented capture is not
another controlled elapsed comparison.

| Capture | Invocations | Guest-thread CPU / invocation | Other-thread CPU / invocation | Guest / other leaves | Unknown guest leaves |
| --- | ---: | ---: | ---: | ---: | ---: |
| Run | 4,096 | 1,274 µs | 95 µs | 1,034 / 66 | 77 |
| RunAsync | 4,096 | 1,260 µs | 171 µs | 1,025 / 122 | 81 |

Zero ambiguous guest leaves. Other CPU is predominantly thread-pool work plus
about 0.06 s tiered JIT in each capture. Most other-thread leaves are unresolved
kernel/runtime frames; they do not identify a specific queueing instruction.
Guest file-line inclusive counts rise from 89 to 138 while text-operation counts
remain 268/269. Inclusive counts overlap and include useful work. Guest instruction,
dictionary accumulation and native-helper work remain substantial in both APIs.

Interpretation: these captures do not reproduce the earlier elapsed ratio or prove
that all extra CPU is removable. They suggest invocation scheduling/completion is
a concrete candidate, alongside per-line async work. Source shows an outer public
async wrapper resumes ownership cleanup after the dedicated worker completes.
A bounded prototype can move that complete ownership lifetime into the existing
scheduled operation, retaining asynchronous caller publication and genuine I/O
suspension. Do not create another dispatcher or remove effective protection.

One 5 s, 199 Hz cpu-clock/fp capture per API after eight guest-seconds of warmup;
all-thread stacks and CPU are retained, including reset/protocol/validation outside
the guest execution clock. Quiet Azure F4as_v7, Ubuntu 24.04.4, four EPYC 9V45 vCPUs,
.NET 11 RC3, ordinary workstation GC/tiering. Only perf-map diagnostics are enabled.
No allocation, startup, heap/RSS, embedding-tail, CPython or speedup claim.

Exact identities, full goldens, quiet-before/after, shared balance, Closed/join and
archive gates pass. Every RAM trace byte is archived and hash-verified persistently
under the lease before unmount; no users or fixtures remain. Two exact working JSON
copies were retired only after checking their bytes against committed archives and
fresh VM metadata. All archives, images, catalogs and toolchains remain; 254/252 MiB
floors are unchanged. Final free space is 265,850,880 B. No collection restart.

[All mapped samples and thread CPU](attribution.json), [raw traces and machine gates](observations.tgz),
[analysis and launch procedures](procedures.tar.xz), [retirement custody](custody.json),
[archive proof](archive-receipt.json), [terminal state](terminal-status.json).

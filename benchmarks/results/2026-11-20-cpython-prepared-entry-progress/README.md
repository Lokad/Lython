# B1229 Prepared function entry against CPython

Measures `46ccc2efa` with the exact source-matched `eaf4d65c1` image
against stock CPython 3.13.16 on the quiet Azure F4as_v7, Ubuntu 24.04.4
and .NET 11 RC3, on 2026-11-20 UTC. Each workload uses two processes per
engine and five rotated rounds targeting 0.4-second batches. The ratios
cross the four pairings of process medians and divide Lython guest
elapsed by CPython guest elapsed.

| Workload | Boundary | Lython | CPython | Ratio | Duplicate elapsed spreads, Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: |
| Nested annotated factories | Warm | 1.460–1.506 ms | 0.623–0.632 ms | 2.31–2.42× | 3.16% / 1.51% |
| Postponed nested factories | Warm | 1.387–1.446 ms | 0.583–0.599 ms | 2.31–2.48× | 4.28% / 2.76% |
| Postponed nested factories | Compile-to-result | 1.421–1.474 ms | 0.630–0.644 ms | 2.21–2.34× | 3.71% / 2.23% |
| Postponed nested factories | Compilation only | 0.02708–0.02797 ms | 0.04281–0.04315 ms | 0.628–0.653× | 3.27% / 0.80% |
| CSV invoice | Warm | 6.877–7.224 ms | 6.588–6.618 ms | 1.04–1.10× | 5.04% / 0.45% |

Each factory workload defines 4,096 functions across 2,048 inputs.
The invoice reads 4,096 data records from 127,031 bytes. Warm includes
host-mediated RAM-backed file I/O where used, output and file goldens,
and prior-result cleanup. Compile-to-result adds one compilation and
code-owner disposal. Compile-only includes compiled-code disposal.
Reset, startup, request framing and final teardown sit outside guest
clocks. Process CPU covers the wider request. Lython uses an unlimited
shared allowance with accounting active. CPython uses native file APIs.

Both nested workloads remain above 2.3× CPython's warm elapsed.
Postponed compilation is about 0.63–0.65×. The warm invoice is close
to CPython with 5.04% spread between Lython process medians. Comparisons
against earlier Lython sources belong to [B1225/B1226](../2026-11-20-prepared-entry-progress/README.md).

All 100 batches and both machine checks passed. The complete worker
image and CPython executable identities matched. Workers joined, fixtures
were removed and the persistent archive was verified under the lease.

[Timings and controls](summary.json) · [Raw observations](observations.tgz) ·
[Source match and procedure](procedures.tar.xz).

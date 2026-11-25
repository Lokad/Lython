# B1666 Shipment callback and sparse validator CPU after direct zip unpack

The profile uses the exact B1665 candidate image, whose core source matches
`ac9f9211b`. The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on
November 25, 2026 UTC. The unchanged worker warms for at least eight seconds,
samples process CPU at 199 Hz and verifies each complete golden before and
after the batch. Shipment uses warm execution with mediated RAM-backed input.
The sparse validator uses compile-only batches. Counts describe the worker
request as a whole, including work outside the execution clocks.

| Workload | Invocations | All samples | Resolved runtime ancestry | Nearest frontend samples |
| --- | ---: | ---: | ---: | ---: |
| Shipment callback report, warm | 2,560 | 1,266 | 1,230 | 8 |
| Sparse validator, compilation | 4,864 | 1,215 | 3 | 1,193 |

The shipment report has 85 nearest `PyMapIterator.TryMoveNext` samples,
following 77 in B1664 and 60 in the initial B1666 partial capture. Its two
maps call the same compiled conditional with ordinary positional arguments.
The next experiment passes operands directly and keeps the existing binder
for general calls. The counts include source pulls and do not isolate the
cost of building a call packet. The existing fixed-unpack destination still
accounts for 25 nearest samples.

The validator has 364 nearest parser samples, 111 tokenization samples,
190 executable compiler samples and 68 executable stack-layout samples.
Local-binding diagnostics account for 34 nearest samples, including six
for inserting assignment facts. The profile favors investigating a larger
parser or executable-lowering pass before changing assignment storage alone.
Sampling is diagnostic and does not establish an achievable speedup.

Both quiet gates pass for the final capture. Workers and profiler join, guest
fixtures are removed and the persistent archive verifies under the lease.
The first two runs exhausted disk during the validator capture. Their partial
files and service journals are preserved. The final run compressed symbol
maps as they were saved and started with more than 32 MiB free after 71
older build outputs were copied, verified and removed from the VM. The
workloads, images, profiler and sample windows stayed the same.

[Attribution](attribution.json) · [Identity](identity.json) ·
[Raw observations](observations.tar.xz) · [Terminal receipt](terminal-status.json) ·
[Failed conditions and procedures](failed-conditions.tar.xz) · [Final procedure](procedures.tar.xz).

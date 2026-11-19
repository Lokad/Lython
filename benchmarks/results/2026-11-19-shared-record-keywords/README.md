# B1123 shared record keys for keyword forwarding

Dense CSV and JSON records already use an admitted immutable key table.
The forwarded `**row` dictionary now shares that layout under the same
execution owner. It takes an independent snapshot of current values and
keeps the original key identities and insertion order. This removes a
per-forward key table and bucket rebuild. The existing path serves other
mapping shapes. Admission still precedes copying and the source is
revalidated afterwards for structural changes caused by reentry.

The reference was `630101daf` and the isolated prototype was `b982742f5`.
The measured runtime change was retained at `647a0f9b5`. Both complete
32-file images passed five CPython output and file goldens. Windows
.NET 11 and Ubuntu .NET 10 each passed 116 distinct focused Release
checks, including independent mutations, aliased values, reentered
deletion, refused admission and allowance refunds.
The integrated source at `b4a1248e2` passed 586 focused checks on each
platform, including the generator and regex provider regressions.

Five warm lanes ran on the quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11
RC3 on November 19, 2026 UTC. Warm elapsed includes mediated RAM-backed
I/O, golden checks and prior-result cleanup. Process CPU covers the wider
worker boundary. Ranges span the four combinations of duplicate worker
medians.

| Workload | Warm elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| JSON transform with keyword forwarding | −6.89…−5.60% | −8.66…−6.22% | 0.56 / 0.81% |
| CSV invoice with keyword forwarding | −6.18…−3.84% | −6.49…−2.78% | 1.09 / 1.39% |
| Expanded text report | −0.82…+2.42% | 0.00…+1.56% | 1.86 / 1.38% |
| JSON transform, positional control | −1.22…+1.26% | −2.72…+1.59% | 0.31 / 2.20% |
| CSV invoice, positional control | −0.11…+2.95% | +0.62…+2.78% | 1.32 / 1.72% |

Local allocations fall 16.72% for the JSON variant and 23.15% for the CSV
variant. The accounted peaks fall from 8,084,014 to 6,969,902 bytes and
from 16,378,981 to 12,709,253 bytes, respectively. All three controls have
effectively flat allocation and identical peaks. All five return the
shared allowance to baseline. The expanded-report CPU and the adverse
positional CSV CPU remain open controls. The positional invoice exercises
the ordinary positional-call route. Its CPU movement remains unexplained.

Both quiet gates and all 100 timed batches passed in 271.73 seconds.
Workers joined, guest fixtures were removed and the persistent archive
verified under the lease. Full delivery checks are pending.

[Summary](summary.json) · [Observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) ·
[Source, checks and procedures](checks-and-procedures.tar.xz).

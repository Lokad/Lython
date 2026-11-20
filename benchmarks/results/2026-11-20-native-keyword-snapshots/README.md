# B1161 native dictionary snapshot prototype

The prototype uses the same native copy routine for `dict.copy()` and exact
keyword expansion. Equal-capacity tables copy bucket heads and links along
with their keys. Compact copies relink into the destination. Both take an
independent value snapshot, keep original key and scalar identity, and
preserve shared record layouts.

The source pair is `b84ce1fe5` and private `c1d6e3c01`. Both complete
32-file images matched five independent CPython goldens and returned the
allowance to baseline. Warm allocations were effectively flat. Accounted
peaks matched in every case. The initial public test launches picked up
the wrong .NET host on Windows and fixture output layout on Ubuntu.
The corrected launches passed 1,954 public and 127 white-box focused
Release checks per platform, on Windows .NET 11 and Ubuntu .NET 10.

| Workload | Phase | Guest elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Expanded forwarding | Warm | −1.81…+1.88% | −3.04…+1.47% |
| JSON keyword transform | Warm | −2.56…+3.05% | −2.61…+4.71% |
| CSV keyword invoice | Warm | +0.09…+1.81% | +0.00…+1.47% |
| Positional invoice control | Warm | −0.27…+2.83% | +1.35…+4.64% |
| JSON record copy | Warm | −0.53…+1.57% | −0.30…+1.71% |
| Expanded forwarding | Compile-to-result | −0.51…+1.02% | −0.78…+1.19% |

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 20,
2026 UTC. Warm includes mediated RAM-backed I/O, golden verification and
prior-result disposal. Compile-to-result adds compilation and code-owner
disposal. Startup, reset and final teardown are outside guest elapsed.
Process CPU covers the wider batch. Ranges compare all four pairings of
duplicate worker medians.

Keep the performance prototype private. Expanded forwarding timing spans
zero. The CSV keyword case has a small adverse direction, and the positional
invoice uses more CPU. Flat allocation, unclear target gains and an adverse
control leave insufficient evidence to replace the common copy routine.

The original core throws a CLR array-copy exception when an admission
callback grows the source of `dict.copy()`. The correction is committed
separately at `a4a0dc098`. It sizes the destination from the current source
and refunds the unpublished copy if replacement admission fails. Windows
.NET 11 and Ubuntu .NET 10 each passed 126 focused checks of the correction.

Both quiet gates and all 120 timed batches passed. Workers joined,
guest fixtures were removed and the persistent archive verified under the
lease. Staging used a 172 MiB filesystem reserve after checking the size
of the retained checkpoint archives, and removed only binary replicas
already verified in the committed evidence.

[Summary](summary.json) · [Observations](observations.tgz) ·
[Archive receipt](archive-receipt.json) · [Source and checks](checks-and-procedures.tar.xz).

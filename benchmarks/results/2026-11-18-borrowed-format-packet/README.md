# Borrowed argument packets for method formatting

B999 compared `604dc52db` (frozen `47e0a58d`) with the borrowed-packet
implementation (frozen `334f106b`) on the quiet Azure F4as_v7 VM,
2026-11-18 UTC. Method formatting reads the existing argument packet
through completion. It admits indices only when named arguments or
interleaving require them. It removes the positional value copy, the
empty keyword dictionary and an async wrapper.

| Workload and phase | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Positional `str.format` report, warm | −8.68% to −4.68% | −8.37% to −2.76% |
| Expanded forwarding report, warm | −2.02% to −0.85% | −3.28% to −1.74% |
| Expanded forwarding report, compile-to-result | −2.64% to −1.18% | −2.54% to −1.76% |
| Named `str.format` report, warm | −2.09% to +1.03% | −2.39% to +0.40% |
| F-string report control, warm | −0.82% to −0.12% | −1.25% to +1.15% |
| Positional calls control, warm | +0.96% to +2.56% | +1.24% to +2.51% |

Retained for the measured positional formatting and forwarding gains and
the removal of two scratch containers. The positional-call control's
elapsed and CPU adversity remains open for focused confirmation.
The named formatting result spans zero. The positional formatting
reference spread was 2.96% and candidate spread was 1.38%.

The local allocation screen reduced the positional formatting report
13.35%, named formatting 1.35% and expanded forwarding 4.08%. Accounted
peaks for positional formatting and forwarding were unchanged. Both
workers restored the host allowance and the unrelated controls allocated
the same amount. The existing f-string report is an unchanged control.

Warm includes mediated RAM-backed I/O, golden checks and prior-result
cleanup. Compile-to-result adds compilation and compiled-owner disposal.
Startup, fixture reset and final cache teardown are outside elapsed.
Process CPU covers the wider requests. Both complete .NET 11 RC3 worker
images verified. All six jobs finished in 275 seconds, passed both quiet
gates and joined every worker. The raw archive was verified under the lease.

Windows passed 735 focused public checks per framework. Ubuntu on WSL
passed 22 focused white-box checks per framework. Coverage includes
interleaved positional and keyword arguments, dynamic format specifications,
actual callback suspension and disposal. An independent CPython check
agrees on the interleaved call's output.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Source, checks and procedures](checks-and-procedures.tar.xz)

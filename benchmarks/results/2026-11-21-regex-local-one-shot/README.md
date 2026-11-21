# B1303 local scratch for single regex matches

Single `match`, `fullmatch` and `search` own their capture scratch
for the duration of the synchronous provider operation and final
projection. Up to eight slots use a bounded managed inline buffer.
Larger captures and large Unicode coordinate sorts reserve their
capacity before allocation. Cursor operations keep their reusable
prepared state. Both routes validate the same provider result and
produce the same escaping match data.

Compared `d51891009` with `225575497` on the quiet Azure F4as_v7
with Ubuntu 24.04.4 and .NET 11 RC3 on November 21, 2026 UTC.
Two independently warmed processes per revision contributed seven
rotated batches each. Ranges cover all process-median pairings.
Warm elapsed includes mediated RAM-backed I/O, golden checks and
prior-result disposal. Process CPU includes the wider request.
Compilation, startup and final teardown are outside both clocks.

| Complete warm workload | Elapsed change | Process CPU change | Local CLR allocation |
| --- | ---: | ---: | ---: |
| Small fullmatch per record | −4.02% to −1.89% | −4.69% to −2.55% | −3.61% |
| Cursor records | +0.46% to +1.17% | −0.99% to +0.62% | 0.00% |
| 13-slot ASCII fullmatch | −1.11% to +1.32% | −1.32% to +0.20% | −2.82% |
| 43-slot Unicode search | −0.42% to +1.93% | −0.64% to +1.79% | −0.37% |

Retain the local ownership path for the small-match CPU and allocation
improvement. The wide elapsed and CPU ranges span zero. The cursor's
small elapsed cost is a tradeoff, with mixed process CPU. The local
Windows cursor and wide ASCII controls were slower and remain recorded.
Accounted peaks are unchanged for small matches and cursors and
slightly lower for wide matches. All allowances return to baseline.

Windows .NET 11 and Ubuntu .NET 10 each passed 67 white-box and 424
public focused Release checks. The regression covers a wide Unicode
match with lookbehind before the requested window, bounded lookahead,
unmatched groups, character offsets and identities after cache purge
through both public run APIs. Both 32-file images passed all four
CPython goldens. All 112 timed batches and both quiet gates passed.
The durable archive was verified under the VM lease after workers
joined and RAM fixtures were removed. These results use the existing
optional 0.4.0 adapter.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Local timing](local-timing.json) · [Raw observations](observations.tgz) ·
[Source and checks](checks-and-procedures.tar.xz).

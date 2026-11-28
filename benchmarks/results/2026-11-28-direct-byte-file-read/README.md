# B1920 Call owned byte-file reads directly

Sequential and positional reads share one path over `9b66513a1`. The host
receives the caller's original admitted destination and cancellation token.
The read drops the captured delegate and the generic async host wrapper.
It keeps one async boundary, the pre-call synchronous capability check,
settlement of a false synchronous promise, error translation, and the
post-read cancellation and count checks. Both reads keep the prior messages
for invalid counts.

The first candidate removed the remaining async boundary too. A host setting
an `AsyncLocal` during a ready `Path.read_bytes()` read then leaked that value
into the next read. Both new cases fail on the first candidate and pass on
its reference and the final build. Four small positional ZIP cases also
verify negative and overlong counts, retirement and allowance reuse.

Focused Release passes 302 public and 114 white-box cases per target on
Windows .NET 10 and 11 and Ubuntu WSL .NET 11. Tests include a false sync
promise, late completion after cancellation, terminal refusal, and positional
refill and close. Both 32-file images match four independent CPython 3.13.16
output and file goldens. Only the core DLL differs.

The Windows allocation witness saves 96 B per first-line search, 288 B
per file-fed shipment and 768–824 B per dense JSON document. Eight budget
observations restore the shared baseline with identical execution peaks
and compiled reservations. The generated-definition control allocates the
same amount in both builds.

## Local timing

November 28, 2026, Ubuntu 24.04 WSL and .NET 11 RC3, one pinned logical CPU.
Two workers per image each warm for at least five seconds and contribute
seven rotated batches per lane. Ranges span all pairings of worker medians.
Warm elapsed includes mediated RAM-backed I/O, verification and prior-result
disposal. External process CPU also includes fixture reset and framing.
Compilation and startup sit outside the clocks.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Dense nested JSON | Async | +2.45% to +4.74% | −3.89% to +7.55% |
| First-line file search | Async | −6.50% to +5.96% | −5.72% to +2.75% |
| Generated definitions (unchanged) | Async | −2.48% to +1.34% | −4.87% to +0.93% |
| File-fed shipment | Async | −2.55% to +5.77% | −3.77% to +6.58% |
| Dense nested JSON | Sync | −2.64% to +0.96% | −4.40% to +2.67% |

All 140 batches verify in 191.2 seconds. Keep the direct read for
the removed wrapper and measured allocation reduction. CPU ranges cross zero
in all lanes. Dense async elapsed is 2.45–4.74% higher, with a 9.56% candidate
CPU spread. Carry this observation into the combined quiet checkpoint.

[Timing summary](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Original and final source, checks and logs](procedures.tar.xz).

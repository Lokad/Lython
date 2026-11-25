# B1718 Complete buffered DictReader rows inline

The dictionary reader completes recorded input and native header handling
on its caller's synchronous prefix in both hosting modes. One progress
loop skips blank records. A deferred read resumes that loop, and guest
iteration keeps the private row funded through its actual consumer. This
removes the unconditional per-row and per-header async state machines.
Native record construction and adoption stay shared. The reference
`36a4c681f` already includes the blank-row refund correction.

The two frozen images differ only in the core DLL. Both verify all five
complete script goldens. The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and
.NET 11 RC3 on November 25, 2026 UTC. Two workers per source warm for
five full-request seconds each and contribute seven rotated batches per
phase. Ranges span all four pairings of worker medians. Negative changes
mean less time.

Warm elapsed includes mediated RAM-backed I/O, verification of output and
files, and result cleanup. Wider CPU additionally includes fixture reset
and request framing. Compilation, fresh-process startup and final process
teardown sit outside these clocks.

| Workload and phase | Elapsed change | Wider CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| File-fed map, warm async | −10.14 to −7.62% | −10.21 to −7.13% | 0.75 / 2.04% |
| Quoted invoice, warm sync | −2.26 to −2.07% | −4.55 to −2.04% | 0.16 / 0.03% |
| Shipment, warm async | −2.96 to −1.70% | −3.74 to −1.78% | 0.72 / 0.58% |
| Nonlocal text control, warm sync | −0.92 to +9.95% | −0.62 to +10.22% | 5.47 / 5.21% |
| File-fed map, warm sync | −6.71 to −3.86% | −7.33 to −3.44% | 1.87 / 1.15% |
| Quoted invoice, warm async | −1.55 to −0.79% | −2.04 to −1.32% | 0.35 / 0.42% |
| Shipment, warm sync | −5.07 to −2.48% | −4.85 to −1.88% | 0.33 / 2.39% |
| Plain reader control, warm sync | +0.19 to +1.29% | +0.00 to +2.60% | 0.88 / 0.21% |

## Confirmation

The exact images repeat four phases in a different order with reversed
starting roles and seven full-request seconds of warmup per worker.

| Workload and phase | Elapsed change | Wider CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Nonlocal text control, warm sync | −5.50 to +2.61% | −5.85 to +3.52% | 7.91 / 0.62% |
| Plain reader control, warm sync | −1.69 to −0.78% | −1.89 to +0.74% | 0.72 / 0.20% |
| File-fed map, warm async | −9.29 to −0.58% | −8.13 to +0.25% | 1.81 / 7.65% |
| Quoted invoice, warm sync | +1.01 to +1.98% | +1.89 to +4.47% | 0.01 / 0.95% |

Keep the combined split private. The invoice reverses at low worker spread
while the file-fed elapsed improvement repeats at wider candidate spread.
The two unchanged controls leave attribution uncertain. The confirmation
passes both quiet checks and all 112 batches with joined workers, retired
fixtures and a verified archive.

[Confirmation timing](confirmation/quiet-summary.json) ·
[Observations](confirmation/quiet-observations.tar.xz) ·
[Terminal receipt](confirmation/quiet-terminal-status.json) ·
[Procedures](confirmation/procedures.tar.xz).

## Correctness and admission

Compilation allocation, retained compilation allowance and all execution
peaks match. Local warm allocation differs by at most 672 B per complete
script. Windows .NET 11 and Ubuntu .NET 10 each pass 555 focused Release
checks and the added suspension cases. The new cases check `line_num`
after consecutive real suspensions and after a header is cleared
during data acquisition. They verify CPython output, awaited cancellation
and allowance recovery. Both quiet gates and all 224 timed batches pass.
Workers join and the persistent archive verifies after fixture cleanup.

[Identity](identity.json) · [Timing](quiet-summary.json) ·
[Runtime allocation](allocation-summary.json) · [Compiler allocation](compile-allocation-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal receipt](quiet-terminal-status.json) ·
[Procedures and patch](procedures.tar.xz).

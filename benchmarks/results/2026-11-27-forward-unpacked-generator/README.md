# B1838 Forward unpacked operands into generator lists

The trial on `0fb8ee7b1` keeps each half of a completed fixed unpack in
its operand slot. A single-clause generator can use that storage when a
list reads each distinct target once and the other items are name reads
or scalar literals. The trial preserves the order of those other reads.
The existing display and staged yield use one encoded operand order.
They read the original slots directly into the list or callback's final
storage. The trial removes two local slots and four instructions in the
short and wide shipment tapes. Their captured policy remains between the
two unpacked values. Seven control tapes match the original, including
a generator that observes `locals()` and one with a repeated target.

The candidate passed 505 public and 89 white-box Release checks on each of
Windows .NET 11 and Ubuntu WSL .NET 10. In the focused scenario, a suspended
source preserves the original list aliases and closes its borrowed file
when cancelled. Normal materialization and a callback that resumes the
generator see identical ordering. Admission refusal during list creation
unwinds the frame and returns the allowance to baseline. Four complete
scripts match CPython 3.13.2 output and file goldens in the frozen verifier.

The Windows allocation diagnostic restores the shared allowance after all
scripts. The short shipment allocates 512 fewer CLR bytes in both APIs.
The wide shipment allocates 742 fewer bytes in sync and 512 fewer in async.
Both show 3,600 fewer bytes of peak admission in each API. The control
peaks are unchanged. Allocation changes are below 0.1% on these inputs.

## Initial local timing

Local Ubuntu WSL .NET 11 used one logical CPU, two workers per frozen
image and seven rotated samples per worker after warmup. Only the core
library differs between images. Ranges cover all four pairs of worker
medians. Warm elapsed includes mediated I/O, verification and prior-result
disposal. Process CPU also includes fixture reset and framing. Compilation
and startup are outside the measurement.

| Workload | Warm async elapsed | Process CPU |
| --- | ---: | ---: |
| Bound zip control | −4.64% to −2.00% | −5.69% to +0.75% |
| Wide shipment | +1.45% to +17.81% | −1.52% to +9.32% |
| File-fed map control | −3.47% to +2.62% | −1.61% to −0.09% |
| Short shipment | −3.11% to −2.60% | −4.02% to +0.28% |

The two wide candidate elapsed medians differ by 12.20%. The bound zip
control and short shipment improve by similar amounts in elapsed time.
All 112 initial batches verify. The workers join and the guest fixtures
are removed before the persistent archive verifies.

## Same-image repeat

The repeat keeps the same frozen workers, warm boundary and sampling
schedule. It runs only file-fed map and the wide shipment, with file-fed
map first. All 56 batches verify and the worker and fixture cleanup
checks pass.

| Workload | Warm async elapsed | Process CPU |
| --- | ---: | ---: |
| File-fed map control | −22.75% to +2.19% | −2.36% to +6.76% |
| Wide shipment | −3.31% to −0.35% | −3.78% to +0.45% |

The wide candidate elapsed spread falls to 1.41% and the elapsed direction
reverses. CPU spans zero across both wide runs. The unchanged file-fed
reference workers differ by 27.03% in elapsed time on the repeat. Keep both
sets of observations. Withdraw the runtime trial because its additional
compiler and interpreter paths buy small storage savings and the measured
throughput effect remains unresolved. Keep the order, alias, cancellation
and admission regressions. All five pass on each target after nonincremental
rebuilds of the original core. Revisit the handoff if a shared slot-placement
design can remove it with less machinery.

[Frozen images](identity.json) · [Tape proof](tape-proof.json) ·
[Allocation and admission](allocation-summary.json) ·
[Initial timing](summary.json) · [Repeat timing](repeat/summary.json) ·
[Initial observations](observations.tar.xz) ·
[Repeat observations](repeat/observations.tar.xz) ·
[Exact patches and checks](procedures.tar.xz).

# B1919 Share bounded and whole text reads

Both execution APIs use one native accumulation loop for `read()`.
It replaces four whole-file and bounded loops, removing 56 lines over
`017ba75cc`. Ready host reads finish without a read continuation. A pending
refill transfers the existing builder to one continuation. That owner keeps
its admitted backing through completion and cleanup. Bounded reads preserve
exact-fit behavior, borrowable slices and cursor advancement after admission.

Focused Release passed 257 public and 263 white-box checks per target on
Windows .NET 10 and 11 and Ubuntu WSL .NET 11. The selection covers held
reads, cancellation, refusal, retained aliases, UTF-8 and UTF-16 boundaries,
text gzip, and delayed CSV reads. Both frozen images match four CPython
3.13.16 output and file goldens. Eight Windows allocation checks restore
shared allowance and show identical execution peaks and compiled charges.
Only the unaffected async text histogram differs in CLR allocation, by 56 B.

## Local timing

November 28, 2026, Ubuntu 24.04 WSL and .NET 11 RC3, one pinned logical CPU.
Each of two workers per build warms for at least five seconds and supplies
seven rotated batches per lane. Ranges span all pairings of worker medians.
Warm elapsed includes mediated RAM-backed I/O, verification and prior-result
disposal. External process CPU also includes fixture reset and request
framing. Compilation and startup sit outside the clocks. The text histogram
streams lines and the generated definitions perform no file reads.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Dense nested JSON | Async | −1.92% to +1.68% | +0.12% to +2.17% |
| Text histogram (unchanged) | Async | −22.50% to +215.20% | −25.68% to +221.54% |
| Whole-file uppercase | Async | −13.02% to +13.94% | −6.79% to +8.41% |
| Dense nested JSON | Sync | −0.79% to +2.98% | −2.28% to +4.49% |
| Generated definitions (unchanged) | Async | −2.17% to +0.60% | −1.23% to +1.59% |

All 140 initial batches verify in 165.7 seconds. Same-image CPU spreads reach
161.61% and 65.38% on the unchanged text control. The bounded repeat rotates
worker placement and rechecks the two affected async scripts and that control.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Text histogram (unchanged) | Async | −1.90% to +7.47% | +0.30% to +2.45% |
| Whole-file uppercase | Async | −3.28% to +2.08% | −2.15% to −0.16% |
| Dense nested JSON | Async | +0.02% to +2.02% | −2.76% to −0.16% |

All 84 repeat batches verify in 110.2 seconds.

Keep the shared loop for the simpler ownership path. The repeat finds
0.16–2.15% lower process CPU for whole-file uppercase and 0.16–2.76% lower
CPU for dense async JSON. The text control uses 0.30–2.45% more CPU. Elapsed
crosses zero for uppercase. Dense async elapsed ranges from +0.02% to +2.02%,
within the control movement. The first pass and repeat support no broad speedup
claim. Keep both runs for the next quiet checkpoint.

[First timing](summary.json) · [Repeat timing](repeat/summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [First observations](observations.tar.xz) ·
[Repeat observations](repeat/observations.tar.xz) ·
[Source and checks](procedures.tar.xz).

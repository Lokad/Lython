# B1722 Publish prepared tuple elements directly

Both executable drivers check the private source slots, hold at most one
computed element in a managed local and admit scalar publication before
writing to the caller's existing unpack operands. This removes the
callee's staging window, extent changes and the subsequent copy from
that window. Refusal, cancellation and a declining type check leave
the caller's destination intact. Multiple computed elements and
dynamic operations use the existing tuple return and unpack paths.

The frozen reference uses the runtime of `93eead293`. Both 32-file images
differ only in the core DLL and match six independent output/file goldens.
A separate instrumented Windows run reaches the prepared exit 4,096 times
per invoice in each hosting mode. The corresponding 4,096 staging copies
disappear. The five controls never take the prepared exit. CLR allocation
essentially matches on the invoice, and all measured admission peaks,
retained compilation estimates and final balances match across the pair.

The quiet Azure F4as_v7 used Ubuntu 24.04.4 and .NET 11 RC3 on
November 25, 2026 UTC. Two workers per image warm for five full-request
seconds and contribute seven rotated batches per phase. Ranges span the
four pairings of worker medians. Warm elapsed includes mediated RAM-backed
I/O where applicable, verification and prior-result cleanup. Wider CPU
includes fixture reset and request framing. Compilation, warm and
compile-to-result are separate.

| Workload | Phase | Elapsed change | Wider CPU change | Reference / candidate elapsed spread |
| --- | --- | ---: | ---: | ---: |
| Quoted CSV invoice | Warm sync | −0.52 to +1.01% | +0.00 to +2.08% | 1.05% / 0.48% |
| File-fed CSV map | Warm async | −3.85 to +5.14% | −5.13 to +5.28% | 8.40% / 0.87% |
| Defaulted rule factories | Warm sync | −2.64 to −0.70% | −5.50 to +1.52% | 0.39% / 1.59% |
| Nonlocal Unicode text | Warm sync | −1.79 to +11.73% | −3.00 to +12.62% | 8.80% / 4.57% |
| Shipment callback report | Warm async | −0.41 to +2.32% | −0.54 to +4.35% | 0.45% / 2.28% |
| Quoted CSV invoice | Warm async | +1.10 to +2.25% | +2.70 to +2.70% | 0.17% / 0.96% |
| Generated ordinary definitions | Compile-to-result | −0.14 to +1.01% | +1.12 to +3.87% | 1.13% / 0.02% |

The measured prototype stays private. Invoice warm sync elapsed crosses
zero. Async invoice elapsed rises 1.10–2.25% and wider CPU rises 2.70%
across all rounded worker pairings. Ordinary definitions also use
1.12–3.87% more CPU through result. Text and file-fed controls show substantial
worker spread. Removing the staging copy alone does not justify the new
publication path. The slot-map refinement also stays private.
[Refinement](indexed/README.md).

Windows .NET 11 and Ubuntu .NET 10 each pass 436 focused Release checks,
including caller storage on refusal/cancellation, return aliases, target
order, and fallback into an operator that genuinely suspends. All 196
timed batches verify. Workers join and the persistent archive verifies
after fixture removal under the lease.

[Identity](identity.json) · [Timings](quiet-summary.json) · [Route counts](routes-summary.json) ·
[Runtime allocation](allocation-summary.json) · [Compilation allocation](compile-allocation-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal receipt](quiet-terminal-status.json) ·
[Source and procedures](procedures.tar.xz).

# B1722c Publish prepared tuple elements with a compiled slot map

Both executable drivers check the private source slots, hold at most one
computed element in a managed local and admit scalar publication before
writing to the caller's existing unpack operands. This removes the
callee's staging window, extent changes and the subsequent copy from
that window. The existing instruction carries source slot indexes so
publication also skips the second instruction-tape read. The prepared
range fits eight four-bit positions. Larger or dynamic returns use
their ordinary paths. Refusal, cancellation and a declining type check
leave the caller's destination intact. Multiple computed elements and
dynamic operations use the existing tuple return and unpack paths.

The frozen reference uses the runtime of `93eead293`. Both 32-file images
differ only in the core DLL and match four independent output/file goldens.
A separate instrumented Windows run reaches the prepared exit 4,096 times
per invoice in each hosting mode. The corresponding 4,096 staging copies
disappear. The three controls never take the prepared exit. CLR allocation
essentially matches on the invoice, and all measured admission peaks,
retained compilation estimates and final balances match across the pair.

The quiet Azure F4as_v7 used Ubuntu 24.04.4 and .NET 11 RC3 on
November 25, 2026 UTC. Two workers per image warm for seven full-request
seconds and contribute seven rotated batches per phase. Ranges span the
four pairings of worker medians. Warm elapsed includes mediated RAM-backed
I/O where applicable, verification and prior-result cleanup. Wider CPU
includes fixture reset and request framing. Compilation, warm and
compile-to-result are separate.

| Workload | Phase | Elapsed change | Wider CPU change | Reference / candidate elapsed spread |
| --- | --- | ---: | ---: | ---: |
| Quoted CSV invoice | Warm async | +0.76 to +2.32% | +0.00 to +2.70% | 1.11% / 0.43% |
| Nonlocal Unicode text | Warm sync | +2.50 to +8.23% | +3.44 to +6.88% | 2.20% / 3.31% |
| Generated ordinary definitions | Compile-to-result | −0.28 to +2.09% | −1.16 to +1.01% | 2.30% / 0.08% |
| File-fed CSV map | Warm async | +1.38 to +8.82% | +1.22 to +8.03% | 3.77% / 3.45% |
| Quoted CSV invoice | Warm sync | +1.09 to +1.74% | +1.30 to +2.08% | 0.06% / 0.58% |

Keep both prototypes private. The slot map removes the additional tape read
but the invoice still takes 0.76–2.32% longer warm async and 1.09–1.74%
longer warm sync. Invoice CPU is 1.30–2.08% higher in the sync comparison.
Text and file-fed map also regress despite taking zero prepared exits.
Ordinary definitions through result span zero in this block. The control
movement leaves the cause open. It supplies no evidence that this
publication change makes the complete script faster.

Windows .NET 11 and Ubuntu .NET 10 each pass 436 focused Release checks,
including caller storage on refusal/cancellation, return aliases, target
order, and fallback into an operator that genuinely suspends. Another
seven Windows Debug checks passed with the same patch. All 140
timed batches verify. Workers join and the persistent archive verifies
after fixture removal under the lease.

[Identity](identity.json) · [Timings](quiet-summary.json) · [Route counts](routes-summary.json) ·
[Runtime allocation](allocation-summary.json) · [Compilation allocation](compile-allocation-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal receipt](quiet-terminal-status.json) ·
[Source and procedures](procedures.tar.xz) · [First comparison](../README.md).

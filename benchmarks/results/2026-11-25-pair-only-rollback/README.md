# B1717c Isolate rollback ownership for closed pairs

This candidate moves rollback for a completed private pair member into the
callable. The factory keeps the two callable references through publication.
The single computed definition path is byte-for-byte equal to the reference.
Both variants keep the original admission order and totals. The strengthened
refund test covers admitted positional defaults, a multi-entry keyword backing
and eager annotations. Windows .NET 11 and Ubuntu .NET 10 each pass 36 checks.

The reference is `60a293724`, the same source as in [B1717](../2026-11-25-callable-owned-rollback/README.md).
The two frozen 32-file images differ only in the core DLL. Each verifies three
complete CPython-derived goldens. The quiet Azure F4as_v7 ran Ubuntu 24.04.4
and .NET 11 RC3 on November 25, 2026 UTC. Two workers per image warm for
five full-request seconds each and contribute seven rotated batches per
phase. Ranges span all four pairings of worker medians.

Warm elapsed includes applicable mediated RAM-backed I/O, result and file
verification, and result cleanup. Compile-to-result also includes compilation
and compiled-owner disposal in an already-running host. The CPU clock adds
fixture reset and request framing. Both clocks exclude fresh-process startup.

| Workload and phase | Elapsed change | Wider CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Shipment, warm async | +0.32 to +1.54% | −0.92 to +2.50% | 0.33 / 0.88% |
| Defaulted pairs, warm | +2.61 to +6.51% | +2.30 to +6.52% | 1.32 / 2.45% |
| Postponed annotations, warm | −7.19 to +3.36% | −6.20 to +4.98% | 10.57 / 0.72% |
| Defaulted pairs, compile-to-result | −4.53 to −1.72% | −5.79 to −1.63% | 0.69 / 2.24% |
| Postponed annotations, compile-to-result | −7.14 to +12.24% | −7.48 to +11.39% | 4.56 / 15.59% |

Keep the source private. Warm defaulted execution regresses even though its
compile-to-result CPU improves in this block. The unchanged postponed control
spans both directions at high worker spread. These observations cannot assign
the split to a particular method or to compilation. Runtime allocations on the
rule scripts and all sampled admission peaks match. A future construction
redesign needs to eliminate work beyond changing the rollback boundary alone.
All 140 batches and both quiet checks passed. Workers joined, fixtures were
removed and the persistent archive was verified under the lease.

[Source and image identity](identity.json) · [Timing](quiet-summary.json) ·
[Runtime allocation](allocation-summary.json) · [Compilation allocation](compile-allocation-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal receipt](quiet-terminal-status.json) ·
[Procedures and patch](procedures.tar.xz).

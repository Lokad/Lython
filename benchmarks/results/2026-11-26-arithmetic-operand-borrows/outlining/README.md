# B1758b Outline the arithmetic slow paths

The refinement keeps the operand borrow and moves named or captured source
resolution and generic binary evaluation out of the exact arithmetic entry.
The local Windows .NET 11 JIT diagnostic emits a 2,434-byte Tier 1 entry,
down from 3,481 B for the first prototype. The outlined methods still exist.
The reference is `2d2905bc8`, with the same production source and frozen
reference image as [B1758](../README.md).

| Workload | Warm lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| File-fed map control | Async | −6.89 to +1.08% | −7.97 to +10.31% |
| Typed Unicode CSV meter | Sync | +0.53 to +5.27% | +2.01 to +4.78% |
| Shipment callbacks | Async | −0.71 to +3.97% | +0.45 to +4.87% |

The November 26, 2026 UTC diagnostic ran on local WSL Ubuntu with .NET 11
RC3, pinned to one logical CPU. It uses two workers per source, four seconds
of full-request warmup per worker and seven rotated batches. Ranges cover
four pairings of process medians. Warm time includes mediated RAM-backed
I/O, verification and result disposal. CPU also includes framing and reset.
The quiet VM was below the transport staging floor. The local file-fed
control has wide CPU spreads of 7.88% and 11.11% within each source.

Both complete frozen images pass four independent CPython output and file
goldens. Allocation matches within 56 B and peak admission matches exactly.
Disposal returns the shared allowance to baseline. Windows .NET 11 and
Ubuntu .NET 10 each pass 601 white-box and 2,652 public focused Release
checks. All 84 local batches complete, workers join and the archive verifies.

The typed-meter slowdown is visible in both the first quiet comparison and
the outlined variant's local diagnostic. Shipment elapsed still spans zero.
Keep both patches and restore the production path. A new operand design
needs to remove a different stage or demonstrate that it preserves the
existing cost of exact scalar arithmetic.

[Timing](summary.json) · [Identity](identity.json) · [Allocation and admission](allocation-summary.json) ·
[Observations](observations.tar.xz) · [Receipt](archive-receipt.json) ·
[Patches, diagnostics and checks](procedures.tar.xz).

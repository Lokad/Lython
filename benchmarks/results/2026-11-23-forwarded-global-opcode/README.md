# B1493 Dispatch forwarded operands only at their instruction

Private `a92a41031` gives the retained forwarded-call operands their own
instruction variant. The ordinary call entry no longer checks for them.
The forwarded variant uses the original binding, storage and resume path
when it needs a frame. Instruction counts and workspace capacities match
on both images. The quoted invoice and fully supplied helper have identical
compiled tapes. The short, mixed-width and rebound helpers use the new variant.

On November 23, 2026 UTC, the quiet Azure F4as_v7 with Ubuntu 24.04.4 and
.NET 11 RC3 compares retained `2b501135a` with the prototype. Both 32-file
images verify and differ by the core DLL. Two independent workers per
source warm for eight seconds and supply seven rotated batches. Ranges
pair medians. Warm elapsed includes applicable mediated RAM-file I/O,
verification and prior-result disposal. Process CPU also includes resets
and protocol. Compilation and startup are outside elapsed.

| Warm script | Elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Quote-heavy invoice | −0.84 to <0.00% | −0.70 to +1.44% | 0.03 / 0.82% |
| Mixed-width word histogram | +0.24 to +10.52% | −1.18 to +10.48% | 0.67 / 9.52% |
| Fully supplied helper | −1.01 to −0.11% | −1.26 to +1.11% | 0.72 / 0.18% |
| Short nonlocal word report | +3.65 to +5.44% | +4.23 to +8.62% | 0.36 / 1.37% |

Keep the opcode variant private. The quoted control's CPU spans zero
and the short workload reproducibly takes longer across worker pairings.
Mixed-width workers vary widely. The full retained benefit remains in
B1489 and the confirmed invoice cost remains in B1490. Windows x64 Tier 1
stack subtraction in the ordinary call falls from 344 to 248 bytes.
Local allocation stays effectively flat, admitted peaks match, and all
allowances return to baseline. Code generation alone did not predict the
whole-script direction.

Windows .NET 11 and Ubuntu .NET 10 each pass 39 white-box and 179 public
focused checks, including the managed stack continuation. Both frozen
images verify five independent CPython goldens. All 112 timed batches
verify in 188 seconds. Workers join, fixtures retire and the receipt
verifies under the quiet lease.

[Timings](summary.json) · [Identity](identity.json) · [Tape](tape-summary.json) ·
[JIT](jit-summary.json) · [Allocation](allocation-summary-six.json) ·
[Raw observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures](procedures.tar.xz) · [Retained reference](../2026-11-23-forwarded-global-call/README.md).

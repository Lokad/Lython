# B1492 Share forwarded-call reads and fallback staging

Private `0bcc5e035` uses one ordered operand read and fallback staging
routine for native and resumable execution. The same private workspace
and live extent still identify a prepared call after transfer. The ordinary
call entry continues to check whether its operands were forwarded.

On November 23, 2026 UTC, the quiet Azure F4as_v7 with Ubuntu 24.04.4 and
.NET 11 RC3 compares retained `2b501135a` with the prototype. Both 32-file
images verify and differ only by their core DLL. Two workers per source
warm for eight seconds and contribute seven rotated batches. Ranges pair
worker medians. Warm elapsed includes applicable mediated RAM-file I/O,
verification and prior-result disposal. Process CPU includes resets and
protocol. Compilation, startup and final worker teardown sit outside elapsed.

| Warm script | Elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Quote-heavy invoice | −0.83 to +3.35% | −0.66 to +2.56% | 0.85 / 3.35% |
| Mixed-width word histogram | −3.15 to +3.68% | −3.33 to +3.11% | 0.22 / 6.82% |
| Fully supplied helper | −3.38 to −1.17% | −3.12 to +0.72% | 1.74 / 0.53% |
| Short nonlocal word report | −1.16 to +4.17% | −1.04 to +3.94% | 1.88 / 3.45% |

Keep the variant private. This comparison leaves invoice debt unresolved,
with mixed-width and short worker variation. The fully supplied helper's
elapsed improves and CPU spans zero. Local allocation is effectively flat,
admitted peaks match and every allowance returns to baseline. Windows x64
Tier 1 subtracts 328 bytes from the native call stack, down from 344. The
instruction driver's observed Tier 1 OSR bodies are smaller and the native
call body is larger. These JIT results describe the Windows diagnostic.

Windows .NET 11 and Ubuntu .NET 10 each pass 142 focused Release checks.
Five independent CPython goldens verify on both images. All 112 timed
batches, worker joins, fixture removal and both quiet gates pass, with the
archive verified under the lease. A follow-up can test targeting the merged
operand instruction itself, so ordinary dispatch leaves forwarding to the
eligible instruction.

[Timings](summary.json) · [Identity](identity.json) · [JIT](jit-summary.json) ·
[Allocation](allocation-summary-six.json) · [Raw observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Procedures](procedures.tar.xz) ·
[Retained comparison and exact reference core](../2026-11-23-forwarded-global-call/README.md).

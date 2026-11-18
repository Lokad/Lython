# Reuse syntax validity scratch and skip an empty directive pass

B985 compared private `610d403d` with `4ff69a9af` / `74d19a10` on the quiet
Azure F4as_v7 VM on 2026-11-18. The verified 32-file .NET 11 RC3 images
use ordinary GC and tiering and differ only in the core DLL. All four complete
scripts matched CPython output and file goldens on both source images.

The syntax validity traversal reuses one expression stack and lazily creates
one duplicate-name set per script. It records the presence of scope directives
while checking the existing statements. The compiler skips the separate
scope-directive traversal for proven absence and removes the private array of
analysis delegates. It keeps the ordered checks for actual or unknown
presence, and checks cancellation throughout the main syntax traversal.

| Complete script and phase | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| CSV invoice, compile | −3.92 to −1.75% | −4.24 to −2.40% | 1.85 / 0.40% |
| Configuration edit, compile | −1.86 to +0.25% | −4.53 to +0.56% | 2.03 / 0.12% |
| Configuration edit, compile-to-result | −0.46 to +3.11% | +0.07 to +1.92% | 1.93 / 1.63% |
| Helper text, compile-to-result | +0.53 to +1.70% | −0.74 to +0.00% | 0.42 / 0.75% |
| Positional calls, warm control | −2.36 to +0.16% | −1.64 to +0.41% | 1.70 / 0.87% |

Ranges cover all combinations of two process medians per image. Negative
favors the candidate. The workers ran eight-second warmups and five samples
per process. Compilation includes compiled-owner disposal. Compile-to-result
includes mediated RAM I/O, output verification and prior-result cleanup.
Process CPU also includes fixture reset and protocol. Startup is outside
elapsed. The five jobs ran in 231.87 seconds. Both quiet endpoints passed,
all users joined, and the observation archive matched its receipt before
release. Staging retired only the two exact working aliases of `4eb89711`
after verifying its committed archive and path, owner and inode evidence.
[Archived branch-map image](../2026-11-18-abstract-branch-maps/candidate-core.dll.gz)

Local phase allocation for the CSV invoice changed from 12,272 to 6,136 bytes
for syntax validity and from 1,560 to zero for the omitted directive walk.
The analogous reductions for helper text were 2,832 and 896 bytes, for
configuration 2,384 and 176 bytes, and for positional calls 1,128 and 816 bytes.
The other observed phase allocations were flat. Warm allocations and accounted
peaks were effectively unchanged. All allowance balances returned to baseline.
Local phase timing is diagnostic and is separate from the paired VM results.

Both actual Release targets passed 217 focused public and 89 regex-provider
and analysis cases on Windows, plus 103 focused public cases on WSL. Eighteen
before/after programs matched public diagnostics, order and output per target.
The detached WSL checkout gave SourceLink warnings. Official CI has its own
source checkout and delivery boundary.

Retain the shared scratch, deletion of the no-op traversal and CSV compile
CPU gain. Carry the configuration compile-to-result CPU increase and helper
elapsed increase to the next relevant compiler checkpoint. This pair establishes
no whole-script speedup. The preceding text runtime improvements are separate.

[Timing](summary.json) · [Phase allocation](phase-summary.json) ·
[Warm allocation and reuse](allocation-summary.json) · [Raw observations](observations.tgz) ·
[Source and checks](checks-and-procedures.tar.xz)

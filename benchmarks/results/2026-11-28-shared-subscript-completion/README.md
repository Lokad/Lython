# B1977 Share subscript reads across executable loops

The ordinary and native loops read the receiver before the key and
use the same builtin completion path. The separate ordinary sequence
probe is gone. Builtin sequence positions can stay in machine slots.
An exact range lookup can finish with a boolean or wide integer key.
Record reads use the existing admitted lookup storage. A guest
descriptor keeps both evaluated operands across suspension.

On the 2,048-row range-indexed CSV diagnostic, both APIs allocate about
1.38 MiB less CLR memory (1,442,022 B sync and 1,441,848 B async).
Peak execution admission drops from 6,137,399 to 3,517,239 B. Compiled
charge and the 8,222 growth requests are unchanged in either API.
The remaining eight diagnostics differ by at most 230 CLR bytes at
matching admission. Retained aliases use the same allocation. All
allowances return to baseline. A regression with 256 small reductions
fits a 16 KiB allowance in both APIs. Both variants refuse admission
with the reference core.

Windows .NET 10 and 11 and Ubuntu WSL .NET 11 each pass 732 public
and 75 white-box focused Release checks. The measured worker images
differ only in the core DLL. The reference uses `53b27d277` and the
candidate uses the archived patch over that revision. Each image
matches nine CPython 3.13.16 output and file goldens locally and on
the VM. The target generator uses eight slots and two shared lookups
in both images.

## Quiet warm async comparison

November 28, 2026 UTC on Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3.
Two workers per image warm for at least five seconds and run seven
rotated batches each. All four timed workloads use the same 2,048 CSV
rows. The range-indexed script uses each record flag to index a shared
`range(2)`. Elapsed includes mediated RAM-backed I/O, verification and
previous-result disposal. Process CPU also includes fixture reset and
framing. Both exclude compilation and launch. Ranges span the pairings
of worker medians.

| Workload | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Range-indexed generator | −27.01% to −24.40% | −26.69% to −25.26% |
| Explicit loop control | −3.73% to +0.29% | −3.74% to 0.00% |
| Ordinary tuple generator control | −2.13% to +5.60% | −3.02% to +5.71% |
| Iterable shipment control | −3.76% to −0.85% | −3.32% to −2.30% |

The ordinary tuple CPU comparison remains inconclusive. Its candidate
workers differ by 6.43% in CPU. Both quiet gates and all 112 timed
batches verify. The workers join, the RAM-backed fixtures are removed,
and the persistent archive verifies before the lease ends.

[Timing summary](summary.json) · [Worker identities](identity.json) ·
[Allocation](allocation-summary.json) · [Admission](admission-summary.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Patch, layouts and checks](procedures.tar.xz).

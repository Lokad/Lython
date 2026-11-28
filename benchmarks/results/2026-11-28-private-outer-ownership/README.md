# B1962 Fund the private generator outer with its operand

The candidate admits the exact builtin outer iterator with the private
`sum` operand. The existing iterator is acquired before the call and
keeps its position and identity if the generator promotes. A completed
private prefix releases the iterator charge with its operand. A promoted
iterator stays charged to the execution because Python can retain `.0`.

The Windows .NET 11 witness removes 2,048 growth requests per 2,048-row
tuple or range script in either API. Peak execution admission falls from
3,779,207 to 3,517,191 B for the tuple and from 3,910,463 to 3,648,447 B
for the range. CLR allocation is essentially flat. An additional script
retains 128 promoted `.0` aliases, advances each inside its callback,
and checks the iterators after the reductions. It saves 128 growth
requests at the same 280,064 B peak. The unrelated controls keep the
same admission. All commands return their allowance to baseline.

The new bounded regression completes 256 reductions under 16 KiB in
both APIs. Both variants fail with refused admission against the
previous frozen core. Windows .NET 10 and 11 and Ubuntu WSL .NET 11
each pass 427 focused Release cases. They exercise observable iterator
position, refusal before callback effects, cancellation and cleanup.

## Matched warm comparison

November 28, 2026 in Ubuntu WSL on .NET 11 RC3 and one logical CPU.
The candidate patch on `8a619a657` and the reference with identical
runtime source at `b663af554` use verified 32-file images. Only the core
DLL differs. Both verify five CPython 3.13.16 output and file goldens.
Two workers per image warm for five seconds each and run seven rotated
batches. The ranges pair all worker medians.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Tuple generator reduction | `RunAsync` | −4.20% to +2.40% | −6.32% to −1.94% |
| Range generator reduction | `RunAsync` | −5.10% to −2.34% | −4.42% to −1.72% |
| File-fed map control | `RunAsync` | +0.29% to +8.37% | +0.73% to +4.05% |

Elapsed includes RAM-backed mediated I/O, verification and previous-result
disposal. Process CPU includes fixture reset and request framing as well.
Compilation and startup are outside these clocks. All 84 timed batches
verify, the workers join, and guest fixtures are removed. Retain the
ownership fix for bounded repeated reductions and the lower peak
admission. Keep the adverse file-fed control visible alongside the
favorable reduction CPU observations.

[Timing](summary.json) · [Allocation](allocation.json) · [Admission](admission.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Regression and procedures](procedures.tar.xz).

# B1417 Share diagnostic identity with executable code

Private `77467f919` reads each compiled function's diagnostic name
from its code object and derives the lambda kind on error. Executable
function objects drop their duplicate fields and constructor
arguments. Generated methods still own their specific diagnostic
name and share existing parameter layouts. Default storage,
admission and the managed call boundaries stay in place.

The quiet Azure F4as_v7 compares `b5f0c0c18` and `77467f919` on
November 22, 2026 UTC with Ubuntu 24.04.4 and .NET 11 RC3. The
reference core source matches `76b7934f7`. Only the core differs in
the 32-file images. Two workers per source warm for eight seconds
each and complete seven rotated batches per lane. Ranges span their
four median pairings.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed spread before / after |
| --- | --- | ---: | ---: | ---: |
| Postponed nested factories | Warm | +1.70 to +6.57% | +0.77 to +7.12% | 0.23 / 4.55% |
| Postponed nested factories | Compile to result | −3.94 to +2.06% | −4.51 to +1.57% | 4.93 / 1.26% |
| Eager nested factories | Warm | −6.01 to −5.08% | −5.51 to −3.81% | 0.85 / 0.15% |
| Factory observing defaults | Warm | −0.74 to +0.90% | 0.00 to +0.43% | 0.44 / 1.21% |
| Fully supplied helper | Warm | −0.34 to +1.30% | −1.14 to +2.98% | 0.07 / 1.57% |
| Quote-heavy CSV invoice | Warm | −2.33 to +1.87% | −2.38 to +0.32% | 3.34 / 0.93% |
| Large positional call control | Warm | −1.72 to +0.15% | −0.84 to −0.26% | 0.90 / 1.00% |

Warm elapsed includes verification, prior-result disposal and
mediated RAM-backed file I/O where exercised. Compile to result adds
compilation and code-owner disposal. Wider CPU includes request
handling and reset. Startup and final teardown remain separate.

Both nested factories allocate 32,776 fewer bytes per execution
(3.34%) in the separate verified Windows diagnostic. The defaults
observer saves the same number of bytes (2.47%). Controls are
effectively flat. Admitted peaks and allowance refunds agree.
Eager warm elapsed and CPU improve in every pairing. Postponed warm
elapsed and CPU worsen in every pairing, with a wider split between
candidate workers. Compile-to-result timing spans zero. Keep the
prototype private pending a bounded warm confirmation of these two
opposing cases.

Both platforms pass 928 white-box and 3,494 public focused Release
checks. Three initial Windows subprocess cases picked up an
unmatched probe/runtime. The corrected Windows and Ubuntu reruns
use matching probe images and pass. The two exact core images
also agree on six binding errors through both APIs, including
lambda, generator, mangled method and generated constructor names.
The original diagnostic compared the CLI's separate mode label
along with the output. The corrected check compares each mode
across images and confirms identical guest messages.

Both images verify six CPython goldens. All 196 timing batches
pass. Workers join, the guest fixtures are removed and the raw
archive verifies under the lease. Staging preserved the first
rejected archive path and recovered by proving the custody of two
redundant VM binaries. This comparison supplies no CPython ratio.

[Images and source](source.json) · [Timing](summary.json) ·
[Allocation and admission](local-allocation.json) · [Raw observations](observations.tgz) ·
[Checks and procedures](procedures.tar.xz).

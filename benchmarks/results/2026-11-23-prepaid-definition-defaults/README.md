# B1472 Admit a lexical definition with its private defaults

Private `d9a43e331` makes one preflight for a dynamic positional-default
tuple and the final callable, including known annotation storage. The
existing private tuple carries funding through default computation and can
transfer the definition charge without another allowance resize. The
compiler restricts this treatment to lexical definitions without new
captures, decorators, type parameters or generators. Python expression
and annotation evaluation retain their order. Under tight allowances the
earlier preflight can cause earlier refusal and longer overlap with
default-expression scratch.

Compared with `fed081ff0`, each of the two 2,048-input factory runs
makes 4,121 positive allowance requests instead of 6,169. The 520-byte
default and 672-byte callable requests become one 1,192-byte request.
Managed allocation and admitted peaks match on all four scripts.
The allowance returns to baseline after execution, then to zero when
the compiled script is disposed.

## Quiet comparison

Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3, November 23, 2026 UTC.
Both 32-file images passed four CPython fixture goldens. Each lane
used two processes per image, eight seconds of warmup and seven
rotated batches per process. Warm includes applicable mediated
RAM-backed I/O, golden validation and disposal of the prior result.
Time to result adds compilation and compiled-owner cleanup.
Wider process CPU also includes worker reset and protocol.
Startup and final teardown are separate.

| Workload | Boundary | Elapsed change | Wider CPU change | Worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Postponed factories | Time to result | +3.63% to +7.04% | +4.87% to +7.27% | 1.41% / 1.85% |
| Postponed factories | Warm | −2.28% to +3.36% | −1.96% to +3.03% | 1.73% / 3.98% |
| Eager factories | Warm | −5.66% to +6.18% | −4.51% to +6.68% | 11.29% / 1.13% |
| Defaults observer | Warm | +0.25% to +3.70% | +2.11% to +3.39% | 1.35% / 2.06% |
| Fully supplied helper | Warm | −5.20% to +25.63% | −5.85% to +25.39% | 7.44% / 23.34% |

Keep this variant private. The admission reduction is established. The
postponed time-to-result and observer results are adverse, and their
within-source spreads are smaller than the range on the unchanged helper.
The helper and eager runs are inconclusive. A retry would help interpret
those controls, but the extra staged funding state and earlier overlap
provide little reason to repeat this particular source pair. Reconsider
only with a design that removes a construction phase or storage owner
as well as the request.

Windows .NET 11 and Ubuntu .NET 10 each passed 170 white-box and 905
public Release checks, including a new default-expression suspension
case. Cancellation joins the host operation, restores budget and leaves
the file unchanged. Reuse preserves result and default aliases.
All 140 timed batches passed. Both machine gates, worker joins, fixture
removal and archive verification completed. The original local freeze
compared the wrong parent of the two-commit prototype and failed before
producing images. The corrected proof checks the actual base and
verifies every file on both images.

[Source and image identity](identity.json) · [Admission counts](admission.json) ·
[Allocations and peaks](allocation-summary-six.json) · [Timing summary](summary.json) ·
[Raw observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures and checks](procedures.tar.xz).

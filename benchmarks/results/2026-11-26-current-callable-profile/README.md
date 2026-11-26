# B1786 Current warm callable attribution

The two warm synchronous profiles use runtime `0172ed7cf` and the
ordinary and escaping factory inputs from the [B1784 CPython comparison](../2026-11-26-post-fixed-row-cpython/README.md).
On November 26, 2026 UTC, the quiet Azure F4as_v7 ran Ubuntu 24.04.4 and
.NET 11 RC3. Workers warm for at least eight seconds before a six-second
199 Hz CPU capture. Samples include execution, request framing, output
verification and result cleanup. These scripts do no file I/O.

| Observation | Ordinary definitions | Escaping rules |
| --- | ---: | ---: |
| Verified invocations during capture | 31,232 | 7,168 |
| All-thread samples | 1,139 | 1,221 |
| Samples with a resolved runtime ancestor | 1,119 | 1,199 |
| Nearest computed-argument call | 116 | 0 |
| Nearest direct-operand return evaluator | 95 | 93 |
| Nearest closed pair member construction | 0 | 197 |
| Nearest default allocation helper | 99 | 46 |
| Nearest memory reservation | 145 | 77 |
| Of those, allocation helper as nearest caller | 138 | 73 |
| Nearest deferred-definition refund | 25 | 0 |

The ordinary factory still calls a single-operation helper for computed
defaults. Both factories spend sampled time in the shared return evaluator.
Test computing the first arithmetic result in the existing caller and
entering the shared continuation only for a genuine tail. This also lets
ordinary dispatch keep the first result when a later machine operation
declines. Preserve both instruction checkpoints and the exact fallback.
Inlined and unresolved work limit attribution. The counts give no
prediction of removable CPU or elapsed improvement.

Both independent CPython goldens and all 32 image files verify. Both quiet
gates pass, workers and profilers join, and the persistent archive verifies
after fixture removal. Reusing the existing image needed no new worker
storage. The staging floor used the verified 1.18 MB expanded and 0.35 MB
compressed footprint of the preceding two-profile capture.

[Attribution](attribution.json) · [Image identity](identity.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Verification and procedures](procedures.tar.xz)

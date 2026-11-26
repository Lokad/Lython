# B1776 Keep ready JSON values in the active frame

The patch against `7045bcb35` appends ready scalars through the current
construction frame without saving and reloading their property key. It reuses
the already established position of array members. Child containers and
callbacks that suspend retain the same funded continuation. Error coordinates,
callback order, shared admission and cleanup use the existing implementation.

The instrumented build observes 4,096 ready property values and 2,048
established array positions per document totals run. JSON Lines uses the
ready property path 8,192 times per run. The counts match in sync and async
hosting. The text control uses neither path. Peak admission matches across all
cases and allowance returns to baseline. Document and text allocations match,
with a 222 B rise in the JSON Lines diagnostic. [Routes](routes.json) ·
[Allocation](allocation-summary.json)

Windows .NET 11 and Ubuntu WSL .NET 10 each pass 223 white-box and 921 public
JSON Release checks. Both frozen images match three independent CPython
output and file goldens. The focused suite covers nested hooks that suspend,
raw offsets, cancellation, terminal refusal, and retained-result unwind.
[Source patch, goldens and test logs](procedures.tar.xz)

The first November 26, 2026 UTC block used the quiet Azure F4as_v7 running
Ubuntu 24.04.4 and .NET 11 RC3. Each lane uses two workers per image with
five seconds of warmup and seven rotated batches targeting 440 ms. Negative
changes mean lower time. Ranges span all four pairings of worker medians.
Warm elapsed includes applicable RAM-backed I/O, output checking and cleanup.
Wider CPU includes fixture reset and request framing. [Medians and spreads](summary.json)

| Workload | Lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Unicode text control | Warm sync | +3.18% to +7.56% | +3.33% to +5.26% |
| JSON document totals | Warm async | −1.84% to +0.52% | −4.10% to −0.34% |
| JSON Lines report | Warm sync | −1.02% to +0.76% | −2.99% to 0.00% |
| JSON document totals | Warm sync | −3.17% to −2.04% | −4.54% to −0.88% |
| JSON Lines report | Warm async | −1.27% to +5.82% | −0.81% to +5.88% |

The unchanged text control moved adversely with low worker spread. The
confirmation retains the same binaries, inputs and per-lane settings and
repeats text at two positions. Both phases keep individual observations and
machine checks. Initial storage staging stopped at an overly conservative
floor. After applying the launcher’s actual required floor plus headroom,
verified duplicate profile expansions provided the needed room. The existing
archives still hold their original bytes. [Frozen identity](identity.json) ·
[Observations](observations.tar.xz) · [Terminal receipt](terminal-status.json)

The reordered confirmation supports keeping the direct ready path for its
smaller construction state and repeated document sync improvement. Async
timings remain mixed and the text control varies by placement. [Confirmation](confirmation/README.md)

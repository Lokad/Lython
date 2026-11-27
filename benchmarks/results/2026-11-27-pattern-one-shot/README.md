# B1862 Match once directly on the compiled regex pattern

Runtime matching and frontend literal analysis now borrow the original
UTF-8 subject through `ILythonRegexPattern.TryMatch`. The provider completes
its working storage and releases the borrow within that call. This removes
the per-match prepared subject and its forwarding and disposal. Cursor
preparation keeps its separate lifetime. Both callers admit capture storage
before invoking the provider.

The reference contains the runtime from `7305efabf`. The candidate uses
`b285b0bcd` plus the archived source patch. The source revisions differ
only by documentation and benchmark evidence before the patch. Only
the Lython core and optional adapter DLLs differ between the 32-file
images. Both use the same signed PythonRe 0.5.0 backend and comparison
host `05c1059de`. Both verify all five complete scripts against independent
CPython output and file goldens on Windows and on the quiet VM.

## Allocation and admission

Windows Release .NET 11, warm CLR bytes per invocation. The log scripts
scan 3,072 lines and consume a group. Every row returns to its compiled
allowance after cleanup, with matching compilation and execution peaks.

| Workload | Sync bytes saved | Async bytes saved | Peak shared bytes |
| --- | ---: | ---: | ---: |
| Compiled-pattern log search | 221,166 (10.86%) | 221,152 (10.85%) | 1,178,381 |
| Module-level log search | 221,122 (9.90%) | 221,072 (9.90%) | 1,178,293 |
| Cursor control | 0 | 0 | 523,444 |
| Quoted CSV invoice control | 672 (0.02%) | 44 (<0.01%) | 9,040,266 |
| Generated regex selectors, warm execution | 3,576 (12.35%) | 3,456 (11.89%) | 9,722 |

## Matched timing

The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 27,
2026 UTC. Two workers per image warm for at least five seconds each
and contribute seven rotated batches. Ranges span all pairings of
worker medians. Warm elapsed covers verification, mediated RAM-backed I/O
and disposal of the previous result. The compile-only lane covers
compilation and compiled-owner cleanup. The wider process CPU clock
includes fixture reset and request framing. Process startup is outside
these clocks.

| Workload | Boundary | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Compiled-pattern log search | Warm | −2.20% to −0.91% | −1.96% to −0.29% |
| Module-level log search | Warm | −4.80% to −2.69% | −3.67% to −0.85% |
| Cursor control | Warm | +0.23% to +3.46% | +0.17% to +3.82% |
| Quoted CSV invoice control | Warm | −2.41% to +1.87% | −2.63% to +2.22% |
| Generated regex selectors | Compile only | −2.46% to +2.73% | −2.22% to +4.09% |

Both log scripts use less CPU in this pair. The cursor control moves
adversely despite unchanged cursor allocation and lifetime. Its cause
remains open. The CSV and compile-only timing ranges span zero. Keep the
simplification for its lower allocation and shorter ownership lifetime.
These internal ratios leave the historical CPython ratios as measured.

All 140 batches and both machine gates pass in 200.33 seconds of
collection. Workers join and the persistent archive verifies under the
lease after RAM-backed fixtures are removed. The focused Release filter
passes 743 tests on each target on Windows and Ubuntu WSL. It includes
cooperating-provider cancellation and refusal, signed-backend interruption,
capture ranges and reuse of the allowance. The local Windows Release
package passes both installed-consumer checks.

[Timing summary](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Checks and exact patches](procedures.tar.xz).

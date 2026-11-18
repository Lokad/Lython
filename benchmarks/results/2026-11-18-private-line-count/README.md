# Count private split lines without constructing a list

B983 compared private `74d19a10` with committed `6f494109f` / `e4905aa5`
on the quiet Azure F4as_v7 VM on 2026-11-18. The verified 32-file .NET 11 RC3
images differ only in the core DLL. All four scripts matched CPython output
and file goldens before measurement.

Both executable drivers reuse their adjacent producer/consumer path. An
exact, zero-argument `str.splitlines()` immediately consumed by exact builtin
`len` uses the ordinary split-list builder's UTF-8 sizing pass as its result.
Fragment replay, text allocations, list capacity and list publication are
unnecessary. The scanner handles CRLF across chunk boundaries and every
Python Unicode line separator. The change keeps stack admission and
cancellation on both calls. Shadowed functions, custom receivers, explicit
`keepends` and escaping lists keep the ordinary call path and preflights.

| Script and phase | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Normalization, warm | −15.56 to −15.01% | −13.86 to −12.95% | 0.36 / 0.29% |
| Helper text, warm | −8.03 to −6.27% | −7.90 to −6.56% | 0.94 / 0.97% |
| Helper text, compile-to-result | −7.27 to −5.45% | −7.58 to −5.86% | 0.83 / 1.12% |
| Validated CSV, warm control | −3.88 to +2.92% | −4.96 to +3.39% | 4.41 / 2.54% |
| Positional calls, warm control | −1.46 to −0.67% | −3.12 to +0.86% | 0.41 / 0.39% |

Ranges cover all pairings of two process medians per image. Negative values
favor the candidate. The workers used eight-second warmup and five batches
per process, ordinary GC and tiering, and RAM-backed guest files. Warm
includes mediated file I/O, output checks and prior-result cleanup.
Compile-to-result adds compilation and compiled-owner disposal. Process CPU
covers reset and protocol too. Startup is outside these elapsed clocks.
The five jobs finished in 230.78 seconds. Both quiet endpoints passed, all
workers joined and the complete archive matched its recorded SHA-256 hash.
The VM retired only the two exact working aliases of `643f3980` after
verifying the committed archive, file hashes, path, owner and link count.
[Archived directive-presence image](../2026-11-18-directive-presence/candidate-core.dll.gz)

Local diagnostics show 745,784 to 540,880 allocated bytes for normalization
(27.47% lower), and 1,288,040 to 1,083,136 for helper text (15.91% lower).
Accounted peaks fell from 1,229,513 to 399,166 and 3,009,731 to 2,343,490
bytes respectively. Controls matched their allocation and peaks, and every
allowance returned to baseline. Windows passed 117 focused Release cases and
WSL passed 75 on each actual framework. Eighty-four independent Unicode and chunk-boundary
programs matched CPython 3.13.2 through each API and framework. Focused
regressions cover the guarded list omission and terminal refusal when the
list remains visible to Python.

Retain the two text gains and deletion of the private list construction.
Validated CSV remains uncertain within moving controls. This collection
compares two Lython images. The preceding direct CPython baseline measures
an earlier revision.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source and checks](checks-and-procedures.tar.xz)

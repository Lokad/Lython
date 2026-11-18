# Join private trimmed splits directly into UTF-8 storage

B981 compared `e4905aa5` with `b810f660b` / `93739eed` on the quiet Azure
F4as_v7 VM on 2026-11-18. The verified 32-file .NET 11 RC3 workers differ
only in the core DLL. All four scripts passed the same CPython output and
file golden checks before measurement.

When both evaluated methods are the exact string builtins, the executable
engine can combine `sep.join(line.rstrip() for line in text.split(delim))`.
A nonempty explicit delimiter lets it replay immutable UTF-8 ranges and
write directly into admitted final storage. This removes the eager split
list, temporary text slices, generator frame and join drain for this
private sequence. Other call shapes and dynamic receivers keep ordinary
Python evaluation and iteration. Both paths use the same delimiter and
whitespace scans.

| Script and phase | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Normalization, warm | −43.42 to −42.38% | −40.02 to −39.26% | 1.77 / 0.06% |
| Normalization, compile-to-result | −40.36 to −39.52% | −36.79 to −35.08% | 0.96 / 0.44% |
| Helper text, warm | −1.29 to +0.80% | −1.44 to −0.85% | 1.55 / 0.55% |
| Validated CSV, warm | −1.01 to +0.41% | −2.83 to +0.34% | 0.80 / 0.63% |
| Positional calls, warm | −3.44 to −1.25% | −4.38 to −3.92% | 0.46 / 1.81% |

Ranges cover all pairings of two process medians per image. Negative
values favor the candidate. The workers used eight-second warmup and five
samples per process, ordinary GC and tiering, and RAM-backed guest files.
Warm includes mediated file I/O, output checks and previous-result cleanup.
Compile-to-result adds compilation and compiled-owner disposal. Process CPU
also includes reset and protocol. Startup is outside these elapsed clocks.
All five jobs finished in 230.95 seconds. Both quiet endpoints passed, all
workers joined, and the archived observations were verified before unmount.

Local diagnostic allocation for 4,096 lines fell from 1,255,352 to 745,784
bytes (40.59%). The accounted peak fell from 2,355,001 to 1,229,513 bytes
(47.79%). The measured controls had unchanged peaks and all allowances
returned to baseline. Windows passed 74 white-box and 266 public Release
checks on each actual framework. WSL passed 36 focused public checks per
framework. On both frameworks, 39 independent probes matched CPython
3.13.2 on both execution APIs. They cover Unicode whitespace and delimiters,
empty-delimiter errors, method effects and the observable generator passed
to a custom consumer. Focused regressions cover governed result refusal
and cleanup.

The target elapsed and CPU changes support retention. The control rows
bound the observed side effects and make no broader speedup claim. This is
a comparison between two Lython images. An external-baseline refresh and
full platform qualification are tracked separately.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source and checks](checks-and-procedures.tar.xz)

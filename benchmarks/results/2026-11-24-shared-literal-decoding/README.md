# B1638–B1639 Shared text and bytes literal decoding

Text, bytes and f-string text share escape classification. A `\x`
escape requires two hexadecimal digits. Bytes decode from the token
span into byte storage. Executable code, lowered expressions and
frontend byte facts use the same immutable payload. The compiled path
eliminates the intermediate decoded CLR string and the later payload
copy. Both trials keep the existing compile owner's coarse admission.

The reference is `31c353bb2`, which has the same runtime and tests as
`3d8f68aaf`. The 32-file reference and candidate images differ only in
the core DLL and all files verify by hash. Both compare against the
same independent CPython goldens. The quiet Azure F4as_v7 ran Ubuntu
24.04.4 and .NET 11 RC3 on November 24, 2026 UTC. Two workers per
image warm for five seconds of full-request time and each contributes
seven rotated batches. Ranges span their four process-median pairings.
Positive means more time.

Compilation includes code-owner disposal. Time to result adds one
execution with applicable RAM-backed I/O, output and file verification
and prior-result disposal. Process CPU includes reset and protocol.
Startup and final process teardown are separate. The generated table
embeds 16,384 bytes as 64 adjacent literals and has no file I/O. The
manifest reads two files and the report reads 3,072 lines.

## Initial candidate

| Workload | Phase | Elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Generated binary table | Compile | +8.09 to +9.58% | +8.01 to +8.78% | 1.11 / 0.27% |
| Configuration manifest | Compile | −0.73 to +0.40% | −2.20 to +0.46% | 0.32 / 0.81% |
| Generated binary table | Compile to result | +0.35 to +0.48% | −1.80 to +2.29% | 0.12 / 0.02% |
| Short Unicode report | Compile to result | +1.93 to +6.68% | +2.19 to +6.37% | 0.83 / 3.80% |
| Configuration manifest | Compile to result | −0.23 to +0.55% | −0.53 to +0.00% | 0.06 / 0.72% |

The initial candidate spends more CPU compiling the binary script and
through result for the report. The bounded refinement replaces the
remaining per-character ASCII validation loop with the runtime's
bounded ASCII check. It keeps the same error order before decoding.

## Retained refinement

| Workload | Phase | Elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Short Unicode report | Compile to result | −12.74 to +0.31% | −10.51 to +0.00% | 2.96 / 11.64% |
| Generated binary table | Compile | −9.87 to −8.62% | −11.39 to −8.74% | 0.84 / 0.54% |
| Configuration manifest | Compile | −1.57 to +0.28% | −2.42 to +2.07% | 0.80 / 1.08% |
| Generated binary table | Compile to result | −7.70 to −6.63% | −8.81 to −7.14% | 0.25 / 0.91% |

The binary diagnostic improves in both phases with the same allocation
savings. The manifest compilation ranges cross zero. The report's
candidate workers disagree, so the change in report timing remains
inconclusive. Keep the original adverse run. Across both trials, 252
timed batches and both sets of quiet gates pass. All workers join,
the guest fixtures are removed, and the raw archives verify.

## Compilation storage and semantics

| Workload | Compiler bytes allocated, reference / both candidates | Retained compiler admission |
| --- | ---: | ---: |
| Generated binary table | 528,112 / 143,032 B | 6,384,320 B |
| Configuration manifest | 217,768 / 217,648 B | 212,960 B |
| Short Unicode report | 81,064 / 81,064 B | 92,127 B |

The binary table saves 385,080 B, or 72.92%, of observed managed
compiler allocation. Admission matches and the shared budget returns
to zero after every disposal. The current-thread allocation diagnostic
ran separately on Windows and includes compilation and code-owner
disposal. Use the VM comparisons for timings.

In both execution APIs, seven independent CPython 3.13.2 programs
verify ordinary and raw escapes, adjacent bytes, physical line joining,
mapping patterns, f-strings and postponed annotations. Both candidates
reject three malformed forms that the earlier Lython accepts and
CPython rejects. Each platform passes 638 white-box and 2,922 public
initial .NET 11 Release checks and 172 focused .NET 10 checks. The refined
source passes 172 focused checks on each target and platform. The
initial WSL subprocess test fails because its default `python` command
is absent. Rebuilding the matching probe and supplying the installed
CPython 3.13.2 alias passes the same filter.

[Initial timings](quiet-summary.json) · [Initial image identity](identity.json) ·
[Initial raw observations](quiet-observations.tar.xz) · [Initial receipt](quiet-terminal-status.json) ·
[Refined timings](refinement/quiet-summary.json) · [Refined image identity](refinement/identity.json) ·
[Refined raw observations](refinement/quiet-observations.tar.xz) · [Refined receipt](refinement/quiet-terminal-status.json) ·
[Compiler ownership](compiler-ownership.json) · [Procedures and exact patches](procedures.tar.xz).

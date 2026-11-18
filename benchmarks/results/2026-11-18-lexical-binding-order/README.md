# Derive lexical binding membership and order together

B989 compares `f4268303` with `bfe076110` / `610d403d` on the quiet Azure
F4as_v7 VM on 2026-11-18. Verified 32-file .NET 11 RC3 images differ only
in the core DLL. Both pass the same four script and output-file goldens.
This compiler source pair is separate from the later exact-list runtime change.

The lexical collector compacts its scratch binding list in source order and
builds the one membership set used by lexical lookup. This removes a second
deduplication set and LINQ enumeration of the collected bindings. Global and
nonlocal names remain excluded. Class-cell analysis reuses lexical expression
enumeration for deleted targets, including tuple and parenthesized targets.

| Local diagnostic script | Lexical preparation before | After |
| --- | ---: | ---: |
| CSV invoice | 16,096 B | 14,392 B |
| Helper normalization | 7,176 B | 6,200 B |
| Configuration edit | 5,104 B | 4,616 B |
| Positional calls | 5,344 B | 4,448 B |

All other observed diagnostic phase allocations matched. Warm allocation,
accounted peaks and allowance restitution matched. Both Windows targets
passed 226 focused public Release checks, and WSL passed 140 per actual
framework. Fourteen programs yield identical lexical facts and diagnostics,
including order, class-cell requirements and nested nonlocals, across 36
inspected scopes. The isolated WSL worktree emits SourceLink warnings.

| Complete script and phase | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| CSV invoice, compile | −2.81 to −0.36% | −3.24 to −0.38% | 0.62 / 1.89% |
| Configuration edit, compile | −0.73 to +1.65% | +0.33 to +1.88% | 1.13 / 1.26% |
| Configuration edit, compile-to-result | −1.78 to −0.47% | −0.30 to +0.46% | 0.87 / 0.46% |
| Helper normalization, compile-to-result | −0.09 to +1.33% | −1.03 to +1.82% | 0.06 / 1.36% |
| Positional calls, warm control | +0.56 to +0.86% | −1.56 to +2.08% | 0.10 / 0.20% |

Ranges cover all pairings of two process medians per image. Negative favors
the candidate. Each process ran eight-second warmups and five measured
batches with normal GC and tiering. Compilation includes compiled-owner
disposal. Compile-to-result adds execution with RAM-backed mediated file
I/O, output verification and cleanup. Process CPU also covers reset and
protocol. Startup stays outside elapsed. All five lanes completed in
232.63 seconds, the quiet endpoints passed, all workers joined and the
complete observation archive matched its SHA-256 receipt. The VM retired
three older `93739eed` working aliases and two exact committed observation
replicas after checking ownership, paths, hashes and inode aliases.
[Archived consumed-slot image](../2026-11-18-local-store-slots/candidate-core.dll.gz)

Retain the simpler binding collector and established allocation reduction.
CSV compilation improves modestly by both clocks. Configuration compilation
CPU and positional warm elapsed each show small adverse movement. Helper
compile-to-result, configuration one-shot CPU and positional CPU remain
mixed. Keep these limits for the next retained compiler checkpoint. The
comparison establishes neither a general execution gain nor a CPython ratio.

[Timing](summary.json) · [Phase allocation](phase-summary.json) ·
[Warm allocation and reuse](allocation-summary.json) · [Raw observations](observations.tgz) ·
[Source and checks](checks-and-procedures.tar.xz)

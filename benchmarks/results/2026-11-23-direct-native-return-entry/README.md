# B1455 Prepare closed integer returns before the native workspace

Private `2f83b871a` moves the existing closed integer return ahead of
ordinary workspace entry and fixed-return setup. The normal call still
owns the full storage for wide, dynamic and suspended bodies. The
reference is `107536d02`, using the identical retained B1454 core image.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on
November 23, 2026 UTC. Two fresh processes per source supplied seven
rotated batches after eight seconds of warmup per process. Ranges span
the four pairings of process medians. Negative values favor the
candidate. Warm elapsed includes mediated RAM-backed I/O, verification
and previous-result disposal. Compile to result adds compilation and
compiled-owner disposal. Process CPU also includes worker protocol.
Startup and final teardown are outside the timed interval.

| Workload | Boundary | Elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Postponed factories | Warm | −2.57 to +5.78% | −1.24 to +7.19% | 1.23 / 7.24% |
| Postponed factories | Compile to result | +0.40 to +3.58% | −0.18 to +3.64% | 0.89 / 2.26% |
| Eager factories | Warm | +1.35 to +3.78% | +2.36 to +3.43% | 1.47 / 0.92% |
| Fully supplied helper | Warm | +0.99 to +10.09% | +1.02 to +10.19% | 0.22 / 8.78% |
| Large positional call loop | Warm | −5.14 to −2.74% | −4.62 to −2.71% | 1.88 / 0.64% |
| Wide native-buffer call loop | Warm | −7.11 to +7.46% | −8.36 to +9.24% | 7.62 / 7.50% |
| Quote-heavy CSV invoice | Warm | −3.35 to −1.32% | −3.45 to −2.44% | 0.54 / 1.56% |
| Ordinary split report | Warm | −2.05 to +1.26% | −3.87 to +1.46% | 2.87 / 0.49% |

Keep the runtime change private. The call loop and invoice improve,
while eager factories consistently use more CPU and the fully supplied
helper also takes longer. The postponed and wide lanes leave uncertainty
about their warm costs. The source moves dispatch work as it removes
preparation. The timing does not isolate which change drives each result.

Tier 1 x64 stack subtraction in `StartNativeCall` falls 504 → 448 B on
Windows and 440 → 384 B on Linux. The interpreter caller rises
408 → 424 B and 248 → 296 B respectively. The nested caller stays at
200 B and 168 B and can use a separate 80 B or 56 B direct helper.
These subtractions exclude saved registers. All admitted execution peaks
and compilation reservations match. Local allocation medians match
except for 120 B more on the 4.07 MB Linux invoice. All owners refund.

Both 32-file images pass eight CPython goldens and all 224 timed batches
verify. The quiet-VM archive verifies after workers join and guest
fixtures are removed. Focused Release runtime regressions pass on
Windows .NET 11 and Ubuntu .NET 10. The scalar-unpack error-order case
also passes on the retained runtime, together with the established
native-return and subprocess stack suite (125 checks per target).

In the separate strengthened Linux continuation probe, guest checks
pass but both current images resume on a thread other than the one
with the 256 KiB stack. The earlier control images resume on the
assigned thread in the follow-up. The strengthened probe does not
qualify the new stack layout. Its two result sets remain in the archive.

[Timing](summary.json) · [Image identities](identity.json) ·
[JIT and ownership](jit-and-ownership.json) · [Raw observations](observations.tar.xz) ·
[Source and checks](procedures.tar.xz) · [Archive receipt](terminal-status.json).

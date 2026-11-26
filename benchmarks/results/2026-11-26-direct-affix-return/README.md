# B1746 Direct return for an exact string affix helper

An eligible Python helper that returns `value.startswith('literal')` or
`value.endswith('literal')` can complete from its supplied operand. The
compiler verifies the complete four-instruction body. Both executable
drivers already call the direct-return evaluator, which uses the existing
affix operation for an exact string. It skips the callee workspace and
receiver-method dispatch. Ordinary guest members still resolve and suspend
through the original tape. The function and builtin stack checks remain.

| Workload | Phase | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Protected file search | Compile-to-result | −25.80% to −23.69% | −21.89% to −20.28% |
| Unicode text report | Warm sync | −1.09% to +6.60% | −1.24% to +7.52% |
| Protected search with dynamic receiver | Warm sync | −1.36% to +1.77% | −1.23% to +1.67% |
| File-fed CSV quantity map | Warm async | +2.97% to +3.45% | +2.78% to +3.03% |
| Protected file search | Warm sync | −26.18% to −23.14% | −20.34% to −18.81% |

The November 26, 2026 UTC comparison pairs `265e48ba2` with the archived
patch on the Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. Ranges span the
four pairings of process medians. Two workers per build warm for seven
seconds and provide seven rotated batches each. Warm elapsed includes
mediated RAM-backed I/O, output checks and previous-result disposal.
Compile-to-result also compiles and disposes compiled code. Wider process
CPU includes fixture reset and request framing. Process startup is outside
these clocks.

A route check using the exact frozen DLLs sees a string match and miss
complete directly in the candidate. A non-string receiver declines, and
the new public check verifies a custom descriptor that suspends and can
cancel without publishing output or retaining the budget. The original
search helper has one input local and five value slots. The candidate
keeps the same tape for fallback and completes the ordinary exact call
before preparing a workspace. Local compile and warm execution allocations
match on all four scripts. Peak admission also matches and allowance
returns to baseline. Windows .NET 11 and Ubuntu .NET 10 each passed 1,267
public and 835 internal focused Release checks.

The first launch stopped at the loaded-core hash check. Its symlinked
benchmark entry assembly resolved the reference core. Copying the entry
assembly and its metadata into the candidate directory fixed the image
layout. The retry verified the loaded core and passed four CPython goldens
before timed collection. The original failure remains in the procedures
archive. The valid 140 batches passed both quiet checks, output checks,
worker joins, fixture cleanup and persistent archive verification.

## B1747 Reordered confirmation

The confirmation uses the same images and catalog, with file-fed CSV
first and Unicode text last. Process medians use the same clocks and
rotated sampling as the first valid block.

| Workload | Phase | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| File-fed CSV quantity map | Warm async | −4.18% to −2.20% | −3.76% to −1.44% |
| Protected file search | Compile-to-result | −29.17% to −23.17% | −25.06% to −19.12% |
| Unicode text report | Warm sync | +2.20% to +3.69% | +2.74% to +4.57% |

Retain the direct return. The through-result gain repeats for protected
search. The file-fed control reverses direction. Its quantity helper has
the same executable instructions and direct-return eligibility in both
images. The Unicode text report incurs a measurable cost in confirmation.
Keep that regression open. Its cause remains unverified. All 84 confirmation
batches passed, along with both quiet checks, worker joins, fixture cleanup
and persistent archive verification.

[Confirmation summary](confirmation/summary.json) ·
[Confirmation observations](confirmation/observations.tar.xz) ·
[Confirmation terminal status](confirmation/terminal-status.json) ·
[Confirmation procedures](confirmation/procedures.tar.xz).

[Identity](identity.json) · [Timing summary](quiet-summary.json) ·
[Execution allocation](allocation-summary.json) · [Compilation allocation](compile-allocation-summary.json) ·
[Direct-return check](direct-return-check.json) · [Observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Procedures and patch](procedures.tar.xz).

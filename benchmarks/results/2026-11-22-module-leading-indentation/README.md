# B1405 Module opening indentation

Retained at `55424591f`. The production and test diff matches the
measured candidate `2813c9b42` against `8c7da02be`. The tokenizer now
reports leading module indentation through its existing indentation
reader. This removes the separate scan of leading comments and the
first content line. Fragment tokenization is unchanged.

| Workload and boundary | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Sparse generated validator, compile | +0.15 to +1.37% | −1.45 to +1.04% |
| Dataclass configuration, compile | −0.98 to +1.70% | −1.21 to +0.86% |
| Generated read-only branches, compile | −0.61 to +1.65% | −0.82 to +4.33% |
| Synthetic 64 KiB first-line literal, compile | −22.39 to −13.46% | −22.88 to −13.13% |
| Sparse generated validator, compile to result | −0.33 to +1.13% | −1.23 to +0.97% |

Negative values mean lower time. These diagnostic ranges span the
pairings of two reference and two candidate worker medians, each with
seven batches. All 140 timed batches and both quiet gates passed on the
Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on November 22, 2026
UTC. Compilation includes compiled-owner disposal. Compile to result
also includes execution, result verification and cleanup, with startup
and fixture preparation outside the clock. The wide literal shows where
the duplicate scan mattered. Its reference workers differ by 11.17%,
and even the smallest observed improvement exceeds that spread. The
validator compile slowdown is small and its CPU range spans zero. Keep
the read-only branch CPU increase as unresolved. Local compile
allocation settled at matching values. The earlier noisy desktop
timings remain in the procedure archive.

Both Windows .NET 11 and Ubuntu .NET 10 pass 788 focused Release
checks. Five complete scripts verify against CPython 3.13.2. All 61
inspected executable objects have matching instructions, slots, source
spans and exception regions. A 949-input diagnostic matrix matches
normalized spans, error precedence, validity and allowance cleanup.
Blank and comment lines before an invalid first indentation now
populate the tokenizer's line map within the existing compilation
allowance. The initial catalog with inherited fixture-size metadata
failed verification and was corrected before collection.

[Timing and CPU observations](summary.json) · [Source and image hashes](source.json) ·
[Local allocation](local-allocation.json) · [Raw measurements](observations.tgz) ·
[Tests and procedures](procedures.tar.xz).

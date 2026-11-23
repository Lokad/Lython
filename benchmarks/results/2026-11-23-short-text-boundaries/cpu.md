# B1535 locate the short word report's CPU cost

All three source shapes use the same Lython runtime at `0112138c1`,
the same 3,072-line fixture and the same report. The measured variant
first moves the width update into the caller, then also moves the
`line.lower().split()` expression next to its consuming loop.

| Source comparison | Warm elapsed change | Whole-request CPU change |
| --- | ---: | ---: |
| Move only the width update to the caller | −1.92% to +0.98% | −2.12% to +0.87% |
| Also inline the per-line helper, compared with the preceding variant | −27.75% to −25.27% | −26.78% to −24.11% |
| Inline the helper, compared with the original | −27.41% to −26.33% | −26.94% to −24.91% |

Moving the nonlocal store on its own yields overlapping timings. Inlining
the helper removes both its entry and its separate native execution of the
same cased split expression. All three compiled tapes keep the native
dictionary loop. The common casing kernel remains. The difference supports
investigating call entry and the handoff to private iteration. It combines
runtime work at several boundaries, so it supplies no cost estimate for
any individual guard or frame operation and no speedup claim for the
unchanged representative source.

Two workers per source ran on the quiet Azure F4as_v7 VM with Ubuntu
24.04.4 and .NET 11 RC3 on November 23, 2026 UTC. Each worker warmed eight
seconds and contributed seven rotated batches. Ranges pair the worker
medians. Warm elapsed includes mediated RAM-backed I/O, verification and
prior-result disposal. The CPU clock includes request overhead. Compilation
and process startup are outside both clocks. All 42 batches, output/file
goldens, full image hashes, quiet gates, worker joins and archive checks pass.

[Timing](summary.json) · [Raw observations](observations.tar.xz) ·
[Terminal receipt](terminal-status.json) ·
[Procedures](procedures.tar.xz) · [Allocation and source shapes](README.md).

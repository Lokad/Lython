# B1892 Fill an admitted call buffer before leaving the generator

Starred calls and arbitrary `starmap` rows use the full admitted positional
capacity during a single generator advance. A four-yield row can reach
exhaustion in one frame entry instead of two. An excess yield still leaves
the frame before the consumer admits replacement storage. Bounded list
batches still stop at their requested size. The focused delayed-source
fixture fills all four positions, suspends on a host read, and verifies
ordered overflow or cancellation and cleanup. The callback checks that the
first argument retains its identity.

The reference is `b76704821`. The candidate applies the archived patch to
that revision. Only the core DLL differs between the verified 32-file
images. Both pass four independent CPython 3.13.16 output and file goldens.
The affected script reads 2,048 rows from a 27,350-byte CSV fixture. The
Windows .NET 11 allocation witness finds equal admitted peaks for all four
scripts and both APIs. The affected sync lane allocates 2,645,710 CLR bytes
in each image. The async lane allocates 2,645,894 B before and 2,645,950 B
after. Every measured invocation restores its borrowed allowance baseline.

Focused Release checks pass on Windows and Ubuntu WSL with .NET 10 and
.NET 11. The public runs pass 649 on Windows .NET 11 and 126 per remaining
configuration. Each white-box run passes 60. Coverage includes expanded
calls, refusal, cancellation, generator cleanup and retained aliases.

## Paired timing

Ubuntu WSL ran on one logical CPU with .NET 11 RC3 on November 27. The
quiet Azure VM had 4,080 KiB of persistent space available and was used
only for the storage check. Two workers per image warm for at least five
seconds each and supply seven rotated batches. Ranges span pairings of
worker medians. Warm elapsed includes mediated RAM-backed I/O, output and
file verification and prior-result cleanup. Process CPU also includes
fixture reset and request framing. Compilation and startup are separate.

| Pass | Workload | API | Elapsed change | Process CPU change |
| --- | --- | --- | ---: | ---: |
| Initial | Iterable shipment | `Run` | −31.88% to −8.63% | −12.50% to −1.74% |
| Initial | Iterable shipment | `RunAsync` | −11.75% to −0.35% | −10.95% to −3.50% |
| Initial | Wide shipment control | `RunAsync` | −8.35% to −1.62% | −11.74% to −3.06% |
| Initial | Bound shipment control | `RunAsync` | −3.09% to +18.72% | 0.00% to +5.88% |
| Initial | File-fed map control | `RunAsync` | −2.74% to +5.11% | +5.07% to +11.24% |
| Reverse placement | Iterable shipment | `Run` | −7.26% to +0.94% | −5.87% to +0.74% |
| Reverse placement | Iterable shipment | `RunAsync` | −6.70% to −0.54% | −10.00% to +1.89% |
| Reverse placement | File-fed map control | `RunAsync` | −15.68% to +5.51% | −5.46% to +5.62% |

The first sync reference workers differ by 32.48% in elapsed, and the
unchanged wide control also improves. The file-fed CPU increase from the
first pass spans zero with reversed placement. Iterable CPU likewise
spans zero in both APIs on the repeat. Keep this change for the removed
frame handoff. The timing ranges are diagnostic. All 224 timed batches
verify. The workers join before the persistent archives verify.

[Initial timing](summary.json) · [Reverse-placement timing](repeat/summary.json) ·
[Allocation and admission](allocation-summary.json) · [Image identity](identity.json) ·
[Initial observations](observations.tar.xz) · [Repeat observations](repeat/observations.tar.xz) ·
[Exact source and verification](procedures.tar.xz).

# B1867 Feed dictionary fields directly into the CSV row writer

`DictWriter` previously created an async iterator to pass resolved values to
the row writer in both hosting modes. The shared row loop now resolves each
field immediately before formatting it. The captured append continuation and
separate `DictWriter.writerows` loop are gone. The row builder still owns its
bytes until the write completes or unwinds.

Both 32-file images match three independent CPython 3.13.2 output and file
goldens. Only the core DLL differs from `f0c7b5ad6`. The affected report reads
2,048 CSV records and writes 27,350 bytes in a different column order. A plain
writer produces the same file and exercises the shared loop without mapping
callbacks. The file-fed map is disjoint from CSV output.

Both Windows Release targets and Ubuntu WSL .NET 10 each pass 505 public and
165 white-box checks. The new cases verify live field-list and default changes,
awaited mapping I/O, disposal and allowance reuse. Cancellation leaves the
flushed row intact and discards the unfinished row. The first fixture assumed
that an unflushed row already reached the host. The corrected fixture flushes
that row before the interrupted read. CPython verifies the completed file.
The first build's cursor return-type change made existing extension calls
ambiguous. The corrected build retains the original enumerable signature.

## Allocation and admission

Windows Release .NET 11, median managed bytes per command. All executions
return their shared allowance to the compilation baseline.

| Workload | Sync CLR bytes | Async CLR bytes | Peak shared bytes in both APIs |
| --- | ---: | ---: | ---: |
| Dictionary report | 7,948,916 → 7,194,772 | 8,031,092 → 7,276,948 | 6,775,123 → 6,775,123 |
| Plain writer | 1,834,054 → 1,538,888 | 1,834,264 → 1,539,096 | 3,104,123 → 3,104,123 |
| File-fed map | 808,824 → 808,768 | 808,984 → 808,984 | 2,270,071 → 2,270,071 |

## Local warm timing

Ubuntu WSL and .NET 11 RC3, one logical CPU, two workers per image.
Each worker warms for five seconds and contributes seven rotated batches.
Intervals span all pairings of worker medians. Elapsed includes mediated
RAM-backed I/O, output and file verification and prior-result cleanup. CPU
also includes fixture reset and request framing. Startup and compilation
are outside both clocks. All 112 batches verify.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Dictionary report | Sync | −12.53% to −1.22% | −7.99% to +1.47% |
| Plain writer | Async | −20.01% to −19.32% | −19.00% to −11.22% |
| Dictionary report | Async | −12.81% to +1.20% | −13.55% to +0.19% |
| File-fed map | Async | +1.84% to +7.20% | +2.75% to +8.50% |

Both timed APIs use completed comparison-host I/O. The focused tests check
real suspension independently. The higher unchanged map times warrant a
bounded reverse-order repeat on the same images and catalog.

| Workload | API | Repeat elapsed change | Repeat process CPU change |
| --- | --- | ---: | ---: |
| File-fed map | Async | −3.69% to +2.12% | −1.69% to −0.12% |
| Dictionary report | Async | −13.55% to −1.93% | −8.88% to −4.52% |

Retain the shared loop. The dictionary report saves 754,144 managed bytes per
command in each mode at the same admitted peak. The async repeat uses less
elapsed and CPU across all worker pairings, and the initial map slowdown
does not recur. The first block's dictionary CPU ranges still span zero.
The repeat verifies all 56 batches and both collections join their workers
and preserve the observations. Use a quiet checkpoint for a broader claim.

[Initial timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Archive verification](archive-receipt.json) · [Patch and checks](procedures.tar.xz) ·
[Repeat timing](repeat/summary.json) · [Repeat observations](repeat/observations.tar.xz) ·
[Repeat checks](repeat/procedures.tar.xz).

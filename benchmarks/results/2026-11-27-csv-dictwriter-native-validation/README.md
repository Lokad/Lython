# B1868 Validate CSV dictionary fields in their native slots

An ordinary `DictWriter` row only needs to know whether its keys are covered
by the current field names. The validator matches exact strings against live
native dictionary slots and tracks coverage in one scalar mask. It removes
the dictionary keys view, two materialized operands and their set difference
from the eligible path. Duplicate fields count once for coverage and still
produce repeated columns. Wider tables and protocol values retain the general
subtraction and processing order.

The reference is `fd3e24896`. Only the core DLL differs in the two frozen
32-file images. Both match the same three independent CPython 3.13.2 output
and file goldens. The dictionary report reads 2,048 input records and writes
27,350 bytes with reordered fields. The plain writer report and file-fed map
exercise unchanged paths.

Each Windows Release target and Ubuntu WSL .NET 10 pass 507 public and 168
white-box checks. Two new cases match CPython with deleted slots, reordered
and duplicate fields, a live header change, missing and extra fields, an
unhashable field and rows at and beyond the scalar mask width. Existing
tests cover cancellation, suspended mapping callbacks, refusal and cleanup.

## Allocation and admission

Windows Release .NET 11, median managed bytes per command. Every command
returns the shared allowance to the compilation baseline. Retained compilation
matches for each workload.

| Workload | Sync CLR bytes | Async CLR bytes | Peak shared bytes in both APIs |
| --- | ---: | ---: | ---: |
| Dictionary report | 7,194,804 → 3,129,438 | 7,276,980 → 3,129,654 | 6,775,123 → 2,316,075 |
| Plain writer | 1,538,992 → 1,538,886 | 1,539,150 → 1,539,064 | 3,104,123 → 3,104,123 |
| File-fed map | 808,768 → 808,768 | 808,976 → 808,984 | 2,270,071 → 2,270,071 |

## Local warm timing

Ubuntu WSL with .NET 11 RC3, one logical CPU and two workers per image. Each
worker warms for five seconds and contributes seven rotated batches. Intervals
span all pairings of worker medians. Elapsed includes mediated RAM-backed I/O,
file and output verification and previous-result disposal. Process CPU also
includes fixture reset and request framing. Startup and compilation sit outside
both clocks. All 112 batches verify and the archive records joined workers.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| File-fed map | Async | −4.99% to +4.90% | −4.70% to +4.26% |
| Dictionary report | Async | −54.74% to −49.08% | −52.86% to −47.93% |
| Plain writer | Async | +0.67% to +2.73% | +0.44% to +1.37% |
| Dictionary report | Sync | −55.93% to −53.71% | −55.11% to −52.82% |

Keep the direct validation. The dictionary report allocates 56.50–56.99% less
managed memory, needs 65.82% less peak admission and takes less time across
all observed pairs. File-fed timing spans zero and candidate worker elapsed
spread reaches 9.60% there. The unrelated plain writer takes 0.67–2.73% longer
locally. Carry that control into the next relevant timing boundary.

[Timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Archive verification](archive-receipt.json) · [Patch and checks](procedures.tar.xz).

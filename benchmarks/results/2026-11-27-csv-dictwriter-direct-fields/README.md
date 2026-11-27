# B1870 Read ordinary CSV dictionary fields without a call packet

`DictWriter` uses the existing direct dictionary-get entry when an exact
string key and a concrete dictionary can complete synchronously. It shares
the managed interpreter frame and exception translation used by executable
calls. The ordinary path avoids binding `dict.get`, packaging two arguments
and starting an async field lookup. The general mapping path still resolves
`get` for each field and can suspend in lookup or equality.

The reference is `6e59cd41e`. Only the core DLL changes in the frozen 32-file
images. Both match the three CPython 3.13.2 output and file goldens for the
2,048-row dictionary re-export, plain writer report and file-fed map. Each
Windows Release target and Ubuntu WSL .NET 10 pass 510 public and 162
white-box checks. A new CPython golden covers live row and default changes
during a formatting callback. The async equality regression verifies both
successful suspension and cancellation with read cleanup, no partial row
and a reusable allowance.

## Allocation and admission

Windows Release .NET 11, median CLR bytes per command. Every run restores
the allowance to its compilation baseline. Retained compilation matches.

| Workload | Sync CLR bytes | Async CLR bytes | Peak shared bytes in both APIs |
| --- | ---: | ---: | ---: |
| Dictionary report | 3,129,438 → 1,424,972 | 3,129,686 → 1,424,656 | 2,316,075 → 2,316,075 |
| Plain writer | 1,538,880 → 1,538,880 | 1,539,214 → 1,539,208 | 3,104,123 → 3,104,123 |
| File-fed map | 808,768 → 808,768 | 808,984 → 808,984 | 2,270,071 → 2,270,071 |

## Local warm timing

Ubuntu WSL with .NET 11 RC3, one logical CPU and two workers per image.
Workers warm for five seconds and supply seven rotated batches. Intervals
span all pairings of worker medians. Elapsed includes mediated RAM-backed I/O,
output and file verification and previous-result disposal. Process CPU also
includes fixture reset and request framing. Both clocks exclude startup and
compilation. All 112 initial batches verify.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Dictionary report | Sync | −38.90% to −36.49% | −37.82% to −33.31% |
| Plain writer | Async | −0.20% to +2.12% | −6.27% to +0.99% |
| Dictionary report | Async | −53.60% to −36.74% | −54.82% to −35.68% |
| File-fed map | Async | +2.25% to +16.55% | −8.24% to +13.10% |

Initial async reference CPU spread reaches 29.78% on the report and
18.75% on the map. The related uncertainty warrants a same-image repeat of
those two lanes in reverse order.

| Workload | API | Repeat elapsed change | Repeat process CPU change |
| --- | --- | ---: | ---: |
| File-fed map | Async | −20.67% to +36.21% | +11.36% to +59.86% |
| Dictionary report | Async | −40.53% to −35.95% | −42.86% to −35.48% |

The repeat verifies all 56 batches. The file-fed map has up to 50.79%
same-image elapsed spread and 31.89% same-image CPU spread. Keep the
direct path for the eliminated call work, 54.47–54.48% lower managed
allocation at the same peak, and lower report CPU in both blocks.
The adverse map CPU on repeat is unresolved. Keep it visible and recheck
the exact pair at a quiet boundary. These measurements are local.

[Initial timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Patch and checks](procedures.tar.xz) · [Repeat timing](repeat/summary.json) ·
[Repeat observations](repeat/observations.tar.xz) · [Repeat checks](repeat/procedures.tar.xz).

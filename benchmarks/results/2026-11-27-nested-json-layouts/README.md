# B1840 Reuse nested JSON record layouts by depth

The single JSON record-layout hint alternates between a parent and its child
when their field names differ. Each mismatch can rebuild a private key table.
This change keeps the existing funded layouts in four inline slots, one for
each of the first three container depths and one shared by deeper containers.
Each record still owns its mutable values. Sync and async reads use the same
construction loop and preserve the snapshot used by a suspended hook.

The new regression fails on `f48ab7583` in both APIs when it checks that two
children reuse the same key. It passes with the patch and checks that modifying
one child leaves the other child unchanged and disposing the owner restores its
allowance. Windows .NET 11 and Ubuntu WSL .NET 10 each pass 925 public and 289
white-box Release checks. Four complete scripts match independent CPython
3.13.2 output and file goldens in both frozen images. Only the core DLL changes
between those images.

## Allocation and admission

Windows Release .NET 11, median CLR bytes per invocation. Peak shared admission
is the same across APIs for each image. Every invocation returns the allowance
to its compilation baseline. The input sizes describe the JSON payload.

| Workload | JSON input | CLR bytes, sync | CLR bytes, async | Peak shared bytes |
| --- | ---: | ---: | ---: | ---: |
| Nested document | 2,048 records, 103,331 B | 1,962,480 → 1,127,352 | 1,962,696 → 1,127,440 | 4,247,714 → 2,855,754 |
| Nested Lines | 4,096 records, 159,744 B | 3,715,192 → 2,044,424 | 3,715,582 → 2,044,870 | 8,221,333 → 5,444,923 |
| Flat Lines control | | 1,370,808 → 1,370,856 | 1,371,472 → 1,371,528 | 3,297,534 → 3,297,534 |
| File-fed map control | | 808,608 → 808,600 | 808,886 → 808,984 | 2,270,071 → 2,270,071 |

## Local warm timing

Two workers per image run on one logical CPU in local Ubuntu WSL .NET 11.
Each warms for five seconds and contributes seven rotated batches. Ranges
include every pairing of worker medians. Elapsed includes mediated I/O, output
verification and prior-result disposal. Process CPU also includes fixture reset
and framing. Both exclude startup and compilation. All 112 batches verify.

| Workload | Lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Flat Lines control | Warm async | −0.97% to +1.38% | −2.15% to +3.06% |
| Nested document | Warm sync | −32.32% to −25.36% | −34.82% to −25.64% |
| File-fed map control | Warm async | −4.82% to +3.53% | −3.69% to +1.38% |
| Nested Lines | Warm async | −25.60% to +14.42% | −32.75% to −10.93% |

Keep the layout reuse. Both nested inputs use 42–45% less CLR allocation and
32–34% less peak admission. Every nested-document comparison uses less CPU
and elapsed time in this local run. Nested Lines elapsed spans zero and the
reference workers differ by 32.21% in CPU. Carry that spread into the next
quiet comparison. CPython verifies semantics here. The timing comparison is
against the prior Lython build.

[Image identities](identity.json) · [Fixture sizes](fixture-census.json) ·
[Allocation and admission](allocation-summary.json) · [Timing summary](summary.json) ·
[Raw observations](observations.tar.xz) · [Archive verification](archive-receipt.json) ·
[Exact patch, scripts and checks](procedures.tar.xz).

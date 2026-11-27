# B1831 Admit ordinary list construction directly

On `a768ff8d7`, ordinary list construction reserves its known initial
capacity directly and commits to the list when backing allocation succeeds.
Failure releases the pending charge. This removes the per-list temporary
reservation object. Empty JSON arrays, object-pairs lists and zero-argument
`list()` enter the capacity constructor directly. Existing backing growth
continues to admit replacement overlap.

Both Release targets pass 1,255 focused checks. Two complete scripts match
independent CPython 3.13.2 output and report-file goldens. The nested-array
variant of the existing JSON Lines summary stores amounts as two-element
intervals and computes the difference. Both inputs have 4,096 actual data
records. The nested-array input is 131,826 bytes and the flat input is
110,592 bytes. The inherited catalog size field is 1,024.

## Allocation and admission

Windows .NET 11 estimates median CLR allocations from complete executions.
Execution, result handling and disposal return the allowance on every run.

| Workload | API | CLR bytes, reference → candidate | Peak admitted bytes, both |
| --- | --- | ---: | ---: |
| Nested-array Lines | sync | 2,518,934 → 2,387,868 | 6,214,167 |
| Flat Lines control | sync | 1,371,262 → 1,371,256 | 3,297,534 |
| Nested-array Lines | async | 2,519,150 → 2,387,998 | 6,214,167 |
| Flat Lines control | async | 1,371,478 → 1,371,472 | 3,297,534 |

## Local timing

Two frozen workers per source warm for five seconds on one logical Ubuntu WSL
.NET 11 CPU and contribute seven rotated batches. Ranges cover worker-median
pairs. Warm elapsed includes mediated I/O, verification and prior-result
disposal. Process CPU also includes fixture reset and framing. Compilation
and startup sit outside the timed lanes. Only the core DLL changes.

| Workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Nested-array Lines | warm | −2.92% to +2.34% | −5.79% to +2.41% |
| Flat Lines control | warm-async | −5.28% to +22.50% | +3.09% to +15.84% |
| Nested-array Lines | warm-async | +1.45% to +15.38% | +5.78% to +12.50% |

The nested input allocates about 128 KiB less in both APIs, or 5.20–5.21%,
at the same admitted peak. Flat control allocation differs by six bytes
per execution. Nested sync timing spans zero. The initial nested async
lane uses 5.78–12.50% more CPU, and the flat control also uses more CPU.
A shorter reversed-order repeat uses the same frozen images and two async
lanes. It reverses the nested CPU direction, while the flat control's
same-source CPU spread reaches 37.57%. Retain direct admission and treat
whole-script timing as inconclusive. The repeat verifies all 56 batches
and archives the observations after joining workers and removing fixtures.

| Repeated workload | API | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Nested-array Lines | warm-async | −8.83% to +0.12% | −4.11% to −0.38% |
| Flat Lines control | warm-async | −15.08% to +27.53% | −15.48% to +24.74% |

[Repeat summaries](repeat/summary.json) · [Repeat observations](repeat/observations.tar.xz).

All 84 timed batches verify. Workers join, guest fixtures are removed and
the persistent archive verifies.

[Images](identity.json) · [Fixture census](field-census.json) ·
[Allocation](allocation-summary.json) · [Medians and spreads](summary.json) ·
[Raw observations](observations.tar.xz) · [Archive receipt](archive-receipt.json) ·
[Exact patches and checks](procedures.tar.xz).

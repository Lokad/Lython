# B1841 Store the sole regex range in the match

Matches for patterns without capture groups store their group-zero byte and
scalar range in the match object. The projection fills the final storage in
place. Matches with capture groups retain their array and the same admitted
size. Both forms retain the original subject, so later match access has its own
immutable ranges even after the cursor advances or the pattern cache is purged.
The provider contract and its cursor scratch stay the same. Only the core DLL
changes from `3cb9a1240`. Both images use the pinned PythonRe 0.4.0 adapter.

The first build put an inline range on every match. The single-range extraction
saved 16,368 CLR bytes, and the named-capture control allocated 10,912 more.
The second build puts the inline field only on matches without groups. It
preserves the original size for grouped matches.

Windows .NET 11 and Ubuntu WSL .NET 10 each pass 707 public and 102 white-box
Release checks. A new public case checks Unicode windows, adjacent empty and
nonempty matches, retained registers, and one-shot search in both API modes.
The grouped and ungrouped extraction scripts read the same 1,024 records
(23,014 UTF-8 bytes) and produce 682 matches each. Both scripts and the file-fed
map control match independent CPython 3.13.2 output and file goldens in both
frozen images.

## Allocation and admission

Windows Release .NET 11 median CLR bytes per invocation. Every invocation
returns the allowance to its compilation baseline. Peak admission is the same
across APIs for each image.

| Workload | CLR bytes, sync | CLR bytes, async | Peak shared bytes |
| --- | ---: | ---: | ---: |
| Whole-match extraction | 397,104 → 375,280 | 397,320 → 375,496 | 145,518 → 123,694 |
| Named-capture control | 480,904 → 480,904 | 481,120 → 481,120 | 168,179 → 168,179 |
| File-fed map control | 808,768 → 808,768 | 808,984 → 808,984 | 2,270,071 → 2,270,071 |

The whole-match extraction saves 32 CLR and 32 peak shared bytes per match.

## Local warm timing

Two workers per image run on one logical CPU in local Ubuntu WSL .NET 11.
Each warms for five seconds and contributes seven rotated batches. Ranges span
all pairings of worker medians. Warm elapsed includes mediated I/O, verification
and prior-result disposal. Process CPU also includes fixture reset and framing.
Startup and compilation sit outside both measurements. All 112 batches verify.

| Workload | Lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| File-fed map control | Warm async | +0.99% to +9.64% | +2.26% to +11.06% |
| Whole-match extraction | Warm sync | −1.23% to +3.44% | −1.71% to +2.60% |
| Named-capture control | Warm sync | −9.87% to +1.43% | −7.92% to −4.31% |
| Whole-match extraction | Warm async | +1.89% to +3.44% | −2.40% to +2.63% |

Keep the smaller match storage. Whole-match CPU spans zero in both APIs.
The async elapsed increase is a local observation and the unchanged file-fed
map varies more. Carry the adverse async observation into the next relevant
quiet comparison. Released-provider qualification remains separate.

[First layout allocation](initial-allocation-summary.json) ·
[Final allocation and admission](allocation-summary.json) ·
[Fixture sizes](fixture-census.json) · [Image identities](identity.json) ·
[Timing summary](summary.json) · [Raw observations](observations.tar.xz) ·
[Archive verification](archive-receipt.json) · [Patches and checks](procedures.tar.xz).

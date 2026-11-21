# B1272 pinned UTF-8 casing prototype

Lython `8f6c22584` versus `406388271`, with the reference source
retained at `6e972677e`. Measured on November 21, 2026 UTC on Azure
F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. The prototype replaces
platform scalar casing, the later-Unicode partner filter, titlecase
exceptions and the separate full expansion lookup. It copies the
chosen UTF-8 mapping or original scalar bytes into governed output.
Contextual sigma and the shared ASCII writer still apply.

| Complete warm text report | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Original mixed ASCII/accented/emoji, 35,840 B | −12.53 to −3.21% | −12.02 to −2.90% |
| Mixed Unicode, 107,520 B | −12.43 to −7.70% | −11.81 to −7.39% |
| ASCII control, 35,840 B | −4.70 to −2.71% | −5.42 to −1.55% |

Each report reads 3,072 physical lines, counts words and writes a
sorted report. Ranges cover all pairings of two independently warmed
process medians per source. Each process contributes seven rotated
batches. Negative changes indicate faster runs. The original
reference control spread is 9.21%, with all pairings favorable.
The mixed Unicode control spreads are 2.01% and 3.33%. There is
no corresponding ASCII mapping change, so the measured ASCII
improvement has uncertain attribution.

Elapsed includes mediated RAM-backed I/O, output verification and
prior-result cleanup. Process CPU also includes fixture reset and
request processing. Startup and final teardown are excluded.
All 84 batches pass in 278 seconds with both quiet gates, all worker
exits, fixture cleanup and SHA-verified persistent observations.

Windows .NET 11 and Ubuntu .NET 10 each pass 564 focused Release
checks. The Windows scalar audit matches CPython 3.13.2 for every
non-ASCII scalar across lower, upper, title and swapcase. Before the
prototype it found 49 differing method results. Both complete
32-file images pass all three CPython goldens. Local CLR allocation
is effectively flat, and accounted peaks and budget reuse match.
The prototype adds 32 KiB to the Windows Release DLL. Cold impact
was outside this comparison.

The follow-up adds casefold to the same pipeline and removes the
standalone folding builder. Use that version's measured source
for the retention decision.

[Timing summary](summary.json) · [Allocation](allocation-summary.json)
· [Raw observations](observations.tgz) · [Exact source and checks](checks-and-procedures.tar.xz).

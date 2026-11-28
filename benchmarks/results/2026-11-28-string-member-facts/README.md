# B1965 Share string member facts across frontend queries

The frontend uses one table for string callable and return metadata.
It serves both `str` instances and the `str` type. The 60 callable rows
and 43 return rows combine into 61 entries. The `format` row keeps its
return-only fact. General callable and return initializers omit the old
duplicated text data.

The frozen images use `ad1bfd7c7` and the candidate patch. Only the core
DLL differs. The images agree on 57,408 queries per surface, including
962 callable matches and 596 return matches. Twenty-one compiler cases
preserve validity and diagnostics, including receiver forms, arguments,
and downstream return facts. Both images also pass three CPython 3.13.16
output and file goldens. Windows .NET 10 and 11 and Ubuntu WSL .NET 11
each pass 502 public and 545 white-box focused Release checks.

## First compilation

Each probe uses a fresh Windows process. The caller-thread allocation
clock spans the first compile call. Seven reference and seven candidate
samples per workload alternate order. Admitted storage matches and
compiled-owner disposal returns allowance to zero in all 28 samples.

| Workload | CLR bytes, reference → candidate | First compile elapsed medians |
| --- | ---: | ---: |
| Configuration edit | 547,672 → 484,888 B | 107.31 → 106.00 ms |
| File search, match at line 1 | 507,328 → 464,200 B | 115.85 → 116.93 ms |

The allocation decrease is 11.46% for configuration and 8.50% for file
search. These probes leave first-compile elapsed inconclusive.

## Warm compilation

November 28, 2026 UTC on Ubuntu WSL and .NET 11 RC3. Two workers per
image warm for at least five seconds each and supply seven rotated
batches. Elapsed includes compilation from resident source and the
previous compiled owner's disposal. Process CPU also includes framing.
No guest execution occurs in the timed lanes. Ranges span worker median
pairings.

| Workload | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Configuration edit | −11.89% to +18.25% | −10.19% to +12.51% |
| File search, match at line 1 | −3.76% to −1.01% | −1.09% to +0.79% |
| Generated guarded report control | −1.56% to −0.66% | −0.72% to +2.69% |

Configuration CPU worker spread is about 12% for each image. All warm
CPU intervals span zero. Retain the smaller and later-initialized
metadata, the warm ranges and the unresolved configuration timing.
All 84 batches verify. Workers join, temporary fixtures are removed
and the persistent archive verifies.

[Cold allocation](cold-allocation.json) · [Warm timings](summary.json) ·
[Worker identity](identity.json) · [Observations](observations.tar.xz) ·
[Procedures and parity checks](procedures.tar.xz).

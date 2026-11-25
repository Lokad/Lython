# B1734 Share UTF-8 storage for accepted regex literals

The compiler reuses the key's immutable UTF-8 backing in an eligible
executable literal. The two string objects retain separate identities.
Only accepted direct literals with a surviving compiled-program owner
reuse the backing. Program selection remains keyed by the effective
options, and the owner keeps the bytes through import or script execution.

| Compiled workload | Reference allocation | Initial allocation | Refined allocation | Refined bytes saved |
| --- | ---: | ---: | ---: | ---: |
| Regex literal, 128 scalars | 236,632 B | 236,384 B | 236,288 B | 344 B |
| Regex literal, 4,096 scalars | 5,975,680 B | 5,967,496 B | 5,967,400 B | 8,280 B |
| Configuration manifest, small | 72,776 B | 72,896 B | 72,320 B | 456 B |
| Generated ordinary definitions | 1,444,680 B | 1,444,688 B | 1,435,040 B | 9,640 B |
| Shipment callbacks | 152,192 B | — | 151,152 B | 1,040 B |

The initial representation adds one reference to every string literal.
The refinement moves the existing comprehension scope field from the
common expression base into the four comprehension forms. It also
replaces the temporary item arrays in lexical preparation with direct
visits in the existing order. The initial and refined probes independently
confirm sharing of the 256 B and 8,192 B arrays. Retained admission
matches for each case, and disposing compilation returns the allowance
to baseline.

The reference production source is `a420c3e05`. The recorded patches
identify both candidates. Their 32-file images differ from the reference
only in the core DLL. Local Windows .NET 11 RC3 results are medians of
eleven compilation-and-disposal allocation samples after six warmups.
The first four-case verification stopped on a diagnostic fixture size
mismatch. The corrected four-case catalog and the refined five-case
catalog both match independent CPython 3.13.2 output and file checks
on their exact reference and candidate images.

The initial variant passes 827 focused Release tests per platform.
The refinement passes 1,630 per platform on Windows .NET 11 and Ubuntu
.NET 10, including admitted storage, cancellation, imports, lexical
scope, and generator checks. The comparison measures compilation
allocation before any matching or guest execution.

[Initial identity](initial-identity.json) · [Refined identity](refined-identity.json) ·
[Allocation summary](allocation-summary.json) · [Raw observations and source patches](observations.tar.xz).

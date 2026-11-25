# Return released construction capacity to the shared allowance

The private 0.5.0 bridge consolidates producer claims in one reservation
per owner. When the producer releases construction storage but keeps its
compiled program, that reservation can retain its peak. The refinement
resizes it once after construction and before cache publication. It keeps
the producer's live program claim and the separate adapter metadata charge.

The Windows .NET 11 probe counts the immutable producer's own outstanding
claims and compares two bridge images using the same 0.5.0 assemblies
from `61228b20f`. Only the optional bridge differs. The first rebuild
resolved both adapter references to the same assembly. The recorded
comparison copies and hashes the complete images before and after each probe.

| Pattern shape | Producer peak | Producer live after construction | Bridge retained before | Bridge retained after |
| --- | ---: | ---: | ---: | ---: |
| Literal | 29,696 B | 29,696 B | 30,021 B | 30,021 B |
| Named log groups | 56,760 B | 42,560 B | 57,245 B | 43,045 B |
| Unicode word and lookahead | 35,296 B | 35,296 B | 35,698 B | 35,698 B |
| 64 capture groups | 237,704 B | 195,616 B | 242,376 B | 200,288 B |

After the first match the existing bridge retains a small reusable allowance
in some live cursors. It returns to baseline when enumeration closes. The
refinement leaves that cursor behavior intact and refunds 14,200 B for the
named log program and 42,088 B for the wide program. The probe verifies
successful captures and full refunds after disposing both owners. Windows
.NET 11 and Ubuntu .NET 10 each pass the 159 focused ownership, cancellation
and compatibility checks. The change remains in the development bridge.

[Original claims](original.json) · [Refined claims](trimmed.json) ·
[Complete images](image-files.json) · [Source and procedures](procedures.tar.xz).

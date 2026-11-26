# B1761 CSV physical-line scan source gate

A separate Windows .NET 11 Release build of `bc93d4d74` counts native CSV
routes on the existing benchmark scripts. Both hosting APIs produce the same
counts and match independent output and file goldens. Host allowance returns
to baseline after each result and compiled owner are disposed.

| Workload | Buffered line searches | Borrowed lines | Completed by literal field scan | Bytes in those literal records |
| --- | ---: | ---: | ---: | ---: |
| Shipment callback report | 2,048 | 2,047 | 2,047 | 27,314 |
| File-fed integer quantity map | 2,048 | 2,047 | 2,047 | 27,314 |
| CSV meter with a float field helper | 4,096 | 4,088 | 4,088 | 138,972 |
| Quote-heavy CSV policy invoice | 4,096 | 4,087 | 3,064 | 123,581 |

The native file path locates the complete physical line before parsing its
fields. Every borrowed line in these runs uses universal newline handling
and a dialect without CSV numeric coercion. The literal field scanner
already identifies a line terminator in these completed records. The quoted
invoice uses general field parsing on 1,023 of its borrowed lines. Counts
measure source routes and bytes. They do not estimate removable CPU.

Test passing the decoded window to the existing literal scanner so it can
locate the terminator once. Advance physical position before admission or
row publication. Keep multiline, transformed fields, explicit newline
modes, numeric conversion and partial windows on their existing behavior.
A candidate must preserve CR/LF/CRLF at window boundaries, strict errors and
reentry, cancellation and ownership cleanup. The quote-heavy control checks
whether speculative work on fallback rows offsets savings. No production
candidate or timing is included in this gate.
[Earlier CPU attribution](../2026-11-26-remaining-shipment-cpu/README.md) ·
[Exact external checkpoint](../2026-11-26-record-field-cpython/README.md).

[Route counts](routes.json) · [Identity](identity.json) ·
[Probe and fixture procedures](procedures.tar.xz).

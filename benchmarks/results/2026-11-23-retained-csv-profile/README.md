# B1464 CPU attribution after the retained CSV changes

On November 23, 2026 UTC, the quiet Azure F4as_v7 sampled production
`f9c476e27` using the archived B1463 image built at `6aee32253`.
The full 32-file image verified on Ubuntu 24.04.4 and .NET 11 RC3.
Eight seconds of guest warmup per script precede a five-second perf
CPU-clock/frame-pointer capture at 199 Hz. The JIT map is a diagnostic
override. Both complete scripts match CPython's stdout and file
goldens, and all profiled batches verify.

| Resolved runtime stack observations | Typed ASCII meter | Quote-heavy invoice |
| --- | ---: | ---: |
| Resolved runtime ancestry | 984 | 1,047 |
| Row source or parser ancestry | 280 | 153 |
| Literal descriptor scan | 149 | 38 |
| Numeric parsing | 85 | 0 |
| Final row or slot publication | 56 | 43 |
| Dictionary reader and row adoption | 0 | 197 |
| Function entry and slot binding | 0 | 366 |
| Admission ancestry | 42 | 42 |

The meter records 75 leaves directly in .NET UTF-8 or floating-point
parsing helpers. The literal scanner is its largest resolved parser
leaf. On the invoice, dictionary and function handling have wider
ancestry. Categories overlap and include useful work. The 104 and 92
unknown leaves, inlining and incomplete stacks limit attribution.
The traces provide no source-pair timing or new CPython ratio.

The next candidate shares JSON's exact small-decimal quotient with
the existing UTF-8 float parser, replacing general-number parsing for
short decimal tokens. CSV and builtin float keep their full grammar
and fallback. Compare the two typed reports, explicit float
conversion, JSON and the quoted invoice. This leaves numeric row
boxing for a separate question. [Earlier slot trial](../2026-11-22-csv-numeric-slot-ingress/README.md).

All workers and profilers join, guest fixtures are removed and the
persistent archive verifies under the exclusive lease. The meter
verifies 1,664 invocations during capture. The invoice verifies 768.
Worker batch clocks include execution, applicable mediated RAM-file
I/O, result verification and prior-result disposal. Thread samples
also capture wider worker activity.

[Identity](identity.json) · [Attribution](attribution.json) ·
[Raw profiles and exact fixtures](observations.tar.xz) ·
[Procedures](procedures.tar.xz) · [Terminal status](terminal-status.json).

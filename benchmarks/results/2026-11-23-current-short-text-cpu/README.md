# B1540 attribute CPU after the cased-split changes

Both profiles use `43688c753`, the same 3,072-line input and the same
report. In the inline version the caller computes the width and performs
the same casing and split expression. This is a source-boundary
diagnostic. It makes no new elapsed comparison.

| Nearest resolved runtime group | Representative, 1,152 samples | Inline, 1,109 samples |
| --- | ---: | ---: |
| Entry and operand movement | 102 | 1 |
| Native instruction dispatch | 296 | 115 |
| Word decoding and casing | 170 | 231 |
| Lookup and accumulation | 146 | 204 |
| Admission or retirement | 83 | 105 |
| Host I/O | 68 | 94 |

The representative completes 4,608 invocations and the inline form
6,400. An inclusive entry classification observes 598 representative
stacks, including useful work done below entry. Among the nearest methods,
the representative records 119 samples in the native instruction runner,
72 in its call handler, 35 in the native starter and 29 in `StartInline`.
The difference identifies a place to investigate. It cannot attribute all
inclusive entry time to removable overhead. The source shows that native
entry initializes bookkeeping for an activation transfer even when the
synchronous path completes without one.

On the quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3, each worker
warms for eight guest seconds. The collector uses a six-second CPU
window at 199 Hz with frame-pointer unwinding and JIT symbol maps.
There are 1,229 and 1,214 all-thread samples, with 154 and 175
unmapped leaves. Whole-request stacks include resets and verification.
Both golden checks, the frozen image, quiet gates, joined cleanup and
the persistent archive verify. The lease ended on November 23, 2026 UTC.

[Attribution](attribution.json) · [Image and workload](identity.json) ·
[Raw profiles](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal receipt](terminal-status.json) ·
[Earlier controlled source comparison](../2026-11-23-short-text-boundaries/cpu.md).

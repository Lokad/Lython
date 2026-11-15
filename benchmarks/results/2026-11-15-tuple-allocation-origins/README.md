# Tuple and scalar construction origins

2026-11-15. Current qualified runtime b58e2dc4, frozen f304b3db. Local Windows
11/.NET 11 RC3, normal workstation GC/tiering. This is allocation attribution,
not a timing comparison, exact type-byte accounting or a retained-heap profile.

The unchanged complete invoice and JSON scripts each receive four guest seconds
of warmup and 128 captured invocations. Compilation precedes capture; execution,
mediated local file I/O, validation and result cleanup remain in the captured
request. Before/after goldens, worker Closed/exit, trace joins and cleanup pass.

| Script | Allocation ticks | Resolved production stacks | Other/unresolved |
| --- | ---: | ---: | ---: |
| CSV invoice | 5,506 | 5,504 | 2 |
| JSON summary | 2,406 | 2,394 | 12 |

Invoice tuple construction triggers 863 BigInteger, 86 object-array, 194 tuple
and 119 temporary-reservation samples. Other leading origins include CSV field
strings (1,473) and native instruction-loop integer boxes (645). These are sample
trigger counts, **not allocated-byte or CPU percentages**; inlining can hide
intermediate frames. JSON instead leads with strings, record dictionaries and
their backing arrays. Event loss is not inferred absent.

This supports assessing end-to-end integer tuple storage and operand transport,
rather than another inline-header optimization. Merely postponing boxing to the
next subscript would not eliminate the work. The [earlier storage-only prototype](../2026-11-12-inline-tuple-prototype/README.md) remains withdrawn: its larger
header and extra branches had no reliable targeted CPU benefit. Its evidence is
not superseded by this capture.

Any selected migration must preserve stable item identity, aliases, exact integer
promotion, generic tuple behavior, cancellation and funding through observation
and cleanup. Reference-heavy text/JSON are required controls. No runtime change
or speedup follows from this attribution alone.

[Exact image/catalog identity](identity.json), [invoice origins](invoice/origins.json),
[JSON origins](json-control/origins.json), [cleanup receipt](receipt.json),
[raw EventPipe streams and reused collector/reader](traces-and-procedure.tar.xz).
The initial setup failed on a stale output-directory name before launching users.
The final cleanup assertion found compiler analyzer shadow copies after all guest
and trace users joined; the compiler server was shut down, contents recorded and
the owned cache removed. Both conditions remain in the archived procedure/receipt.
No capture was repeated to correct either condition. Diagnostics dependencies
remain outside the runtime; no collection or heap/root walk was requested.

# B1564 compiled-pattern receiver calls

Direct positional calls to the eight compiled-pattern methods now borrow
the evaluated operands. They skip the bound callable, captured delegate
and bound argument array. A saved method uses the existing bound-receiver
wrapper and exposes its pattern as `__self__`. Receiver binding preserves
omission across sparse keywords. Pattern methods share one range reader.

| Complete warm script | Elapsed change | Process CPU change | CLR allocation change |
| --- | ---: | ---: | ---: |
| Compiled pattern, 3,072 log lines | −6.87 to −3.63% | −5.52 to −3.42% | −4.18% |
| Saved bound search, same log | −0.09 to +1.30% | 0.00 to +0.85% | −0.35% |
| Module search, same log | −1.07 to +0.11% | −0.90 to +1.08% | 0.00% |
| Quoted invoice | −0.96 to +0.45% | −0.72 to +0.72% | −392 bytes |

All admitted peaks match. Every observed allowance returns to
baseline. The saved method uses a bound argument array; its
allocation savings are smaller. Control elapsed ranges overlap zero.
This measures core receiver dispatch through the same optional
PythonRe 0.4.0 adapter. It does not qualify a producer upgrade.

The frozen reference `5d54f7afb` matches the runtime and tests of
`566259ebc`. Candidate `81ce6201e` differs only in the core DLL
within the verified 32-file images. The quiet Azure F4as_v7 ran Ubuntu
24.04.4 and .NET 11 RC3 on November 24, 2026 UTC. Two workers per
source warm for eight seconds and contribute seven rotated samples.
Ranges span all pairings of their medians. Warm elapsed includes
mediated RAM-file I/O, verification and prior-result disposal.
Process CPU also includes reset and request overhead. Compilation,
startup and final teardown are outside warm elapsed. Both quiet
gates, all 112 batches, worker joins, fixture cleanup and persistent
archive verification passed.

The first local manifest lacked the empty output-file map required
for read-only fixtures. The first verified VM staging needed 98,304
more bytes of persistent space. Both stopped before timing. The final
collection reused the unchanged workload and checks after correcting
that manifest and retiring a verified redundant archived binary.

Windows and Ubuntu each pass 688 white-box and 2,895 public .NET 11
Release checks. The tests cover explicit None, omitted keywords,
saved methods across cache purge, original windows, asynchronous
replacement callbacks, provider cancellation, refusal and shared
ownership. Sparse string keywords also preserve `rsplit(maxsplit=...)`
and enable the default encoding for `str.encode(errors=...)`.

[Timing](summary.json) · [Allocations](allocation-summary.json) ·
[Image identity](identity.json) · [Raw observations](observations.tar.xz) ·
[Procedures, failures and exact patch](procedures.tar.xz) · [Terminal receipt](terminal-status.json).

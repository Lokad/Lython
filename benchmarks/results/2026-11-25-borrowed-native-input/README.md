# B1703 Borrow subscript arguments in native calls

The candidate lets unary native prefixes keep a materialized argument
outside their writable slots when the compiled tape reads that slot
only as a subscript receiver. If execution needs a continuation, it
places the argument directly in the funded frame before any suspension.
The native runner and its managed scratch and stack admission still
serve both routes.

An instrumented Windows build logs 4,096 borrowed inputs on invoice
and 2,048 on file-fed map for each hosting API. Both formerly copied
each argument into a mutable slot. The candidate copies zero and
transfers zero continuations on those scripts. Other instrumented
shapes take no borrows. Managed allocation is effectively flat, all
six admitted peaks match, and the shared allowance returns to
baseline.

The frozen images compare committed `701f0b1e5` and the private
candidate on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3
on November 25, 2026 UTC. Two processes per source warm for five
seconds each and run seven rotated batches. Ranges span all pairings
of process medians. Warm includes applicable mediated RAM-backed I/O,
output and file verification, and previous-result disposal. Through
result adds compilation and compiled-owner disposal. Wider CPU also
includes fixture reset and framing. Process startup and final teardown
stay outside both clocks.

| Complete script and boundary | Elapsed change | Wider process CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Quoted CSV invoice, warm sync | −4.28 to −0.22% | −4.80 to −1.67% | 2.72 / 1.48% |
| File-fed map, warm async | −4.85 to −0.09% | −4.41 to +1.91% | 1.53 / 3.43% |
| Shipment callbacks, warm async | −4.81 to +0.44% | −4.37 to 0.00% | 4.20 / 1.26% |
| File-fed map, warm sync | −3.00 to +1.42% | −2.84 to +0.81% | 3.29 / 1.22% |
| Ordinary definitions, through result | −0.38 to +0.50% | −1.97 to +1.05% | 0.59 / 0.30% |
| Defaulted rules, through result | −0.15 to +3.87% | +0.59 to +4.13% | 2.59 / 1.40% |

Invoice has lower measured CPU. Both file-fed map CPU ranges span
zero, and defaulted rules take more CPU despite taking no borrows.
The candidate includes a compiler proof walk over potential code
objects. These observations do not locate the rule regression.
The [gated follow-up](gated/README.md) preserves the copy reduction
and shifts the observed warm gains between workloads. Both sources
stay private.

Windows .NET 11 and Ubuntu .NET 10 each pass 657 white-box and 2,804
public focused checks. A delayed subscript can transition from a
borrowed input to a materialized frame, both directly and through
`map`. It checks alias identity, two ordered host reads, both file
closes, and allowance recovery. Initial test builds omitted linked
benchmark sources and representative CSV fixtures from the sparse
checkout. The archived failures precede the successful build after
restoring both sets. Both frozen images match five CPython goldens.
All 168 timed batches, both quiet machine gates, worker joins, fixture
retirement and under-lease archive verification complete.

[Source and images](identity.json) · [Timings](quiet-summary.json) ·
[Routes](routes.json) · [Allocation and admission](allocation-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal receipt](quiet-terminal-status.json) ·
[Builds and procedures](procedures.tar.xz).

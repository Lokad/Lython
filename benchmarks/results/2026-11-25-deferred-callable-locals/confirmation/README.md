# B1735c Deferred callable construction with shared literal defaults

This refinement also stages eligible definitions whose positional default
tuple already belongs to compilation. It leaves that tuple with its original
owner. The factory marks 93 of its 96 definitions and runs 16 times. A separate
route probe counts 1,488 staged and retired definitions with no materialization
on this script, in each hosting mode. The observer calling an alias of `locals()`
materializes both pending definitions before the call.

| Workload | Phase | Elapsed change | Process CPU change | Same-source elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Complete ordinary definitions | Warm sync | −17.26% to −15.50% | −18.19% to −14.80% | 1.72% / 0.40% |
| Complete ordinary definitions | Warm async | −17.09% to −15.20% | −15.55% to −14.08% | 1.11% / 1.15% |
| Complete ordinary definitions | Compile-to-result | −7.75% to −5.03% | −6.03% to −2.83% | 2.64% / 0.30% |
| Escaping defaulted rules | Warm sync | −6.19% to −2.43% | −7.84% to −1.37% | 3.22% / 0.77% |
| Shipment callback report | Warm async | −2.97% to +0.87% | −3.19% to +0.54% | 1.91% / 2.01% |

The reference is `35eace33b`. The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and
.NET 11 RC3 on November 25, 2026 UTC. Two processes per frozen image and phase
each used seven seconds of warmup followed by seven rotated batches. Ranges
span all four pairings of process medians. Negative values mean the candidate
uses less time. Execution includes verification, result cleanup and the shipment
script's RAM-backed fixture reads and writes. The combined phase also includes
compilation and its cleanup. Process CPU covers the wider request with framing
and fixture resets.

The local ordinary-definition run allocates 23,224 B, down from 200,296 B, and
peaks at 92,760 B admitted, down from 971,730 B. The extra staging slots raise
the factory's value capacity from 101 to 194 slots and compile allocation by
2,792 B. Retained compiler admission matches. The escaping rules and shipment
show matching execution allocation and admission and take no new staging route.
The observer allocates 32 B more and peaks 48 B higher. Under a close allowance,
the larger initial workspace can be refused before later savings materialize.

Windows .NET 11 and Ubuntu .NET 10 each pass 286 focused Release checks.
Four independent CPython goldens pass for each frozen image. Both APIs preserve
lookup through the aliased builtin, default evaluation failure, and return of
the shared allowance after cleanup. All quiet workers joined and both archives
were verified.

[Initial block](../README.md) · [Identity](identity.json) · [Shape](shape-summary.json) ·
[Routes](routes.json) · [Execution allocation](allocation-summary.json) ·
[Compilation allocation](compile-allocation-summary.json) ·
[Timing summary](quiet-summary.json) · [Raw observations](quiet-observations.tar.xz) ·
[Terminal receipt](quiet-terminal-status.json) · [Procedures](procedures.tar.xz).

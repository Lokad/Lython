# B1166 method-format continuation

Private `8131df7a8` replaces the per-invocation renderer object with immutable
inputs in the render continuation. Expanded spec text has a lease that ends
after the consuming field, including when a selector or guest format callback
suspends. The `07a88e0ed` reference retains one growable expanded-spec lease
for the invocation.

| Workload | Boundary | Windows warm allocation | Quiet elapsed change | Quiet process CPU change |
| --- | --- | ---: | ---: | ---: |
| Shipment selectors | Warm | −8.85% | −3.40% to +1.61% | −3.33% to +1.96% |
| Shipment selectors | Compile to result | — | −2.77% to −0.91% | −3.09% to −0.39% |
| Expanded report forwarding | Warm | +0.01% | −6.22% to +1.01% | −5.74% to +1.00% |
| Named formatting | Warm | −10.73% | −3.22% to −2.47% | −5.71% to −3.33% |
| Two nested widths | Warm | −5.42% | +1.53% to +5.99% | +2.21% to +4.64% |
| Positional invoice control | Warm | Flat | −3.39% to +0.75% | −4.54% to +2.00% |

Negative values favor the candidate. Quiet rows span the four candidate
and reference process median pairings on Azure F4as_v7 with Ubuntu 24.04.4
and .NET 11 RC3, 2026-11-20 UTC. Warm elapsed includes mediated I/O, golden
checks and prior-result disposal. Compile-to-result adds compilation and
code-owner disposal. Process CPU covers the wider sampled invocation loop.
The unchanged invoice moves by pairing. All 120 batches passed golden
checks, both machine gates passed, and workers and fixture storage were
released before the collection ended.

Both complete images matched five CPython goldens. Windows .NET 11 and
Ubuntu .NET 10 each passed 848 focused checks, including nested specs held
through successful callbacks, cancellation and refusal. Local accounted
peaks matched except for nested formatting, which fell by 120 bytes. The
borrowed allowance returned to baseline in every local workload.

Keep the combined change private. Named formatting improves with lower CPU.
Every nested pairing uses more CPU, and shipment warm spans zero. The two-
width case exercises two admissions and releases where the reference reuses
one lease. This run cannot isolate that cost from changes to continuation
state. A successor needs to separate those costs or remove another stage
of nested formatting before a new comparison.

[Pairing summary](summary.json) · [Raw observations](observations.tgz) ·
[Source, checks and procedures](checks-and-procedures.tar.xz) ·
[Verified archive receipt](archive-receipt.json).

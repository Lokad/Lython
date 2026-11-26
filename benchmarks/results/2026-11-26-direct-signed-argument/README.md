# B1775 Direct transport of a computed integer argument

The patch against `088cf9d37` passes the computed machine integer directly
to the existing closed-return evaluator. It removes construction of the
one-element operand span and the return evaluator's read of that slot.
The current target is still resolved before evaluating the argument. Exact
arithmetic can decline to the ordinary tape. Both managed entry checks and
the normal fallback remain in place.

A separately instrumented build takes the new route 2,560 times per run in
both the complete factory and the defaults-only diagnostic, in either hosting
mode. Escaping rules and shipment take zero. The four output and file goldens
match CPython on both frozen builds. Allocations and peak admission match on
the affected cases. The shipment allocation diagnostic varies by 174 B.
Focused Release checks pass on Windows .NET 11 and Ubuntu WSL .NET 10.
They cover target rebinding across host I/O, overflow, suspension and allowance cleanup.
[Route counts](routes.json) · [Allocation](allocation-summary.json) ·
[Test logs, patches and provenance](procedures.tar.xz)

The first block uses local Ubuntu WSL .NET 11 RC3 on November 26, 2026 UTC.
Two workers per source and lane warm for three seconds each before seven
rotated batches targeting 360 ms per request. The ranges span all four
pairings of process medians. Negative changes mean lower time. Warm elapsed
includes applicable RAM-backed I/O, output verification and cleanup. Wider
CPU also covers request framing and fixture reset. [Medians and control spreads](summary.json)

| Workload | Lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Shipment control | Warm async | +4.73% to +112.72% | −5.00% to +10.34% |
| Ordinary definitions | Warm async | −27.91% to +14.38% | −24.54% to +11.58% |
| Escaping rules control | Warm sync | −8.96% to +26.55% | −7.89% to +26.44% |
| Defaults-only diagnostic | Warm sync | −22.35% to −5.03% | −7.94% to −3.96% |
| Ordinary definitions | Warm sync | −17.61% to −5.28% | −18.16% to +1.35% |

The defaults-only CPU result justifies a bounded confirmation of the complete
script. The shipment candidate workers differ by 101% in elapsed and the
complete factory reference workers by 35% in async CPU. This block leaves the
whole-script timing inconclusive. [Raw observations](observations.tar.xz) ·
[Frozen image identities](identity.json) · [Terminal receipt](archive-receipt.json)

The quiet confirmation supports keeping the direct path. It resolves the
ordinary sync comparison and confirms lower defaults-only CPU. The async
reference and unchanged controls retain measurable variation. [Quiet comparison](confirmation/README.md)

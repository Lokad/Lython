# B1283 direct admission for function attribute overrides

This refinement removes the temporary reservation object on the first
standard metadata write. A normal function reads the code object's name
and docstring directly. A function that overrides those fields retains
one admitted record, separate from the guest-visible `__dict__`.
Subsequent writes reuse the admitted record. A refused first write is
terminal and returns the allowance through normal cleanup.

Reference `a3d0bfc34` uses the exact production-equivalent `da805131a`
image. The candidate is `a887d3e8a`. The collection ran on November 21,
2026 UTC on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3.
Two processes per source warmed for eight seconds or more per lane and
contributed seven rotated batches each. Ranges compare all four pairs
of process medians. Warm elapsed covers mediated RAM-backed I/O,
golden verification and prior-result disposal. Compile to result adds
compilation and code-owner disposal. Process CPU covers the full
request. Startup and final execution cleanup are outside the guest clocks.

| Complete script | Boundary | Elapsed change | CPU change | Local warm CLR allocation |
| --- | --- | ---: | ---: | ---: |
| Postponed nested factories | Warm | −2.12% to +3.34% | −3.10% to +1.80% | −7.71% |
| Postponed nested factories | Compile to result | −8.00% to −0.31% | −6.49% to +0.38% | — |
| Eager nested factories | Warm | −3.99% to +2.36% | −4.93% to +1.61% | −7.07% |
| Factory with wrapper metadata writes | Warm | −0.76% to +3.34% | −0.71% to +2.74% | +0.69% |
| Keyword dataclasses | Warm | −2.80% to +0.13% | −3.24% to +1.50% | +0.00% |

Both 32-file images reproduce six CPython workload goldens. Each platform
passes the 138 focused checks for first-write refusal, later-write reuse,
deletion, wrapper behavior and cleanup. The local six-script allocation
diagnostic returns all allowances to baseline. The wrapper allocation
increase falls from 1.60% in B1282 to 0.69%. Its accounted peak remains
131,072 bytes above the reference. All other accounted peaks are equal.

Retain the refined layout. The three complete factory scripts allocate 5.28–7.71% less. Postponed compile to result is 0.31–8.00% sooner, with a 5.98% reference control spread and mixed CPU. B1282 also measured an elapsed gain at that boundary. Warm factory CPU and elapsed span zero, as do the wrapper and dataclass controls. The wrapper's extra CLR allocation and accounted capacity remain real costs when metadata is written. Preserve B1282's adverse timing and reassess if this cost recurs on a representative wrapper-heavy script.

All 140 timed batches passed. The workers joined and persistent
observations verified under the lease with both quiet gates and all RAM
fixtures removed.

[Timing](summary.json) · [Allocation](allocation-summary.json)
· [Raw observations](observations.tgz)
· [Source, checks and procedures](checks-and-procedures.tar.xz)
· [B1282 first layout](../2026-11-21-callable-standard-overrides/README.md).

# B1474 Resolve live root-module names through the existing global path

Retained `123b2c894` is source-equivalent to frozen candidate `ff8f370a2`.
The compiler routes root-module names through the existing live global
and builtin lookup once it has ruled out a local, capture or special scope.
Runtime lookup uses the current binding and preserves the evaluated
call target. The general resolver still serves class bodies and
comprehension or enclosing namespace cases.

Compared with `fed081ff0`, the explicit CSV module replaces ten
`LoadName` instructions and the typed module replaces seven.
The configuration script replaces four. Those three instruction
counts stay the same. Forwarding removes three additional instructions
from the shadowing test. The six non-module or unaffected code objects
match. The test verifies rebinding during argument evaluation, deletion
and restoration of a builtin, and class-scope lookup. CPython produces
the same output.

Windows .NET 11 and Ubuntu .NET 10 each passed 189 white-box and 1,121
public focused Release checks. Windows also passed 165 regex provider,
lifetime, analysis and adapter checks. Both frozen images matched all
four CPython fixture goldens. All four local compile allocations and
admitted peaks match. Managed execution allocations either match or
differ by 56 bytes, and execution peaks match.

## Controlled comparison

The same .NET 11 RC3 worker and optional adapter ran on the dedicated
Azure F4as_v7 with Ubuntu 24.04.4 on November 23, 2026 UTC. Two processes
per image warmed for eight seconds, then each contributed seven rotated
batches. Warm elapsed includes applicable mediated RAM-file I/O,
goldens and prior-result disposal. Time to result includes compilation,
execution and code-owner disposal. Wider CPU includes reset and protocol.
Startup and final teardown are separate.

| Workload | Boundary | Elapsed change | Wider process CPU change |
| --- | --- | ---: | ---: |
| Explicit-float CSV | Warm | −3.01% to −1.33% | −3.88% to −1.96% |
| Explicit-float CSV | Time to result | −3.02% to +0.92% | −1.02% to +1.43% |
| Typed ASCII CSV | Warm | −2.47% to +0.90% | −2.74% to 0.00% |
| Configuration edit | Time to result | −0.91% to +0.98% | −2.21% to +2.94% |
| Short nonlocal text | Warm | −4.70% to −3.04% | −4.66% to −3.48% |

All 140 timed batches pass. Ranges cover the four pairings of worker
medians. The two lower warm elapsed ranges have supporting process
CPU. The time-to-result and typed CSV ranges remain inconclusive.
The independent CPython checkpoint retains its original revision.

Both quiet checks pass. All workers join, the RAM fixtures are removed,
and the persistent archive verifies under the exclusive lease. Staging
retired only the older private B1472 working binary after verifying
its complete preserved copy and observations.

[Frozen identity](identity.json) · [Full timing summary](summary.json) ·
[Executable parity](executable-parity.json) · [Compile allocations](compile-allocations.json) ·
[Runtime allocations](allocation-summary-six.json) · [Raw observations](observations.tar.xz) ·
[Procedures and validation](procedures.tar.xz) · [Terminal status](terminal-status.json).

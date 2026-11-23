# B1502 Isolate admission for prepared eager annotations

`b32bf6b6b` retains the candidate source `8b79aef66`. A separate method admits
the eager annotation backing after defaults, runs the existing prepared live
reads, and calls the shared definition constructor. It refunds on failure.
The shared computed-default path carries only its existing default leases.
General annotation expressions and overloaded or wide arithmetic use their
ordinary executable activation. Compiler inspection finds one additional
eligible eager body, with identical tapes and operand capacities.

Compared with `a61b79041` using the immutable B1500 candidate image. The quiet
Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 23, 2026 UTC.
Both full 32-file images verify. Two independent processes per source warm
for eight seconds and supply seven rotated batches. Ranges pair their
medians. Warm elapsed includes applicable mediated RAM-file I/O, output
verification and prior-result disposal. Compile to result adds compilation
and code-owner disposal. Wider CPU includes request reset and protocol.
Startup and final teardown stay outside elapsed.

| Workload | Phase | Elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Eager nested factories | Warm | −19.97 to −16.96% | −19.41 to −17.97% | 2.30 / 1.43% |
| Postponed nested factories | Warm | −11.34 to −2.58% | −13.13 to −0.35% | 8.18 / 1.57% |
| Factories observing defaults | Warm | −1.44 to +0.62% | −0.72 to +2.87% | 1.44 / 0.65% |
| Fully supplied helper | Warm | −1.14 to +0.52% | −2.19 to +0.95% | 1.29 / 0.39% |
| Quote-heavy invoice | Warm | −0.99 to +1.09% | −0.73 to +1.79% | 0.98 / 1.11% |
| Eager nested factories | Compile to result | −22.58 to −17.52% | −22.56 to −16.80% | 5.75 / 0.74% |

The eager gains support retention. The postponed slowdown from B1501 does
not recur. Its reference spread leaves the size of any improvement uncertain.
Quoted elapsed spans zero in this comparison. Earlier quoted-control debt
remains recorded at its measured revisions. In a FullOpts Windows JIT
diagnostic, the shared constructor's first stack subtraction is 248 B and
emitted code is 938 B. Those figures were 280 B and 1,066 B in B1501 and
240 B and 887 B in B1500. The diagnostic describes the Windows shape.

Windows .NET 11 and Ubuntu .NET 10 each pass 138 white-box and 982 public
focused Release checks, including eager evaluation order, live global
rebinding, ownership, refusal and unwinding. Both images match five
workload goldens and 13 independent eager CPython cases. Reported execution
peaks match across the five workloads. Local allocation matches for
postponed and quoted scripts and differs by 120 B on the other three.
All 168 timed batches verify in 281 seconds. Workers join, guest fixtures
are removed, and the persistent archive verifies under the quiet lease.

[Identity](identity.json) · [Timing](summary.json) · [Allocation](allocation-summary-six.json) ·
[Selected body](selected-returns.json) · [JIT diagnostic](jit-frame-summary.json) ·
[Raw observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Checks and procedures](procedures.tar.xz) ·
[Exact reference core](../2026-11-23-direct-computed-definition/README.md).

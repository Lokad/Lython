# B1623 Forward positional operands through native calls

The candidate extends existing operand forwarding to adjacent local
target and argument reads whose result feeds another expression or
unpack. Both drivers pass a captured argument to native invocation
without staging it in the caller's operand stack. Ordinary dispatch
retains the evaluated target and argument before it can transfer or
suspend the call. A prepared native child has its own funded storage.
The existing augmented integer update retains its operand proof.

The defaulted-rule module goes from 32 to 26 instructions. Its three
calls per group each lose two local loads, corresponding to 12,288
loads across 2,048 groups. The three target bodies retain their native
eligibility. The eager-rule tape and compiler metadata match in the
detailed comparison. This distinguishes work removed from
runtime timing changes when the compiler emits the same tape.

## Quiet comparison

The reference source is `48790b2c7`, whose runtime matches the sealed
B1621 candidate. Both 32-file images verify the same six complete
CPython goldens. The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11
RC3 on November 24, 2026 UTC. Two warmed workers per image supply seven
rotated batches. Ranges span every reference/candidate worker pairing.
Warm elapsed includes applicable mediated RAM-backed I/O, verification
and previous-result cleanup. Compile to result also includes compilation
and compiled-owner disposal. Wider CPU includes fixture reset and
protocol. Process startup remains outside the clocks.

| Complete script and boundary | Elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Defaulted rules, warm | −10.10 to +0.90% | −10.75 to −3.99% | 7.85 / 4.06% |
| Defaulted rules, compile to result | −0.19 to +1.22% | +0.00 to +2.27% | 0.15 / 1.26% |
| Eager annotated rules, warm | +0.31 to +4.31% | +2.05 to +4.35% | 1.80 / 2.14% |
| Postponed annotated rules, warm | −6.69 to +6.13% | −7.40 to +4.95% | 8.47 / 4.86% |
| Captured rules, warm | −8.10 to −2.87% | −8.97 to −4.28% | 3.43 / 2.18% |
| Short Unicode report, warm | −8.97 to −5.51% | −8.77 to −4.73% | 0.01 / 3.79% |
| Quoted CSV invoice, warm | −2.54 to −0.93% | −2.38 to +3.57% | 1.18 / 0.46% |

Captured rules and the Unicode report improve on both clocks. Defaulted
warm CPU falls, while elapsed crosses zero in moving workers and
compile-to-result shows little change. The eager workload has a
consistent CPU cost with the same tape. Postponed timings are uncertain.
Admitted peaks match, and warm allocations match except for variations
of at most 60 B. These are Lython-to-Lython timings.

Windows and Ubuntu each pass 5,301 focused .NET 11 Release checks. The
Windows retry puts the pinned SDK on PATH and rebuilds the subprocess
probe. Its initial three probe failures could not launch .NET 11 and
remain archived. Both images pass 15 CPython 3.13.2 probes in each
execution mode. These cover unpublished scalar identity, missing local
order, callable objects, bound methods, exceptions and generators.
Suspended native child coverage checks that its arguments survive and
that cancellation restores the invocation-owned storage. All 196 timed
batches and quiet gates pass. Both sets of workers join, fixture cleanup
finishes, and the raw archive verifies.

This candidate stays private. The retained refinement reuses the framed
call entry and carries the eager CPU cost into the next checkpoint.
[Framed entry](framed-entry/README.md).

[Identity](identity.json) · [Timings](quiet-summary.json) ·
[Tape census](call-tape-summary.json) · [Allocation](allocation-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Procedures and patch](procedures.tar.xz) ·
[Terminal receipt](quiet-terminal-status.json).

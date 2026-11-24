# B1583 Complete prepared fixed tuple returns in one pass

Private `a88ca826e` generalizes the existing prepared integer tuple
return to canonical objects in uncaptured locals. Both drivers tally
scalar admission while preparing those slots and complete the fixed
return without redispatching its tuple instruction. Dynamic arithmetic,
unproven reads and escaping tuples still use the original tape.
Admission precedes publication. Caller and callee cleanup keep their
existing owners.

A separate instrumented run finds prepared returns for `price_line`
on both builds and adds `make_rules` on the defaulted script in the
candidate. Captured rules, eager annotations and short text never use
this route. Counts include all diagnostic iterations and cannot
compare throughput. In a separate Windows x64 FullOpts JIT diagnostic,
the native dispatch method grows from 5,670 to 5,955 bytes. The framed
dispatch method grows from 12,620 to 12,732 bytes. This identifies a
shared code-size change to isolate. It does not establish the cause
of any elapsed result.

## Complete-script timing

The quiet Azure F4as_v7 used Ubuntu 24.04.4 and .NET 11 RC3 on
November 24, 2026 UTC. Compared `b8c2aa8c8` and `a88ca826e` with two
workers per revision. Only the core DLL differs across 32 verified
image files. All five CPython 3.13.16 goldens pass. CPython supplies
expected behavior in this experiment.

| Warm script | Elapsed change | Wider process CPU change | Elapsed worker spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Captured rules | −2.79 to +2.76% | −2.13 to +4.20% | 3.47 / 2.16% |
| Defaulted rules | −3.06 to +1.18% | −1.97 to +0.87% | 2.21 / 2.11% |
| Eager annotated rules | −8.10 to +3.03% | −8.97 to +3.42% | 5.84 / 5.92% |
| Short text report | −2.00 to +7.32% | −1.55 to +8.19% | 6.02 / 3.28% |
| Quoted CSV invoice | −4.74 to +4.11% | −2.60 to +2.63% | 4.92 / 4.17% |

Workers warm for eight seconds and contribute seven rotated batches.
Ranges cover all pairings of steady worker medians. The warm clock
includes mediated RAM-backed I/O where applicable, verification and
prior-result disposal. Wider CPU includes fixture reset and protocol.
Compilation, startup and final teardown are outside these clocks.

Allocation medians and admitted peaks match on all five scripts. Every
elapsed and CPU range crosses zero. Keep this source private. The
short report has an adverse pairing and variable same-source workers.
The next source comparison can isolate shared dispatch growth, with
these results retained.

Windows and Ubuntu each pass 117 white-box and 495 public focused
Release checks on .NET 11. A new fixture checks callable and list
identity through fixed unpack and an escaping tuple. Existing checks
cover scalar refusal, delayed cleanup, arbitrary-width promotion and
the shared allowance. Both quiet gates, 140 timed batches, worker joins,
guest fixture cleanup and persistent archive verification pass.

[Timing and spread](summary.json) · [Allocation](allocation-summary.json) ·
[Prepared-route counts](prepared-routes.json) · [Exact images](identity.json) ·
[Raw observations](observations.tar.xz) · [Procedures and JIT](procedures.tar.xz) ·
[Terminal receipt](terminal-status.json).

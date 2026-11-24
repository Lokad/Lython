# B1622 One constructor for closed function definitions

The prototype routes a single computed definition and each member of a
closed pair through the same default-store loop and callable constructor.
The compiler already proves both shapes with the same instruction scan.
The candidate removes 16 net lines of runtime code. It keeps the default
backing and callable admissions at their original boundaries.

The failure-order checks explain why merging those admissions is a
separate change. An allowance can refuse the default backing before any
arithmetic. If that grant succeeds, division by zero stops the call before
a separately refused callable admission. For a valid argument, the first
refusal remains attributed to the lambda's creation. Both APIs return
the allowance after cleanup. The defaulted-rule workload also includes
`index + 1`, which reaches the existing exact integer promotion path at
the upper machine bound. Eager annotation lookups can fail or observe
live globals. Reserving the callable earlier would move its host-visible
admission ahead of these operations.

## Controlled comparison

The reference is `bfae5253b`. Both sealed 32-file images verify the same
four complete CPython goldens. The quiet Azure F4as_v7 ran Ubuntu 24.04.4
and .NET 11 RC3 on November 24, 2026 UTC. Two workers per image warm for
five seconds each and supply seven rotated batches. Ranges cover all four
pairings of their medians. Warm elapsed includes applicable mediated RAM
I/O, verification and prior-result cleanup. Compile to result also
includes compilation and compiled-owner disposal. Wider CPU includes
fixture reset and request framing. Process startup is outside the clocks.

| Complete script and boundary | Elapsed change | Wider process CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Defaulted rules, warm | −13.35 to +2.10% | −13.09 to +1.07% | 9.83 / 7.29% |
| Defaulted rules, compile to result | −6.42 to +3.54% | −6.12 to +8.06% | 9.78 / 0.79% |
| Eager annotated rules, warm | +5.66 to +8.87% | +6.20 to +13.36% | 0.39 / 2.64% |
| Postponed annotated rules, warm | +4.92 to +11.24% | +5.75 to +11.34% | 4.72 / 1.24% |
| Quoted CSV invoice, warm | −2.41 to +0.40% | −2.27 to +1.27% | 2.23 / 0.64% |

Managed allocation and peak admission match on all four scripts. Worker
variation leaves the defaulted result inconclusive. Both affected
annotation scripts consistently use more CPU and elapsed time. The
reference single-definition producer has a fixed two-default sequence.
The candidate repeats layout and store selection inside a general loop.
The trial measures the whole change and does not isolate those branches'
individual costs. Keep the prototype private and retain the admission
order regression.

Windows and Ubuntu each pass 2,452 focused .NET 11 Release checks. Both
images match CPython 3.13.2 on eight probes in each execution mode. These
cover exact promotion, default identity, mutation, fixed captured
defaults, eager lookup errors and postponed annotations. Every allowance
in the allocation checks returns to baseline. All 140 batches and both
quiet gates pass, the workers join and the fixture mount is cleaned up.
The original observations verify against the archive receipt. After
restoring the production paths and rebuilding from scratch, both
platforms pass all 21 closed definition pair checks with the new test.

The sealed reference tape also shows two local-load instructions before
each of the three calls in a 2,048-group defaulted-rule loop. None of
those sites uses existing operand forwarding. This establishes a
candidate for removing operand staging and gives no timing estimate.
The call sites and their consumers are recorded in the tape census.

[Identity](identity.json) · [Timings](quiet-summary.json) ·
[Allocation](allocation-summary.json) · [Tape census](call-operand-tape.json) ·
[Observations](quiet-observations.tar.xz) ·
[Procedures and patch](procedures.tar.xz) · [Terminal receipt](quiet-terminal-status.json).

# B1501 Share eager annotation admission with the direct factory

`594e1331f` extends the computed-default factory to its existing prepared
eager annotation reads. It runs those reads after both defaults, feeds their
already-admitted backing to the same lexical constructor and unwinds both
owners if a read or creation fails. General annotations and overloaded or
wide arithmetic keep the executable activation. Compiler inspection selects
the eager factory alone relative to B1500, with unchanged tapes and operand
capacities. A JIT diagnostic runs the direct constructor on the eager-only
fixture.

Compared with `a61b79041` using the verified B1500 candidate. The quiet
Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 23, 2026 UTC.
Both full 32-file images verify. Two independent processes per source
warm for eight seconds and supply seven rotated batches. Ranges pair
their medians. Warm elapsed includes applicable mediated RAM-file I/O,
output verification and prior-result disposal. Compile to result adds
compilation and code-owner disposal. Wider CPU includes request reset
and protocol. Startup and final teardown stay outside elapsed.

| Workload | Phase | Elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Eager nested factories | Warm | −21.67 to −17.78% | −22.12 to −17.41% | 1.19 / 3.74% |
| Postponed nested factories | Warm | +4.31 to +10.40% | +5.30 to +10.23% | 4.28 / 1.50% |
| Factories observing defaults | Warm | −1.96 to +2.10% | +0.38 to +2.53% | 1.31 / 2.80% |
| Fully supplied helper | Warm | −0.65 to +1.33% | −2.01 to −0.61% | 1.68 / 0.31% |
| Quote-heavy invoice | Warm | +1.16 to +4.15% | −0.69 to +5.26% | 2.13 / 0.81% |
| Eager nested factories | Compile to result | −18.41 to −13.48% | −17.68 to −14.31% | 3.62 / 2.34% |

Keep this variant private. The eager factory improves in both phases.
The postponed control slows with supporting CPU, and quoted elapsed rises.
In the tiering-disabled Windows diagnostic, the shared constructor's first
stack subtraction grows from 240 to 280 B and emitted code from 887 to
1,066 B. This is a hypothesis for a bounded refinement. Move the eager
reservation and refund out of the shared body, then compare the same pair
of factories and controls. All 168 timed batches verify. Workers join,
guest fixtures are removed, and the persistent archive verifies under the
quiet lease.

Windows .NET 11 and Ubuntu .NET 10 each pass 138 white-box and 982 public
focused Release checks. The new cases verify synchronous and asynchronous
eager reads after defaults, live global rebinding, retained annotation
identity, missing-name recovery and shared-budget reuse. They also pass on
the reference. Both images match five workload goldens and 13 separate
CPython cases with eager annotations. Factory and supplied allocations
match, the quote control differs by 126 B, and all reported execution peaks
match. The first stage used the wrong reference directory and never
started the worker. Its byte-verified upload was retired under the lease
before the corrected staging.

[Identity](identity.json) · [Timing](summary.json) · [Allocation](allocation-summary-six.json) ·
[Selected body](selected-returns.json) · [Raw observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Checks and procedures](procedures.tar.xz) ·
[Exact reference core](../2026-11-23-direct-computed-definition/README.md).

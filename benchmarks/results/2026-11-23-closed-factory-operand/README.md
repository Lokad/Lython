# B1529 forward private integer operands through closed factory calls

Retained `b04e371c0` reads the existing local slot for a computed-definition
factory. Its exact arithmetic result flows through the existing call update
when argument binding reads the owned defaults without observation or
callbacks. Generic calls still have their canonical arguments. The factory
and its defaults keep their own identities, preflight and cleanup. The
reference is `23756d1ea`. The measured `25252f96c` runtime and tests match
the retained source.

| Complete script and phase | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Postponed nested factories, warm | −24.12% to −18.07% | −25.45% to −17.84% |
| Postponed nested factories, compile to result | −27.61% to −19.64% | −27.31% to −18.76% |
| Eager nested factories, warm | −15.54% to −10.52% | −14.84% to −11.30% |
| Factories reading default metadata, warm | −2.22% to +3.06% | −1.69% to +2.08% |
| Fully supplied arithmetic, warm | −1.11% to −1.07% | −1.34% to +1.04% |
| Quote-heavy invoice, warm | −2.49% to +1.49% | −4.12% to 0.00% |

Changes compare all four pairings of process medians. Negative values mean
lower cost. Postponed warm reference workers differ by 7.39%. Postponed
compile-to-result candidate workers differ by 7.81%. Eager warm candidate
spread is 5.24%. The affected ranges and CPU support the direction while
leaving the exact magnitude uncertain. The previous deferred-tuple
prototype had a consistent adverse observer result. These observer
elapsed and CPU ranges span zero. [B1432](../2026-11-23-unboxed-singleton-default/README.md).

Both factory reports allocate 131,040 fewer CLR bytes per Windows
diagnostic invocation, about 17.1%. The difference matches eliminating
one loop-argument box on every iteration and one accumulator box on all
but the first. Each target's executable module replaces two local loads
with direct call operands. Its existing update entry now accepts exact
arguments with prepared defaults. The three control allocations are
identical. All five admitted peaks match and all budgets return to baseline.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 23,
2026 UTC. Two workers per image warmed for eight seconds each and
contributed seven rotated batches. Warm elapsed includes execution,
applicable RAM-backed I/O, output verification and prior-result disposal.
Time to result adds compilation and code disposal. CPU covers the whole
request. Both images match five CPython goldens. All 168 timed batches,
both quiet gates, complete image hashes, worker joins, fixture cleanup and
the persistent archive verify.

Windows .NET 11 and Ubuntu .NET 10 each passed 742 public and 955 white-box
focused Release tests. A new two-mode test verifies changing defaults,
retained metadata aliases, ordinary argument identity, one asynchronous
protocol read and shared-budget cleanup. The initial sparse checkout
omitted twelve tests' representative script fixtures. Restoring the
tracked fixtures and rebuilding clears those failures.

[Timings](summary.json) · [Identity](identity.json) · [Tape](tape-proof.json) ·
[Allocation](allocation-summary-six.json) · [Observations](observations.tar.xz) ·
[Procedures](procedures.tar.xz) · [Terminal receipt](terminal-status.json).
